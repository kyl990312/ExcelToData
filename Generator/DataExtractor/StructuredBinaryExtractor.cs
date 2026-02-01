using ClosedXML.Excel;
using ExcelParser;
using TableAutomation.Common.Utils;
using BinaryWriter = BinaryParser.BinaryWriter;

namespace Generator.DataExtractor;

/// <summary>
/// Excel ?°ì´?°ë? êµ¬ì¡°?”ëœ ë°”ì´?ˆë¦¬ ?•ì‹?¼ë¡œ ì¶”ì¶œ?˜ëŠ” ?´ë˜??/// Header: ì¹¼ëŸ¼ ê°?ˆ˜, ?°ì´??ê°?ˆ˜, ì¹¼ëŸ¼ ë©”í??°ì´??/// Data: ì¹¼ëŸ¼ë³„ë¡œ ?°ì´???€??/// </summary>
public class StructuredBinaryExtractor : DataExtractorBase
{
    private readonly BinaryWriter _binaryWriter;

    public StructuredBinaryExtractor(string directory) : base(directory, "bin")
    {
        _binaryWriter = new BinaryWriter();
    }

    protected override List<CustomDataTable> GetDataTablesByFile(string filePath)
    {
        List<CustomDataTable> tables = new List<CustomDataTable>();
        using (var workbook = new XLWorkbook(filePath))
        {
            if (workbook.Worksheets.Count == 0)
            {
                Logger.Instance.Error($"Fail to load. (file path : {filePath})");
                return tables;
            }
            Logger.Instance.Log($"Success to load. (file path : {filePath})");
            
            foreach (var workSheet in workbook.Worksheets)
            {
                Logger.Instance.Log($"Start Convert Worksheet to DataTable({workSheet.Name})");
                
                // CustomDataTable ?¬ìš©
                var table = TableHelper.GetTable(workSheet);
                
#if DEBUG
                // ?¤ë” ?•ë³´ ë¡œê·¸ ì¶œë ¥
                Logger.Instance.Log($"[StructuredBinaryExtractor] Table: {table.Name}, ColumnCount: {table.ColumnCount}, RowCount: {table.RowCount}", ConsoleColor.Cyan);
                foreach (var header in table.Header.Headers)
                {
                    Logger.Instance.Log($"[StructuredBinaryExtractor]   - {header.name}: {header.typeString} (DataType: {header.dataType}, IsArray: {header.isArray})", ConsoleColor.Cyan);
                }
#endif
                
                tables.Add(table);
                Logger.Instance.Log($"Finish Convert Worksheet to DataTable({workSheet.Name})");
            }
        }
        return tables;
    }

    protected override void SaveData(CustomDataTable table, string path)
    {
        try
        {
            // ì¹¼ëŸ¼ ?€???•ë³´?€ ?°ì´?°ë? ?¨ê»˜ ?„ë‹¬?˜ì—¬ ?€??            _binaryWriter.WriteToFile(table.GetColumnTypes(), table.GetAllRowsData(), path);
            Logger.Instance.Log($"[StructuredBinaryExtractor] Save File StructuredBinary {path}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[StructuredBinaryExtractor] Error saving structured binary file {path}: {ex.Message}");
            throw;
        }
    }
}
