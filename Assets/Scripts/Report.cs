using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// End-of-game case analysis: Mistral debrief grounded in passages retrieved from the law reference (server/rag.py).
public partial class Verdict
{
    readonly List<RoundRec> rounds = new();
    Report report, shownReport;
    bool reportLoading;
    int reportTok;
    Phase reportBack;
    Vector2 repScroll;
    float repH = 600;

    IEnumerator FetchReport(string body)
    {
        int tok = ++reportTok;
        report = null; reportLoading = true;
        using var req = new UnityWebRequest(Base + "/api/report", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 120;
        yield return req.SendWebRequest();
        if (tok != reportTok) yield break;
        reportLoading = false;
        Report r = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { r = JsonUtility.FromJson<Report>(req.downloadHandler.text); } catch (System.Exception) { r = null; }
        }
        report = r != null && !string.IsNullOrEmpty(r.summary) ? r : new Report { error = "The analysis could not be prepared." };
    }

    void ReportBtn(Rect r, Report rep, bool loading)
    {
        bool ok = rep != null && !string.IsNullOrEmpty(rep.summary);
        string label = loading ? "PREPARING ANALYSIS" + Dots : ok ? "FULL CASE ANALYSIS" : "ANALYSIS UNAVAILABLE";
        if (ok) Box(Grow(r, 2 + 1.5f * Mathf.Sin(Time.time * 3)), A(Accent, 0.18f), 7);
        if (Btn(r, label, false, ok))
        {
            shownReport = rep; reportBack = phase; repScroll = Vector2.zero;
            phase = Phase.Report; phaseT = 0;
            Play("select");
        }
    }

    float Section(float x, float y, float w, string head, List<string> items, Color dot, int size = 15)
    {
        if (items == null || items.Count == 0) return y;
        Txt(new Rect(x, y, w, 18), head, 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 28;
        foreach (var s in items)
        {
            if (string.IsNullOrEmpty(s)) continue;
            Box(new Rect(x, y + 7, 7, 7), dot, 3.5f);
            y = Para(x + 20, y, w - 20, s, size, Ink, FontStyle.Normal, 10);
        }
        return y + 18;
    }

    void DrawReport()
    {
        var rep = shownReport;
        TopBar();
        Txt(new Rect(60, 66, W - 120, 40), "Case analysis", 30, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Txt(new Rect(60, 104, W - 120, 22), "Written by Mistral AI from your trial, grounded in passages retrieved from a French criminal law reference.", 14, Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
        var view = new Rect(60, 138, W - 120, 508);
        Box(view, Panel, 8);
        Border(view, Line, 1, 8);
        float w = view.width - 48 - 20;
        var ev = Event.current;
        if (ev.type == EventType.ScrollWheel)
        {
            repScroll.y = Mathf.Clamp(repScroll.y + ev.delta.y * 30, 0, Mathf.Max(0, repH - view.height + 4));
            ev.Use();
        }
        repScroll = GUI.BeginScrollView(new Rect(view.x + 2, view.y + 2, view.width - 4, view.height - 4), repScroll,
            new Rect(0, 0, view.width - 22, Mathf.Max(repH, view.height - 4)));
        float x = 24, y = 22;
        if (rep != null)
        {
            Txt(new Rect(x, y, w, 18), "HOW THE TRIAL WENT", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            y = Para(x, y + 28, w, rep.summary, 17, Ink, FontStyle.Normal, 26);
            y = Section(x, y, w, "YOUR MISTAKES - AND THE RIGHT REASONING", rep.mistakes, Red);
            y = Section(x, y, w, "WHAT HAD TO BE PROVEN", rep.proofs, Accent);
            y = Section(x, y, w, "COMPARABLE SITUATIONS FROM THE REFERENCE", rep.precedents, Muted);
            y = Section(x, y, w, "NEXT TIME", rep.nextTime, Green);
            if (rep.sources != null && rep.sources.Count > 0)
            {
                Box(new Rect(x, y, w, 1), Line);
                Txt(new Rect(x, y + 14, w, 18), "SOURCES RETRIEVED FROM THE REFERENCE", 11, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
                y = Para(x, y + 38, w, string.Join("   \u00b7   ", rep.sources), 13, Muted, FontStyle.Italic, 10);
                y = Para(x, y, w, "Educational summary - not legal advice. Article numbers and penalties change often; check Legifrance.", 12, A(Muted, 0.8f), FontStyle.Italic, 10);
            }
        }
        GUI.EndScrollView();
        repH = y + 16;
        if (repH > view.height) Txt(new Rect(view.x, view.yMax + 2, view.width, 16), "scroll for more", 11, A(Muted, 0.7f), TextAnchor.MiddleRight, FontStyle.Italic);
        if (Btn(new Rect(W / 2 - 150, 664, 300, 44), "BACK TO THE VERDICT", true)) { phase = reportBack; Play("select"); }
    }
}
