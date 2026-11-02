using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Receipts;

public interface IDonationReceiptFactory
{
    string NewReceiptNumber(DateTime utcNow);
    DonationReceipt Create(Donation donation, string donorName, string? animalName);
}
