"""Retrieval over the French criminal-law reference in server/knowledge/*.md (RAG for the end-of-game analysis).

Sections (### / ##) become chunks, embedded once with mistral-embed and cached in server/rag_cache.json.
If embeddings are unavailable, retrieval falls back to a lexical (idf-weighted overlap) score."""
import hashlib
import json
import math
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).parent
KNOW = ROOT / "knowledge"
CACHE = ROOT / "rag_cache.json"
SKIP = ("0.", "8.")  # "how to use this file" and "known gaps" are about the document, not the law
CHUNKS, VECS = [], []


def _clean(t):
    return re.sub(r"\n{3,}", "\n\n", re.sub(r"</?claim[^>]*>", "", t)).strip()


def _split(md):
    out, head, buf = [], None, []

    def flush():
        text = _clean("\n".join(buf))
        if head and len(text) > 120:
            m = re.match(r"(\d+(?:\.\d+)?)\.?\s+(.*)", head)
            ref, title = (m.group(1), m.group(2)) if m else ("", head)
            if not ref.startswith(SKIP) and ref + "." not in SKIP:
                out.append({"ref": ref, "title": title.strip(), "text": text})

    for line in md.splitlines():
        h = re.match(r"^#{2,3}\s+(.*)", line)
        if h:
            flush()
            head, buf = h.group(1).strip(), []
        else:
            buf.append(line)
    flush()
    return out


def _tokens(t):
    return re.findall(r"[a-zà-ÿ0-9-]{3,}", t.lower())


def _embed(texts, key):
    vecs = []
    for i in range(0, len(texts), 8):
        req = urllib.request.Request("https://api.mistral.ai/v1/embeddings",
                                     data=json.dumps({"model": "mistral-embed", "input": texts[i:i + 8]}).encode(),
                                     headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            vecs += [d["embedding"] for d in json.load(r)["data"]]
    return vecs


def load(key):
    """Chunk the knowledge files and embed them (cached by content hash)."""
    global CHUNKS, VECS
    CHUNKS = [c for f in sorted(KNOW.glob("*.md")) for c in _split(f.read_text(encoding="utf-8"))]
    texts = [f'{c["title"]}\n{c["text"]}'[:6000] for c in CHUNKS]
    sig = hashlib.sha1("\n\0".join(texts).encode()).hexdigest()
    try:
        cached = json.loads(CACHE.read_text())
        if cached.get("sig") == sig:
            VECS = cached["vecs"]
            return
    except Exception:
        pass
    if not key:
        return
    try:
        VECS = _embed(texts, key)
        CACHE.write_text(json.dumps({"sig": sig, "vecs": VECS}))
        print(f"rag: embedded {len(CHUNKS)} chunks")
    except Exception as e:
        VECS = []
        print("rag: embeddings unavailable, lexical fallback:", e)


def retrieve(query, key, k=5):
    if not CHUNKS:
        return []
    scores = None
    if VECS and len(VECS) == len(CHUNKS) and key:
        try:
            q = _embed([query[:6000]], key)[0]
            qn = math.sqrt(sum(x * x for x in q))
            scores = [sum(a * b for a, b in zip(q, v)) / (qn * math.sqrt(sum(b * b for b in v))) for v in VECS]
        except Exception as e:
            print("rag: query embedding failed, lexical fallback:", e)
    if scores is None:
        docs = [set(_tokens(c["title"] + " " + c["text"])) for c in CHUNKS]
        n = len(docs)
        qt = set(_tokens(query))
        scores = [sum(math.log(n / (1 + sum(t in d for d in docs))) for t in qt if t in doc) for doc in docs]
    best = sorted(range(len(CHUNKS)), key=lambda i: -scores[i])[:k]
    return [CHUNKS[i] for i in best]
