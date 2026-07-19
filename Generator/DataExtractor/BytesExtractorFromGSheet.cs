using ExcelParser;
using Generator.DataExtractor;
using GAuth;
using TableAutomation.Common.Utils;

namespace EnumGenerator.DataExtractor;

public class BytesExtractorFromGSheet : StructuredBinaryExtractor
{
    private readonly string? _targetFileName;

    public BytesExtractorFromGSheet(string directory, string? targetFileName = null) : base(directory)
    {
        _targetFileName = targetFileName;
    }

    protected override List<CustomDataTable> GetDataTablesByFile(string filePath)
    {
        var tables = GAuthService.GetTableFromGSheet(filePath);
        List<CustomDataTable> result;
        try
        {
            result = tables.Select(x => TableHelper.GetTable(x.TableName, x.Data)).ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new List<CustomDataTable>();
        }
        foreach (var table in result)
        {
            var nameStrs = table.Name.Split('_');
            table.Name = table.Name.Replace(nameStrs[0]+"_", "");

#if DEBUG
            // ��� ���� �α� ���
            Logger.Instance.Log($"[BytesExtractorFromGSheet] Table: {table.Name}, ColumnCount: {table.ColumnCount}, RowCount: {table.RowCount}", ConsoleColor.Cyan);
            foreach (var header in table.Header.Headers)
            {
                Logger.Instance.Log($"[BytesExtractorFromGSheet]   - {header.name}: {header.typeString} (DataType: {header.dataType}, IsArray: {header.isArray})", ConsoleColor.Cyan);
            }
#endif
        }

        if (!string.IsNullOrEmpty(_targetFileName))
        {
            var before = result.Count;
            result = result
                .Where(t => t.Name.Equals(_targetFileName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (result.Count == 0)
                Logger.Instance.Log(
                    $"[BytesExtractorFromGSheet] Warning: No table matched '{_targetFileName}' (searched {before} tables). 0 files will be written.",
                    ConsoleColor.Yellow);
        }

        return result;
    }
}