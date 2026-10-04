using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class Fx { public string when, act, tgt; public int n; }
[Serializable] public class Vfx { public string c1, c2, pattern; public float shake, pitch; }

[Serializable]
public class CardDef
{
    public string id, name, type, art, flavor, text;
    public int cost, atk, hp;
    public List<string> kw = new();
    public List<Fx> fx = new();
    public Vfx vfx;
    public bool forged;
    public bool IsWitness => type == "witness";
}

[Serializable]
public class Lines
{
    public string start, hit, low, turn, forge, win, lose;
    public string Get(string e) => e switch
    {
        "start" => start, "hit" => hit, "low" => low, "forge" => forge, "win" => win, "lose" => lose, _ => turn
    };
}

[Serializable] public class BossDef { public string id, name, art, title; public int hp; public List<string> deck; public Lines lines; }
[Serializable] public class Content { public List<CardDef> cards; public CardDef token; public List<string> starter, rewards; public List<BossDef> bosses; }
[Serializable] public class ForgeReq { public string prompt; }
[Serializable] public class ForgeResp { public CardDef card; public string announce; }
[Serializable] public class TauntReq { public string boss, @event, detail; public int boss_hp, player_hp; }
[Serializable] public class TauntResp { public string line; }

public class Side
{
    public bool isPlayer;
    public int hp, maxHp, mana, maxMana, fatigue;
    public List<CardDef> deck = new(), hand = new();
    public List<Unit> board = new();
    public Side foe;
    public float heroShake, heroFlash;
}

public class Unit
{
    public CardDef def;
    public int atk, hp, maxHp;
    public bool taunt, charge, shield, sleeping, attacked, lunging;
    public List<Fx> fx;
    public Side owner;
    public Vector2 pos;
    public float pop, flash, shake;
}

public class Target
{
    public Unit unit;
    public Side hero;
}
