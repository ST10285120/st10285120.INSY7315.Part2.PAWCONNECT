using Npgsql;

namespace PawConnect.Infrastructure.Data;

/// <summary>
/// Hosts such as Neon, Render and Heroku hand out PostgreSQL URLs
/// ("postgresql://user:pass@host/db?sslmode=require"), while Npgsql expects
/// "Host=...;Username=...". This converts a URL to Npgsql format and leaves
/// normal connection strings unchanged, so either can be pasted into settings.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        var value = connectionString.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var val = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            if (key == "sslmode" && Enum.TryParse<SslMode>(val.Replace("-", ""), ignoreCase: true, out var mode))
                builder.SslMode = mode;
            // Other URL options (e.g. channel_binding) are negotiated automatically by Npgsql.
        }
        return builder.ConnectionString;
    }
}
