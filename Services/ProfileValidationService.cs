using System.Text;

namespace SqlSchemaBridgeMCP.Services;

public class ProfileValidationService
{
    private readonly ProfileManager _profileManager;

    public ProfileValidationService(ProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<ValidationResult> ValidateProfileAsync(string profileName)
    {
        var result = new ValidationResult { ProfileName = profileName };
        var profilePath = _profileManager.GetProfileDirectory(profileName);

        if (!Directory.Exists(profilePath))
        {
            result.AddError($"Profile directory does not exist: {profilePath}");
            return result;
        }

        await ValidateTablesFileAsync(profilePath, result);
        await ValidateColumnsFileAsync(profilePath, result);
        await ValidateRelationsFileAsync(profilePath, result);
        await ValidateConsistencyAsync(profilePath, result);

        return result;
    }

    private async Task ValidateTablesFileAsync(string profilePath, ValidationResult result)
    {
        var tablesFile = Path.Combine(profilePath, "tables.csv");

        if (!File.Exists(tablesFile))
        {
            result.AddError("tables.csv file does not exist");
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(tablesFile);
            if (lines.Length == 0)
            {
                result.AddError("tables.csv file is empty");
                return;
            }

            var header = lines[0];
            var expectedColumns = new[] { "database_name", "schema_name", "logical_name", "physical_name", "primary_key", "description" };
            var headerColumns = header.Split(',').Select(c => c.Trim()).ToArray();

            foreach (var expectedCol in expectedColumns)
            {
                if (!headerColumns.Contains(expectedCol))
                {
                    result.AddError($"tables.csv: Required column not found: {expectedCol}");
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length != expectedColumns.Length)
                {
                    result.AddWarning($"tables.csv: Incorrect number of columns in row {i + 1} (expected: {expectedColumns.Length}, actual: {columns.Length})");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(2)))
                {
                    result.AddError($"tables.csv: logical_name is empty in row {i + 1}");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(3)))
                {
                    result.AddError($"tables.csv: physical_name is empty in row {i + 1}");
                }
            }

            result.AddInfo($"tables.csv: Validated {lines.Length - 1} table definitions");
        }
        catch (Exception ex)
        {
            result.AddError($"Error reading tables.csv: {ex.Message}");
        }
    }

    private async Task ValidateColumnsFileAsync(string profilePath, ValidationResult result)
    {
        var columnsFile = Path.Combine(profilePath, "columns.csv");

        if (!File.Exists(columnsFile))
        {
            result.AddError("columns.csv file does not exist");
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(columnsFile);
            if (lines.Length == 0)
            {
                result.AddError("columns.csv file is empty");
                return;
            }

            var header = lines[0];
            var expectedColumns = new[] { "table_physical_name", "logical_name", "physical_name", "data_type", "description" };
            var headerColumns = header.Split(',').Select(c => c.Trim()).ToArray();

            foreach (var expectedCol in expectedColumns)
            {
                if (!headerColumns.Contains(expectedCol))
                {
                    result.AddError($"columns.csv: Required column not found: {expectedCol}");
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length != expectedColumns.Length)
                {
                    result.AddWarning($"columns.csv: Incorrect number of columns in row {i + 1} (expected: {expectedColumns.Length}, actual: {columns.Length})");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(0)))
                {
                    result.AddError($"columns.csv: table_physical_name is empty in row {i + 1}");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(1)))
                {
                    result.AddError($"columns.csv: logical_name is empty in row {i + 1}");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(2)))
                {
                    result.AddError($"columns.csv: physical_name is empty in row {i + 1}");
                }

                if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(3)))
                {
                    result.AddError($"columns.csv: data_type is empty in row {i + 1}");
                }
            }

            result.AddInfo($"columns.csv: Validated {lines.Length - 1} column definitions");
        }
        catch (Exception ex)
        {
            result.AddError($"Error reading columns.csv: {ex.Message}");
        }
    }

    private async Task ValidateRelationsFileAsync(string profilePath, ValidationResult result)
    {
        var relationsFile = Path.Combine(profilePath, "relations.csv");

        if (!File.Exists(relationsFile))
        {
            result.AddError("relations.csv file does not exist");
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(relationsFile);
            if (lines.Length == 0)
            {
                result.AddError("relations.csv file is empty");
                return;
            }

            var header = lines[0];
            var expectedColumns = new[] { "source_table", "source_column", "target_table", "target_column" };
            var headerColumns = header.Split(',').Select(c => c.Trim()).ToArray();

            foreach (var expectedCol in expectedColumns)
            {
                if (!headerColumns.Contains(expectedCol))
                {
                    result.AddError($"relations.csv: Required column not found: {expectedCol}");
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length != expectedColumns.Length)
                {
                    result.AddWarning($"relations.csv: Incorrect number of columns in row {i + 1} (expected: {expectedColumns.Length}, actual: {columns.Length})");
                }

                for (int j = 0; j < expectedColumns.Length; j++)
                {
                    if (string.IsNullOrWhiteSpace(columns.ElementAtOrDefault(j)))
                    {
                        result.AddError($"relations.csv: {expectedColumns[j]} is empty in row {i + 1}");
                    }
                }
            }

            result.AddInfo($"relations.csv: Validated {lines.Length - 1} relation definitions");
        }
        catch (Exception ex)
        {
            result.AddError($"Error reading relations.csv: {ex.Message}");
        }
    }

    private async Task ValidateConsistencyAsync(string profilePath, ValidationResult result)
    {
        try
        {
            var tablesFile = Path.Combine(profilePath, "tables.csv");
            var columnsFile = Path.Combine(profilePath, "columns.csv");
            var relationsFile = Path.Combine(profilePath, "relations.csv");

            if (!File.Exists(tablesFile) || !File.Exists(columnsFile) || !File.Exists(relationsFile))
            {
                return;
            }

            var tableNames = new HashSet<string>();
            var tableLines = await File.ReadAllLinesAsync(tablesFile);
            for (int i = 1; i < tableLines.Length; i++)
            {
                var line = tableLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length >= 4 && !string.IsNullOrWhiteSpace(columns[3]))
                {
                    tableNames.Add(columns[3].Trim());
                }
            }

            var columnTableNames = new HashSet<string>();
            var columnLines = await File.ReadAllLinesAsync(columnsFile);
            for (int i = 1; i < columnLines.Length; i++)
            {
                var line = columnLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length >= 1 && !string.IsNullOrWhiteSpace(columns[0]))
                {
                    var tableName = columns[0].Trim();
                    columnTableNames.Add(tableName);

                    if (!tableNames.Contains(tableName))
                    {
                        result.AddWarning($"Consistency check: Table '{tableName}' referenced in columns.csv is not defined in tables.csv");
                    }
                }
            }

            var relationLines = await File.ReadAllLinesAsync(relationsFile);
            for (int i = 1; i < relationLines.Length; i++)
            {
                var line = relationLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length >= 4)
                {
                    var sourceTable = columns[0]?.Trim();
                    var targetTable = columns[2]?.Trim();

                    if (!string.IsNullOrEmpty(sourceTable) && !tableNames.Contains(sourceTable))
                    {
                        result.AddWarning($"Consistency check: Source table '{sourceTable}' referenced in relations.csv is not defined in tables.csv");
                    }

                    if (!string.IsNullOrEmpty(targetTable) && !tableNames.Contains(targetTable))
                    {
                        result.AddWarning($"Consistency check: Target table '{targetTable}' referenced in relations.csv is not defined in tables.csv");
                    }
                }
            }

            result.AddInfo($"Consistency check completed: {tableNames.Count} table definitions, {columnTableNames.Count} tables with columns");
        }
        catch (Exception ex)
        {
            result.AddError($"Consistency check error: {ex.Message}");
        }
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

