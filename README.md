# מערכת בקרות נתונים גנרית

מערכת Web להגדרה, הרצה וניתוח של בקרות איכות נתונים — ללא SQL וללא קוד חדש לכל בקרה.
Stack: SQL Server · .NET 10 Web API · Python (Mock AI) · Angular 22 (עברית, RTL).

## מבנה

| תיקייה | תוכן |
|---|---|
| `db/` | סקריפטים: סכמה, נתוני דוגמה, קטלוג עסקי ובקרות לדוגמה |
| `backend/` | פתרון .NET (`Core`, `Infrastructure`, `Api`, `Tests`) |
| `ai_service/` | שירות Python (מודל AI מדומה מבוסס כללים) |
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
3. **שירות ה-AI (Mock)**:
   ```powershell
   cd ai_service
   python app.py     # http://localhost:8000
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

## הערה חשובה
סביבת הפיתוח לא כללה SQL Server, ולכן סקריפטי ה-SQL ושכבת ה-Repository/Executor לא הורצו מול שרת אמיתי.
יצירת ה-SQL ואימותו נבדקים ב-unit tests; מומלץ להריץ את הסקריפטים ולבצע smoke test בהקמה ראשונה.

המסמך המלא: [docs/architecture.md](docs/architecture.md) · חוזה AI: [docs/ai-contract.md](docs/ai-contract.md)
