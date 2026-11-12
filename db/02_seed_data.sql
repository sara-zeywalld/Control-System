/* 02_seed_data.sql - נתוני דוגמה: רשומות תקינות לצד רשומות שגויות (מסומנות בהערה) */
USE ControlSystemDb;
GO
DELETE FROM dbo.Employees; DELETE FROM dbo.JobTitles; DELETE FROM dbo.Departments;
GO

INSERT dbo.Departments (DepartmentId, Name, CostCenter, IsActive) VALUES
 (1, N'פיתוח',   N'CC-100', 1),
 (2, N'כספים',   N'CC-200', 1),
 (3, N'שיווק',   N'CC-300', 1),
 (4, N'משאבי אנוש', N'CC-400', 1),
 (5, N'מחלקת מו"פ ישנה', N'CC-900', 0),   -- מחלקה לא פעילה
 (6, N'לוגיסטיקה (סגורה)', NULL, 0);      -- מחלקה לא פעילה

INSERT dbo.JobTitles (JobTitleId, Title, MinSalary, MaxSalary) VALUES
 (1, N'מנהל/ת',      25000, 45000),
 (2, N'מפתח/ת',      14000, 30000),
 (3, N'רואה חשבון',  12000, 22000),
 (4, N'מנהל/ת שיווק', 13000, 25000),
 (5, N'סטודנט/ית',    5000, 10000);

DECLARE @today DATE = CAST(GETDATE() AS DATE);

/* עובדים תקינים */
INSERT dbo.Employees (EmployeeId, FirstName, LastName, Email, BirthDate, HireDate, EndDate, Salary, IsActive, DepartmentId, JobTitleId, ManagerId) VALUES
 (1,  N'דנה',   N'כהן',     N'dana.cohen@example.com',  DATEADD(YEAR,-45,@today), DATEADD(YEAR,-12,@today), NULL, 38000, 1, 1, 1, NULL),
 (2,  N'יוסי',  N'לוי',     N'yossi.levi@example.com',  DATEADD(YEAR,-38,@today), DATEADD(YEAR,-9,@today),  NULL, 28000, 1, 1, 2, 1),
 (3,  N'מיכל',  N'אברהם',   N'michal.a@example.com',    DATEADD(YEAR,-31,@today), DATEADD(YEAR,-5,@today),  NULL, 21000, 1, 1, 2, 1),
 (4,  N'אורי',  N'שמש',     N'uri.shemesh@example.com', DATEADD(YEAR,-29,@today), DATEADD(YEAR,-3,@today),  NULL, 17000, 1, 1, 2, 2),
 (5,  N'רונית', N'מזרחי',   N'ronit.m@example.com',     DATEADD(YEAR,-41,@today), DATEADD(YEAR,-10,@today), NULL, 20000, 1, 2, 3, 1),
 (6,  N'עומר',  N'פרידמן',  N'omer.f@example.com',      DATEADD(YEAR,-35,@today), DATEADD(YEAR,-6,@today),  NULL, 19500, 1, 2, 3, 5),
 (7,  N'נועה',  N'ברק',     N'noa.barak@example.com',   DATEADD(YEAR,-33,@today), DATEADD(YEAR,-4,@today),  NULL, 23000, 1, 3, 4, 1),
 (8,  N'גיל',   N'דיין',    N'gil.dayan@example.com',   DATEADD(YEAR,-27,@today), DATEADD(YEAR,-2,@today),  NULL, 15000, 1, 3, 4, 7),
 (9,  N'שירה',  N'גולן',    N'shira.g@example.com',     DATEADD(YEAR,-26,@today), DATEADD(YEAR,-1,@today),  NULL, 12500, 1, 4, 3, 1),
 (10, N'תמר',   N'רוזן',    N'tamar.r@example.com',     DATEADD(YEAR,-52,@today), DATEADD(YEAR,-20,@today), DATEADD(MONTH,-6,@today), 26000, 0, 4, 1, NULL), -- עזבה כדין
 (11, N'אלון',  N'חזן',     N'alon.hazan@example.com',  DATEADD(YEAR,-30,@today), DATEADD(YEAR,-7,@today),  DATEADD(YEAR,-1,@today),  24000, 0, 1, 2, 1),    -- עזב כדין
 (12, N'ליאור', N'נחום',    N'lior.n@example.com',      DATEADD(YEAR,-22,@today), DATEADD(YEAR,-1,@today),  NULL, 8000,  1, 1, 5, 2);        -- סטודנט תקין

