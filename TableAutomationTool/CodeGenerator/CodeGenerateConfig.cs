using TableAutomationTool.Shared;

namespace TableAutomationTool.CodeGenerator;

/// <summary>
/// 코드 생성기 설정 클래스
/// 외부에서 설정 파일을 JSON 형태로 저장
/// </summary>
public class CodeGenerateConfig
{
    // ============================================
    // 클래스 코드 생성 설정
    // ============================================

    /// <summary>
    /// 바이너리 파일 디렉터리 경로
    /// </summary>
    public string BinaryDirectory { get; set; } = "Binary";

    /// <summary>
    /// 클래스 코드 출력 디렉터리 경로
    /// </summary>
    public string ClassOutputDirectory { get; set; } = "Generated/Classes";

    /// <summary>
    /// 테이블 매니저 클래스 출력 디렉터리 경로
    /// </summary>
    public string ManagerOutputDirectory { get; set; } = "Generated/Managers";

    /// <summary>
    /// 클래스 네임스페이스
    /// </summary>
    public string ClassNamespace { get; set; } = "Generated.Data";

    /// <summary>
    /// 테이블 매니저 네임스페이스
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
    /// Enum 코드 출력 디렉터리
    /// </summary>
    public string EnumOutputDirectory { get; set; } = "Generated/Enums";

    /// <summary>
    /// 코드 파일 확장자
    /// </summary>
    public string CodeExtension { get; set; } = ".cs";

    /// <summary>
    /// EnumDB 바이너리인지 판정한다.
    ///
    /// EnumDB 는 테이블이 아니라 "사용자 정의 Enum 메타데이터"이고, 다른 단계가 내부적으로 읽어
    /// Enum 코드를 만드는 데만 쓴다. 그런데 기본 배치상 EnumDB.bin 이 테이블 바이너리 폴더
    /// (BinaryDirectory) 안에 같이 놓이기 때문에 "*.bin" 글롭에 딸려 들어온다.
    /// 그대로 두면 EnumDB 의 내부 스키마(sheetName/type/member/value)가 테이블 데이터 클래스로
    /// 생성되므로, 코드 생성 대상에서 명시적으로 제외해야 한다.
    /// </summary>
    public bool IsEnumBinary(string binaryFilePath)
    {
        if (string.IsNullOrWhiteSpace(binaryFilePath))
            return false;

        var name = Path.GetFileName(binaryFilePath);
        var configured = string.IsNullOrWhiteSpace(EnumBinaryPath) ? SystemPath.EnumBinaryPath : EnumBinaryPath;

        return string.Equals(name, Path.GetFileName(configured), StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, Path.GetFileName(SystemPath.EnumBinaryPath), StringComparison.OrdinalIgnoreCase);
    }

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
