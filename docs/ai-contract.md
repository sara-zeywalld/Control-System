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
- בכל אובייקט ב-`definition` המאפיין `"type"` חייב להופיע ראשון.
- התשובה מאומתת בשרת מול הקטלוג; הצעה לא תקינה מסומנת `isValid=false` ואינה ניתנת לאישור.

## הצעות יזומות (`POST /v1/proactive-suggestions`)
בקשה: `{ "catalog": [...], "allowedOperators": {...}, "schemaVersion": 1 }` (ללא `prompt`).
תשובה: `{ "suggestions": [ AiControlProposal, ... ] }`.
