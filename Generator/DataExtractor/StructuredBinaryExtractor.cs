using ClosedXML.Excel;
using ExcelParser;
using Common.Utils;
using BinaryWriter = BinaryParser.BinaryWriter;

namespace Generator.DataExtractor;

/// <summary>
/// Excel 데이터를 구조화된 바이너리 형식으로 추출하는 클래스
/// Header: 칼럼 갯수, 데이터 갯수, 칼럼 메타데이터
/// Data: 칼럼별로 데이터 저장
/// </summary>
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
                
                // CustomDataTable 사용
                var table = TableHelper.GetTable(workSheet);
                
#if DEBUG
                // 헤더 정보 로그 출력
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
            // 칼럼 타입 정보와 데이터를 함께 전달하여 저장
            _binaryWriter.WriteToFile(table.GetColumnTypes(), table.GetAllRowsData(), path);
            Logger.Instance.Log($"[StructuredBinaryExtractor] Save File StructuredBinary {path}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[StructuredBinaryExtractor] Error saving structured binary file {path}: {ex.Message}");
            throw;
        }
    }
}