public class ValidationResult
{
    public string ProfileName { get; set; } = string.Empty;
    public List<ValidationMessage> Messages { get; set; } = new();

    public bool HasErrors => Messages.Any(m => m.Type == MessageType.Error);
    public bool HasWarnings => Messages.Any(m => m.Type == MessageType.Warning);

    public void AddError(string message) => Messages.Add(new ValidationMessage(MessageType.Error, message));
    public void AddWarning(string message) => Messages.Add(new ValidationMessage(MessageType.Warning, message));
    public void AddInfo(string message) => Messages.Add(new ValidationMessage(MessageType.Info, message));

    public string GetSummary()
    {
        var errors = Messages.Count(m => m.Type == MessageType.Error);
        var warnings = Messages.Count(m => m.Type == MessageType.Warning);
        var infos = Messages.Count(m => m.Type == MessageType.Info);

        return $"Validation completed - Errors: {errors}, Warnings: {warnings}, Info: {infos}";
    }

    public string GetDetailedReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Validation Results for Profile '{ProfileName}' ===");
        sb.AppendLine(GetSummary());
        sb.AppendLine();

        foreach (var group in Messages.GroupBy(m => m.Type))
        {
            sb.AppendLine($"[{group.Key}]");
            var messagesToShow = group.Key == MessageType.Error
                ? group.AsEnumerable()
                : group.OrderBy(m => m.Message);

            foreach (var message in messagesToShow)
            {
                sb.AppendLine($"  {message.Message}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class ValidationMessage
{
    public MessageType Type { get; set; }
    public string Message { get; set; }

    public ValidationMessage(MessageType type, string message)
    {
        Type = type;
        Message = message;
    }
}

public enum MessageType
{
    Error,
    Warning,
    Info
}