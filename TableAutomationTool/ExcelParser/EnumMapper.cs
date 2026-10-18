namespace TableAutomationTool.ExcelToData.ExtractBytes;

public struct EnumData
{
    public string EnumName;
    public Dictionary<string, int> EnumValues;
}

public class EnumMapper
{
    private Dictionary<string, Dictionary<string, int>> _enumMap = new Dictionary<string, Dictionary<string, int>>();
}