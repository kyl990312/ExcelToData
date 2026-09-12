namespace TableAutomationTool.EnumGenerator;

public class EnumGeneratorConfig
{
    // ============================================
    // 로컬 파일 설정
    // ============================================
    
    /// <summary>
    /// 로컬 파일에서 로드할지 여부
    /// </summary>
    public bool IsLoadFromLocal { get; set; } = true;
    
    /// <summary>
    /// 로컬 Enum Excel 파일 경로
    /// </summary>
    public string EnumFilePath { get; set; } = "SystemEnum.xlsx";
    
    // ============================================
    // 구글 시트 설정
    // ============================================
    
    /// <summary>
    /// 구글 시트에서 로드할지 여부
    /// </summary>
    public bool IsLoadFromGSheet { get; set; } = false;
    
    /// <summary>
    /// 구글 드라이브 폴더 링크 (Enum 시트가 포함된 폴더)
    /// </summary>
    public string SrcGDriveLink { get; set; } = string.Empty;
    
    /// <summary>
    /// 구글 시트에서 Enum 시트를 식별하기 위한 와일드카드 패턴
    /// </summary>
    public string[] EnumSheetPatterns { get; set; } = ["*Enum*", "*enum*"];
    
    // ============================================
    // 바이너리 출력 설정
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
