using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Duel rules, effect resolution and the deterministic opponent AI.
public partial class CardGame
{
    Side player, enemy;
    bool playerTurn, busy, duelOver, duelWon, lowSaid;
    int forgeCharges;
    string lastAction = "";
    float endT;

    static readonly HashSet<string> Picks = new() { "pick_enemy", "pick_minion", "pick_enemy_minion" };

    void StartDuel()
    {
        player = new Side { isPlayer = true, hp = 30, maxHp = 30 };
        enemy = new Side { hp = Boss.hp, maxHp = Boss.hp };
        player.foe = enemy; enemy.foe = player;
        player.deck = Shuffle(playerDeck);
        enemy.deck = Shuffle(Boss.deck.Select(id => cardsById[id]));
        // guarantee a forged card or a Legal Loophole shows up early so the Forge is part of every duel
        var star = player.deck.FindIndex(c => c.forged || c.id == "loophole");
        if (star > 3) { var c = player.deck[star]; player.deck.RemoveAt(star); player.deck.Insert(UnityEngine.Random.Range(0, 4), c); }
        for (int i = 0; i < 3; i++) { Draw(player, true); Draw(enemy, true); }
        Draw(enemy, true);
        forgeCharges = 1; duelOver = false; duelWon = false; lowSaid = false; busy = true; lastAction = "";
        parts.Clear(); floats.Clear();
        mode = Mode.Duel;
        StartCoroutine(BeginTurn(player));
    }

