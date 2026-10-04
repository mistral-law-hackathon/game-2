using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// IMGUI rendering (1280x720 virtual canvas), input, particles and procedural audio.
public partial class CardGame
{
    const float W = 1280, H = 720;
    Texture2D white, soft, disc, ring;
    readonly Dictionary<string, Texture2D> art = new();
    GUIStyle st, fieldStyle;
    bool inModal;

    class P { public Vector2 p, v; public Color c; public float size, life, max, grav, drag, grow; public int kind; }
    readonly List<P> parts = new();
    class FloatText { public Vector2 p; public string s; public Color c; public float t; }
    readonly List<FloatText> floats = new();
    float shakeAmt;
    string banner = ""; float bannerT = 99; Color bannerCol;
    string splash = ""; float splashT = 99; Color splashCol;
    CardDef showEnemyCard; float showEnemyT = 99;
    CardDef flyCard; float flyT = 99;
    CardDef dragCard; Unit dragUnit; Vector2 dropPos;
    float clangT;

    static readonly Rect EnemyHero = new(585, 10, 110, 110), PlayerHero = new(585, 458, 110, 110);
    static readonly Rect EndTurnRect = new(1118, 268, 146, 54), ForgeRect = new(1104, 440, 168, 84);

    // ---------- setup ----------
    void InitGfx()
    {
        white = MakeTex(4, r => 1);
        soft = MakeTex(64, r => Mathf.Pow(Mathf.Clamp01(1 - r), 2));
        disc = MakeTex(64, r => Mathf.Clamp01((1 - r) * 20));
        ring = MakeTex(128, r => Mathf.Clamp01(1 - Mathf.Abs(r - 0.88f) / 0.08f));
    }

