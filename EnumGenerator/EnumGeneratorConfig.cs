namespace EnumGenerator;

public class EnumGeneratorConfig
{
    // ============================================
    // ë¡œì»¬ ?Œì¼ ?¤ì •
    // ============================================
    
    /// <summary>
    /// ë¡œì»¬ ?Œì¼?ì„œ ë¡œë“œ? ì? ?¬ë?
    /// </summary>
    public bool IsLoadFromLocal { get; set; } = true;
    
    /// <summary>
    /// ë¡œì»¬ Enum Excel ?Œì¼ ê²½ë¡œ
    /// </summary>
    public string EnumFilePath { get; set; } = "SystemEnum.xlsx";
    
    // ============================================
    // êµ¬ê? ?œíŠ¸ ?¤ì •
    // ============================================
    
    /// <summary>
    /// êµ¬ê? ?œíŠ¸?ì„œ ë¡œë“œ? ì? ?¬ë?
    /// </summary>
    public bool IsLoadFromGSheet { get; set; } = false;
    
    /// <summary>
    /// êµ¬ê? ?œë¼?´ë¸Œ ?´ë” ë§í¬ (Enum ?œíŠ¸ê°€ ?¬í•¨???´ë”)
    /// </summary>
    public string SrcGDriveLink { get; set; } = string.Empty;
    
    /// <summary>
    /// êµ¬ê? ?œíŠ¸?ì„œ Enum ?œíŠ¸ë¥??ë³„?˜ê¸° ?„í•œ ?Œì¼ëª??¨í„´
    /// </summary>
    public string[] EnumSheetPatterns { get; set; } = ["*Enum*", "*enum*"];
    
    // ============================================
    // ë°”ì´?ˆë¦¬ ì¶œë ¥ ?¤ì •
    // ============================================
   

    public static EnumGeneratorConfig GetDefault()
    {
        return new EnumGeneratorConfig()
        {
            IsLoadFromLocal = true,
            EnumFilePath = "SystemEnum.xlsx",
            IsLoadFromGSheet = false,
            SrcGDriveLink = string.Empty,
            EnumSheetPatterns = ["*Enum*", "*enum*"],
        };
    }
}
