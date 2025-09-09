using ExcelParser;
using Generator.DataExtractor;

namespace EnumGenerator.DataExtractor;

public class BytesExtractorFromGSheet : BytesExtractor
{
    public BytesExtractorFromGSheet(string directory) : base(directory)
    {
    }

    protected override List<CustomDataTable> GetDataTablesByFile(string filePath)
    {
        return base.GetDataTablesByFile(filePath);
    }
}