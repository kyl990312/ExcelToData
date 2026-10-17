using System.IO;
using System.Text.Json;
using TableAutomationTool.CodeGenerator;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.EnumGenerator;
using TableAutomationTool.GAuth.Core;
using TableAutomationTool.Shared;
using TableAutomationTool.Vcs;

namespace TableEditor.Services;

/// <summary>
/// 각 엔진 Config(JSON)의 로드/저장을 담당한다.
/// 경로 규약은 CLI 인보커와 동일: parent(BaseDirectory) + SystemPath.*
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public string BaseDirectory { get; } =
        Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

    public string BinaryConfigFile => Path.Combine(BaseDirectory, SystemPath.BinaryConfigPath);
    public string JsonConfigFile => Path.Combine(BaseDirectory, SystemPath.JsonConfigPath);
    public string CodeGenConfigFile => Path.Combine(BaseDirectory, SystemPath.CodeGeneratorConfigPath);
    public string EnumConfigFile => Path.Combine(BaseDirectory, SystemPath.EnumGeneratorConfigPath);
    public string GAuthConfigFile => Path.Combine(BaseDirectory, SystemPath.GAuthConfigPath);
    public string GitConfigFile => Path.Combine(BaseDirectory, SystemPath.GitConfigPath);

    public DataExtractConfig Binary { get; private set; } = new();
    public DataExtractConfig Json { get; private set; } = new();
    public CodeGenerateConfig CodeGen { get; private set; } = CodeGenerateConfig.GetDefault();
    public EnumGeneratorConfig EnumCfg { get; private set; } = EnumGeneratorConfig.GetDefault();
    public UserCredentialConfig GAuthCfg { get; private set; } = NewGAuthDefault();
    public GitConfig Git { get; private set; } = GitConfig.GetDefault();

    public void LoadAll()
    {
        Binary = Load(BinaryConfigFile, () => new DataExtractConfig
        {
            SrcDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data"),
            DstDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output"),
        });
        Json = Load(JsonConfigFile, () => new DataExtractConfig());
        CodeGen = Load(CodeGenConfigFile, CodeGenerateConfig.GetDefault);
        EnumCfg = Load(EnumConfigFile, EnumGeneratorConfig.GetDefault);
        GAuthCfg = Load(GAuthConfigFile, NewGAuthDefault);
        Git = Load(GitConfigFile, GitConfig.GetDefault);
    }

    public void SaveAll()
    {
        Save(BinaryConfigFile, Binary);
        Save(JsonConfigFile, Json);
        Save(CodeGenConfigFile, CodeGen);
        Save(EnumConfigFile, EnumCfg);
        Save(GAuthConfigFile, GAuthCfg);
        Save(GitConfigFile, Git);
        UiLog.Write($"[Config] 설정 6종 저장 완료 ({Path.Combine(BaseDirectory, SystemPath.ConfigDirectory)})");
    }

    /// <summary>
    /// GAuth 설정만 파일에 저장한다. [Google 인증] 버튼이 CLI 와 같은 설정을 쓰도록 인증 전에 호출한다.
    /// </summary>
    public void SaveGAuth()
    {
        Save(GAuthConfigFile, GAuthCfg);
        UiLog.Write($"[Config] GAuth 설정 저장 ({GAuthConfigFile})");
    }

    public void ResetDefaults()
    {
        Binary = new DataExtractConfig
        {
            SrcDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data"),
            DstDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output"),
        };
        Json = new DataExtractConfig();
        CodeGen = CodeGenerateConfig.GetDefault();
        EnumCfg = EnumGeneratorConfig.GetDefault();
        GAuthCfg = NewGAuthDefault();
        Git = GitConfig.GetDefault();
    }

    private static UserCredentialConfig NewGAuthDefault() => new()
    {
        ApplicationName = "TableAutomationTool",
        User = "user@example.com",
        ClientSceret = "path/to/client_secret.json",
    };

    private static T Load<T>(string path, Func<T> defaultFactory)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOpts);
                if (loaded != null)
                    return loaded;
            }
        }
        catch (Exception ex)
        {
            UiLog.Write($"[Config][Error] {Path.GetFileName(path)} 로드 실패: {ex.Message}");
        }
        return defaultFactory();
    }

    private static void Save<T>(string path, T config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOpts));
    }
}
