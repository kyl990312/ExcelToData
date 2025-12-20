using Common.Utils;
using ExcelParser;

namespace EnumGenerator;

internal class SharedData : Singleton<SharedData>
{
    public EnumCache EnumCache { get; } = new EnumCache();
}
