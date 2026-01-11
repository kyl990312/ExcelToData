using ExcelParser;
using Generator.DataExtractor;
using GAuth;
using Common.Utils;

namespace EnumGenerator.DataExtractor;

public class BytesExtractorFromGSheet : StructuredBinaryExtractor
{
    public BytesExtractorFromGSheet(string directory) : base(directory)
    {
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
            // 헤더 정보 로그 출력
            Logger.Instance.Log($"[BytesExtractorFromGSheet] Table: {table.Name}, ColumnCount: {table.ColumnCount}, RowCount: {table.RowCount}", ConsoleColor.Cyan);
            foreach (var header in table.Header.Headers)
            {
                Logger.Instance.Log($"[BytesExtractorFromGSheet]   - {header.name}: {header.typeString} (DataType: {header.dataType}, IsArray: {header.isArray})", ConsoleColor.Cyan);
            }
#endif
        }
        return result;
    }
}