namespace EnumGenerator;

public class EnumGeneratorConfig
{
    public string EnumFilePath { get; set; } = "SystemEnum.xml";
    public string DstDirectory { get; set; } = "Enums/";
    
    public string CodeExtension { get; set; } = ".cs";

    public static EnumGeneratorConfig GetDefault()
    {
        return new EnumGeneratorConfig()
        {
            EnumFilePath = "SystemEnum.xml",
            DstDirectory = "Enums/",
            CodeExtension = ".cs",
        };
    }
}