using System.IO;
using System.Text;

namespace TableEditor.Services;

/// <summary>
/// GUI 로그 패널로 라인을 전달하는 허브.
/// 엔진 Logger(Console 출력)와 GUI 자체 메시지가 모두 여기로 모인다.
/// </summary>
public static class UiLog
{
    public static event Action<string>? LineWritten;

    public static void Write(string line) => LineWritten?.Invoke(line);
}

/// <summary>
/// Console.Out 리다이렉트용 TextWriter — 라인 단위로 UiLog에 흘린다.
/// </summary>
public sealed class UiLogWriter : TextWriter
{
    private readonly StringBuilder _buf = new();
    private readonly object _lock = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (_lock)
        {
            if (value == '\n')
            {
                var line = _buf.ToString();
                _buf.Clear();
                UiLog.Write(line);
            }
            else if (value != '\r')
            {
                _buf.Append(value);
            }
        }
    }
}
