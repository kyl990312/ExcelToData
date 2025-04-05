using System.Runtime.CompilerServices;
using System.Text.Json;
using Common.Utils;

namespace Common.Command;

public static class FileLoader
{
    public static bool SaveJson<T>(string filePath, T obj)
    {
        try
        {
            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string configJson = JsonSerializer.Serialize(obj, options);
            File.WriteAllText(filePath, configJson);

            Logger.Instance.Log($"[CodeGenerateHelper] Saved config to: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[CodeGenerateHelper] Error saving config: {ex.Message}");
            return false;
        }
    }

    public static bool LoadAndConvertJson<T>(string path, out T result)
        where T : class
    {
        try
        {
            result = null;
            if (!File.Exists(path))
            {
                Logger.Instance.Warning($"[FileLoader] json file not found: {path}");
                return false;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
            result = JsonSerializer.Deserialize<T>(json, options);
            if (result is null)
            {
                Logger.Instance.Error($"[FileLoader] Failed to deserialize json: {path}");
                return false;
            }

            Logger.Instance.Log($"[FileLoader] Convert config from: {path}");
            return true;
        }
        catch (Exception ex)
        {
            result = null;
            Logger.Instance.Error($"[FileLoader] Error loading config: {ex.Message}");
            return false;
        }
    }
}