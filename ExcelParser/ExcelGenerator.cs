using ClosedXML.Excel;

namespace ExcelParser;

public struct SheetData
{
    public string SheetName;
    public string[] ColumnNames;
    public Type[] ColumnTypes;
    public List<object[]> DataList;
}

public class ExcelGenerator
{
    private string _path;
    private XLWorkbook _workbook;

    public XLWorkbook Workbook => _workbook;
    
    /// <summary>
    /// ì£¼ì–´ì§??°ì´?°ì— ë§ëŠ” ?‘ì????ì„±?©ë‹ˆ??
    /// </summary>
    public ExcelGenerator(List<SheetData> sheetList, string path)
    {
        _path = path;
        _workbook = new();
        _workbook.Worksheets.Add("temp");
        
        // ?œíŠ¸ ?ì„± ?„ì— ë¹??Œì¼ ?€?¥ë???
        // ?œíŠ¸ ?ì„± ?œì— ?„ì‹œë¡??Œì¼???€?¥í•˜??ê²½ìš°ê°€ ë°œìƒ?˜ëŠ”?? ?´ë•Œ ?Œì¼ëª…ì´ ?†ìœ¼ë©??•ì¥???ë‹¨???´ë ¤??exception????
        SaveFile();
        
        ClearWorkbook();
        foreach (var data in sheetList)
        {
            CreateSheet(data);
        }
    }

    /// <summary>
    /// ì§€?•ëœ ê²½ë¡œë¡??€?¥í•©?ˆë‹¤.
    /// </summary>
    /// <param name="path">Full Pathë¡??˜ê²¨ì£¼ì„¸??</param>
    public void SaveFile(string path)
    {
        _workbook.SaveAs(path);   
    }

    public void SaveFile()
    {
        SaveFile(_path);
    }
    
    private void CreateSheet(SheetData data)
    {
        if(_workbook.Worksheets.Contains(data.SheetName))
            _workbook.Worksheets.Delete(data.SheetName);
        var worksheet = _workbook.Worksheets.Add(data.SheetName);
        
        // ì»¬ëŸ¼ ?´ë¦„ ???‘ì„± (1??
        for (int i = 0; i < data.ColumnNames.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.SetValue(data.ColumnNames[i]);
        }
    
        // ì»¬ëŸ¼ ?€?????‘ì„± (2??
        for (int i = 0; i < data.ColumnTypes.Length; i++)
        {
            var cell = worksheet.Cell(2, i + 1);
            cell.SetValue(data.ColumnTypes[i].FullName);
        }
    
        // ?°ì´????ì±„ìš°ê¸?(3?‰ë???
        for (int row = 0; row < data.DataList.Count; row++)
        {
            var rowData = data.DataList[row];
            for (int col = 0; col < rowData.Length; col++)
            {
                var cell = worksheet.Cell(row + 3, col + 1);
                var value = rowData[col];
                var targetType = data.ColumnTypes[col];
            
                if (targetType.IsArray)
                {
                    if (value is Array arr)
                    {
                        var elements = new List<string>();
                        foreach (var element in arr)
                        {
                            elements.Add(element.ToString()!);
                        }
                        cell.SetValue($"[{string.Join(",", elements)}]");
                    }
                    else
                    {
                        cell.SetValue("[]");
                    }
                }
                else
                {
                    if (targetType == typeof(int))
                    {
                        cell.SetValue((int)value);
                    }
                    else if(targetType == typeof(long))
                    {
                        cell.SetValue((long)value);
                    }
                    else if(targetType == typeof(float))
                    {
                        cell.SetValue((float)value);
                    }
                    else if(targetType == typeof(double))
                    {
                        cell.SetValue((double)value);
                    }
                    else if(targetType == typeof(string))
                    {
                        cell.SetValue((string)value);
                    }
                }
            }
        }
    }

    private void ClearWorkbook()
    {
        // ?Œí¬?œíŠ¸ ì»¬ë ‰?˜ì˜ ë³µì‚¬ë³¸ì„ ë§Œë“­?ˆë‹¤ (?? œ ì¤‘ì— ì»¬ë ‰?˜ì´ ë³€ê²½ë˜??ê²ƒì„ ë°©ì?)
        var worksheets = _workbook.Worksheets.ToList();
    
        // ê°??Œí¬?œíŠ¸ë¥??œê±°?©ë‹ˆ??
        foreach (var worksheet in worksheets)
        {
            worksheet.Delete();
        }
    }
}
