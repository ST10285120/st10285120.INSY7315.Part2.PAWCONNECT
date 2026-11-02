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
