# חוזה AI (גרסה 1)

נקודות קצה בשירות ה-AI (HTTP, JSON, camelCase):
`POST /v1/control-suggestions` · `POST /v1/proactive-suggestions`

## בקשה (`AiControlRequest`)
```json
{
  "prompt": "מצא עובדים פעילים שגילם נמוך מ-18 ומשכורתם גבוהה מ-15,000",
  "language": "he",
  "schemaVersion": 1,
  "allowedOperators": ["eq","neq","gt","gte","lt","lte","between","in","notIn","contains","notContains","startsWith","endsWith","isNull","isNotNull"],
  "catalog": { "entities": [ { "key": "Employee", "displayName": "עובד", "synonyms": ["עובדים"], "fields": [], "relations": [] } ] }
}
```
הקטלוג כולל שמות עסקיים, תיאורים, סוגי שדות, קשרים ומילים נרדפות — בלי מבנה טכני של ה-DB.

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
