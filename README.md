# מערכת בקרות נתונים גנרית

מערכת Web להגדרה, הרצה וניתוח של בקרות איכות נתונים — ללא SQL וללא קוד חדש לכל בקרה.
Stack: SQL Server · .NET 10 Web API · Python (Mock AI) · Angular 22 (עברית, RTL).

## מבנה

| תיקייה | תוכן |
|---|---|
| `db/` | סקריפטים: סכמה, נתוני דוגמה, קטלוג עסקי ובקרות לדוגמה |
| `backend/` | פתרון .NET (`BL` – לוגיקה עסקית, `DAL` – גישה לנתונים, `Api`, `Tests`) |
| `ai_service/` | שירות Python (FastAPI) עם מודל AI מדומה מבוסס כללים |
| `frontend/` | אפליקציית Angular |
| `docs/` | ארכיטקטורה וחוזה ה-AI |

## דרישות
SQL Server (כולל LocalDB/Express), .NET SDK 10, Python 3.10+, Node 20+.

## התקנה והרצה

1. **בסיס נתונים** – להריץ לפי הסדר (קידוד UTF-8):
   ```powershell
   sqlcmd -S . -E -f 65001 -i db\01_schema.sql
   sqlcmd -S . -E -f 65001 -i db\02_seed_data.sql
   sqlcmd -S . -E -f 65001 -i db\03_catalog_and_controls.sql
   ```
   מומלץ ליצור משתמש קריאה בלבד (`db_datareader`) ולהגדיר אותו ב-`ConnectionStrings:DataReadOnly`.
2. **Backend** – עדכנו connection strings ב-`backend/src/ControlSystem.Api/appsettings.json`, ואז:
   ```powershell
   cd backend\src\ControlSystem.Api
   dotnet run        # http://localhost:5230  (Swagger ב-/swagger)
   ```
3. **שירות ה-AI (Mock, FastAPI)**:
   ```powershell
   cd ai_service
   pip install -r requirements.txt
   python app.py     # http://localhost:8000  (תיעוד החוזה ב-/docs)
   ```
4. **Frontend**:
   ```powershell
   cd frontend
   npm install
   npm start         # http://localhost:4200
   ```

### בדיקות
```powershell
cd backend; dotnet test
cd ai_service; python -m unittest discover -s tests
cd frontend; npx ng test --watch=false
```

## בקרות לדוגמה (נטענות ב-`03_catalog_and_controls.sql`)
1. עובדים קטינים עם שכר גבוה (גיל < 18 וגם שכר > 15,000)
2. תאריך סיום לפני תאריך תחילה
3. עובד פעיל במחלקה לא פעילה
4. חוסר בנתוני חובה
5. שכר מעל תקרת תפקיד
6. שכר גבוה משכר המנהל

בנתוני הדוגמה יש רשומות תקינות ושגויות (עובדים 101–112 שגויים בכוונה).

## הדגמת שלב ב' (Mock)
בלשונית "יצירה בשפה חופשית" הזינו, למשל: *מצא עובדים פעילים שגילם נמוך מ-18 ומשכורתם גבוהה מ-15,000 ש"ח*.
המערכת שולחת ל-Python את תיאור הקטלוג, מקבלת הצעת בקרה (JSON), מאמתת אותה, ומציגה לאישור.
רק אחרי אישור היא נשמרת ומורצת. כפתור "הצעות יזומות" מדגים את המשימה המתקדמת.

## בדיקה מול SQL Server
הסקריפטים (`db/*.sql`) והמערכת כולה הורצו ונבדקו מול SQL Server (LocalDB 2025): הרצת שלושת הסקריפטים, טעינת הקטלוג והבקרות, הרצת כל 8 הבקרות לדוגמה דרך ה-API וקבלת רשומות חריגות עם סיבת כשל, בקשת שלב ב' מול שירות ה-Python (כולל הצעות יזומות), תצוגה מקדימה לנתיב של שני קשרים, וידוא שקלט זדוני נדחה (400). בבדיקה זו נמצא ותוקן באג בסקריפט הסכמה (אילוץ `CK_ctl_Field_Source`).
לא נבדקו: ה-UI בדפדפן, ומשתמש קריאה בלבד נפרד (הבדיקה בוצעה עם אותו חיבור לשני השימושים).
אם משתמשים ב-sqlcmd החדש (go-sqlcmd) יש להשמיט את `-f 65001`, והוא אינו נתמך שם.

המסמך המלא: [docs/architecture.md](docs/architecture.md) · חוזה AI: [docs/ai-contract.md](docs/ai-contract.md)
