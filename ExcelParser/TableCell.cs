using DocumentFormat.OpenXml.Office2019.Excel.RichData;

namespace ExcelParser;

public class TableCell
{
    public string Name { get; private set; }
    public object Value { get; private set; }
    public Type Type { get; private set; }

    public TableCell(string name, Type type, object value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}

public class TableColumn
{
    public string Name { get; private set; }
    public string Type { get; private set; }
    
    public List<object> Values { get; private set; }

    public TableColumn(string name, string type)
    {
        Name = name;
        type = type;
        Values = new List<object>();
    }
}