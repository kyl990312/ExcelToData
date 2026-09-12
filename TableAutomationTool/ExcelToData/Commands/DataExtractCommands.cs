using System.Text;
using System.Text.Json;
using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.EnumGenerator.DataExtractor;
using TableAutomationTool.ExcelParser;
using TableAutomationTool.Generator.DataExtractor;
using TableAutomationTool.GAuth.Core;

namespace TableAutomationTool.ExcelToData;

public class ExtractJsonCommand : Command
{
    private DataExtractConfig _config;
    private string? _targetFileName;

    public ExtractJsonCommand(string configPath, string? targetFileName = null)
    {
        var config = JsonSerializer.Deserialize<DataExtractConfig>(File.ReadAllText(configPath));
        _config = config;
        _targetFileName = targetFileName;
    }
    public override void Execute(out string result)
    {
        var sb = new StringBuilder();
        if (!Directory.Exists(_config.SrcDirectory))
        {
            sb.AppendLine($"[ExtractJsonCommand] Wrong Src Directory : {_config.SrcDirectory}");
            result = sb.ToString();
            return;
        }
        if (string.Empty.Equals(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractJsonCommand] Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }

        // targetFileName이 지정되어 있으면 해당 파일만 처리, 없으면 모든 파일 처리
        string[] files;
        if (!string.IsNullOrEmpty(_targetFileName))
        {
            // 확장자 없이 파일명이 들어오므로 패턴에 맞는 파일 찾기
            files = ProjectDefine.SearchPatternsBin
                .Select(pattern => Path.Combine(_config.SrcDirectory, $"{_targetFileName}{pattern.TrimStart('*')}"))
                .Where(File.Exists)
                .ToArray();
            sb.AppendLine($"[ExtractJsonCommand] Processing specific file: {_targetFileName}");
        }
        else
        {
            files = ProjectDefine.SearchPatternsBin
                .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
                .Distinct()
                .ToArray();
            sb.AppendLine($"[ExtractJsonCommand] Processing all files in directory");
        }

        var extractor = new JsonExtractor(_config.DstDirectory);
        foreach (var filePath in files)
        {
            extractor.ExtractData(filePath);
            sb.AppendLine($"[ExtractJsonCommand] Extract Json ({filePath})");
        }
        sb.AppendLine($"[ExtractJsonCommand] Complete Extract Json ({files.Length} files)");
        result = sb.ToString();
    }
}

/// <summary>
/// Excel 파일을 구조화된 바이너리 파일로 추출하는 명령어
/// </summary>
public class ExtractBinaryCommand : Command
{
    private readonly DataExtractConfig _config;
    private readonly string? _targetFileName;

    public ExtractBinaryCommand(string configPath, string? targetFileName = null)
    {
        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<DataExtractConfig>(configText);
        _config = config ?? new DataExtractConfig();
        _targetFileName = targetFileName;
    }

