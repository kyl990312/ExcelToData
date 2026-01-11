using System.Text;
using Common.Utils;

namespace BinaryParser;

/// <summary>
/// 바이너리 파일의 스키마(헤더) 정보를 읽어오는 클래스
/// </summary>
public class BinarySchema
{
    /// <summary>
    /// 칼럼 이름과 타입 정보를 담는 Dictionary
    /// Key: 칼럼 이름, Value: 타입 문자열
    /// </summary>
    public Dictionary<string, string> Columns { get; private set; } = new();

    /// <summary>
    /// 데이터 행 개수
    /// </summary>
    public int RowCount { get; private set; }

    /// <summary>
    /// 칼럼 개수
    /// </summary>
    public int ColumnCount => Columns.Count;

    /// <summary>
    /// 바이트 배열에서 스키마 정보를 읽어옵니다
    /// </summary>
    /// <param name="binaryData">바이너리 데이터</param>
    /// <returns>스키마 정보가 담긴 BinarySchema 인스턴스</returns>
    public static BinarySchema ReadFromBytes(byte[] binaryData)
    {
        var schema = new BinarySchema();

        try
        {
            using var memoryStream = new MemoryStream(binaryData);
            using var reader = new System.IO.BinaryReader(memoryStream, Encoding.UTF8);

            schema.ReadHeader(reader);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinarySchema] Error reading schema from bytes: {ex.Message}");
        }

        return schema;
    }

    private void ReadHeader(System.IO.BinaryReader reader)
    {
        // 1. 칼럼 갯수
        int columnCount = reader.ReadInt32();

        // 2. 데이터 갯수 (행 갯수)
        RowCount = reader.ReadInt32();

        // 3. 각 칼럼의 메타데이터
        Columns = new Dictionary<string, string>();
        for (int i = 0; i < columnCount; i++)
        {
            // 칼럼 이름
            string columnName = reader.ReadString();

            // 칼럼 타입
            string columnType = reader.ReadString();

            Columns[columnName] = columnType;
        }

#if DEBUG
        // 스키마 정보 로그 출력
        Logger.Instance.Log($"[BinarySchema] ColumnCount: {columnCount}, RowCount: {RowCount}", ConsoleColor.Cyan);
        foreach (var column in Columns)
        {
            Logger.Instance.Log($"[BinarySchema]   - {column.Key}: {column.Value}", ConsoleColor.Cyan);
        }
#endif
    }

    /// <summary>
    /// 칼럼 이름 목록을 반환합니다
    /// </summary>
    public IEnumerable<string> GetColumnNames() => Columns.Keys;

    /// <summary>
    /// 칼럼 타입 목록을 반환합니다
    /// </summary>
    public IEnumerable<string> GetColumnTypes() => Columns.Values;

    /// <summary>
    /// 특정 칼럼의 타입을 반환합니다
    /// </summary>
    /// <param name="columnName">칼럼 이름</param>
    /// <returns>타입 문자열, 없으면 null</returns>
    public string? GetColumnType(string columnName)
    {
        return Columns.TryGetValue(columnName, out var type) ? type : null;
    }

    /// <summary>
    /// 특정 칼럼이 존재하는지 확인합니다
    /// </summary>
    /// <param name="columnName">칼럼 이름</param>
    /// <returns>존재 여부</returns>
    public bool HasColumn(string columnName) => Columns.ContainsKey(columnName);
}