    static List<CardDef> Shuffle(IEnumerable<CardDef> src)
    {
        var l = src.ToList();
        for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); }
        return l;
    }

    void Draw(Side s, bool silent = false)
    {
        if (s.deck.Count == 0)
        {
            s.fatigue++;
            Float(HeroCenter(s), "Out of evidence! -" + s.fatigue, new Color(1, 0.4f, 0.3f));
            DamageHero(s, s.fatigue);
            return;
        }
        var c = s.deck[0];
        s.deck.RemoveAt(0);
        if (s.hand.Count >= 10) { Float(HeroCenter(s), c.name + " burned!", Color.gray); return; }
        s.hand.Add(c);
        if (s.isPlayer && !silent) Sfx("card", 1.2f);
    }

    static WaitForSeconds Wait(float t) => new(t);

    IEnumerator BeginTurn(Side s)
    {
        busy = true;
        playerTurn = s.isPlayer;
        if (s.isPlayer) statTurns++;
        s.maxMana = Mathf.Min(10, s.maxMana + 1);
        s.mana = s.maxMana;
        foreach (var u in s.board) { u.sleeping = false; u.attacked = false; }
        Banner(s.isPlayer ? "YOUR TURN" : Boss.name.ToUpper() + "'S TURN", s.isPlayer ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.35f, 0.3f));
        Sfx("whoosh", s.isPlayer ? 1.1f : 0.8f);
        yield return Wait(0.6f);
        Draw(s);
        foreach (var u in s.board.ToList())
            foreach (var f in u.fx.Where(f => f.when == "turn").ToList())
                if (s.board.Contains(u)) yield return Resolve(f, s, u, null, u.def.vfx, u.pos);
        yield return Cleanup();
        if (duelOver) yield break;
        if (s.isPlayer) busy = false;
        else yield return AiTurn();
    }

    void EndTurn()
    {
        if (busy || !playerTurn || duelOver || forgeOpen) return;
        StartCoroutine(BeginTurn(enemy));
    }

    Unit MakeUnit(CardDef c, Side s) => new()
    {
        def = c, atk = c.atk, hp = c.hp, maxHp = c.hp, owner = s, pop = 1,
        taunt = c.kw.Contains("taunt"), charge = c.kw.Contains("charge"), shield = c.kw.Contains("shield"),
        sleeping = !c.kw.Contains("charge"), fx = new List<Fx>(c.fx)
    };

    static Fx PickFx(CardDef c) => c.IsWitness ? null : c.fx.FirstOrDefault(f => Picks.Contains(f.tgt));

    bool ValidPick(string tgt, Side s, Target t)
    {
        if (t == null) return false;
        return tgt switch
        {
            "pick_enemy" => t.hero == s.foe || (t.unit != null && t.unit.owner == s.foe),
            "pick_minion" => t.unit != null,
            "pick_enemy_minion" => t.unit != null && t.unit.owner == s.foe,
            _ => false
        };
    }

    bool HasAnyPick(string tgt, Side s) => tgt switch
    {
        "pick_enemy" => true,
        "pick_minion" => s.board.Count + s.foe.board.Count > 0,
        _ => s.foe.board.Count > 0
    };

    IEnumerator PlayCard(Side s, CardDef c, Target t, int slot)
    {
        busy = true;
        s.mana -= c.cost;
        s.hand.Remove(c);
        Sfx("card", 0.9f);
        if (s.isPlayer) { lastAction = $"Rookie played '{c.name}': {c.text}"; statCards++; }
        bool big = c.forged || (!c.IsWitness && c.cost >= 3);
        if (!s.isPlayer) { showEnemyCard = c; showEnemyT = 0; yield return Wait(0.9f); }
        if (big)
        {
            Splash(c.forged ? "FORGED!" : "OBJECTION!", Col(c.vfx?.c1, Color.red));
            Shake(4 + (c.vfx?.shake ?? 0.5f) * 14);
            Sfx("boom", c.vfx?.pitch ?? 1f);
            yield return Wait(0.9f);
        }
        Vector2 origin = HeroCenter(s);
        Unit src = null;
        if (c.IsWitness && s.board.Count < 7)
        {
            src = MakeUnit(c, s);
            slot = Mathf.Clamp(slot, 0, s.board.Count);
            s.board.Insert(slot, src);
            src.pos = s.isPlayer ? dropPos : new Vector2(640, 40);
            origin = SlotPos(s, slot, s.board.Count);
            Emit(c.vfx, origin, origin, 0.7f);
            Sfx("hit", 1.4f);
            yield return Wait(0.35f);
        }
        int before = s.foe.hp;
        foreach (var f in c.fx.Where(f => f.when == "play").ToList())
        {
            yield return Resolve(f, s, src, t, c.vfx, src != null ? src.pos : origin);
            if (duelOver) yield break;
        }
        yield return Cleanup();
        if (s.isPlayer && before - enemy.hp >= 4 && !duelOver) Taunt("hit", lastAction);
        if (s.isPlayer && !duelOver) busy = false;
    }

    List<Target> Targets(string tgt, Side s, Unit src, Target picked)
    {
        var foe = s.foe;
        var r = new List<Target>();
        switch (tgt)
        {
            case "pick_enemy": case "pick_minion": case "pick_enemy_minion":
                if (picked != null && (picked.hero != null || picked.unit.hp > 0)) r.Add(picked);
                break;
            case "enemy_hero": r.Add(new Target { hero = foe }); break;
            case "own_hero": r.Add(new Target { hero = s }); break;
            case "random_enemy":
            {
                var all = foe.board.Select(u => new Target { unit = u }).Append(new Target { hero = foe }).ToList();
                r.Add(all[UnityEngine.Random.Range(0, all.Count)]);
                break;
            }
            case "random_enemy_minion":
                if (foe.board.Count > 0) r.Add(new Target { unit = foe.board[UnityEngine.Random.Range(0, foe.board.Count)] });
                break;
            case "all_enemy_minions": r.AddRange(foe.board.Select(u => new Target { unit = u })); break;
            case "all_enemies":
                r.AddRange(foe.board.Select(u => new Target { unit = u }));
                r.Add(new Target { hero = foe });
                break;
            case "all_minions": r.AddRange(foe.board.Concat(s.board).Where(u => u != src).Select(u => new Target { unit = u })); break;
            case "own_minions": r.AddRange(s.board.Where(u => u != src).Select(u => new Target { unit = u })); break;
            case "self": if (src != null && src.hp > 0) r.Add(new Target { unit = src }); break;
        }
        return r;
    }

    IEnumerator Resolve(Fx f, Side s, Unit src, Target picked, Vfx v, Vector2 from)
    {
        v ??= new Vfx { c1 = "#ffd166", c2 = "#ffffff", pattern = "burst", shake = 0.3f, pitch = 1 };
        var ts = Targets(f.tgt, s, src, picked);
        switch (f.act)
        {
            case "damage":
                foreach (var x in ts) Emit(v, from, TargetPos(x), 1f);
                Sfx("zap", v.pitch);
                Shake(v.shake * 8 + (ts.Count > 2 ? 6 : 0));
                yield return Wait(0.3f);
                foreach (var x in ts) Damage(x, f.n);
                break;
            case "heal":
                foreach (var x in ts)
                {
                    Emit(new Vfx { c1 = "#7CFC9A", c2 = "#ffffff", pattern = "spiral" }, TargetPos(x), TargetPos(x), 0.6f);
                    if (x.unit != null) x.unit.hp = Mathf.Min(x.unit.maxHp, x.unit.hp + f.n);
                    else x.hero.hp = Mathf.Min(x.hero.maxHp, x.hero.hp + f.n);
                    Float(TargetPos(x), "+" + f.n, new Color(0.4f, 1f, 0.5f));
                }
                Sfx("chime", 1.2f);
                break;
            case "draw":
                for (int i = 0; i < f.n; i++) { Draw(s); yield return Wait(0.15f); }
                break;
            case "buff":
                foreach (var x in ts.Where(x => x.unit != null))
                {
                    x.unit.atk += f.n; x.unit.hp += f.n; x.unit.maxHp += f.n; x.unit.pop = 0.6f;
                    Emit(v, x.unit.pos, x.unit.pos, 0.5f);
                    Float(x.unit.pos, $"+{f.n}/+{f.n}", new Color(0.5f, 1f, 0.5f));
                }
                Sfx("chime", 1.5f);
                break;
            case "summon":
                for (int i = 0; i < f.n && s.board.Count < 7; i++)
                {
                    var u = MakeUnit(content.token, s);
                    u.pos = from;
                    s.board.Add(u);
                    Emit(v, from, from, 0.4f);
                    Sfx("card", 1.5f);
                    yield return Wait(0.15f);
                }
                break;
            case "silence":
                foreach (var x in ts.Where(x => x.unit != null))
                {
                    x.unit.taunt = x.unit.shield = x.unit.charge = false;
                    x.unit.fx = new List<Fx>();
                    Emit(v, from, x.unit.pos, 0.6f);
                    Float(x.unit.pos, "INADMISSIBLE!", new Color(1f, 0.85f, 0.4f));
                }
                Sfx("zap", 0.6f);
                break;
            case "steal":
                foreach (var x in ts.Where(x => x.unit != null && x.unit.owner != s))
                {
                    Emit(v, from, x.unit.pos, 0.8f);
                    if (s.board.Count < 7)
                    {
                        x.unit.owner.board.Remove(x.unit);
                        s.board.Add(x.unit);
                        x.unit.owner = s; x.unit.sleeping = true;
                        Float(x.unit.pos, "POACHED!", new Color(1f, 0.5f, 1f));
                    }
                    else x.unit.hp = 0;
                }
                Sfx("whoosh", 1.3f);
                break;
            case "destroy":
                foreach (var x in ts.Where(x => x.unit != null))
                {
                    Emit(v, from, x.unit.pos, 1.2f);
                    x.unit.hp = 0;
                    Float(x.unit.pos, "DESTROYED!", new Color(1f, 0.3f, 0.3f));
                }
                Shake(10);
                Sfx("boom", 1.2f);
                break;
            case "forge":
                if (s.isPlayer)
                {
                    OpenForge(ForgeSource.Card);
                    while (forgeOpen) yield return null;
                }
                break;
        }
        yield return Wait(0.25f);
    }

    void Damage(Target x, int n)
    {
        if (n <= 0) return;
        if (x.unit != null)
        {
            var u = x.unit;
            if (u.shield) { u.shield = false; Float(u.pos, "Blocked!", new Color(0.5f, 0.9f, 1f)); Sfx("chime", 2f); return; }
            u.hp -= n; u.flash = 1; u.shake = 0.35f;
            Float(u.pos, "-" + n, new Color(1f, 0.35f, 0.3f));
        }
        else DamageHero(x.hero, n);
    }

    void DamageHero(Side s, int n)
    {
        s.hp -= n; s.heroFlash = 1; s.heroShake = 0.4f;
        Float(HeroCenter(s), "-" + n, new Color(1f, 0.3f, 0.25f));
        if (!s.isPlayer) statDamage += n;
        if (!s.isPlayer && !lowSaid && s.hp > 0 && s.hp <= s.maxHp * 0.4f) { lowSaid = true; Taunt("low", lastAction); }
    }

    IEnumerator Cleanup()
    {
        for (int guard = 0; guard < 20; guard++)
        {
            var dead = player.board.Concat(enemy.board).Where(u => u.hp <= 0).ToList();
            if (dead.Count == 0) break;
            foreach (var u in dead) { u.owner.board.Remove(u); Shatter(u); }
            Sfx("hit", 0.7f);
            yield return Wait(0.3f);
            foreach (var u in dead)
                foreach (var f in u.fx.Where(f => f.when == "death").ToList())
                    yield return Resolve(f, u.owner, u, null, u.def.vfx, u.pos);
        }
        CheckEnd();
    }

    void CheckEnd()
    {
        if (duelOver || (player.hp > 0 && enemy.hp > 0)) return;
        duelOver = true; busy = true; endT = 0;
        duelWon = enemy.hp <= 0;
        Shake(16);
        Sfx("boom", 0.6f);
        if (duelWon)
        {
            Splash("CASE WON!", new Color(1f, 0.8f, 0.2f));
            Emit(new Vfx { c1 = "#ffd166", c2 = "#ffffff", pattern = "confetti" }, new Vector2(640, 200), new Vector2(640, 200), 4f);
            Taunt("lose", "The rookie just WON the duel and destroyed your credibility. You lost. React dramatically.");
        }
        else
        {
            Splash("OVERRULED!", new Color(1f, 0.3f, 0.3f));
            Taunt("win", "You just won the duel against the rookie. Gloat.");
        }
    }

    bool CanAttack(Unit u) => !u.sleeping && !u.attacked && u.atk > 0;

    bool ValidAttack(Unit a, Target t)
    {
        if (t == null) return false;
        var foe = a.owner.foe;
        bool onFoe = t.hero == foe || (t.unit != null && t.unit.owner == foe);
        if (!onFoe) return false;
        if (foe.board.Any(u => u.taunt)) return t.unit != null && t.unit.taunt;
        return true;
    }

    IEnumerator Attack(Unit a, Target t)
    {
        busy = true;
        a.attacked = true;
        a.lunging = true;
        Vector2 start = a.pos, end = TargetPos(t);
        for (float k = 0; k < 1; k += Time.deltaTime / 0.2f) { a.pos = Vector2.Lerp(start, end, k * k); yield return null; }
        Sfx("hit", 1f);
        Shake(3 + a.atk * 1.2f);
        Emit(new Vfx { c1 = "#ffffff", c2 = "#ffd166", pattern = "burst" }, end, end, 0.5f + a.atk * 0.1f);
        int counter = t.unit != null ? t.unit.atk : 0;
        Damage(t, a.atk);
        if (counter > 0) Damage(new Target { unit = a }, counter);
        if (a.owner.isPlayer) lastAction = $"Rookie's {a.def.name} attacked {(t.unit != null ? t.unit.def.name : "you")}";
        for (float k = 0; k < 1; k += Time.deltaTime / 0.2f) { a.pos = Vector2.Lerp(end, start, k); yield return null; }
        a.lunging = false;
        yield return Cleanup();
        if (a.owner.isPlayer && !duelOver) busy = false;
    }

    // ---------- Opponent AI ----------
    IEnumerator AiTurn()
    {
        yield return Wait(0.5f);
        if (statTurns % 2 == 0 && !tauntBusy) Taunt("turn", "Your turn starts. Last thing the rookie did: " + lastAction);
        for (int guard = 0; guard < 12 && !duelOver; guard++)
        {
            bool played = false;
            foreach (var c in enemy.hand.Where(c => c.cost <= enemy.mana).OrderByDescending(c => c.cost).ToList())
            {
                if (c.IsWitness && enemy.board.Count >= 7) continue;
                if (!AiWants(c, out var t)) continue;
                yield return PlayCard(enemy, c, t, enemy.board.Count);
                yield return Wait(0.4f);
                played = true;
                break;
            }
            if (!played) break;
        }
        foreach (var u in enemy.board.ToList())
        {
            if (duelOver) yield break;
            if (!enemy.board.Contains(u) || !CanAttack(u)) continue;
            var t = AiAttackTarget(u);
            if (t == null) continue;
            yield return Attack(u, t);
            yield return Wait(0.3f);
        }
        if (!duelOver) StartCoroutine(BeginTurn(player));
    }

    static int Value(Unit u) => u.atk + u.hp + (u.taunt ? 2 : 0) + (u.shield ? 2 : 0);

    bool AiWants(CardDef c, out Target t)
    {
        t = null;
        if (c.fx.Any(f => f.act == "damage" && f.tgt == "all_minions") && enemy.board.Sum(Value) > player.board.Sum(Value)) return false;
        if (c.fx.All(f => f.act == "heal" || f.act == "draw") && c.fx.Any(f => f.act == "heal") && enemy.hp > enemy.maxHp - 4 && enemy.hand.Count > 1)
            return false;
        var pf = PickFx(c);
        if (pf == null) return true;
        var mine = player.board;
        switch (pf.act)
        {
            case "damage":
            {
                var kill = mine.Where(u => u.hp <= pf.n && !u.shield).OrderByDescending(Value).FirstOrDefault();
                if (kill != null) { t = new Target { unit = kill }; return true; }
                if (pf.tgt == "pick_enemy") { t = new Target { hero = player }; return true; }
                var any = mine.OrderByDescending(Value).FirstOrDefault();
                if (any == null) return false;
                t = new Target { unit = any };
                return true;
            }
            case "buff": case "heal":
            {
                var own = enemy.board.OrderByDescending(Value).FirstOrDefault();
                if (own == null) return false;
                t = new Target { unit = own };
                return true;
            }
            default:
            {
                var best = mine.OrderByDescending(u => Value(u) + (pf.act == "silence" && (u.taunt || u.shield) ? 5 : 0)).FirstOrDefault();
                if (best == null || (pf.act != "silence" && Value(best) < 5)) return false;
                t = new Target { unit = best };
                return true;
            }
        }
    }

    Target AiAttackTarget(Unit u)
    {
        var taunts = player.board.Where(x => x.taunt).ToList();
        if (taunts.Count > 0)
        {
            var k = taunts.Where(x => x.hp <= u.atk && !x.shield).OrderByDescending(Value).FirstOrDefault()
                    ?? taunts.OrderBy(x => x.hp).First();
            return new Target { unit = k };
        }
        int lethal = enemy.board.Where(CanAttack).Sum(x => x.atk);
        if (lethal >= player.hp || Boss.id == "bulldozer") return new Target { hero = player };
        var trade = player.board
            .Where(x => x.hp <= u.atk && !x.shield && (x.atk < u.hp || Value(x) >= Value(u)))
            .OrderByDescending(Value).FirstOrDefault();
        if (trade != null && (trade.atk >= 3 || bossIndex >= 1)) return new Target { unit = trade };
        return new Target { hero = player };
    }
}
