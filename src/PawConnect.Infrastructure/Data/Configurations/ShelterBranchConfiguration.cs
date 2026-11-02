using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

public class ShelterBranchConfiguration : IEntityTypeConfiguration<ShelterBranch>
{
    public void Configure(EntityTypeBuilder<ShelterBranch> b)
    {
        b.ToTable("ShelterBranches");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);
        b.HasIndex(x => x.Name).IsUnique();
    }
}
