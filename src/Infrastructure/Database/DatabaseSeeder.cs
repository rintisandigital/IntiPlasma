using Application.Abstractions.Authentication;
using Domain.Access;
using Domain.MasterData.Uoms;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database;

public static class DatabaseSeeder
{
    /// <summary>
    /// Idempotent seed: keeps the Administrator role in sync with the permission catalog and creates the
    /// initial administrator from the "Seed:Admin" configuration section (if configured and not yet present).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Role? administrator = await dbContext.Roles
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.IsSystem && r.Name == Role.AdministratorName, cancellationToken);

        if (administrator is null)
        {
            administrator = Role.CreateAdministrator();
            dbContext.Roles.Add(administrator);
        }
        else
        {
            administrator.SyncSystemPermissions();
        }

        await SeedAccessProfilesAsync(dbContext, cancellationToken);

        await SeedMobileRolesAsync(dbContext, cancellationToken);

        string? email = configuration["Seed:Admin:Email"];
        string? password = configuration["Seed:Admin:Password"];

        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            User? admin = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (admin is null)
            {
                admin = User.Create(email, "System", "Administrator", passwordHasher.Hash(password));
                admin.SetRoles([administrator.Id]);

                dbContext.Users.Add(admin);
            }

            // The seeded administrator always keeps full menu and branch access.
            if (admin.MenuAccessProfileId is null || admin.BranchAccessProfileId is null)
            {
                admin.SetAccess(MenuAccessProfile.FullAccessId, BranchAccessProfile.AllBranchesId, admin.DefaultBranchId);
            }
        }

        await SeedUomsAsync(dbContext, cancellationToken);

        await FinanceSeeder.SeedAsync(dbContext, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// System profiles Full Access and All Branches (also created by the PhaseW1 migration).
    /// </summary>
    private static async Task SeedAccessProfilesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.MenuAccessProfiles.AnyAsync(p => p.Id == MenuAccessProfile.FullAccessId, cancellationToken))
        {
            dbContext.MenuAccessProfiles.Add(MenuAccessProfile.CreateFullAccess());
        }

        if (!await dbContext.BranchAccessProfiles.AnyAsync(p => p.Id == BranchAccessProfile.AllBranchesId, cancellationToken))
        {
            dbContext.BranchAccessProfiles.Add(BranchAccessProfile.CreateAllBranches());
        }
    }

    /// <summary>
    /// Default API roles for the mobile app (PLAN-MOBILE §4.2), created only when no role with that name exists:
    /// administrators may change them afterwards without the seeder overwriting their changes.
    /// </summary>
    private static async Task SeedMobileRolesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        (string Name, string Description, string[] Permissions)[] defaults =
        [
            (
                MobileRoles.FieldOfficer,
                "Petugas lapangan (mobile): farmers, coops, contracts and daily inputs of the assigned coops",
                [
                    Permissions.PartnershipAssignedOnly,
                    Permissions.FarmersRead,
                    Permissions.FarmersManage,
                    Permissions.ContractsRead,
                    Permissions.ContractsManage,
                    Permissions.CyclesRead,
                    Permissions.ProductionRead,
                    Permissions.ProductionRecord,
                    Permissions.ProductionRevise,
                    Permissions.ProductionStockReport,
                    Permissions.InventoryRead,
                    Permissions.InventoryRequestFeed,
                    Permissions.InventoryRequestFeedMutation,
                    Permissions.MasterDataRead,
                    Permissions.WarehousesRead,
                    Permissions.AttachmentsUpload,
                    Permissions.AttachmentsRead
                ]
            ),
            (
                MobileRoles.Manager,
                "Manager (mobile): reads the data of their branches and decides approvals",
                [
                    Permissions.FarmersRead,
                    Permissions.ContractsRead,
                    Permissions.CyclesRead,
                    Permissions.ProductionRead,
                    Permissions.InventoryRead,
                    Permissions.MasterDataRead,
                    Permissions.WarehousesRead,
                    Permissions.AttachmentsRead,
                    Permissions.ApprovalsDecide
                ]
            )
        ];

        List<string> existing = await dbContext.Roles.Select(r => r.Name).ToListAsync(cancellationToken);

        foreach ((string name, string description, string[] permissions) in defaults.Where(d => !existing.Contains(d.Name)))
        {
            dbContext.Roles.Add(Role.Create(name, description, permissions).Value);
        }
    }

    /// <summary>
    /// Standard units used by broiler farming. Tax codes are deliberately not seeded: tariffs and
    /// facilities must be confirmed by the tax consultant and entered as data.
    /// </summary>
    private static async Task SeedUomsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        (string Code, string Name)[] defaults =
        [
            ("EKOR", "Ekor"),
            ("KG", "Kilogram"),
            ("GR", "Gram"),
            ("SAK", "Sak"),
            ("BTL", "Botol"),
            ("VIAL", "Vial"),
            ("LTR", "Liter"),
            ("ML", "Mililiter"),
            ("PCS", "Pcs")
        ];

        List<string> existing = await dbContext.Uoms.Select(u => u.Code).ToListAsync(cancellationToken);

        dbContext.Uoms.AddRange(defaults
            .Where(d => !existing.Contains(d.Code))
            .Select(d => Uom.Create(d.Code, d.Name)));
    }
}
