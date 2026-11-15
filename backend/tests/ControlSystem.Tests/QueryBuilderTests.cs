using ControlSystem.BL;
using ControlSystem.BL.Definitions;
using ControlSystem.BL.Execution;
using ControlSystem.BL.Querying;
using static ControlSystem.Tests.TestData;

namespace ControlSystem.Tests;

public class QueryBuilderTests
{
    private readonly SqlServerQueryBuilder _builder = new();
    private readonly ControlSystem.BL.Catalog.DataCatalog _catalog = Catalog();

    [Fact]
    public void And_conditions_are_parameterized_and_values_never_inlined()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("Age"), ComparisonOperator.Lt, 18),
            Cond(Ref("Salary"), ComparisonOperator.Gt, 15000));

        var q = _builder.Build(def, _catalog);

        Assert.Contains("WHERE (DATEDIFF(YEAR, [t0].BirthDate, GETDATE()) < @p0 AND [t0].[Salary] > @p1)", q.Sql);
        Assert.DoesNotContain("15000", q.Sql);
        Assert.Equal([18m, 15000m], q.Parameters.Select(p => (decimal)p.Value));
        Assert.Contains("TOP (1000)", q.Sql);
    }

    [Fact]
    public void Related_field_adds_one_left_join_even_when_used_twice()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("IsActive", "Department"), ComparisonOperator.Eq, false),
            Cond(Ref("Name", "Department"), ComparisonOperator.IsNotNull));

        var q = _builder.Build(def, _catalog);

        Assert.Single(q.Sql.Split("LEFT JOIN"), s => s.Contains("[dbo].[Departments]"));
        Assert.Contains("ON [t1].[DepartmentId] = [t0].[DepartmentId]", q.Sql);
    }

    [Fact]
    public void Self_relation_uses_distinct_alias_and_field_to_field_comparison()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("Salary"), ComparisonOperator.Gt, compare: Ref("Salary", "Manager")));

        var q = _builder.Build(def, _catalog);

        Assert.Contains("[t0].[Salary] > [t1].[Salary]", q.Sql);
        Assert.Contains("LEFT JOIN [dbo].[Employees] AS [t1]", q.Sql);
        Assert.Empty(q.Parameters);
    }

    [Fact]
    public void Two_hop_path_chains_joins_through_the_parent_alias()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("IsActive", "Manager", "Department"), ComparisonOperator.Eq, false));

        var q = _builder.Build(def, _catalog);

        Assert.Contains("LEFT JOIN [dbo].[Employees] AS [t1] ON [t1].[EmployeeId] = [t0].[ManagerId]", q.Sql);
        Assert.Contains("LEFT JOIN [dbo].[Departments] AS [t2] ON [t2].[DepartmentId] = [t1].[DepartmentId]", q.Sql);
        Assert.Contains("[t2].[IsActive] = @p0", q.Sql);
    }

    [Fact]
    public void Nested_groups_keep_precedence()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("IsActive"), ComparisonOperator.Eq, true),
            new GroupNode(LogicalOperator.Or,
            [
                Cond(Ref("Email"), ComparisonOperator.IsNull),
                Cond(Ref("Salary"), ComparisonOperator.IsNull)
            ]));

        var q = _builder.Build(def, _catalog);

        Assert.Contains("([t0].[IsActive] = @p0 AND (([t0].[Email] IS NULL OR LTRIM(RTRIM([t0].[Email])) = N'') OR [t0].[Salary] IS NULL))", q.Sql);
    }

    [Fact]
    public void Malicious_text_stays_a_parameter_and_like_wildcards_are_escaped()
    {
        var evil = "x'; DROP TABLE dbo.Employees; --";
        var q = _builder.Build(Def(LogicalOperator.And, Cond(Ref("FullName"), ComparisonOperator.Contains, evil + "%_")), _catalog);

        Assert.DoesNotContain("DROP", q.Sql);
        Assert.Equal("%" + evil.Replace("'", "'") + "\\%\\_%", (string)q.Parameters[0].Value);
        SqlSafetyGuard.EnsureReadOnly(q.Sql);
    }

    [Fact]
    public void Dates_are_typed_and_in_list_expands_parameters()
    {
        var def = Def(LogicalOperator.Or,
            Cond(Ref("HireDate"), ComparisonOperator.Gte, "2024-01-31"),
            new ConditionNode(Ref("EmployeeId"), ComparisonOperator.In, null, [J(1), J(2), J(3)]));

        var q = _builder.Build(def, _catalog);

        Assert.Equal(new DateTime(2024, 1, 31), q.Parameters[0].Value);
        Assert.Contains("IN (@p1, @p2, @p3)", q.Sql);
    }

    [Fact]
    public void Query_selects_flags_values_and_total_for_reasons()
    {
        var q = _builder.Build(Def(LogicalOperator.And, Cond(Ref("Salary"), ComparisonOperator.Gt, 1)), _catalog);

        Assert.Contains("AS [f0]", q.Sql);
        Assert.Contains("AS [v0]", q.Sql);
        Assert.Contains("COUNT(*) OVER() AS [total]", q.Sql);
        Assert.Contains("ORDER BY [t0].[EmployeeId]", q.Sql);
    }

    [Fact]
    public void Result_mapper_explains_only_the_conditions_that_matched()
    {
        var def = Def(LogicalOperator.Or,
            Cond(Ref("Age"), ComparisonOperator.Lt, 18),
            Cond(Ref("Salary"), ComparisonOperator.Gt, 15000),
            Cond(Ref("Email"), ComparisonOperator.IsNull));
        var q = _builder.Build(def, _catalog);

        var row = new Dictionary<string, object?>
        {
            ["c0"] = 7, ["c1"] = "דנה", ["c2"] = null, ["c3"] = 16, ["c4"] = 16500m,
            ["c5"] = null, ["c6"] = null, ["c7"] = true,
            ["f0"] = 1, ["v0"] = 16, ["f1"] = 1, ["v1"] = 16500m, ["f2"] = 0, ["v2"] = "x@y.z", ["total"] = 1
        };
        var (rows, total) = ResultMapper.Map(q, new RawResult([row]));

        Assert.Equal(1, total);
        Assert.Equal(["גיל (16) קטן מ-18", "שכר (16,500) גדול מ-15,000"], rows[0].Reasons);
        Assert.Equal("דנה", rows[0].Values["FullName"]);
    }

    [Theory]
    [InlineData("Unknown", ComparisonOperator.Eq, 1)]
    [InlineData("Salary", ComparisonOperator.Contains, "a")]
    [InlineData("Salary", ComparisonOperator.Gt, "not-a-number")]
    [InlineData("HireDate", ComparisonOperator.Lt, "yesterday-ish")]
    [InlineData("Salary", ComparisonOperator.Gt, null)]
    public void Invalid_conditions_are_rejected_with_messages(string field, ComparisonOperator op, object? value)
    {
        var def = Def(LogicalOperator.And, Cond(Ref(field), op, value));
        var ex = Assert.Throws<ControlValidationException>(() => _builder.Build(def, _catalog));
        Assert.NotEmpty(ex.Errors);
    }

    [Fact]
    public void Unknown_relation_empty_group_and_too_deep_nesting_are_rejected()
    {
        Assert.NotEmpty(ControlValidator.Validate(Def(LogicalOperator.And, Cond(Ref("Name", "Nope"), ComparisonOperator.IsNull)), _catalog));
        Assert.NotEmpty(ControlValidator.Validate(Def(LogicalOperator.And), _catalog));

        DefinitionNode node = Cond(Ref("Salary"), ComparisonOperator.IsNull);
        for (var i = 0; i < 8; i++) node = new GroupNode(LogicalOperator.And, [node]);
        Assert.NotEmpty(ControlValidator.Validate(Def(LogicalOperator.And, node), _catalog));
    }

    [Fact]
    public void Comparing_fields_of_different_types_is_rejected()
    {
        var def = Def(LogicalOperator.And, Cond(Ref("Salary"), ComparisonOperator.Gt, compare: Ref("HireDate")));
        Assert.NotEmpty(ControlValidator.Validate(def, _catalog));
    }
}