    static Texture2D MakeTex(int size, Func<float, float> alpha)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                px[y * size + x] = new Color(1, 1, 1, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    Texture2D Art(string n)
    {
        n ??= "objection";
        if (!art.TryGetValue(n, out var t)) { t = Resources.Load<Texture2D>("Art/" + n); art[n] = t; }
        return t ? t : (n == "objection" ? white : Art("objection"));
    }

    static Color Col(string hex, Color fallback) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    static Rect Grow(Rect r, float d) => new(r.x - d, r.y - d, r.width + 2 * d, r.height + 2 * d);
    static Rect Around(Vector2 c, float w, float h) => new(c.x - w / 2, c.y - h / 2, w, h);
    static Color A(Color c, float a) { c.a *= a; return c; }
    static float Pulse(float speed = 4) => 0.5f + 0.5f * Mathf.Sin(Time.time * speed);

    void Box(Rect r, Color c, float radius = 0) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, 0, radius);
    void Border(Rect r, Color c, float w, float radius) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, w, radius);
    void Img(Rect r, Texture t, float radius, Color tint) => GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop, true, 0, tint, 0, radius);
    void Glow(Rect r, Color c) => GUI.DrawTexture(r, soft, ScaleMode.StretchToFill, true, 0, c, 0, 0);

    void Txt(Rect r, string s, float size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool bold = true, bool shadow = true, FontStyle? fs = null)
    {
        st.fontSize = Mathf.Max(6, Mathf.RoundToInt(size));
        st.alignment = a;
        st.fontStyle = fs ?? (bold ? FontStyle.Bold : FontStyle.Normal);
        if (shadow)
        {
            st.normal.textColor = new Color(0, 0, 0, c.a * 0.85f);
            GUI.Label(new Rect(r.x + size * 0.08f + 1, r.y + size * 0.1f + 1, r.width, r.height), s, st);
        }
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    bool Button(Rect r, string s, Color c, float size = 20, bool enabled = true)
    {
        if (forgeOpen && !inModal) enabled = false;
        var e = Event.current;
        bool hover = enabled && r.Contains(e.mousePosition);
        var col = enabled ? (hover ? Color.Lerp(c, Color.white, 0.25f) : c) : new Color(0.3f, 0.3f, 0.32f, 0.9f);
        Box(new Rect(r.x, r.y + 5, r.width, r.height), new Color(0, 0, 0, 0.45f), 14);
        Box(r, col, 14);
        Box(new Rect(r.x + 4, r.y + 3, r.width - 8, r.height * 0.42f), new Color(1, 1, 1, 0.13f), 10);
        Border(r, new Color(1, 1, 1, hover ? 0.9f : 0.45f), 2, 14);
        Txt(r, s, size, Color.white);
        if (enabled && hover && e.type == EventType.MouseDown && e.button == 0) { e.Use(); Sfx("card", 1.5f); return true; }
        return false;
    }

    // ---------- main loop ----------
    void Update()
    {
        float dt = Time.deltaTime;
        shakeAmt = Mathf.MoveTowards(shakeAmt, 0, dt * 45);
        bubbleT += dt; bannerT += dt; splashT += dt; forgeT += dt; showEnemyT += dt; flyT += dt; endT += dt;
        foreach (var s in new[] { player, enemy })
        {
            if (s == null) continue;
            s.heroShake = Mathf.MoveTowards(s.heroShake, 0, dt);
            s.heroFlash = Mathf.MoveTowards(s.heroFlash, 0, dt * 2.5f);
            for (int i = 0; i < s.board.Count; i++)
            {
                var u = s.board[i];
                if (!u.lunging) u.pos = Vector2.Lerp(u.pos, SlotPos(s, i, s.board.Count), 1 - Mathf.Exp(-12 * dt));
                u.pop = Mathf.MoveTowards(u.pop, 0, dt * 3);
                u.flash = Mathf.MoveTowards(u.flash, 0, dt * 3);
                u.shake = Mathf.MoveTowards(u.shake, 0, dt);
            }
        }
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var p = parts[i];
            p.life -= dt;
            if (p.life <= 0) { parts.RemoveAt(i); continue; }
            p.v.y += p.grav * dt;
            p.v *= 1 - Mathf.Clamp01(p.drag * dt);
            p.p += p.v * dt;
            p.size += p.grow * dt;
        }
        for (int i = floats.Count - 1; i >= 0; i--) { floats[i].t += dt; floats[i].p.y -= 40 * dt; if (floats[i].t > 1.4f) floats.RemoveAt(i); }
        if (forgeOpen && forgePhase == 1)
        {
            float a = Time.time * 6;
            for (int k = 0; k < 3; k++)
            {
                float ang = a + k * 2.1f;
                var c = Color.HSVToRGB((Time.time * 0.4f + k * 0.33f) % 1, 0.8f, 1);
                var pos = new Vector2(640, 330) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 120;
                parts.Add(new P { p = pos, v = (new Vector2(640, 330) - pos) * 1.5f, c = c, size = 18, life = 0.6f, max = 0.6f, drag = 1 });
            }
            clangT += dt;
            if (clangT > 0.45f) { clangT = 0; Sfx("forge", UnityEngine.Random.Range(0.9f, 1.3f)); Shake(3); }
        }
    }

    void OnGUI()
    {
        if (st == null)
        {
            st = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 22, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 6, 6) };
            fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = Color.white;
            fieldStyle.normal.background = fieldStyle.focused.background = fieldStyle.hover.background = white;
        }
        GUI.matrix = Matrix4x4.identity;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white, ScaleMode.StretchToFill, true, 0, Color.black, 0, 0);
        float s = Mathf.Min(Screen.width / W, Screen.height / H);
        var off = new Vector2((Screen.width - W * s) / 2, (Screen.height - H * s) / 2);
        var sh = shakeAmt > 0 ? UnityEngine.Random.insideUnitCircle * shakeAmt : Vector2.zero;
        GUI.matrix = Matrix4x4.TRS(new Vector3(off.x + sh.x * s, off.y + sh.y * s, 0), Quaternion.identity, new Vector3(s, s, 1));

        switch (mode)
        {
            case Mode.Loading: DrawLoading(); break;
            case Mode.Title: DrawTitle(); break;
            case Mode.Intro: DrawIntro(); break;
            case Mode.Duel: DrawDuel(); break;
            case Mode.Reward: DrawReward(); break;
            case Mode.Hall: DrawHall(); break;
        }
        DrawParticles();
        DrawFloats();
        DrawBanner();
        if (forgeOpen) { inModal = true; DrawForge(); inModal = false; }
        DrawSplash();
    }

    void Background(float dark)
    {
        Img(new Rect(0, 0, W, H), Art("courtroom"), 0, Color.white);
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.02f, 0.08f, dark));
    }

    // ---------- screens ----------
    void DrawLoading()
    {
        Background(0.7f);
        Txt(new Rect(0, 300, W, 60), loadError ?? "Preparing the courtroom...", 28, Color.white);
    }

    void Logo(float y, float size)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-4, new Vector2(640, y + size * 0.6f));
        float k = 1 + 0.03f * Mathf.Sin(Time.time * 3);
        GUIUtility.ScaleAroundPivot(new Vector2(k, k), new Vector2(640, y + size * 0.6f));
        Txt(new Rect(0, y + 6, W, size * 1.3f), "OBJECTION!", size, new Color(0.75f, 0.05f, 0.1f), shadow: false);
        Txt(new Rect(0, y, W, size * 1.3f), "OBJECTION!", size, new Color(1f, 0.85f, 0.2f));
        GUI.matrix = m;
    }

    void DrawTitle()
    {
        Background(0.55f);
        if (UnityEngine.Random.value < 0.08f)
            parts.Add(new P { p = new Vector2(UnityEngine.Random.Range(0, W), -10), v = new Vector2(UnityEngine.Random.Range(-30, 30), 120), c = Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1), size = 9, life = 6, max = 6, kind = 1 });
        Logo(40, 120);
        Txt(new Rect(0, 210, W, 40), "THE AI CARD DUEL", 30, Color.white);
        Txt(new Rect(0, 250, W, 30), "Out-argue three ridiculous lawyers. Forge ANY card you can imagine - designed live by Mistral AI.", 18, new Color(1, 0.92f, 0.75f), bold: false);
        var rk = new Rect(170, 300, 260, 260);
        Glow(Grow(rk, 50), new Color(0.3f, 0.6f, 1f, 0.6f));
        Img(rk, Art("rookie"), 24, Color.white);
        Border(rk, new Color(0.4f, 0.7f, 1f), 5, 24);
        Txt(new Rect(rk.x, rk.yMax + 6, rk.width, 30), "YOU - Rookie Lawyer", 18, Color.white);
        Txt(new Rect(470, 390, 120, 80), "VS", 64, new Color(1, 0.4f, 0.3f));
        for (int i = 0; i < content.bosses.Count; i++)
        {
            var b = content.bosses[i];
            var r = new Rect(620 + i * 200, 320 + (i == 1 ? -20 : 0), 180, 180);
            Img(r, Art(b.art), 20, Color.white);
            Border(r, new Color(1, 0.35f, 0.3f), 4, 20);
            Txt(new Rect(r.x - 10, r.yMax + 4, r.width + 20, 26), b.name, 17, Color.white);
            Txt(new Rect(r.x - 10, r.yMax + 26, r.width + 20, 22), "Case " + (i + 1), 14, new Color(1, 0.8f, 0.5f), bold: false);
        }
        if (Button(new Rect(490, 610, 300, 70), "START TRIAL", new Color(0.85f, 0.15f, 0.2f), 30)) { ResetRun(); EnterIntro(); }
    }

    void Bubble(Rect r, string text, bool tailLeft = true)
    {
        if (string.IsNullOrEmpty(text)) return;
        int n = Mathf.Min(text.Length, (int)(bubbleT * 50));
        Box(new Rect(r.x + 4, r.y + 6, r.width, r.height), new Color(0, 0, 0, 0.35f), 18);
        Box(r, new Color(1, 0.98f, 0.93f), 18);
        var tail = tailLeft ? new Vector2(r.x - 8, r.y + 30) : new Vector2(r.xMax + 8, r.y + 30);
        GUI.DrawTexture(Around(tail, 22, 22), disc, ScaleMode.StretchToFill, true, 0, new Color(1, 0.98f, 0.93f), 0, 0);
        GUI.DrawTexture(Around(tail + new Vector2(tailLeft ? -14 : 14, 12), 11, 11), disc, ScaleMode.StretchToFill, true, 0, new Color(1, 0.98f, 0.93f), 0, 0);
        Txt(new Rect(r.x + 14, r.y + 8, r.width - 28, r.height - 16), text.Substring(0, n), 17, new Color(0.15f, 0.1f, 0.12f), TextAnchor.MiddleLeft, shadow: false);
    }

    void DrawIntro()
    {
        Background(0.6f);
        var r = new Rect(130, 140, 380, 380);
        Glow(Grow(r, 70), new Color(1, 0.25f, 0.2f, 0.55f + 0.2f * Pulse(2)));
        Img(r, Art(Boss.art), 28, Color.white);
        Border(r, new Color(1, 0.4f, 0.3f), 6, 28);
        Txt(new Rect(560, 120, 680, 40), $"CASE #{bossIndex + 1} OF {content.bosses.Count}", 22, new Color(1, 0.8f, 0.5f));
        Txt(new Rect(560, 160, 680, 70), Boss.name, 56, Color.white, TextAnchor.MiddleLeft);
        Txt(new Rect(560, 228, 680, 34), Boss.title, 22, new Color(1, 0.9f, 0.8f), TextAnchor.MiddleLeft, fs: FontStyle.Italic);
        Bubble(new Rect(560, 290, 620, 110), bubble);
        Txt(new Rect(560, 420, 680, 30), $"Opponent credibility: {Boss.hp}     Your deck: {playerDeck.Count} cards ({forgedCards.Count} forged)", 18, Color.white, TextAnchor.MiddleLeft, bold: false);
        if (Button(new Rect(560, 480, 300, 70), "BEGIN DUEL", new Color(0.85f, 0.15f, 0.2f), 28)) StartDuel();
    }

    void DrawReward()
    {
        Background(0.65f);
        Txt(new Rect(0, 40, W, 70), "CASE WON!", 60, new Color(1, 0.85f, 0.2f));
        Txt(new Rect(0, 110, W, 40), "Choose a reward for your deck", 24, Color.white);
        var m = Event.current.mousePosition;
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(250 + i * 280, 180, 220, 308);
            bool hover = r.Contains(m) && !forgeOpen;
            if (hover) r = Grow(r, 8);
            if (i < rewardChoices.Count) DrawCard(r, rewardChoices[i], hover);
            else
            {
                Glow(Grow(r, 40), Color.HSVToRGB(Time.time * 0.2f % 1, 0.8f, 1) * new Color(1, 1, 1, 0.8f));
                Box(r, new Color(0.35f, 0.05f, 0.4f), 18);
                Border(r, Color.HSVToRGB(Time.time * 0.3f % 1, 0.7f, 1), 5, 18);
                Txt(new Rect(r.x, r.y + 40, r.width, 80), "THE\nFORGE", 40, Color.white);
                Txt(new Rect(r.x + 16, r.y + 150, r.width - 32, 120), "Describe any card you want. Mistral AI designs it live.", 17, new Color(1, 0.9f, 1), bold: false);
            }
            if (hover)
            {
                Txt(new Rect(r.x, r.yMax + 10, r.width, 30), "Click to choose", 18, new Color(1, 1, 0.7f));
                if (Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                    Sfx("chime", 1.2f);
                    if (i < rewardChoices.Count) PickReward(rewardChoices[i]); else OpenForge(ForgeSource.Reward);
                }
            }
        }
        Bubble(new Rect(340, 560, 600, 90), bubble);
        GUI.DrawTexture(new Rect(230, 550, 100, 100), Art(Boss.art), ScaleMode.ScaleAndCrop, true, 0, Color.white, 0, 16);
    }

    void DrawHall()
    {
        Background(0.7f);
        if (UnityEngine.Random.value < 0.25f)
            parts.Add(new P { p = new Vector2(UnityEngine.Random.Range(0, W), -10), v = new Vector2(UnityEngine.Random.Range(-40, 40), 160), c = Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1), size = 10, life = 6, max = 6, kind = 1 });
        Txt(new Rect(0, 24, W, 80), "JUSTICE SERVED!", 64, new Color(1, 0.85f, 0.2f));
        Txt(new Rect(0, 100, W, 34), "You out-argued all three lawyers. Hall of Fame - your forged cards:", 22, Color.white);
        var list = forgedCards.TakeLast(5).ToList();
        if (list.Count == 0) Txt(new Rect(0, 280, W, 40), "No forged cards this time... try the Forge next trial!", 22, new Color(1, 0.9f, 0.8f));
        float total = list.Count * 200 - 20;
        for (int i = 0; i < list.Count; i++) DrawCard(new Rect(640 - total / 2 + i * 200, 160, 180, 252), list[i], false);
        Txt(new Rect(0, 440, W, 34), $"Turns played: {statTurns}     Cards played: {statCards}     Damage dealt: {statDamage}     Cards forged: {forgedCards.Count}", 20, new Color(1, 0.95f, 0.85f), bold: false);
        var rk = new Rect(560, 490, 160, 160);
        Img(rk, Art("rookie"), 20, Color.white);
        Border(rk, new Color(1, 0.85f, 0.2f), 5, 20);
        if (Button(new Rect(1000, 600, 240, 64), "PLAY AGAIN", new Color(0.85f, 0.15f, 0.2f), 24)) { ResetRun(); mode = Mode.Title; }
    }

    // ---------- duel ----------
    Vector2 SlotPos(Side s, int i, int n) => new(640 + (i - (n - 1) / 2f) * 108, s.isPlayer ? 380 : 210);
    static Vector2 HeroCenterOf(bool isPlayer) => isPlayer ? PlayerHero.center : EnemyHero.center;
    Vector2 HeroCenter(Side s) => HeroCenterOf(s.isPlayer);
    Vector2 TargetPos(Target t) => t.unit != null ? t.unit.pos : HeroCenter(t.hero);
    static Rect UnitRect(Unit u) => Around(u.pos, 96, 130);

    List<Rect> HandRects()
    {
        int n = player.hand.Count;
        float sp = Mathf.Min(122, 880f / Mathf.Max(1, n));
        float x0 = 640 - (n - 1) * sp / 2;
        var l = new List<Rect>();
        for (int i = 0; i < n; i++) l.Add(new Rect(x0 + i * sp - 60, 556 + Mathf.Abs(i - (n - 1) / 2f) * 3, 120, 168));
        return l;
    }

    Target TargetAt(Vector2 m)
    {
        foreach (var u in enemy.board.Concat(player.board)) if (UnitRect(u).Contains(m)) return new Target { unit = u };
        if (EnemyHero.Contains(m)) return new Target { hero = enemy };
        if (PlayerHero.Contains(m)) return new Target { hero = player };
        return null;
    }

    bool Playable(CardDef c)
    {
        if (c.cost > player.mana) return false;
        if (c.IsWitness && player.board.Count >= 7) return false;
        var pf = PickFx(c);
        return pf == null || HasAnyPick(pf.tgt, player);
    }

    bool HasMoves() => player.hand.Any(Playable) || player.board.Any(CanAttack) || forgeCharges > 0;

    void DrawDuel()
    {
        var e = Event.current;
        var m = e.mousePosition;
        bool interactive = playerTurn && !busy && !duelOver && !forgeOpen;
        Background(0.35f);
        Box(new Rect(150, 132, 980, 318), new Color(0.05f, 0.02f, 0.05f, 0.35f), 30);
        Box(new Rect(180, 294, 920, 2), new Color(1, 0.85f, 0.5f, 0.35f));

        var pf = dragCard != null ? PickFx(dragCard) : null;
        DrawHero(enemy, EnemyHero, Boss.art, Boss.name, pf != null && ValidPick(pf.tgt, player, new Target { hero = enemy }) || dragUnit != null && ValidAttack(dragUnit, new Target { hero = enemy }));
        DrawHero(player, PlayerHero, "rookie", "You", pf != null && ValidPick(pf.tgt, player, new Target { hero = player }));
        DrawMana(player, new Vector2(712, 516));
        Txt(new Rect(712, 74, 200, 24), $"Evidence {enemy.mana}/{enemy.maxMana}", 15, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleLeft);
        Txt(new Rect(712, 94, 300, 24), $"Hand {enemy.hand.Count}   Deck {enemy.deck.Count}", 15, Color.white, TextAnchor.MiddleLeft, bold: false);
        for (int i = 0; i < enemy.hand.Count; i++)
        {
            var r = new Rect(400 - i * 20, 14 + Mathf.Abs(i - 4) * 2, 44, 62);
            Box(r, new Color(0.45f, 0.08f, 0.12f), 6);
            Border(r, new Color(1, 0.75f, 0.3f), 2, 6);
            Txt(r, "!", 22, new Color(1, 0.8f, 0.3f));
        }
        Txt(new Rect(1120, 330, 150, 24), $"Deck {player.deck.Count}", 16, Color.white);

        if (bubbleT < 7) Bubble(new Rect(712, 16, 380, 54), bubble);

        Unit hoverUnit = null;
        foreach (var u in enemy.board.Concat(player.board).OrderBy(u => u.lunging ? 1 : 0))
        {
            bool canAct = interactive && u.owner.isPlayer && CanAttack(u);
            bool tgt = (pf != null && ValidPick(pf.tgt, player, new Target { unit = u })) || (dragUnit != null && ValidAttack(dragUnit, new Target { unit = u }));
            DrawUnit(u, canAct, tgt);
            if (UnitRect(u).Contains(m) && dragCard == null) hoverUnit = u;
        }

        // hand
        var rects = HandRects();
        int hoverHand = -1;
        if (dragCard == null && dragUnit == null && !forgeOpen)
            for (int i = rects.Count - 1; i >= 0; i--) if (rects[i].Contains(m)) { hoverHand = i; break; }
        for (int i = 0; i < player.hand.Count; i++)
        {
            var c = player.hand[i];
            if (c == dragCard || i == hoverHand) continue;
            var r = rects[i];
            if (c == flyCard && flyT < 0.6f)
            {
                float k = Mathf.SmoothStep(0, 1, flyT / 0.6f);
                var big = new Rect(525, 175, 230, 322);
                r = new Rect(Mathf.Lerp(big.x, r.x, k), Mathf.Lerp(big.y, r.y, k), Mathf.Lerp(big.width, r.width, k), Mathf.Lerp(big.height, r.height, k));
            }
            DrawCard(r, c, interactive && Playable(c));
        }
        if (hoverHand >= 0)
        {
            var c = player.hand[hoverHand];
            var r = new Rect(rects[hoverHand].center.x - 90, H - 262, 180, 252);
            DrawCard(r, c, interactive && Playable(c));
            if (!string.IsNullOrEmpty(c.flavor)) Txt(new Rect(r.x - 60, r.y - 44, r.width + 120, 40), "\"" + c.flavor + "\"", 14, new Color(1, 0.95f, 0.8f), bold: false, fs: FontStyle.Italic);
        }

        // buttons
        bool pulse = interactive && !HasMoves();
        if (pulse) Glow(Grow(EndTurnRect, 26), new Color(1, 0.8f, 0.2f, 0.4f + 0.5f * Pulse()));
        if (Button(EndTurnRect, playerTurn ? "END TURN" : "ENEMY TURN", playerTurn ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.35f, 0.3f, 0.3f), 20, interactive)) EndTurn();
        if (forgeCharges > 0 && interactive) Glow(Grow(ForgeRect, 34), A(Color.HSVToRGB(Time.time * 0.25f % 1, 0.8f, 1), 0.55f + 0.35f * Pulse(3)));
        if (Button(ForgeRect, forgeCharges > 0 ? "THE FORGE\n(1 free use)" : "FORGE USED", new Color(0.6f, 0.1f, 0.65f), 19, interactive && forgeCharges > 0))
        {
            forgeCharges--;
            OpenForge(ForgeSource.Button);
        }
        Txt(new Rect(1090, 528, 200, 40), "Invent any card -\nAI designs it", 13, new Color(1, 0.85f, 1), bold: false);

        // hint
        string hint = !playerTurn ? $"{Boss.name} is thinking..." :
            dragCard != null ? (pf != null ? "Drop on a glowing target" : "Drop on the board to play") :
            dragUnit != null ? "Drop on an enemy to attack" :
            "Drag cards up to play them  -  drag your glowing witnesses to attack  -  try THE FORGE!";
        if (!duelOver) Txt(new Rect(180, 284, 920, 22), hint, 14, new Color(1, 0.95f, 0.8f, 0.9f), bold: false);

        // inspect
        if (hoverUnit != null && dragUnit == null)
        {
            var r = new Rect(hoverUnit.pos.x < 640 ? 950 : 30, 150, 170, 238);
            DrawCard(r, hoverUnit.def, false);
            if (!string.IsNullOrEmpty(hoverUnit.def.flavor)) Txt(new Rect(r.x - 10, r.yMax + 4, r.width + 20, 60), "\"" + hoverUnit.def.flavor + "\"", 13, new Color(1, 0.95f, 0.8f), bold: false, fs: FontStyle.Italic);
        }

        // enemy card reveal
        if (showEnemyCard != null && showEnemyT < 1.3f)
        {
            float k = Mathf.Clamp01(showEnemyT * 5);
            var r = Around(new Vector2(250, 260), 180 * k, 252 * k);
            DrawCard(r, showEnemyCard, false);
            Txt(new Rect(140, 395, 220, 26), Boss.name + " plays", 15, new Color(1, 0.6f, 0.5f));
        }

        // drag visuals
        if (dragCard != null)
        {
            if (pf != null && m.y < 520) { Arrow(PlayerHero.center, m, new Color(1, 0.3f, 0.25f)); DrawCard(Around(PlayerHero.center + new Vector2(-150, 0), 100, 140), dragCard, true); }
            else DrawCard(Around(m, 120, 168), dragCard, true);
        }
        if (dragUnit != null) Arrow(dragUnit.pos, m, new Color(1, 0.75f, 0.2f));

        // input
        if (interactive && e.type == EventType.MouseDown && e.button == 0)
        {
            if (hoverHand >= 0)
            {
                var c = player.hand[hoverHand];
                if (Playable(c)) { dragCard = c; Sfx("card", 1.3f); }
                else
                {
                    Float(rects[hoverHand].center + new Vector2(0, -60), c.cost > player.mana ? "Not enough Evidence!" : "Can't play that now", new Color(1, 0.6f, 0.4f));
                    Sfx("hit", 2f);
                }
                e.Use();
            }
            else
            {
                var u = player.board.FirstOrDefault(x => UnitRect(x).Contains(m));
                if (u != null)
                {
                    if (CanAttack(u)) dragUnit = u;
                    else Float(u.pos + new Vector2(0, -70), u.attacked ? "Already attacked" : u.atk <= 0 ? "No attack" : "Zzz... next turn", new Color(1, 0.85f, 0.6f));
                    e.Use();
                }
            }
        }
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (dragCard != null)
            {
                var c = dragCard;
                dragCard = null;
                if (interactive) TryPlay(c, m);
                e.Use();
            }
            else if (dragUnit != null)
            {
                var u = dragUnit;
                dragUnit = null;
                var t = TargetAt(m);
                if (interactive && ValidAttack(u, t)) StartCoroutine(Attack(u, t));
                else if (interactive && t != null && t.hero != player && (t.unit == null || t.unit.owner != player))
                    Float(m, "A Key Witness blocks the way!", new Color(1, 0.8f, 0.5f));
                e.Use();
            }
        }
        if (e.type == EventType.MouseDrag && (dragCard != null || dragUnit != null)) e.Use();

        if (duelOver) DrawDuelEnd();
    }

    void TryPlay(CardDef c, Vector2 m)
    {
        var pf = PickFx(c);
        if (pf != null)
        {
            var t = TargetAt(m);
            if (ValidPick(pf.tgt, player, t)) StartCoroutine(PlayCard(player, c, t, 0));
            else if (m.y < 520) Float(m, "Not a valid target", new Color(1, 0.7f, 0.5f));
            return;
        }
        if (m.y > 520) return;
        int slot = player.board.Count(u => u.pos.x < m.x);
        dropPos = m;
        StartCoroutine(PlayCard(player, c, null, slot));
    }

    void DrawDuelEnd()
    {
        float a = Mathf.Clamp01(endT - 0.8f);
        if (a <= 0) return;
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.65f * a));
        Txt(new Rect(0, 150, W, 100), duelWon ? "CASE WON!" : "OVERRULED!", 84, A(duelWon ? new Color(1, 0.85f, 0.2f) : new Color(1, 0.35f, 0.3f), a));
        GUI.DrawTexture(new Rect(330, 290, 130, 130), Art(Boss.art), ScaleMode.ScaleAndCrop, true, 0, new Color(1, 1, 1, a), 0, 18);
        Bubble(new Rect(490, 300, 470, 100), bubble);
        if (duelWon)
        {
            bool last = bossIndex >= content.bosses.Count - 1;
            if (Button(new Rect(490, 470, 300, 66), last ? "FINAL VERDICT" : "CLAIM REWARD", new Color(0.2f, 0.65f, 0.3f), 24))
            {
                if (last) { mode = Mode.Hall; Sfx("chime", 0.8f); } else OpenReward();
            }
        }
        else
        {
            Txt(new Rect(0, 420, W, 30), "No game over in this court - your deck (and forged cards) stay. Try again!", 18, A(Color.white, a), bold: false);
            if (Button(new Rect(490, 470, 300, 66), "RETRY TRIAL", new Color(0.85f, 0.15f, 0.2f), 24)) StartDuel();
        }
    }

    void DrawHero(Side s, Rect r, string artName, string label, bool targetable)
    {
        var sh = s.heroShake > 0 ? UnityEngine.Random.insideUnitCircle * 10 * s.heroShake : Vector2.zero;
        r.position += sh;
        Color frame = s.isPlayer ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.35f, 0.3f);
        if (targetable) Glow(Grow(r, 30), new Color(1, 0.25f, 0.2f, 0.5f + 0.4f * Pulse(6)));
        Box(new Rect(r.x + 4, r.y + 6, r.width, r.height), new Color(0, 0, 0, 0.5f), 22);
        Img(r, Art(artName), 20, Color.white);
        if (s.heroFlash > 0) Box(r, new Color(1, 0.1f, 0.1f, s.heroFlash * 0.6f), 20);
        Border(r, frame, 4, 20);
        var hp = Around(new Vector2(r.xMax - 6, r.yMax - 10), 44, 44);
        GUI.DrawTexture(hp, disc, ScaleMode.StretchToFill, true, 0, new Color(0.75f, 0.1f, 0.12f), 0, 0);
        Txt(hp, Mathf.Max(0, s.hp).ToString(), 20, Color.white);
        Txt(new Rect(r.x - 120, r.y + (s.isPlayer ? 64 : 10), 116, 24), label, 16, Color.white, TextAnchor.MiddleRight);
    }

    void DrawMana(Side s, Vector2 at)
    {
        Txt(new Rect(at.x, at.y - 22, 220, 22), $"Evidence {s.mana}/{s.maxMana}", 16, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleLeft);
        for (int i = 0; i < 10; i++)
        {
            var r = new Rect(at.x + i * 21, at.y + 2, 18, 18);
            var c = i < s.mana ? new Color(0.25f, 0.6f, 1f) : i < s.maxMana ? new Color(0.15f, 0.2f, 0.35f) : new Color(0.1f, 0.1f, 0.15f, 0.5f);
            if (i < s.mana) GUI.DrawTexture(Grow(r, 5), soft, ScaleMode.StretchToFill, true, 0, new Color(0.3f, 0.7f, 1f, 0.6f), 0, 0);
            GUI.DrawTexture(r, disc, ScaleMode.StretchToFill, true, 0, c, 0, 0);
        }
    }

    Color FrameColor(CardDef c, bool enemySide)
    {
        if (c.forged) return Color.HSVToRGB(Time.time * 0.3f % 1, 0.65f, 1);
        if (enemySide) return new Color(0.85f, 0.25f, 0.22f);
        return c.IsWitness ? new Color(0.92f, 0.72f, 0.3f) : new Color(0.55f, 0.75f, 1f);
    }

    void StatDisc(Vector2 c, float size, int v, Color bg, Color fg)
    {
        var r = Around(c, size, size);
        GUI.DrawTexture(Grow(r, 2), disc, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.6f), 0, 0);
        GUI.DrawTexture(r, disc, ScaleMode.StretchToFill, true, 0, bg, 0, 0);
        Txt(r, v.ToString(), size * 0.6f, fg);
    }

    void DrawUnit(Unit u, bool canAct, bool targetable)
    {
        var r = UnitRect(u);
        if (u.pop > 0) r = Grow(r, u.pop * 14);
        if (u.shake > 0) r.position += UnityEngine.Random.insideUnitCircle * 14 * u.shake;
        if (targetable) Glow(Grow(r, 30), new Color(1, 0.25f, 0.2f, 0.5f + 0.4f * Pulse(6)));
        if (canAct) Glow(Grow(r, 22), new Color(0.3f, 1f, 0.4f, 0.45f + 0.3f * Pulse()));
        if (u.shield) Glow(Grow(r, 26), new Color(0.4f, 0.9f, 1f, 0.8f));
        Box(new Rect(r.x + 4, r.y + 7, r.width, r.height), new Color(0, 0, 0, 0.5f), 18);
        Box(r, FrameColor(u.def, !u.owner.isPlayer), 18);
        Img(Grow(r, -4), Art(u.def.art), 15, Color.white);
        if (u.flash > 0) Box(r, new Color(1, 0.15f, 0.1f, u.flash * 0.55f), 18);
        if (u.taunt)
        {
            Border(Grow(r, 5), new Color(0.82f, 0.85f, 0.9f), 6, 22);
            var tag = new Rect(r.center.x - 24, r.y - 12, 48, 18);
            Box(tag, new Color(0.3f, 0.32f, 0.38f), 8);
            Txt(tag, "KEY", 11, Color.white);
        }
        if (u.shield) Border(Grow(r, 2), new Color(0.5f, 0.95f, 1f), 3, 20);
        if (u.def.forged)
        {
            var tag = new Rect(r.center.x - 30, r.yMax - 30, 60, 16);
            Box(tag, new Color(0.7f, 0.1f, 0.6f, 0.9f), 7);
            Txt(tag, "FORGED", 10, Color.white);
        }
        StatDisc(new Vector2(r.x + 6, r.yMax - 6), 34, u.atk, new Color(0.95f, 0.75f, 0.15f), Color.white);
        StatDisc(new Vector2(r.xMax - 6, r.yMax - 6), 34, u.hp, new Color(0.8f, 0.15f, 0.15f), u.hp < u.maxHp ? new Color(1, 0.75f, 0.75f) : Color.white);
        if (u.sleeping && u.owner.isPlayer) Txt(new Rect(r.xMax - 40, r.y - 4, 40, 30), "z z", 16, new Color(0.85f, 0.9f, 1f));
        if (u.fx.Any(f => f.when == "death")) Txt(new Rect(r.x, r.y + 4, 30, 20), "+", 18, new Color(0.7f, 1f, 0.9f));
    }

    void DrawCard(Rect r, CardDef c, bool playable)
    {
        float k = r.width / 120f;
        if (playable) Glow(Grow(r, 18 * k), new Color(0.3f, 1f, 0.4f, 0.55f + 0.3f * Pulse()));
        if (c.forged) Glow(Grow(r, 26 * k), A(Color.HSVToRGB(Time.time * 0.3f % 1, 0.7f, 1), 0.6f));
        Box(new Rect(r.x + 4 * k, r.y + 6 * k, r.width, r.height), new Color(0, 0, 0, 0.5f), 12 * k);
        Box(r, FrameColor(c, false), 12 * k);
        var inner = Grow(r, -4 * k);
        Box(inner, c.forged ? new Color(0.28f, 0.08f, 0.3f) : c.IsWitness ? new Color(0.33f, 0.21f, 0.12f) : new Color(0.15f, 0.17f, 0.38f), 10 * k);
        var artR = new Rect(r.x + 8 * k, r.y + 8 * k, r.width - 16 * k, 76 * k);
        Img(artR, Art(c.art), 8 * k, Color.white);
        var plate = new Rect(r.x + 5 * k, r.y + 82 * k, r.width - 10 * k, 20 * k);
        Box(plate, new Color(0.08f, 0.05f, 0.06f, 0.9f), 6 * k);
        Txt(plate, c.name, (c.name.Length > 16 ? 9.5f : 11) * k, Color.white);
        Txt(new Rect(r.x + 8 * k, r.y + 104 * k, r.width - 16 * k, 42 * k), c.text, (c.text.Length > 70 ? 7.5f : 8.8f) * k, new Color(1, 0.95f, 0.85f), bold: false, shadow: false);
        Txt(new Rect(r.x, r.yMax - 17 * k, r.width, 14 * k), c.forged ? "FORGED" : c.IsWitness ? "Witness" : "Motion", 7.5f * k, c.forged ? new Color(1, 0.6f, 1) : new Color(1, 1, 1, 0.55f));
        var cost = new Rect(r.x - 6 * k, r.y - 6 * k, 30 * k, 30 * k);
        GUI.DrawTexture(Grow(cost, 2 * k), disc, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.6f), 0, 0);
        GUI.DrawTexture(cost, disc, ScaleMode.StretchToFill, true, 0, new Color(0.2f, 0.45f, 0.95f), 0, 0);
        Txt(cost, c.cost.ToString(), 17 * k, Color.white);
        if (c.IsWitness)
        {
            StatDisc(new Vector2(r.x + 6 * k, r.yMax - 6 * k), 28 * k, c.atk, new Color(0.95f, 0.75f, 0.15f), Color.white);
            StatDisc(new Vector2(r.xMax - 6 * k, r.yMax - 6 * k), 28 * k, c.hp, new Color(0.8f, 0.15f, 0.15f), Color.white);
        }
    }

    void Arrow(Vector2 from, Vector2 to, Color c)
    {
        var mid = (from + to) / 2 + new Vector2(0, -80);
        for (int i = 0; i <= 18; i++)
        {
            float t = (i + (Time.time * 3 % 1)) / 19f;
            var p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * mid + t * t * to;
            GUI.DrawTexture(Around(p, 14, 14), disc, ScaleMode.StretchToFill, true, 0, c, 0, 0);
        }
        GUI.DrawTexture(Around(to, 40, 40), ring, ScaleMode.StretchToFill, true, 0, c, 0, 0);
        GUI.DrawTexture(Around(to, 16, 16), disc, ScaleMode.StretchToFill, true, 0, c, 0, 0);
    }

    // ---------- Forge overlay ----------
    void DrawForge()
    {
        var e = Event.current;
        Box(new Rect(0, 0, W, H), new Color(0.03f, 0, 0.06f, 0.82f));
        var panel = new Rect(240, 70, 800, 580);
        Glow(Grow(panel, 60), A(Color.HSVToRGB(Time.time * 0.15f % 1, 0.8f, 1), 0.35f));
        Box(panel, new Color(0.12f, 0.04f, 0.16f, 0.97f), 26);
        Border(panel, Color.HSVToRGB(Time.time * 0.25f % 1, 0.7f, 1), 4, 26);
        Txt(new Rect(panel.x, panel.y + 14, panel.width, 50), "THE FORGE", 42, new Color(1, 0.85f, 0.3f));
        if (forgePhase == 0)
        {
            Txt(new Rect(panel.x + 40, 140, panel.width - 80, 50), "Describe ANY card idea in plain words. Mistral AI designs it live - the court keeps it balanced.", 19, Color.white, bold: false);
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { SubmitForge(); e.Use(); }
            var fr = new Rect(300, 210, 680, 56);
            Box(Grow(fr, 3), new Color(1, 0.5f, 1, 0.8f), 12);
            GUI.backgroundColor = new Color(0.08f, 0.02f, 0.1f);
            GUI.SetNextControlName("forge");
            forgeText = GUI.TextField(fr, forgeText, 120, fieldStyle);
            GUI.backgroundColor = Color.white;
            if (forgeFocus) { GUI.FocusControl("forge"); forgeFocus = false; }
            if (string.IsNullOrEmpty(forgeText)) Txt(new Rect(318, 210, 660, 56), "e.g. a pigeon that testifies against everyone", 20, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft, bold: false, shadow: false);
            Txt(new Rect(300, 285, 680, 26), "Need inspiration? Click one:", 16, new Color(1, 0.85f, 1), TextAnchor.MiddleLeft, bold: false);
            for (int i = 0; i < ForgeIdeas.Length; i++)
            {
                var r = new Rect(300 + (i % 2) * 345, 318 + (i / 2) * 52, 335, 42);
                if (Button(r, ForgeIdeas[i], new Color(0.35f, 0.12f, 0.45f), 15)) { forgeText = ForgeIdeas[i]; forgeFocus = true; }
            }
            if (!string.IsNullOrEmpty(forgeErr)) Txt(new Rect(300, 480, 680, 30), forgeErr, 18, new Color(1, 0.5f, 0.4f));
            if (Button(new Rect(470, 530, 340, 72), "FORGE IT!", new Color(0.85f, 0.2f, 0.6f), 30, !string.IsNullOrWhiteSpace(forgeText))) SubmitForge();
            if (forgeSrc != ForgeSource.Reward && Button(new Rect(840, 548, 160, 44), forgeSrc == ForgeSource.Button ? "Cancel" : "Skip", new Color(0.3f, 0.25f, 0.32f), 16)) CancelForge();
        }
        else if (forgePhase == 1)
        {
            var c = new Vector2(640, 330);
            GUI.DrawTexture(Around(c, 260 + 20 * Pulse(8), 260 + 20 * Pulse(8)), ring, ScaleMode.StretchToFill, true, 0, Color.HSVToRGB(Time.time * 0.5f % 1, 0.7f, 1), 0, 0);
            Glow(Around(c, 200, 200), new Color(1, 0.6f, 0.2f, 0.8f));
            Txt(new Rect(0, 300, W, 60), "Forging" + new string('.', 1 + (int)(Time.time * 3) % 3), 34, Color.white);
            Txt(new Rect(0, 520, W, 30), "\"" + forgeText + "\"", 20, new Color(1, 0.9f, 0.7f), bold: false, fs: FontStyle.Italic);
            Txt(new Rect(0, 560, W, 30), "Mistral is designing your card...", 18, new Color(1, 0.8f, 1), bold: false);
        }
        else if (forged != null)
        {
            float k = Mathf.Clamp01(forgeT / 0.5f);
            float flip = Mathf.Abs(Mathf.Cos((1 - k) * Mathf.PI * 1.5f));
            var r = new Rect(525, 150, 230, 322);
            var mtx = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(flip * (0.4f + 0.6f * k), 0.4f + 0.6f * k), r.center);
            DrawCard(r, forged, false);
            GUI.matrix = mtx;
            Txt(new Rect(panel.x + 20, 112, panel.width - 40, 36), forgeAnnounce, 20, new Color(1, 0.85f, 0.3f), fs: FontStyle.BoldAndItalic);
            Txt(new Rect(panel.x + 40, 486, panel.width - 80, 40), "\"" + forged.flavor + "\"", 16, new Color(1, 0.95f, 0.85f), bold: false, fs: FontStyle.Italic);
            if (forgeT > 0.6f && Button(new Rect(490, 540, 300, 70), forgeSrc == ForgeSource.Reward ? "ADD TO DECK!" : "ADD TO HAND!", new Color(0.2f, 0.65f, 0.3f), 26))
                AcceptForge();
        }
    }

    // ---------- effects ----------
    void Shake(float a) => shakeAmt = Mathf.Max(shakeAmt, a);
    void Banner(string s, Color c) { banner = s; bannerT = 0; bannerCol = c; }
    void Splash(string s, Color c) { splash = s; splashT = 0; splashCol = c; }
    void Float(Vector2 p, string s, Color c) => floats.Add(new FloatText { p = p, s = s, c = c });

    void DrawBanner()
    {
        if (bannerT > 1.3f) return;
        float a = bannerT < 0.2f ? bannerT / 0.2f : Mathf.Clamp01((1.3f - bannerT) / 0.3f);
        Box(new Rect(0, 300, W, 90), new Color(0, 0, 0, 0.6f * a));
        Box(new Rect(0, 300, W, 4), A(bannerCol, a));
        Box(new Rect(0, 386, W, 4), A(bannerCol, a));
        float x = Mathf.Lerp(-200, 0, Mathf.Clamp01(bannerT * 6));
        Txt(new Rect(x, 300, W, 90), banner, 54, A(Color.white, a));
    }

    void DrawSplash()
    {
        if (splashT > 1.25f) return;
        float a = Mathf.Clamp01((1.25f - splashT) / 0.25f);
        float s = splashT < 0.15f ? Mathf.Lerp(3f, 1f, splashT / 0.15f) : 1 + 0.04f * (splashT - 0.15f);
        var mtx = GUI.matrix;
        var c = new Vector2(640, 330);
        GUIUtility.RotateAroundPivot(-7, c);
        Box(new Rect(-200, 260, W + 400, 140), A(splashCol, 0.85f * a));
        Box(new Rect(-200, 252, W + 400, 6), A(Color.white, a));
        Box(new Rect(-200, 402, W + 400, 6), A(Color.white, a));
        GUIUtility.ScaleAroundPivot(new Vector2(s, s), c);
        Txt(new Rect(0, 270, W, 120), splash, 100, A(new Color(1, 0.95f, 0.4f), a));
        GUI.matrix = mtx;
    }

    void DrawParticles()
    {
        foreach (var p in parts)
        {
            float a = Mathf.Clamp01(p.life / p.max * 1.5f);
            var tex = p.kind == 1 ? white : p.kind == 2 ? ring : soft;
            GUI.DrawTexture(Around(p.p, p.size, p.size), tex, ScaleMode.StretchToFill, true, 0, A(p.c, a), 0, p.kind == 1 ? 2 : 0);
        }
    }

    void DrawFloats()
    {
        foreach (var f in floats)
        {
            float a = Mathf.Clamp01((1.4f - f.t) / 0.4f);
            float s = f.t < 0.12f ? Mathf.Lerp(1.8f, 1, f.t / 0.12f) : 1;
            Txt(new Rect(f.p.x - 160, f.p.y - 30, 320, 60), f.s, 26 * s, A(f.c, a));
        }
    }

    static float Rnd(float a, float b) => UnityEngine.Random.Range(a, b);

    void Emit(Vfx v, Vector2 from, Vector2 to, float power)
    {
        v ??= new Vfx { c1 = "#ffd166", c2 = "#ffffff", pattern = "burst" };
        Color c1 = Col(v.c1, Color.yellow), c2 = Col(v.c2, Color.white);
        int n = Mathf.Clamp((int)(40 * power), 8, 160);
        Color C() => Color.Lerp(c1, c2, UnityEngine.Random.value);
        if ((from - to).sqrMagnitude > 100)
            for (int i = 0; i < 22; i++)
            {
                var p = Vector2.Lerp(from, to, i / 22f);
                parts.Add(new P { p = p, v = UnityEngine.Random.insideUnitCircle * 40, c = C(), size = Rnd(10, 22), life = 0.25f + i * 0.012f, max = 0.5f, drag = 2 });
            }
        switch (v.pattern)
        {
            case "spiral":
                for (int i = 0; i < n; i++)
                {
                    float ang = i * 0.5f, sp = 60 + i * 4;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    parts.Add(new P { p = to, v = dir * sp + new Vector2(-dir.y, dir.x) * 220, c = C(), size = Rnd(8, 20), life = Rnd(0.5f, 1f), max = 1, drag = 2.5f });
                }
                break;
            case "rain":
                for (int i = 0; i < n; i++)
                    parts.Add(new P { p = to + new Vector2(Rnd(-150, 150), Rnd(-320, -160)), v = new Vector2(Rnd(-20, 20), Rnd(500, 800)), c = C(), size = Rnd(8, 16), life = 0.5f, max = 0.5f, kind = 1 });
                break;
            case "beam":
                for (int i = 0; i < n; i++)
                    parts.Add(new P { p = to, v = UnityEngine.Random.insideUnitCircle.normalized * Rnd(200, 600), c = C(), size = Rnd(6, 14), life = Rnd(0.3f, 0.6f), max = 0.6f, drag = 3 });
                parts.Add(new P { p = to, c = Color.white, size = 120 * power, life = 0.2f, max = 0.2f });
                break;
            case "shockwave":
                for (int i = 0; i < 3; i++)
                    parts.Add(new P { p = to, c = i == 0 ? c1 : c2, size = 30, grow = 700 + i * 250, life = 0.55f, max = 0.55f, kind = 2 });
                for (int i = 0; i < n / 2; i++)
                    parts.Add(new P { p = to, v = UnityEngine.Random.insideUnitCircle.normalized * Rnd(250, 500), c = C(), size = Rnd(8, 16), life = 0.5f, max = 0.5f, drag = 2 });
                break;
            case "confetti":
                for (int i = 0; i < n; i++)
                    parts.Add(new P { p = to, v = new Vector2(Rnd(-380, 380), Rnd(-650, -200)), c = UnityEngine.Random.value < 0.5f ? C() : Color.HSVToRGB(UnityEngine.Random.value, 0.75f, 1), size = Rnd(7, 13), life = Rnd(1f, 1.8f), max = 1.8f, grav = 900, drag = 0.8f, kind = 1 });
                break;
            default:
                for (int i = 0; i < n; i++)
                    parts.Add(new P { p = to, v = UnityEngine.Random.insideUnitCircle * 520, c = C(), size = Rnd(8, 24), life = Rnd(0.4f, 0.9f), max = 0.9f, drag = 3 });
                break;
        }
        if (parts.Count > 1200) parts.RemoveRange(0, parts.Count - 1200);
    }

    void Shatter(Unit u)
    {
        var c = FrameColor(u.def, !u.owner.isPlayer);
        for (int i = 0; i < 36; i++)
            parts.Add(new P { p = u.pos + new Vector2(Rnd(-40, 40), Rnd(-55, 55)), v = new Vector2(Rnd(-260, 260), Rnd(-380, -60)), c = i % 3 == 0 ? Color.white : c, size = Rnd(8, 16), life = Rnd(0.6f, 1.1f), max = 1.1f, grav = 1100, kind = 1 });
        Float(u.pos, u.def.name + " leaves the stand", new Color(1, 0.8f, 0.7f));
    }

    // ---------- procedural audio ----------
    readonly Dictionary<string, AudioClip> clips = new();
    AudioSource[] srcs;
    int srcI;

    void InitAudio()
    {
        srcs = new AudioSource[8];
        for (int i = 0; i < srcs.Length; i++) srcs[i] = gameObject.AddComponent<AudioSource>();
        var rng = new System.Random(7);
        float N() => (float)(rng.NextDouble() * 2 - 1);
        const float TAU = Mathf.PI * 2;
        clips["card"] = Synth(0.09f, t => N() * Mathf.Exp(-t * 55) * 0.6f);
        clips["hit"] = Synth(0.3f, t => Mathf.Sin(TAU * (110 * t - 120 * t * t)) * Mathf.Exp(-t * 11) + N() * 0.35f * Mathf.Exp(-t * 35));
        clips["zap"] = Synth(0.35f, t => Mathf.Sign(Mathf.Sin(TAU * (300 * t + 1800 * t * t))) * 0.35f * Mathf.Exp(-t * 7));
        clips["chime"] = Synth(0.8f, t => (Mathf.Sin(TAU * 1046 * t) + Mathf.Sin(TAU * 1318 * t) * 0.7f + Mathf.Sin(TAU * 1568 * t) * 0.5f) * 0.3f * Mathf.Exp(-t * 4.5f));
        float lp = 0;
        clips["boom"] = Synth(0.9f, t => { lp += (N() - lp) * 0.05f; return (lp * 3 + Mathf.Sin(TAU * 55 * t) * 0.8f) * Mathf.Exp(-t * 4); });
        clips["forge"] = Synth(0.4f, t => (Mathf.Sin(TAU * 1210 * t) + Mathf.Sin(TAU * 1733 * t) * 0.8f + Mathf.Sin(TAU * 2511 * t) * 0.6f + N() * 0.4f * Mathf.Exp(-t * 60)) * 0.3f * Mathf.Exp(-t * 9));
        float lp2 = 0;
        clips["whoosh"] = Synth(0.5f, t => { lp2 += (N() - lp2) * (0.02f + 0.2f * t); return lp2 * 2.5f * Mathf.Sin(Mathf.PI * t / 0.5f); });
    }

    static AudioClip Synth(float len, Func<float, float> f)
    {
        const int sr = 22050;
        var d = new float[(int)(len * sr)];
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(f(i / (float)sr), -1, 1) * 0.6f;
        var c = AudioClip.Create("sfx", d.Length, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    void Sfx(string n, float pitch)
    {
        if (srcs == null || !clips.TryGetValue(n, out var c)) return;
        var s = srcs[srcI++ % srcs.Length];
        s.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        s.PlayOneShot(c, 0.8f);
    }
}
