using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CreditNote_Void : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "sales",
                table: "sales_credit_notes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Posted");

            migrationBuilder.AddColumn<DateOnly>(
                name: "void_date",
                schema: "sales",
                table: "sales_credit_notes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                schema: "sales",
                table: "sales_credit_notes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                schema: "sales",
                table: "sales_credit_notes");

            migrationBuilder.DropColumn(
                name: "void_date",
                schema: "sales",
                table: "sales_credit_notes");

            migrationBuilder.DropColumn(
                name: "void_reason",
                schema: "sales",
                table: "sales_credit_notes");
        }
    }
}
