namespace SqlSchemaBridgeMCP.Models;

public class SqlValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Analysis { get; set; } = new();
}

public class PerformanceAnalysisResult
{
    public List<string> Issues { get; set; } = new();
    public string ComplexityLevel { get; set; } = "Low";
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class ColumnReference
{
    public string Table { get; set; } = "";
    public string Column { get; set; } = "";
}

public class SqlQueryAnalysis
{
    public List<string> TablesReferenced { get; set; } = new();
    public List<ColumnReference> ColumnsReferenced { get; set; } = new();
    public List<string> JoinTypes { get; set; } = new();
    public bool HasWhereClause { get; set; }
    public bool HasOrderBy { get; set; }
    public bool HasGroupBy { get; set; }
    public bool HasHaving { get; set; }
    public bool HasSubqueries { get; set; }
    public string QueryType { get; set; } = "Unknown";
    public string EstimatedComplexity { get; set; } = "Low";
}

public class SqlValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> SyntaxErrors { get; set; } = new();
    public List<string> SchemaErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string>? PerformanceIssues { get; set; }
    public SqlQueryAnalysis? Analysis { get; set; }
    public string? Summary { get; set; }
}