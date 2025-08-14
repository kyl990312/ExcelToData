using BinaryParser;
using Common.Utils;

namespace BinaryParser.Examples;

/// <summary>
/// BinaryParser 라이브러리 사용 예제
/// </summary>
public class BinaryParserUsageExample
{
    /// <summary>
    /// 기본 사용법 예제
    /// </summary>
    public static void BasicUsageExample()
    {
        Logger.Instance.Log("=== BinaryParser Basic Usage Example ===");
        
        // 1. 샘플 Dictionary 데이터 생성
        var sampleData = CreateSampleData();
        
        // 2. 구조화된 바이너리 파일로 저장
        var binaryFilePath = "example_data.sbin";
        BinaryHelper.SaveToFile(sampleData, binaryFilePath);
        Logger.Instance.Log($"Saved to binary file: {binaryFilePath}");
        
        // 3. 바이너리 파일에서 읽기
        var loadedData = BinaryHelper.LoadFromFile(binaryFilePath);
        Logger.Instance.Log($"Loaded from binary file - Rows: {loadedData.Count}");
        
        // 4. 파일 정보 출력
        var fileInfo = BinaryHelper.GetFileInfo(binaryFilePath);
        Logger.Instance.Log($"File Info:\n{fileInfo}");
        
        // 5. JSON으로 변환
        var jsonFilePath = "example_data.json";
        BinaryHelper.ConvertToJson(binaryFilePath, jsonFilePath);
        Logger.Instance.Log($"Converted to JSON: {jsonFilePath}");
        
        // 6. JSON에서 다시 바이너리로 변환
        var newBinaryFilePath = "example_data_from_json.sbin";
        BinaryHelper.ConvertFromJson(jsonFilePath, newBinaryFilePath);
        Logger.Instance.Log($"Converted back to binary: {newBinaryFilePath}");
    }

    /// <summary>
    /// 고급 사용법 예제 - 직접 Reader/Writer 사용
    /// </summary>
    public static void AdvancedUsageExample()
    {
        Logger.Instance.Log("=== BinaryParser Advanced Usage Example ===");
        
        var complexData = CreateComplexData();
        
        // 직접 Writer 사용
        var writer = new BinaryWriter();
        var binaryData = writer.WriteToBytes(complexData);
        Logger.Instance.Log($"Binary data size: {binaryData.Length} bytes");
        
        // 직접 Reader 사용
        var reader = new BinaryReader();
        var loadedData = reader.ReadFromBytes(binaryData);
        
        // 데이터 검증
        ValidateData(complexData, loadedData);
    }

