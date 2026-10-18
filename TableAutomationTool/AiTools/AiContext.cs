using TableAutomationTool.CodeGenerator;
using TableAutomationTool.Common.Command;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.GAuth;
using TableAutomationTool.Shared;

namespace TableAutomationTool.AiTools;

public enum AiTableSourceKind
{
    Local,
    GSheet,
    Bin
}

/// <summary>
/// AI 툴이 쓰는 설정과 경로.
/// Config 의 상대 경로는 exe 폴더 기준이다(Directory.Build.props 참고). 사용자가 넘긴 경로만 호출 위치 기준으로 푼다.
/// </summary>
public sealed class AiContext
{
    public string BaseDirectory { get; }
    public string CallerDirectory { get; }
    public DataExtractConfig Extract { get; }
    public DataExtractConfig? Json { get; }
    public CodeGenerateConfig? CodeGen { get; }

    private AiContext(string baseDirectory, string callerDirectory, DataExtractConfig extract,
                      DataExtractConfig? json, CodeGenerateConfig? codeGen)
    {
        BaseDirectory = baseDirectory;
        CallerDirectory = callerDirectory;
        Extract = extract;
        Json = json;
        CodeGen = codeGen;
    }

    public static AiContext Load(string baseDirectory, string callerDirectory)
    {
        var extractPath = Path.Combine(baseDirectory, SystemPath.BinaryConfigPath);
        if (!FileLoader.LoadAndConvertJson(extractPath, out DataExtractConfig extract))
            throw new AiToolException("config_not_found", $"설정 파일을 읽을 수 없습니다: {extractPath}",
                "TableAutomationTool.exe init-config 로 기본 설정을 만든 뒤 경로를 채우세요.");

        // 아래 두 설정은 run 의 산출물 추적에만 쓰므로 없어도 된다.
        FileLoader.LoadAndConvertJson(Path.Combine(baseDirectory, SystemPath.JsonConfigPath), out DataExtractConfig json);
        FileLoader.LoadAndConvertJson(Path.Combine(baseDirectory, SystemPath.CodeGeneratorConfigPath), out CodeGenerateConfig codeGen);

        return new AiContext(baseDirectory, callerDirectory, extract, json, codeGen);
    }

    /// <summary>원본 xlsx 폴더 (ExcelToDataConfig.SrcDirectory)</summary>
    public string SrcDirectory => ResolveConfigPath(Extract.SrcDirectory);

    /// <summary>추출 결과 .bin 폴더 (ExcelToDataConfig.DstDirectory)</summary>
    public string BinDirectory => ResolveConfigPath(Extract.DstDirectory);

    /// <summary>
    /// 설정상 원본 소스. 추출 엔진은 로컬 → GSheet 순으로 둘 다 돌리므로, 둘 다 켜져 있으면 GSheet 결과가 남는다.
    /// </summary>
    public AiTableSourceKind DefaultSource
        => Extract.IsLoadFromGSheet ? AiTableSourceKind.GSheet : AiTableSourceKind.Local;

    public bool BothSourcesEnabled => Extract.IsLoadFromGSheet && Extract.IsLoadFromLocal;

    public string ResolveConfigPath(string path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(Path.Combine(BaseDirectory, path));

    public string ResolveCallerPath(string path)
        => Path.GetFullPath(Path.Combine(CallerDirectory, path));

    public AiTableSourceKind ResolveSource(string? option)
    {
        if (string.IsNullOrWhiteSpace(option))
            return DefaultSource;

        return option.ToLowerInvariant() switch
        {
            "local" or "xlsx" => AiTableSourceKind.Local,
            "gsheet" => AiTableSourceKind.GSheet,
            "bin" or "binary" => AiTableSourceKind.Bin,
            _ => throw new AiToolException("invalid_args", $"알 수 없는 소스: {option}", "--source local | gsheet | bin")
        };
    }

    public static string SourceName(AiTableSourceKind kind) => kind switch
    {
        AiTableSourceKind.Local => "local",
        AiTableSourceKind.GSheet => "gsheet",
        _ => "bin"
    };

    /// <summary>
    /// GSheet 호출 전 검사. 캐시된 토큰이 없으면 인증 라이브러리가 브라우저 로그인을 기다리며 멈추므로 미리 막는다.
    /// </summary>
    public void EnsureGSheetReady()
    {
        if (string.IsNullOrWhiteSpace(Extract.SrcGDriveLink))
            throw new AiToolException("gsheet_not_configured", "ExcelToDataConfig.json 의 SrcGDriveLink 가 비어 있습니다.",
                "사용자에게 TableEditor 설정 탭에서 GSheet 폴더 링크를 지정해 달라고 요청하세요.");

        if (!GAuthService.HasCachedToken())
            throw new AiToolException("gsheet_auth_required",
                $"캐시된 Google 토큰이 없습니다({Path.Combine(BaseDirectory, "token.json")}). 이 상태로 호출하면 브라우저 로그인을 기다리며 멈춥니다.",
                "사용자에게 TableEditor 설정 탭의 [Google 인증]을 한 번 실행해 달라고 요청하세요.");
    }
}
