using Microsoft.VisualBasic;
using System;
using System.IO;

namespace Common.Utils;

public class Logger : IDisposable
{
    private static readonly string PATH = "Logs.txt";
    
    private string _log = "";

    private static Logger _instnace = null;
    public static Logger Instance
    {
        get
        {
            if (_instnace == null)
                _instnace = new Logger();
            return _instnace;
        }
    }

    public void Dispose()
    {
        CreateLogFile();
    }

    public void Error(string msg)
    {
        var log = $"[Error][{DateTime.Now}] {msg}";
        Console.Error.WriteLine(log);
        _log += '\n'+log;
    }

    public void Warning(string msg)
    {
        var log = $"[Warning][{DateTime.Now}] {msg}";
        Console.WriteLine(log);
        _log += '\n'+log;
    }

    public void Log(string msg)
    {
        var log = $"[{DateTime.Now}] {msg}";
        Console.WriteLine(log);
        _log += '\n'+log;
    }
    
    private void CreateLogFile()
    {
        using (StreamWriter sw = new StreamWriter(PATH, true))
        {
            sw.Write(_log);
        }
    }
}