using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

// Table, key, index and relationship rules. These implement the ERD (section 5.2).
// The services validate input too, but the database has the final say: CHECK constraints and
// (partial) unique indexes keep the data valid even if a bug or a race slips past the code.

internal static class Sql
{
    /// <summary>CHECK constraint SQL limiting a string-stored enum column to its defined names.</summary>
    public static string EnumIn<TEnum>(string column) where TEnum : struct, Enum =>
        $"\"{column}\" IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
