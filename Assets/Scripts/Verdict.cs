using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public partial class Verdict : MonoBehaviour
{
    const float W = 1280, H = 720;
    const int Threshold = 60, MaxPick = 2;
    enum Phase { Loading, Menu, Create, Scene, Brief, Trial, Closing, Judging, End, Lobby, MpWait, MpTrial, MpEnd, Report }
    enum Mic { Idle, Starting, Recording, Busy }
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MicStart(string go);
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MicStop();
#else
    static void MicStart(string go) { }
    static void MicStop() { }
#endif
    class Reaction { public string title, body; public int delta; }

    static readonly Color Bg = Hex("0A0A0B"), Panel = Hex("141416"), Panel2 = Hex("1C1C1F"), Line = Hex("2D2D31"),
        Accent = Hex("F2EFE8"), Ink = Hex("ECECEA"), Muted = Hex("8E8E93"), Red = Hex("C4524C"), Green = Hex("5E9E78");

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

    Texture2D white, grad, hero;
    GUIStyle st, area;
    Font fReg, fBold, fIt;
    readonly Dictionary<string, Texture2D> tex = new();
    int reactSnd, voTok, shot;
    bool voPending, voiceOn = true;
    float shotT0, prevLen, sceneEndT = -1, voEnd;
    readonly Dictionary<string, AudioClip> voCache = new();
    AudioSource sfx, amb, vo, fx;
    Mic mic;
    float micT;
    string micErr, desc = "", genErr;
    bool micForCreate, generating, dropFocus;
    readonly Dictionary<string, AudioClip> clips = new();
    const int SR = 44100;

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
        grad = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < 64; i++) grad.SetPixel(0, i, new Color(0, 0, 0, Mathf.SmoothStep(0.9f, 0, i / 63f)));
        grad.Apply();
        hero = Tex("hero");
        BuildSounds();
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
        StartCoroutine(Preload());
    }

    void Go(Phase p)
    {
        Hush();
        if (mic == Mic.Recording) { MicStop(); mic = Mic.Busy; }
        micErr = null; phase = p; phaseT = 0;
    }

    string CaseLabel => caseIdx < 3 ? "CASE " + (caseIdx + 1) : "YOUR CASE";
    static string Look(CaseDef c) => string.IsNullOrEmpty(c.look) ? c.id : c.look;

    void Update()
    {
        SceneTick();
        phaseT += Time.deltaTime;
        meterShown = Mathf.Lerp(meterShown, meter, 1 - Mathf.Exp(-4 * Time.deltaTime));
    }

    // ---------- game flow ----------
    Texture2D Tex(string n)
    {
        if (!tex.TryGetValue(n, out var t)) tex[n] = t = Resources.Load<Texture2D>("Cases/" + n);
        return t;
    }

    void StartCase(int i, bool intro = true)
    {
        caseIdx = i; cs = content.cases[i]; round = 0; meter = meterShown = cs.start;
        hand.Clear(); hand.AddRange(cs.cards.OrderBy(_ => Random.value));
        played.Clear(); selected.Clear(); reactions.Clear(); record.Clear();
        argument = ""; verdict = null; resolved = false;
        rounds.Clear(); report = null; reportLoading = false; reportTok++;
        if (intro) PlayScene(); else Go(Phase.Brief);
    }

    void PlayScene()
    {
        Go(Phase.Scene);
        shot = 0; shotT0 = Time.time; prevLen = 0; sceneEndT = -1;
        amb.clip = clips["drone"]; amb.loop = true; amb.volume = voiceOn ? 0.45f : 0.8f; amb.Play();
        if (cs.scenes != null && cs.scenes.Count > 0) Say("narrator", cs.scenes[0]);
        SceneFx(0);
    }

    void SceneFx(int k)
    {
        if (cs.sfx == null || k >= cs.sfx.Count || string.IsNullOrEmpty(cs.sfx[k])) return;
        float vol = voiceOn ? 0.55f : 0.9f;
        var tags = cs.sfx[k].Split('+');
        for (int i = 0; i < tags.Length; i++)
        {
            if (!clips.TryGetValue("fx_" + tags[i].Trim(), out var c)) continue;
            if (i == 0) fx.PlayOneShot(c, vol);
            else StartCoroutine(Later(1.3f * i, () => { if (phase == Phase.Scene) fx.PlayOneShot(c, vol); }));
        }
    }

    void SceneTick()
    {
        if (phase != Phase.Scene) return;
        if (sceneEndT >= 0) { if (Time.time - sceneEndT > 0.5f) EndScene(); return; }
        float lt = Time.time - shotT0;
        bool talking = voiceOn && (voPending || Time.time < voEnd + 0.15f);
        if (lt < (voiceOn ? 1.2f : 10f / 3f) || (talking && lt < 15)) return;
        if (shot < 2)
        {
            shot++; prevLen = lt; shotT0 = Time.time; Play("whoosh", 0.5f);
            if (cs.scenes != null && shot < cs.scenes.Count) Say("narrator", cs.scenes[shot]);
            SceneFx(shot);
        }
        else sceneEndT = Time.time;
    }

    // ---------- voice (Gradium TTS via /api/tts) ----------
    void Say(string voice, string text, float delay = 0)
    {
        if (!voiceOn || string.IsNullOrEmpty(text)) return;
        int tok = ++voTok;
        if (delay <= 0 && voCache.TryGetValue(voice + "|" + text, out var c)) { PlayVoice(c); return; }
        StartCoroutine(SayCo(voice, text, tok, delay));
    }

    void PlayVoice(AudioClip c)
    {
        voPending = false;
        vo.Stop(); vo.clip = c; vo.Play();
        voEnd = Time.time + c.length;
    }

    IEnumerator Fetch(string voice, string text)
    {
        var key = voice + "|" + text;
        if (voCache.ContainsKey(key)) yield break;
        using var req = UnityWebRequestMultimedia.GetAudioClip(Base + "/api/tts?voice=" + voice + "&text=" + UnityWebRequest.EscapeURL(text), AudioType.WAV);
        req.timeout = 20;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;
        var clip = DownloadHandlerAudioClip.GetContent(req);
        if (clip != null) voCache[key] = clip;
    }

    IEnumerator Preload()
    {
        foreach (var c in content.cases)
            if (c.scenes != null) foreach (var line in c.scenes) yield return Fetch("narrator", line);
        foreach (var c in content.cases)
            foreach (var m in c.moves) yield return Fetch("prosecutor", m.text);
    }

    IEnumerator SayCo(string voice, string text, int tok, float delay)
    {
        voPending = true;
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (tok != voTok) yield break;
        yield return Fetch(voice, text);
        if (tok != voTok) yield break;
        voPending = false;
        if (voCache.TryGetValue(voice + "|" + text, out var c)) PlayVoice(c);
    }

    void Hush() { voTok++; voPending = false; voEnd = 0; if (vo != null) vo.Stop(); if (fx != null) fx.Stop(); }

    void SpeakMove() => Say("prosecutor", cs.moves[round].text, 0.7f);

    void EndScene()
    {
        amb.Stop();
        Go(Phase.Brief);
    }

    void Present()
    {
        var mv = cs.moves[round];
        reactions.Clear();
        bool answered = false;
        int total = 0;
        var names = new List<string>();
        rounds.Add(new RoundRec { move = mv.id, cards = selected.ToList() });
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
        Hush();
        meter = Mathf.Clamp(meter + total, 0, 100);
        popDelta = total; popT = Time.time;
        record.Add("Round " + (round + 1) + ": " + string.Join(", ", names) + "|" + total);
        selected.Clear(); resolved = true; resolveT = Time.time; reactSnd = 0;
        Play("gavel", 0.8f);
    }

    void NextRound()
    {
        resolved = false; round++;
        if (round >= cs.moves.Count) { Go(Phase.Closing); focusArg = true; } else SpeakMove();
    }

    void SubmitClosing()
    {
        Go(Phase.Judging);
        Play("whoosh", 0.7f);
        var body = JsonUtility.ToJson(new VerdictReq { caseId = cs.id, cards = played.Select(c => c.id).ToList(), argument = argument.Trim(), meter = Mathf.RoundToInt(meter), rounds = rounds.ToList() });
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
        var rq = JsonUtility.FromJson<VerdictReq>(body); rq.score = r.score; rq.meter = Mathf.RoundToInt(Mathf.Clamp(meter + r.score, 0, 100));
        StartCoroutine(FetchReport(JsonUtility.ToJson(rq)));
        meter = Mathf.Clamp(meter + r.score, 0, 100);
        popDelta = r.score; popT = Time.time;
        if (meter >= Threshold) won.Add(cs.id);
        Go(Phase.End);
        Play("gavel3", 0.9f);
        StartCoroutine(Later(0.75f, () => Play(meter >= Threshold ? "acquit" : "guilty", 0.9f)));
        Say("judge", (meter >= Threshold ? "Not guilty. " : "Guilty. ") + r.headline + ". " + r.feedback, 1.6f);
    }

    IEnumerator Later(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

    // ---------- generated cases (/api/generate) ----------
    IEnumerator Generate()
    {
        generating = true; genErr = null; dropFocus = true;
        Play("whoosh", 0.6f);
        using var req = new UnityWebRequest(Base + "/api/generate", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new GenReq { description = desc.Trim() })));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 100;
        yield return req.SendWebRequest();
        CaseDef c = null; string err = null;
        var body = req.downloadHandler.text;
        if (!string.IsNullOrEmpty(body))
        {
            try { c = JsonUtility.FromJson<CaseDef>(body); if (req.responseCode == 200) err = JsonUtility.FromJson<ErrResp>(body).error; }
            catch (System.Exception) { c = null; }
        }
        if (c == null || c.moves == null || c.moves.Count < 3 || c.cards == null || c.cards.Count < 6 || c.scenes == null || c.scenes.Count < 3)
        {
            generating = false;
            genErr = !string.IsNullOrEmpty(err) ? err : "The court clerk could not draft this case. Try rephrasing it.";
            Play("bad", 0.6f);
            yield break;
        }
        if (voiceOn) yield return Fetch("narrator", c.scenes[0]);
        generating = false; desc = "";
        content.cases.Add(c);
        StartCase(content.cases.Count - 1);
        StartCoroutine(PreloadCase(c));
    }

    IEnumerator PreloadCase(CaseDef c)
    {
        foreach (var line in c.scenes) yield return Fetch("narrator", line);
        foreach (var m in c.moves) yield return Fetch("prosecutor", m.text);
    }

    // ---------- voice input (Mic.jslib -> /api/stt -> Mistral Voxtral) ----------
    void ToggleMic(bool forCreate)
    {
        micErr = null;
        if (mic == Mic.Recording) { MicStop(); mic = Mic.Busy; return; }
        if (mic != Mic.Idle) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        micForCreate = forCreate; mic = Mic.Starting; Hush(); MicStart(gameObject.name);
#else
        micErr = "Voice input works in the browser build only.";
#endif
    }

    public void OnMicStarted(string _) { mic = Mic.Recording; micT = Time.time; Play("select", 0.6f); }
    public void OnMicError(string e) { mic = Mic.Idle; micErr = e; }

    public void OnTranscript(string t)
    {
        mic = Mic.Idle;
        t = (t ?? "").Trim();
        if (t.Length == 0) { micErr = "Nothing was heard. Try again, closer to the microphone."; return; }
        if (micForCreate) desc = (desc.Trim() + " " + t).Trim();
        else argument = (argument.Trim() + " " + t).Trim();
        dropFocus = true;
        Play("select");
    }

    void MicBtn(Rect r, bool forCreate)
    {
        bool rec = mic == Mic.Recording && micForCreate == forCreate;
        int sec = Mathf.FloorToInt(Time.time - micT);
        string label = rec ? "STOP  " + sec / 60 + ":" + (sec % 60).ToString("00")
            : mic == Mic.Busy ? "TRANSCRIBING..." : mic == Mic.Starting ? "ALLOW THE MICROPHONE..." : "SPEAK INSTEAD";
        if (rec) Box(Grow(r, 3 + 2 * Mathf.Sin(Time.time * 5)), A(Red, 0.3f), 7);
        if (Btn(r, label, false, mic == Mic.Idle || rec)) ToggleMic(forCreate);
        if (mic == Mic.Idle || rec) Box(new Rect(r.x + 20, r.center.y - 5, 10, 10), rec ? Accent : Red, rec ? 1 : 5);
    }

    // ---------- procedural sound ----------
    static AudioClip Synth(string name, float len, System.Func<float, float> f)
    {
        int n = (int)(SR * len); var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)SR), -1, 1);
        var c = AudioClip.Create(name, n, 1, SR, false); c.SetData(d, 0); return c;
    }

    void Play(string k, float v = 1) { if (sfx != null && clips.TryGetValue(k, out var c)) sfx.PlayOneShot(c, v); }

    void BuildSounds()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        fx = gameObject.AddComponent<AudioSource>();
        amb = gameObject.AddComponent<AudioSource>();
        vo = gameObject.AddComponent<AudioSource>();
        var rng = new System.Random(7);
        float N() => (float)(rng.NextDouble() * 2 - 1);
        const float TAU = Mathf.PI * 2;
        float Knock(float t) => t < 0 ? 0 : (Mathf.Sin(TAU * 150 * t) * 0.9f + Mathf.Sin(TAU * 310 * t) * 0.5f + Mathf.Sin(TAU * 720 * t) * 0.25f) * Mathf.Exp(-t * 28) + N() * Mathf.Exp(-t * 220) * 0.6f;
        float Tone(float f, float t, float dec) => t < 0 ? 0 : (Mathf.Sin(TAU * f * t) + 0.3f * Mathf.Sin(2 * TAU * f * t)) * Mathf.Exp(-t * dec);
        clips["click"] = Synth("click", 0.06f, t => (Mathf.Sin(TAU * 1800 * t) * 0.5f + N() * 0.3f) * Mathf.Exp(-t * 90) * 0.5f);
        clips["select"] = Synth("select", 0.14f, t => Mathf.Sin(TAU * 1100 * t) * Mathf.Exp(-t * 40) * 0.35f + Mathf.Sin(TAU * 2200 * t) * Mathf.Exp(-t * 60) * 0.1f);
        clips["gavel"] = Synth("gavel", 0.8f, t => (Knock(t) + 0.8f * Knock(t - 0.22f)) * 0.7f);
        clips["gavel3"] = Synth("gavel3", 1.1f, t => (Knock(t) + 0.9f * Knock(t - 0.25f) + Knock(t - 0.5f)) * 0.7f);
        clips["good"] = Synth("good", 0.9f, t => (Tone(659.25f, t, 5) + Tone(987.77f, t - 0.09f, 5)) * 0.22f * Mathf.Min(1, t * 200));
        clips["bad"] = Synth("bad", 0.6f, t => { float ph = TAU * (180 * t - 30 * t * t); return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2 * ph)) * Mathf.Min(1, t * 40) * Mathf.Exp(-t * 5) * 0.35f; });
        float lp = 0;
        clips["whoosh"] = Synth("whoosh", 0.55f, t => { float e = Mathf.Sin(Mathf.PI * t / 0.55f); lp += (N() - lp) * (0.03f + 0.2f * e); return lp * e * 1.2f; });
        float[] maj = { 261.63f, 329.63f, 392f, 523.25f }, min = { 110f, 130.81f, 164.81f, 220f };
        clips["acquit"] = Synth("acquit", 2.6f, t => { float v = 0; foreach (var f in maj) v += Mathf.Sin(TAU * f * t) + 0.25f * Mathf.Sin(2 * TAU * f * t); return v * Mathf.Min(1, t * 3) * Mathf.Exp(-t * 1.2f) * 0.1f; });
        clips["guilty"] = Synth("guilty", 2.6f, t => { float v = 0; foreach (var f in min) v += Mathf.Sin(TAU * f * t) + 0.3f * Mathf.Sin(2 * TAU * f * t); return v * Mathf.Min(1, t * 3) * Mathf.Exp(-t * 1.2f) * (0.85f + 0.15f * Mathf.Sin(TAU * 5 * t)) * 0.12f; });
        float b = 0;
        clips["drone"] = Synth("drone", 10.5f, t =>
        {
            b = (b + N() * 0.02f) * 0.995f;
            float tau = t % 1.25f;
            float v = Mathf.Sin(TAU * 55 * t) * 0.25f + Mathf.Sin(TAU * 82.6f * t) * 0.16f + Mathf.Sin(TAU * 110.5f * t) * 0.07f * (0.6f + 0.4f * Mathf.Sin(TAU * 0.25f * t))
                + b * 0.6f + Mathf.Sin(TAU * 58 * tau) * Mathf.Exp(-tau * 14) * 0.35f;
            return v * Mathf.Min(1, t) * Mathf.Min(1, (10.5f - t)) * 0.7f;
        });
        BuildSceneFx(rng, N, TAU);
    }

    // Reconstruction sound effects, referenced by tag from CaseDef.sfx.
    void BuildSceneFx(System.Random rng, System.Func<float> N, float TAU)
    {
        float R() => (float)rng.NextDouble();
        float sPh = 0, sLp = 0;
        clips["fx_siren"] = Synth("siren", 4.5f, t =>
        {
            float f = ((int)(t / 0.5f) % 2 == 0 ? 440f : 587f) * (1 + 0.015f * t);
            sPh += TAU * f / SR;
            float v = (float)System.Math.Tanh(2.5 * Mathf.Sin(sPh)) + 0.2f * Mathf.Sin(2 * sPh);
            sLp += (v - sLp) * 0.18f;
            return sLp * Mathf.Pow(Mathf.Min(1, t / 2.2f), 1.5f) * Mathf.Min(1, (4.5f - t) / 1.2f) * 0.45f;
        });
        float aLp = 0;
        clips["fx_alarm"] = Synth("alarm", 2.2f, t =>
        {
            float c = t % 0.32f;
            float v = c > 0.2f ? 0 : (Mathf.Sin(TAU * 1760 * t) > 0 ? 0.5f : -0.5f) + Mathf.Sin(TAU * 3520 * t) * 0.15f;
            aLp += (v - aLp) * 0.35f;
            return aLp * 0.3f;
        });
        const int np = 46;
        float[] gT = new float[np], gF = new float[np], gD = new float[np], gA = new float[np];
        for (int i = 0; i < np; i++) { float u = R(); gT[i] = 0.02f + u * u * 0.9f; gF[i] = 2200 + R() * 5200; gD[i] = 18 + R() * 30; gA[i] = 0.25f + R() * 0.5f; }
        float pn = 0;
        clips["fx_glass"] = Synth("glass", 1.8f, t =>
        {
            float n = N(), hp = n - pn; pn = n;
            float v = hp * Mathf.Exp(-t * 9) * 0.5f + Mathf.Sin(TAU * 190 * t) * Mathf.Exp(-t * 35) * 0.5f;
            for (int i = 0; i < np; i++) { float d = t - gT[i]; if (d > 0 && d < 0.4f) v += Mathf.Sin(TAU * gF[i] * d) * Mathf.Exp(-d * gD[i]) * gA[i] * 0.22f; }
            return v * 0.8f;
        });
        clips["fx_punch"] = Synth("punch", 0.5f, t => (Mathf.Sin(TAU * (95 - 45 * t) * t) * Mathf.Exp(-t * 16) + N() * Mathf.Exp(-t * 70) * 0.6f) * 0.9f);
        float[] clT = new float[7], clF = new float[7];
        for (int i = 0; i < 7; i++) { clT[i] = 0.4f + R() * 5.2f; clF[i] = 2400 + R() * 1800; }
        float b1 = 0, b2 = 0;
        clips["fx_bar"] = Synth("bar", 6f, t =>
        {
            float n = N(); b1 += (n - b1) * 0.12f; b2 += (n - b2) * 0.02f;
            float am = 0.55f + 0.25f * Mathf.Sin(TAU * 3.1f * t) * Mathf.Sin(TAU * 0.7f * t + 1) + 0.2f * Mathf.Sin(TAU * 5.3f * t + 2);
            float v = (b1 - b2) * am * 2.2f;
            for (int i = 0; i < 7; i++) { float d = t - clT[i]; if (d > 0 && d < 0.3f) v += (Mathf.Sin(TAU * clF[i] * d) + 0.5f * Mathf.Sin(TAU * clF[i] * 2.7f * d)) * Mathf.Exp(-d * 30) * 0.12f; }
            return v * Mathf.Min(1, t / 0.8f) * Mathf.Min(1, (6 - t) / 1.2f) * 0.8f;
        });
        float[] beeps = { 0.25f, 1.05f, 1.9f, 2.7f };
        float m1 = 0, m2 = 0;
        clips["fx_store"] = Synth("store", 4f, t =>
        {
            float v = 0;
            foreach (var bt in beeps) { float d = t - bt; if (d > 0 && d < 0.09f) v += Mathf.Sin(TAU * 1975 * d) * Mathf.Min(1, d * 400) * Mathf.Min(1, (0.09f - d) * 400) * 0.3f; }
            float n = N(); m1 += (n - m1) * 0.1f; m2 += (n - m2) * 0.015f;
            v += (m1 - m2) * (0.6f + 0.4f * Mathf.Sin(TAU * 2.3f * t)) * 0.7f;
            return v * Mathf.Min(1, (4 - t) / 0.8f);
        });
        float cv = 0;
        clips["fx_cctv"] = Synth("cctv", 2.6f, t =>
        {
            float v = Mathf.Sin(TAU * 100 * t) * 0.04f + N() * 0.015f;
            if (t > 0.15f && t < 0.2f) v += N() * Mathf.Exp(-(t - 0.15f) * 200) * 0.5f;
            if (t > 0.35f && t < 1.35f) { float d = t - 0.35f, w = (260 + 60 * d) * d % 1f * 2 - 1; cv += (w - cv) * 0.08f; v += cv * Mathf.Sin(Mathf.PI * d) * 0.3f; }
            if (t > 1.6f && t < 1.72f) v += Mathf.Sin(TAU * 2600 * t) * 0.18f;
            return v * Mathf.Min(1, (2.6f - t) / 0.5f);
        });
        var keys = new List<float>();
        for (float kt = 0.1f; kt < 3.2f; kt += 0.07f + R() * 0.16f) keys.Add(kt);
        var keyA = keys.Select(_ => 0.5f + R() * 0.5f).ToArray();
        int ki = 0;
        clips["fx_typing"] = Synth("typing", 3.5f, t =>
        {
            while (ki + 1 < keys.Count && t >= keys[ki + 1]) ki++;
            float d = t - keys[ki];
            if (d < 0) return 0;
            return (N() * Mathf.Exp(-d * 350) * 0.6f + Mathf.Sin(TAU * 2800 * d) * Mathf.Exp(-d * 500) * 0.3f + Mathf.Sin(TAU * 180 * d) * Mathf.Exp(-d * 120) * 0.25f) * keyA[ki] * 0.7f;
        });
        clips["fx_phone"] = Synth("phone", 3.4f, t =>
        {
            float c = t % 2f;
            if (!(c < 0.4f || (c > 0.6f && c < 1.0f))) return 0;
            float trill = Mathf.Sin(TAU * 18 * t) > 0 ? 1 : 0.35f;
            return (Mathf.Sin(TAU * 1250 * t) + 0.5f * Mathf.Sin(TAU * 1700 * t)) * trill * 0.15f;
        });
        float dl = 0;
        clips["fx_door"] = Synth("door", 1.4f, t =>
        {
            float n = N(); dl += (n - dl) * 0.05f;
            return (Mathf.Sin(TAU * 62 * t) * Mathf.Exp(-t * 9) * 0.8f + dl * Mathf.Exp(-t * 14) * 3f + (t > 0.12f ? N() * Mathf.Exp(-(t - 0.12f) * 30) * 0.08f : 0)) * 0.8f;
        });
    }

    // ---------- drawing helpers ----------
    void Box(Rect r, Color c, float rad = 0) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, 0, rad);
    void Border(Rect r, Color c, float w, float rad = 0) => GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, w, rad);
    bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

    Font FontFor(FontStyle f) => f == FontStyle.Bold || f == FontStyle.BoldAndItalic ? fBold : f == FontStyle.Italic ? fIt : fReg;

    void Txt(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, FontStyle f = FontStyle.Normal)
    {
        st.fontSize = size; st.normal.textColor = c; st.alignment = a; st.font = FontFor(f); st.fontStyle = FontStyle.Normal;
        GUI.Label(r, s, st);
    }

    float TxtH(string s, int size, float w, FontStyle f = FontStyle.Normal)
    {
        st.fontSize = size; st.font = FontFor(f); st.fontStyle = FontStyle.Normal;
        return st.CalcHeight(new GUIContent(s), w);
    }

    // draws wrapped text at y, returns the y below it
    float Para(float x, float y, float w, string s, int size, Color c, FontStyle f = FontStyle.Normal, float gap = 10)
    {
        float h = TxtH(s, size, w, f);
        Txt(new Rect(x, y, w, h), s, size, c, TextAnchor.UpperLeft, f);
        return y + h + gap;
    }

    bool Btn(Rect r, string label, bool primary = true, bool on = true)
    {
        bool h = on && Hover(r);
        if (primary) Box(r, !on ? Panel2 : h ? Color.white : Accent, 4);
        else { Box(r, h ? Panel2 : A(Panel, 0.9f), 4); Border(r, h ? Accent : Line, 1, 4); }
        Txt(r, label, 14, !on ? Muted : primary ? Bg : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        bool pressed = on && GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (pressed) Play("click", 0.6f);
        return pressed;
    }

    void PanelBox(Rect r, string header = null)
    {
        Box(r, Panel, 6);
        Border(r, Line, 1, 6);
        if (header != null) Txt(new Rect(r.x + 20, r.y + 16, r.width - 40, 20), header, 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
    }

    static Color KindColor(string k) => k switch
    {
        "Principle" => Hex("F2EFE8"),
        "Evidence" => Hex("BDBDC2"),
        "Witness" => Hex("9A9AA0"),
        _ => Hex("74747B"),
    };

    static string Signed(int d) => d > 0 ? "+" + d : d.ToString();

    // ---------- OnGUI ----------
    void OnGUI()
    {
        if (st == null)
        {
            fReg = Resources.Load<Font>("Fonts/LB-400"); fBold = Resources.Load<Font>("Fonts/LB-700"); fIt = Resources.Load<Font>("Fonts/LB-400i");
            st = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            area = new GUIStyle(GUI.skin.textArea) { font = fReg, fontSize = 17, wordWrap = true, padding = new RectOffset(18, 18, 16, 16) };
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, Hex("0F0F11")); t.Apply();
            foreach (var s in new[] { area.normal, area.focused, area.hover, area.active }) { s.background = t; s.textColor = Ink; }
        }
        if (dropFocus && Event.current.type == EventType.Layout) { GUIUtility.keyboardControl = 0; dropFocus = false; }
        GUI.matrix = Matrix4x4.identity;
        Box(new Rect(0, 0, Screen.width, Screen.height), Bg);
        float sc = Mathf.Min(Screen.width / W, Screen.height / H);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * sc) / 2, (Screen.height - H * sc) / 2, 0), Quaternion.identity, new Vector3(sc, sc, 1));

        switch (phase)
        {
            case Phase.Loading: DrawLoading(); break;
            case Phase.Menu: DrawMenu(); break;
            case Phase.Create: DrawCreate(); break;
            case Phase.Scene: DrawScene(); break;
            case Phase.Brief: DrawBrief(); break;
            case Phase.Trial: DrawTrial(); break;
            case Phase.Closing: DrawClosing(); break;
            case Phase.Judging: DrawJudging(); break;
            case Phase.End: DrawEnd(); break;
            case Phase.Lobby: DrawLobby(); break;
            case Phase.MpWait: DrawMpWait(); break;
            case Phase.MpTrial: DrawMpTrial(); break;
            case Phase.MpEnd: DrawMpEnd(); break;
            case Phase.Report: DrawReport(); break;
        }
        if (mp) MpBanner();
    }

    void DrawLoading()
    {
        Txt(new Rect(0, 300, W, 70), "VERDICT", 56, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 380, W, 30), loadErr ?? "Preparing the courtroom...", 18, loadErr != null ? Red : Muted, TextAnchor.MiddleCenter);
    }

    void DrawMenu()
    {
        if (hero != null)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.2f), 0, 0);
            GUI.DrawTexture(new Rect(0, 260, W, 460), grad, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.9f), 0, 0);
        }
        Txt(new Rect(0, 40, W, 80), "VERDICT", 66, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Box(new Rect(W / 2 - 230, 130, 140, 1), Line);
        Box(new Rect(W / 2 + 90, 130, 140, 1), Line);
        Txt(new Rect(0, 120, W, 20), "THE AI COURTROOM", 11, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 152, W, 30), "You are the defence lawyer. Answer the prosecution with real law, then convince the jury.", 18, Ink, TextAnchor.MiddleCenter);
        Txt(new Rect(0, 182, W, 24), "French criminal law in ten minutes  \u00b7  win " + Threshold + "%+ of the jury  \u00b7  closing arguments judged live by Mistral AI", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Italic);

        float tw = 360, gap = 30, x0 = (W - (3 * tw + 2 * gap)) / 2;
        for (int i = 0; i < content.cases.Count && i < 3; i++)
        {
            var c = content.cases[i];
            var r = new Rect(x0 + i * (tw + gap), 222, tw, 360);
            bool h = Hover(r);
            Box(r, h ? Panel2 : Panel, 6);
            var img = Tex(Look(c) + "_1");
            var ir = new Rect(r.x, r.y, r.width, 150);
            if (img != null) GUI.DrawTexture(ir, img, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, h ? 1 : 0.8f), Vector4.zero, new Vector4(6, 6, 0, 0));
            Border(r, h ? Accent : Line, 1, 6);
            float x = r.x + 22, w = r.width - 44;
            Txt(new Rect(x, ir.yMax + 14, w, 18), "CASE " + (i + 1), 11, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
            if (won.Contains(c.id)) Txt(new Rect(x, ir.yMax + 14, w, 18), "ACQUITTED", 11, Green, TextAnchor.UpperRight, FontStyle.Bold);
            float y = Para(x, ir.yMax + 34, w, c.title, 22, Ink, FontStyle.Bold, 6);
            y = Para(x, y, w, c.charge, 14, Ink, FontStyle.Normal, 2);
            Para(x, y, w, c.law, 13, Muted, FontStyle.Italic);
            if (Btn(new Rect(x, r.yMax - 60, w, 42), "DEFEND THIS CLIENT", true)) StartCase(i);
        }

        string[] steps = { "Watch the facts, read the case file and the charge", "Each round, answer the prosecution with up to 2 law cards", "Deliver your closing argument - the AI judge scores it" };
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(x0 + i * (tw + gap), 600, tw, 60);
            Border(r, Line, 1, 6);
            Txt(new Rect(r.x + 16, r.y, 36, r.height), (i + 1).ToString(), 26, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Box(new Rect(r.x + 60, r.y + 14, 1, r.height - 28), Line);
            Txt(new Rect(r.x + 76, r.y + 6, r.width - 90, r.height - 12), steps[i], 13, Ink, TextAnchor.MiddleLeft);
        }
        var cr = new Rect(W / 2 - 430, 666, 420, 40);
        if (Btn(cr, "+   CREATE A CASE FROM YOUR OWN STORY", false)) { genErr = null; Go(Phase.Create); }
        Border(cr, A(Accent, 0.55f), 1, 4);
        var mr = new Rect(W / 2 + 10, 666, 420, 40);
        if (Btn(mr, "MULTIPLAYER  -  PROSECUTION VS DEFENCE", false)) OpenLobby();
        Border(mr, A(Accent, 0.55f), 1, 4);
        if (Btn(new Rect(W - 160, 670, 120, 34), voiceOn ? "VOICE: ON" : "VOICE: OFF", false)) { voiceOn = !voiceOn; Hush(); }
    }

    void DrawScene()
    {
        const float per = 10f / 3f;
        int k = shot;
        float lt = Time.time - shotT0;
        GUI.BeginGroup(new Rect(0, 0, W, H));
        for (int j = Mathf.Max(0, k - 1); j <= k; j++)
        {
            var img = Tex(Look(cs) + "_" + (j + 1));
            if (img == null) continue;
            float jt = j == k ? lt : lt + prevLen;
            float a = j < k ? 1 : k == 0 ? Mathf.Clamp01(lt / 0.8f) : Mathf.Clamp01(lt / 0.7f);
            float m = 1 - Mathf.Exp(-jt / 5f);
            float z = 1.04f + 0.11f * m, dx = (j % 2 == 0 ? -1 : 1) * 44 * m;
            float iw = W * z, ih = W * img.height / (float)img.width * z;
            GUI.DrawTexture(new Rect((W - iw) / 2 + dx, (H - ih) / 2, iw, ih), img, ScaleMode.StretchToFill, false, 0, A(Color.white, a), 0, 0);
        }
        GUI.EndGroup();

        GUI.DrawTexture(new Rect(0, H - 64 - 220, W, 220), grad, ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
        Box(new Rect(0, 0, W, 64), Color.black);
        Box(new Rect(0, H - 64, W, 64), Color.black);
        Txt(new Rect(40, 0, 700, 64), CaseLabel + "   \u00b7   " + cs.title.ToUpper(), 13, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Box(new Rect(W - 300, 28, 8, 8), A(Red, 0.5f + 0.5f * Mathf.Sin(Time.time * 4)), 4);
        Txt(new Rect(W - 285, 0, 245, 64), "RECONSTRUCTION OF THE FACTS", 11, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);

        if (cs.scenes != null && k < cs.scenes.Count)
        {
            float ca = Mathf.Clamp01((lt - 0.3f) / 0.5f);
            Txt(new Rect(140, H - 64 - 120, W - 280, 100), cs.scenes[k], 24, A(Color.white, ca), TextAnchor.MiddleCenter, FontStyle.Italic);
        }
        Box(new Rect(0, H - 64, W * Mathf.Clamp01((k + Mathf.Clamp01(lt / per)) / 3f), 2), A(Accent, 0.7f));
        Txt(new Rect(40, H - 62, 200, 62), (k + 1) + " / 3", 12, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        if (Btn(new Rect(W - 170, H - 52, 130, 40), "SKIP", false)) { EndScene(); return; }
        if (sceneEndT >= 0) Box(new Rect(0, 0, W, H), A(Color.black, Mathf.Clamp01((Time.time - sceneEndT) / 0.5f)));
    }

    void DrawBrief()
    {
        var left = new Rect(70, 60, 640, 600);
        Txt(new Rect(left.x, left.y, 400, 20), "CASE FILE  -  " + CaseLabel, 13, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        float y = Para(left.x, left.y + 28, left.width, cs.title, 44, Ink, FontStyle.Bold, 8);
        y = Para(left.x, y, left.width, (IsPros ? "The accused: " : "Your client: ") + cs.client, 18, Muted, FontStyle.Normal, 26);
        Txt(new Rect(left.x, y, 300, 20), "THE FACTS", 13, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 30;
        foreach (var f in cs.facts)
        {
            Box(new Rect(left.x, y + 9, 6, 6), Accent, 3);
            y = Para(left.x + 20, y, left.width - 20, f, 18, Ink, FontStyle.Normal, 12);
        }

        var right = new Rect(760, 60, 450, 520);
        PanelBox(right, "THE CHARGE");
        float x = right.x + 24, w = right.width - 48;
        y = Para(x, right.y + 46, w, cs.charge, 24, Ink, FontStyle.Bold, 4);
        y = Para(x, y, w, cs.law, 15, Accent, FontStyle.Normal, 12);
        y = Para(x, y, w, cs.definition, 16, Muted, FontStyle.Italic, 26);
        Box(new Rect(x, y, w, 1), Line);
        y += 18;
        Txt(new Rect(x, y, w, 20), "YOUR OBJECTIVE", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        y = Para(x, y + 26, w, BriefGoal, 17, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, BriefHow, 15, Muted, FontStyle.Normal, 22);
        if (caseIdx >= 3)
        {
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 16, w, "This case was written by Mistral AI from your description. Treat its legal details as a learning aid, not legal advice.", 13, Muted, FontStyle.Italic);
        }

        if (mp) MpBriefButtons();
        else
        {
            if (Btn(new Rect(760, 600, 300, 52), "ENTER THE COURTROOM", true)) { Go(Phase.Trial); resolved = false; Play("gavel"); SpeakMove(); }
            if (Btn(new Rect(1076, 600, 134, 52), "Back", false)) Go(Phase.Menu);
        }
        if (Btn(new Rect(70, 600, 240, 52), "REPLAY THE FACTS", false)) PlayScene();
    }

    void TopBar()
    {
        Box(new Rect(0, 0, W, 52), Panel);
        Box(new Rect(0, 52, W, 1), Line);
        Txt(new Rect(24, 0, 200, 52), "VERDICT", 20, Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
        Txt(new Rect(0, 0, W, 52), cs.title + "   |   " + cs.charge + " (" + cs.law + ")", 15, Ink, TextAnchor.MiddleCenter);
        string right = (phase == Phase.Report ? "ANALYSIS" : phase == Phase.Trial || phase == Phase.MpTrial ? "ROUND " + (round + 1) + " / " + Rounds : phase == Phase.End || phase == Phase.MpEnd ? "JUDGMENT" : "CLOSING")
            + (mp ? "   \u00b7   " + Side(mpRole) : "");
        Txt(new Rect(W - 424, 0, 400, 52), right, 15, Accent, TextAnchor.MiddleRight, FontStyle.Bold);
    }

    void DrawMeter(Rect r)
    {
        Txt(new Rect(r.x, r.y, 200, 20), "JURY", 12, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
        bool ok = meterShown >= Threshold - 0.5f;
        Txt(new Rect(r.x, r.y - 6, r.width, 30), Mathf.RoundToInt(meterShown) + "% NOT GUILTY", 22, ok ? Green : Ink, TextAnchor.UpperRight, FontStyle.Bold);
        var track = new Rect(r.x, r.y + 30, r.width, 14);
        Box(track, Line, 7);
        float fw = track.width * Mathf.Clamp01(meterShown / 100f);
        if (fw > 2) Box(new Rect(track.x, track.y, fw, track.height), ok ? Green : Accent, 7);
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
        float x = r.x + 22, w = r.width - 44;
        float y = Para(x, r.y + 46, w, "Client: " + cs.client, 13, Muted, FontStyle.Normal, 16);
        foreach (var f in cs.facts)
        {
            Box(new Rect(x, y + 6, 4, 4), Accent, 2);
            y = Para(x + 14, y, w - 14, f, 13, Ink, FontStyle.Normal, 12);
        }
    }

    void RecordPanel(Rect r)
    {
        PanelBox(r, "COURT RECORD");
        float x = r.x + 22, w = r.width - 44, y = r.y + 46;
        y = Para(x, y, w, "Jury started at " + cs.start + "%", 13, Muted, FontStyle.Normal, 16);
        if (record.Count == 0) Para(x, y, w, "Your arguments will be recorded here.", 13, Muted, FontStyle.Italic);
        foreach (var e in record)
        {
            var p = e.Split('|');
            int d = int.Parse(p[1]);
            Txt(new Rect(x, y, w, 20), Signed(d) + "%", 14, d >= 0 ? Green : Red, TextAnchor.UpperRight, FontStyle.Bold);
            y = Para(x, y, w - 56, p[0], 13, Ink, FontStyle.Normal, 16);
        }
    }

    void DrawTrial()
    {
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        CaseFilePanel(new Rect(28, 76, 284, 380));
        RecordPanel(new Rect(968, 76, 284, 380));

        var mv = cs.moves[round];
        var center = new Rect(340, 164, 600, 200);
        if (!resolved)
        {
            MovePanel(center, mv);

            string hint = round == 0
                ? "Pick up to 2 cards that rebut this exact point. Weak law backfires."
                : "Select up to 2 cards, then present them to the court.";
            Txt(new Rect(340, 380, 600, 20), hint, 13, Accent, TextAnchor.MiddleCenter);
            if (Btn(new Rect(520, 414, 240, 42), "PRESENT " + selected.Count + " / " + MaxPick + " CARDS", true, selected.Count > 0)) Present();
        }
        else
        {
            float age = Time.time - resolveT;
            DrawReactions(center, age);
            if (age > 0.4f + reactions.Count * 0.55f && Btn(new Rect(520, 414, 240, 42), round + 1 < cs.moves.Count ? "NEXT ROUND" : "CLOSING ARGUMENT", true)) NextRound();
        }
        DrawHand();
    }

    void MovePanel(Rect center, MoveDef mv)
    {
            Box(center, Panel, 12);
            Border(center, A(Red, 0.6f), 1.5f, 12);
            Box(new Rect(center.x, center.y + 14, 4, center.height - 28), Red, 2);
            float x = center.x + 26, w = center.width - 52;
            Txt(new Rect(x, center.y + 18, w, 18), "THE PROSECUTION ARGUES", 12, Red, TextAnchor.UpperLeft, FontStyle.Bold);
            Txt(new Rect(x, center.y + 18, w, 18), mv.law, 12, Muted, TextAnchor.UpperRight);
            float y = Para(x, center.y + 48, w, mv.title, 26, Ink, FontStyle.Bold, 10);
            Para(x, y, w, "\"" + mv.text + "\"", 17, Ink, FontStyle.Italic);
            Txt(new Rect(x, center.yMax - 36, w, 20), (IsPros ? "If the defence can't answer it: " : "If you don't answer it: ") + Signed(-mv.power) + "% jury", 14, Red, TextAnchor.UpperLeft, FontStyle.Bold);
    }

    void DrawReactions(Rect center, float age)
    {
            Txt(new Rect(center.x, center.y - 6, center.width, 20), "THE JURY REACTS", 12, Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            float y = center.y + 20;
            for (int i = 0; i < reactions.Count; i++)
            {
                float a = Mathf.Clamp01((age - 0.4f - i * 0.55f) / 0.3f);
                if (a <= 0) break;
                var re = reactions[i];
                if (i >= reactSnd && Event.current.type == EventType.Repaint) { reactSnd = i + 1; Play(re.delta > 0 ? "good" : re.delta < 0 ? "bad" : "select", 0.8f); }
                float bh = TxtH(re.body, 13, center.width - 96);
                var row = new Rect(center.x, y, center.width, 30 + bh);
                Box(row, A(Panel, a), 10);
                var col = re.delta > 0 ? Green : re.delta < 0 ? Red : Muted;
                Box(new Rect(row.x + 12, row.y + 10, 56, 28), A(col, 0.18f * a), 6);
                Txt(new Rect(row.x + 12, row.y + 10, 56, 28), re.delta == 0 ? "0" : Signed(re.delta), 16, A(col, a), TextAnchor.MiddleCenter, FontStyle.Bold);
                Txt(new Rect(row.x + 82, row.y + 7, row.width - 96, 20), re.title, 14, A(Ink, a), TextAnchor.UpperLeft, FontStyle.Bold);
                Txt(new Rect(row.x + 82, row.y + 26, row.width - 96, bh), re.body, 13, A(Muted, a));
                y += row.height + 10;
            }
    }

    void DrawHand()
    {
        const float cw = 142, ch = 226, gap = 12;
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
            Box(r, sel ? Hex("2A2A2E") : Panel2, 6);
            Border(r, sel ? Accent : h ? Muted : Line, sel ? 2f : 1, 6);
            var kc = KindColor(c.kind);
            Box(new Rect(r.x + 1, r.y + 1, r.width - 2, 4), A(kc, dim), 2);
            float x = r.x + 14, w = r.width - 28;
            Txt(new Rect(x, r.y + 14, w, 16), c.kind.ToUpper(), 10, A(kc, dim), TextAnchor.UpperLeft, FontStyle.Bold);
            float y = Para(x, r.y + 34, w, c.name, 15, A(Ink, dim), FontStyle.Bold, 6);
            y = Para(x, y, w, c.law, 11, A(Accent, dim), FontStyle.Normal, 12);
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 12, w, c.plain, 12, A(Muted, dim));
            if (sel) Txt(new Rect(r.x, r.yMax - 24, r.width, 18), "SELECTED", 10, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (h && e.type == EventType.MouseDown && e.button == 0)
            {
                if (sel) selected.Remove(c.id);
                else if (selected.Count < MaxPick) selected.Add(c.id);
                Play("select");
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
        float y = Para(x, p.y + 44, w, ClosingTitle, 26, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, (mp ? ClosingHelp : "Explain which legal condition of the charge is missing (or which defence applies), link it to the facts and cite the article. Mistral AI plays the judge: a strong closing can swing the jury by up to +15%, a wrong one costs you."), 15, Muted, FontStyle.Normal, 14);
        y = Para(x, y, w, (IsPros ? "The defence relied on: " + string.Join("  -  ", played.Select(c => c.name)) : "Law on your side: " + string.Join("  -  ", played.Where(c => c.power > 0).Select(c => c.name + " (" + c.law + ")"))), 13, Accent, FontStyle.Normal, 18);
        var ta = new Rect(x, y, w, p.yMax - y - 80);
        Border(Grow(ta, 1), Line, 1, 8);
        GUI.SetNextControlName("arg");
        GUI.enabled = !MpSubmitted;
        argument = GUI.TextArea(ta, argument, 1500, area);
        GUI.enabled = true;
        if (focusArg) { GUI.FocusControl("arg"); if (GUI.GetNameOfFocusedControl() == "arg") focusArg = false; }
        if (string.IsNullOrEmpty(argument))
            Txt(new Rect(ta.x + 18, ta.y + 16, ta.width - 36, 60), (IsPros ? "e.g. \"Members of the jury, every element of " + cs.law + " is proven...\"" : "e.g. \"Members of the jury, theft requires fraudulent intent under article 311-1...\""), 17, A(Muted, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
        Txt(new Rect(x, p.yMax - 60, 360, 44), micErr ?? (MpSubmitted ? "Delivered. Waiting for the other side's closing" + Dots : argument.Trim().Length < 20 ? "Write or say at least a couple of sentences." : argument.Length + " characters"), 13, micErr != null ? Red : Muted, TextAnchor.MiddleLeft);
        if (!MpSubmitted) MicBtn(new Rect(p.xMax - 330 - 16 - 260, p.yMax - 64, 260, 48), false);
        if (Btn(new Rect(p.xMax - 330, p.yMax - 64, 300, 48), "DELIVER TO THE JURY", true, argument.Trim().Length >= 20 && !MpSubmitted && !mpBusy))
        {
            if (mp) { Hush(); MpAct("closing", text: argument.Trim()); }
            else SubmitClosing();
        }
    }

    void DrawCreate()
    {
        if (hero != null) GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.12f), 0, 0);
        var p = new Rect(190, 60, 900, 600);
        PanelBox(p, "NEW CASE");
        float x = p.x + 36, w = p.width - 72;
        float y = Para(x, p.y + 50, w, "Tell the court what happened.", 30, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, "Describe any situation in a few sentences, real or invented. Mistral AI turns it into a full trial under French criminal law: the charge and its article, the facts, three prosecution points and eight law cards - some of them traps.", 15, Muted, FontStyle.Normal, 18);
        var ta = new Rect(x, y, w, 150);
        Border(Grow(ta, 1), Line, 1, 8);
        GUI.enabled = !generating;
        GUI.SetNextControlName("desc");
        desc = GUI.TextArea(ta, desc, 800, area);
        GUI.enabled = true;
        if (string.IsNullOrEmpty(desc))
            Txt(new Rect(ta.x + 18, ta.y + 16, ta.width - 36, 60), "e.g. \"My neighbour borrowed my scooter without asking and brought it back the next day with a scratch.\"", 17, A(Muted, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
        y = ta.yMax + 22;
        Txt(new Rect(x, y, w, 18), "OR START FROM AN EXAMPLE", 11, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
        y += 24;
        string[] ex =
        {
            "A teenager takes his neighbour's scooter for a joyride and brings it back the next morning.",
            "An Airbnb host keeps a guest's forgotten laptop and sells it online two weeks later.",
            "A man finds a wallet with EUR 300 in the street, keeps the cash and posts the ID card back.",
        };
        foreach (var e in ex)
        {
            if (Btn(new Rect(x, y, w, 36), e, false, !generating)) { desc = e; dropFocus = true; }
            y += 44;
        }
        string status = generating ? "Mistral is writing the case file, the prosecution and your cards" + new string('.', 1 + (int)(Time.time * 2) % 3) : micErr ?? genErr;
        if (status != null) Txt(new Rect(x, p.yMax - 112, w, 22), status, 14, generating ? Accent : Red, TextAnchor.MiddleLeft, generating ? FontStyle.Italic : FontStyle.Normal);
        if (!generating) MicBtn(new Rect(x, p.yMax - 76, 260, 48), true);
        if (Btn(new Rect(p.xMax - 36 - 300, p.yMax - 76, 300, 48), generating ? "DRAFTING THE CASE..." : "GENERATE THE CASE", true, !generating && mic == Mic.Idle && desc.Trim().Length >= 15)) StartCoroutine(Generate());
        if (Btn(new Rect(p.xMax - 36 - 300 - 16 - 130, p.yMax - 76, 130, 48), "Back", false, !generating)) Go(Phase.Menu);
    }

    static Rect Grow(Rect r, float d) => new(r.x - d, r.y - d, r.width + 2 * d, r.height + 2 * d);

    void DrawJudging()
    {
        TopBar();
        DrawMeter(new Rect(340, 76, 600, 70));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
        Box(new Rect(W / 2 - 50, 300, 100, 100), A(Accent, 0.15f + 0.2f * pulse), 50);
        Box(new Rect(W / 2 - 22, 328, 44, 44), Accent, 22);
        Txt(new Rect(0, 430, W, 40), "The judge is deliberating" + new string('.', 1 + (int)(Time.time * 2) % 3), 26, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Txt(new Rect(0, 474, W, 24), mp ? "Mistral AI is weighing both closing arguments against the facts and the law" : "Mistral AI is weighing your closing argument against the facts and the law", 15, Muted, TextAnchor.MiddleCenter);
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
        y = Para(x, r.y + 44, w, cs.takeaway, 16, Accent, FontStyle.Bold, 14);
        foreach (var c in played)
        {
            Box(new Rect(x, y + 5, 8, 8), c.power > 0 ? Green : Red, 4);
            y = Para(x + 18, y, w - 18, c.name + " (" + c.law + "): " + c.lesson, 12, Ink, FontStyle.Normal, 6);
        }

        if (Btn(new Rect(W / 2 - 390, 664, 240, 44), "RETRY THIS CASE", false)) StartCase(caseIdx, false);
        ReportBtn(new Rect(W / 2 - 130, 664, 260, 44), report, reportLoading);
        bool more = caseIdx + 1 < content.cases.Count;
        if (Btn(new Rect(W / 2 + 150, 664, 240, 44), more ? "NEXT CASE" : "ALL CASES", true))
        {
            if (more) StartCase(caseIdx + 1); else Go(Phase.Menu);
        }
    }
}
