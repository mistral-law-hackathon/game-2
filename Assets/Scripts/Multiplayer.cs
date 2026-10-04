using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Multiplayer: prosecution vs defence on two browsers. The Python server holds the room state; clients poll it.
public partial class Verdict
{
    bool mp, mpBusy;
    string mpRole = "d", lobbyRole = "d", mpCode = "", mpPhase = "", mpErr, mpSel, codeInput = "";
    int lobbyCase, mpPollTok;
    RoomState rs;
    CaseDef mpCase;
    GUIStyle codeSt;

    bool IsPros => mp && mpRole == "p";
    string Opp => mpRole == "p" ? "d" : "p";
    int Rounds => mp ? 3 : cs.moves.Count;
    bool MpSubmitted => mp && rs != null && rs.myClosing;
    bool MpWon => (meter >= Threshold) == (mpRole == "d");
    static string Side(string r) => r == "p" ? "PROSECUTION" : "DEFENCE";
    static string Dots => new string('.', 1 + (int)(Time.time * 2) % 3);

    string BriefGoal => IsPros
        ? "The jury starts at " + cs.start + "% Not guilty. Keep it below " + Threshold + "% to win a conviction."
        : "The jury starts at " + cs.start + "% Not guilty. Bring it to " + Threshold + "% or more.";
    string BriefHow => !mp ? "3 rounds: the prosecution makes a point, you answer with up to 2 cards. Then you deliver your closing argument."
        : IsPros ? "You play the PROSECUTION against a human defence lawyer. Each round you choose an accusation, the defence answers with up to 2 law cards. Then both sides address the jury."
        : "You play the DEFENCE against a human prosecutor. Each round the prosecution chooses an accusation, you answer with up to 2 law cards. Then both sides address the jury.";
    string ClosingTitle => IsPros ? "Ask the jury to convict - type it or speak it." : "Address the jury in your own words - type it or speak it.";
    string ClosingHelp => IsPros
        ? "Show that every legal element of the charge is proven by the facts, and answer the defence. Mistral AI judges both closings: the stronger one can swing the jury by up to 15%."
        : "Explain which legal condition of the charge is missing (or which defence applies), link it to the facts and cite the article. Mistral AI judges both closings: the stronger one can swing the jury by up to 15%.";

    CardDef CardById(string id) => mpCase?.cards.FirstOrDefault(c => c.id == id);
    MoveDef MoveById(string id) => mpCase?.moves.FirstOrDefault(m => m.id == id);

