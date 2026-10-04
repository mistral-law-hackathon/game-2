using System;
using System.Collections.Generic;

[Serializable] public class CardDef { public string id, name, kind, law, plain, counters, lesson; public int power; }
[Serializable] public class MoveDef { public string id, title, text, law; public int power; }
[Serializable] public class CaseDef
{
    public string id, title, client, charge, law, definition, takeaway;
    public int start;
    public List<string> scenes;
    public List<string> facts;
    public List<MoveDef> moves;
    public List<CardDef> cards;
}
[Serializable] public class Content { public List<CaseDef> cases; }
[Serializable] public class VerdictReq { public string caseId, argument; public List<string> cards; public int meter; }
[Serializable] public class VerdictResp { public int score; public string headline, feedback; public List<string> strengths, missed; }
