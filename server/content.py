"""Game content: cards, decks, bosses and the balance rules shared by the forge."""

ACTS = {
    # act: {target: value per point of n (or flat value when n is unused)}
    "damage": {"pick_enemy": 1.0, "pick_minion": 1.0, "pick_enemy_minion": 1.0, "enemy_hero": 0.7,
               "random_enemy": 0.8, "random_enemy_minion": 0.8, "all_enemy_minions": 2.0,
               "all_enemies": 2.5, "all_minions": 1.3},
    "heal": {"own_hero": 0.4, "own_minions": 0.8, "self": 0.4, "pick_minion": 0.5},
    "draw": {"none": 1.6},
    "buff": {"self": 2.0, "pick_minion": 2.0, "own_minions": 3.0},
    "summon": {"none": 2.0},
    "silence": {"pick_enemy_minion": 1.5, "all_enemy_minions": 3.5},
    "steal": {"pick_enemy_minion": 8.0, "random_enemy_minion": 6.0},
    "destroy": {"pick_enemy_minion": 5.0, "random_enemy_minion": 4.0},
}
FLAT = {"silence", "steal", "destroy"}
WHEN_MULT = {"play": 1.0, "death": 0.8, "turn": 2.0}
KW_COST = {"taunt": 1.0, "shield": 1.5, "charge": None}  # charge = 0.5 + 0.5 * atk
PICK = {"pick_enemy", "pick_minion", "pick_enemy_minion"}
PATTERNS = ["burst", "spiral", "rain", "beam", "shockwave", "confetti"]

TOKEN = {"id": "paralegal", "name": "Paralegal", "type": "witness", "cost": 1, "atk": 1, "hp": 1,
         "art": "intern", "flavor": "Unpaid. Unstoppable.", "kw": [], "fx": []}

ART = {
    "sleepy_juror": "sleepy old juror dozing", "intern": "eager intern with a stack of files",
    "grandma": "fierce grandma with a handbag", "detective": "trench-coat detective with magnifier",
    "pigeon": "pigeon in a tie at the witness stand", "expert": "shady scientist with test tube",
    "bailiff": "huge muscular bailiff", "cat": "smug cat in a lawyer suit", "reporter": "tabloid reporter with camera",
    "judge": "stern judge with gavel", "robot": "retro robot lawyer", "dog": "police sniffer dog",
    "ghost": "friendly ghost in a suit", "influencer": "influencer with ring light and sunglasses",
    "gavel": "giant glowing gavel (object)", "papers": "tornado of flying documents (object)",
    "objection": "lawyer pointing dramatically", "dna": "glowing DNA test tube (object)",
    "coffee": "giant espresso with lightning (object)",
}


def fx(act, to="none", n=1, when="play"):
    return {"when": when, "act": act, "tgt": to, "n": n}


def vfx(c1, c2, pattern="burst", shake=0.3, pitch=1.0):
    return {"c1": c1, "c2": c2, "pattern": pattern, "shake": shake, "pitch": pitch}


def W(id, name, cost, atk, hp, art, flavor, kw=(), effects=(), v=None):
    return {"id": id, "name": name, "type": "witness", "cost": cost, "atk": atk, "hp": hp, "art": art,
            "flavor": flavor, "kw": list(kw), "fx": list(effects), "vfx": v or vfx("#ffd166", "#ffffff")}


def M(id, name, cost, art, flavor, effects, v=None):
    return {"id": id, "name": name, "type": "motion", "cost": cost, "atk": 0, "hp": 0, "art": art,
            "flavor": flavor, "kw": [], "fx": list(effects), "vfx": v or vfx("#7bdff2", "#ffffff")}


