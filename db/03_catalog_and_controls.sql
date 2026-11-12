/* 03_catalog_and_controls.sql - הגדרת הקטלוג העסקי ובקרות לדוגמה.
   הוספת טבלה חדשה לבקרות = הוספת שורות כאן בלבד, ללא שינוי קוד. */
USE ControlSystemDb;
GO
DELETE FROM ctl.ControlRun; DELETE FROM ctl.Control;
DELETE FROM ctl.Relation; DELETE FROM ctl.Field; DELETE FROM ctl.Entity;
GO

INSERT ctl.Entity (EntityKey, DisplayName, PluralName, Description, Synonyms, SchemaName, TableName, PrimaryKeyColumn) VALUES
 ('Employee',   N'עובד',   N'עובדים',   N'עובדי הארגון',            N'עובד,עובדים,עובדת,עובדות,משרת,אנשי צוות', 'dbo', 'Employees',   'EmployeeId'),
 ('Department', N'מחלקה',  N'מחלקות',   N'יחידות ארגוניות',          N'מחלקה,מחלקות,יחידה',                      'dbo', 'Departments', 'DepartmentId'),
 ('JobTitle',   N'תפקיד',  N'תפקידים',  N'תפקידים וטווחי שכר מותרים', N'תפקיד,תפקידים,משרה',                      'dbo', 'JobTitles',   'JobTitleId');

/* ----- עובד ----- */
INSERT ctl.Field (EntityKey, FieldKey, DisplayName, Description, Synonyms, ColumnName, SqlExpression, DataType, IsRequired, ShowInResults, SortOrder) VALUES
 ('Employee','EmployeeId', N'מספר עובד', NULL, N'מזהה,מס עובד', 'EmployeeId', NULL, 'number', 0, 1, 10),
 ('Employee','FullName',   N'שם מלא',    N'שם פרטי ושם משפחה', N'שם', NULL, N'{t}.FirstName + N'' '' + {t}.LastName', 'string', 0, 1, 20),
 ('Employee','FirstName',  N'שם פרטי',   NULL, NULL, 'FirstName', NULL, 'string', 0, 0, 30),
 ('Employee','LastName',   N'שם משפחה',  NULL, NULL, 'LastName',  NULL, 'string', 0, 0, 40),
 ('Employee','Email',      N'דוא"ל',     N'כתובת דואר אלקטרוני', N'מייל,אימייל,דואל', 'Email', NULL, 'string', 1, 1, 50),
 ('Employee','BirthDate',  N'תאריך לידה', NULL, N'לידה', 'BirthDate', NULL, 'date', 0, 0, 60),
 ('Employee','Age',        N'גיל',       N'גיל בשנים שלמות (מחושב מתאריך הלידה)', N'גילם,גילו,גילה',
     NULL, N'DATEDIFF(YEAR, {t}.BirthDate, GETDATE()) - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, {t}.BirthDate, GETDATE()), {t}.BirthDate) > CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END',
     'number', 0, 1, 70),
 ('Employee','HireDate',   N'תאריך תחילת העסקה', N'תאריך תחילת עבודה', N'תחילת העסקה,תחילת עבודה,תחילה,התחלה', 'HireDate', NULL, 'date', 0, 1, 80),
 ('Employee','EndDate',    N'תאריך סיום העסקה',  N'תאריך סיום עבודה',  N'סיום העסקה,סיום עבודה,סיום,עזיבה',  'EndDate',  NULL, 'date', 0, 1, 90),
 ('Employee','Salary',     N'שכר',       N'שכר חודשי ברוטו בש"ח', N'משכורת,משכורתם,שכרם,שכרו,שכרה', 'Salary', NULL, 'number', 1, 1, 100),
 ('Employee','IsActive',   N'פעיל',      N'האם העובד פעיל כיום', N'פעילים,פעילה,פעילות', 'IsActive', NULL, 'boolean', 0, 1, 110),
 ('Employee','DepartmentName', N'שם מחלקה', N'שם המחלקה אליה משויך העובד', NULL, NULL,
     N'(SELECT d.Name FROM dbo.Departments AS d WHERE d.DepartmentId = {t}.DepartmentId)', 'string', 0, 1, 120);

INSERT ctl.Field (EntityKey, FieldKey, DisplayName, Description, Synonyms, ColumnName, SqlExpression, DataType, IsRequired, ShowInResults, SortOrder) VALUES
 ('Employee','DepartmentId', N'מחלקה משויכת', N'שיוך העובד למחלקה', NULL, 'DepartmentId', NULL, 'number', 1, 0, 130);

