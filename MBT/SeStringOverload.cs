using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text;

namespace MBT
{
    internal static class SeStringOverload
    {
        public static string ExtractText(this SeString seStr, bool onlyFirst = false)
        {
            StringBuilder sb = new();
            foreach (var x in seStr.Payloads)
            {
                if (x is TextPayload tp)
                {
                    sb.Append(tp.Text);
                    if (onlyFirst) break;
                }
            }
            return sb.ToString();
        }
}
}
