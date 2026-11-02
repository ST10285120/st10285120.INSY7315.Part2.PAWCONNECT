using System.Globalization;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

public record MonthlyTotal(string Label, int Year, int Month, decimal Amount);
