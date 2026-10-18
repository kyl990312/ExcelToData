using System.Text;

namespace TableAutomationTool.AiTools;

/// <summary>
/// 엔진 로그(Console.Out/Error)를 stderr 로 흘리면서 줄 단위로 모은다.
/// stdout 은 JSON 결과 전용으로 비워 두고, run 은 모은 줄에서 오류·경고를 골라낸다.
/// </summary>
public sealed class AiConsoleCapture : TextWriter
{
    private readonly TextWriter _inner;
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _lines = new();
    private readonly object _lock = new();

    public AiConsoleCapture(TextWriter inner)
    {
        _inner = inner;
    }

    public override Encoding Encoding => _inner.Encoding;

    public int LineCount
    {
        get
        {
            lock (_lock)
                return _lines.Count;
        }
    }

    public List<string> LinesFrom(int index)
    {
        lock (_lock)
            return _lines.Skip(index).ToList();
    }

    public override void Write(char value)
    {
        lock (_lock)
        {
            _inner.Write(value);
            Append(value);
        }
    }

    public override void Write(string? value)
    {
        if (value == null)
            return;

        lock (_lock)
        {
            _inner.Write(value);
            foreach (var c in value)
                Append(c);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_lock)
        {
            _inner.Write(buffer, index, count);
            for (int i = index; i < index + count; i++)
                Append(buffer[i]);
        }
    }

    public override void Flush() => _inner.Flush();

    private void Append(char c)
    {
        if (c == '\n')
        {
            _lines.Add(_buffer.ToString());
            _buffer.Clear();
        }
        else if (c != '\r')
        {
            _buffer.Append(c);
        }
    }
}
