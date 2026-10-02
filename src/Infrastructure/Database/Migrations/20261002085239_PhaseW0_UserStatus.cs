using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseW0_UserStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                schema: "identity",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Existing users stay active and each gets its own stamp.
            migrationBuilder.Sql("UPDATE identity.users SET security_stamp = replace(gen_random_uuid()::text, '-', '');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "identity",
                table: "users");
        }
    }
}
