using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PawConnect.Core.Common;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;
using PawConnect.Core.Receipts;
using PawConnect.Core.Services;
using PawConnect.Infrastructure.Data;
using PawConnect.Infrastructure.Notifications;
using PawConnect.Infrastructure.Repositories;

namespace PawConnect.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the database, repositories, services and notification channels.</summary>
    public static IServiceCollection AddPawConnect(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("PawConnect");
        var useInMemory = config.GetValue<bool>("Database:UseInMemory");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (useInMemory)
                // In-memory mode is for automated tests and quick demos only (no PostgreSQL needed).
                options.UseInMemoryDatabase(config["Database:InMemoryName"] ?? "pawconnect")
                       .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            else
                options.UseNpgsql(PostgresConnectionString.Normalize(connectionString ?? throw new InvalidOperationException(
                    "Connection string 'PawConnect' is missing. Set ConnectionStrings__PawConnect.")),
                    npgsql => npgsql.EnableRetryOnFailure(3));
        });

        // Repositories + unit of work (scoped = one per HTTP request, sharing the DbContext).
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAnimalRepository, AnimalRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IHourLogRepository, HourLogRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Business services.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDonationReceiptFactory, DonationReceiptFactory>();
        services.AddScoped<AdoptionService>();
        services.AddScoped<AnimalService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<HourLogService>();
        services.AddScoped<MedicalRecordService>();
        services.AddScoped<DonationService>();
        services.AddScoped<ReportService>();

        // Notification strategies: the selector picks from whichever are registered.
        var email = config.GetSection("Notifications:Email").Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(email);
        services.AddSingleton(config.GetSection("Notifications:Retry").Get<NotificationOptions>() ?? new NotificationOptions());
        if (email.IsConfigured)
            services.AddSingleton<INotificationStrategy, SmtpEmailStrategy>();
        else
            services.AddSingleton<INotificationStrategy, LogOnlyEmailStrategy>();
        if (config.GetValue<bool>("Notifications:Sms:Enabled"))
            services.AddSingleton<INotificationStrategy, LogOnlySmsStrategy>();
        services.AddSingleton<NotificationStrategySelector>();
        services.AddScoped<NotificationService>();
        if (config.GetValue("Notifications:Background", true))
        {
            // Default: services enqueue and return; a hosted worker sends (see BackgroundNotifications.cs).
            services.AddSingleton<BackgroundNotificationQueue>();
            services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<BackgroundNotificationQueue>());
            services.AddHostedService<NotificationDispatcher>();
        }
        else
        {
            services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        }

        // Share data-protection keys through the database (see EfXmlRepository).
        services.AddDataProtection().SetApplicationName("PawConnect");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IServiceScopeFactory>((options, scopes) => options.XmlRepository = new EfXmlRepository(scopes));

        services.AddScoped<DataSeeder>();
        return services;
    }
}
