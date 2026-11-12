using ControlSystem.Core.Catalog;
using ControlSystem.Core.Controls;
using ControlSystem.Core.Definitions;

namespace ControlSystem.Infrastructure.Demo;

public sealed class DemoCatalogProvider : ICatalogProvider
{
    public Task<DataCatalog> GetCatalogAsync(CancellationToken ct) => Task.FromResult(DemoData.Catalog);
}

/// <summary>נתוני דוגמה בזיכרון, המשקפים את סקריפטי ה-SQL (db/02 ו-db/03). משמש למצב Demo ללא SQL Server.</summary>
public static class DemoData
{
    private static string[] Syn(string? s) => string.IsNullOrEmpty(s) ? [] : s.Split(',');

    private static FieldMeta F(string entity, string key, string name, string? desc, string? syn, string? col, string? expr,
        FieldType type, bool required, bool show, int sort) =>
        new(entity, key, name, desc, Syn(syn), col, expr, type, required, show, sort);

    private const string AgeExpr =
        "DATEDIFF(YEAR, {t}.BirthDate, GETDATE()) - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, {t}.BirthDate, GETDATE()), {t}.BirthDate) > CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END";

    public static readonly DataCatalog Catalog = BuildCatalog();

    private static DataCatalog BuildCatalog()
    {
        var employee = new EntityMeta("Employee", "עובד", "עובדים", "עובדי הארגון", Syn("עובד,עובדים,עובדת,עובדות,משרת,אנשי צוות"),
            "dbo", "Employees", "EmployeeId", true,
            [
                F("Employee", "EmployeeId", "מספר עובד", null, "מזהה,מס עובד", "EmployeeId", null, FieldType.Number, false, true, 10),
                F("Employee", "FullName", "שם מלא", "שם פרטי ושם משפחה", "שם", null, "{t}.FirstName + N' ' + {t}.LastName", FieldType.String, false, true, 20),
                F("Employee", "FirstName", "שם פרטי", null, null, "FirstName", null, FieldType.String, false, false, 30),
                F("Employee", "LastName", "שם משפחה", null, null, "LastName", null, FieldType.String, false, false, 40),
                F("Employee", "Email", "דוא\"ל", "כתובת דואר אלקטרוני", "מייל,אימייל,דואל", "Email", null, FieldType.String, true, true, 50),
                F("Employee", "BirthDate", "תאריך לידה", null, "לידה", "BirthDate", null, FieldType.Date, false, false, 60),
                F("Employee", "Age", "גיל", "גיל בשנים שלמות (מחושב מתאריך הלידה)", "גילם,גילו,גילה", null, AgeExpr, FieldType.Number, false, true, 70),
                F("Employee", "HireDate", "תאריך תחילת העסקה", "תאריך תחילת עבודה", "תחילת העסקה,תחילת עבודה,תחילה,התחלה", "HireDate", null, FieldType.Date, false, true, 80),
                F("Employee", "EndDate", "תאריך סיום העסקה", "תאריך סיום עבודה", "סיום העסקה,סיום עבודה,סיום,עזיבה", "EndDate", null, FieldType.Date, false, true, 90),
                F("Employee", "Salary", "שכר", "שכר חודשי ברוטו בש\"ח", "משכורת,משכורתם,שכרם,שכרו,שכרה", "Salary", null, FieldType.Number, true, true, 100),
                F("Employee", "IsActive", "פעיל", "האם העובד פעיל כיום", "פעילים,פעילה,פעילות", "IsActive", null, FieldType.Boolean, false, true, 110),
                F("Employee", "DepartmentName", "שם מחלקה", "שם המחלקה אליה משויך העובד", null, null,
                    "(SELECT d.Name FROM dbo.Departments AS d WHERE d.DepartmentId = {t}.DepartmentId)", FieldType.String, false, true, 120),
                F("Employee", "DepartmentId", "מחלקה משויכת", "שיוך העובד למחלקה", null, "DepartmentId", null, FieldType.Number, true, false, 130),
            ],
            [
                new("Employee", "Department", "Department", "DepartmentId", "DepartmentId", "מחלקה", "המחלקה אליה משויך העובד", Syn("מחלקה,משויך למחלקה,שייך למחלקה")),
                new("Employee", "JobTitle", "JobTitle", "JobTitleId", "JobTitleId", "תפקיד", "תפקיד העובד", Syn("תפקיד,משרה")),
                new("Employee", "Manager", "Employee", "ManagerId", "EmployeeId", "מנהל ישיר", "המנהל הישיר של העובד", Syn("מנהל,מנהלו,מנהלם")),
            ]);

        var department = new EntityMeta("Department", "מחלקה", "מחלקות", "יחידות ארגוניות", Syn("מחלקה,מחלקות,יחידה"),
            "dbo", "Departments", "DepartmentId", true,
            [
                F("Department", "DepartmentId", "מספר מחלקה", null, null, "DepartmentId", null, FieldType.Number, false, true, 10),
                F("Department", "Name", "שם מחלקה", null, null, "Name", null, FieldType.String, false, true, 20),
                F("Department", "CostCenter", "מרכז עלות", null, "מרכז רווח", "CostCenter", null, FieldType.String, true, true, 30),
                F("Department", "IsActive", "פעילה", "האם המחלקה פעילה", "פעיל,פעילה,פעילים", "IsActive", null, FieldType.Boolean, false, true, 40),
            ], []);

        var job = new EntityMeta("JobTitle", "תפקיד", "תפקידים", "תפקידים וטווחי שכר מותרים", Syn("תפקיד,תפקידים,משרה"),
            "dbo", "JobTitles", "JobTitleId", true,
            [
                F("JobTitle", "JobTitleId", "מספר תפקיד", null, null, "JobTitleId", null, FieldType.Number, false, true, 10),
                F("JobTitle", "Title", "שם תפקיד", null, null, "Title", null, FieldType.String, false, true, 20),
                F("JobTitle", "MinSalary", "שכר מינימום לתפקיד", null, "מינימום", "MinSalary", null, FieldType.Number, false, true, 30),
                F("JobTitle", "MaxSalary", "שכר מקסימום לתפקיד", null, "תקרת שכר,מקסימום", "MaxSalary", null, FieldType.Number, false, true, 40),
            ], []);

        return new DataCatalog([employee, department, job]);
    }

