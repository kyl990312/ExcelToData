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

    protected override void SaveData(CustomDataTable table, string path)
    {
        try
        {
            // 구조화된 바이너리 형식으로 저장
            _binaryWriter.WriteToFile(table.GetAllRowsData(), path);
            Logger.Instance.Log($"[StructuredBinaryExtractor] Save File StructuredBinary {path}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[StructuredBinaryExtractor] Error saving structured binary file {path}: {ex.Message}");
            throw;
        }
    }
}
