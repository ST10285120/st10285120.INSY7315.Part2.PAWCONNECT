using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

public class AnimalPhotoConfiguration : IEntityTypeConfiguration<AnimalPhoto>
{
    public void Configure(EntityTypeBuilder<AnimalPhoto> b)
    {
        b.Property(p => p.ContentType).HasMaxLength(40).IsRequired();
        b.Property(p => p.Data).IsRequired();
        b.HasIndex(p => new { p.AnimalId, p.IsPrimary });
        // At most one primary (cover) photo per animal.
        b.HasIndex(p => p.AnimalId)
            .HasDatabaseName("IX_AnimalPhotos_AnimalId_Primary")
            .IsUnique()
            .HasFilter("\"IsPrimary\"");
        b.ToTable(t => t.HasCheckConstraint("CK_AnimalPhotos_Size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 2097152"));
    }
}
