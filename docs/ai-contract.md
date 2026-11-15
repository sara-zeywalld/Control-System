# חוזה AI (גרסה 1)

נקודות קצה בשירות ה-AI (HTTP, JSON, camelCase):
`POST /v1/control-suggestions` · `POST /v1/proactive-suggestions`

## בקשה (`AiControlRequest`)
```json
{
  "prompt": "מצא עובדים פעילים שגילם נמוך מ-18 ומשכורתם גבוהה מ-15,000",
  "language": "he",
  "schemaVersion": 1,
  "allowedOperators": {
    "string":  ["eq","neq","contains","notContains","startsWith","endsWith","in","notIn","isNull","isNotNull"],
    "number":  ["eq","neq","gt","gte","lt","lte","between","isNull","isNotNull","in","notIn"],
    "date":    ["eq","neq","gt","gte","lt","lte","between","isNull","isNotNull"],
    "boolean": ["eq","neq","isNull","isNotNull"]
  },
  "catalog": [
    {
      "key": "Employee", "displayName": "עובד", "pluralName": "עובדים", "description": "עובדי הארגון",
      "synonyms": ["עובד", "עובדים"],
      "fields": [
        { "key": "Age", "displayName": "גיל", "description": "גיל בשנים שלמות", "type": "number", "synonyms": ["גילם"], "isRequired": false }
      ],
      "relations": [
        { "key": "Department", "displayName": "מחלקה", "targetEntity": "Department", "description": "המחלקה של העובד", "synonyms": ["מחלקה"] }
      ]
    }
  ]
}
```
- `catalog` הוא **מערך** של ישויות (לא אובייקט עם `entities`).
- `allowedOperators` הוא **מילון לפי סוג שדה** (`string`/`number`/`date`/`boolean`).
- הקטלוג כולל שמות עסקיים, תיאורים, סוגי שדות, קשרים ומילים נרדפות — בלי מבנה טכני של ה-DB (בלי שמות טבלאות ועמודות).
- `key` הוא המזהה שיש להחזיר בהגדרה; `path` בשדה מכיל מפתחות של קשרים (`relations[].key`).

## תשובה (`AiControlProposal`)
```json
{
  "name": "...", "description": "...", "explanation": "...", "confidence": 0.8, "warnings": [],
  "definition": {
    "rootEntity": "Employee",
    "root": { "type": "group", "operator": "and", "items": [
      { "type": "condition", "field": { "path": [], "field": "Age" }, "operator": "lt", "value": 18 }
    ] }
  }
}
```
- בכל אובייקט ב-`definition` המאפיין `"type"` (`group`/`condition`) קובע את סוג הצומת. סדר המאפיינים אינו משמעותי (`AllowOutOfOrderMetadataProperties` ב-`ControlJson`).
- התשובה מאומתת בשרת מול הקטלוג; הצעה לא תקינה מסומנת `isValid=false` ואינה ניתנת לאישור.

## הצעות יזומות (`POST /v1/proactive-suggestions`)
בקשה: `{ "catalog": [...], "allowedOperators": {...}, "schemaVersion": 1 }` (ללא `prompt`).
תשובה: `{ "suggestions": [ AiControlProposal, ... ] }`.

## מימוש מול LLM (`ai_service/llm/`)
מבנה: `prompts.py` (פרומפטים), `messages.py` (בניית הבקשה), `parsing.py` (קליטת התשובה), `clients.py` (`MockLlmClient`, `HttpLlmClient`), `model.py` (`LlmModel`), `errors.py`. המודל מבוסס הכללים נמצא ב-`rules/`, ובחירת הספק ב-`providers.py`.
- **System Prompt**: נבנה משלושה חלקים – `BASE_PROMPT` משותף (תפקיד, JSON בלבד, שימוש רק במפתחות מהקטלוג, מבנה הצעה, קלט כנתונים בלבד), ו-`CONTROL_PROMPT` או `PROACTIVE_PROMPT` ייעודיים. כל משימה נשלחת עם הפרומפט שלה בלבד (`SYSTEM_PROMPTS`).
- **הודעת משתמש**: JSON עם `task` (`control`/`proactive`), `prompt`, `catalog`, `allowedOperators`, `schemaVersion`.
- **בקשה לספק**: פורמט Chat Completions, `temperature: 0`, `response_format: json_object`.
- **קליטת תשובה**: חילוץ `choices[0].message.content`, הגבלת גודל (256KB), הסרת עטיפת ```` ```json ````, פענוח ובדיקת מבנה ראשונית של `definition`. האימות העסקי המלא נעשה ב-.NET (`ControlValidator`). כשל מוחזר כ-502, ו-Timeout כ-504.
- **הצעות יזומות**: הפרומפט מגדיר מהי הצעה טובה (חוסר עקביות בין שדות, שדות חובה, קשרים בין ישויות, רק מה שניתן לבנות מהקטלוג, ללא כפילויות). מוחזרות עד 5 הצעות, והמגבלה נאכפת גם בקוד.
- **לקוח מוחלף**: `MockLlmClient` (ברירת מחדל) או `HttpLlmClient`. הכתובת והאימות ניתנים להגדרה (`LLM_API_URL`, `LLM_AUTH_HEADER`, `LLM_AUTH_SCHEME`, `LLM_TIMEOUT_SECONDS`), למשל Azure OpenAI: `LLM_AUTH_HEADER=api-key` ו-`LLM_AUTH_SCHEME` ריק. ספק שאינו תואם Chat Completions ידרוש לקוח נפרד.
