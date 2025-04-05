namespace ExcelParser;
public static class TableDefine
{
    public const int ROW_HEADER_NAME = 1;
    public const int ROW_HEADER_TYPE = 2;
    public const string REGEX_START_EXCEPT_COLUMN = "#";
    public const string REGEX_START_EXCEPT_ROW = "//";
}

public enum EDataType
{
    Int16,
    Int32,
    Int64,
    Single,
    Double,
    String,
    Array,
    
    Max
}