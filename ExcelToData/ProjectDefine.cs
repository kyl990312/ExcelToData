using TableAutomation.Common;

namespace ExcelToData;

/// <summary>
/// ?œìŠ¤???„ì²´?ì„œ ?¬ìš©?˜ëŠ” ?ìˆ˜ ?•ì˜
/// </summary>
public static class ProjectDefine
{
    // ?ˆë¡œ??êµ¬ì¡°?”ëœ ë°”ì´?ˆë¦¬ ?¤ì • ?Œì¼ ê²½ë¡œ
    public const string StructuredBinaryConfigPath = "Config/StructuredBinaryConfig.json";
    
    // ?Œì¼ ?•ì¥??    public const string ExcelExtension = "*.xlsm";
    public const string JsonExtension = ".json";
    public const string BinaryExtension = ".bin";
    public const string StructuredBinaryExtension = ".sbin";
    
    // ?”ë ‰? ë¦¬ ê¸°ë³¸ê°?    public const string DefaultDataDirectory = "Data";
    public const string DefaultOutputDirectory = "Output";
    public const string DefaultBinaryDirectory = "Binary";
    public const string DefaultStructuredBinaryDirectory = "StructuredBinary";
    public const string DefaultJsonDirectory = "Json";
    
    // ê²€???¨í„´ (ê¸°ì¡´ ?¸í™˜??? ì?)
    public static string[] SearchPatterns = { "*.xlsm", "*.xlsx" };
    public static string[] SearchPatternsBin = { "*.bin" };
}
