"""מודל AI מדומה (Mock) - ללא קריאה לשירות חיצוני.

המודל מקבל את אותו קלט שמודל אמיתי היה מקבל (טקסט חופשי + תיאור עסקי של הקטלוג) ומחזיר הצעת בקרה
באותו מבנה JSON שמוגדר בחוזה (docs/ai-contract.md). המימוש כאן מבוסס כללים פשוטים לעברית, ונועד להדגים
את התשתית בלבד. כדי לעבור למודל אמיתי מחליפים את הקובץ הזה (או את הפונקציות suggest_*) בקריאה ל-LLM,
בלי שינוי בצד ה-.NET.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

HEB = "א-ת"
PREFIX = "והשבלכמ"

COMPARATORS = [
    (r"נמוך או שווה|קטן או שווה|לכל היותר", "lte"),
    (r"גבוה או שווה|גדול או שווה|לפחות", "gte"),
    (r"נמוכ|נמוך|קטנ|קטן|פחות|מתחת|צעיר|מוקדם", "lt"),
    (r"גבוה|גדול|יותר|מעל|מעבר|מאוחר", "gt"),
    (r"שווה|בדיוק", "eq"),
]
NEGATION = re.compile(r"(לא|שאינ\w*|ללא|אינ\w*)\s*$")
MISSING = re.compile(r"חסר|ריק|חסרים|לא מולא|ללא ערך")
NUMBER = re.compile(r"\d+(?:\.\d+)?")
DATE_DMY = re.compile(r"(\d{1,2})[/.](\d{1,2})[/.](\d{4})")
DATE_ISO = re.compile(r"(\d{4})-(\d{2})-(\d{2})")


def normalize(text: str) -> str:
    text = text.replace("־", " ").replace("״", "").replace('"', "").replace("׳", "").replace("'", "")
    text = re.sub(r"(?<=\d),(?=\d{3})", "", text)
    return re.sub(r"\s+", " ", text).strip()


def _terms(*parts) -> list[str]:
    out: list[str] = []
    for part in parts:
        if isinstance(part, str):
            part = [part]
        out.extend(normalize(p) for p in (part or []) if p)
    return sorted(set(out), key=len, reverse=True)


def _find(text: str, terms: list[str]) -> list[tuple[int, int]]:
    hits = []
    for term in terms:
        stem = term[:-1] if term.endswith("ה") and len(term) > 3 else term
        for m in re.finditer(rf"(?<![{HEB}])[{PREFIX}]{{0,2}}({re.escape(stem)}[{HEB}]*)", text):
            hits.append((m.start(1), m.end(1)))
    return hits


@dataclass
class Match:
    start: int
    end: int
    path: list[str]
    field: dict
    consumed: bool = False


@dataclass
class Result:
    items: list = field(default_factory=list)
    warnings: list = field(default_factory=list)


def _pick_entity(text: str, catalog: list[dict]) -> dict:
    def score(e):
        return len(_find(text, _terms(e["displayName"], e.get("pluralName"), e.get("synonyms"))))
    best = max(catalog, key=score)
    return best if score(best) > 0 else catalog[0]


def _collect_matches(text: str, entity: dict, by_key: dict) -> list[Match]:
    rel_hits = {}
    for rel in entity.get("relations", []):
        hits = _find(text, _terms(rel["displayName"], rel.get("synonyms")))
        if hits:
            rel_hits[rel["key"]] = hits

    found: list[Match] = []
    for f in entity["fields"]:
        for s, e in _find(text, _terms(f["displayName"], f.get("synonyms"))):
            found.append(Match(s, e, [], f))

    for rel in entity.get("relations", []):
        if rel["key"] not in rel_hits:
            continue
        target = by_key.get(rel["targetEntity"])
        if not target:
            continue
        for f in target["fields"]:
            for s, e in _find(text, _terms(f["displayName"], f.get("synonyms"))):
                if any(0 <= s - he <= 40 for _, he in rel_hits[rel["key"]]):
                    found.append(Match(s, e, [rel["key"]], f))

    # אם אותו מקום בטקסט תואם גם שדה של קשר וגם שדה של הישות הראשית - מעדיפים את שדה הקשר
    related_spans = [(m.start, m.end) for m in found if m.path]
    found = [m for m in found if m.path or (m.start, m.end) not in related_spans]
    # מעדיפים התאמה ארוכה כשיש חפיפה
    found.sort(key=lambda m: (m.start, -(m.end - m.start)))
    result: list[Match] = []
    for m in found:
        if result and m.start < result[-1].end:
            continue
        result.append(m)
    return result


def _literal(segment: str, ftype: str):
    if ftype == "date":
        m = DATE_ISO.search(segment)
        if m:
            return m.group(0)
        m = DATE_DMY.search(segment)
        if m:
            d, mo, y = m.groups()
            return f"{y}-{int(mo):02d}-{int(d):02d}"
        return None
    m = NUMBER.search(segment)
    if m:
        return float(m.group(0)) if "." in m.group(0) else int(m.group(0))
    return None


def _cond(path: list[str], field_key: str, op: str, value=None, compare: tuple[list[str], str] | None = None) -> dict:
    cond = {"type": "condition", "field": {"path": path, "field": field_key}, "operator": op}
    if compare is not None:
        cond["compareField"] = {"path": compare[0], "field": compare[1]}
    elif value is not None:
        cond["value"] = value
    return cond


def _condition(m: Match, op: str, value=None, compare: Match | None = None) -> dict:
    return _cond(m.path, m.field["key"], op, value, (compare.path, compare.field["key"]) if compare else None)


def suggest_control(request: dict) -> dict:
    prompt = request.get("prompt", "")
    catalog = request["catalog"]
    by_key = {e["key"]: e for e in catalog}
    text = normalize(prompt)
    entity = _pick_entity(text, catalog)
    matches = _collect_matches(text, entity, by_key)
    result = Result()
    null_items: list[dict] = []

    for i, m in enumerate(matches):
        if m.consumed:
            continue
        ftype = m.field["type"]
        seg_end = matches[i + 1].start if i + 1 < len(matches) else len(text)
        segment = text[m.end:seg_end]
        before = text[max(0, m.start - 14):m.start]

        if MISSING.search(segment[:20]) or MISSING.search(before):
            null_items.append(_condition(m, "isNull"))
        elif ftype == "boolean":
            negated = bool(NEGATION.search(before.strip()))
            result.items.append(_condition(m, "eq", not negated))
        else:
            op = next((o for pat, o in COMPARATORS if re.search(pat, segment)), None)
            value = _literal(segment, ftype)
            nxt = matches[i + 1] if i + 1 < len(matches) else None
            if op and value is None and nxt is not None and nxt.field["type"] == ftype:
                nxt.consumed = True
                result.items.append(_condition(m, op, compare=nxt))
            elif op and value is not None:
                result.items.append(_condition(m, op, value))
            elif value is not None:
                result.items.append(_condition(m, "eq", value))
            else:
                result.warnings.append(f"לא הצלחתי להבין את התנאי על '{m.field['displayName']}'")

    if re.search(r"חסר|ריק", text) and not null_items:
        required = [f for f in entity["fields"] if f.get("isRequired")]
        null_items = [_condition(Match(0, 0, [], f), "isNull") for f in required]
        if required:
            result.warnings.append("לא צוינו שדות ספציפיים - נבחרו כל שדות החובה")

    items = list(result.items)
    if len(null_items) == 1:
        items.append(null_items[0])
    elif null_items:
        items.append({"type": "group", "operator": "or", "items": null_items})

    if not items:
        result.warnings.append("לא זוהו תנאים בטקסט. נסו לנסח מחדש, למשל: 'עובדים פעילים שגילם נמוך מ-18'")

    explanation = (
        f"זיהיתי את הנתונים '{entity['pluralName']}' ו-{len(items)} תנאים, "
        "וחיברתי ביניהם בהיגיון 'וגם'. הרשומות שיעמדו בתנאים יוצגו כחריגות."
    )
    confidence = round(min(0.9, 0.4 + 0.15 * len(items)), 2) if items else 0.1
    return {
        "name": _title(prompt),
        "description": prompt.strip(),
        "definition": {"rootEntity": entity["key"], "root": {"type": "group", "operator": "and", "items": items}},
        "explanation": explanation,
        "confidence": confidence,
        "warnings": result.warnings,
    }


def _title(prompt: str) -> str:
    t = re.sub(r"^(מצא|איתור|אתר|הצג)\s+", "", prompt.strip())
    t = t.rstrip(".")
    return t if len(t) <= 60 else t[:57] + "..."


def _find_field(fields: list[dict], ftype: str, pattern: str) -> dict | None:
    return next((f for f in fields if f["type"] == ftype and re.search(pattern, f["displayName"])), None)


def suggest_proactive(request: dict) -> dict:
    """מנתח את מבנה הקטלוג ומציע בקרות איכות נתונים כלליות.

    הזיהוי מבוסס על סוגי שדות וקשרים, ועל היוריסטיקות שמות פשוטות (תחילה/סיום, פעיל, Max/Min).
    מודל אמיתי יחליף את ההיוריסטיקות בהבנה סמנטית של הקטלוג."""
    catalog = request["catalog"]
    by_key = {e["key"]: e for e in catalog}
    out = []

    for e in catalog:
        fields = e["fields"]

        required = [f for f in fields if f.get("isRequired")]
        if required:
            out.append(_proposal(
                e, f"חסרים נתוני חובה ב{e['pluralName']}",
                {"type": "group", "operator": "or", "items": [_cond([], f["key"], "isNull") for f in required]},
                f"שדות החובה: {', '.join(f['displayName'] for f in required)}", 0.8))

        starts = [f for f in fields if f["type"] == "date" and re.search("תחיל|התחל", f["displayName"])]
        ends = [f for f in fields if f["type"] == "date" and re.search("סיום|סוף", f["displayName"])]
        for s in starts:
            for en in ends:
                out.append(_proposal(
                    e, f"{en['displayName']} מוקדם מ{s['displayName']}",
                    _all([_cond([], en["key"], "lt", compare=([], s["key"]))]),
                    "זוג תאריכים התחלה/סיום - הסיום לא אמור להקדים את ההתחלה", 0.85))

        active = _find_field(fields, "boolean", "פעיל")
        for rel in e.get("relations", []):
            target = by_key.get(rel["targetEntity"])
            if not target:
                continue

            t_active = _find_field(target["fields"], "boolean", "פעיל")
            if active and t_active and target["key"] != e["key"]:
                out.append(_proposal(
                    e, f"{e['pluralName']} פעילים המשויכים ל{rel['displayName']} שאינו פעיל",
                    _all([_cond([], active["key"], "eq", True), _cond([rel["key"]], t_active["key"], "eq", False)]),
                    "ישות פעילה שמקושרת לישות לא פעילה", 0.8))

            for f in target["fields"]:
                for prefix, op, word in (("Max", "gt", "תקרה"), ("Min", "lt", "רצפה")):
                    src = next((x for x in fields if x["type"] == "number" and f["key"] == prefix + x["key"]), None)
                    if src:
                        out.append(_proposal(
                            e, f"{src['displayName']} חורג מה{word} של {rel['displayName']}",
                            _all([_cond([], src["key"], op, compare=([rel["key"]], f["key"]))]),
                            f"השוואת {src['displayName']} ל'{f['displayName']}'", 0.7))
    return {"suggestions": out}


def _all(items: list) -> dict:
    return {"type": "group", "operator": "and", "items": items}


def _proposal(entity: dict, name: str, root: dict, why: str, confidence: float) -> dict:
    if root["type"] == "condition":
        root = _all([root])
    return {
        "name": name,
        "description": f"בקרה מוצעת על {entity['pluralName']}: {name}",
        "definition": {"rootEntity": entity["key"], "root": root},
        "explanation": why,
        "confidence": confidence,
        "warnings": [],
    }
