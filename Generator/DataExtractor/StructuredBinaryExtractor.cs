using System.Data;
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

    protected override List<(DataTable table, string name)> ConvertToDataTable(string filePath)
    {
       List<(DataTable table, string name)> result = new ();
       using (var workbook = new XLWorkbook(filePath))
       {
           if (workbook.Worksheets.Count == 0)
           {
               Logger.Instance.Error($"Fail to load. (file path : {filePath})");
               return null;
           }
           Logger.Instance.Log($"Success to load. (file path : {filePath})");
           foreach (var workSheet in workbook.Worksheets)
           {
               Logger.Instance.Log($"Start Convert Worksheet ({workSheet.Name})");
               var table = TableHelper.GetTable(workSheet);
               result.Add((table,workSheet.Name));
           }
       }
       return result;
    }

    protected override void SaveData(DataTable table, string path)
    {
        try
        {
            // DataTable을 Dictionary 형태로 변환
            var data = TableHelper.ConvertDataTableToBinary(table);
            
            // 구조화된 바이너리 형식으로 저장
            _binaryWriter.WriteToFile(data, path);
            Logger.Instance.Log($"[StructuredBinaryExtractor] Save File StructuredBinary {path}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[StructuredBinaryExtractor] Error saving structured binary file {path}: {ex.Message}");
            throw;
        }
    }
}