CARDS = [
    # --- player pool ---
    W("sleepy_juror", "Sleepy Juror", 1, 1, 3, "sleepy_juror", "Has not been awake for a verdict since 1997.", ["taunt"]),
    W("eager_intern", "Eager Intern", 2, 2, 1, "intern", "Brought 400 pages. Nobody asked.", [], [fx("draw", n=1)]),
    W("pigeon", "Pigeon Witness", 2, 2, 1, "pigeon", "Saw everything. From above.", ["charge"]),
    W("k9", "K-9 Sniffer", 2, 2, 3, "dog", "Good boy. Very good lawyer."),
    W("grandma", "Grandma Evidence", 3, 2, 4, "grandma", "\"I KNOW what I saw, young man.\"", ["taunt"],
      [fx("heal", "own_hero", 3, "death")]),
    W("detective", "Hard-Boiled Detective", 3, 3, 2, "detective", "The rain always follows him. Indoors too.", [],
      [fx("damage", "random_enemy", 2)], vfx("#ffb703", "#fb8500", "beam", 0.3)),
    W("ghost", "Ghost of Precedent", 3, 2, 3, "ghost", "Died in 1804. Still cited weekly.", [],
      [fx("summon", n=1, when="death")], vfx("#80ffdb", "#ffffff", "spiral", 0.2)),
    W("cat_attorney", "Cat Attorney", 4, 3, 4, "cat", "Objects to everything. Especially your pen.", ["shield"]),
    W("reporter", "Tabloid Reporter", 4, 3, 3, "reporter", "FLASH! Your reputation is gone.", [],
      [fx("damage", "all_enemy_minions", 1)], vfx("#ffffff", "#ffe066", "shockwave", 0.4)),
    W("bailiff", "The Bailiff", 5, 4, 6, "bailiff", "You shall not pass. Literally.", ["taunt"]),
    W("robo_counsel", "Robo-Counsel 3000", 6, 5, 4, "robot", "Read every law ever written in 0.3 seconds.", [],
      [fx("draw", n=2)], vfx("#4cc9f0", "#b5179e", "spiral", 0.3)),
    W("judge", "The Honourable Judge", 7, 6, 7, "judge", "ORDER! ORDER! ...and a coffee.", ["taunt"]),
    M("objection", "OBJECTION!", 2, "objection", "Shouted with exactly the right finger.", [fx("damage", "pick_enemy", 3)],
      vfx("#ff595e", "#ffca3a", "beam", 0.8, 0.8)),
    M("espresso", "Triple Espresso", 2, "coffee", "Legally a stimulant. Barely.", [fx("buff", "pick_minion", 1), fx("draw", n=1)],
      vfx("#bc6c25", "#ffd166", "spiral", 0.2, 1.4)),
    M("dna_test", "Surprise DNA Test", 3, "dna", "The results are in... and they are NOT the father.",
      [fx("destroy", "pick_enemy_minion")], vfx("#00f5d4", "#9b5de5", "spiral", 0.6, 1.2)),
    M("paper_storm", "Paperwork Storm", 3, "papers", "Death by a thousand forms.",
      [fx("damage", "all_enemies", 1), fx("draw", n=1)], vfx("#ffffff", "#adb5bd", "rain", 0.4)),
    M("gavel_slam", "Gavel Slam", 4, "gavel", "The sound of justice. And of a headache.",
      [fx("damage", "all_enemy_minions", 2), fx("damage", "enemy_hero", 2)], vfx("#ffd166", "#ef476f", "shockwave", 1.0, 0.6)),
    M("loophole", "Legal Loophole", 1, "objection", "Describe ANY card. The court will allow it.", [fx("forge")],
      vfx("#f72585", "#7209b7", "confetti", 0.5, 1.3)),
    # --- boss cards ---
    W("hired_muscle", "Hired Muscle", 2, 3, 2, "bailiff", "Charges by the hour. And by the punch."),
    W("paparazzi", "Paparazzi", 2, 2, 1, "reporter", "No comment? That IS a comment.", ["charge"]),
    W("bribed_expert", "Bribed Expert", 3, 4, 3, "expert", "His PhD came in a cereal box."),
    W("attack_dog", "Attack Dog", 4, 5, 3, "dog", "Was a good boy. Not anymore.", ["charge"]),
    M("shouting_match", "Shouting Match", 1, "objection", "Volume is an argument, right?", [fx("damage", "enemy_hero", 2)],
      vfx("#ff595e", "#ff924c", "shockwave", 0.6, 0.7)),
    M("bulldoze", "BULLDOZE!", 3, "gavel", "Due process? Never heard of her.", [fx("damage", "pick_enemy", 4)],
      vfx("#ffbe0b", "#fb5607", "beam", 0.9, 0.6)),
    W("filing_golem", "Filing Cabinet Golem", 4, 2, 6, "papers", "Contains every form. Opens none.", ["taunt"]),
    W("tired_clerk", "Tired Clerk", 2, 1, 4, "sleepy_juror", "Your appointment is in 2031.", ["taunt"]),
    M("endless_appeal", "Endless Appeal", 3, "papers", "And then we appeal the appeal.", [fx("heal", "own_hero", 5), fx("draw", n=1)],
      vfx("#9b5de5", "#f15bb5", "spiral", 0.2, 1.1)),
    M("red_tape", "Red Tape", 2, "papers", "Your witness needs form 27-B. In triplicate.", [fx("silence", "pick_enemy_minion"), fx("draw", n=1)],
      vfx("#d62828", "#ffffff", "spiral", 0.3)),
    W("follower", "Superfan", 1, 1, 2, "influencer", "Smash that like button, your honour."),
    W("influencer_w", "Viral Witness", 3, 2, 2, "influencer", "2 million people saw it. In a filter.", [],
      [fx("summon", n=1)], vfx("#f72585", "#4cc9f0", "confetti", 0.3, 1.4)),
    M("go_viral", "Go Viral", 4, "influencer", "Trending: #YouLose", [fx("damage", "random_enemy", 2), fx("summon", n=2)],
      vfx("#f72585", "#ffd60a", "confetti", 0.6, 1.5)),
    M("poach", "Poach Witness", 7, "influencer", "\"He follows ME now.\"", [fx("steal", "pick_enemy_minion")],
      vfx("#7209b7", "#f72585", "spiral", 0.7, 0.9)),
]
BY_ID = {c["id"]: c for c in CARDS}