    public override void Execute(out string result)
    {
        var sb = new StringBuilder();
        if (_config.IsLoadFromLocal == false)
        {
            sb.AppendLine($"[ExtractBinaryCommand] IsUseLoadFromLocal is false.");
            result = sb.ToString();
            return;
        }

        if (!Directory.Exists(_config.SrcDirectory))
        {
            sb.AppendLine($"[ExtractBinaryCommand] Wrong Src Directory : {_config.SrcDirectory}");
            result = sb.ToString();
            return;
        }

        if (string.IsNullOrEmpty(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractBinaryCommand] Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }

        try
        {
            var allWorkbooks = ProjectDefine.SearchPatterns
                .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
                .Distinct()
                .ToArray();

            // targetFileName이 지정되어 있으면 해당 대상만 처리, 없으면 전부 처리
            string[] files;
            IReadOnlyCollection<string>? tableFilter = null;

            if (!string.IsNullOrEmpty(_targetFileName))
            {
                // 1) 먼저 "워크북 이름"으로 찾는다.
                //    diff 는 변경된 엑셀 '파일' 이름을 넘기므로 이 경로가 유지돼야 한다.
                files = ProjectDefine.SearchPatterns
                    .Select(pattern => Path.Combine(_config.SrcDirectory, $"{_targetFileName}{pattern.TrimStart('*')}"))
                    .Where(File.Exists)
                    .ToArray();

                if (files.Length > 0)
                {
                    sb.AppendLine($"[ExtractBinaryCommand] Processing workbook: {_targetFileName}");
                }
                else
                {
                    // 2) 워크북이 아니면 "테이블(워크시트) 이름"으로 본다.
                    //    .bin 은 워크시트 이름으로 저장되므로 GUI 목록·CodeGenerator 의 -each 가 쓰는 단위가 이쪽이다.
                    var owner = allWorkbooks.FirstOrDefault(wb => TableHelper.GetTableNames(wb)
                        .Any(t => string.Equals(t, _targetFileName, StringComparison.OrdinalIgnoreCase)));

                    if (owner != null)
                    {
                        files = new[] { owner };
                        tableFilter = new[] { _targetFileName! };
                        sb.AppendLine($"[ExtractBinaryCommand] Processing table '{_targetFileName}' in {Path.GetFileName(owner)}");
                    }
                    else
                    {
                        sb.AppendLine($"[ExtractBinaryCommand] Not found as workbook or table: {_targetFileName}");
                    }
                }
            }
            else
            {
                files = allWorkbooks;
                sb.AppendLine($"[ExtractBinaryCommand] Processing all files in directory");
            }

            var extractor = new StructuredBinaryExtractor(_config.DstDirectory);

            foreach (var filePath in files)
            {
                extractor.ExtractData(filePath, tableFilter);
                sb.AppendLine($"[ExtractBinaryCommand] Extract StructuredBinary ({filePath})");
            }

            sb.AppendLine($"[ExtractBinaryCommand] Complete Extract StructuredBinary ({files.Length} files)");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[ExtractBinaryCommand] Error: {ex.Message}\n{ex.StackTrace}");
        }

        result = sb.ToString();
    }
}

public class ExtractBinaryFromGSheet : Command
{
    private readonly DataExtractConfig _config;
    private readonly string? _targetFileName;

    public ExtractBinaryFromGSheet(string configPath, string? targetFileName = null)
    {
        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<DataExtractConfig>(configText);
        _config = config ?? new DataExtractConfig();
        _targetFileName = targetFileName;
    }

    public override void Execute(out string result)
    {
        var sb = new StringBuilder();
        if (_config.IsLoadFromGSheet == false)
        {
            sb.AppendLine($"[ExtractBinaryFromGSheet] IsUseLoadFromLocal is false.");
            result = sb.ToString();
            return;
        }
        if (string.IsNullOrEmpty(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractBinaryFromGSheet] Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }

        try
        {
            var extractor = new BytesExtractorFromGSheet(_config.DstDirectory, _targetFileName);
            extractor.ExtractData(_config.SrcGDriveLink);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[ExtractBinaryFromGSheet] Error: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
        
        result = sb.ToString();
    }
}

public class GenerateConfigCommand : Command
{
    private string _filePath;
    public GenerateConfigCommand(string filePath)
    {
        _filePath = filePath;
    }
    public override void Execute(out string result)
    {
        if (File.Exists(_filePath))
        {
            result = $"Config file already exists at: {_filePath}";
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);

        var config = new DataExtractConfig
        {
            SrcDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data"),
            DstDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output")
        };
        var options = new JsonSerializerOptions
        {
            IncludeFields = true // 필드를 직렬화에 포함
        };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_filePath,json);
        result = $"[GenerateConfigCommand] Generate Config : {_filePath}. Please Edit Config. \n result : {json}";
    }
}

/// <summary>
/// GAuth (Google API 인증) Config 파일을 생성하는 명령어
/// </summary>
public class GenerateGAuthConfigCommand : Command
{
    private string _filePath;

    public GenerateGAuthConfigCommand(string filePath)
    {
        _filePath = filePath;
    }

    public override void Execute(out string result)
    {
        if (File.Exists(_filePath))
        {
            result = $"[GenerateGAuthConfigCommand] Config file already exists at: {_filePath}";
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);

        var config = new UserCredentialConfig
        {
            ApplicationName = "TableAutomationTool",
            User = "user@example.com",
            ClientSceret = "path/to/client_secret.json"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_filePath, json);
        result = $"[GenerateGAuthConfigCommand] Generated GAuth Config: {_filePath}\nPlease edit the config with your Google API credentials.\n{json}";
    }
}