# ארכיטקטורה ומנגנון הבקרות

## שכבות
```
Angular ──► .NET API ──► ControlService ──► SqlServerQueryBuilder ──► SqlQueryExecutor ──► SQL Server
                 │                 ▲
                 └─► AiControlService ──► IAiControlProvider ──HTTP──► Python (Mock AI)
```
- **Core** – ללא תלות בתשתית: הגדרות, קטלוג, ולידציה, בניית שאילתה, הרצה, חוזי AI.
- **Infrastructure** – Dapper/SqlClient, קטלוג וקריאת בקרות מה-DB, לקוח HTTP ל-AI.
- **Api** – Controllers דקים, Middleware אחיד לשגיאות (`{status,title,errors[]}`), CORS, Swagger.
- **Frontend** – רכיבים: `condition-group` (רקורסיבי), `condition-row`, `results-table`, `alert`; מצב ב-Signals; CSS בקבצים נפרדים; RTL.

## הפרדה: הגדרה ← שאילתה ← הרצה
1. **הגדרת בקרה** – JSON עסקי (`ControlDefinition`) בטבלה `ctl.Control`: ישות שורש + עץ AND/OR של תנאים. הרשומות שעומדות בעץ הן החריגות. אין בו שמות טבלאות.
2. **תרגום** – `SqlServerQueryBuilder` הופך הגדרה ל-SQL לפי **קטלוג** (`ctl.Entity/Field/Relation`): שדה הוא עמודה או ביטוי (`{t}` = alias), יחסים הם many-to-one (כולל self-join) ונבנים כ-LEFT JOIN לכל נתיב.
3. **הרצה** – אימות → בנייה → `SqlSafetyGuard` → ביצוע → `ResultMapper` מפיק סיבת כשל לכל רשומה מהדגלים לכל תנאי.

## גנריות
הוספת ישות/שדה/קשר = הוספת שורות בטבלאות הקטלוג בלבד. בקרה חדשה = שורה ב-`ctl.Control` (דרך ה-UI). אין קוד ייעודי לאף בקרה.

## אבטחה
1. מזהים (טבלאות/עמודות) רק מהקטלוג, עם בדיקת regex וציטוט; כל ערך הוא פרמטר מוקלד.
2. `ControlValidator` – מגבלות עומק (5), מספר תנאים (50), ערכי רשימה (100).
3. `SqlSafetyGuard` – SELECT בלבד; חוסם `;`, הערות, DML/DDL/EXEC/INTO.
4. חיבור נפרד לקריאה בלבד; timeout של 30 שניות; `TOP(1000)`.
5. LIKE עם escape.

## שלב ב' – AI
- חוזה: `AiControlRequest` (טקסט חופשי, תיאור קטלוג עם מילים נרדפות, אופרטורים מותרים, גרסה) ← `AiControlProposal` (שם, הגדרה, הסבר, ביטחון, אזהרות). ראו [ai-contract.md](ai-contract.md).
- `IAiControlProvider` היא נקודת ההחלפה; היום `HttpAiControlProvider` מול Python Mock. החלפה למודל אמיתי = מימוש חדש של הממשק/שירות, ללא שינוי בלוגיקת הבקרות.
- פלט ה-AI אינו מהימן: `AiControlService` מאמת אותו מול הקטלוג ומחזיר `isValid`. דבר לא נשמר או מורץ ללא אישור המשתמש.
- משימה מתקדמת: `POST /api/ai/suggest-proactive` – הצעות כלליות מתוך מבנה הקטלוג (שדות חובה חסרים, סיום<התחלה, ישות פעילה מול קשורה לא פעילה, ערך מעל Max של ישות קשורה).

## הנחות, מגבלות והרחבות
- הנחות: קשרים many-to-one; "חריג" = מקיים את התנאים.
- מגבלות: ה-UI תומך בשרשרת של עד שני קשרים (למשל עובד › מחלקה › אתר), וה-Backend עד שלושה; תקרת 1000 שורות; Mock AI מבוסס כללים בעברית; אין אימות משתמשים; SQL לא הורץ מול שרת בסביבת הפיתוח.
- הרחבות: אימות והרשאות, תזמון הרצות והתראות, ייצוא ל-Excel, יחסי many-to-many, מודל AI אמיתי (LLM עם structured output), היסטוריית הרצות ב-UI.

## כלי AI בשימוש
| כלי | מטרת השימוש |
|---|---|
| GitHub Copilot (AI Assistant ב-VS Code) | תכנון הארכיטקטורה והפרדת השכבות; כתיבת Backend (.NET), Frontend (Angular), שירות ה-Python ה-Mock וסקריפטי SQL |
| GitHub Copilot | כתיבת בדיקות יחידה (xUnit, unittest, Vitest) והרצתן; סקירת קוד ותיקון ליקויי אבטחה (תלות עם פגיעות ידועה) |
| GitHub Copilot | כתיבת מסמכי התיעוד (README, ארכיטקטורה, חוזה AI) |

כל קוד שנוצר נסקר ונבדק ידנית: build, בדיקות יחידה בשלוש השכבות, והרצה מקומית.
> אם נעשה שימוש בכלי AI נוסף (למשל לצורך ייעוץ או יצירת נתוני דוגמה) יש להוסיף אותו לטבלה.

