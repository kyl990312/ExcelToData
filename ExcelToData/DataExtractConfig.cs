namespace ExcelToData;

[Serializable]
public class DataExtractConfig
{
    public bool IsLoadFromLocal { get; set; } = true;
    public string SrcDirectory { get; set; } = string.Empty;
    public string DstDirectory { get; set; } = string.Empty;

    public bool IsLoadFromGSheet { get; set; } = false;

    public string SrcGDriveLink { get; set; } = string.Empty;
}


