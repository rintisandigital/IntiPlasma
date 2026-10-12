using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseM6_ApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approval");

            migrationBuilder.AddColumn<string>(
                name: "pending_change",
                schema: "master",
                table: "farmers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_status",
                schema: "master",
                table: "farmers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<string>(
                name: "registration_status",
                schema: "master",
                table: "coops",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.CreateTable(
                name: "approval_delegations",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegate_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_delegations", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_delegations_users_delegate_user_id",
                        column: x => x.delegate_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_delegations_users_delegator_user_id",
                        column: x => x.delegator_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_flows",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_flows", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_flows_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_flow_levels",
                schema: "approval",
                columns: table => new
                {
                    flow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_no = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    approver_user_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_flow_levels", x => new { x.flow_id, x.level_no });
                    table.ForeignKey(
                        name: "fk_approval_flow_levels_approval_flows_flow_id",
                        column: x => x.flow_id,
                        principalSchema: "approval",
                        principalTable: "approval_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    flow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    current_level = table.Column<int>(type: "integer", nullable: false),
                    level_count = table.Column<int>(type: "integer", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_requests_approval_flows_flow_id",
                        column: x => x.flow_id,
                        principalSchema: "approval",
                        principalTable: "approval_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_approval_requests_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_no = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: false),
                    on_behalf_of = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_decisions_approval_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "approval",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_approval_decisions_users_decided_by",
                        column: x => x.decided_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_decisions_users_on_behalf_of",
                        column: x => x.on_behalf_of,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_request_levels",
                schema: "approval",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_no = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    approver_user_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_request_levels", x => new { x.request_id, x.level_no });
                    table.ForeignKey(
                        name: "fk_approval_request_levels_approval_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "approval",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_decided_by",
                schema: "approval",
                table: "approval_decisions",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_on_behalf_of",
                schema: "approval",
                table: "approval_decisions",
                column: "on_behalf_of");

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_request_id",
                schema: "approval",
                table: "approval_decisions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_delegations_delegate_user_id",
                schema: "approval",
                table: "approval_delegations",
                column: "delegate_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_delegations_delegator_user_id",
                schema: "approval",
                table: "approval_delegations",
                column: "delegator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_flows_branch_id",
                schema: "approval",
                table: "approval_flows",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_flows_document_type_branch_id",
                schema: "approval",
                table: "approval_flows",
                columns: new[] { "document_type", "branch_id" },
                unique: true,
                filter: "is_active")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_branch_id",
                schema: "approval",
                table: "approval_requests",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_document_id_document_type",
                schema: "approval",
                table: "approval_requests",
                columns: new[] { "document_id", "document_type" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_document_pending",
                schema: "approval",
                table: "approval_requests",
                columns: new[] { "document_type", "document_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_flow_id",
                schema: "approval",
                table: "approval_requests",
                column: "flow_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_requested_by",
                schema: "approval",
                table: "approval_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_status_branch_id",
                schema: "approval",
                table: "approval_requests",
                columns: new[] { "status", "branch_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_decisions",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_delegations",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_flow_levels",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_request_levels",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_flows",
                schema: "approval");

            migrationBuilder.DropColumn(
                name: "pending_change",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropColumn(
                name: "registration_status",
                schema: "master",
                table: "farmers");

            migrationBuilder.DropColumn(
                name: "registration_status",
                schema: "master",
                table: "coops");
        }
    }
}