/* עובדים עם בעיות נתונים (מקרי כישלון) */
INSERT dbo.Employees (EmployeeId, FirstName, LastName, Email, BirthDate, HireDate, EndDate, Salary, IsActive, DepartmentId, JobTitleId, ManagerId) VALUES
 -- קטין עם שכר גבוה מ-15,000
 (101, N'נדב',   N'קטן',    N'nadav.k@example.com',  DATEADD(YEAR,-16,@today), DATEADD(YEAR,-1,@today), NULL, 16500, 1, 1, 2, 2),
 (102, N'עדי',   N'צעיר',   N'adi.z@example.com',    DATEADD(YEAR,-17,@today), DATEADD(MONTH,-8,@today), NULL, 15001, 1, 3, 4, 7),
 -- קטין עם שכר נמוך (לא אמור להיכשל בבקרה הראשונה)
 (103, N'מאיה',  N'נוער',   N'maya.n@example.com',   DATEADD(YEAR,-17,@today), DATEADD(MONTH,-3,@today), NULL, 4000, 1, 3, 5, 7),
 -- תאריך סיום מוקדם מתאריך תחילה
 (104, N'רפי',   N'הפוך',   N'rafi.h@example.com',   DATEADD(YEAR,-40,@today), DATEADD(YEAR,-2,@today), DATEADD(YEAR,-3,@today), 18000, 0, 2, 3, 5),
 (105, N'סיגל',  N'טעות',   N'sigal.t@example.com',  DATEADD(YEAR,-36,@today), DATEADD(MONTH,-10,@today), DATEADD(MONTH,-11,@today), 14000, 0, 1, 2, 2),
 -- עובדים פעילים במחלקה לא פעילה
 (106, N'בן',    N'ישן',    N'ben.y@example.com',    DATEADD(YEAR,-44,@today), DATEADD(YEAR,-15,@today), NULL, 22000, 1, 5, 3, 1),
 (107, N'הדס',   N'סגורה',  N'hadas.s@example.com',  DATEADD(YEAR,-37,@today), DATEADD(YEAR,-8,@today),  NULL, 19000, 1, 6, 3, 5),
 -- חסרים נתוני חובה (דוא"ל / מחלקה / שכר)
 (108, N'אסף',   N'חסר',    NULL,                    DATEADD(YEAR,-34,@today), DATEADD(YEAR,-2,@today),  NULL, 20000, 1, 1, 2, 2),
 (109, N'קרן',   N'בלי',    N'keren.b@example.com',  DATEADD(YEAR,-39,@today), DATEADD(YEAR,-4,@today),  NULL, 21000, 1, NULL, 2, 2),
 (110, N'זיו',   N'אפס',    N'ziv.a@example.com',    DATEADD(YEAR,-28,@today), DATEADD(YEAR,-1,@today),  NULL, NULL,  1, 2, 3, 5),
 -- שכר חורג מהתקרה של תפקיד / שכר גבוה משל המנהל
 (111, N'ארז',   N'מופרז',  N'erez.m@example.com',   DATEADD(YEAR,-32,@today), DATEADD(YEAR,-3,@today),  NULL, 34000, 1, 1, 2, 2),
 (112, N'סתיו',  N'סטודנט', N'stav.s@example.com',   DATEADD(YEAR,-23,@today), DATEADD(YEAR,-1,@today),  NULL, 13000, 1, 3, 5, 7);
GO
