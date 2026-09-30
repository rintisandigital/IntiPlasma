using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_MasterData_Partnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "master");

            migrationBuilder.EnsureSchema(
                name: "partnership");

            migrationBuilder.CreateTable(
                name: "branches",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    payment_term_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_identity_is_pkp = table.Column<bool>(type: "boolean", nullable: false),
                    tax_identity_nitku = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: true),
                    tax_identity_npwp = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_codes",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    vat_treatment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    income_tax_article = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uoms",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uoms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    payment_term_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    bank_account_account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    bank_account_account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bank_account_bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_identity_is_pkp = table.Column<bool>(type: "boolean", nullable: false),
                    tax_identity_nitku = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: true),
                    tax_identity_npwp = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "farmers",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nik = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    bank_account_account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    bank_account_account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bank_account_bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_identity_is_pkp = table.Column<bool>(type: "boolean", nullable: false),
                    tax_identity_nitku = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: true),
                    tax_identity_npwp = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farmers", x => x.id);
                    table.ForeignKey(
                        name: "fk_farmers_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateTable(
                name: "contracts",
                schema: "partnership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheme = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    plasma_profit_share_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    income_tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_contracts_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contracts_tax_codes_income_tax_code_id",
                        column: x => x.income_tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_rates",
                schema: "master",
                columns: table => new
                {
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    tax_base_ratio = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_rates", x => new { x.tax_code_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_tax_rates_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_items_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_items_uoms_base_uom_id",
                        column: x => x.base_uom_id,
                        principalSchema: "master",
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coops",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    house_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coops", x => x.id);
                    table.ForeignKey(
                        name: "fk_coops_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coops_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalSchema: "master",
                        principalTable: "farmers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_incentives",
                schema: "partnership",
                columns: table => new
                {
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    metric = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    range_from = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    range_to = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_incentives", x => new { x.contract_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_contract_incentives_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "partnership",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_live_bird_prices",
                schema: "partnership",
                columns: table => new
                {
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_weight_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    max_weight_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_live_bird_prices", x => new { x.contract_id, x.min_weight_kg });
                    table.ForeignKey(
                        name: "fk_contract_live_bird_prices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "partnership",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_input_prices",
                schema: "partnership",
                columns: table => new
                {
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_input_prices", x => new { x.contract_id, x.item_id });
                    table.ForeignKey(
                        name: "fk_contract_input_prices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "partnership",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contract_input_prices_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_uom_conversions",
                schema: "master",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_uom_conversions", x => new { x.item_id, x.uom_id });
                    table.ForeignKey(
                        name: "fk_item_uom_conversions_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_item_uom_conversions_uoms_uom_id",
                        column: x => x.uom_id,
                        principalSchema: "master",
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_cycles",
                schema: "partnership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    planned_chick_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_population = table.Column<int>(type: "integer", nullable: false),
                    chick_in_date = table.Column<DateOnly>(type: "date", nullable: true),
                    initial_population = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_cycles", x => x.id);
                    table.ForeignKey(
                        name: "fk_production_cycles_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_cycles_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "partnership",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_cycles_coops_coop_id",
                        column: x => x.coop_id,
                        principalSchema: "master",
                        principalTable: "coops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_cycles_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalSchema: "master",
                        principalTable: "farmers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    coop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "fk_warehouses_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouses_coops_coop_id",
                        column: x => x.coop_id,
                        principalSchema: "master",
                        principalTable: "coops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branches_code",
                schema: "master",
                table: "branches",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contract_input_prices_item_id",
                schema: "partnership",
                table: "contract_input_prices",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_branch_id",
                schema: "partnership",
                table: "contracts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_code",
                schema: "partnership",
                table: "contracts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contracts_income_tax_code_id",
                schema: "partnership",
                table: "contracts",
                column: "income_tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_coops_branch_id",
                schema: "master",
                table: "coops",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_coops_code",
                schema: "master",
                table: "coops",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coops_farmer_id",
                schema: "master",
                table: "coops",
                column: "farmer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_code",
                schema: "master",
                table: "customers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farmers_branch_id",
                schema: "master",
                table: "farmers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_farmers_code",
                schema: "master",
                table: "farmers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_uom_conversions_uom_id",
                schema: "master",
                table: "item_uom_conversions",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_base_uom_id",
                schema: "master",
                table: "items",
                column: "base_uom_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_code",
                schema: "master",
                table: "items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_tax_code_id",
                schema: "master",
                table: "items",
                column: "tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_branch_id",
                schema: "partnership",
                table: "production_cycles",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_contract_id",
                schema: "partnership",
                table: "production_cycles",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_coop_id_open",
                schema: "partnership",
                table: "production_cycles",
                column: "coop_id",
                unique: true,
                filter: "status IN ('Planned', 'Active', 'Harvesting')");

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_farmer_id",
                schema: "partnership",
                table: "production_cycles",
                column: "farmer_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_cycles_number",
                schema: "partnership",
                table: "production_cycles",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_codes_code",
                schema: "master",
                table: "tax_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_uoms_code",
                schema: "master",
                table: "uoms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_branches_branch_id",
                schema: "identity",
                table: "user_branches",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_code",
                schema: "master",
                table: "vendors",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_branch_id",
                schema: "master",
                table: "warehouses",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_code",
                schema: "master",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_coop_id",
                schema: "master",
                table: "warehouses",
                column: "coop_id",
                unique: true,
                filter: "coop_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_incentives",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "contract_input_prices",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "contract_live_bird_prices",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "master");

            migrationBuilder.DropTable(
                name: "item_uom_conversions",
                schema: "master");

            migrationBuilder.DropTable(
                name: "production_cycles",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "tax_rates",
                schema: "master");

            migrationBuilder.DropTable(
                name: "user_branches",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "vendors",
                schema: "master");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "master");

            migrationBuilder.DropTable(
                name: "items",
                schema: "master");

            migrationBuilder.DropTable(
                name: "contracts",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "coops",
                schema: "master");

            migrationBuilder.DropTable(
                name: "uoms",
                schema: "master");

            migrationBuilder.DropTable(
                name: "tax_codes",
                schema: "master");

            migrationBuilder.DropTable(
                name: "farmers",
                schema: "master");

            migrationBuilder.DropTable(
                name: "branches",
                schema: "master");
        }
    }
}
