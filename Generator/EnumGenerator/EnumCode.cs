using System.CodeDom.Compiler;
using System.Net;
using System.Text;

namespace ExcelToData.EnumGenerator;

public class EnumCode
{
    private string _tempalte;
    private string _enumName;
    private string[] _values;
    private string _exe;

    public EnumCode(string name, string[] values, string tempalte = "")
    {
        _enumName = name;
        _values = values;
        _tempalte = tempalte;
    }

    public string GetScript()
    {
        StringBuilder script = new StringBuilder();

        // Enum 정의
        script.AppendLine($"    public enum {_enumName}");
        script.AppendLine("    {");

        // Enum 멤버 생성
        int enumValue = 0;
        foreach (var value in _values)
        {
            script.AppendLine($"{value},");
        }

        script.AppendLine("    }");
        script.AppendLine();

        return _tempalte.Replace("{script}",script.ToString());
    }


}