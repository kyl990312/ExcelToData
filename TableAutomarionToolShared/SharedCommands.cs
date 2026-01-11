using Common.Command;
using UserEnum;


namespace TableAutomarionToolShared
{
    public class LoadEnumDBCommand : Command
    {
        public override void Execute(out string result)
        {
            if (EnumDB.Instance.LoadWithFallback(SystemPath.EnumBinaryPath) == false)
            {
                result = "[LoadEnumDBCommand] Load Enum DB Failed.";
                return;
            }
            result = "[LoadEnumDBCommand] Load Enum DB Complete.";
        }
    }
}
