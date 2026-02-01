using System.Text;
using System.Text.Json;
using TableAutomation.Common;
using TableAutomation.Common.Command;
using EnumGenerator.DataExtractor;
using ExcelParser;
using Generator.DataExtractor;

namespace ExcelToData;

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
        
        var files = ProjectDefine.SearchPatternsBin
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
/// Excel ?åÏùº??Íµ¨Ï°∞?îÎêú Î∞îÏù¥?àÎ¶¨ ?ïÏãù?ºÎ°ú Ï∂îÏ∂ú?òÎäî Î™ÖÎ†π??
/// </summary>
public class ExtractBinaryCommand : Command
{
    private readonly DataExtractConfig _config;

    public ExtractBinaryCommand(string configPath)
    {
        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<DataExtractConfig>(configText);
        _config = config ?? new DataExtractConfig();
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
            var files = ProjectDefine.SearchPatterns
                .SelectMany(pattern => Directory.GetFiles(_config.SrcDirectory, pattern))
                .Distinct()
                .ToArray();
            var extractor = new StructuredBinaryExtractor(_config.DstDirectory);
            
            foreach (var filePath in files)
            {
                extractor.ExtractData(filePath);
                sb.AppendLine($"[ExtractBinaryCommand] Extract StructuredBinary ({filePath})");
            }
            
            sb.AppendLine($"[ExtractBinaryCommand] Complete Extract StructuredBinary ({_config.SrcDirectory})");
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

    public ExtractBinaryFromGSheet(string configPath)
    {
        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<DataExtractConfig>(configText);
        _config = config ?? new DataExtractConfig();
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
            var extractor = new BytesExtractorFromGSheet(_config.DstDirectory);
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
            IncludeFields = true // ?ÑÎìúÎ•?ÏßÅÎ†¨?îÏóê ?¨Ìï®
        };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_filePath,json);
        result = $"[GenerateConfigCommand] Generate Config : {_filePath}. Please Edit Config. \n result : {json}";
    }
}