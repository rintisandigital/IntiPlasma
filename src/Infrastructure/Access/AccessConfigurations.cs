using Domain.Access;
using Domain.MasterData.Branches;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Access;

internal sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("menus", Schemas.Identity);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Code).HasMaxLength(100);
        builder.Property(m => m.ParentCode).HasMaxLength(100);
        builder.Property(m => m.Name).HasMaxLength(100);
        builder.Property(m => m.DefaultName).HasMaxLength(100);
        builder.Property(m => m.Icon).HasMaxLength(100);
        builder.Property(m => m.Route).HasMaxLength(200);

        builder.HasIndex(m => m.Code).IsUnique();
    }
}

internal sealed class MenuAccessProfileConfiguration : IEntityTypeConfiguration<MenuAccessProfile>
{
    public void Configure(EntityTypeBuilder<MenuAccessProfile> builder)
    {
        builder.ToTable("menu_access_profiles", Schemas.Identity);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(500);

        builder.HasIndex(p => p.Name).IsUnique();

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MenuAccessItemConfiguration : IEntityTypeConfiguration<MenuAccessItem>
{
    public void Configure(EntityTypeBuilder<MenuAccessItem> builder)
    {
        builder.ToTable("menu_access_profile_items", Schemas.Identity);

        builder.HasKey(i => new { i.ProfileId, i.MenuId });

        builder.HasOne<Menu>()
            .WithMany()
            .HasForeignKey(i => i.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BranchAccessProfileConfiguration : IEntityTypeConfiguration<BranchAccessProfile>
{
    public void Configure(EntityTypeBuilder<BranchAccessProfile> builder)
    {
        builder.ToTable("branch_access_profiles", Schemas.Identity);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(500);

        builder.HasIndex(p => p.Name).IsUnique();

        builder.HasMany(p => p.Branches)
            .WithOne()
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Branches)
            .HasField("_branches")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BranchAccessProfileBranchConfiguration : IEntityTypeConfiguration<BranchAccessProfileBranch>
{
    public void Configure(EntityTypeBuilder<BranchAccessProfileBranch> builder)
    {
        builder.ToTable("branch_access_profile_branches", Schemas.Identity);

        builder.HasKey(b => new { b.ProfileId, b.BranchId });

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(b => b.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeletedUserConfiguration : IEntityTypeConfiguration<DeletedUser>
{
    public void Configure(EntityTypeBuilder<DeletedUser> builder)
    {
        builder.ToTable("user_old", Schemas.Identity);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Email).HasMaxLength(256);
        builder.Property(d => d.FirstName).HasMaxLength(100);
        builder.Property(d => d.LastName).HasMaxLength(100);
        builder.Property(d => d.Reason).HasMaxLength(500);

        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.Email);
    }
}
