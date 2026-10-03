using Domain.Access;
using Domain.MasterData.Branches;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", Schemas.Identity);

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.SecurityStamp).HasMaxLength(32);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // A profile in use cannot be deleted (the use case reports how many users still use it).
        builder.HasOne<MenuAccessProfile>()
            .WithMany()
            .HasForeignKey(u => u.MenuAccessProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BranchAccessProfile>()
            .WithMany()
            .HasForeignKey(u => u.BranchAccessProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(u => u.DefaultBranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", Schemas.Identity);

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
