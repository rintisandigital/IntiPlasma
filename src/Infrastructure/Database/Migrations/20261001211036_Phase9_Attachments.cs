using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_Attachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "master",
                table: "vendors",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "finance",
                table: "vendor_invoices",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "inventory",
                table: "stock_transfers",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "inventory",
                table: "stock_returns",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "sales",
                table: "sales_orders",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "procurement",
                table: "purchase_orders",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "partnership",
                table: "production_cycles",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "costing",
                table: "plasma_settlements",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "finance",
                table: "payment_vouchers",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "finance",
                table: "journal_entries",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "inventory",
                table: "goods_receipts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "master",
                table: "farmers",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "sales",
                table: "delivery_orders",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "production",
                table: "daily_recordings",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "production",
                table: "daily_recording_revisions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "partnership",
                table: "cycle_harvests",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "master",
                table: "customers",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "finance",
                table: "customer_receipts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "master",
                table: "coops",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "partnership",
                table: "contracts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "documents",
                schema: "finance",
                table: "cash_transactions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unlinked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attachment_links",
                schema: "documents",
                columns: table => new
                {
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment_links", x => new { x.owner_type, x.owner_id, x.owner_key, x.attachment_id });
                    table.ForeignKey(
                        name: "fk_attachment_links_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalSchema: "documents",
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachment_links_attachment_id",
                schema: "documents",
                table: "attachment_links",
                column: "attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_checksum",
                schema: "documents",
                table: "attachments",
                column: "checksum");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_status_unlinked_at_utc",
                schema: "documents",
                table: "attachments",
                columns: new[] { "status", "unlinked_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_links",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "attachments",
                schema: "documents");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "master",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "finance",
                table: "vendor_invoices");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "inventory",
                table: "stock_transfers");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "inventory",
                table: "stock_returns");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "sales",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "costing",
                table: "plasma_settlements");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "finance",
                table: "payment_vouchers");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "finance",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "inventory",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "sales",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "production",
                table: "daily_recordings");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "production",
                table: "daily_recording_revisions");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "partnership",
                table: "cycle_harvests");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "master",
                table: "coops");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "partnership",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "documents",
                schema: "finance",
                table: "cash_transactions");
        }
    }
}
