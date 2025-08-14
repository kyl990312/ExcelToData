namespace ExcelToData;

/// <summary>
/// 시스템 전체에서 사용되는 상수 정의
/// </summary>
public static class SystemDefine
{
    // 기존 설정 파일 경로
    public const string BinaryConfigPath = "Config/BinaryConfig.json";
    public const string JsonConfigPath = "Config/JsonConfig.json";
    
    // 새로운 구조화된 바이너리 설정 파일 경로
    public const string StructuredBinaryConfigPath = "Config/StructuredBinaryConfig.json";
    
    // 파일 확장자
    public const string ExcelExtension = "*.xlsm";
    public const string JsonExtension = ".json";
    public const string BinaryExtension = ".bin";
    public const string StructuredBinaryExtension = ".sbin";
    
    // 디렉토리 기본값
    public const string DefaultDataDirectory = "Data";
    public const string DefaultOutputDirectory = "Output";
    public const string DefaultBinaryDirectory = "Binary";
    public const string DefaultStructuredBinaryDirectory = "StructuredBinary";
    public const string DefaultJsonDirectory = "Json";
    
    // 검색 패턴 (기존 호환성 유지)
    public static string[] SearchPatterns = { "*.xlsm", "*.xlsx" };
    public static string[] SearchPatternsBin = { "*.bin" };
}