    public static Dictionary<string, List<Dictionary<string, object?>>> CreateTables()
    {
        var today = DateTime.Today;
        DateTime Y(int years) => today.AddYears(-years);
        DateTime M(int months) => today.AddMonths(-months);

        var departments = new List<Dictionary<string, object?>>();
        void Dept(int id, string name, string? cc, bool active) => departments.Add(new()
        { ["DepartmentId"] = (decimal)id, ["Name"] = name, ["CostCenter"] = cc, ["IsActive"] = active });
        Dept(1, "פיתוח", "CC-100", true); Dept(2, "כספים", "CC-200", true); Dept(3, "שיווק", "CC-300", true);
        Dept(4, "משאבי אנוש", "CC-400", true); Dept(5, "מחלקת מו\"פ ישנה", "CC-900", false); Dept(6, "לוגיסטיקה (סגורה)", null, false);

        var jobs = new List<Dictionary<string, object?>>();
        void Job(int id, string title, int min, int max) => jobs.Add(new()
        { ["JobTitleId"] = (decimal)id, ["Title"] = title, ["MinSalary"] = (decimal)min, ["MaxSalary"] = (decimal)max });
        Job(1, "מנהל/ת", 25000, 45000); Job(2, "מפתח/ת", 14000, 30000); Job(3, "רואה חשבון", 12000, 22000);
        Job(4, "מנהל/ת שיווק", 13000, 25000); Job(5, "סטודנט/ית", 5000, 10000);

        var deptNames = departments.ToDictionary(d => (decimal)d["DepartmentId"]!, d => (string)d["Name"]!);
        var employees = new List<Dictionary<string, object?>>();
        void Emp(int id, string first, string last, string? email, DateTime birth, DateTime hire, DateTime? end,
            int? salary, bool active, int? dept, int job, int? manager)
        {
            var age = today.Year - birth.Year - (birth.AddYears(today.Year - birth.Year) > today ? 1 : 0);
            decimal? deptId = dept;
            employees.Add(new()
            {
                ["EmployeeId"] = (decimal)id, ["FirstName"] = first, ["LastName"] = last, ["FullName"] = first + " " + last,
                ["Email"] = email, ["BirthDate"] = birth, ["Age"] = (decimal)age, ["HireDate"] = hire, ["EndDate"] = end,
                ["Salary"] = salary is null ? null : (decimal)salary, ["IsActive"] = active, ["DepartmentId"] = deptId,
                ["DepartmentName"] = deptId is null ? null : deptNames.GetValueOrDefault(deptId.Value),
                ["JobTitleId"] = (decimal)job, ["ManagerId"] = manager is null ? null : (decimal)manager,
            });
        }

        Emp(1, "דנה", "כהן", "dana.cohen@example.com", Y(45), Y(12), null, 38000, true, 1, 1, null);
        Emp(2, "יוסי", "לוי", "yossi.levi@example.com", Y(38), Y(9), null, 28000, true, 1, 2, 1);
        Emp(3, "מיכל", "אברהם", "michal.a@example.com", Y(31), Y(5), null, 21000, true, 1, 2, 1);
        Emp(4, "אורי", "שמש", "uri.shemesh@example.com", Y(29), Y(3), null, 17000, true, 1, 2, 2);
        Emp(5, "רונית", "מזרחי", "ronit.m@example.com", Y(41), Y(10), null, 20000, true, 2, 3, 1);
        Emp(6, "עומר", "פרידמן", "omer.f@example.com", Y(35), Y(6), null, 19500, true, 2, 3, 5);
        Emp(7, "נועה", "ברק", "noa.barak@example.com", Y(33), Y(4), null, 23000, true, 3, 4, 1);
        Emp(8, "גיל", "דיין", "gil.dayan@example.com", Y(27), Y(2), null, 15000, true, 3, 4, 7);
        Emp(9, "שירה", "גולן", "shira.g@example.com", Y(26), Y(1), null, 12500, true, 4, 3, 1);
        Emp(10, "תמר", "רוזן", "tamar.r@example.com", Y(52), Y(20), M(6), 26000, false, 4, 1, null);
        Emp(11, "אלון", "חזן", "alon.hazan@example.com", Y(30), Y(7), Y(1), 24000, false, 1, 2, 1);
        Emp(12, "ליאור", "נחום", "lior.n@example.com", Y(22), Y(1), null, 8000, true, 1, 5, 2);

        Emp(101, "נדב", "קטן", "nadav.k@example.com", Y(16), Y(1), null, 16500, true, 1, 2, 2);
        Emp(102, "עדי", "צעיר", "adi.z@example.com", Y(17), M(8), null, 15001, true, 3, 4, 7);
        Emp(103, "מאיה", "נוער", "maya.n@example.com", Y(17), M(3), null, 4000, true, 3, 5, 7);
        Emp(104, "רפי", "הפוך", "rafi.h@example.com", Y(40), Y(2), Y(3), 18000, false, 2, 3, 5);
        Emp(105, "סיגל", "טעות", "sigal.t@example.com", Y(36), M(10), M(11), 14000, false, 1, 2, 2);
        Emp(106, "בן", "ישן", "ben.y@example.com", Y(44), Y(15), null, 22000, true, 5, 3, 1);
        Emp(107, "הדס", "סגורה", "hadas.s@example.com", Y(37), Y(8), null, 19000, true, 6, 3, 5);
        Emp(108, "אסף", "חסר", null, Y(34), Y(2), null, 20000, true, 1, 2, 2);
        Emp(109, "קרן", "בלי", "keren.b@example.com", Y(39), Y(4), null, 21000, true, null, 2, 2);
        Emp(110, "זיו", "אפס", "ziv.a@example.com", Y(28), Y(1), null, null, true, 2, 3, 5);
        Emp(111, "ארז", "מופרז", "erez.m@example.com", Y(32), Y(3), null, 34000, true, 1, 2, 2);
        Emp(112, "סתיו", "סטודנט", "stav.s@example.com", Y(23), Y(1), null, 13000, true, 3, 5, 7);

        return new() { ["Employee"] = employees, ["Department"] = departments, ["JobTitle"] = jobs };
    }

