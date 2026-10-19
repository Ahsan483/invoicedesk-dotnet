using System.Globalization;

namespace InvoiceDesk.Web;

public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal v) => v.ToString("C2", Us);
}