STARTER = ["sleepy_juror", "sleepy_juror", "eager_intern", "eager_intern", "pigeon", "k9", "k9", "grandma",
           "detective", "detective", "ghost", "cat_attorney", "reporter", "bailiff", "robo_counsel", "judge",
           "objection", "objection", "espresso", "dna_test", "paper_storm", "gavel_slam", "loophole", "loophole"]

BOSSES = [
    {"id": "bulldozer", "name": "Maitre Bulldozer", "art": "boss_bulldozer", "hp": 22, "title": "The Loudest Lawyer in Paris",
     "persona": "Maitre Bulldozer: a huge, LOUD, aggressive lawyer who thinks shouting wins cases. Speaks in CAPS sometimes, "
                "brags about crushing rookies, makes construction-machine metaphors.",
     "deck": ["hired_muscle", "hired_muscle", "paparazzi", "paparazzi", "bribed_expert", "bribed_expert", "attack_dog",
              "shouting_match", "shouting_match", "bulldoze", "pigeon", "k9", "detective", "bailiff", "objection"],
     "lines": {"start": "I'LL FLATTEN YOU LIKE A PARKING LOT, ROOKIE!", "hit": "THAT TICKLED!", "low": "No... my bulldozer is stalling!",
               "turn": "BEEP BEEP! Bulldozer coming through!", "forge": "You can't just MAKE UP cards! ...can you?",
               "win": "Back to law school, rookie!", "lose": "Impossible... flattened by a beginner!"}},
    {"id": "paperwork", "name": "Mme Paperwork", "art": "boss_paperwork", "hp": 26, "title": "Queen of Procedure",
     "persona": "Madame Paperwork: an icy, hyper-bureaucratic lawyer who wins by delays and forms. Speaks politely but "
                "passive-aggressively, cites imaginary form numbers, loves stamps and triplicates.",
     "deck": ["tired_clerk", "tired_clerk", "filing_golem", "filing_golem", "endless_appeal", "endless_appeal", "red_tape",
              "red_tape", "sleepy_juror", "grandma", "paper_storm", "bailiff", "judge", "robo_counsel", "gavel_slam"],
     "lines": {"start": "Please take a number. Your defeat will be processed shortly.", "hit": "That will require form 12-C.",
               "low": "This is... highly irregular!", "turn": "Stamp. Stamp. Stamp.", "forge": "That card has not been approved in triplicate!",
               "win": "Case closed. Please exit through the gift shop.", "lose": "I... I appeal! I APPEAL!"}},
    {"id": "influencer", "name": "Lil' Verdict", "art": "boss_influencer", "hp": 30, "title": "Influencer Lawyer, 12M Followers",
     "persona": "Lil' Verdict: a chaotic influencer lawyer who livestreams every trial, talks to chat, uses Gen-Z slang, "
                "begs for likes, and treats the trial as content.",
     "deck": ["follower", "follower", "follower", "influencer_w", "influencer_w", "go_viral", "go_viral", "poach", "paparazzi",
              "paparazzi", "cat_attorney", "reporter", "attack_dog", "dna_test", "objection"],
     "lines": {"start": "Hey chat! Watch me destroy this rookie LIVE!", "hit": "Chat, that's NOT canon.", "low": "Chat is spamming L's... noooo",
               "turn": "Like and subscribe, your honour!", "forge": "Wait that's actually fire, can I have it?",
               "win": "GG EZ. Clip it!", "lose": "Unsubscribing from law forever!"}},
]

