using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PawConnect.Infrastructure.Data;

/// <summary>A data-protection key (used to encrypt login cookies and tokens), stored in PostgreSQL.</summary>
public class DataProtectionKey
{
    public int Id { get; set; }
    public string? FriendlyName { get; set; }
    public string Xml { get; set; } = "";
}

/// <summary>
/// Keeps ASP.NET Core's data-protection keys in the database instead of on the web server's disk.
/// That way every App Service instance and deployment slot shares the same keys, and users stay
/// signed in after a restart or a blue-green slot swap.
/// </summary>
public class EfXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory _scopes;
    public EfXmlRepository(IServiceScopeFactory scopes) => _scopes = scopes;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.DataProtectionKeys.AsNoTracking().Select(k => k.Xml).ToList().Select(XElement.Parse).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting) });
        db.SaveChanges();
    }
}
