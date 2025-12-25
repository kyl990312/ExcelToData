namespace Common;

/// <summary>
/// 시스템 전역에서 공유하는 경로 정의
/// - Config 파일 경로
/// - EnumDB 경로
/// </summary>
public static class SystemPath
{
    // ============================================
    // Config 파일 경로
    // ============================================
    
    /// <summary>
    /// Config 디렉토리
    /// </summary>
    public const string ConfigDirectory = "Config";
    
    /// <summary>
    /// EnumGenerator Config 파일 경로
    /// </summary>
    public const string EnumGeneratorConfigPath = "Config/EnumGeneratorConfig.json";
    
    /// <summary>
    /// CodeGenerator Config 파일 경로
    /// </summary>
    public const string CodeGeneratorConfigPath = "Config/CodeGenerateConfig.json";
    
    /// <summary>
    /// ExcelToData Binary Config 파일 경로
    /// </summary>
    public const string BinaryConfigPath = "Config/BinaryConfig.json";
    
    /// <summary>
    /// ExcelToData Json Config 파일 경로
    /// </summary>
    public const string JsonConfigPath = "Config/JsonConfig.json";
    
    // ============================================
    // EnumDB 경로 (시스템 전역 공유)
    // ============================================
    
    /// <summary>
    /// EnumDB 바이너리 상대 경로
    /// </summary>
    public const string EnumBinaryPath = "Binary/EnumDB.bin";
}