REWARDS = ["cat_attorney", "robo_counsel", "dna_test", "gavel_slam", "bribed_expert", "attack_dog", "endless_appeal",
           "red_tape", "influencer_w", "go_viral", "filing_golem", "judge", "reporter", "espresso"]


def value(card):
    v = 0.0
    if card["type"] == "witness":
        v += card["atk"] + card["hp"]
        for k in card["kw"]:
            v += (0.5 + 0.5 * card["atk"]) if k == "charge" else KW_COST.get(k, 0)
    for e in card["fx"]:
        if e["act"] == "forge":
            v += 2
            continue
        per = ACTS[e["act"]][e["tgt"]]
        base = per if e["act"] in FLAT else per * e["n"]
        v += base * WHEN_MULT.get(e["when"], 1.0)
    return v


def budget(card):
    return 2 * card["cost"] + 1 if card["type"] == "witness" else 1.5 * card["cost"] + 1.5


TGT_TEXT = {"pick_enemy": "an enemy", "pick_minion": "a witness", "pick_enemy_minion": "an enemy witness",
            "enemy_hero": "the enemy lawyer", "own_hero": "your lawyer", "random_enemy": "a random enemy",
            "random_enemy_minion": "a random enemy witness", "all_enemy_minions": "all enemy witnesses",
            "all_enemies": "all enemies", "all_minions": "ALL witnesses", "own_minions": "your witnesses", "self": "itself"}
WHEN_TEXT = {"play": "Opening Statement: ", "death": "Last Words: ", "turn": "Each turn: "}
KW_TEXT = {"taunt": "Key Witness", "charge": "Surprise Witness", "shield": "Lawyered Up"}


def describe(card):
    parts = [KW_TEXT[k] for k in card["kw"]]
    for e in card["fx"]:
        a, t, n = e["act"], TGT_TEXT.get(e["tgt"], ""), e["n"]
        s = {"damage": f"Deal {n} to {t}", "heal": f"Restore {n} to {t}", "draw": f"Draw {n} card{'s' if n > 1 else ''}",
             "buff": f"Give {t} +{n}/+{n}", "summon": f"Summon {n} Paralegal{'s' if n > 1 else ''}",
             "silence": f"Silence {t}", "steal": f"Take control of {t}", "destroy": f"Destroy {t}",
             "forge": "FORGE a brand-new card from your words"}[a]
        prefix = WHEN_TEXT[e["when"]] if card["type"] == "witness" else ""
        parts.append(prefix + s)
    return ". ".join(parts) + ("." if parts else "")


for c in CARDS + [TOKEN]:
    c.setdefault("vfx", vfx("#ffd166", "#ffffff"))
    c["text"] = describe(c)
    c.setdefault("forged", False)


def public_content():
    return {"cards": CARDS, "token": TOKEN, "starter": STARTER, "bosses": [
        {k: b[k] for k in ("id", "name", "art", "hp", "title", "deck", "lines")} for b in BOSSES], "rewards": REWARDS}
