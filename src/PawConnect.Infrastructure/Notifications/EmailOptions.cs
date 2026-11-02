using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

public class EmailOptions
{
    /// <summary>SMTP host, e.g. smtp.azurecomm.net (Azure Communication Services) or smtp.sendgrid.net.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = "no-reply@pawconnect.local";
    public string FromName { get; set; } = "Hope & Paws Animal Shelter";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}
