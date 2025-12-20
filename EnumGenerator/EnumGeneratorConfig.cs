namespace EnumGenerator;

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
    /// 구글 시트에서 Enum 시트를 식별하기 위한 파일명 패턴
    /// </summary>
    public string[] EnumSheetPatterns { get; set; } = ["*Enum*", "*enum*"];
    
    // ============================================
    // 코드 출력 설정
    // ============================================
    
    /// <summary>
    /// Enum 코드 출력 디렉토리
    /// </summary>
    public string DstDirectory { get; set; } = "Enums/";
    
    /// <summary>
    /// 코드 파일 확장자
    /// </summary>
    public string CodeExtension { get; set; } = ".cs";
    
    // ============================================
    // 바이너리 출력 설정
    // ============================================
    
    /// <summary>
    /// EnumDB 바이너리 생성 여부
    /// </summary>
    public bool IsGenerateEnumBinary { get; set; } = true;
    
    /// <summary>
    /// EnumDB 바이너리 출력 디렉토리
    /// </summary>
    public string EnumBinaryDirectory { get; set; } = "Binary/";
    
    /// <summary>
    /// EnumDB 바이너리 파일명
    /// </summary>
    public string EnumBinaryFileName { get; set; } = "EnumDB.bin";
    
    /// <summary>
    /// EnumDB 바이너리 전체 경로 (디렉토리 + 파일명)
    /// </summary>
    public string EnumDBPath => Path.Combine(EnumBinaryDirectory, EnumBinaryFileName);

    public static EnumGeneratorConfig GetDefault()
    {
        return new EnumGeneratorConfig()
        {
            IsLoadFromLocal = true,
            EnumFilePath = "SystemEnum.xlsx",
            IsLoadFromGSheet = false,
            SrcGDriveLink = string.Empty,
            EnumSheetPatterns = ["*Enum*", "*enum*"],
            DstDirectory = "Enums/",
            CodeExtension = ".cs",
            IsGenerateEnumBinary = true,
            EnumBinaryDirectory = "Binary/",
            EnumBinaryFileName = "EnumDB.bin",
        };
    }
}
