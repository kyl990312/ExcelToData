using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Command;
using ExcelParser;
using Generator.DataExtractor;

namespace ExcelToData.ExtractBytes;

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
            sb.AppendLine($"[ExtractBinaryCommand] Wrong Src Directory : {_config.SrcDirectory}");
            result = sb.ToString();
            return;
        }
        if (string.Empty.Equals(_config.DstDirectory))
        {
            sb.AppendLine($"[ExtractBinaryCommand] Wrong Dst Directory : {_config.DstDirectory}");
            result = sb.ToString();
            return;
        }
        
        var files = Directory.GetFiles(_config.SrcDirectory, "*.xlsm");
        var extractor = new BytesExtractor(_config.DstDirectory);
        foreach (var filePath in files)
        {
            extractor.ExtractData(filePath);
            sb.AppendLine($"[ExtractBinaryCommand] Extract Binary ({filePath})");
        }
        sb.AppendLine($"[ExtractBinaryCommand] Complete Extract Binary ({_config.SrcDirectory})");
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
        
        var files = Directory.GetFiles(_config.SrcDirectory, "*.xlsm");
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