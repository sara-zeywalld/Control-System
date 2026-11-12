/* 01_schema.sql - יצירת בסיס הנתונים, טבלאות עסקיות לדוגמה וטבלאות מנוע הבקרות.
   להרצה: sqlcmd -S . -E -f 65001 -i 01_schema.sql   (או SSMS) */
IF DB_ID(N'ControlSystemDb') IS NULL CREATE DATABASE ControlSystemDb COLLATE Hebrew_CI_AS;
GO
USE ControlSystemDb;
GO

/* ---------- ניקוי (הרצה חוזרת) ---------- */
IF OBJECT_ID(N'ctl.ControlRun') IS NOT NULL DROP TABLE ctl.ControlRun;
IF OBJECT_ID(N'ctl.Control') IS NOT NULL DROP TABLE ctl.Control;
IF OBJECT_ID(N'ctl.Relation') IS NOT NULL DROP TABLE ctl.Relation;
IF OBJECT_ID(N'ctl.Field') IS NOT NULL DROP TABLE ctl.Field;
IF OBJECT_ID(N'ctl.Entity') IS NOT NULL DROP TABLE ctl.Entity;
IF OBJECT_ID(N'dbo.Employees') IS NOT NULL DROP TABLE dbo.Employees;
IF OBJECT_ID(N'dbo.JobTitles') IS NOT NULL DROP TABLE dbo.JobTitles;
IF OBJECT_ID(N'dbo.Departments') IS NOT NULL DROP TABLE dbo.Departments;
GO
IF SCHEMA_ID(N'ctl') IS NULL EXEC(N'CREATE SCHEMA ctl');
GO

/* ---------- נתונים עסקיים לדוגמה ---------- */
CREATE TABLE dbo.Departments (
    DepartmentId INT           NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    CostCenter   NVARCHAR(20)  NULL,
    IsActive     BIT           NOT NULL
);

CREATE TABLE dbo.JobTitles (
    JobTitleId INT           NOT NULL CONSTRAINT PK_JobTitles PRIMARY KEY,
    Title      NVARCHAR(100) NOT NULL,
    MinSalary  DECIMAL(12,2) NOT NULL,
    MaxSalary  DECIMAL(12,2) NOT NULL
);

CREATE TABLE dbo.Employees (
    EmployeeId   INT           NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    FirstName    NVARCHAR(50)  NOT NULL,
    LastName     NVARCHAR(50)  NOT NULL,
    Email        NVARCHAR(200) NULL,
    BirthDate    DATE          NOT NULL,
    HireDate     DATE          NOT NULL,
    EndDate      DATE          NULL,
    Salary       DECIMAL(12,2) NULL,
    IsActive     BIT           NOT NULL,
    DepartmentId INT           NULL CONSTRAINT FK_Employees_Departments REFERENCES dbo.Departments(DepartmentId),
    JobTitleId   INT           NULL CONSTRAINT FK_Employees_JobTitles    REFERENCES dbo.JobTitles(JobTitleId),
    ManagerId    INT           NULL CONSTRAINT FK_Employees_Manager      REFERENCES dbo.Employees(EmployeeId)
);
CREATE INDEX IX_Employees_Department ON dbo.Employees(DepartmentId);
CREATE INDEX IX_Employees_JobTitle   ON dbo.Employees(JobTitleId);
CREATE INDEX IX_Employees_Manager    ON dbo.Employees(ManagerId);
GO

/* ---------- קטלוג עסקי: ממפה שמות טכניים למונחים עסקיים ---------- */
CREATE TABLE ctl.Entity (
    EntityKey        VARCHAR(50)   NOT NULL CONSTRAINT PK_ctl_Entity PRIMARY KEY,
    DisplayName      NVARCHAR(100) NOT NULL,
    PluralName       NVARCHAR(100) NOT NULL,
    Description      NVARCHAR(500) NULL,
    Synonyms         NVARCHAR(500) NULL,            -- מילים נרדפות מופרדות בפסיק (עבור AI)
    SchemaName       SYSNAME       NOT NULL,
    TableName        SYSNAME       NOT NULL,
    PrimaryKeyColumn SYSNAME       NOT NULL,
    IsQueryable      BIT           NOT NULL CONSTRAINT DF_ctl_Entity_Q DEFAULT 1   -- האם ניתן להריץ עליה בקרה
);

