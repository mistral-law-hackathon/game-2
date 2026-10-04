using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class Verdict : MonoBehaviour
{
    const float W = 1280, H = 720;
    const int Threshold = 60, MaxPick = 2;
    enum Phase { Loading, Menu, Brief, Trial, Closing, Judging, End }
    class Reaction { public string title, body; public int delta; }

    static readonly Color Bg = Hex("0D1524"), Panel = Hex("152038"), Panel2 = Hex("1B2946"), Line = Hex("2C3D60"),
        Gold = Hex("D9B45A"), Ink = Hex("E9EDF5"), Muted = Hex("93A0BC"), Red = Hex("E46A6A"), Green = Hex("4CC38A");

    Phase phase = Phase.Loading;
    Content content;
    CaseDef cs;
    int caseIdx, round;
    float meter, meterShown, phaseT, resolveT, popT = -10;
    int popDelta;
    bool resolved, focusArg;
    readonly List<CardDef> hand = new(), played = new();
    readonly List<string> selected = new(), record = new();
    readonly List<Reaction> reactions = new();
    readonly HashSet<string> won = new();
    string argument = "", loadErr;
    VerdictResp verdict;

    Texture2D white;
    GUIStyle st, area;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static Color A(Color c, float a) { c.a *= a; return c; }

    static string Base
    {
        get
        {
            var u = Application.absoluteURL;
            if (string.IsNullOrEmpty(u)) return "http://localhost:8080";
            int i = u.IndexOf('/', u.IndexOf("//") + 2);
            return i < 0 ? u : u.Substring(0, i);
        }
    }

    void Start()
    {
        white = Texture2D.whiteTexture;
        StartCoroutine(Load());
    }

    IEnumerator Load()
    {
        using var req = UnityWebRequest.Get(Base + "/api/content");
        req.timeout = 15;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { loadErr = "Could not reach the court server: " + req.error; yield break; }
        content = JsonUtility.FromJson<Content>(req.downloadHandler.text);
        Go(Phase.Menu);
    }

    void Go(Phase p) { phase = p; phaseT = 0; }

    void Update()
    {
        phaseT += Time.deltaTime;
        meterShown = Mathf.Lerp(meterShown, meter, 1 - Mathf.Exp(-4 * Time.deltaTime));
    }

    // ---------- game flow ----------
    void StartCase(int i)
    {
        caseIdx = i; cs = content.cases[i]; round = 0; meter = meterShown = cs.start;
        hand.Clear(); hand.AddRange(cs.cards.OrderBy(_ => Random.value));
        played.Clear(); selected.Clear(); reactions.Clear(); record.Clear();
        argument = ""; verdict = null; resolved = false;
        Go(Phase.Brief);
    }

    void Present()
    {
        var mv = cs.moves[round];
        reactions.Clear();
        bool answered = false;
        int total = 0;
        var names = new List<string>();
        foreach (var id in selected)
        {
            var c = hand.First(x => x.id == id);
            bool counter = !string.IsNullOrEmpty(c.counters) && c.counters == mv.id;
            int d = c.power + (counter ? 4 : 0);
            answered |= counter;
            reactions.Add(new Reaction { title = c.name + (counter ? "  -  directly answers the prosecution" : ""), body = c.lesson, delta = d });
            total += d; names.Add(c.name);
            hand.Remove(c); played.Add(c);
        }
        if (answered)
            reactions.Add(new Reaction { title = "Prosecution's point neutralised", body = "You answered \"" + mv.title + "\" head-on, so it no longer sways the jury.", delta = 0 });
        else
        {
            reactions.Add(new Reaction { title = "Prosecution's point stands: " + mv.title, body = "Nothing you presented answered it directly. Tip: look for the card that rebuts this exact point.", delta = -mv.power });
            total -= mv.power;
        }
        meter = Mathf.Clamp(meter + total, 0, 100);
        popDelta = total; popT = Time.time;
        record.Add("Round " + (round + 1) + ": " + string.Join(", ", names) + "|" + total);
        selected.Clear(); resolved = true; resolveT = Time.time;
    }

    void NextRound()
    {
        resolved = false; round++;
        if (round >= cs.moves.Count) { Go(Phase.Closing); focusArg = true; }
    }

    void SubmitClosing()
    {
        Go(Phase.Judging);
        var body = JsonUtility.ToJson(new VerdictReq { caseId = cs.id, cards = played.Select(c => c.id).ToList(), argument = argument.Trim(), meter = Mathf.RoundToInt(meter) });
        StartCoroutine(PostVerdict(body));
    }

    IEnumerator PostVerdict(string body)
    {
        using var req = new UnityWebRequest(Base + "/api/verdict", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 45;
        yield return req.SendWebRequest();
        VerdictResp r = null;
        if (req.result == UnityWebRequest.Result.Success) r = JsonUtility.FromJson<VerdictResp>(req.downloadHandler.text);
        r ??= new VerdictResp { headline = "The judge is unavailable", feedback = "The AI judge could not be reached, so your closing was not scored." };
        r.strengths ??= new List<string>(); r.missed ??= new List<string>();
        verdict = r;
        meter = Mathf.Clamp(meter + r.score, 0, 100);
        popDelta = r.score; popT = Time.time;
        if (meter >= Threshold) won.Add(cs.id);
        Go(Phase.End);
    }

    // ---------- drawing helpers ----------
    void Box(Rect r, Color c, float rad = 0) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, 0, rad);
    void Border(Rect r, Color c, float w, float rad = 0) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, w, rad);
    bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

    void Txt(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, FontStyle f = FontStyle.Normal)
    {
        st.fontSize = size; st.normal.textColor = c; st.alignment = a; st.fontStyle = f;
        GUI.Label(r, s, st);
    }

    float TxtH(string s, int size, float w, FontStyle f = FontStyle.Normal)
    {
        st.fontSize = size; st.fontStyle = f;
        return st.CalcHeight(new GUIContent(s), w);
    }

    // draws wrapped text at y, returns the y below it
    float Para(float x, float y, float w, string s, int size, Color c, FontStyle f = FontStyle.Normal, float gap = 6)
    {
        float h = TxtH(s, size, w, f);
        Txt(new Rect(x, y, w, h), s, size, c, TextAnchor.UpperLeft, f);
        return y + h + gap;
    }

    bool Btn(Rect r, string label, bool primary = true, bool on = true)
    {
        bool h = on && Hover(r);
        if (primary) Box(r, !on ? Panel2 : h ? Color.Lerp(Gold, Color.white, 0.2f) : Gold, 8);
        else { Box(r, h ? Line : Panel2, 8); Border(r, h ? Gold : Line, 1.5f, 8); }
        Txt(r, label, 16, !on ? Muted : primary ? Bg : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        return on && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void PanelBox(Rect r, string header = null)
    {
        Box(r, Panel, 12);
        Border(r, Line, 1, 12);
        if (header != null) Txt(new Rect(r.x + 20, r.y + 16, r.width - 40, 20), header, 12, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
    }

    static Color KindColor(string k) => k switch
    {
        "Principle" => Hex("D9B45A"),
        "Evidence" => Hex("5AA9E6"),
        "Witness" => Hex("B48CF0"),
        _ => Hex("E39A5B"),
    };

    static string Signed(int d) => d > 0 ? "+" + d : d.ToString();

    // ---------- OnGUI ----------
    void OnGUI()
    {
        if (st == null)
        {
            st = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            area = new GUIStyle(GUI.skin.textArea) { fontSize = 18, wordWrap = true, padding = new RectOffset(18, 18, 16, 16) };
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, Hex("0F1A2E")); t.Apply();
            foreach (var s in new[] { area.normal, area.focused, area.hover, area.active }) { s.background = t; s.textColor = Ink; }
        }
        GUI.matrix = Matrix4x4.identity;
        Box(new Rect(0, 0, Screen.width, Screen.height), Bg);
        float sc = Mathf.Min(Screen.width / W, Screen.height / H);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * sc) / 2, (Screen.height - H * sc) / 2, 0), Quaternion.identity, new Vector3(sc, sc, 1));

        switch (phase)
        {
            case Phase.Loading: DrawLoading(); break;
            case Phase.Menu: DrawMenu(); break;
            case Phase.Brief: DrawBrief(); break;
            case Phase.Trial: DrawTrial(); break;
            case Phase.Closing: DrawClosing(); break;
            case Phase.Judging: DrawJudging(); break;
            case Phase.End: DrawEnd(); break;
        }
    }

    void DrawLoading()
    {
        Txt(new Rect(0, 300, W, 70), "VERDICT", 56, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 380, W, 30), loadErr ?? "Preparing the courtroom...", 18, loadErr != null ? Red : Muted, TextAnchor.MiddleCenter);
    }

    void DrawMenu()
    {
        Txt(new Rect(0, 56, W, 70), "VERDICT", 64, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        Box(new Rect(W / 2 - 40, 132, 80, 2), Gold);
        Txt(new Rect(0, 150, W, 30), "You are the defence lawyer. Answer the prosecution with real law, then win the jury.", 20, Ink, TextAnchor.MiddleCenter);
        Txt(new Rect(0, 180, W, 24), "Learn French criminal law in 10 minutes  -  closing arguments judged live by Mistral AI", 15, Muted, TextAnchor.MiddleCenter);

        float tw = 360, gap = 30, x0 = (W - (3 * tw + 2 * gap)) / 2;
        for (int i = 0; i < content.cases.Count && i < 3; i++)
        {
            var c = content.cases[i];
            var r = new Rect(x0 + i * (tw + gap), 236, tw, 290);
            bool h = Hover(r);
            Box(r, h ? Panel2 : Panel, 12);
            Border(r, h ? Gold : Line, h ? 2 : 1, 12);
            float x = r.x + 24, w = r.width - 48;
            Txt(new Rect(x, r.y + 22, w, 18), "CASE " + (i + 1), 12, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            if (won.Contains(c.id)) Txt(new Rect(x, r.y + 22, w, 18), "ACQUITTED", 12, Green, TextAnchor.UpperRight, FontStyle.Bold);
            float y = Para(x, r.y + 46, w, c.title, 26, Ink, FontStyle.Bold, 10);
            y = Para(x, y, w, c.charge, 16, Ink);
            y = Para(x, y, w, c.law, 14, Gold, FontStyle.Normal, 14);
            Para(x, y, w, "Client: " + c.client, 14, Muted);
            if (Btn(new Rect(x, r.yMax - 64, w, 44), "DEFEND THIS CLIENT", true)) StartCase(i);
        }

        string[] steps = { "Read the case file and the charge", "Each round, answer the prosecution with up to 2 law cards", "Deliver a closing argument in your own words - the AI judge scores it" };
        float sw = 360;
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(x0 + i * (sw + gap), 560, sw, 90);
            Box(r, A(Panel, 0.6f), 10);
            Box(new Rect(r.x + 18, r.y + 25, 40, 40), A(Gold, 0.15f), 20);
            Txt(new Rect(r.x + 18, r.y + 25, 40, 40), (i + 1).ToString(), 20, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Txt(new Rect(r.x + 72, r.y + 10, r.width - 90, r.height - 20), steps[i], 15, Ink, TextAnchor.MiddleLeft);
        }
        Txt(new Rect(0, 672, W, 24), "Goal: get at least " + Threshold + "% of the jury to vote NOT GUILTY.", 14, Muted, TextAnchor.MiddleCenter);
    }

    void DrawBrief()
    {
        var left = new Rect(70, 60, 640, 600);
        Txt(new Rect(left.x, left.y, 400, 20), "CASE FILE  -  CASE " + (caseIdx + 1), 13, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
        float y = Para(left.x, left.y + 28, left.width, cs.title, 44, Ink, FontStyle.Bold, 8);
        y = Para(left.x, y, left.width, "Your client: " + cs.client, 18, Muted, FontStyle.Normal, 26);
        Txt(new Rect(left.x, y, 300, 20), "THE FACTS", 13, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 30;
        foreach (var f in cs.facts)
        {
            Box(new Rect(left.x, y + 9, 6, 6), Gold, 3);
            y = Para(left.x + 20, y, left.width - 20, f, 18, Ink, FontStyle.Normal, 12);
        }

        var right = new Rect(760, 60, 450, 520);
        PanelBox(right, "THE CHARGE");
        float x = right.x + 24, w = right.width - 48;
        y = Para(x, right.y + 46, w, cs.charge, 24, Ink, FontStyle.Bold, 4);
        y = Para(x, y, w, cs.law, 15, Gold, FontStyle.Normal, 12);
        y = Para(x, y, w, cs.definition, 16, Muted, FontStyle.Italic, 26);
        Box(new Rect(x, y, w, 1), Line);
        y += 18;
        Txt(new Rect(x, y, w, 20), "YOUR OBJECTIVE", 12, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
        y = Para(x, y + 26, w, "The jury starts at " + cs.start + "% Not guilty. Bring it to " + Threshold + "% or more.", 17, Ink, FontStyle.Bold, 10);
        Para(x, y, w, "3 rounds: the prosecution makes a point, you answer with up to 2 cards. Then you deliver your closing argument.", 15, Muted);

        if (Btn(new Rect(760, 600, 300, 52), "ENTER THE COURTROOM", true)) { Go(Phase.Trial); resolved = false; }
        if (Btn(new Rect(1076, 600, 134, 52), "Back", false)) Go(Phase.Menu);
    }

    void TopBar()
    {
        Box(new Rect(0, 0, W, 52), Panel);
        Box(new Rect(0, 52, W, 1), Line);
        Txt(new Rect(24, 0, 200, 52), "VERDICT", 20, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        Txt(new Rect(0, 0, W, 52), cs.title + "   |   " + cs.charge + " (" + cs.law + ")", 15, Ink, TextAnchor.MiddleCenter);
        string right = phase == Phase.Trial ? "ROUND " + (round + 1) + " / " + cs.moves.Count : phase == Phase.End ? "VERDICT" : "CLOSING";
        Txt(new Rect(W - 224, 0, 200, 52), right, 15, Gold, TextAnchor.MiddleRight, FontStyle.Bold);
    }

    void DrawMeter(Rect r)
    {
        Txt(new Rect(r.x, r.y, 200, 20), "JURY", 12, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
        bool ok = meterShown >= Threshold - 0.5f;
        Txt(new Rect(r.x, r.y - 6, r.width, 30), Mathf.RoundToInt(meterShown) + "% NOT GUILTY", 22, ok ? Green : Ink, TextAnchor.UpperRight, FontStyle.Bold);
        var track = new Rect(r.x, r.y + 30, r.width, 14);
        Box(track, Line, 7);
        float fw = track.width * Mathf.Clamp01(meterShown / 100f);
        if (fw > 2) Box(new Rect(track.x, track.y, fw, track.height), ok ? Green : Gold, 7);
        float tx = track.x + track.width * Threshold / 100f;
        Box(new Rect(tx - 1, track.y - 6, 2, track.height + 12), Ink);
        Txt(new Rect(r.x, track.yMax + 6, 200, 18), "GUILTY", 11, Red, TextAnchor.UpperLeft, FontStyle.Bold);
        Txt(new Rect(tx - 100, track.yMax + 6, 200, 18), "acquittal " + Threshold + "%", 11, Ink, TextAnchor.UpperCenter);
        Txt(new Rect(r.xMax - 200, track.yMax + 6, 200, 18), "NOT GUILTY", 11, Green, TextAnchor.UpperRight, FontStyle.Bold);
        float age = Time.time - popT;
        if (age < 2.2f)
            Txt(new Rect(r.xMax + 12, r.y - 4 - age * 14, 90, 40), Signed(popDelta) + "%", 26, A(popDelta >= 0 ? Green : Red, 1 - age / 2.2f), TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    void CaseFilePanel(Rect r)
    {
        PanelBox(r, "CASE FILE");
        float x = r.x + 18, w = r.width - 36;
        float y = Para(x, r.y + 42, w, "Client: " + cs.client, 13, Muted, FontStyle.Normal, 12);
        foreach (var f in cs.facts)
        {
            Box(new Rect(x, y + 6, 4, 4), Gold, 2);
            y = Para(x + 12, y, w - 12, f, 13, Ink, FontStyle.Normal, 8);
        }
    }

    void RecordPanel(Rect r)
    {
        PanelBox(r, "COURT RECORD");
        float x = r.x + 18, w = r.width - 36, y = r.y + 42;
        y = Para(x, y, w, "Jury started at " + cs.start + "%", 13, Muted, FontStyle.Normal, 12);
        if (record.Count == 0) Para(x, y, w, "Your arguments will be recorded here.", 13, Muted, FontStyle.Italic);
        foreach (var e in record)
        {
            var p = e.Split('|');
            int d = int.Parse(p[1]);
            Txt(new Rect(x, y, w, 20), Signed(d) + "%", 14, d >= 0 ? Green : Red, TextAnchor.UpperRight, FontStyle.Bold);
            y = Para(x, y, w - 56, p[0], 13, Ink, FontStyle.Normal, 12);
        }
    }

    void DrawTrial()
    {
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        CaseFilePanel(new Rect(20, 72, 300, 388));
        RecordPanel(new Rect(960, 72, 300, 388));

        var mv = cs.moves[round];
        var center = new Rect(340, 160, 600, 236);
        if (!resolved)
        {
            Box(center, Panel, 12);
            Border(center, A(Red, 0.6f), 1.5f, 12);
            Box(new Rect(center.x, center.y + 14, 4, center.height - 28), Red, 2);
            float x = center.x + 26, w = center.width - 52;
            Txt(new Rect(x, center.y + 18, w, 18), "THE PROSECUTION ARGUES", 12, Red, TextAnchor.UpperLeft, FontStyle.Bold);
            Txt(new Rect(x, center.y + 18, w, 18), mv.law, 12, Muted, TextAnchor.UpperRight);
            float y = Para(x, center.y + 44, w, mv.title, 26, Ink, FontStyle.Bold, 8);
            Para(x, y, w, "\"" + mv.text + "\"", 17, Ink, FontStyle.Italic);
            Txt(new Rect(x, center.yMax - 36, w, 20), "If you don't answer it: " + Signed(-mv.power) + "% jury", 14, Red, TextAnchor.UpperLeft, FontStyle.Bold);

            string hint = round == 0
                ? "Select up to 2 cards below. A card that rebuts this exact point neutralises it. Weak law backfires!"
                : "Select up to 2 cards, then present them to the court.";
            Txt(new Rect(340, 400, 600, 20), hint, 13, Gold, TextAnchor.MiddleCenter);
            if (Btn(new Rect(520, 424, 240, 42), "PRESENT " + selected.Count + " / " + MaxPick + " CARDS", true, selected.Count > 0)) Present();
        }
        else
        {
            float age = Time.time - resolveT;
            Txt(new Rect(center.x, center.y - 4, center.width, 20), "THE JURY REACTS", 12, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            float y = center.y + 22;
            for (int i = 0; i < reactions.Count; i++)
            {
                float a = Mathf.Clamp01((age - i * 0.45f) / 0.3f);
                if (a <= 0) break;
                var re = reactions[i];
                float bh = TxtH(re.body, 13, center.width - 96);
                var row = new Rect(center.x, y, center.width, 30 + bh);
                Box(row, A(Panel, a), 10);
                var col = re.delta > 0 ? Green : re.delta < 0 ? Red : Muted;
                Box(new Rect(row.x + 12, row.y + 10, 56, 28), A(col, 0.18f * a), 6);
                Txt(new Rect(row.x + 12, row.y + 10, 56, 28), re.delta == 0 ? "0" : Signed(re.delta), 16, A(col, a), TextAnchor.MiddleCenter, FontStyle.Bold);
                Txt(new Rect(row.x + 82, row.y + 7, row.width - 96, 20), re.title, 14, A(Ink, a), TextAnchor.UpperLeft, FontStyle.Bold);
                Txt(new Rect(row.x + 82, row.y + 26, row.width - 96, bh), re.body, 13, A(Muted, a));
                y += row.height + 6;
            }
            if (age > reactions.Count * 0.45f && Btn(new Rect(520, 424, 240, 42), round + 1 < cs.moves.Count ? "NEXT ROUND" : "CLOSING ARGUMENT", true)) NextRound();
        }
        DrawHand();
    }

    void DrawHand()
    {
        const float cw = 148, ch = 226, gap = 8;
        int n = hand.Count;
        float total = n * cw + (n - 1) * gap, x0 = (W - total) / 2;
        var e = Event.current;
        for (int i = 0; i < n; i++)
        {
            var c = hand[i];
            bool sel = selected.Contains(c.id);
            var baseR = new Rect(x0 + i * (cw + gap), 484, cw, ch);
            bool h = !resolved && Hover(baseR);
            var r = baseR; r.y -= sel ? 16 : h ? 6 : 0;
            float dim = resolved ? 0.45f : 1f;
            Box(r, A(sel ? Hex("22335A") : Panel2, 1), 10);
            Border(r, sel ? Gold : h ? Muted : Line, sel ? 2.5f : 1, 10);
            var kc = KindColor(c.kind);
            Box(new Rect(r.x + 1, r.y + 1, r.width - 2, 4), A(kc, dim), 2);
            float x = r.x + 12, w = r.width - 24;
            Txt(new Rect(x, r.y + 12, w, 16), c.kind.ToUpper(), 10, A(kc, dim), TextAnchor.UpperLeft, FontStyle.Bold);
            float y = Para(x, r.y + 30, w, c.name, 15, A(Ink, dim), FontStyle.Bold, 4);
            y = Para(x, y, w, c.law, 11, A(Gold, dim), FontStyle.Normal, 8);
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 8, w, c.plain, 12, A(Muted, dim));
            if (sel) Txt(new Rect(r.x, r.yMax - 24, r.width, 18), "SELECTED", 10, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (h && e.type == EventType.MouseDown && e.button == 0)
            {
                if (sel) selected.Remove(c.id);
                else if (selected.Count < MaxPick) selected.Add(c.id);
                e.Use();
            }
        }
    }

    void DrawClosing()
    {
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        var p = new Rect(140, 166, 1000, 530);
        PanelBox(p, "CLOSING ARGUMENT");
        float x = p.x + 30, w = p.width - 60;
        float y = Para(x, p.y + 44, w, "Address the jury in your own words.", 26, Ink, FontStyle.Bold, 6);
        y = Para(x, y, w, "Explain which legal condition of the charge is missing (or which defence applies), link it to the facts and cite the article. Mistral AI plays the judge: a strong closing can swing the jury by up to +15%, a wrong one costs you.", 15, Muted, FontStyle.Normal, 8);
        y = Para(x, y, w, "Law on your side: " + string.Join("  -  ", played.Where(c => c.power > 0).Select(c => c.name + " (" + c.law + ")")), 13, Gold, FontStyle.Normal, 12);
        var ta = new Rect(x, y, w, p.yMax - y - 80);
        Border(Grow(ta, 1), Line, 1, 8);
        GUI.SetNextControlName("arg");
        argument = GUI.TextArea(ta, argument, 1500, area);
        if (focusArg) { GUI.FocusControl("arg"); if (GUI.GetNameOfFocusedControl() == "arg") focusArg = false; }
        if (string.IsNullOrEmpty(argument))
            Txt(new Rect(ta.x + 18, ta.y + 16, ta.width - 36, 60), "e.g. \"Members of the jury, theft requires fraudulent intent under article 311-1...\"", 17, A(Muted, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
        Txt(new Rect(x, p.yMax - 60, 400, 44), argument.Trim().Length < 20 ? "Write at least a couple of sentences." : argument.Length + " characters", 13, Muted, TextAnchor.MiddleLeft);
        if (Btn(new Rect(p.xMax - 330, p.yMax - 64, 300, 48), "DELIVER TO THE JURY", true, argument.Trim().Length >= 20)) SubmitClosing();
    }

    static Rect Grow(Rect r, float d) => new(r.x - d, r.y - d, r.width + 2 * d, r.height + 2 * d);

    void DrawJudging()
    {
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
        Box(new Rect(W / 2 - 50, 300, 100, 100), A(Gold, 0.15f + 0.2f * pulse), 50);
        Box(new Rect(W / 2 - 22, 328, 44, 44), Gold, 22);
        Txt(new Rect(0, 430, W, 40), "The judge is deliberating" + new string('.', 1 + (int)(Time.time * 2) % 3), 26, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 474, W, 24), "Mistral AI is weighing your closing argument against the facts and the law", 15, Muted, TextAnchor.MiddleCenter);
    }

    void DrawEnd()
    {
        TopBar();
        bool acquit = meter >= Threshold;
        Txt(new Rect(0, 62, W, 70), acquit ? "NOT GUILTY" : "GUILTY", 56, acquit ? Green : Red, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 130, W, 22), acquit ? "Your client walks free." : "The jury was not convinced - see what you can learn below, then try again.", 16, Muted, TextAnchor.MiddleCenter);
        DrawMeter(new Rect(340, 166, 600, 70));

        var l = new Rect(60, 250, 570, 400);
        PanelBox(l, "THE JUDGE ON YOUR CLOSING");
        float x = l.x + 24, w = l.width - 48;
        var col = verdict.score > 0 ? Green : verdict.score < 0 ? Red : Muted;
        Box(new Rect(x, l.y + 44, 64, 34), A(col, 0.18f), 6);
        Txt(new Rect(x, l.y + 44, 64, 34), Signed(verdict.score) + "%", 17, col, TextAnchor.MiddleCenter, FontStyle.Bold);
        float y = Para(x + 80, l.y + 48, w - 80, verdict.headline, 18, Ink, FontStyle.Bold, 14);
        y = Mathf.Max(y, l.y + 92);
        y = Para(x, y, w, verdict.feedback, 15, Ink, FontStyle.Normal, 12);
        foreach (var s in verdict.strengths) y = Para(x, y, w, "+  " + s, 14, Green, FontStyle.Normal, 4);
        y += 4;
        foreach (var s in verdict.missed) y = Para(x, y, w, "-  " + s, 14, Red, FontStyle.Normal, 4);

        var r = new Rect(650, 250, 570, 400);
        PanelBox(r, "WHAT YOU LEARNED");
        x = r.x + 24; w = r.width - 48;
        y = Para(x, r.y + 44, w, cs.takeaway, 16, Gold, FontStyle.Bold, 14);
        foreach (var c in played)
        {
            Box(new Rect(x, y + 5, 8, 8), c.power > 0 ? Green : Red, 4);
            y = Para(x + 18, y, w - 18, c.name + " (" + c.law + "): " + c.lesson, 12, Ink, FontStyle.Normal, 6);
        }

        if (Btn(new Rect(W / 2 - 250, 664, 240, 44), "RETRY THIS CASE", false)) StartCase(caseIdx);
        bool more = caseIdx + 1 < content.cases.Count;
        if (Btn(new Rect(W / 2 + 10, 664, 240, 44), more ? "NEXT CASE" : "ALL CASES", true))
        {
            if (more) StartCase(caseIdx + 1); else Go(Phase.Menu);
        }
    }
}