    /// <summary>
    /// 성능 테스트 예제
    /// </summary>
    public static void PerformanceTestExample()
    {
        Logger.Instance.Log("=== BinaryParser Performance Test ===");
        
        var largeData = CreateLargeData(10000); // 10,000 rows
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // 저장 성능 측정
        var filePath = "performance_test.sbin";
        BinaryHelper.SaveToFile(largeData, filePath);
        stopwatch.Stop();
        
        var fileSize = new FileInfo(filePath).Length;
        Logger.Instance.Log($"Write Performance: {largeData.Count} rows in {stopwatch.ElapsedMilliseconds}ms");
        Logger.Instance.Log($"File size: {fileSize / 1024.0:F2} KB");
        
        // 읽기 성능 측정
        stopwatch.Restart();
        var loadedData = BinaryHelper.LoadFromFile(filePath);
        stopwatch.Stop();
        
        Logger.Instance.Log($"Read Performance: {loadedData.Count} rows in {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// JSON 호환성 테스트
    /// </summary>
    public static void JsonCompatibilityExample()
    {
        Logger.Instance.Log("=== BinaryParser JSON Compatibility Example ===");
        
        var originalData = CreateSampleData();
        
        // JSON으로 변환
        var jsonString = BinaryHelper.ConvertToJSON(originalData);
        Logger.Instance.Log($"JSON size: {jsonString.Length} characters");
        
        // JSON에서 다시 Dictionary로 변환
        var fromJsonData = BinaryHelper.ConvertFromJSON(jsonString);
        
        // 데이터 검증
        ValidateData(originalData, fromJsonData);
        Logger.Instance.Log("JSON compatibility test passed!");
    }

    /// <summary>
    /// 데이터 무결성 검증 예제
    /// </summary>
    public static void DataValidationExample()
    {
        Logger.Instance.Log("=== BinaryParser Data Validation Example ===");
        
        var testData = CreateSampleData();
        
        // 데이터 검증
        var validationResult = BinaryHelper.ValidateData(testData);
        Logger.Instance.Log($"Validation Result: {validationResult.IsValid}");
        Logger.Instance.Log($"Message: {validationResult.Message}");
        
        if (!string.IsNullOrEmpty(validationResult.ErrorMessage))
        {
            Logger.Instance.Error($"Error: {validationResult.ErrorMessage}");
        }
    }

    /// <summary>
    /// 바이너리 라운드트립 테스트
    /// </summary>
    public static void RoundTripTestExample()
    {
        Logger.Instance.Log("=== BinaryParser Round Trip Test ===");
        
        var originalData = CreateComplexData();
        
        // Binary로 변환 후 다시 읽기
        var binaryData = BinaryHelper.ConvertToBytes(originalData);
        var restoredData = BinaryHelper.ConvertFromBytes(binaryData);
        
        // 데이터 동일성 확인
        bool areEqual = BinaryHelper.AreDataEqual(originalData, restoredData);
        Logger.Instance.Log($"Round trip test: {(areEqual ? "PASSED" : "FAILED")}");
        
        if (!areEqual)
        {
            Logger.Instance.Error("Data integrity compromised during round trip!");
        }
    }

    private static List<Dictionary<string, object>> CreateSampleData()
    {
        var data = new List<Dictionary<string, object>>();
        
        for (int i = 1; i <= 5; i++)
        {
            var row = new Dictionary<string, object>
            {
                ["ID"] = i,
                ["Name"] = $"User{i}",
                ["Score"] = 85.5 + i * 2.5,
                ["IsActive"] = i % 2 == 1,
                ["Tags"] = new string[] { $"tag{i}", $"category{i % 3}" },
                ["CreatedDate"] = DateTime.Now.AddDays(-i)
            };
            data.Add(row);
        }
        
        return data;
    }

    private static List<Dictionary<string, object>> CreateComplexData()
    {
        var data = new List<Dictionary<string, object>>();
        
        // 다양한 타입의 데이터
        var row1 = new Dictionary<string, object>
        {
            ["SmallInt"] = (short)100,
            ["RegularInt"] = 1000000,
            ["BigInt"] = 9999999999L,
            ["FloatValue"] = 3.14f,
            ["DoubleValue"] = 2.718281828,
            ["Text"] = "Hello, BinaryParser! 🚀",
            ["Flag"] = true,
            ["IntArray"] = new int[] { 1, 2, 3, 4, 5 },
            ["StringArray"] = new string[] { "apple", "banana", "cherry" }
        };
        data.Add(row1);
        
        // null 값 테스트
        var row2 = new Dictionary<string, object>
        {
            ["SmallInt"] = (short)0,
            ["RegularInt"] = 0,
            ["BigInt"] = 0L,
            ["FloatValue"] = 0.0f,
            ["DoubleValue"] = 0.0,
            ["Text"] = null,
            ["Flag"] = false,
            ["IntArray"] = new int[0],
            ["StringArray"] = null
        };
        data.Add(row2);
        
        return data;
    }

    private static List<Dictionary<string, object>> CreateLargeData(int rowCount)
    {
        var data = new List<Dictionary<string, object>>();
        var random = new Random(42); // 재현 가능한 결과를 위한 시드
        
        for (int i = 0; i < rowCount; i++)
        {
            var row = new Dictionary<string, object>
            {
                ["ID"] = i,
                ["Value1"] = random.NextDouble() * 1000,
                ["Value2"] = $"Data_{i}_{random.Next(1000)}",
                ["Flag"] = random.Next(2) == 1,
                ["Category"] = random.Next(5),
                ["Score"] = random.Next(100)
            };
            data.Add(row);
        }
        
        return data;
    }

    private static void ValidateData(List<Dictionary<string, object>> original, List<Dictionary<string, object>> loaded)
    {
        Logger.Instance.Log("Validating data integrity...");
        
        if (original.Count != loaded.Count)
        {
            Logger.Instance.Error($"Row count mismatch: {original.Count} vs {loaded.Count}");
            return;
        }
        
        for (int i = 0; i < Math.Min(original.Count, 3); i++) // 처음 3행만 검증
        {
            var origRow = original[i];
            var loadRow = loaded[i];
            
            foreach (var kvp in origRow)
            {
                if (!loadRow.ContainsKey(kvp.Key))
                {
                    Logger.Instance.Warning($"Column {kvp.Key} missing in loaded data at row {i}");
                    continue;
                }
                
                if (!AreValuesEqual(kvp.Value, loadRow[kvp.Key]))
                {
                    Logger.Instance.Warning($"Value mismatch at row {i}, column {kvp.Key}: {kvp.Value} vs {loadRow[kvp.Key]}");
                }
            }
        }
        
        Logger.Instance.Log("Data validation completed successfully!");
    }

    private static bool AreValuesEqual(object original, object loaded)
    {
        if (original == null && loaded == null) return true;
        if (original == null || loaded == null) return false;
        
        if (original is Array origArray && loaded is Array loadArray)
        {
            if (origArray.Length != loadArray.Length) return false;
            for (int i = 0; i < origArray.Length; i++)
            {
                if (!Equals(origArray.GetValue(i), loadArray.GetValue(i)))
                    return false;
            }
            return true;
        }
        
        return Equals(original, loaded);
    }
}
