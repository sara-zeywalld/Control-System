using System.Text.Json;
using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.Tests;

/// <summary>קטלוג בזיכרון, תואם ל-db/03_catalog_and_controls.sql.</summary>
internal static class TestData
{
    public static DataCatalog Catalog()
    {
        FieldMeta F(string e, string k, string name, FieldType t, string? col = null, string? expr = null, bool show = false, bool req = false) =>
            new(e, k, name, null, [], expr is null ? col ?? k : null, expr, t, req, show, 0);

        var employee = new EntityMeta("Employee", "עובד", "עובדים", null, [], "dbo", "Employees", "EmployeeId", true,
        [
            F("Employee", "EmployeeId", "מספר עובד", FieldType.Number, show: true),
            F("Employee", "FullName", "שם מלא", FieldType.String, expr: "{t}.FirstName + N' ' + {t}.LastName", show: true),
            F("Employee", "Email", "דוא\"ל", FieldType.String, show: true, req: true),
            F("Employee", "Age", "גיל", FieldType.Number, expr: "DATEDIFF(YEAR, {t}.BirthDate, GETDATE())", show: true),
            F("Employee", "Salary", "שכר", FieldType.Number, show: true),
            F("Employee", "HireDate", "תאריך תחילת העסקה", FieldType.Date, show: true),
            F("Employee", "EndDate", "תאריך סיום העסקה", FieldType.Date, show: true),
            F("Employee", "IsActive", "פעיל", FieldType.Boolean, show: true)
        ],
        [
            new RelationMeta("Employee", "Department", "Department", "DepartmentId", "DepartmentId", "מחלקה", null, []),
            new RelationMeta("Employee", "Manager", "Employee", "ManagerId", "EmployeeId", "מנהל ישיר", null, [])
        ]);

        var department = new EntityMeta("Department", "מחלקה", "מחלקות", null, [], "dbo", "Departments", "DepartmentId", true,
        [
            F("Department", "Name", "שם מחלקה", FieldType.String, show: true),
            F("Department", "IsActive", "פעילה", FieldType.Boolean, show: true)
        ], []);

        return new DataCatalog([employee, department]);
    }

    public static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);

    public static FieldRef Ref(string field, params string[] path) => new([.. path], field);

    public static ConditionNode Cond(FieldRef f, ComparisonOperator op, object? value = null, FieldRef? compare = null) =>
        new(f, op, value is null ? null : J(value), null, compare);

    public static ControlDefinition Def(LogicalOperator op, params DefinitionNode[] items) =>
        new("Employee", new GroupNode(op, [.. items]));
}