public class SafetyAndJsonTests
{
    [Theory]
    [InlineData("DROP TABLE x")]
    [InlineData("SELECT 1; DELETE FROM x")]
    [InlineData("SELECT * INTO y FROM x")]
    [InlineData("SELECT 1 -- comment")]
    [InlineData("EXEC sp_who")]
    [InlineData("UPDATE x SET a = 1")]
    public void Guard_blocks_dangerous_statements(string sql) =>
        Assert.Throws<UnsafeQueryException>(() => SqlSafetyGuard.EnsureReadOnly(sql));

    [Fact]
    public void Guard_ignores_keywords_inside_string_literals() =>
        SqlSafetyGuard.EnsureReadOnly("SELECT a FROM t WHERE b = N'drop; --'");

    [Fact]
    public void Definition_json_round_trips_with_polymorphic_nodes()
    {
        const string json = """
            {"rootEntity":"Employee","root":{"type":"group","operator":"and","items":[
              {"type":"condition","field":{"path":["Department"],"field":"IsActive"},"operator":"eq","value":false},
              {"type":"group","operator":"or","items":[
                {"type":"condition","field":{"path":[],"field":"Email"},"operator":"isNull"}]}]}}
            """;

        var def = ControlJson.Parse(json);
        var again = ControlJson.Parse(ControlJson.Serialize(def));

        Assert.Empty(ControlValidator.Validate(def, Catalog()));
        Assert.IsType<GroupNode>(again.Root.Items[1]);
        Assert.Equal(ComparisonOperator.IsNull, ((ConditionNode)((GroupNode)again.Root.Items[1]).Items[0]).Operator);
    }

    [Fact]
    public void Definition_json_does_not_depend_on_property_order()
    {
        const string json = """
            {"root":{"operator":"and","items":[
              {"field":{"path":[],"field":"Age"},"operator":"lt","value":18,"type":"condition"}],"type":"group"},
             "rootEntity":"Employee"}
            """;

        var def = ControlJson.Parse(json);

        Assert.IsType<ConditionNode>(def.Root.Items[0]);
    }

    [Fact]
    public void Describer_produces_business_sentence()
    {
        var def = Def(LogicalOperator.And,
            Cond(Ref("Age"), ComparisonOperator.Lt, 18),
            Cond(Ref("EndDate"), ComparisonOperator.Lt, compare: Ref("HireDate")));

        Assert.Equal("עובדים שבהם: גיל קטן מ-18 וגם תאריך סיום העסקה מוקדם מ-\"תאריך תחילת העסקה\"",
            ControlDescriber.Describe(def, Catalog()));
    }
}
