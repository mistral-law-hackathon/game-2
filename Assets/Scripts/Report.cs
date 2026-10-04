using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// End-of-game case analysis (Mistral debrief grounded in passages retrieved by server/rag.py) + law-themed drawing helpers.
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
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
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
        if (ok) Box(Grow(r, 2 + 1.5f * Mathf.Sin(Time.time * 3)), A(Brass, 0.22f), 7);
        if (Btn(r, label, false, ok))
        {
            shownReport = rep; reportBack = phase; repScroll = Vector2.zero;
            phase = Phase.Report; phaseT = 0;
            Play("select");
        }
        if (ok) Border(r, A(Brass, 0.8f), 1, 4);
    }

    // ---------- law-themed helpers ----------
    void LineSeg(Vector2 a, Vector2 b, float w, Color c)
    {
        var m = GUI.matrix;
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(ang, a);
        Box(new Rect(a.x, a.y - w / 2, Vector2.Distance(a, b), w), c, w / 2);
        GUI.matrix = m;
    }

    // scales of justice, drawn from primitives; s = overall height
    void Scales(Vector2 c, float s, Color col, float sway = 1)
    {
        float t = Mathf.Sin(Time.time * 0.9f) * 4 * sway * Mathf.Deg2Rad;
        float lw = Mathf.Max(1.2f, s * 0.035f);
        var top = new Vector2(c.x, c.y - s * 0.38f);
        Box(new Rect(c.x - lw / 2, top.y, lw, s * 0.82f), col);
        Box(new Rect(c.x - s * 0.22f, c.y + s * 0.42f, s * 0.44f, lw * 1.6f), col, lw * 0.8f);
        Box(new Rect(c.x - s * 0.07f, c.y + s * 0.36f, s * 0.14f, lw * 1.6f), col, lw * 0.8f);
        float k = s * 0.065f;
        Box(new Rect(c.x - k, top.y - k * 2.1f, k * 2, k * 2), col, k);
        var dir = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * s * 0.46f;
        var l = top - dir; var r = top + dir;
        LineSeg(l, r, lw, col);
        foreach (var e in new[] { l, r })
        {
            var pan = new Vector2(e.x, e.y + s * 0.34f);
            LineSeg(e, pan + new Vector2(-s * 0.15f, 0), lw * 0.6f, col);
            LineSeg(e, pan + new Vector2(s * 0.15f, 0), lw * 0.6f, col);
            Box(new Rect(pan.x - s * 0.17f, pan.y - lw * 0.5f, s * 0.34f, s * 0.07f), col, s * 0.035f);
        }
    }

    void Watermark() => Scales(new Vector2(W - 150, H - 230), 340, A(Brass, 0.035f), 0.3f);

    // seal-style badge: big value + small label in a double ring
    void Seal(Vector2 c, float d, string big, string small, Color col)
    {
        var r = new Rect(c.x - d / 2, c.y - d / 2, d, d);
        Box(r, A(col, 0.08f), d / 2);
        Border(r, A(col, 0.85f), 1.5f, d / 2);
        Border(Grow(r, -6), A(col, 0.35f), 1, d / 2 - 6);
        Txt(new Rect(r.x, r.y + d * 0.2f, d, d * 0.4f), big, Mathf.RoundToInt(Mathf.Min(d * 0.26f, d * 1.25f / Mathf.Max(1, big.Length))), col, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(r.x + 8, r.y + d * 0.58f, d - 16, d * 0.2f), small, 10, A(col, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    // wrapped text with generous line spacing (IMGUI has no line-height setting)
    float ParaLines(float x, float y, float w, string s, int size, Color c, FontStyle f = FontStyle.Normal, float lineMul = 1.6f, float gap = 0)
    {
        if (string.IsNullOrEmpty(s)) return y;
        st.fontSize = size; st.font = FontFor(f); st.fontStyle = FontStyle.Normal;
        float lh = size * lineMul;
        var line = new StringBuilder();
        foreach (var word in s.Split(' '))
        {
            string cand = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && st.CalcSize(new GUIContent(cand)).x > w)
            {
                Txt(new Rect(x, y, w + 40, lh), line.ToString(), size, c, TextAnchor.MiddleLeft, f);
                y += lh; line.Clear(); line.Append(word);
            }
            else { line.Clear(); line.Append(cand); }
        }
        if (line.Length > 0) { Txt(new Rect(x, y, w + 40, lh), line.ToString(), size, c, TextAnchor.MiddleLeft, f); y += lh; }
        return y + gap;
    }

    void Pill(Rect r, string s, Color c)
    {
        Box(r, A(c, 0.12f), r.height / 2);
        Border(r, A(c, 0.7f), 1, r.height / 2);
        Txt(r, s, 11, c, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    float Head(float x, float y, float w, string label, int count = -1)
    {
        Txt(new Rect(x, y, w, 18), "\u00a7  " + label, 12, Brass, TextAnchor.UpperLeft, FontStyle.Bold);
        Box(new Rect(x, y + 26, w, 1), A(Brass, 0.25f));
        return y + 42;
    }

    // one finding: coloured rail, bold title, airy body, source pill
    float ItemCard(float x, float y, float w, ReportItem it, Color rail, string tag = null, Color? tagCol = null)
    {
        if (it == null || string.IsNullOrEmpty(it.title)) return y;
        float pw = 0;
        if (!string.IsNullOrEmpty(it.src) && it.src != "case") pw = 64;
        float tx = x + 22, tw = w - 22 - 16 - pw;
        float y0 = y;
        float yy = ParaLines(tx, y + 14, tw, it.title, 16, Ink, FontStyle.Bold, 1.45f, 2);
        if (tag != null)
        {
            float tw2 = 26 + tag.Length * 7.5f;
            Pill(new Rect(tx, yy + 2, tw2, 20), tag, tagCol ?? Muted);
            yy += 30;
        }
        yy = ParaLines(tx, yy, tw, it.body, 14, Hex("C4C4C0"), FontStyle.Normal, 1.7f, 12);
        var card = new Rect(x, y0, w, yy - y0);
        if (Event.current.type == EventType.Repaint)
        {
            // drawn after measuring, so paint the card behind by re-drawing text on top
        }
        Box(new Rect(x, y0, 3, card.height), rail, 1.5f);
        if (pw > 0) Pill(new Rect(x + w - pw - 4, y0 + 14, pw, 22), it.src, Brass);
        return yy + 14;
    }

    void DrawReport()
    {
        var rep = shownReport;
        TopBar();
        bool acquit = meter >= Threshold;
        Scales(new Vector2(92, 104), 52, Brass);
        Txt(new Rect(136, 66, 600, 44), "Case analysis", 32, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Txt(new Rect(138, 108, 640, 22), "Mistral AI debrief, grounded in a French criminal law reference", 14, Muted, TextAnchor.MiddleLeft, FontStyle.Italic);

        if (rep != null)
        {
            float sx = W - 92;
            Seal(new Vector2(sx, 100), 84, acquit ? "NOT" : "GUILTY", acquit ? "GUILTY" : "VERDICT", acquit ? Green : Red);
            Seal(new Vector2(sx - 100, 100), 84, Mathf.RoundToInt(meter) + "%", "JURY", Ink);
            if (rep.rounds > 0) Seal(new Vector2(sx - 200, 100), 84, rep.rebutted + "/" + rep.rounds, "REBUTTED", rep.rebutted * 2 >= rep.rounds ? Green : Red);
        }

        var view = new Rect(60, 156, W - 120, 494);
        Box(new Rect(60, 154, W - 120, 1), A(Brass, 0.3f));
        var ev = Event.current;
        if (ev.type == EventType.ScrollWheel && view.Contains(ev.mousePosition))
        {
            repScroll.y = Mathf.Clamp(repScroll.y + Mathf.Sign(ev.delta.y) * Mathf.Max(70, Mathf.Abs(ev.delta.y) * 25), 0, Mathf.Max(0, repH - view.height));
            ev.Use();
        }
        repScroll = GUI.BeginScrollView(view, repScroll, new Rect(0, 0, view.width - 20, Mathf.Max(repH, view.height)));
        float lw = 620, gap = 56, rx = lw + gap, rw = view.width - 20 - rx;
        float ly = 20, ry = 20;
        if (rep != null)
        {
            ly = Head(0, ly, lw, "HOW THE TRIAL WENT");
            ly = ParaLines(0, ly, lw, rep.summary, 17, Ink, FontStyle.Normal, 1.65f, 24);
            if (rep.mistakes != null && rep.mistakes.Count > 0)
            {
                ly = Head(0, ly, lw, "WHERE IT WENT WRONG");
                foreach (var it in rep.mistakes) ly = ItemCard(0, ly, lw, it, Red);
                ly += 12;
            }
            if (rep.nextTime != null && rep.nextTime.Count > 0)
            {
                ly = Head(0, ly, lw, "NEXT TIME");
                int n = 1;
                foreach (var t in rep.nextTime)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    Txt(new Rect(0, ly, 30, 26), (n++).ToString(), 20, Brass, TextAnchor.MiddleLeft, FontStyle.Bold);
                    ly = ParaLines(30, ly, lw - 30, t, 15, Ink, FontStyle.Normal, 1.7f, 10);
                }
            }

            if (rep.proofs != null && rep.proofs.Count > 0)
            {
                ry = Head(rx, ry, rw, "WHAT HAD TO BE PROVEN");
                foreach (var it in rep.proofs) ry = ItemCard(rx, ry, rw, it, it.ok ? Green : Red, it.ok ? "PROVEN" : "NOT PROVEN", it.ok ? Green : Red);
                ry += 12;
            }
            if (rep.precedents != null && rep.precedents.Count > 0)
            {
                ry = Head(rx, ry, rw, "WHAT IF THE FACTS CHANGED");
                foreach (var it in rep.precedents) ry = ItemCard(rx, ry, rw, it, Brass);
                ry += 12;
            }
            if (rep.sources != null && rep.sources.Count > 0)
            {
                ry = Head(rx, ry, rw, "SOURCES RETRIEVED");
                foreach (var src in rep.sources) ry = ParaLines(rx, ry, rw, src, 12, Muted, FontStyle.Italic, 1.6f, 2);
                ry = ParaLines(rx, ry + 10, rw, "Educational summary, not legal advice. Check Legifrance for current law.", 11, A(Muted, 0.75f), FontStyle.Italic, 1.5f);
            }
        }
        GUI.EndScrollView();
        repH = Mathf.Max(ly, ry) + 20;
        if (repH > view.height && repScroll.y < repH - view.height - 10)
        {
            GUI.DrawTexture(new Rect(view.x, view.yMax - 50, view.width, 50), grad, ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
            Txt(new Rect(view.x, view.yMax - 20, view.width - 24, 18), "scroll for more  \u2193", 11, Muted, TextAnchor.MiddleRight, FontStyle.Italic);
        }
        if (Btn(new Rect(W / 2 - 150, 664, 300, 44), "BACK TO THE VERDICT", true)) { phase = reportBack; Play("select"); }
    }
}
