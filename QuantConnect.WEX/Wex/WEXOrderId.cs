using System.Globalization;

namespace QuantConnect.WEX.Wex
{
    public static class WEXOrderId
    {
        public static string GetNext()
        {
            return Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }
    }
}
