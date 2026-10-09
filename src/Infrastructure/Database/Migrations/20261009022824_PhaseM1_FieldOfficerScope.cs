using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseM1_FieldOfficerScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "field_officer_user_id",
                schema: "master",
                table: "farmers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "field_officer_user_id",
                schema: "master",
                table: "coops",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_farmers_field_officer_user_id",
                schema: "master",
                table: "farmers",
                column: "field_officer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_coops_field_officer_user_id",
                schema: "master",
                table: "coops",
                column: "field_officer_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_coops_users_field_officer_user_id",
                schema: "master",
                table: "coops",
                column: "field_officer_user_id",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_farmers_users_field_officer_user_id",
                schema: "master",
                table: "farmers",
                column: "field_officer_user_id",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_coops_users_field_officer_user_id",
                schema: "master",
                table: "coops");

            migrationBuilder.DropForeignKey(
                name: "fk_farmers_users_field_officer_user_id",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropIndex(
                name: "ix_farmers_field_officer_user_id",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropIndex(
                name: "ix_coops_field_officer_user_id",
                schema: "master",
                table: "coops");

            migrationBuilder.DropColumn(
                name: "field_officer_user_id",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropColumn(
                name: "field_officer_user_id",
                schema: "master",
                table: "coops");
        }
    }
}
