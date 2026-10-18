namespace TableAutomationTool.AiTools;

/// <summary>
/// AI 툴 오류. Code 는 AI 가 분기할 수 있는 고정 문자열이고, Hint 는 다음에 할 일을 적는다.
/// </summary>
public class AiToolException : Exception
{
    public string Code { get; }
    public string? Hint { get; }

    public AiToolException(string code, string message, string? hint = null) : base(message)
    {
        Code = code;
        Hint = hint;
    }
}

/// <summary>
/// "ai &lt;tool&gt; [위치 인자...] [--옵션 값] [--플래그]" 파서.
/// 같은 옵션을 여러 번 주면 값이 누적된다(--where, --table).
/// </summary>
public sealed class AiArgs
{
    /// <summary>값 없이 쓰는 옵션</summary>
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "pretty", "dry-run", "all" };

    /// <summary>모든 툴이 받는 옵션</summary>
    public static readonly string[] CommonOptions = { "timeout", "pretty" };

    private readonly Dictionary<string, List<string>> _options = new(StringComparer.OrdinalIgnoreCase);

    public string Tool { get; }
    public IReadOnlyList<string> Positionals { get; }

    public AiArgs(string[] args)
    {
        var positionals = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--"))
            {
                positionals.Add(a);
                continue;
            }

            var name = a[2..];
            if (Flags.Contains(name))
            {
                Add(name, "true");
                continue;
            }

            if (i + 1 >= args.Length)
                throw new AiToolException("invalid_args", $"--{name} 뒤에 값이 필요합니다.");
            Add(name, args[++i]);
        }

        Tool = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "help";
        Positionals = positionals.Skip(1).ToList();
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out var values) ? values[^1] : null;

    public IReadOnlyList<string> GetAll(string name)
        => _options.TryGetValue(name, out var values) ? values : Array.Empty<string>();

    public int GetInt(string name, int defaultValue)
    {
        var text = Get(name);
        if (text is null)
            return defaultValue;
        if (!int.TryParse(text, out var value) || value < 0)
            throw new AiToolException("invalid_args", $"--{name} 는 0 이상의 정수여야 합니다: {text}");
        return value;
    }

    public string RequirePositional(int index, string what)
    {
        if (index < Positionals.Count && !string.IsNullOrWhiteSpace(Positionals[index]))
            return Positionals[index];
        throw new AiToolException("invalid_args", $"{what} 이(가) 필요합니다.", $"ai help 로 {Tool} 사용법을 확인하세요.");
    }

    /// <summary>
    /// 오타 난 옵션을 조용히 무시하지 않도록, 허용 목록 밖의 옵션·위치 인자가 있으면 실패시킨다.
    /// </summary>
    public void EnsureKnown(IEnumerable<string> allowedOptions, int maxPositionals)
    {
        var allowed = new HashSet<string>(allowedOptions.Concat(CommonOptions), StringComparer.OrdinalIgnoreCase);
        var unknown = _options.Keys.Where(k => !allowed.Contains(k)).ToList();
        if (unknown.Count > 0)
            throw new AiToolException("invalid_args",
                $"{Tool} 가 모르는 옵션: {string.Join(", ", unknown.Select(u => "--" + u))}",
                $"사용 가능: {string.Join(", ", allowed.Select(o => "--" + o))}");

        if (Positionals.Count > maxPositionals)
            throw new AiToolException("invalid_args",
                $"{Tool} 의 인자가 너무 많습니다: {string.Join(" ", Positionals.Skip(maxPositionals))}");
    }

    private void Add(string name, string value)
    {
        if (!_options.TryGetValue(name, out var values))
            _options[name] = values = new List<string>();
        values.Add(value);
    }
}
