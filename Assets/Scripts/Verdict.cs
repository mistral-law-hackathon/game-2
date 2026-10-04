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

    // Ink-navy courtroom palette. Gold is the one sparing accent - primary actions, selection, the jury meter.
    // Everything else (labels, citations, hairlines) stays a neutral ink/navy/grey.
    static readonly Color Bg = Hex("0D1320"), Panel = Hex("141B2C"), Panel2 = Hex("1B2438"), Line = Hex("29324A"),
        Ink = Hex("F4F1E8"), Sub = Hex("AEB4C4"), Muted = Hex("747D92"), Gold = Hex("C9A24B"), Red = Hex("B5433C"), Green = Hex("4C8C6B");

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
    Font fReg, fBold, fIt, sReg, sSemi;
    readonly Dictionary<string, Texture2D> tex = new();
    int reactSnd, voTok, shot;
    bool voPending, voiceOn = true;
    float shotT0, prevLen, sceneEndT = -1, voEnd;
    readonly Dictionary<string, AudioClip> voCache = new();
    AudioSource sfx, amb, vo, fx;
    Mic mic;
    float micT;
    string micErr, desc = "", genErr;
    bool micForCreate, generating, dropFocus, createForMp;
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

    Vector2 menuScroll;
    static bool IsGen(CaseDef c) => c.id.StartsWith("gen");
    string CaseLabel => !IsGen(cs) ? "CASE " + (caseIdx + 1) : "YOUR CASE";
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
        generating = false; desc = "";
        content.cases.Add(c);
        if (createForMp) { lobbyCase = content.cases.Count - 1; mpErr = null; Go(Phase.Lobby); Play("good", 0.6f); }
        else StartCase(content.cases.Count - 1, false);
        StartCoroutine(PreloadCase(c));
    }

    IEnumerator PreloadCase(CaseDef c)
    {
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
        string label = rec ? "Stop  " + sec / 60 + ":" + (sec % 60).ToString("00")
            : mic == Mic.Busy ? "Transcribing..." : mic == Mic.Starting ? "Allow the Microphone..." : "Speak Instead";
        if (rec) Box(Grow(r, 3 + 2 * Mathf.Sin(Time.time * 5)), A(Red, 0.3f), 7);
        if (Btn(r, label, false, mic == Mic.Idle || rec)) ToggleMic(forCreate);
        if (mic == Mic.Idle || rec) Box(new Rect(r.x + 20, r.center.y - 5, 10, 10), rec ? Gold : Red, rec ? 1 : 5);
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
        float sp1 = 0, spn = 0;
        clips["fx_spray"] = Synth("spray", 3.6f, t =>
        {
            float v = 0;
            if (t < 0.5f) { float c = t % 0.125f; v += Mathf.Sin(TAU * 3100 * c) * Mathf.Exp(-c * 90) * 0.35f; }
            float n = N(), hp = n - spn; spn = n; sp1 += (hp - sp1) * 0.5f;
            float on = (t > 0.7f && t < 1.9f) || (t > 2.2f && t < 3.4f) ? 1 : 0;
            return v + sp1 * on * 0.5f * Mathf.Min(1, (3.6f - t) / 0.3f);
        });
        float tr1 = 0, tr2 = 0;
        clips["fx_train"] = Synth("train", 5.5f, t =>
        {
            float n = N(); tr1 += (n - tr1) * 0.03f; tr2 += (n - tr2) * 0.004f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 5.5f));
            float clack = 0, c = t % 0.42f; if (t > 1 && t < 4.6f) clack = (N() * Mathf.Exp(-c * 60) + Mathf.Sin(TAU * 140 * c) * Mathf.Exp(-c * 40)) * 0.25f;
            float squeal = t > 3.6f && t < 5f ? Mathf.Sin(TAU * (2300 + 120 * Mathf.Sin(TAU * 3 * t)) * t) * 0.05f * Mathf.Sin(Mathf.PI * (t - 3.6f) / 1.4f) : 0;
            return ((tr1 - tr2) * 3.2f + Mathf.Sin(TAU * 48 * t) * 0.12f) * env + clack * env + squeal;
        });
        float cr1 = 0, cr2 = 0;
        clips["fx_crowd"] = Synth("crowd", 5f, t =>
        {
            float n = N(); cr1 += (n - cr1) * 0.2f; cr2 += (n - cr2) * 0.03f;
            float sw = 0.7f + 0.3f * Mathf.Sin(TAU * 0.6f * t) * Mathf.Sin(TAU * 1.7f * t + 1);
            float chant = Mathf.Sin(TAU * 1.6f * t) > 0.6f ? 0.25f : 0;
            return (cr1 - cr2) * (sw + chant) * 1.6f * Mathf.Min(1, t / 0.8f) * Mathf.Min(1, (5 - t) / 1f);
        });
    }

    // ---------- drawing helpers ----------
    // Two type families throughout: Libre Baskerville (serif) carries headlines, case text and
    // anything meant to be read; Inter (sans) carries UI chrome - labels, buttons, numbers, captions.
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

    // sans-serif UI chrome: labels, captions, buttons, numbers
    void Lbl(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool bold = false)
    {
        st.fontSize = size; st.normal.textColor = c; st.alignment = a; st.font = bold ? sSemi : sReg; st.fontStyle = FontStyle.Normal;
        GUI.Label(r, s, st);
    }

    float LblH(string s, int size, float w, bool bold = false)
    {
        st.fontSize = size; st.font = bold ? sSemi : sReg; st.fontStyle = FontStyle.Normal;
        return st.CalcHeight(new GUIContent(s), w);
    }

    float LPara(float x, float y, float w, string s, int size, Color c, bool bold = false, float gap = 8)
    {
        float h = LblH(s, size, w, bold);
        Lbl(new Rect(x, y, w, h), s, size, c, TextAnchor.UpperLeft, bold);
        return y + h + gap;
    }

    // small tracked uppercase section tag - the recurring "micro-label" motif
    void Tag(float x, float y, string s, Color c, float w = 500) => TagR(new Rect(x, y, w, 16), s, c);
    void TagR(Rect r, string s, Color c, TextAnchor a = TextAnchor.UpperLeft) => Lbl(r, Spaced(s), 11, c, a, true);
    static string Spaced(string s) => string.Join(" ", s.ToUpper().ToCharArray());

    bool Btn(Rect r, string label, bool primary = true, bool on = true)
    {
        bool h = on && Hover(r);
        if (primary) Box(r, !on ? Panel2 : h ? Hex("DBB868") : Gold, 8);
        else { Box(r, h ? Panel2 : A(Panel, 0.5f), 8); Border(r, h ? Sub : Line, 1, 8); }
        Lbl(r, label, 13, !on ? Muted : primary ? Bg : Ink, TextAnchor.MiddleCenter, true);
        bool pressed = on && GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (pressed) Play("click", 0.6f);
        return pressed;
    }

    // filled card (over imagery / needs to separate from busy content) vs. a plain hairline section header
    void PanelBox(Rect r, string header = null, bool solid = true)
    {
        if (solid) Box(r, Panel, 10);
        else Box(new Rect(r.x, r.y, r.width, 1), Line);
        if (header != null) Tag(r.x + (solid ? 22 : 2), r.y + (solid ? 18 : 12), header, Sub);
    }

    static Color KindColor(string k) => k switch
    {
        "Principle" => Hex("D9BD7A"),
        "Evidence" => Hex("B9BFCE"),
        "Witness" => Hex("8C92A3"),
        _ => Hex("5E6578"),
    };

    // minimal line-art scales of justice - the one recurring legal glyph, used sparingly
    void Scales(Rect r, Color c, float w = 1.5f)
    {
        float cx = r.center.x, top = r.y, beamY = r.y + r.height * 0.3f, pan = r.height * 0.34f;
        Box(new Rect(cx - w / 2, top, w, r.height - pan * 0.4f), c);
        Box(new Rect(r.x, beamY - w / 2, r.width, w), c);
        Box(new Rect(cx - 3, beamY - 3, 6, 6), c, 3);
        foreach (float side in new[] { r.x + pan * 0.5f, r.xMax - pan * 0.5f })
        {
            Box(new Rect(side - w / 2, beamY, w, pan * 0.7f), c);
            var bowl = new Rect(side - pan / 2, beamY + pan * 0.55f, pan, pan * 0.5f);
            Border(bowl, c, w, pan * 0.5f);
        }
        Box(new Rect(cx - r.width * 0.16f, r.yMax - w, r.width * 0.32f, w), c, 0);
    }

    static string Signed(int d) => d > 0 ? "+" + d : d.ToString();

    // ---------- OnGUI ----------
    void OnGUI()
    {
        if (st == null)
        {
            fReg = Resources.Load<Font>("Fonts/LB-400"); fBold = Resources.Load<Font>("Fonts/LB-700"); fIt = Resources.Load<Font>("Fonts/LB-400i");
            sReg = Resources.Load<Font>("Fonts/Inter-Regular"); sSemi = Resources.Load<Font>("Fonts/Inter-SemiBold");
            st = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            area = new GUIStyle(GUI.skin.textArea) { font = fReg, fontSize = 17, wordWrap = true, padding = new RectOffset(18, 18, 16, 16) };
            foreach (var s in new[] { area.normal, area.focused, area.hover, area.active }) { s.background = null; s.textColor = Ink; }
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
        if (phase != Phase.Loading && phase != Phase.Menu) MenuBtn();
        if (mp) MpBanner();
    }

    void MenuBtn()
    {
        if (!Btn(new Rect(W - 128, 10, 104, 32), "Menu", false, phase != Phase.Judging && !generating)) return;
        if (mp) { MpLeave(); return; }
        if (amb != null) amb.Stop();
        sceneEndT = -1;
        Go(Phase.Menu);
    }

    void DrawLoading()
    {
        Scales(new Rect(W / 2 - 20, 254, 40, 46), A(Gold, 0.9f), 2);
        Txt(new Rect(0, 312, W, 64), "VERDICT", 46, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Lbl(new Rect(0, 384, W, 20), loadErr ?? "Preparing the courtroom", 13, loadErr != null ? Red : Muted, TextAnchor.MiddleCenter);
    }

    void DrawMenu()
    {
        if (hero != null)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.16f), 0, 0);
            GUI.DrawTexture(new Rect(0, 230, W, 420), grad, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.85f), 0, 0);
        }
        Txt(new Rect(0, 36, W, 64), "VERDICT", 52, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        TagR(new Rect(0, 100, W, 16), "The AI Courtroom", Muted, TextAnchor.MiddleCenter);
        Txt(new Rect(0, 130, W, 28), "You are the defence lawyer. Answer the prosecution with real law, and win the jury.", 17, Ink, TextAnchor.MiddleCenter);

        float tw = 360, gap = 30, x0 = (W - (3 * tw + 2 * gap)) / 2;
        float by = 664, bh = 44, bw = (3 * tw + 2 * gap - 20) / 2;
        var cr = new Rect(x0, by, bw, bh);
        if (Btn(cr, "", false)) { genErr = null; createForMp = false; Go(Phase.Create); }
        Lbl(new Rect(cr.x + 24, cr.y + 6, cr.width - 48, 18), "Create Your Own Case", 14, Ink, TextAnchor.UpperLeft, true);
        Lbl(new Rect(cr.x + 24, cr.y + 24, cr.width - 48, 16), "Describe a situation - Mistral AI drafts the trial", 11, Muted);
        var mr = new Rect(x0 + bw + 20, by, bw, bh);
        if (Btn(mr, "", false)) OpenLobby();
        Lbl(new Rect(mr.x + 24, mr.y + 6, mr.width - 48, 18), "Multiplayer", 14, Ink, TextAnchor.UpperLeft, true);
        Lbl(new Rect(mr.x + 24, mr.y + 24, mr.width - 48, 16), "Prosecution vs. defence, on two screens", 11, Muted);
        if (Btn(new Rect(W - 150, 36, 110, 30), voiceOn ? "Voice: On" : "Voice: Off", false)) { voiceOn = !voiceOn; Hush(); }

        var built = new List<int>();
        for (int i = 0; i < content.cases.Count; i++) if (!IsGen(content.cases[i])) built.Add(i);
        const float imgH = 240, ch = 372, rg = 28;
        var view = new Rect(x0 - 8, 168, 3 * tw + 2 * gap + 40, 468);
        int rows = (built.Count + 2) / 3;
        float contentH = rows * ch + (rows - 1) * rg + 16, maxS = Mathf.Max(0, contentH - view.height);
        var ev = Event.current;
        bool inView = view.Contains(ev.mousePosition);
        var track = new Rect(view.xMax - 10, view.y + 8, 4, view.height - 16);
        if (ev.type == EventType.ScrollWheel && inView)
        {
            menuScroll.y = Mathf.Clamp(menuScroll.y + Mathf.Sign(ev.delta.y) * Mathf.Max(80, Mathf.Abs(ev.delta.y) * 25), 0, maxS);
            ev.Use();
        }
        if (maxS > 0 && (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && Grow(track, 8).Contains(ev.mousePosition))
        {
            menuScroll.y = Mathf.Clamp01((ev.mousePosition.y - track.y) / track.height) * maxS;
            ev.Use();
        }
        menuScroll.y = Mathf.Clamp(menuScroll.y, 0, maxS);
        menuScroll = GUI.BeginScrollView(view, menuScroll, new Rect(0, 0, view.width - 24, Mathf.Max(contentH, view.height)), GUIStyle.none, GUIStyle.none);
        for (int nb = 0; nb < built.Count; nb++)
        {
            int i = built[nb];
            var c = content.cases[i];
            var r = new Rect(8 + nb % 3 * (tw + gap), 8 + nb / 3 * (ch + rg), tw, ch);
            bool h = inView && Hover(r);
            var img = Tex(Look(c) + "_1");
            var ir = new Rect(r.x, r.y, r.width, imgH);
            if (img != null) GUI.DrawTexture(ir, img, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, h ? 1 : 0.85f), Vector4.zero, new Vector4(10, 10, 0, 0));
            Box(new Rect(r.x, ir.yMax, r.width, r.height - ir.height), A(Panel, h ? 0.95f : 0.75f), 0);
            Border(r, h ? Ink : Line, 1, 10);
            float x = r.x + 20, w = r.width - 40;
            Tag(x, ir.yMax + 12, "Case " + (nb + 1), Muted);
            if (won.Contains(c.id)) TagR(new Rect(x, ir.yMax + 12, w, 16), "Acquitted", Green, TextAnchor.UpperRight);
            Txt(new Rect(x, ir.yMax + 30, w, 26), c.title, 19, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            Lbl(new Rect(x, ir.yMax + 58, w, 16), c.charge + "   \u00b7   " + c.law, 11, Sub);
            if (Btn(new Rect(x, r.yMax - 44, w, 32), "Defend This Client", true) && inView) StartCase(i);
        }
        GUI.EndScrollView();
        if (maxS > 0)
        {
            Box(track, A(Line, 0.8f), 2);
            float th = track.height * view.height / contentH;
            Box(new Rect(track.x, track.y + (track.height - th) * menuScroll.y / maxS, track.width, th), A(Gold, 0.85f), 2);
            if (menuScroll.y < maxS - 10)
                Lbl(new Rect(view.x, view.yMax + 2, view.width - 24, 18), "Scroll down for cases 4\u2013" + built.Count + "  \u2193", 11, Sub, TextAnchor.MiddleRight);
        }
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
        Lbl(new Rect(40, 0, 700, 64), CaseLabel + "   \u00b7   " + cs.title, 13, Ink, TextAnchor.MiddleLeft, true);
        Box(new Rect(W - 430, 28, 8, 8), A(Red, 0.5f + 0.5f * Mathf.Sin(Time.time * 4)), 4);
        TagR(new Rect(W - 415, 0, 245, 64), "Reconstruction of the Facts", Muted, TextAnchor.MiddleLeft);

        if (cs.scenes != null && k < cs.scenes.Count)
        {
            float ca = Mathf.Clamp01((lt - 0.3f) / 0.5f);
            Txt(new Rect(140, H - 64 - 120, W - 280, 100), cs.scenes[k], 24, A(Color.white, ca), TextAnchor.MiddleCenter, FontStyle.Italic);
        }
        Box(new Rect(0, H - 64, W * Mathf.Clamp01((k + Mathf.Clamp01(lt / per)) / 3f), 2), A(Gold, 0.7f));
        Lbl(new Rect(40, H - 62, 200, 62), (k + 1) + " / 3", 12, Muted, TextAnchor.MiddleLeft, true);
        if (Btn(new Rect(W - 170, H - 52, 130, 40), "Skip", false)) { EndScene(); return; }
        if (sceneEndT >= 0) Box(new Rect(0, 0, W, H), A(Color.black, Mathf.Clamp01((Time.time - sceneEndT) / 0.5f)));
    }

    void DrawBrief()
    {
        var left = new Rect(70, 60, 640, 600);
        Tag(left.x, left.y, "Case File · " + CaseLabel, Muted);
        float y = Para(left.x, left.y + 26, left.width, cs.title, 42, Ink, FontStyle.Bold, 8);
        y = Para(left.x, y, left.width, (IsPros ? "The accused: " : "Your client: ") + cs.client, 17, Muted, FontStyle.Normal, 28);
        Tag(left.x, y, "The Facts", Sub);
        y += 28;
        foreach (var f in cs.facts)
        {
            Box(new Rect(left.x, y + 9, 6, 6), Sub, 3);
            y = Para(left.x + 20, y, left.width - 20, f, 18, Ink, FontStyle.Normal, 12);
        }

        var right = new Rect(760, 60, 450, 520);
        PanelBox(right, "THE CHARGE");
        float x = right.x + 24, w = right.width - 48;
        y = Para(x, right.y + 46, w, cs.charge, 24, Ink, FontStyle.Bold, 4);
        y = Para(x, y, w, cs.law, 15, Sub, FontStyle.Normal, 12);
        y = Para(x, y, w, cs.definition, 16, Muted, FontStyle.Italic, 26);
        Box(new Rect(x, y, w, 1), Line);
        y += 18;
        Tag(x, y, "Your Objective", Sub);
        y = Para(x, y + 24, w, BriefGoal, 17, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, BriefHow, 15, Muted, FontStyle.Normal, 22);
        if (IsGen(cs))
        {
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 16, w, "This case was written by Mistral AI from your description. Treat its legal details as a learning aid, not legal advice.", 13, Muted, FontStyle.Italic);
        }

        if (mp) MpBriefButtons();
        else
        {
            if (Btn(new Rect(760, 600, 300, 52), "Enter the Courtroom", true)) { Go(Phase.Trial); resolved = false; Play("gavel"); SpeakMove(); }
            if (Btn(new Rect(1076, 600, 134, 52), "Back", false)) Go(Phase.Menu);
        }
        if (!IsGen(cs) && Btn(new Rect(70, 600, 240, 52), "Replay the Facts", false)) PlayScene();
    }

    void TopBar()
    {
        Box(new Rect(0, 52, W, 1), Line);
        Txt(new Rect(24, 0, 160, 52), "VERDICT", 17, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Lbl(new Rect(0, 0, W, 52), cs.title + "   \u00b7   " + cs.charge + "  (" + cs.law + ")", 13, Sub, TextAnchor.MiddleCenter);
        string right = (phase == Phase.Report ? "Analysis" : phase == Phase.Trial || phase == Phase.MpTrial ? "Round " + (round + 1) + " / " + Rounds : phase == Phase.End || phase == Phase.MpEnd ? "Judgment" : "Closing")
            + (mp ? "  \u00b7  " + Side(mpRole) : "");
        TagR(new Rect(W - 552, 18, 400, 16), right, Sub, TextAnchor.MiddleRight);
    }

    void DrawMeter(Rect r)
    {
        Tag(r.x, r.y + 2, "Jury", Muted);
        bool ok = meterShown >= Threshold - 0.5f;
        Lbl(new Rect(r.x, r.y - 7, r.width, 30), Mathf.RoundToInt(meterShown) + "% Not Guilty", 21, ok ? Green : Ink, TextAnchor.UpperRight, true);
        var track = new Rect(r.x, r.y + 30, r.width, 10);
        Box(track, Line, 5);
        float fw = track.width * Mathf.Clamp01(meterShown / 100f);
        if (fw > 2) Box(new Rect(track.x, track.y, fw, track.height), ok ? Green : Gold, 5);
        float tx = track.x + track.width * Threshold / 100f;
        Box(new Rect(tx - 1, track.y - 5, 2, track.height + 10), Ink);
        TagR(new Rect(r.x, track.yMax + 8, 200, 16), "Guilty", Red);
        Lbl(new Rect(tx - 100, track.yMax + 8, 200, 16), "acquittal at " + Threshold + "%", 11, Muted, TextAnchor.UpperCenter);
        TagR(new Rect(r.xMax - 200, track.yMax + 8, 200, 16), "Not Guilty", Green, TextAnchor.UpperRight);
        float age = Time.time - popT;
        if (age < 2.2f)
            Lbl(new Rect(r.xMax + 12, r.y - 4 - age * 14, 90, 40), Signed(popDelta) + "%", 24, A(popDelta >= 0 ? Green : Red, 1 - age / 2.2f), TextAnchor.MiddleLeft, true);
    }

    void CaseFilePanel(Rect r)
    {
        PanelBox(r, "Case File", false);
        float x = r.x, w = r.width;
        float y = Para(x, r.y + 36, w, "Client: " + cs.client, 13, Muted, FontStyle.Normal, 16);
        foreach (var f in cs.facts)
        {
            Box(new Rect(x, y + 6, 4, 4), Sub, 2);
            y = Para(x + 14, y, w - 14, f, 13, Ink, FontStyle.Normal, 12);
        }
    }

    void RecordPanel(Rect r)
    {
        PanelBox(r, "Court Record", false);
        float x = r.x, w = r.width, y = r.y + 36;
        Lbl(new Rect(x, y, w, 16), "Jury started at " + cs.start + "%", 12, Muted);
        y += 24;
        if (record.Count == 0) Lbl(new Rect(x, y, w, 32), "Your arguments will be recorded here.", 13, Muted);
        foreach (var e in record)
        {
            var p = e.Split('|');
            int d = int.Parse(p[1]);
            Lbl(new Rect(x, y, w, 18), Signed(d) + "%", 13, d >= 0 ? Green : Red, TextAnchor.UpperRight, true);
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
            Lbl(new Rect(340, 380, 600, 20), hint, 13, Sub, TextAnchor.MiddleCenter);
            if (Btn(new Rect(520, 414, 240, 42), "Present " + selected.Count + " / " + MaxPick + " Cards", true, selected.Count > 0)) Present();
        }
        else
        {
            float age = Time.time - resolveT;
            DrawReactions(center, age);
            if (age > 0.4f + reactions.Count * 0.55f && Btn(new Rect(520, 414, 240, 42), round + 1 < cs.moves.Count ? "Next Round" : "Closing Argument", true)) NextRound();
        }
        DrawHand();
    }

    void MovePanel(Rect center, MoveDef mv)
    {
            Box(center, Panel, 12);
            Border(center, A(Red, 0.5f), 1.5f, 12);
            Box(new Rect(center.x, center.y + 14, 3, center.height - 28), Red, 2);
            float x = center.x + 26, w = center.width - 52;
            TagR(new Rect(x, center.y + 18, w, 18), "The Prosecution Argues", Red);
            Lbl(new Rect(x, center.y + 18, w, 18), mv.law, 11, Muted, TextAnchor.UpperRight);
            float y = Para(x, center.y + 48, w, mv.title, 25, Ink, FontStyle.Bold, 10);
            Para(x, y, w, "“" + mv.text + "”", 17, Ink, FontStyle.Italic);
            Lbl(new Rect(x, center.yMax - 34, w, 20), (IsPros ? "If the defence can't answer it: " : "If you don't answer it: ") + Signed(-mv.power) + "% jury", 13, Red, TextAnchor.UpperLeft, true);
    }

    void DrawReactions(Rect center, float age)
    {
            TagR(new Rect(center.x, center.y - 6, center.width, 20), "The Jury Reacts", Sub);
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
                Lbl(new Rect(row.x + 12, row.y + 10, 56, 28), re.delta == 0 ? "0" : Signed(re.delta), 15, A(col, a), TextAnchor.MiddleCenter, true);
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
            Box(r, sel ? Hex("242C42") : Panel2, 10);
            Border(r, sel ? Gold : h ? Sub : Line, sel ? 2f : 1, 10);
            var kc = KindColor(c.kind);
            Box(new Rect(r.x + 1, r.y + 1, r.width - 2, 4), A(kc, dim), 2);
            float x = r.x + 14, w = r.width - 28;
            Lbl(new Rect(x, r.y + 14, w, 16), Spaced(c.kind), 10, A(kc, dim), TextAnchor.UpperLeft, true);
            float y = Para(x, r.y + 34, w, c.name, 15, A(Ink, dim), FontStyle.Bold, 6);
            Lbl(new Rect(x, y, w, 14), c.law, 11, A(Sub, dim));
            y += 18;
            Box(new Rect(x, y, w, 1), Line);
            Para(x, y + 12, w, c.plain, 12, A(Muted, dim));
            if (sel) TagR(new Rect(r.x, r.yMax - 22, r.width, 16), "Selected", Gold, TextAnchor.MiddleCenter);
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
        PanelBox(p, "Closing Argument");
        float x = p.x + 30, w = p.width - 60;
        float y = Para(x, p.y + 42, w, ClosingTitle, 25, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, (mp ? ClosingHelp : "Explain which legal condition of the charge is missing (or which defence applies), link it to the facts and cite the article. Mistral AI judges it live."), 14, Muted, FontStyle.Normal, 14);
        y = LPara(x, y, w, (IsPros ? "The defence relied on: " + string.Join("  ·  ", played.Select(c => c.name)) : "Law on your side: " + string.Join("  ·  ", played.Where(c => c.power > 0).Select(c => c.name + " (" + c.law + ")"))), 12, Sub, false, 18);
        var ta = new Rect(x, y, w, p.yMax - y - 80);
        Border(Grow(ta, 1), Line, 1, 8);
        GUI.SetNextControlName("arg");
        GUI.enabled = !MpSubmitted;
        argument = GUI.TextArea(ta, argument, 1500, area);
        GUI.enabled = true;
        if (focusArg) { GUI.FocusControl("arg"); if (GUI.GetNameOfFocusedControl() == "arg") focusArg = false; }
        if (string.IsNullOrEmpty(argument))
            Txt(new Rect(ta.x + 18, ta.y + 16, ta.width - 36, 60), (IsPros ? "e.g. \"Members of the jury, every element of " + cs.law + " is proven...\"" : "e.g. \"Members of the jury, theft requires fraudulent intent under article 311-1...\""), 17, A(Muted, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
        Lbl(new Rect(x, p.yMax - 60, 360, 44), micErr ?? (MpSubmitted ? "Delivered. Waiting for the other side's closing" + Dots : argument.Trim().Length < 20 ? "Write or say at least a couple of sentences." : argument.Length + " characters"), 13, micErr != null ? Red : Muted, TextAnchor.MiddleLeft);
        if (!MpSubmitted) MicBtn(new Rect(p.xMax - 330 - 16 - 260, p.yMax - 64, 260, 48), false);
        if (Btn(new Rect(p.xMax - 330, p.yMax - 64, 300, 48), "Deliver to the Jury", true, argument.Trim().Length >= 20 && !MpSubmitted && !mpBusy))
        {
            if (mp) { Hush(); MpAct("closing", text: argument.Trim()); }
            else SubmitClosing();
        }
    }

    void DrawCreate()
    {
        if (hero != null) GUI.DrawTexture(new Rect(0, 0, W, H), hero, ScaleMode.ScaleAndCrop, false, 0, A(Color.white, 0.12f), 0, 0);
        var p = new Rect(190, 60, 900, 600);
        PanelBox(p, createForMp ? "New Case for Multiplayer" : "New Case");
        float x = p.x + 36, w = p.width - 72;
        float y = Para(x, p.y + 48, w, "Tell the court what happened.", 28, Ink, FontStyle.Bold, 10);
        y = Para(x, y, w, "Describe any situation, real or invented. " + (createForMp ? "Mistral AI turns it into a full trial under French criminal law. It becomes the case for your multiplayer room: the prosecution gets the accusations, the defence the law cards." : "Mistral AI turns it into a full trial under French criminal law - the charge, the facts, the prosecution's points and your law cards."), 15, Muted, FontStyle.Normal, 18);
        var ta = new Rect(x, y, w, 150);
        Border(Grow(ta, 1), Line, 1, 8);
        GUI.enabled = !generating;
        GUI.SetNextControlName("desc");
        desc = GUI.TextArea(ta, desc, 800, area);
        GUI.enabled = true;
        if (string.IsNullOrEmpty(desc))
            Txt(new Rect(ta.x + 18, ta.y + 16, ta.width - 36, 60), "e.g. \"My neighbour borrowed my scooter without asking and brought it back the next day with a scratch.\"", 17, A(Muted, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
        y = ta.yMax + 22;
        Tag(x, y, "Or Start From an Example", Muted);
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
        if (status != null) Lbl(new Rect(x, p.yMax - 112, w, 22), status, 14, generating ? Sub : Red, TextAnchor.MiddleLeft);
        if (!generating) MicBtn(new Rect(x, p.yMax - 76, 260, 48), true);
        if (Btn(new Rect(p.xMax - 36 - 300, p.yMax - 76, 300, 48), generating ? "Drafting the Case..." : "Generate the Case", true, !generating && mic == Mic.Idle && desc.Trim().Length >= 15)) StartCoroutine(Generate());
        if (Btn(new Rect(p.xMax - 36 - 300 - 16 - 130, p.yMax - 76, 130, 48), "Back", false, !generating)) Go(createForMp ? Phase.Lobby : Phase.Menu);
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
        Txt(new Rect(0, 474, W, 24), mp ? "Mistral AI is weighing both closing arguments against the facts and the law" : "Mistral AI is weighing your closing argument against the facts and the law", 15, Muted, TextAnchor.MiddleCenter);
    }

    void DrawEnd()
    {
        TopBar();
        bool acquit = meter >= Threshold;
        Scales(new Rect(W / 2 - 16, 58, 32, 36), A(acquit ? Green : Red, 0.8f), 1.5f);
        Txt(new Rect(0, 100, W, 60), acquit ? "Not Guilty" : "Guilty", 46, acquit ? Green : Red, TextAnchor.MiddleCenter, FontStyle.Bold);
        Lbl(new Rect(0, 152, W, 20), acquit ? "Your client walks free." : "The jury was not convinced - see what you can learn below, then try again.", 14, Muted, TextAnchor.MiddleCenter);
        DrawMeter(new Rect(340, 176, 600, 70));

        var l = new Rect(60, 260, 570, 390);
        PanelBox(l, "The Judge on Your Closing");
        float x = l.x + 24, w = l.width - 48;
        var col = verdict.score > 0 ? Green : verdict.score < 0 ? Red : Muted;
        Box(new Rect(x, l.y + 44, 60, 32), A(col, 0.16f), 6);
        Lbl(new Rect(x, l.y + 44, 60, 32), Signed(verdict.score) + "%", 16, col, TextAnchor.MiddleCenter, true);
        float y = Para(x + 76, l.y + 48, w - 76, verdict.headline, 18, Ink, FontStyle.Bold, 14);
        y = Mathf.Max(y, l.y + 92);
        y = Para(x, y, w, verdict.feedback, 15, Ink, FontStyle.Normal, 12);
        foreach (var s in verdict.strengths) y = Para(x, y, w, "+  " + s, 14, Green, FontStyle.Normal, 4);
        y += 4;
        foreach (var s in verdict.missed) y = Para(x, y, w, "-  " + s, 14, Red, FontStyle.Normal, 4);

        var r = new Rect(650, 260, 570, 390);
        PanelBox(r, "What You Learned");
        x = r.x + 24; w = r.width - 48;
        y = Para(x, r.y + 44, w, cs.takeaway, 16, Sub, FontStyle.Bold, 14);
        foreach (var c in played)
        {
            Box(new Rect(x, y + 5, 8, 8), c.power > 0 ? Green : Red, 4);
            y = Para(x + 18, y, w - 18, c.name + " (" + c.law + "): " + c.lesson, 12, Ink, FontStyle.Normal, 6);
        }

        if (Btn(new Rect(W / 2 - 390, 664, 240, 44), "Retry This Case", false)) StartCase(caseIdx, false);
        ReportBtn(new Rect(W / 2 - 130, 664, 260, 44), report, reportLoading);
        bool more = caseIdx + 1 < content.cases.Count;
        if (Btn(new Rect(W / 2 + 150, 664, 240, 44), more ? "Next Case" : "All Cases", true))
        {
            if (more) StartCase(caseIdx + 1); else Go(Phase.Menu);
        }
    }
}