CREATE TABLE ctl.Field (
    EntityKey     VARCHAR(50)   NOT NULL CONSTRAINT FK_ctl_Field_Entity REFERENCES ctl.Entity(EntityKey),
    FieldKey      VARCHAR(50)   NOT NULL,
    DisplayName   NVARCHAR(100) NOT NULL,
    Description   NVARCHAR(500) NULL,
    Synonyms      NVARCHAR(500) NULL,
    ColumnName    SYSNAME       NULL,               -- עמודה פיזית, או
    SqlExpression NVARCHAR(1000) NULL,              -- ביטוי מחושב עם {t} כאליאס הטבלה (נשלט ע"י מנהל, לא ע"י המשתמש)
    DataType      VARCHAR(10)   NOT NULL CONSTRAINT CK_ctl_Field_Type CHECK (DataType IN ('string','number','date','boolean')),
    IsRequired    BIT           NOT NULL CONSTRAINT DF_ctl_Field_Req DEFAULT 0,
    ShowInResults BIT           NOT NULL CONSTRAINT DF_ctl_Field_Show DEFAULT 0,
    SortOrder     INT           NOT NULL CONSTRAINT DF_ctl_Field_Sort DEFAULT 0,
    CONSTRAINT PK_ctl_Field PRIMARY KEY (EntityKey, FieldKey),
    CONSTRAINT CK_ctl_Field_Source CHECK ((ColumnName IS NULL AND SqlExpression IS NOT NULL) OR (ColumnName IS NOT NULL AND SqlExpression IS NULL))
);

CREATE TABLE ctl.Relation (     -- קשר "רבים לאחד" מישות מקור לישות יעד
    FromEntityKey VARCHAR(50)   NOT NULL CONSTRAINT FK_ctl_Rel_From REFERENCES ctl.Entity(EntityKey),
    RelationKey   VARCHAR(50)   NOT NULL,
    ToEntityKey   VARCHAR(50)   NOT NULL CONSTRAINT FK_ctl_Rel_To REFERENCES ctl.Entity(EntityKey),
    FromColumn    SYSNAME       NOT NULL,
    ToColumn      SYSNAME       NOT NULL,
    DisplayName   NVARCHAR(100) NOT NULL,
    Description   NVARCHAR(500) NULL,
    Synonyms      NVARCHAR(500) NULL,
    CONSTRAINT PK_ctl_Relation PRIMARY KEY (FromEntityKey, RelationKey)
);

/* ---------- הגדרות בקרות והיסטוריית הרצות ---------- */
CREATE TABLE ctl.Control (
    ControlId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ctl_Control PRIMARY KEY,
    Name           NVARCHAR(200)  NOT NULL,
    Description    NVARCHAR(1000) NULL,
    RootEntityKey  VARCHAR(50)    NOT NULL CONSTRAINT FK_ctl_Control_Entity REFERENCES ctl.Entity(EntityKey),
    DefinitionJson NVARCHAR(MAX)  NOT NULL CONSTRAINT CK_ctl_Control_Json CHECK (ISJSON(DefinitionJson) = 1),
    IsDeleted      BIT            NOT NULL CONSTRAINT DF_ctl_Control_Del DEFAULT 0,
    CreatedAt      DATETIME2      NOT NULL CONSTRAINT DF_ctl_Control_C DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2      NOT NULL CONSTRAINT DF_ctl_Control_U DEFAULT SYSUTCDATETIME(),
    Source         VARCHAR(20)    NOT NULL CONSTRAINT DF_ctl_Control_Src DEFAULT 'manual'  -- manual | ai
);

CREATE TABLE ctl.ControlRun (
    RunId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ctl_ControlRun PRIMARY KEY,
    ControlId      INT           NOT NULL CONSTRAINT FK_ctl_Run_Control REFERENCES ctl.Control(ControlId),
    RunAt          DATETIME2     NOT NULL CONSTRAINT DF_ctl_Run_At DEFAULT SYSUTCDATETIME(),
    ViolationCount INT           NULL,
    DurationMs     INT           NULL,
    Succeeded      BIT           NOT NULL,
    ErrorMessage   NVARCHAR(1000) NULL
);
CREATE INDEX IX_ctl_Run_Control ON ctl.ControlRun(ControlId, RunAt DESC);
GO
