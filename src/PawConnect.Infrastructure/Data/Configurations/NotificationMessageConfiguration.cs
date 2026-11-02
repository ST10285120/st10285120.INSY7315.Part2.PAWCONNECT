using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> b)
    {
        b.ToTable("Notifications");
        b.Property(n => n.Recipient).HasMaxLength(200);
        b.Property(n => n.Subject).HasMaxLength(200);
        b.Property(n => n.Body).HasMaxLength(4000);
        b.Property(n => n.Error).HasMaxLength(1000);
        b.HasIndex(n => n.CreatedAt);
    }
}
