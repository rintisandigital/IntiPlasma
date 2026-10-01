using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_PayablesCashBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "price_tolerance_percent",
                schema: "master",
                table: "vendors",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "credited_amount",
                schema: "sales",
                table: "sales_invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "credited_amount",
                schema: "sales",
                table: "sales_invoice_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_invoiced",
                schema: "inventory",
                table: "goods_receipt_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "value_invoiced",
                schema: "inventory",
                table: "goods_receipt_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "advance_amount",
                schema: "finance",
                table: "customer_receipts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "applied_advance_amount",
                schema: "finance",
                table: "customer_receipts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "finance",
                table: "customer_receipts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Posted");

            migrationBuilder.AddColumn<DateOnly>(
                name: "void_date",
                schema: "finance",
                table: "customer_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                schema: "finance",
                table: "customer_receipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_bank_accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_bank_accounts_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_bank_accounts_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_advance_applications",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_advance_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_advance_applications_customer_receipts_customer_re",
                        column: x => x.customer_receipt_id,
                        principalSchema: "finance",
                        principalTable: "customer_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_advance_applications_sales_invoices_sales_invoice_",
                        column: x => x.sales_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_credit_notes",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_credit_notes_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_credit_notes_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "master",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_credit_notes_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vendor_invoices",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tax_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    income_tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    income_tax_rate_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    max_price_deviation_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    price_variance_approval_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    goods_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_invoices_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoices_tax_codes_income_tax_code_id",
                        column: x => x.income_tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoices_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalSchema: "master",
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_reconciliations",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    statement_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_reconciliations", x => x.id);
                    table.ForeignKey(
                        name: "fk_bank_reconciliations_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bank_reconciliations_cash_bank_accounts_cash_bank_account_id",
                        column: x => x.cash_bank_account_id,
                        principalSchema: "finance",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_transfers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_cash_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_cash_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_transfers", x => x.id);
                    table.ForeignKey(
                        name: "fk_bank_transfers_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bank_transfers_cash_bank_accounts_from_cash_bank_account_id",
                        column: x => x.from_cash_bank_account_id,
                        principalSchema: "finance",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bank_transfers_cash_bank_accounts_to_cash_bank_account_id",
                        column: x => x.to_cash_bank_account_id,
                        principalSchema: "finance",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_transactions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_transactions_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_transactions_cash_bank_accounts_cash_bank_account_id",
                        column: x => x.cash_bank_account_id,
                        principalSchema: "finance",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_vouchers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_by = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_vouchers_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_vouchers_cash_bank_accounts_cash_bank_account_id",
                        column: x => x.cash_bank_account_id,
                        principalSchema: "finance",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_vouchers_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalSchema: "master",
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_credit_note_lines",
                schema: "sales",
                columns: table => new
                {
                    sales_credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_line_number = table.Column<int>(type: "integer", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_credit_note_lines", x => new { x.sales_credit_note_id, x.invoice_line_number });
                    table.ForeignKey(
                        name: "fk_sales_credit_note_lines_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_credit_note_lines_sales_credit_notes_sales_credit_not",
                        column: x => x.sales_credit_note_id,
                        principalSchema: "sales",
                        principalTable: "sales_credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_invoice_lines",
                schema: "finance",
                columns: table => new
                {
                    vendor_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_number = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_number = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_rate_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    price_deviation_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    goods_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    order_unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_tax_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_invoice_lines", x => new { x.vendor_invoice_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inventory",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalSchema: "master",
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_invoice_lines_vendor_invoices_vendor_invoice_id",
                        column: x => x.vendor_invoice_id,
                        principalSchema: "finance",
                        principalTable: "vendor_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_lines",
                schema: "finance",
                columns: table => new
                {
                    bank_reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    matched_journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_journal_line_number = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_statement_lines", x => new { x.bank_reconciliation_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_bank_statement_lines_bank_reconciliations_bank_reconciliati",
                        column: x => x.bank_reconciliation_id,
                        principalSchema: "finance",
                        principalTable: "bank_reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bank_statement_lines_journal_entries_matched_journal_entry_",
                        column: x => x.matched_journal_entry_id,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_transaction_lines",
                schema: "finance",
                columns: table => new
                {
                    cash_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_center_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_transaction_lines", x => new { x.cash_transaction_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_cash_transaction_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_transaction_lines_cash_transactions_cash_transaction_id",
                        column: x => x.cash_transaction_id,
                        principalSchema: "finance",
                        principalTable: "cash_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cash_transaction_lines_cost_centers_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "finance",
                        principalTable: "cost_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_voucher_allocations",
                schema: "finance",
                columns: table => new
                {
                    payment_voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_voucher_allocations", x => new { x.payment_voucher_id, x.vendor_invoice_id });
                    table.ForeignKey(
                        name: "fk_payment_voucher_allocations_payment_vouchers_payment_vouche",
                        column: x => x.payment_voucher_id,
                        principalSchema: "finance",
                        principalTable: "payment_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_payment_voucher_allocations_vendor_invoices_vendor_invoice_",
                        column: x => x.vendor_invoice_id,
                        principalSchema: "finance",
                        principalTable: "vendor_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipts_cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts",
                column: "cash_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_reconciliations_branch_id",
                schema: "finance",
                table: "bank_reconciliations",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_reconciliations_cash_bank_account_id_in_progress",
                schema: "finance",
                table: "bank_reconciliations",
                column: "cash_bank_account_id",
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "ix_bank_reconciliations_cash_bank_account_id_statement_date",
                schema: "finance",
                table: "bank_reconciliations",
                columns: new[] { "cash_bank_account_id", "statement_date" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_lines_matched_journal_entry_id_matched_journ",
                schema: "finance",
                table: "bank_statement_lines",
                columns: new[] { "matched_journal_entry_id", "matched_journal_line_number" },
                unique: true,
                filter: "matched_journal_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfers_branch_id_date",
                schema: "finance",
                table: "bank_transfers",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfers_from_cash_bank_account_id",
                schema: "finance",
                table: "bank_transfers",
                column: "from_cash_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfers_number",
                schema: "finance",
                table: "bank_transfers",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfers_to_cash_bank_account_id",
                schema: "finance",
                table: "bank_transfers",
                column: "to_cash_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_bank_accounts_account_id",
                schema: "finance",
                table: "cash_bank_accounts",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cash_bank_accounts_branch_id",
                schema: "finance",
                table: "cash_bank_accounts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_bank_accounts_code",
                schema: "finance",
                table: "cash_bank_accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cash_transaction_lines_account_id",
                schema: "finance",
                table: "cash_transaction_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_transaction_lines_cost_center_id",
                schema: "finance",
                table: "cash_transaction_lines",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_branch_id_date",
                schema: "finance",
                table: "cash_transactions",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_cash_bank_account_id_date",
                schema: "finance",
                table: "cash_transactions",
                columns: new[] { "cash_bank_account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_number",
                schema: "finance",
                table: "cash_transactions",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_advance_applications_customer_receipt_id",
                schema: "finance",
                table: "customer_advance_applications",
                column: "customer_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_advance_applications_sales_invoice_id",
                schema: "finance",
                table: "customer_advance_applications",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_voucher_allocations_vendor_invoice_id",
                schema: "finance",
                table: "payment_voucher_allocations",
                column: "vendor_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_branch_id_payment_date",
                schema: "finance",
                table: "payment_vouchers",
                columns: new[] { "branch_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_cash_bank_account_id",
                schema: "finance",
                table: "payment_vouchers",
                column: "cash_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_number",
                schema: "finance",
                table: "payment_vouchers",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_vendor_id_status",
                schema: "finance",
                table: "payment_vouchers",
                columns: new[] { "vendor_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_credit_note_lines_cycle_id",
                schema: "sales",
                table: "sales_credit_note_lines",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_credit_notes_branch_id",
                schema: "sales",
                table: "sales_credit_notes",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_credit_notes_customer_id_date",
                schema: "sales",
                table: "sales_credit_notes",
                columns: new[] { "customer_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_credit_notes_number",
                schema: "sales",
                table: "sales_credit_notes",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_credit_notes_sales_invoice_id",
                schema: "sales",
                table: "sales_credit_notes",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoice_lines_goods_receipt_id_goods_receipt_line_nu",
                schema: "finance",
                table: "vendor_invoice_lines",
                columns: new[] { "goods_receipt_id", "goods_receipt_line_number" });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoice_lines_item_id",
                schema: "finance",
                table: "vendor_invoice_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoice_lines_purchase_order_id",
                schema: "finance",
                table: "vendor_invoice_lines",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoice_lines_tax_code_id",
                schema: "finance",
                table: "vendor_invoice_lines",
                column: "tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoice_lines_uom_id",
                schema: "finance",
                table: "vendor_invoice_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoices_branch_id_invoice_date",
                schema: "finance",
                table: "vendor_invoices",
                columns: new[] { "branch_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoices_income_tax_code_id",
                schema: "finance",
                table: "vendor_invoices",
                column: "income_tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoices_number",
                schema: "finance",
                table: "vendor_invoices",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoices_vendor_id_status",
                schema: "finance",
                table: "vendor_invoices",
                columns: new[] { "vendor_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_invoices_vendor_id_vendor_invoice_number_active",
                schema: "finance",
                table: "vendor_invoices",
                columns: new[] { "vendor_id", "vendor_invoice_number" },
                unique: true,
                filter: "status <> 'Cancelled'");

            migrationBuilder.AddForeignKey(
                name: "fk_customer_receipts_cash_bank_accounts_cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts",
                column: "cash_bank_account_id",
                principalSchema: "finance",
                principalTable: "cash_bank_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customer_receipts_cash_bank_accounts_cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropTable(
                name: "bank_statement_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "bank_transfers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "cash_transaction_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "customer_advance_applications",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payment_voucher_allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "sales_credit_note_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "vendor_invoice_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "bank_reconciliations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "cash_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payment_vouchers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "sales_credit_notes",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "vendor_invoices",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "cash_bank_accounts",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_customer_receipts_cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "price_tolerance_percent",
                schema: "master",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "credited_amount",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "credited_amount",
                schema: "sales",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "quantity_invoiced",
                schema: "inventory",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "value_invoiced",
                schema: "inventory",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "advance_amount",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "applied_advance_amount",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "cash_bank_account_id",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "void_date",
                schema: "finance",
                table: "customer_receipts");

            migrationBuilder.DropColumn(
                name: "void_reason",
                schema: "finance",
                table: "customer_receipts");
        }
    }
}