/* ----- מחלקה ----- */
INSERT ctl.Field (EntityKey, FieldKey, DisplayName, Description, Synonyms, ColumnName, SqlExpression, DataType, IsRequired, ShowInResults, SortOrder) VALUES
 ('Department','DepartmentId', N'מספר מחלקה', NULL, NULL, 'DepartmentId', NULL, 'number', 0, 1, 10),
 ('Department','Name',         N'שם מחלקה',   NULL, NULL, 'Name',         NULL, 'string', 0, 1, 20),
 ('Department','CostCenter',   N'מרכז עלות',  NULL, N'מרכז רווח', 'CostCenter', NULL, 'string', 1, 1, 30),
 ('Department','IsActive',     N'פעילה',      N'האם המחלקה פעילה', N'פעיל,פעילה,פעילים', 'IsActive', NULL, 'boolean', 0, 1, 40);

/* ----- תפקיד ----- */
INSERT ctl.Field (EntityKey, FieldKey, DisplayName, Description, Synonyms, ColumnName, SqlExpression, DataType, IsRequired, ShowInResults, SortOrder) VALUES
 ('JobTitle','JobTitleId', N'מספר תפקיד', NULL, NULL, 'JobTitleId', NULL, 'number', 0, 1, 10),
 ('JobTitle','Title',      N'שם תפקיד',   NULL, NULL, 'Title',      NULL, 'string', 0, 1, 20),
 ('JobTitle','MinSalary',  N'שכר מינימום לתפקיד', NULL, N'מינימום', 'MinSalary', NULL, 'number', 0, 1, 30),
 ('JobTitle','MaxSalary',  N'שכר מקסימום לתפקיד', NULL, N'תקרת שכר,מקסימום', 'MaxSalary', NULL, 'number', 0, 1, 40);

/* ----- קשרים (מהישות המקורית ליעד) ----- */
INSERT ctl.Relation (FromEntityKey, RelationKey, ToEntityKey, FromColumn, ToColumn, DisplayName, Description, Synonyms) VALUES
 ('Employee','Department','Department','DepartmentId','DepartmentId', N'מחלקה',     N'המחלקה אליה משויך העובד', N'מחלקה,משויך למחלקה,שייך למחלקה'),
 ('Employee','JobTitle',  'JobTitle',  'JobTitleId',  'JobTitleId',   N'תפקיד',     N'תפקיד העובד',            N'תפקיד,משרה'),
 ('Employee','Manager',   'Employee',  'ManagerId',   'EmployeeId',   N'מנהל ישיר', N'המנהל הישיר של העובד',   N'מנהל,מנהלו,מנהלם');
GO

/* ---------- בקרות לדוגמה (הגדרה בלבד; השאילתה נוצרת דינמית בזמן הרצה) ---------- */
INSERT ctl.Control (Name, Description, RootEntityKey, DefinitionJson) VALUES
(N'קטינים עם שכר גבוה', N'עובדים שגילם נמוך מ-18 ושכרם גבוה מ-15,000 ש"ח', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"Age"},"operator":"lt","value":18},
    {"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","value":15000}]}}'),

(N'תאריך סיום מוקדם מתאריך תחילה', N'עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"EndDate"},"operator":"lt","compareField":{"path":[],"field":"HireDate"}}]}}'),

(N'עובד פעיל במחלקה לא פעילה', N'עובדים פעילים המשויכים למחלקה שאינה פעילה', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"IsActive"},"operator":"eq","value":true},
    {"type":"condition","field":{"path":["Department"],"field":"IsActive"},"operator":"eq","value":false}]}}'),

(N'חסרים נתוני חובה בעובד פעיל', N'עובדים פעילים שחסר להם דוא"ל, שיוך למחלקה או שכר', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"IsActive"},"operator":"eq","value":true},
    {"type":"group","operator":"or","items":[
      {"type":"condition","field":{"path":[],"field":"Email"},"operator":"isNull"},
      {"type":"condition","field":{"path":[],"field":"DepartmentId"},"operator":"isNull"},
      {"type":"condition","field":{"path":[],"field":"Salary"},"operator":"isNull"}]}]}}'),

(N'שכר מעל תקרת התפקיד', N'עובדים ששכרם גבוה משכר המקסימום המוגדר לתפקידם', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","compareField":{"path":["JobTitle"],"field":"MaxSalary"}}]}}'),

(N'שכר גבוה משל המנהל הישיר', N'עובדים שמשתכרים יותר ממנהלם הישיר', 'Employee',
 N'{"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
    {"type":"condition","field":{"path":[],"field":"Salary"},"operator":"gt","compareField":{"path":["Manager"],"field":"Salary"}}]}}');
GO
