using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_ReportingClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cash_flow_category",
                schema: "finance",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Operating");

            // Classify the existing chart of accounts like the seeded one: fixed assets are investing activities,
            // long-term liabilities and equity financing. Finance can change any account afterwards.
            migrationBuilder.Sql(
                """
                UPDATE finance.accounts SET cash_flow_category = 'Investing' WHERE code LIKE '1-2%';
                UPDATE finance.accounts SET cash_flow_category = 'Financing' WHERE code LIKE '2-2%' OR type = 'Equity';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cash_flow_category",
                schema: "finance",
                table: "accounts");
        }
    }
}
