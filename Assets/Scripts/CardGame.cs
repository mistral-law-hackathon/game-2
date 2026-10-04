using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Flow, networking and screens. Rules/AI in CardGame.Logic.cs, rendering/VFX/audio in CardGame.Draw.cs.
public partial class CardGame : MonoBehaviour
{
    enum Mode { Loading, Title, Intro, Duel, Reward, Hall }
    enum ForgeSource { Button, Card, Reward }

    Mode mode = Mode.Loading;
    Content content;
    readonly Dictionary<string, CardDef> cardsById = new();
    string loadError;
    List<CardDef> playerDeck = new();
    readonly List<CardDef> forgedCards = new();
    int bossIndex;
    BossDef Boss => content.bosses[bossIndex];
    int statTurns, statDamage, statCards;

    string bubble = "";
    float bubbleT = 99;
    bool tauntBusy;

    bool forgeOpen;
    ForgeSource forgeSrc;
    int forgePhase;
    string forgeText = "", forgeAnnounce = "", forgeErr = "";
    CardDef forged;
    float forgeT;
    bool forgeFocus;
    static readonly string[] ForgeIdeas =
    {
        "a pigeon that testifies against everyone", "grandma's lasagna as evidence", "a lawyer made of 1000 bees",
        "the Eiffel Tower as a surprise witness", "a time-travelling judge", "my cat walked on the keyboard"
    };

    List<CardDef> rewardChoices = new();

    static string Base
    {
        get
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || string.IsNullOrEmpty(Application.absoluteURL))
                return "http://localhost:8080";
            return new Uri(Application.absoluteURL).GetLeftPart(UriPartial.Authority);
        }
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        InitGfx();
        InitAudio();
        StartCoroutine(LoadContent());
    }

    IEnumerator LoadContent()
    {
        using var req = UnityWebRequest.Get(Base + "/api/content");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { loadError = "Could not reach the game server: " + req.error; yield break; }
        content = JsonUtility.FromJson<Content>(req.downloadHandler.text);
        foreach (var c in content.cards) cardsById[c.id] = c;
        cardsById[content.token.id] = content.token;
        ResetRun();
        mode = Mode.Title;
    }

    IEnumerator Post<T>(string path, object body, Action<T> done) where T : class
    {
        using var req = new UnityWebRequest(Base + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 40;
        yield return req.SendWebRequest();
        T result = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { result = JsonUtility.FromJson<T>(req.downloadHandler.text); } catch (Exception e) { Debug.LogWarning(e); }
        }
        done(result);
    }

    void ResetRun()
    {
        playerDeck = content.starter.Select(id => cardsById[id]).ToList();
        forgedCards.Clear();
        bossIndex = 0;
        statTurns = statDamage = statCards = 0;
    }

    void Taunt(string ev, string detail)
    {
        if (tauntBusy || content == null) return;
        tauntBusy = true;
        var fallback = Boss.lines.Get(ev);
        var req = new TauntReq
        {
            boss = Boss.id, @event = ev, detail = detail,
            boss_hp = enemy?.hp ?? Boss.hp, player_hp = player?.hp ?? 30
        };
        StartCoroutine(Post<TauntResp>("/api/taunt", req, r =>
        {
            tauntBusy = false;
            Say(r != null && !string.IsNullOrEmpty(r.line) ? r.line : fallback);
        }));
    }

    void Say(string s) { bubble = s ?? ""; bubbleT = 0; }

    void EnterIntro()
    {
        mode = Mode.Intro;
        player = enemy = null;
        Say(Boss.lines.start);
        Taunt("start", "A rookie lawyer just walked into your courtroom to duel you. Introduce yourself menacingly.");
    }

    // ---------- Forge ----------
    void OpenForge(ForgeSource src)
    {
        forgeOpen = true; forgeSrc = src; forgePhase = 0; forgeText = ""; forged = null; forgeErr = ""; forgeFocus = true;
        Sfx("forge", 0.8f);
    }

    void SubmitForge()
    {
        if (string.IsNullOrWhiteSpace(forgeText) || forgePhase != 0) return;
        forgePhase = 1; forgeT = 0; forgeErr = "";
        Sfx("forge", 1f);
        var idea = forgeText.Trim();
        StartCoroutine(Post<ForgeResp>("/api/forge", new ForgeReq { prompt = idea }, r =>
        {
            if (r?.card == null) { forgePhase = 0; forgeErr = "The Forge sputtered. Try again!"; return; }
            forged = r.card;
            forged.kw ??= new List<string>();
            forged.fx ??= new List<Fx>();
            forgeAnnounce = r.announce;
            forgePhase = 2; forgeT = 0;
            cardsById[forged.id] = forged;
            forgedCards.Add(forged);
            var mid = new Vector2(640, 330);
            Emit(forged.vfx, mid, mid, 2.5f);
            Emit(new Vfx { c1 = "#ffffff", c2 = forged.vfx.c1, pattern = "shockwave" }, mid, mid, 1.5f);
            Shake(6 + forged.vfx.shake * 14);
            Sfx("boom", forged.vfx.pitch);
            Sfx("chime", 1f);
            if (mode == Mode.Duel || mode == Mode.Reward)
                Taunt("forge", $"The rookie just FORGED a brand new card '{forged.name}' ({forged.text}) from the idea \"{idea}\". React to this specific card.");
        }));
    }

    void AcceptForge()
    {
        forgeOpen = false;
        playerDeck.Add(forged);
        if (forgeSrc == ForgeSource.Reward) { NextBoss(); return; }
        if (player.hand.Count < 10)
        {
            player.hand.Add(forged);
            flyCard = forged; flyT = 0;
        }
    }

    void CancelForge()
    {
        forgeOpen = false;
        if (forgeSrc == ForgeSource.Button) forgeCharges++;
    }

    // ---------- Campaign ----------
    void OpenReward()
    {
        mode = Mode.Reward;
        rewardChoices = content.rewards.OrderBy(_ => UnityEngine.Random.value).Take(2).Select(id => cardsById[id]).ToList();
    }

    void PickReward(CardDef c)
    {
        playerDeck.Add(c);
        NextBoss();
    }

    void NextBoss()
    {
        bossIndex++;
        if (bossIndex >= content.bosses.Count) { bossIndex = content.bosses.Count - 1; mode = Mode.Hall; Sfx("chime", 0.8f); return; }
        EnterIntro();
    }
}
