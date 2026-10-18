namespace TableAutomationTool.Shared;

/// <summary>
/// �ý��� �������� �����ϴ� ��� ����
/// - Config ���� ���
/// - EnumDB ���
/// </summary>
public static class SystemPath
{
    // ============================================
    // Config ���� ���
    // ============================================
    
    /// <summary>
    /// Config ���丮
    /// </summary>
    public const string ConfigDirectory = "Config";
    
    /// <summary>
    /// EnumGenerator Config ���� ���
    /// </summary>
    public const string EnumGeneratorConfigPath = "Config/EnumGeneratorConfig.json";
    
    /// <summary>
    /// CodeGenerator Config ���� ���
    /// </summary>
    public const string CodeGeneratorConfigPath = "Config/CodeGeneratorConfig.json";
    
    /// <summary>
    /// ExcelToData Binary Config ���� ���
    /// </summary>
    public const string BinaryConfigPath = "Config/ExcelToDataConfig.json";
    
    /// <summary>
    /// ExcelToData Json Config ���� ���
    /// </summary>
    public const string JsonConfigPath = "Config/JsonConfig.json";

    /// <summary>
    /// GAuth (Google API 인증) Config 파일 경로
    /// </summary>
    public const string GAuthConfigPath = "Config/GAuthConfig.json";

    /// <summary>
    /// Git (VCS diff) Config 파일 경로
    /// </summary>
    public const string GitConfigPath = "Config/GitConfig.json";

    // ============================================
    // EnumDB ��� (�ý��� ���� ����)
    // ============================================
    
    /// <summary>
    /// EnumDB ���̳ʸ� ��� ���
    /// </summary>
    public const string EnumBinaryPath = "Binary/EnumDB.bin";
}