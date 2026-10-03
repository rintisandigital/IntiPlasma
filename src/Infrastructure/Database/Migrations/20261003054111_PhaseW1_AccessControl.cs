using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseW1_AccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_access_profile_id",
                schema: "identity",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_branch_id",
                schema: "identity",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "menu_access_profile_id",
                schema: "identity",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branch_access_profiles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    all_branches = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_access_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_access_profiles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu_access_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menus",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    in_catalog = table.Column<bool>(type: "boolean", nullable: false),
                    supports_create = table.Column<bool>(type: "boolean", nullable: false),
                    supports_edit = table.Column<bool>(type: "boolean", nullable: false),
                    supports_delete = table.Column<bool>(type: "boolean", nullable: false),
                    supports_export = table.Column<bool>(type: "boolean", nullable: false),
                    is_customized = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_old",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    menu_access_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_access_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    role_names = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_old", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "branch_access_profile_branches",
                schema: "identity",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_access_profile_branches", x => new { x.profile_id, x.branch_id });
                    table.ForeignKey(
                        name: "fk_branch_access_profile_branches_branch_access_profiles_profi",
                        column: x => x.profile_id,
                        principalSchema: "identity",
                        principalTable: "branch_access_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_branch_access_profile_branches_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "menu_access_profile_items",
                schema: "identity",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    can_view = table.Column<bool>(type: "boolean", nullable: false),
                    can_create = table.Column<bool>(type: "boolean", nullable: false),
                    can_edit = table.Column<bool>(type: "boolean", nullable: false),
                    can_delete = table.Column<bool>(type: "boolean", nullable: false),
                    can_export = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu_access_profile_items", x => new { x.profile_id, x.menu_id });
                    table.ForeignKey(
                        name: "fk_menu_access_profile_items_menu_access_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "identity",
                        principalTable: "menu_access_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_menu_access_profile_items_menus_menu_id",
                        column: x => x.menu_id,
                        principalSchema: "identity",
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_branch_access_profile_id",
                schema: "identity",
                table: "users",
                column: "branch_access_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_default_branch_id",
                schema: "identity",
                table: "users",
                column: "default_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_menu_access_profile_id",
                schema: "identity",
                table: "users",
                column: "menu_access_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_branch_access_profile_branches_branch_id",
                schema: "identity",
                table: "branch_access_profile_branches",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_branch_access_profiles_name",
                schema: "identity",
                table: "branch_access_profiles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_access_profile_items_menu_id",
                schema: "identity",
                table: "menu_access_profile_items",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_access_profiles_name",
                schema: "identity",
                table: "menu_access_profiles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menus_code",
                schema: "identity",
                table: "menus",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_old_email",
                schema: "identity",
                table: "user_old",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_user_old_user_id",
                schema: "identity",
                table: "user_old",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_branch_access_profiles_branch_access_profile_id",
                schema: "identity",
                table: "users",
                column: "branch_access_profile_id",
                principalSchema: "identity",
                principalTable: "branch_access_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_branches_default_branch_id",
                schema: "identity",
                table: "users",
                column: "default_branch_id",
                principalSchema: "master",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_menu_access_profiles_menu_access_profile_id",
                schema: "identity",
                table: "users",
                column: "menu_access_profile_id",
                principalSchema: "identity",
                principalTable: "menu_access_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ---- Data migration (PLAN-WEBAPP §11.3) ------------------------------------------------------
            // System profiles (ids fixed in Domain: MenuAccessProfile.FullAccessId, BranchAccessProfile.AllBranchesId).
            migrationBuilder.Sql(
                """
                INSERT INTO identity.menu_access_profiles (id, name, description, is_system, created_at_utc)
                VALUES ('0199a3c0-0000-7000-8000-000000000001', 'Full Access',
                        'Every right on every menu (system profile).', true, now() AT TIME ZONE 'utc');

                INSERT INTO identity.branch_access_profiles (id, name, description, all_branches, is_system, created_at_utc)
                VALUES ('0199a3c0-0000-7000-8000-000000000002', 'All Branches',
                        'Every branch, including branches created later (system profile).', true, true, now() AT TIME ZONE 'utc');
                """);

            // Users whose roles granted branches:access-all (head office, Administrator) -> All Branches.
            migrationBuilder.Sql(
                """
                UPDATE identity.users u
                SET branch_access_profile_id = '0199a3c0-0000-7000-8000-000000000002'
                WHERE EXISTS (
                    SELECT 1
                    FROM identity.user_roles ur
                    JOIN identity.role_permissions rp ON rp.role_id = ur.role_id
                    WHERE ur.user_id = u.id AND rp.permission = 'branches:access-all');
                """);

            // Other users with assigned branches -> one profile per identical branch combination.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE w1_user_sets ON COMMIT DROP AS
                SELECT ub.user_id,
                       string_agg(ub.branch_id::text, ',' ORDER BY b.code) AS set_key,
                       string_agg(b.code, ', ' ORDER BY b.code) AS codes,
                       (array_agg(ub.branch_id ORDER BY b.code))[1] AS first_branch
                FROM identity.user_branches ub
                JOIN master.branches b ON b.id = ub.branch_id
                JOIN identity.users u ON u.id = ub.user_id
                WHERE u.branch_access_profile_id IS NULL
                GROUP BY ub.user_id;

                CREATE TEMP TABLE w1_sets ON COMMIT DROP AS
                SELECT set_key, min(codes) AS codes, gen_random_uuid() AS profile_id
                FROM w1_user_sets
                GROUP BY set_key;

                INSERT INTO identity.branch_access_profiles (id, name, description, all_branches, is_system, created_at_utc)
                SELECT profile_id, left('Branches: ' || codes, 100),
                       'Migrated from the former per-user branch assignment.', false, false, now() AT TIME ZONE 'utc'
                FROM w1_sets;

                INSERT INTO identity.branch_access_profile_branches (profile_id, branch_id)
                SELECT s.profile_id, unnest(string_to_array(s.set_key, ','))::uuid
                FROM w1_sets s;

                UPDATE identity.users u
                SET branch_access_profile_id = s.profile_id, default_branch_id = us.first_branch
                FROM w1_user_sets us
                JOIN w1_sets s ON s.set_key = us.set_key
                WHERE u.id = us.user_id;
                """);

            // Administrators keep using every menu.
            migrationBuilder.Sql(
                """
                UPDATE identity.users u
                SET menu_access_profile_id = '0199a3c0-0000-7000-8000-000000000001'
                WHERE EXISTS (
                    SELECT 1
                    FROM identity.user_roles ur
                    JOIN identity.roles r ON r.id = ur.role_id
                    WHERE ur.user_id = u.id AND r.is_system AND r.name = 'Administrator');
                """);

            // W-14: branch scope is no longer a permission.
            migrationBuilder.Sql("DELETE FROM identity.role_permissions WHERE permission = 'branches:access-all';");

            migrationBuilder.DropTable(
                name: "user_branches",
                schema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy (best effort): profile branches go back to per-user rows; "all branches" profiles and the
            // branches:access-all permission are not restored (the old seeder re-adds it to Administrator).
            migrationBuilder.CreateTable(
                name: "user_branches",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_branches", x => new { x.user_id, x.branch_id });
                    table.ForeignKey(
                        name: "fk_user_branches_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_branches_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_branches_branch_id",
                schema: "identity",
                table: "user_branches",
                column: "branch_id");

            migrationBuilder.Sql(
                """
                INSERT INTO identity.user_branches (user_id, branch_id)
                SELECT u.id, pb.branch_id
                FROM identity.users u
                JOIN identity.branch_access_profile_branches pb ON pb.profile_id = u.branch_access_profile_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_users_branch_access_profiles_branch_access_profile_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_branches_default_branch_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_menu_access_profiles_menu_access_profile_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropTable(
                name: "branch_access_profile_branches",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "menu_access_profile_items",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_old",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "branch_access_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "menu_access_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "menus",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_users_branch_access_profile_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_default_branch_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_menu_access_profile_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "branch_access_profile_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "default_branch_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "menu_access_profile_id",
                schema: "identity",
                table: "users");
        }
    }
}
