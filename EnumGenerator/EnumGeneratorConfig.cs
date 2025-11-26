namespace EnumGenerator;

public class EnumGeneratorConfig
{
    public string EnumFilePath { get; set; } = "SystemEnum.xml";
    public string DstDirectory { get; set; } = "Enums/";
    
    public string CodeExtension { get; set; } = ".cs";
    
    /// <summary>
    /// EnumDB 바이너리 파일 저장 경로
    /// </summary>
    public string EnumDBPath { get; set; } = "EnumDB.bin";

    public static EnumGeneratorConfig GetDefault()
    {
        return new EnumGeneratorConfig()
        {
            EnumFilePath = "SystemEnum.xml",
            DstDirectory = "Enums/",
            CodeExtension = ".cs",
            EnumDBPath = "EnumDB.bin",
        };
    }
}
