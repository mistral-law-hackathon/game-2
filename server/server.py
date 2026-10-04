"""Serves the WebGL build and proxies Mistral calls (forge + boss taunts); the API key stays server-side."""
import hashlib
import json
import os
import random
import re
import urllib.request
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import content as C

ROOT = Path(__file__).resolve().parent.parent
WEB_DIR = ROOT / "webgl"


def load_env():
    env_file = ROOT / ".env"
    if env_file.exists():
        for line in env_file.read_text().splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip())


load_env()
API_KEY = os.environ.get("MISTRAL_API_KEY", "")
MODEL = os.environ.get("MISTRAL_MODEL", "mistral-medium-latest")
BOSS = {b["id"]: b for b in C.BOSSES}
forge_count = 0


def mistral_json(system, user, temperature=0.8, timeout=25):
    if not API_KEY:
        raise RuntimeError("no MISTRAL_API_KEY")
    body = json.dumps({
        "model": MODEL, "temperature": temperature, "response_format": {"type": "json_object"},
        "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
    }).encode()
    req = urllib.request.Request("https://api.mistral.ai/v1/chat/completions", data=body,
                                 headers={"Authorization": f"Bearer {API_KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(json.loads(resp.read())["choices"][0]["message"]["content"])


def clamp(v, lo, hi, default):
    try:
        return max(lo, min(hi, int(round(float(v)))))
    except (TypeError, ValueError):
        return default


def color(v, default):
    v = str(v or "")
    return v if re.fullmatch(r"#[0-9a-fA-F]{6}", v) else default


WITNESS_TGT = {"pick_enemy": "random_enemy", "pick_enemy_minion": "random_enemy_minion", "pick_minion": "self"}


def sanitize(raw):
    card = {"type": "motion" if str(raw.get("type", "")).lower().startswith(("m", "s")) else "witness"}
    card["name"] = str(raw.get("name") or "Mystery Card")[:26]
    card["flavor"] = str(raw.get("flavor") or "")[:90]
    card["cost"] = clamp(raw.get("cost"), 1, 10, 3)
    card["atk"] = clamp(raw.get("atk"), 0, 12, 2) if card["type"] == "witness" else 0
    card["hp"] = clamp(raw.get("hp"), 1, 12, 2) if card["type"] == "witness" else 0
    card["art"] = raw.get("art") if raw.get("art") in C.ART else "objection"
    card["kw"] = list(dict.fromkeys(k for k in raw.get("kw") or [] if k in C.KW_COST))[:2] if card["type"] == "witness" else []
    effects = []
    for e in (raw.get("fx") or [])[:3]:
        act = e.get("act") if isinstance(e, dict) else None
        if act not in C.ACTS:
            continue
        tgts = C.ACTS[act]
        tgt = e.get("tgt") if e.get("tgt") in tgts else next(iter(tgts))
        when = e.get("when") if e.get("when") in C.WHEN_MULT else "play"
        if card["type"] == "motion":
            when = "play"
        elif tgt in C.PICK:
            tgt = WITNESS_TGT[tgt] if WITNESS_TGT[tgt] in tgts else next(t for t in tgts if t not in C.PICK)
        n_max = 3 if act in ("draw", "summon") else 8
        effects.append({"when": when, "act": act, "tgt": tgt, "n": clamp(e.get("n"), 1, n_max, 1)})
    picks = [e for e in effects if e["tgt"] in C.PICK]
    if picks:  # one chosen target per card
        effects = [e for e in effects if e["tgt"] not in C.PICK or e["tgt"] == picks[0]["tgt"]]
    if card["type"] == "motion" and not effects:
        effects = [{"when": "play", "act": "damage", "tgt": "enemy_hero", "n": 2}]
    card["fx"] = effects
    v = raw.get("vfx") or {}
    card["vfx"] = {"c1": color(v.get("c1"), "#f72585"), "c2": color(v.get("c2"), "#ffd166"),
                   "pattern": v.get("pattern") if v.get("pattern") in C.PATTERNS else "burst",
                   "shake": max(0.0, min(1.0, float(v.get("shake") or 0.4))),
                   "pitch": max(0.5, min(2.0, float(v.get("pitch") or 1.0)))}
    return rebalance(card)


def rebalance(card):
    while C.value(card) > C.budget(card) and card["cost"] < 10:
        card["cost"] += 1
    guard = 0
    while C.value(card) > C.budget(card) and guard < 60:
        guard += 1
        nums = \
            [e for e in card["fx"] if e["act"] not in C.FLAT and e["n"] > 1]
        if card["type"] == "witness" and max(card["atk"], card["hp"]) > 1 and (not nums or max(card["atk"], card["hp"]) >= max(e["n"] for e in nums)):
            if card["atk"] >= card["hp"]:
                card["atk"] -= 1
            else:
                card["hp"] -= 1
        elif nums:
            max(nums, key=lambda e: e["n"])["n"] -= 1
        elif card["kw"]:
            card["kw"].pop()
        elif len(card["fx"]) > 1:
            card["fx"].pop()
        else:
            break
    global forge_count
    forge_count += 1
    card.update(id=f"forged_{forge_count}", forged=True, text=C.describe(card))
    return card


FORGE_SYSTEM = f"""You are THE FORGE in "OBJECTION!", a comedic courtroom card battler (like Hearthstone).
The player describes any idea in plain words; you design ONE fun, flavourful, playable card that captures it.
Card types: "witness" (a creature with atk/hp that fights) or "motion" (a one-shot spell). Cost 1-10 mana.
Keywords (witness only, optional, max 2): "taunt" (enemies must attack it), "charge" (can attack immediately), "shield" (ignores the first damage).
Effects "fx": list (max 3) of {{"when","act","tgt","n"}}:
- when: "play" (on play), "death" (when it dies, witness only), "turn" (start of each of your turns, witness only). Motions always use "play".
- act -> allowed tgt: {json.dumps({a: list(t) for a, t in C.ACTS.items()})}
  ("none" = no target; summon creates n 1/1 Paralegals; pick_* targets are chosen by the player, motions only.)
- n: amount (1-8; draw/summon 1-3).
Art: pick the best "art" key for the portrait: {json.dumps(C.ART)}
VFX when played: {{"c1": "#rrggbb", "c2": "#rrggbb", "pattern": one of {C.PATTERNS}, "shake": 0-1, "pitch": 0.5-2 (sound pitch)}}. Match the vibe of the idea.
Balance roughly like Hearthstone (a 3-cost witness is ~3/4). Be generous and fun; the game rebalances anyway.
Name: short, punny, max 24 chars. Flavor: one funny sentence, max 80 chars. "announce": a dramatic courtroom-announcer line (max 15 words) revealing the card.
Return ONLY JSON: {{"type","name","cost","atk","hp","kw","fx","art","vfx","flavor","announce"}}"""


def offline_card(prompt):
    rnd = random.Random(hashlib.md5(prompt.encode()).hexdigest())
    words = [w.capitalize() for w in re.findall(r"[A-Za-z]+", prompt)][:3] or ["Mystery"]
    raw = {"type": "witness", "name": " ".join(words), "cost": rnd.randint(2, 6), "atk": rnd.randint(2, 6), "hp": rnd.randint(2, 6),
           "kw": [rnd.choice(["taunt", "charge", "shield"])], "art": rnd.choice(list(C.ART)),
           "fx": [{"when": "play", "act": "damage", "tgt": "random_enemy", "n": rnd.randint(1, 3)}],
           "vfx": {"c1": "#f72585", "c2": "#4cc9f0", "pattern": rnd.choice(C.PATTERNS), "shake": 0.5, "pitch": 1.0},
           "flavor": "Forged offline. Still legally binding."}
    return raw


def forge(data):
    prompt = str(data.get("prompt", "")).strip()[:200] or "a mysterious surprise witness"
    try:
        raw = mistral_json(FORGE_SYSTEM, f"Player idea: {prompt}", 0.9)
        announce = str(raw.get("announce", ""))[:120]
    except Exception as e:  # never block the demo on the LLM
        print("forge fallback:", repr(e))
        raw, announce = offline_card(prompt), "The court accepts this... improvised evidence!"
    card = sanitize(raw)
    return {"card": card, "announce": announce or f"Presenting... {card['name']}!"}


def taunt(data):
    boss = BOSS.get(data.get("boss"), C.BOSSES[0])
    event = str(data.get("event", "turn"))
    detail = str(data.get("detail", ""))[:300]
    try:
        out = mistral_json(
            f"You are {boss['persona']} You are dueling a rookie lawyer in a comedic courtroom card game. "
            "React in character to what just happened with ONE short, funny line (max 18 words). No hashtags spam, no emojis. "
            'Return ONLY JSON: {"line": string}',
            f"Event: {event}. Details: {detail}. Boss HP {data.get('boss_hp')}, rookie HP {data.get('player_hp')}.",
            1.0, timeout=10)
        line = str(out.get("line", "")).strip()[:160]
        if line:
            return {"line": line}
    except Exception as e:
        print("taunt fallback:", repr(e))
    return {"line": boss["lines"].get(event, boss["lines"]["turn"])}


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(WEB_DIR), **kw)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def send_json(self, obj, code=200):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path.startswith("/api/content"):
            return self.send_json(C.public_content())
        return super().do_GET()

    def do_POST(self):
        fn = {"/api/forge": forge, "/api/taunt": taunt}.get(self.path)
        if not fn:
            return self.send_json({"error": "not found"}, 404)
        try:
            data = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
            self.send_json(fn(data))
        except Exception as e:
            self.log_error("api error: %r", e)
            self.send_json({"error": str(e)}, 500)


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8080"))
    print(f"Serving {WEB_DIR} on http://localhost:{port} (model {MODEL}, key {'set' if API_KEY else 'MISSING'})")
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()