    public static IEnumerable<(string Name, string Description, string Json)> SampleControls() =>
    [
        ("קטינים עם שכר גבוה", "עובדים שגילם נמוך מ-18 ושכרם גבוה מ-15,000 ש\"ח",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"Age"},"operator":"lt","value":18},{"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","value":15000}]}}"""),
        ("תאריך סיום מוקדם מתאריך תחילה", "עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"EndDate"},"operator":"lt","compareField":{"path":[],"field":"HireDate"}}]}}"""),
        ("עובד פעיל במחלקה לא פעילה", "עובדים פעילים המשויכים למחלקה שאינה פעילה",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"IsActive"},"operator":"eq","value":true},{"type":"condition","field":{"path":["Department"],"field":"IsActive"},"operator":"eq","value":false}]}}"""),
        ("חסרים נתוני חובה בעובד פעיל", "עובדים פעילים שחסר להם דוא\"ל, שיוך למחלקה או שכר",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"IsActive"},"operator":"eq","value":true},{"type":"group","operator":"or","items":[{"type":"condition","field":{"path":[],"field":"Email"},"operator":"isNull"},{"type":"condition","field":{"path":[],"field":"DepartmentId"},"operator":"isNull"},{"type":"condition","field":{"path":[],"field":"Salary"},"operator":"isNull"}]}]}}"""),
        ("שכר מעל תקרת התפקיד", "עובדים ששכרם גבוה משכר המקסימום המוגדר לתפקידם",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","compareField":{"path":["JobTitle"],"field":"MaxSalary"}}]}}"""),
        ("שכר גבוה משל המנהל הישיר", "עובדים שמשתכרים יותר ממנהלם הישיר",
            """{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[{"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","compareField":{"path":["Manager"],"field":"Salary"}}]}}"""),
    ];
}
