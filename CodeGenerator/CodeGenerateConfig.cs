namespace CodeGenerator;

/// <summary>
/// 코드 생성기 설정 클래스
/// 외부에서 수정 가능한 JSON 형태로 저장됨
/// </summary>
public class CodeGenerateConfig
{
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

    public string ReadFileExtension { get; set; } = "*.bin";
    
    public string EnumFilePath { get; set; } = "Generated/SystemEnum.xmsl";
    public string EnumDriectory { get; set; } = "Generated/Enums";

    public static CodeGenerateConfig GetDefault()
    {
        return new CodeGenerateConfig()
        {
            BinaryDirectory = "Binary",
            ClassOutputDirectory = "Generated/Classes",
            ManagerOutputDirectory = "Generated/Managers",
            EnumFilePath = "Generated/Enums",
            ClassNamespace = "Generated.Data",
            ManagerNamespace = "Generated.Managers"
        };
    }
}
