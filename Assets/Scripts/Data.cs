using System;
using System.Collections.Generic;

[Serializable] public class CardDef { public string id, name, kind, law, plain, counters, lesson; public int power; }
[Serializable] public class MoveDef { public string id, title, text, law, kind; public int power; }
[Serializable] public class CaseDef
{
    public string id, title, client, charge, law, definition, takeaway, look;
    public int start;
    public List<string> scenes, sfx;
    public List<string> facts;
    public List<MoveDef> moves;
    public List<CardDef> cards;
}
[Serializable] public class Content { public List<CaseDef> cases; }
[Serializable] public class RoundRec { public string move; public List<string> cards; }
[Serializable] public class VerdictReq { public string caseId, argument; public List<string> cards; public int meter, score; public List<RoundRec> rounds; }
[Serializable] public class Report { public string summary, error; public List<string> mistakes, proofs, precedents, nextTime, sources; }
[Serializable] public class VerdictResp { public int score; public string headline, feedback; public List<string> strengths, missed; }
[Serializable] public class GenReq { public string description; }
[Serializable] public class ErrResp { public string error; }
[Serializable] public class MpReaction { public string title, body; public int delta; }
[Serializable] public class DuelVerdict { public int score; public string headline, defence, prosecution, lesson; }
[Serializable] public class RoomState
{
    public string code, phase, move, error;
    public int round, meter, v, dCount, pCount;
    public bool myReady, oppReady, myClosing, oppClosing, left, away;
    public List<string> dhand, phand, played, record;
    public List<MpReaction> reactions;
    public DuelVerdict verdict;
    public Report report;
}
[Serializable] public class RoomJoin { public string code, role, error; public CaseDef caseDef; }
[Serializable] public class RoomReq { public string code, role, a, caseId, moveId, text; public List<string> cards; }
