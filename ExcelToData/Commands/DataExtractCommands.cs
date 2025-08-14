using System.Text;
using System.Text.Json;
using Common.Command;
using Generator.DataExtractor;

namespace ExcelToData;

public class ExtractBinaryCommand : Command
{
    private DataExtractConfig _config;

    public ExtractBinaryCommand(string configPath)
    {
        var config = JsonSerializer.Deserialize<DataExtractConfig>(File.ReadAllText(configPath));
        _config = config;
    }

    public override void Execute(out string result)
    {
        var sb = new StringBuilder();
        if (!Directory.Exists(_config.SrcDirectory))
        {
            sb.AppendLine($"[ExtractBinaryCommand] log: Wrong Src Directory : {_config.SrcDirectory}");
            result = sb.ToString();
            return;
        }
        if (string.Empty.Equals(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractBinaryCommand] log: Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }
        
        var files = SystemDefine.SearchPatterns
            .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
            .Distinct()
            .ToArray();
        if (files == null || files.Length == 0)
            sb.AppendLine($"[ExtractBinaryCommand] warning : There is no table files.");
        var extractor = new BytesExtractor(_config.DstDirectory);
        foreach (var filePath in files)
        {
            extractor.ExtractData(filePath);
            sb.AppendLine($"[ExtractBinaryCommand] log:  Extract Binary ({filePath})");
        }
        sb.AppendLine($"[ExtractBinaryCommand] log: Complete Extract Binary ({_config.SrcDirectory})");
        result = sb.ToString();
    }
}

public class ExtractJsonCommand : Command
{
    private DataExtractConfig _config;

    public ExtractJsonCommand(string configPath)
    {
        var config = JsonSerializer.Deserialize<DataExtractConfig>(File.ReadAllText(configPath));
        _config = config;
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
        
        var files = SystemDefine.SearchPatterns
            .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
            .Distinct()
            .ToArray();
        var extractor = new JsonExtractor(_config.DstDirectory);
        foreach (var filePath in files)
        {
            extractor.ExtractData(filePath);
            sb.AppendLine($"[ExtractJsonCommand] Extract Json ({filePath})");
        }
        sb.AppendLine($"[ExtractJsonCommand] Complete Extract Json ({_config.SrcDirectory})");
        result = sb.ToString();
    }
}

/// <summary>
/// Excel 파일을 구조화된 바이너리 형식으로 추출하는 명령어
/// </summary>
public class ExtractStructuredBinaryCommand : Command
{
    private readonly DataExtractConfig _config;

    public ExtractStructuredBinaryCommand(string configPath)
    {
        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<DataExtractConfig>(configText);
        _config = config ?? new DataExtractConfig();
    }

    public override void Execute(out string result)
    {
        var sb = new StringBuilder();
        
        if (!Directory.Exists(_config.SrcDirectory))
        {
            sb.AppendLine($"[ExtractStructuredBinaryCommand] Wrong Src Directory : {_config.SrcDirectory}");
            result = sb.ToString();
            return;
        }
        
        if (string.IsNullOrEmpty(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractStructuredBinaryCommand] Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }
        
        try
        {
            var files = SystemDefine.SearchPatterns
                .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
                .Distinct()
                .ToArray();
            var extractor = new StructuredBinaryExtractor(_config.DstDirectory);
            
            foreach (var filePath in files)
            {
                extractor.ExtractData(filePath);
                sb.AppendLine($"[ExtractStructuredBinaryCommand] Extract StructuredBinary ({filePath})");
            }
            
            sb.AppendLine($"[ExtractStructuredBinaryCommand] Complete Extract StructuredBinary ({_config.SrcDirectory})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[ExtractStructuredBinaryCommand] Error: {ex.Message}");
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