    // ---------- networking ----------
    IEnumerator MpPost(string path, RoomReq body, System.Action<string> done)
    {
        using var req = new UnityWebRequest(Base + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 20;
        yield return req.SendWebRequest();
        done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
    }

    IEnumerator MpEnter(bool create)
    {
        mpBusy = true; mpErr = null;
        string res = null;
        var body = create ? new RoomReq { caseId = content.cases[lobbyCase].id, role = lobbyRole } : new RoomReq { code = codeInput };
        yield return MpPost(create ? "/api/room/create" : "/api/room/join", body, t => res = t);
        mpBusy = false;
        RoomJoin j = null;
        if (res != null) { try { j = JsonUtility.FromJson<RoomJoin>(res); } catch (System.Exception) { j = null; } }
        if (j == null) { mpErr = "Could not reach the court server."; yield break; }
        if (!string.IsNullOrEmpty(j.error) || j.caseDef == null) { mpErr = string.IsNullOrEmpty(j.error) ? "Could not open the room." : j.error; Play("bad", 0.6f); yield break; }
        mp = true; mpRole = j.role; mpCode = j.code; mpCase = j.caseDef; mpPhase = ""; rs = null; mpSel = null;
        Play("select");
        if (create) Go(Phase.MpWait);
        StartCoroutine(MpPoll(++mpPollTok));
    }

    IEnumerator MpPoll(int tok)
    {
        while (mp && tok == mpPollTok)
        {
            using (var req = UnityWebRequest.Get(Base + "/api/room/state?code=" + mpCode + "&role=" + mpRole))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (!mp || tok != mpPollTok) yield break;
                if (req.result == UnityWebRequest.Result.Success) MpApply(req.downloadHandler.text);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void MpAct(string a, string moveId = null, List<string> cards = null, string text = null)
    {
        if (mpBusy || !mp) return;
        StartCoroutine(MpActCo(new RoomReq { code = mpCode, role = mpRole, a = a, moveId = moveId, cards = cards, text = text }));
    }

    IEnumerator MpActCo(RoomReq body)
    {
        mpBusy = true;
        string res = null;
        yield return MpPost("/api/room/act", body, t => res = t);
        mpBusy = false;
        if (res != null) MpApply(res);
    }

    void MpApply(string json)
    {
        RoomState s;
        try { s = JsonUtility.FromJson<RoomState>(json); } catch (System.Exception) { return; }
        if (s == null || !mp) return;
        if (s.phase == "gone") { MpLeave(); mpErr = "The room was closed."; Go(Phase.Lobby); return; }
        if (rs != null && s.v < rs.v) return;
        rs = s;
        string prev = mpPhase;
        mpPhase = s.phase;
        meter = s.meter;
        round = Mathf.Min(s.round, 2);
        record.Clear(); if (s.record != null) record.AddRange(s.record);
        played.Clear(); if (s.played != null) played.AddRange(s.played.Select(CardById).Where(c => c != null));
        if (mpRole == "d")
        {
            hand.Clear(); if (s.dhand != null) hand.AddRange(s.dhand.Select(CardById).Where(c => c != null));
            selected.RemoveAll(id => hand.All(c => c.id != id));
        }
        resolved = !(mpRole == "d" && s.phase == "defend");
        if (prev == s.phase) return;

        switch (s.phase)
        {
            case "brief": StartMpCase(); break;
            case "prosecute":
                if (phase != Phase.MpTrial) Go(Phase.MpTrial);
                mpSel = null; selected.Clear();
                Play(prev == "brief" ? "gavel" : "select", 0.8f);
                break;
            case "defend":
            {
                if (phase != Phase.MpTrial) Go(Phase.MpTrial);
                Play("whoosh", 0.6f);
                var mv = MoveById(s.move);
                if (mv != null) Say("prosecutor", mv.text, 0.3f);
                break;
            }
            case "reveal":
                reactions.Clear();
                if (s.reactions != null) foreach (var r in s.reactions) reactions.Add(new Reaction { title = r.title, body = r.body, delta = r.delta });
                resolveT = Time.time; reactSnd = 0;
                popDelta = reactions.Sum(r => r.delta); popT = Time.time;
                Hush(); Play("gavel", 0.8f);
                break;
            case "closing": Go(Phase.Closing); argument = ""; focusArg = true; break;
            case "judging": Go(Phase.Judging); Play("whoosh", 0.7f); break;
            case "end": MpEnd(); break;
        }
    }

    void StartMpCase()
    {
        cs = mpCase;
        caseIdx = content.cases.FindIndex(c => c.id == cs.id);
        if (caseIdx < 0) caseIdx = 99;
        round = 0; meter = meterShown = cs.start;
        selected.Clear(); reactions.Clear(); argument = ""; verdict = null; mpSel = null;
        if (caseIdx < 3) PlayScene(); else Go(Phase.Brief);
    }

    void MpEnd()
    {
        Go(Phase.MpEnd);
        var v = rs.verdict;
        bool ok = v != null && !string.IsNullOrEmpty(v.headline);
        popDelta = ok ? v.score : 0; popT = Time.time;
        Play("gavel3", 0.9f);
        bool acquit = meter >= Threshold;
        StartCoroutine(Later(0.75f, () => Play(acquit ? "acquit" : "guilty", 0.9f)));
        if (ok) Say("judge", (acquit ? "Not guilty. " : "Guilty. ") + v.headline + ". " + v.lesson, 1.6f);
    }

    void MpLeave()
    {
        if (mp) StartCoroutine(MpPost("/api/room/act", new RoomReq { code = mpCode, role = mpRole, a = "leave" }, _ => { }));
        mp = false; mpPollTok++; rs = null; mpPhase = ""; mpBusy = false;
        if (amb != null) amb.Stop();
        Go(Phase.Menu);
    }

    void OpenLobby()
    {
        mpErr = null;
        if (lobbyCase >= content.cases.Count) lobbyCase = 0;
        Go(Phase.Lobby);
    }

    // ---------- screens ----------
    void DrawLobby()
    {
        if (hero != null) GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.12f), 0, 0);
        Txt(new Rect(0, 30, W, 60), "MULTIPLAYER", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 92, W, 26), "Two players, two screens, one link. The prosecution accuses, the defence answers with the law - Mistral AI judges.", 16, Muted, TextAnchor.MiddleCenter);

        var l = new Rect(90, 140, 530, 500);
        PanelBox(l, "CREATE A ROOM");
        float x = l.x + 26, w = l.width - 52, y = l.y + 48;
        Txt(new Rect(x, y, w, 18), "1.  THE CASE", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 26;
        int n = content.cases.Count;
        for (int i = 0; i < n; i++)
        {
            if (i >= 3 && i < n - 2) continue;
            var c = content.cases[i];
            if (Btn(new Rect(x, y, w, 38), c.title + "   -   " + c.charge, lobbyCase == i, !mpBusy)) { lobbyCase = i; Play("select"); }
            y += 44;
        }
        y += 12;
        Txt(new Rect(x, y, w, 18), "2.  YOUR SIDE", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 26;
        float hw = (w - 12) / 2;
        if (Btn(new Rect(x, y, hw, 40), "PROSECUTION", lobbyRole == "p", !mpBusy)) { lobbyRole = "p"; Play("select"); }
        if (Btn(new Rect(x + hw + 12, y, hw, 40), "DEFENCE", lobbyRole == "d", !mpBusy)) { lobbyRole = "d"; Play("select"); }
        Para(x, y + 52, w, lobbyRole == "p"
            ? "Each round you choose an accusation and push the jury towards GUILTY."
            : "Each round you answer the accusation with law cards and fight for an acquittal.", 13, Muted, FontStyle.Italic);
        if (Btn(new Rect(x, l.yMax - 64, w, 46), mpBusy ? "OPENING..." : "CREATE ROOM", true, !mpBusy)) StartCoroutine(MpEnter(true));

        var r = new Rect(660, 140, 530, 500);
        PanelBox(r, "JOIN A ROOM");
        x = r.x + 26; w = r.width - 52;
        y = Para(x, r.y + 48, w, "Enter the 4-letter code shown on the other player's screen.", 15, Ink, FontStyle.Normal, 18);
        codeSt ??= new GUIStyle(area) { fontSize = 34, font = fBold, alignment = TextAnchor.MiddleCenter };
        var cr = new Rect(r.center.x - 130, y, 260, 72);
        Border(Grow(cr, 1), Line, 1, 8);
        GUI.SetNextControlName("code");
        codeInput = new string(GUI.TextField(cr, codeInput, 4, codeSt).Where(char.IsLetter).ToArray()).ToUpper();
        if (codeInput.Length == 0) Txt(cr, "ABCD", 34, A(Muted, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
        y = cr.yMax + 18;
        if (Btn(new Rect(x, y, w, 46), "JOIN ROOM", true, codeInput.Length == 4 && !mpBusy)) StartCoroutine(MpEnter(false));
        y += 70;
        Box(new Rect(x, y, w, 1), Line);
        y += 18;
        Txt(new Rect(x, y, w, 18), "HOW TO CONNECT", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y = Para(x, y + 26, w, "Both players open the same link in their browser:", 14, Ink, FontStyle.Normal, 6);
        y = Para(x, y, w, Base, 14, Accent, FontStyle.Italic, 10);
        Para(x, y, w, "One player creates a room, the other joins with its code. Each side only sees its own cards.", 14, Muted);

        if (mpErr != null) Txt(new Rect(240, 652, 800, 44), mpErr, 14, Red, TextAnchor.MiddleCenter);
        if (Btn(new Rect(90, 652, 130, 44), "Back", false, !mpBusy)) Go(Phase.Menu);
    }

    void DrawMpWait()
    {
        if (hero != null) GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.12f), 0, 0);
        var p = new Rect(290, 110, 700, 500);
        PanelBox(p, "ROOM CREATED");
        float x = p.x + 40, w = p.width - 80;
        Txt(new Rect(p.x, p.y + 56, p.width, 20), "ROOM CODE", 12, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(p.x, p.y + 80, p.width, 100), string.Join(" ", mpCode.ToCharArray()), 76, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(x, p.y + 196, w, 50), "Ask the other player to open this same link, click MULTIPLAYER and enter the code:", 17, Ink, TextAnchor.MiddleCenter);
        Txt(new Rect(x, p.y + 250, w, 26), Base, 16, Accent, TextAnchor.MiddleCenter, FontStyle.Italic);
        Box(new Rect(x, p.y + 300, w, 1), Line);
        Txt(new Rect(x, p.y + 318, w, 26), "You play the " + Side(mpRole) + "   \u00b7   " + mpCase.title + " (" + mpCase.charge + ")", 15, Ink, TextAnchor.MiddleCenter);
        Txt(new Rect(x, p.y + 352, w, 26), "Waiting for the other player" + Dots, 15, Muted, TextAnchor.MiddleCenter, FontStyle.Italic);
        if (Btn(new Rect(p.center.x - 90, p.yMax - 76, 180, 46), "Cancel", false)) MpLeave();
    }

    void MpBriefButtons()
    {
        bool ready = rs != null && rs.myReady;
        if (Btn(new Rect(760, 600, 300, 52), ready ? "WAITING..." : "I'M READY", true, !ready && !mpBusy && rs != null)) { MpAct("ready"); Play("select"); }
        if (Btn(new Rect(1076, 600, 134, 52), "Leave", false)) MpLeave();
        string note = ready ? "Waiting for the " + Side(Opp).ToLower() + " to be ready" + Dots
            : rs != null && rs.oppReady ? "The " + Side(Opp).ToLower() + " is ready." : "You play the " + Side(mpRole) + "   \u00b7   room " + mpCode;
        Txt(new Rect(760, 662, 450, 22), note, 13, Accent, TextAnchor.MiddleLeft);
    }

    void InfoPanel(Rect r, string label, string title, string body)
    {
        Box(r, Panel, 12);
        Border(r, Line, 1, 12);
        float x = r.x + 26, w = r.width - 52;
        Txt(new Rect(x, r.y + 18, w, 18), label, 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        float y = Para(x, r.y + 48, w, title, 26, Ink, FontStyle.Bold, 10);
        Para(x, y, w, body, 15, Muted);
    }

    void DrawMpTrial()
    {
        if (rs == null || mpCase == null) return;
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        CaseFilePanel(new Rect(28, 76, 284, 380));
        RecordPanel(new Rect(968, 76, 284, 380));
        var center = new Rect(340, 164, 600, 200);
        switch (rs.phase)
        {
            case "prosecute":
                if (IsPros)
                {
                    InfoPanel(center, "YOUR TURN", "Choose an accusation", "Pick one of your " + rs.pCount + " prosecution cards below. A point the defence cannot rebut costs it its full weight. The defence holds " + rs.dCount + " law cards.");
                    if (Btn(new Rect(490, 414, 300, 42), "PRESS THIS ACCUSATION", true, mpSel != null && !mpBusy)) { MpAct("move", moveId: mpSel); mpSel = null; Play("gavel", 0.7f); }
                }
                else InfoPanel(center, "THE PROSECUTION IS PREPARING", "Waiting for the accusation" + Dots, "Study your cards meanwhile: the best answer rebuts the exact point the prosecutor makes.");
                break;
            case "defend":
            {
                var mv = MoveById(rs.move);
                if (mv != null) MovePanel(center, mv);
                if (IsPros) Txt(new Rect(340, 380, 600, 20), "The defence is choosing its answer" + Dots, 13, Muted, TextAnchor.MiddleCenter, FontStyle.Italic);
                else
                {
                    Txt(new Rect(340, 380, 600, 20), "Pick up to 2 cards that rebut this exact point. Weak law backfires.", 13, Accent, TextAnchor.MiddleCenter);
                    if (Btn(new Rect(520, 414, 240, 42), "PRESENT " + selected.Count + " / " + MaxPick + " CARDS", true, selected.Count > 0 && !mpBusy))
                    {
                        MpAct("defend", cards: selected.ToList());
                        selected.Clear();
                    }
                }
                break;
            }
            case "reveal":
            {
                float age = Time.time - resolveT;
                DrawReactions(center, age);
                if (age > 0.4f + reactions.Count * 0.55f)
                {
                    if (rs.myReady) Txt(new Rect(340, 424, 600, 22), "Waiting for the other side" + Dots, 13, Muted, TextAnchor.MiddleCenter, FontStyle.Italic);
                    else if (Btn(new Rect(520, 414, 240, 42), rs.round + 1 < Rounds ? "NEXT ROUND" : "CLOSING ARGUMENTS", true, !mpBusy)) MpAct("ready");
                }
                break;
            }
        }
        if (IsPros) DrawMoveHand(); else DrawHand();
    }

    void DrawMoveHand()
    {
        var moves = (rs.phand ?? new List<string>()).Select(MoveById).Where(m => m != null).ToList();
        const float cw = 190, ch = 226, gap = 14;
        int n = moves.Count;
        float x0 = (W - (n * cw + (n - 1) * gap)) / 2;
        bool active = rs.phase == "prosecute";
        var e = Event.current;
        for (int i = 0; i < n; i++)
        {
            var m = moves[i];
            bool sel = mpSel == m.id;
            var baseR = new Rect(x0 + i * (cw + gap), 484, cw, ch);
            bool h = active && Hover(baseR);
            var r = baseR; r.y -= sel ? 16 : h ? 6 : 0;
            float dim = active ? 1 : 0.45f;
            Box(r, sel ? Hex("2A2A2E") : Panel2, 6);
            Border(r, sel ? Accent : h ? Muted : Line, sel ? 2f : 1, 6);
            Box(new Rect(r.x + 1, r.y + 1, r.width - 2, 4), A(Red, dim), 2);
            float x = r.x + 14, w = r.width - 28;
            Txt(new Rect(x, r.y + 14, w, 16), "ACCUSATION", 10, A(Red, dim), TextAnchor.UpperLeft, FontStyle.Bold);
            Txt(new Rect(x, r.y + 14, w, 16), "-" + m.power + "%", 10, A(Muted, dim), TextAnchor.UpperRight, FontStyle.Bold);
            float y = Para(x, r.y + 34, w, m.title, 15, A(Ink, dim), FontStyle.Bold, 6);
            y = Para(x, y, w, m.law, 11, A(Accent, dim), FontStyle.Normal, 10);
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 10, w, "\"" + m.text + "\"", 12, A(Muted, dim), FontStyle.Italic);
            if (sel) Txt(new Rect(r.x, r.yMax - 22, r.width, 18), "SELECTED", 10, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (h && e.type == EventType.MouseDown && e.button == 0) { mpSel = sel ? null : m.id; Play("select"); e.Use(); }
        }
    }

    void DrawMpEnd()
    {
        if (rs == null) return;
        TopBar();
        bool acquit = meter >= Threshold;
        var v = rs.verdict;
        Txt(new Rect(0, 62, W, 70), acquit ? "NOT GUILTY" : "GUILTY", 56, acquit ? Green : Red, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 130, W, 22), (acquit ? "The defence wins." : "The prosecution wins.") + "   You played the " + Side(mpRole).ToLower() + (MpWon ? " - well argued." : " - see what the judge says below."), 16, Muted, TextAnchor.MiddleCenter);
        DrawMeter(new Rect(340, 166, 600, 70));

        var l = new Rect(60, 250, 570, 400);
        PanelBox(l, "THE JUDGE ON THE CLOSING ARGUMENTS");
        float x = l.x + 24, w = l.width - 48;
        if (v != null && !string.IsNullOrEmpty(v.headline))
        {
            var col = v.score > 0 ? Green : v.score < 0 ? Red : Muted;
            Box(new Rect(x, l.y + 44, 64, 34), A(col, 0.18f), 6);
            Txt(new Rect(x, l.y + 44, 64, 34), Signed(v.score) + "%", 17, col, TextAnchor.MiddleCenter, FontStyle.Bold);
            float y = Para(x + 80, l.y + 48, w - 80, v.headline, 18, Ink, FontStyle.Bold, 14);
            y = Mathf.Max(y, l.y + 96);
            Txt(new Rect(x, y, w, 18), "DEFENCE", 11, Green, TextAnchor.UpperLeft, FontStyle.Bold);
            y = Para(x, y + 20, w, v.defence, 14, Ink, FontStyle.Normal, 16);
            Txt(new Rect(x, y, w, 18), "PROSECUTION", 11, Red, TextAnchor.UpperLeft, FontStyle.Bold);
            Para(x, y + 20, w, v.prosecution, 14, Ink);
        }

        var r = new Rect(650, 250, 570, 400);
        PanelBox(r, "WHAT YOU LEARNED");
        x = r.x + 24; w = r.width - 48;
        float y2 = Para(x, r.y + 44, w, v != null && !string.IsNullOrEmpty(v.lesson) ? v.lesson : cs.takeaway, 16, Accent, FontStyle.Bold, 14);
        foreach (var c in played)
        {
            Box(new Rect(x, y2 + 5, 8, 8), c.power > 0 ? Green : Red, 4);
            y2 = Para(x + 18, y2, w - 18, c.name + " (" + c.law + "): " + c.lesson, 12, Ink, FontStyle.Normal, 6);
        }

        if (Btn(new Rect(W / 2 - 390, 664, 240, 44), "NEW ROOM", false)) { MpLeave(); OpenLobby(); }
        var rp = rs.report;
        bool rpReady = rp != null && !string.IsNullOrEmpty(rp.summary);
        ReportBtn(new Rect(W / 2 - 130, 664, 260, 44), rpReady || (rp != null && !string.IsNullOrEmpty(rp.error)) ? rp : null, !rpReady && (rp == null || string.IsNullOrEmpty(rp.error)));
        if (Btn(new Rect(W / 2 + 150, 664, 240, 44), "MAIN MENU", true)) MpLeave();
    }

    void MpBanner()
    {
        if (rs == null || phase == Phase.Lobby || phase == Phase.MpWait || phase == Phase.MpEnd) return;
        if (!rs.left && !rs.away) return;
        Box(new Rect(0, 52, W, 22), A(Red, 0.35f));
        Txt(new Rect(0, 52, W, 22), rs.left ? "The other player left the room." : "The other player seems disconnected - waiting for them" + Dots, 12, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
    }
}
