using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Receipts;

/// <summary>South African Rand formatting ("R 1 250,00" style is avoided for readability).</summary>
public static class ZarCulture
{
    public static readonly CultureInfo Instance = Create();

    private static CultureInfo Create()
    {
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.CurrencySymbol = "R";
        c.NumberFormat.CurrencyPositivePattern = 0; // R1,250.00
        c.NumberFormat.CurrencyNegativePattern = 1;
        c.NumberFormat.CurrencyDecimalDigits = 2;
        return c;
    }
}
