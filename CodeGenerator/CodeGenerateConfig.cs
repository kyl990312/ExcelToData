namespace CodeGenerator;

/// <summary>
/// 코드 생성기 설정 클래스
/// 외부에서 수정 가능한 JSON 형태로 저장됨
/// </summary>
public class CodeGenerateConfig
{
    // ============================================
    // 클래스 코드 생성 설정
    // ============================================
    
    /// <summary>
    /// 바이너리 파일 디렉토리 경로
    /// </summary>
    public string BinaryDirectory { get; set; } = "Binary";

    /// <summary>
    /// 클래스 코드 생성 디렉토리 경로
    /// </summary>
    public string ClassOutputDirectory { get; set; } = "Generated/Classes";

    /// <summary>
    /// 테이블 관리자 클래스 생성 디렉토리 경로
    /// </summary>
    public string ManagerOutputDirectory { get; set; } = "Generated/Managers";

    /// <summary>
    /// 클래스 네임스페이스
    /// </summary>
    public string ClassNamespace { get; set; } = "Generated.Data";

    /// <summary>
    /// 테이블 관리자 네임스페이스
    /// </summary>
    public string ManagerNamespace { get; set; } = "Generated.Managers";

    /// <summary>
    /// 바이너리 파일 확장자 패턴
    /// </summary>
    public string ReadFileExtension { get; set; } = "*.bin";

    // ============================================
    // Enum 코드 생성 설정
    // ============================================
    
    /// <summary>
    /// Enum 코드 생성 여부
    /// </summary>
    public bool IsGenerateEnumCode { get; set; } = true;

    /// <summary>
    /// Enum 바이너리 파일 경로 (EnumCache 로드용)
    /// </summary>
    public string EnumBinaryPath { get; set; } = "Binary/EnumDB.bin";
    
    /// <summary>
    /// Enum 코드 출력 디렉토리
    /// </summary>
    public string EnumOutputDirectory { get; set; } = "Generated/Enums";
    
    /// <summary>
    /// 코드 파일 확장자
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
