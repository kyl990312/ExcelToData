namespace CodeGenerator;

/// <summary>
/// ì½”ë“œ ?ì„±ê¸??¤ì • ?´ë˜??
/// ?¸ë??ì„œ ?˜ì • ê°€?¥í•œ JSON ?•íƒœë¡??€?¥ë¨
/// </summary>
public class CodeGenerateConfig
{
    // ============================================
    // ?´ë˜??ì½”ë“œ ?ì„± ?¤ì •
    // ============================================
    
    /// <summary>
    /// ë°”ì´?ˆë¦¬ ?Œì¼ ?”ë ‰? ë¦¬ ê²½ë¡œ
    /// </summary>
    public string BinaryDirectory { get; set; } = "Binary";

    /// <summary>
    /// ?´ë˜??ì½”ë“œ ?ì„± ?”ë ‰? ë¦¬ ê²½ë¡œ
    /// </summary>
    public string ClassOutputDirectory { get; set; } = "Generated/Classes";

    /// <summary>
    /// ?Œì´ë¸?ê´€ë¦¬ì ?´ë˜???ì„± ?”ë ‰? ë¦¬ ê²½ë¡œ
    /// </summary>
    public string ManagerOutputDirectory { get; set; } = "Generated/Managers";

    /// <summary>
    /// ?´ë˜???¤ì„?¤í˜?´ìŠ¤
    /// </summary>
    public string ClassNamespace { get; set; } = "Generated.Data";

    /// <summary>
    /// ?Œì´ë¸?ê´€ë¦¬ì ?¤ì„?¤í˜?´ìŠ¤
    /// </summary>
    public string ManagerNamespace { get; set; } = "Generated.Managers";

    /// <summary>
    /// ë°”ì´?ˆë¦¬ ?Œì¼ ?•ì¥???¨í„´
    /// </summary>
    public string ReadFileExtension { get; set; } = "*.bin";

    // ============================================
    // Enum ì½”ë“œ ?ì„± ?¤ì •
    // ============================================
    
    /// <summary>
    /// Enum ì½”ë“œ ?ì„± ?¬ë?
    /// </summary>
    public bool IsGenerateEnumCode { get; set; } = true;

    /// <summary>
    /// Enum ë°”ì´?ˆë¦¬ ?Œì¼ ê²½ë¡œ (EnumCache ë¡œë“œ??
    /// </summary>
    public string EnumBinaryPath { get; set; } = "Binary/EnumDB.bin";
    
    /// <summary>
    /// Enum ì½”ë“œ ì¶œë ¥ ?”ë ‰? ë¦¬
    /// </summary>
    public string EnumOutputDirectory { get; set; } = "Generated/Enums";
    
    /// <summary>
    /// ì½”ë“œ ?Œì¼ ?•ì¥??
    /// </summary>
    public string CodeExtension { get; set; } = ".cs";

    public static CodeGenerateConfig GetDefault()
    {
        return new CodeGenerateConfig()
        {
            BinaryDirectory = "Binary",
            ClassOutputDirectory = "Generated/Classes",
            ManagerOutputDirectory = "Generated/Managers",
            ClassNamespace = "Generated.Data",
            ManagerNamespace = "Generated.Managers",
            ReadFileExtension = "*.bin",
            IsGenerateEnumCode = true,
            EnumBinaryPath = "Binary/EnumDB.bin",
            EnumOutputDirectory = "Generated/Enums",
            CodeExtension = ".cs",
        };
    }
}
