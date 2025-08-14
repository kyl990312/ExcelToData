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
    /// 주어진 데이터에 맞는 엑셀을 생성합니다.
    /// </summary>
    public ExcelGenerator(List<SheetData> sheetList, string path)
    {
        _path = path;
        _workbook = new();
        _workbook.Worksheets.Add("temp");
        
        // 시트 생성 전에 빈 파일 저장부터
        // 시트 생성 시에 임시로 파일을 저장하는 경우가 발생하는데, 이때 파일명이 없으면 확장자 판단이 어려워 exception을 냄
        SaveFile();
        
        ClearWorkbook();
        foreach (var data in sheetList)
        {
            CreateSheet(data);
        }
    }

    /// <summary>
    /// 지정된 경로로 저장합니다.
    /// </summary>
    /// <param name="path">Full Path로 넘겨주세요.</param>
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
        
        // 컬럼 이름 행 작성 (1행)
        for (int i = 0; i < data.ColumnNames.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.SetValue(data.ColumnNames[i]);
        }
    
        // 컬럼 타입 행 작성 (2행)
        for (int i = 0; i < data.ColumnTypes.Length; i++)
        {
            var cell = worksheet.Cell(2, i + 1);
            cell.SetValue(data.ColumnTypes[i].FullName);
        }
    
        // 데이터 행 채우기 (3행부터)
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
        // 워크시트 컬렉션의 복사본을 만듭니다 (삭제 중에 컬렉션이 변경되는 것을 방지)
        var worksheets = _workbook.Worksheets.ToList();
    
        // 각 워크시트를 제거합니다
        foreach (var worksheet in worksheets)
        {
            worksheet.Delete();
        }
    }
}
