using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseM5_BreedStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "breed_standard_id",
                schema: "partnership",
                table: "production_cycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closing_standard",
                schema: "partnership",
                table: "production_cycles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "breed_standards",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    strain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_breed_standards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "breed_standard_rows",
                schema: "master",
                columns: table => new
                {
                    breed_standard_id = table.Column<Guid>(type: "uuid", nullable: false),
                    age_days = table.Column<int>(type: "integer", nullable: false),
                    body_weight_gram = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    fcr = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    depletion_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    daily_feed_gram = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    cumulative_feed_gram = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_breed_standard_rows", x => new { x.breed_standard_id, x.age_days });
                    table.ForeignKey(
                        name: "fk_breed_standard_rows_breed_standards_breed_standard_id",
                        column: x => x.breed_standard_id,
                        principalSchema: "master",
                        principalTable: "breed_standards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_breed_standard_id",
                schema: "partnership",
                table: "production_cycles",
                column: "breed_standard_id");

            migrationBuilder.CreateIndex(
                name: "ix_breed_standards_code",
                schema: "master",
                table: "breed_standards",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_production_cycles_breed_standards_breed_standard_id",
                schema: "partnership",
                table: "production_cycles",
                column: "breed_standard_id",
                principalSchema: "master",
                principalTable: "breed_standards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_production_cycles_breed_standards_breed_standard_id",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropTable(
                name: "breed_standard_rows",
                schema: "master");

            migrationBuilder.DropTable(
                name: "breed_standards",
                schema: "master");

            migrationBuilder.DropIndex(
                name: "ix_production_cycles_breed_standard_id",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "breed_standard_id",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "closing_standard",
                schema: "partnership",
                table: "production_cycles");
        }
    }
}
