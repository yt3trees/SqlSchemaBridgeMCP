using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Repositories;

namespace SqlSchemaBridgeMCP.Tools;

public class SqlValidationTools
{
    private readonly ISchemaRepository _schemaRepository;

    public SqlValidationTools(ISchemaRepository schemaRepository)
    {
        _schemaRepository = schemaRepository;
    }

    [McpServerTool]
    [Description("Validates SQL query syntax and logic against the current schema")]
    public async Task<string> ValidateSqlQuery(
        [Description("SQL query to validate")] string sqlQuery,
        [Description("Include detailed analysis in the result")] bool includeAnalysis = true,
        [Description("Check for potential performance issues")] bool checkPerformance = false)
    {
        var response = new SqlValidationResponse();

        try
        {
            // 1. Syntax validation
            var syntaxResult = ValidateSyntax(sqlQuery);
            response.SyntaxErrors = syntaxResult.Errors;

            // 2. Schema consistency check
            var schemaResult = await ValidateAgainstSchema(sqlQuery);
            response.SchemaErrors = schemaResult.Errors;
            response.Warnings = schemaResult.Warnings;

            // 3. Performance analysis (optional)
            if (checkPerformance)
            {
                var performanceResult = AnalyzePerformance(sqlQuery);
                response.PerformanceIssues = performanceResult.Issues;
            }

            // 4. Detailed analysis (optional)
            if (includeAnalysis)
            {
                response.Analysis = AnalyzeQuery(sqlQuery);
            }

            // 5. Overall validation result
            response.IsValid = !response.SyntaxErrors.Any() && !response.SchemaErrors.Any();

            // 6. Generate summary
            response.Summary = GenerateSummary(response);

            return JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            response.IsValid = false;
            response.SyntaxErrors.Add($"Validation error: {ex.Message}");
            return JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }

    private SqlValidationResult ValidateSyntax(string sqlQuery)
    {
        var result = new SqlValidationResult();

        try
        {
            // Use Microsoft.SqlServer.TransactSql.ScriptDom for syntax parsing
            var parser = new TSql150Parser(true);
            IList<ParseError> errors;
            var fragment = parser.Parse(new StringReader(sqlQuery), out errors);

            if (errors.Any())
            {
                result.IsValid = false;
                result.Errors = errors.Select(e => $"Line {e.Line}, Column {e.Column}: {e.Message}").ToList();
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }

    private async Task<SqlValidationResult> ValidateAgainstSchema(string sqlQuery)
    {
        var result = new SqlValidationResult { IsValid = true };

        try
        {
            // Extract and validate table names
            var referencedTables = ExtractTableNames(sqlQuery);
            var existingTables = await _schemaRepository.GetAllTablesAsync();

            foreach (var table in referencedTables)
            {
                var foundTable = existingTables.FirstOrDefault(t => 
                    string.Equals(t.PhysicalName, table, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.LogicalName, table, StringComparison.OrdinalIgnoreCase));

                if (foundTable == null)
                {
                    result.Errors.Add($"Table '{table}' not found in schema");
                    result.IsValid = false;
                }
            }

            // Extract and validate column names
            var referencedColumns = ExtractColumnReferences(sqlQuery);
            var existingColumns = await _schemaRepository.GetAllColumnsAsync();

            foreach (var (table, column) in referencedColumns)
            {
                var foundColumn = existingColumns.FirstOrDefault(c => 
                    (string.IsNullOrEmpty(table) || string.Equals(c.TablePhysicalName, table, StringComparison.OrdinalIgnoreCase)) &&
                    (string.Equals(c.PhysicalName, column, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(c.LogicalName, column, StringComparison.OrdinalIgnoreCase)));

                if (foundColumn == null)
                {
                    var tablePrefix = string.IsNullOrEmpty(table) ? "" : $"{table}.";
                    result.Warnings.Add($"Column '{tablePrefix}{column}' not found in schema or table context unclear");
                }
            }

            // Validate JOIN conditions
            var joinIssues = await ValidateJoinConditions(sqlQuery);
            result.Warnings.AddRange(joinIssues);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Schema validation warning: {ex.Message}");
        }

        return result;
    }

    private PerformanceAnalysisResult AnalyzePerformance(string sqlQuery)
    {
        var result = new PerformanceAnalysisResult();
        var queryUpper = sqlQuery.ToUpper();

        // Detect SELECT *
        if (Regex.IsMatch(sqlQuery, @"SELECT\s+\*", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Consider specifying column names instead of SELECT * for better performance");
        }

        // Detect queries without WHERE clause (potential full table scan)
        if (!Regex.IsMatch(sqlQuery, @"WHERE", RegexOptions.IgnoreCase) && 
            Regex.IsMatch(sqlQuery, @"FROM\s+\w+", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Query without WHERE clause may cause full table scan");
        }

        // Detect ORDER BY without LIMIT
        if (Regex.IsMatch(sqlQuery, @"ORDER\s+BY", RegexOptions.IgnoreCase) && 
            !Regex.IsMatch(sqlQuery, @"LIMIT|TOP", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("ORDER BY without LIMIT may cause performance issues on large datasets");
        }

        // Detect nested subqueries
        var subqueryCount = Regex.Matches(sqlQuery, @"\(\s*SELECT", RegexOptions.IgnoreCase).Count;
        if (subqueryCount > 2)
        {
            result.Issues.Add($"Query contains {subqueryCount} subqueries - consider using JOINs for better performance");
        }

        // Detect LIKE with leading wildcard
        if (Regex.IsMatch(sqlQuery, @"LIKE\s+['""]%", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("LIKE patterns starting with % cannot use indexes efficiently");
        }

        // Calculate complexity
        var complexityScore = CalculateComplexityScore(sqlQuery);
        result.ComplexityLevel = complexityScore switch
        {
            <= 3 => "Low",
            <= 7 => "Medium",
            <= 12 => "High",
            _ => "Very High"
        };

        result.Metrics["ComplexityScore"] = complexityScore;
        result.Metrics["SubqueryCount"] = subqueryCount;

        return result;
    }

    private SqlQueryAnalysis AnalyzeQuery(string sqlQuery)
    {
        var analysis = new SqlQueryAnalysis();

        // Determine query type
        analysis.QueryType = DetermineQueryType(sqlQuery);

        // Extract table references
        analysis.TablesReferenced = ExtractTableNames(sqlQuery);

        // Extract column references
        analysis.ColumnsReferenced = ExtractColumnReferences(sqlQuery);

        // Extract JOIN types
        analysis.JoinTypes = ExtractJoinTypes(sqlQuery);

        // Check for clause existence
        analysis.HasWhereClause = Regex.IsMatch(sqlQuery, @"\bWHERE\b", RegexOptions.IgnoreCase);
        analysis.HasOrderBy = Regex.IsMatch(sqlQuery, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase);
        analysis.HasGroupBy = Regex.IsMatch(sqlQuery, @"\bGROUP\s+BY\b", RegexOptions.IgnoreCase);
        analysis.HasHaving = Regex.IsMatch(sqlQuery, @"\bHAVING\b", RegexOptions.IgnoreCase);
        analysis.HasSubqueries = Regex.IsMatch(sqlQuery, @"\(\s*SELECT", RegexOptions.IgnoreCase);

        // Estimate complexity
        var complexityScore = CalculateComplexityScore(sqlQuery);
        analysis.EstimatedComplexity = complexityScore switch
        {
            <= 3 => "Low",
            <= 7 => "Medium",
            <= 12 => "High",
            _ => "Very High"
        };

        return analysis;
    }

    private List<string> ExtractTableNames(string sqlQuery)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract table names from FROM clause
        var fromMatches = Regex.Matches(sqlQuery, @"\bFROM\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase);
        foreach (Match match in fromMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // Extract table names from JOIN clause
        var joinMatches = Regex.Matches(sqlQuery, @"\bJOIN\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase);
        foreach (Match match in joinMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        return tables.ToList();
    }

    private List<(string Table, string Column)> ExtractColumnReferences(string sqlQuery)
    {
        var columns = new List<(string, string)>();

        // Extract qualified column references (table.column format)
        var qualifiedMatches = Regex.Matches(sqlQuery, @"\b([a-zA-Z_][a-zA-Z0-9_]*)\.([a-zA-Z_][a-zA-Z0-9_]*)\b", RegexOptions.IgnoreCase);
        foreach (Match match in qualifiedMatches)
        {
            columns.Add((match.Groups[1].Value, match.Groups[2].Value));
        }

        // Extract simple column names from SELECT clause
        var selectMatch = Regex.Match(sqlQuery, @"SELECT\s+(.*?)\s+FROM", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (selectMatch.Success && !selectMatch.Groups[1].Value.Trim().Equals("*"))
        {
            var selectColumns = selectMatch.Groups[1].Value.Split(',');
            foreach (var col in selectColumns)
            {
                var cleanColumn = col.Trim().Split(' ')[0]; // Remove aliases
                if (!cleanColumn.Contains('.') && Regex.IsMatch(cleanColumn, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    columns.Add(("", cleanColumn));
                }
            }
        }

        return columns;
    }

    private List<string> ExtractJoinTypes(string sqlQuery)
    {
        var joinTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var patterns = new[]
        {
            @"\bINNER\s+JOIN\b",
            @"\bLEFT\s+(?:OUTER\s+)?JOIN\b",
            @"\bRIGHT\s+(?:OUTER\s+)?JOIN\b",
            @"\bFULL\s+(?:OUTER\s+)?JOIN\b",
            @"\bCROSS\s+JOIN\b"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(sqlQuery, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                joinTypes.Add(match.Value.ToUpper());
            }
        }

        return joinTypes.ToList();
    }

    private async Task<List<string>> ValidateJoinConditions(string sqlQuery)
    {
        var issues = new List<string>();

        try
        {
            // Detect JOIN without proper ON condition
            var joinMatches = Regex.Matches(sqlQuery, @"\bJOIN\s+([a-zA-Z_][a-zA-Z0-9_]*)\s+(?:AS\s+[a-zA-Z_][a-zA-Z0-9_]*\s+)?(?!ON)", RegexOptions.IgnoreCase);
            if (joinMatches.Count > 0)
            {
                issues.Add("JOIN detected without proper ON condition - this may result in a cartesian product");
            }

            // Suggest proper JOIN conditions based on relationships
            var relations = await _schemaRepository.GetAllRelationsAsync();
            var tables = ExtractTableNames(sqlQuery);

            if (tables.Count > 1 && relations.Any())
            {
                foreach (var table1 in tables)
                {
                    foreach (var table2 in tables)
                    {
                        if (table1 != table2)
                        {
                            var relation = relations.FirstOrDefault(r => 
                                (string.Equals(r.SourceTable, table1, StringComparison.OrdinalIgnoreCase) && 
                                 string.Equals(r.TargetTable, table2, StringComparison.OrdinalIgnoreCase)) ||
                                (string.Equals(r.SourceTable, table2, StringComparison.OrdinalIgnoreCase) && 
                                 string.Equals(r.TargetTable, table1, StringComparison.OrdinalIgnoreCase)));

                            if (relation != null)
                            {
                                var expectedJoin = $"{relation.SourceTable}.{relation.SourceColumn} = {relation.TargetTable}.{relation.TargetColumn}";
                                if (!sqlQuery.Contains(expectedJoin, StringComparison.OrdinalIgnoreCase))
                                {
                                    issues.Add($"Consider using proper JOIN condition: {expectedJoin}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add($"JOIN validation warning: {ex.Message}");
        }

        return issues;
    }

    private string DetermineQueryType(string sqlQuery)
    {
        var queryUpper = sqlQuery.TrimStart().ToUpper();
        
        if (queryUpper.StartsWith("SELECT")) return "SELECT";
        if (queryUpper.StartsWith("INSERT")) return "INSERT";
        if (queryUpper.StartsWith("UPDATE")) return "UPDATE";
        if (queryUpper.StartsWith("DELETE")) return "DELETE";
        if (queryUpper.StartsWith("CREATE")) return "CREATE";
        if (queryUpper.StartsWith("ALTER")) return "ALTER";
        if (queryUpper.StartsWith("DROP")) return "DROP";
        
        return "Unknown";
    }

    private int CalculateComplexityScore(string sqlQuery)
    {
        int score = 0;

        // Base query score
        score += 1;

        // 1 point per JOIN
        score += Regex.Matches(sqlQuery, @"\bJOIN\b", RegexOptions.IgnoreCase).Count;

        // 2 points per subquery
        score += Regex.Matches(sqlQuery, @"\(\s*SELECT", RegexOptions.IgnoreCase).Count * 2;

        // 1 point per aggregate function
        score += Regex.Matches(sqlQuery, @"\b(COUNT|SUM|AVG|MAX|MIN|GROUP_CONCAT)\s*\(", RegexOptions.IgnoreCase).Count;

        // 1 point per CASE WHEN
        score += Regex.Matches(sqlQuery, @"\bCASE\s+WHEN\b", RegexOptions.IgnoreCase).Count;

        // 1 point per UNION
        score += Regex.Matches(sqlQuery, @"\bUNION\b", RegexOptions.IgnoreCase).Count;

        // 2 points per window function
        score += Regex.Matches(sqlQuery, @"\bOVER\s*\(", RegexOptions.IgnoreCase).Count * 2;

        return score;
    }

    private string GenerateSummary(SqlValidationResponse response)
    {
        var summary = new StringBuilder();

        if (response.IsValid)
        {
            summary.Append("✅ SQL query is valid");
        }
        else
        {
            summary.Append("❌ SQL query has issues");
        }

        if (response.SyntaxErrors.Any())
        {
            summary.Append($" ({response.SyntaxErrors.Count} syntax error(s))");
        }

        if (response.SchemaErrors.Any())
        {
            summary.Append($" ({response.SchemaErrors.Count} schema error(s))");
        }

        if (response.Warnings.Any())
        {
            summary.Append($" ({response.Warnings.Count} warning(s))");
        }

        if (response.PerformanceIssues?.Any() == true)
        {
            summary.Append($" ({response.PerformanceIssues.Count} performance issue(s))");
        }

        if (response.Analysis != null)
        {
            summary.Append($". Query complexity: {response.Analysis.EstimatedComplexity}");
        }

        return summary.ToString();
    }
}