using ExcelParser;
using Generator.DataExtractor;
using GAuth;

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
            result = tables.Select(x => TableHelper.GetTable(x)).ToList();
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
        }
        return result;
    }
}