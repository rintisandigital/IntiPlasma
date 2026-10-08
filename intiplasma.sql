--
-- PostgreSQL database dump
--

-- Dumped from database version 15.2
-- Dumped by pg_dump version 15.2

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: costing; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA costing;


ALTER SCHEMA costing OWNER TO postgres;

--
-- Name: documents; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA documents;


ALTER SCHEMA documents OWNER TO postgres;

--
-- Name: finance; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA finance;


ALTER SCHEMA finance OWNER TO postgres;

--
-- Name: identity; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA identity;


ALTER SCHEMA identity OWNER TO postgres;

--
-- Name: infrastructure; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA infrastructure;


ALTER SCHEMA infrastructure OWNER TO postgres;

--
-- Name: inventory; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA inventory;


ALTER SCHEMA inventory OWNER TO postgres;

--
-- Name: master; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA master;


ALTER SCHEMA master OWNER TO postgres;

--
-- Name: partnership; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA partnership;


ALTER SCHEMA partnership OWNER TO postgres;

--
-- Name: procurement; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA procurement;


ALTER SCHEMA procurement OWNER TO postgres;

--
-- Name: production; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA production;


ALTER SCHEMA production OWNER TO postgres;

--
-- Name: sales; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA sales;


ALTER SCHEMA sales OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: plasma_settlement_lines; Type: TABLE; Schema: costing; Owner: postgres
--

CREATE TABLE costing.plasma_settlement_lines (
    plasma_settlement_id uuid NOT NULL,
    line_number integer NOT NULL,
    type character varying(30) NOT NULL,
    description character varying(250) NOT NULL,
    quantity numeric(18,4),
    unit_price numeric(18,2),
    amount numeric(18,2) NOT NULL
);


ALTER TABLE costing.plasma_settlement_lines OWNER TO postgres;

--
-- Name: plasma_settlements; Type: TABLE; Schema: costing; Owner: postgres
--

CREATE TABLE costing.plasma_settlements (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    farmer_id uuid NOT NULL,
    contract_id uuid NOT NULL,
    scheme character varying(30) NOT NULL,
    settlement_date date NOT NULL,
    status character varying(30) NOT NULL,
    notes character varying(1000),
    income_tax_code_id uuid,
    income_tax_rate_percent numeric(7,4) NOT NULL,
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    debt_deduction numeric(18,2) NOT NULL,
    deficit numeric(18,2) NOT NULL,
    gross_income numeric(18,2) NOT NULL,
    income_tax_amount numeric(18,2) NOT NULL,
    net_payable numeric(18,2) NOT NULL,
    paid_amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE costing.plasma_settlements OWNER TO postgres;

--
-- Name: attachment_links; Type: TABLE; Schema: documents; Owner: postgres
--

CREATE TABLE documents.attachment_links (
    attachment_id uuid NOT NULL,
    owner_type character varying(50) NOT NULL,
    owner_id uuid NOT NULL,
    owner_key character varying(20) NOT NULL
);


ALTER TABLE documents.attachment_links OWNER TO postgres;

--
-- Name: attachments; Type: TABLE; Schema: documents; Owner: postgres
--

CREATE TABLE documents.attachments (
    id uuid NOT NULL,
    file_name character varying(255) NOT NULL,
    stored_file_name character varying(60) NOT NULL,
    extension character varying(10) NOT NULL,
    content_type character varying(100) NOT NULL,
    size_bytes bigint NOT NULL,
    storage_path character varying(200) NOT NULL,
    checksum character varying(64) NOT NULL,
    kind character varying(30) NOT NULL,
    branch_id uuid,
    description character varying(500),
    status character varying(30) NOT NULL,
    unlinked_at_utc timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at_utc timestamp with time zone,
    deleted_by uuid,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE documents.attachments OWNER TO postgres;

--
-- Name: accounts; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.accounts (
    id uuid NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(150) NOT NULL,
    type character varying(30) NOT NULL,
    normal_balance character varying(30) NOT NULL,
    parent_id uuid,
    is_postable boolean NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    cash_flow_category character varying(30) DEFAULT 'Operating'::character varying NOT NULL
);


ALTER TABLE finance.accounts OWNER TO postgres;

--
-- Name: bank_reconciliations; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.bank_reconciliations (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    cash_bank_account_id uuid NOT NULL,
    statement_date date NOT NULL,
    status character varying(30) NOT NULL,
    completed_by uuid,
    completed_at_utc timestamp with time zone,
    statement_balance numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.bank_reconciliations OWNER TO postgres;

--
-- Name: bank_statement_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.bank_statement_lines (
    bank_reconciliation_id uuid NOT NULL,
    line_number integer NOT NULL,
    date date NOT NULL,
    description character varying(250) NOT NULL,
    matched_journal_entry_id uuid,
    matched_journal_line_number integer,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.bank_statement_lines OWNER TO postgres;

--
-- Name: bank_transfers; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.bank_transfers (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    from_cash_bank_account_id uuid NOT NULL,
    to_cash_bank_account_id uuid NOT NULL,
    date date NOT NULL,
    reference character varying(100),
    notes character varying(1000),
    amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.bank_transfers OWNER TO postgres;

--
-- Name: cash_bank_accounts; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.cash_bank_accounts (
    id uuid NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(150) NOT NULL,
    type character varying(30) NOT NULL,
    branch_id uuid NOT NULL,
    account_id uuid NOT NULL,
    bank_name character varying(100),
    account_number character varying(40),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.cash_bank_accounts OWNER TO postgres;

--
-- Name: cash_transaction_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.cash_transaction_lines (
    cash_transaction_id uuid NOT NULL,
    line_number integer NOT NULL,
    account_id uuid NOT NULL,
    cost_center_id uuid,
    description character varying(250),
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.cash_transaction_lines OWNER TO postgres;

--
-- Name: cash_transactions; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.cash_transactions (
    id uuid NOT NULL,
    number character varying(50),
    branch_id uuid NOT NULL,
    cash_bank_account_id uuid NOT NULL,
    direction character varying(30) NOT NULL,
    date date NOT NULL,
    description character varying(500) NOT NULL,
    reference character varying(100),
    status character varying(30) NOT NULL,
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    posted_by uuid,
    posted_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE finance.cash_transactions OWNER TO postgres;

--
-- Name: cost_centers; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.cost_centers (
    id uuid NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.cost_centers OWNER TO postgres;

--
-- Name: customer_advance_applications; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.customer_advance_applications (
    id uuid NOT NULL,
    customer_receipt_id uuid NOT NULL,
    sales_invoice_id uuid NOT NULL,
    date date NOT NULL,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.customer_advance_applications OWNER TO postgres;

--
-- Name: customer_receipt_allocations; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.customer_receipt_allocations (
    customer_receipt_id uuid NOT NULL,
    sales_invoice_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.customer_receipt_allocations OWNER TO postgres;

--
-- Name: customer_receipts; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.customer_receipts (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    receipt_date date NOT NULL,
    cash_account_id uuid NOT NULL,
    reference character varying(100),
    notes character varying(1000),
    amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    advance_amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    applied_advance_amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    cash_bank_account_id uuid,
    status character varying(30) DEFAULT 'Posted'::character varying NOT NULL,
    void_date date,
    void_reason character varying(500),
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE finance.customer_receipts OWNER TO postgres;

--
-- Name: fiscal_periods; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.fiscal_periods (
    id uuid NOT NULL,
    year integer NOT NULL,
    month integer NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    status character varying(30) NOT NULL,
    closed_at_utc timestamp with time zone,
    closed_by uuid,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.fiscal_periods OWNER TO postgres;

--
-- Name: journal_entries; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_entries (
    id uuid NOT NULL,
    number character varying(50),
    branch_id uuid NOT NULL,
    date date NOT NULL,
    description character varying(500) NOT NULL,
    source character varying(30) NOT NULL,
    source_type character varying(80),
    source_id uuid,
    status character varying(30) NOT NULL,
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    posted_by uuid,
    posted_at_utc timestamp with time zone,
    reversal_of_id uuid,
    reversed_by_id uuid,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE finance.journal_entries OWNER TO postgres;

--
-- Name: journal_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_lines (
    journal_entry_id uuid NOT NULL,
    line_number integer NOT NULL,
    account_id uuid NOT NULL,
    cost_center_id uuid,
    description character varying(250),
    credit numeric(18,2) NOT NULL,
    debit numeric(18,2) NOT NULL
);


ALTER TABLE finance.journal_lines OWNER TO postgres;

--
-- Name: journal_mapping_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_mapping_lines (
    journal_mapping_id uuid NOT NULL,
    component character varying(50) NOT NULL,
    debit_account_id uuid NOT NULL,
    credit_account_id uuid NOT NULL,
    cost_center_id uuid
);


ALTER TABLE finance.journal_mapping_lines OWNER TO postgres;

--
-- Name: journal_mappings; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_mappings (
    id uuid NOT NULL,
    event_type character varying(50) NOT NULL,
    branch_id uuid,
    description character varying(500),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.journal_mappings OWNER TO postgres;

--
-- Name: journal_template_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_template_lines (
    journal_template_id uuid NOT NULL,
    line_number integer NOT NULL,
    account_id uuid NOT NULL,
    cost_center_id uuid,
    side character varying(30) NOT NULL,
    description character varying(250)
);


ALTER TABLE finance.journal_template_lines OWNER TO postgres;

--
-- Name: journal_templates; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.journal_templates (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE finance.journal_templates OWNER TO postgres;

--
-- Name: payment_voucher_allocations; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.payment_voucher_allocations (
    payment_voucher_id uuid NOT NULL,
    vendor_invoice_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.payment_voucher_allocations OWNER TO postgres;

--
-- Name: payment_voucher_settlement_allocations; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.payment_voucher_settlement_allocations (
    payment_voucher_id uuid NOT NULL,
    plasma_settlement_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE finance.payment_voucher_settlement_allocations OWNER TO postgres;

--
-- Name: payment_vouchers; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.payment_vouchers (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    vendor_id uuid,
    cash_bank_account_id uuid NOT NULL,
    payment_date date NOT NULL,
    reference character varying(100),
    notes character varying(1000),
    status character varying(30) NOT NULL,
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    paid_by uuid,
    paid_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    farmer_id uuid,
    payee_type character varying(30) DEFAULT 'Vendor'::character varying NOT NULL,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE finance.payment_vouchers OWNER TO postgres;

--
-- Name: vendor_invoice_lines; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.vendor_invoice_lines (
    vendor_invoice_id uuid NOT NULL,
    line_number integer NOT NULL,
    goods_receipt_id uuid NOT NULL,
    goods_receipt_line_number integer NOT NULL,
    purchase_order_id uuid NOT NULL,
    purchase_order_line_number integer NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    tax_code_id uuid,
    vat_rate_percent numeric(7,4) NOT NULL,
    price_deviation_percent numeric(9,4) NOT NULL,
    amount numeric(18,2) NOT NULL,
    goods_value numeric(18,2) NOT NULL,
    order_unit_price numeric(18,2) NOT NULL,
    unit_price numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL,
    vat_tax_base numeric(18,2) NOT NULL
);


ALTER TABLE finance.vendor_invoice_lines OWNER TO postgres;

--
-- Name: vendor_invoices; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.vendor_invoices (
    id uuid NOT NULL,
    number character varying(50),
    branch_id uuid NOT NULL,
    vendor_id uuid NOT NULL,
    vendor_invoice_number character varying(50) NOT NULL,
    tax_invoice_number character varying(50),
    invoice_date date NOT NULL,
    due_date date NOT NULL,
    status character varying(30) NOT NULL,
    notes character varying(1000),
    income_tax_code_id uuid,
    income_tax_rate_percent numeric(7,4) NOT NULL,
    max_price_deviation_percent numeric(9,4) NOT NULL,
    price_variance_approval_reason character varying(500),
    posted_by uuid,
    posted_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    goods_value numeric(18,2) NOT NULL,
    income_tax_amount numeric(18,2) NOT NULL,
    paid_amount numeric(18,2) NOT NULL,
    subtotal numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE finance.vendor_invoices OWNER TO postgres;

--
-- Name: branch_access_profile_branches; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.branch_access_profile_branches (
    profile_id uuid NOT NULL,
    branch_id uuid NOT NULL
);


ALTER TABLE identity.branch_access_profile_branches OWNER TO postgres;

--
-- Name: branch_access_profiles; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.branch_access_profiles (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    all_branches boolean NOT NULL,
    is_system boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE identity.branch_access_profiles OWNER TO postgres;

--
-- Name: menu_access_profile_items; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.menu_access_profile_items (
    profile_id uuid NOT NULL,
    menu_id uuid NOT NULL,
    can_view boolean NOT NULL,
    can_create boolean NOT NULL,
    can_edit boolean NOT NULL,
    can_delete boolean NOT NULL,
    can_export boolean NOT NULL
);


ALTER TABLE identity.menu_access_profile_items OWNER TO postgres;

--
-- Name: menu_access_profiles; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.menu_access_profiles (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    is_system boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE identity.menu_access_profiles OWNER TO postgres;

--
-- Name: menus; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.menus (
    id uuid NOT NULL,
    code character varying(100) NOT NULL,
    parent_code character varying(100),
    name character varying(100) NOT NULL,
    default_name character varying(100) NOT NULL,
    icon character varying(100),
    route character varying(200),
    sort_order integer NOT NULL,
    is_active boolean NOT NULL,
    is_available boolean NOT NULL,
    in_catalog boolean NOT NULL,
    supports_create boolean NOT NULL,
    supports_edit boolean NOT NULL,
    supports_delete boolean NOT NULL,
    supports_export boolean NOT NULL,
    is_customized boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE identity.menus OWNER TO postgres;

--
-- Name: refresh_tokens; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.refresh_tokens (
    id uuid NOT NULL,
    token character varying(200) NOT NULL,
    user_id uuid NOT NULL,
    expires_on_utc timestamp with time zone NOT NULL
);


ALTER TABLE identity.refresh_tokens OWNER TO postgres;

--
-- Name: role_permissions; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.role_permissions (
    role_id uuid NOT NULL,
    permission character varying(100) NOT NULL
);


ALTER TABLE identity.role_permissions OWNER TO postgres;

--
-- Name: roles; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.roles (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    is_system boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE identity.roles OWNER TO postgres;

--
-- Name: user_old; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.user_old (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    email character varying(256) NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    password_hash text NOT NULL,
    is_active boolean NOT NULL,
    menu_access_profile_id uuid,
    branch_access_profile_id uuid,
    default_branch_id uuid,
    role_ids uuid[] NOT NULL,
    role_names text[] NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    deleted_at_utc timestamp with time zone NOT NULL,
    deleted_by uuid,
    reason character varying(500)
);


ALTER TABLE identity.user_old OWNER TO postgres;

--
-- Name: user_roles; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.user_roles (
    user_id uuid NOT NULL,
    role_id uuid NOT NULL
);


ALTER TABLE identity.user_roles OWNER TO postgres;

--
-- Name: users; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.users (
    id uuid NOT NULL,
    email character varying(256) NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    password_hash text NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    is_active boolean DEFAULT true NOT NULL,
    security_stamp character varying(32) DEFAULT ''::character varying NOT NULL,
    branch_access_profile_id uuid,
    default_branch_id uuid,
    menu_access_profile_id uuid,
    access_failed_count integer DEFAULT 0 NOT NULL,
    lockout_end_utc timestamp with time zone
);


ALTER TABLE identity.users OWNER TO postgres;

--
-- Name: audit_logs; Type: TABLE; Schema: infrastructure; Owner: postgres
--

CREATE TABLE infrastructure.audit_logs (
    id uuid NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL,
    category character varying(30) NOT NULL,
    action character varying(100) NOT NULL,
    user_id uuid,
    user_email character varying(256),
    entity_type character varying(100),
    entity_id uuid,
    summary character varying(500) NOT NULL,
    details jsonb,
    ip_address character varying(64),
    source character varying(50) NOT NULL
);


ALTER TABLE infrastructure.audit_logs OWNER TO postgres;

--
-- Name: data_protection_keys; Type: TABLE; Schema: infrastructure; Owner: postgres
--

CREATE TABLE infrastructure.data_protection_keys (
    id integer NOT NULL,
    friendly_name text,
    xml text
);


ALTER TABLE infrastructure.data_protection_keys OWNER TO postgres;

--
-- Name: data_protection_keys_id_seq; Type: SEQUENCE; Schema: infrastructure; Owner: postgres
--

ALTER TABLE infrastructure.data_protection_keys ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME infrastructure.data_protection_keys_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: document_sequences; Type: TABLE; Schema: infrastructure; Owner: postgres
--

CREATE TABLE infrastructure.document_sequences (
    key character varying(100) NOT NULL,
    last_value bigint NOT NULL
);


ALTER TABLE infrastructure.document_sequences OWNER TO postgres;

--
-- Name: outbox_messages; Type: TABLE; Schema: infrastructure; Owner: postgres
--

CREATE TABLE infrastructure.outbox_messages (
    id uuid NOT NULL,
    type character varying(500) NOT NULL,
    content jsonb NOT NULL,
    occurred_on_utc timestamp with time zone NOT NULL,
    processed_on_utc timestamp with time zone,
    attempts integer NOT NULL,
    error text
);


ALTER TABLE infrastructure.outbox_messages OWNER TO postgres;

--
-- Name: goods_receipt_lines; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.goods_receipt_lines (
    goods_receipt_id uuid NOT NULL,
    line_number integer NOT NULL,
    purchase_order_line_number integer NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    base_quantity numeric(18,4) NOT NULL,
    unit_cost numeric(18,6) NOT NULL,
    value numeric(18,2) NOT NULL,
    quantity_invoiced numeric(18,4) DEFAULT 0.0 NOT NULL,
    value_invoiced numeric(18,2) DEFAULT 0.0 NOT NULL
);


ALTER TABLE inventory.goods_receipt_lines OWNER TO postgres;

--
-- Name: goods_receipts; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.goods_receipts (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    vendor_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    cycle_id uuid,
    receipt_date date NOT NULL,
    delivery_note_number character varying(50),
    notes character varying(1000),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE inventory.goods_receipts OWNER TO postgres;

--
-- Name: stock_balances; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_balances (
    id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    item_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    value numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE inventory.stock_balances OWNER TO postgres;

--
-- Name: stock_ledger_entries; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_ledger_entries (
    id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    item_id uuid NOT NULL,
    date date NOT NULL,
    type character varying(30) NOT NULL,
    source_type character varying(50) NOT NULL,
    source_id uuid NOT NULL,
    source_number character varying(50) NOT NULL,
    cycle_id uuid,
    quantity numeric(18,4) NOT NULL,
    unit_cost numeric(18,6) NOT NULL,
    balance_quantity numeric(18,4) NOT NULL,
    balance_value numeric(18,2) NOT NULL,
    value numeric(18,2) NOT NULL
);


ALTER TABLE inventory.stock_ledger_entries OWNER TO postgres;

--
-- Name: stock_return_lines; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_return_lines (
    stock_return_id uuid NOT NULL,
    line_number integer NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    base_quantity numeric(18,4) NOT NULL,
    unit_cost numeric(18,6) NOT NULL,
    value numeric(18,2) NOT NULL
);


ALTER TABLE inventory.stock_return_lines OWNER TO postgres;

--
-- Name: stock_returns; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_returns (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    from_warehouse_id uuid NOT NULL,
    to_warehouse_id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    return_date date NOT NULL,
    reason character varying(300) NOT NULL,
    notes character varying(1000),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE inventory.stock_returns OWNER TO postgres;

--
-- Name: stock_transfer_lines; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_transfer_lines (
    stock_transfer_id uuid NOT NULL,
    line_number integer NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    base_quantity numeric(18,4) NOT NULL,
    unit_cost numeric(18,6) NOT NULL,
    value numeric(18,2) NOT NULL
);


ALTER TABLE inventory.stock_transfer_lines OWNER TO postgres;

--
-- Name: stock_transfers; Type: TABLE; Schema: inventory; Owner: postgres
--

CREATE TABLE inventory.stock_transfers (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    from_warehouse_id uuid NOT NULL,
    to_warehouse_id uuid NOT NULL,
    cycle_id uuid,
    transfer_date date NOT NULL,
    notes character varying(1000),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE inventory.stock_transfers OWNER TO postgres;

--
-- Name: branches; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.branches (
    id uuid NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    address character varying(500),
    phone character varying(30),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE master.branches OWNER TO postgres;

--
-- Name: coops; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.coops (
    id uuid NOT NULL,
    code character varying(25) NOT NULL,
    name character varying(100) NOT NULL,
    farmer_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    capacity integer NOT NULL,
    house_type character varying(30) NOT NULL,
    address character varying(500),
    latitude numeric(9,6),
    longitude numeric(9,6),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE master.coops OWNER TO postgres;

--
-- Name: customers; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.customers (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(150) NOT NULL,
    address character varying(500),
    phone character varying(30),
    email character varying(256),
    payment_term_days integer NOT NULL,
    is_active boolean NOT NULL,
    credit_limit numeric(18,2) NOT NULL,
    tax_identity_is_pkp boolean NOT NULL,
    tax_identity_nitku character varying(22),
    tax_identity_npwp character varying(16),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE master.customers OWNER TO postgres;

--
-- Name: farmers; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.farmers (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(150) NOT NULL,
    type character varying(30) NOT NULL,
    branch_id uuid NOT NULL,
    nik character varying(16),
    address character varying(500),
    phone character varying(30),
    is_active boolean NOT NULL,
    bank_account_account_holder_name character varying(150),
    bank_account_account_number character varying(40),
    bank_account_bank_name character varying(100),
    tax_identity_is_pkp boolean NOT NULL,
    tax_identity_nitku character varying(22),
    tax_identity_npwp character varying(16),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE master.farmers OWNER TO postgres;

--
-- Name: item_uom_conversions; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.item_uom_conversions (
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    factor numeric(18,6) NOT NULL
);


ALTER TABLE master.item_uom_conversions OWNER TO postgres;

--
-- Name: items; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.items (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(150) NOT NULL,
    category character varying(30) NOT NULL,
    base_uom_id uuid NOT NULL,
    tax_code_id uuid,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE master.items OWNER TO postgres;

--
-- Name: tax_codes; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.tax_codes (
    id uuid NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    type character varying(30) NOT NULL,
    vat_treatment character varying(30),
    income_tax_article character varying(30),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE master.tax_codes OWNER TO postgres;

--
-- Name: tax_rates; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.tax_rates (
    tax_code_id uuid NOT NULL,
    effective_from date NOT NULL,
    rate_percent numeric(7,4) NOT NULL,
    tax_base_ratio numeric(10,8) NOT NULL
);


ALTER TABLE master.tax_rates OWNER TO postgres;

--
-- Name: uoms; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.uoms (
    id uuid NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE master.uoms OWNER TO postgres;

--
-- Name: vendors; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.vendors (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(150) NOT NULL,
    address character varying(500),
    phone character varying(30),
    email character varying(256),
    payment_term_days integer NOT NULL,
    is_active boolean NOT NULL,
    bank_account_account_holder_name character varying(150),
    bank_account_account_number character varying(40),
    bank_account_bank_name character varying(100),
    tax_identity_is_pkp boolean NOT NULL,
    tax_identity_nitku character varying(22),
    tax_identity_npwp character varying(16),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    price_tolerance_percent numeric(5,2) DEFAULT 0.0 NOT NULL,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE master.vendors OWNER TO postgres;

--
-- Name: warehouses; Type: TABLE; Schema: master; Owner: postgres
--

CREATE TABLE master.warehouses (
    id uuid NOT NULL,
    code character varying(40) NOT NULL,
    name character varying(150) NOT NULL,
    branch_id uuid NOT NULL,
    type character varying(30) NOT NULL,
    coop_id uuid,
    address character varying(500),
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE master.warehouses OWNER TO postgres;

--
-- Name: contract_incentives; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.contract_incentives (
    contract_id uuid NOT NULL,
    line_number integer NOT NULL,
    name character varying(100) NOT NULL,
    kind character varying(30) NOT NULL,
    metric character varying(30) NOT NULL,
    range_from numeric(18,4),
    range_to numeric(18,4),
    basis character varying(30) NOT NULL,
    amount numeric(18,2) NOT NULL
);


ALTER TABLE partnership.contract_incentives OWNER TO postgres;

--
-- Name: contract_input_prices; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.contract_input_prices (
    contract_id uuid NOT NULL,
    item_id uuid NOT NULL,
    price numeric(18,2) NOT NULL
);


ALTER TABLE partnership.contract_input_prices OWNER TO postgres;

--
-- Name: contract_live_bird_prices; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.contract_live_bird_prices (
    contract_id uuid NOT NULL,
    min_weight_kg numeric(10,3) NOT NULL,
    max_weight_kg numeric(10,3) NOT NULL,
    price_per_kg numeric(18,2) NOT NULL
);


ALTER TABLE partnership.contract_live_bird_prices OWNER TO postgres;

--
-- Name: contracts; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.contracts (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(150) NOT NULL,
    branch_id uuid NOT NULL,
    scheme character varying(30) NOT NULL,
    status character varying(30) NOT NULL,
    valid_from date NOT NULL,
    valid_to date,
    plasma_profit_share_percent numeric(7,4),
    income_tax_code_id uuid,
    notes character varying(1000),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE partnership.contracts OWNER TO postgres;

--
-- Name: cycle_harvests; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.cycle_harvests (
    id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    date date NOT NULL,
    age_days integer NOT NULL,
    birds integer NOT NULL,
    weight_kg numeric(14,3) NOT NULL,
    notes character varying(500),
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE partnership.cycle_harvests OWNER TO postgres;

--
-- Name: production_cycles; Type: TABLE; Schema: partnership; Owner: postgres
--

CREATE TABLE partnership.production_cycles (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    farmer_id uuid NOT NULL,
    coop_id uuid NOT NULL,
    contract_id uuid,
    contract_snapshot jsonb,
    status character varying(30) NOT NULL,
    planned_chick_in_date date NOT NULL,
    planned_population integer NOT NULL,
    chick_in_date date,
    initial_population integer,
    notes character varying(1000),
    cancellation_reason character varying(500),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    closed_date date,
    closing_performance jsonb,
    harvested_birds integer DEFAULT 0 NOT NULL,
    harvested_weight_kg numeric(14,3) DEFAULT 0.0 NOT NULL,
    total_culling integer DEFAULT 0 NOT NULL,
    total_mortality integer DEFAULT 0 NOT NULL,
    closing_cost jsonb,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE partnership.production_cycles OWNER TO postgres;

--
-- Name: purchase_order_lines; Type: TABLE; Schema: procurement; Owner: postgres
--

CREATE TABLE procurement.purchase_order_lines (
    purchase_order_id uuid NOT NULL,
    line_number integer NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    tax_code_id uuid,
    quantity_received numeric(18,4) NOT NULL,
    unit_price numeric(18,2) NOT NULL
);


ALTER TABLE procurement.purchase_order_lines OWNER TO postgres;

--
-- Name: purchase_orders; Type: TABLE; Schema: procurement; Owner: postgres
--

CREATE TABLE procurement.purchase_orders (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    vendor_id uuid NOT NULL,
    order_date date NOT NULL,
    expected_date date,
    status character varying(30) NOT NULL,
    notes character varying(1000),
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE procurement.purchase_orders OWNER TO postgres;

--
-- Name: daily_recording_revisions; Type: TABLE; Schema: production; Owner: postgres
--

CREATE TABLE production.daily_recording_revisions (
    daily_recording_id uuid NOT NULL,
    revision_number integer NOT NULL,
    reason character varying(300) NOT NULL,
    previous_values jsonb NOT NULL,
    revised_by uuid,
    revised_at_utc timestamp with time zone NOT NULL,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE production.daily_recording_revisions OWNER TO postgres;

--
-- Name: daily_recording_usages; Type: TABLE; Schema: production; Owner: postgres
--

CREATE TABLE production.daily_recording_usages (
    daily_recording_id uuid NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    quantity numeric(18,4) NOT NULL,
    base_quantity numeric(18,4) NOT NULL,
    value numeric(18,2) NOT NULL
);


ALTER TABLE production.daily_recording_usages OWNER TO postgres;

--
-- Name: daily_recordings; Type: TABLE; Schema: production; Owner: postgres
--

CREATE TABLE production.daily_recordings (
    id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    coop_id uuid NOT NULL,
    date date NOT NULL,
    age_days integer NOT NULL,
    mortality integer NOT NULL,
    culling integer NOT NULL,
    average_body_weight_gram numeric(8,2),
    notes character varying(1000),
    revision_number integer NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE production.daily_recordings OWNER TO postgres;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: delivery_order_lines; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.delivery_order_lines (
    delivery_order_id uuid NOT NULL,
    line_number integer NOT NULL,
    sales_order_line_number integer NOT NULL,
    item_id uuid NOT NULL,
    harvest_id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    birds integer NOT NULL,
    weight_kg numeric(14,3) NOT NULL,
    tax_code_id uuid,
    is_cancelled boolean NOT NULL,
    amount numeric(18,2) NOT NULL,
    price_per_kg numeric(18,2) NOT NULL
);


ALTER TABLE sales.delivery_order_lines OWNER TO postgres;

--
-- Name: delivery_orders; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.delivery_orders (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    sales_order_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    delivery_date date NOT NULL,
    vehicle_number character varying(20),
    driver_name character varying(100),
    notes character varying(1000),
    status character varying(30) NOT NULL,
    sales_invoice_id uuid,
    cancellation_reason character varying(500),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE sales.delivery_orders OWNER TO postgres;

--
-- Name: sales_credit_note_lines; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_credit_note_lines (
    sales_credit_note_id uuid NOT NULL,
    invoice_line_number integer NOT NULL,
    cycle_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL
);


ALTER TABLE sales.sales_credit_note_lines OWNER TO postgres;

--
-- Name: sales_credit_notes; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_credit_notes (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    sales_invoice_id uuid NOT NULL,
    date date NOT NULL,
    reason character varying(500) NOT NULL,
    subtotal numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid
);


ALTER TABLE sales.sales_credit_notes OWNER TO postgres;

--
-- Name: sales_invoice_lines; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_invoice_lines (
    sales_invoice_id uuid NOT NULL,
    line_number integer NOT NULL,
    delivery_order_id uuid NOT NULL,
    delivery_order_line_number integer NOT NULL,
    item_id uuid NOT NULL,
    cycle_id uuid NOT NULL,
    birds integer NOT NULL,
    weight_kg numeric(14,3) NOT NULL,
    tax_code_id uuid,
    vat_rate_percent numeric(7,4) NOT NULL,
    amount numeric(18,2) NOT NULL,
    price_per_kg numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL,
    vat_tax_base numeric(18,2) NOT NULL,
    credited_amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    cost_amount numeric(18,2) DEFAULT 0.0 NOT NULL
);


ALTER TABLE sales.sales_invoice_lines OWNER TO postgres;

--
-- Name: sales_invoices; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_invoices (
    id uuid NOT NULL,
    number character varying(50),
    branch_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    invoice_date date NOT NULL,
    due_date date NOT NULL,
    status character varying(30) NOT NULL,
    notes character varying(1000),
    posted_by uuid,
    posted_at_utc timestamp with time zone,
    cancellation_reason character varying(500),
    paid_amount numeric(18,2) NOT NULL,
    subtotal numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    vat_amount numeric(18,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    credited_amount numeric(18,2) DEFAULT 0.0 NOT NULL
);


ALTER TABLE sales.sales_invoices OWNER TO postgres;

--
-- Name: sales_order_lines; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_order_lines (
    sales_order_id uuid NOT NULL,
    line_number integer NOT NULL,
    item_id uuid NOT NULL,
    birds integer NOT NULL,
    estimated_weight_kg numeric(14,3) NOT NULL,
    tax_code_id uuid,
    delivered_birds integer NOT NULL,
    delivered_weight_kg numeric(14,3) NOT NULL,
    price_per_kg numeric(18,2) NOT NULL
);


ALTER TABLE sales.sales_order_lines OWNER TO postgres;

--
-- Name: sales_orders; Type: TABLE; Schema: sales; Owner: postgres
--

CREATE TABLE sales.sales_orders (
    id uuid NOT NULL,
    number character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    order_date date NOT NULL,
    delivery_date date,
    status character varying(30) NOT NULL,
    notes character varying(1000),
    approved_by uuid,
    approved_at_utc timestamp with time zone,
    credit_override_reason character varying(500),
    cancellation_reason character varying(500),
    created_at_utc timestamp with time zone NOT NULL,
    created_by uuid,
    modified_at_utc timestamp with time zone,
    modified_by uuid,
    documents uuid[] DEFAULT '{}'::uuid[] NOT NULL
);


ALTER TABLE sales.sales_orders OWNER TO postgres;

--
-- Data for Name: plasma_settlement_lines; Type: TABLE DATA; Schema: costing; Owner: postgres
--

COPY costing.plasma_settlement_lines (plasma_settlement_id, line_number, type, description, quantity, unit_price, amount) FROM stdin;
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	1	LiveBirdValue	Panen 18/08/2026: 1592 ekor, BW 1.840 kg	2929.6000	20300.00	59470880.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	2	LiveBirdValue	Panen 19/08/2026: 1589 ekor, BW 1.918 kg	3048.2000	20300.00	61878460.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	3	LiveBirdValue	Panen 20/08/2026: 1588 ekor, BW 2.064 kg	3277.7000	20000.00	65554000.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	4	InputCharge	Doc DOC-CP707	5000.0000	7900.00	-39500000.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	5	InputCharge	Feed PKN-BR1	2898.4000	8600.00	-24926240.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	6	InputCharge	Feed PKN-BR2	10643.1000	8300.00	-88337730.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	7	InputCharge	Ovk VIT-ELK	15.0000	47500.00	-712500.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	8	InputCharge	Ovk VKS-GMB	5.0000	118000.00	-590000.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	9	InputCharge	Ovk VKS-NDIB	10.0000	100000.00	-1000000.00
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	10	Bonus	Bonus FCR ≤ 1,50 (Fcr 1.463)	9255.5000	150.00	1388325.00
01a106ad-fc35-7afc-b5be-7a397a3b50b9	1	ProfitShare	Bagi hasil 40% × laba 25,100,097.60 (penjualan 161,426,310.00 − biaya 136,326,212.40)	40.0000	25100097.60	10040039.04
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	1	LiveBirdValue	Panen 02/09/2026: 1548 ekor, BW 1.863 kg	2883.5000	20300.00	58535050.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	2	LiveBirdValue	Panen 03/09/2026: 1546 ekor, BW 1.960 kg	3030.2000	20300.00	61513060.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	3	LiveBirdValue	Panen 04/09/2026: 1542 ekor, BW 2.060 kg	3176.2000	20000.00	63524000.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	4	InputCharge	Doc DOC-CP707	5000.0000	7900.00	-39500000.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	5	InputCharge	Feed PKN-BR1	3289.5000	8600.00	-28289700.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	6	InputCharge	Feed PKN-BR2	12785.8000	8300.00	-106122140.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	7	InputCharge	Ovk VIT-ELK	15.0000	47500.00	-712500.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	8	InputCharge	Ovk VKS-GMB	5.0000	118000.00	-590000.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	9	InputCharge	Ovk VKS-NDIB	10.0000	100000.00	-1000000.00
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	10	Penalty	Potongan deplesi > 6% (Depletion 7.28)	4636.0000	150.00	-695400.00
\.


--
-- Data for Name: plasma_settlements; Type: TABLE DATA; Schema: costing; Owner: postgres
--

COPY costing.plasma_settlements (id, number, branch_id, cycle_id, farmer_id, contract_id, scheme, settlement_date, status, notes, income_tax_code_id, income_tax_rate_percent, approved_by, approved_at_utc, cancellation_reason, debt_deduction, deficit, gross_income, income_tax_amount, net_payable, paid_amount, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	STL/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-bf87-7c98-8271-811bb311416f	01a106ad-c18d-7237-a0e3-d935699c85c6	PriceContract	2026-08-24	Paid	Settlement KDG-BDG-01	01a106ad-bd06-72df-8a06-c944cfed04a6	2.0000	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.378504+07	\N	0.00	0.00	33225195.00	664503.90	32560691.10	32560691.10	2026-10-04 18:30:25.283126+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.849672+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fc35-7afc-b5be-7a397a3b50b9	STL/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-bf95-77e6-aff7-4d707e0c4d91	01a106ad-c27b-708d-99cc-89e0647e1680	ProfitSharing	2026-09-02	Draft	Settlement KDG-BDG-03	01a106ad-bd06-72df-8a06-c944cfed04a6	2.0000	\N	\N	\N	0.00	0.00	10040039.04	200800.78	9839238.26	0.00	2026-10-04 18:30:28.022322+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	STL/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-bfb4-7c14-9f15-02079aeddd09	01a106ad-c29f-73ee-9803-d9755e1eef7f	PriceContract	2026-09-08	Approved	Settlement KDG-CJR-01	01a106ad-bd06-72df-8a06-c944cfed04a6	2.0000	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.475746+07	\N	0.00	0.00	6662370.00	133247.40	6529122.60	0.00	2026-10-04 18:30:29.45669+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.475769+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	{}
\.


--
-- Data for Name: attachment_links; Type: TABLE DATA; Schema: documents; Owner: postgres
--

COPY documents.attachment_links (attachment_id, owner_type, owner_id, owner_key) FROM stdin;
\.


--
-- Data for Name: attachments; Type: TABLE DATA; Schema: documents; Owner: postgres
--

COPY documents.attachments (id, file_name, stored_file_name, extension, content_type, size_bytes, storage_path, checksum, kind, branch_id, description, status, unlinked_at_utc, is_deleted, deleted_at_utc, deleted_by, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
\.


--
-- Data for Name: accounts; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.accounts (id, code, name, type, normal_balance, parent_id, is_postable, is_active, created_at_utc, created_by, modified_at_utc, modified_by, cash_flow_category) FROM stdin;
01a0f50c-aaf9-7a84-a3e6-ba9253afc7d4	1	Aset	Asset	Debit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-71e3-8542-49cf2a55317a	2	Liabilitas	Liability	Credit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7301-9e8f-50e8b19feaa5	7	Pendapatan Lain-lain	Revenue	Credit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7546-a637-d0cdc0b966e3	8	Beban Lain-lain	Expense	Debit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7d0b-9ebd-aee8885906af	4	Pendapatan Usaha	Revenue	Credit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7d5e-be92-628c5f177cfd	5	Harga Pokok Penjualan	Expense	Debit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7df0-9944-8579aea7132c	6	Beban Operasional	Expense	Debit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7188-a462-9d565bbf2dfa	2-1	Liabilitas Jangka Pendek	Liability	Credit	01a0f50c-aafa-71e3-8542-49cf2a55317a	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-72a2-adf9-810612b1dc3c	6-1901	Beban Umum Lain-lain	Expense	Debit	01a0f50c-aafa-7df0-9944-8579aea7132c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-72d3-acd7-b789ac25dc81	5-1201	Beban Kemitraan Plasma	Expense	Debit	01a0f50c-aafa-7d5e-be92-628c5f177cfd	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7546-9629-1b0bf34ff2ce	6-1101	Beban Gaji & Tunjangan	Expense	Debit	01a0f50c-aafa-7df0-9944-8579aea7132c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-756e-bd62-b8e331981f15	6-1201	Beban Transportasi	Expense	Debit	01a0f50c-aafa-7df0-9944-8579aea7132c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7598-887e-e689365a4bfe	1-1	Aset Lancar	Asset	Debit	01a0f50c-aaf9-7a84-a3e6-ba9253afc7d4	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-766b-8a8f-b3cad2daf8b6	8-1101	Beban Administrasi Bank	Expense	Debit	01a0f50c-aafa-7546-a637-d0cdc0b966e3	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-767c-961a-c26ebac9697e	7-1101	Pendapatan Bunga	Revenue	Credit	01a0f50c-aafa-7301-9e8f-50e8b19feaa5	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7838-9ee0-77eda0e19df1	6-1301	Beban Penyusutan	Expense	Debit	01a0f50c-aafa-7df0-9944-8579aea7132c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7841-94a9-8bb205222c6d	7-1901	Pendapatan Lain-lain	Revenue	Credit	01a0f50c-aafa-7301-9e8f-50e8b19feaa5	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-78a4-9541-e7668ffaffcd	6-1401	Beban Listrik & Air	Expense	Debit	01a0f50c-aafa-7df0-9944-8579aea7132c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7963-a0ab-cd2fa8cb7c05	4-1102	Penjualan Lain-lain	Revenue	Credit	01a0f50c-aafa-7d0b-9ebd-aee8885906af	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-796a-887e-d8e839992c26	4-1101	Penjualan Ayam Hidup	Revenue	Credit	01a0f50c-aafa-7d0b-9ebd-aee8885906af	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7a19-b189-7edcb4dec59d	4-1901	Potongan & Retur Penjualan	Revenue	Debit	01a0f50c-aafa-7d0b-9ebd-aee8885906af	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7a40-afee-18f70c6634f7	5-1301	Selisih Persediaan	Expense	Debit	01a0f50c-aafa-7d5e-be92-628c5f177cfd	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7c72-9cc9-9d1f9fcb269c	8-1201	Beban Bunga	Expense	Debit	01a0f50c-aafa-7546-a637-d0cdc0b966e3	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7e36-91cb-d19960d0fee3	5-1101	HPP Ayam Hidup	Expense	Debit	01a0f50c-aafa-7d5e-be92-628c5f177cfd	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-717b-b756-a81bb2b0f91e	2-1101	Hutang Usaha	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7420-b1ab-a3a49436d61e	1-1200	Bank	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7421-ac1b-8d8cf9ee8311	1-1100	Kas	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-74f2-adfb-25f3563fc9d5	1-1400	Persediaan Sapronak	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-758d-b98b-7633d6ca364d	1-1300	Piutang	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-78ba-aeee-c1256d236027	2-1401	Biaya Yang Masih Harus Dibayar	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7969-8d0c-bf5f3dc5e227	2-1501	Uang Muka Penjualan	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7a6b-be4a-f2e2a8a7258a	1-1700	Uang Muka	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7b24-9853-0576b8e351ac	2-1102	Hutang Belum Ditagih (GRNI)	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7b4a-ba51-f16ecb38296d	2-1300	Hutang Pajak	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7e4e-8c49-f85a74bd480c	1-1600	Pajak Dibayar Dimuka	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7f2a-bc3e-93bbf2234265	1-1500	Persediaan Ayam Dalam Proses	Asset	Debit	01a0f50c-aafa-7598-887e-e689365a4bfe	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7fd7-bacd-51de6995f4d7	2-1201	Hutang Plasma	Liability	Credit	01a0f50c-aafa-7188-a462-9d565bbf2dfa	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-702f-80d4-d4e8ec5fe6f3	1-1303	Piutang Karyawan	Asset	Debit	01a0f50c-aafa-758d-b98b-7633d6ca364d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7030-8e4c-c2348618a4e5	1-1102	Kas Kecil	Asset	Debit	01a0f50c-aafa-7421-ac1b-8d8cf9ee8311	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-703c-acba-a25e7ca9c550	1-1601	PPN Masukan	Asset	Debit	01a0f50c-aafa-7e4e-8c49-f85a74bd480c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7130-8764-a8bebe0584e8	1-1701	Uang Muka Pembelian	Asset	Debit	01a0f50c-aafa-7a6b-be4a-f2e2a8a7258a	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-71b6-9a15-5af3dc4c4973	1-1602	PPh 22 Dibayar Dimuka	Asset	Debit	01a0f50c-aafa-7e4e-8c49-f85a74bd480c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-72fd-904e-b18026f5acfe	1-1402	Persediaan Pakan	Asset	Debit	01a0f50c-aafa-74f2-adfb-25f3563fc9d5	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-742e-8982-5807675275d6	2-1302	Hutang PPh 21	Liability	Credit	01a0f50c-aafa-7b4a-ba51-f16ecb38296d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-758f-9816-8e78405403d8	1-1403	Persediaan OVK	Asset	Debit	01a0f50c-aafa-74f2-adfb-25f3563fc9d5	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-75da-a609-c4447cb81362	1-1101	Kas Besar	Asset	Debit	01a0f50c-aafa-7421-ac1b-8d8cf9ee8311	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7854-93ac-7da131cbf13c	2-1301	PPN Keluaran	Liability	Credit	01a0f50c-aafa-7b4a-ba51-f16ecb38296d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7904-9f2f-5641d87675f6	2-1304	Hutang PPh 4 Ayat 2	Liability	Credit	01a0f50c-aafa-7b4a-ba51-f16ecb38296d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7ab4-b53b-3b479632dc20	1-1302	Piutang Plasma	Asset	Debit	01a0f50c-aafa-758d-b98b-7633d6ca364d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7adb-92b7-886c99b8e875	1-1501	Ayam Dalam Proses (Siklus Berjalan)	Asset	Debit	01a0f50c-aafa-7f2a-bc3e-93bbf2234265	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7b57-8c6f-842c7cea9323	1-1301	Piutang Usaha	Asset	Debit	01a0f50c-aafa-758d-b98b-7633d6ca364d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7b67-aa7f-6aabbd66d329	1-1401	Persediaan DOC	Asset	Debit	01a0f50c-aafa-74f2-adfb-25f3563fc9d5	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7b82-ab79-855a87a43e6a	1-1201	Bank Operasional	Asset	Debit	01a0f50c-aafa-7420-b1ab-a3a49436d61e	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7e8d-9435-b8dbd9b3d1c6	1-1603	PPh 23 Dibayar Dimuka	Asset	Debit	01a0f50c-aafa-7e4e-8c49-f85a74bd480c	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-7f88-bf00-8dfd3adebe85	2-1303	Hutang PPh 23	Liability	Credit	01a0f50c-aafa-7b4a-ba51-f16ecb38296d	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Operating
01a0f50c-aafa-740e-9f14-9dacf2f522a8	1-2	Aset Tetap	Asset	Debit	01a0f50c-aaf9-7a84-a3e6-ba9253afc7d4	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-7245-9ea4-60de2b4597ff	1-2401	Kendaraan	Asset	Debit	01a0f50c-aafa-740e-9f14-9dacf2f522a8	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-7981-84cd-e5ae4457c2dc	1-2101	Tanah	Asset	Debit	01a0f50c-aafa-740e-9f14-9dacf2f522a8	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-799e-939d-838c47f88333	1-2901	Akumulasi Penyusutan Aset Tetap	Asset	Credit	01a0f50c-aafa-740e-9f14-9dacf2f522a8	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-7d36-b6e8-42bbfeb41372	1-2301	Peralatan Kandang	Asset	Debit	01a0f50c-aafa-740e-9f14-9dacf2f522a8	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-7f2c-85b5-58e6de59dbf0	1-2201	Bangunan Kandang	Asset	Debit	01a0f50c-aafa-740e-9f14-9dacf2f522a8	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Investing
01a0f50c-aafa-7b06-b750-8b80b8a63a7a	3	Ekuitas	Equity	Credit	\N	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a0f50c-aafa-7271-94d9-5cce04e41d9b	3-1101	Modal Disetor	Equity	Credit	01a0f50c-aafa-7b06-b750-8b80b8a63a7a	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a0f50c-aafa-74f4-8fd8-fc94e13ee79f	3-2101	Laba Ditahan	Equity	Credit	01a0f50c-aafa-7b06-b750-8b80b8a63a7a	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a0f50c-aafa-7ba3-a0f2-0a22c938d5db	2-2	Liabilitas Jangka Panjang	Liability	Credit	01a0f50c-aafa-71e3-8542-49cf2a55317a	f	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a0f50c-aafa-7d51-b9b0-42b0a870bee3	3-3101	Laba Tahun Berjalan	Equity	Credit	01a0f50c-aafa-7b06-b750-8b80b8a63a7a	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a0f50c-aafa-7434-94f9-a6b099588668	2-2101	Hutang Bank	Liability	Credit	01a0f50c-aafa-7ba3-a0f2-0a22c938d5db	t	t	2026-10-01 08:20:43.36438+07	\N	\N	\N	Financing
01a106ad-ba9c-728f-b38e-3fa3e2849611	1-1103	Kas Besar Cianjur	Asset	Debit	01a0f50c-aafa-7421-ac1b-8d8cf9ee8311	t	t	2026-10-04 18:30:11.237402+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	Operating
01a106ad-babd-7fe2-be29-f318a2585da5	1-1104	Kas Kecil Cianjur	Asset	Debit	01a0f50c-aafa-7421-ac1b-8d8cf9ee8311	t	t	2026-10-04 18:30:11.263618+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	Operating
01a106ad-baca-7b66-8d17-aa3d450ffb52	1-1202	Bank BRI Cianjur	Asset	Debit	01a0f50c-aafa-7420-b1ab-a3a49436d61e	t	t	2026-10-04 18:30:11.276444+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	Operating
\.


--
-- Data for Name: bank_reconciliations; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.bank_reconciliations (id, branch_id, cash_bank_account_id, statement_date, status, completed_by, completed_at_utc, statement_balance, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ae-184d-7e79-8d29-13a0f6b2f781	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-09-30	Completed	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.723343+07	1958682578.90	2026-10-04 18:30:35.229592+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.723778+07	01a0f240-f921-75b0-972d-0f74522a6333
\.


--
-- Data for Name: bank_statement_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.bank_statement_lines (bank_reconciliation_id, line_number, date, description, matched_journal_entry_id, matched_journal_line_number, amount) FROM stdin;
01a106ae-184d-7e79-8d29-13a0f6b2f781	1	2026-07-01	JU/BDG/2026/VII/0001 Saldo awal: setoran modal	01a106ad-c2de-75df-b31f-49ff1b32abd1	1	2500000000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	2	2026-07-01	JO/BDG/2026/VII/0001 Pemindahbukuan TRF/BDG/2026/VII/0001: Bank BCA Bandung ke Kas Kecil Bandung	01a106ad-c424-7c83-8f5c-756d6c810f2d	2	-15000000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	3	2026-07-10	JO/BDG/2026/VII/0003 BKK/BDG/2026/VII/0002: Pembayaran listrik & air kantor dan kandang inti	01a106ad-c60e-78d0-ae7f-255cd0eaf991	2	-7450000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	4	2026-07-25	JO/BDG/2026/VII/0017 BKK/BDG/2026/VII/0005: Gaji & tunjangan karyawan	01a106ad-d1bd-7768-b9e6-6a8e03483618	3	-86500000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	5	2026-08-01	JO/BDG/2026/VIII/0001 Pemindahbukuan TRF/BDG/2026/VIII/0001: Bank BCA Bandung ke Kas Kecil Bandung	01a106ad-d8ea-7715-a431-24d7e4113148	2	-10000000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	6	2026-08-05	JO/BDG/2026/VIII/0003 Pembayaran PV/BDG/2026/VIII/0001 kepada PT Charoen Pokphand Indonesia	01a106ad-dd4e-787f-bc44-64aa3fc3cee3	2	-150732500.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	7	2026-08-05	JO/BDG/2026/VIII/0004 Pembayaran PV/BDG/2026/VIII/0002 kepada PT Medion Farma Jaya	01a106ad-dd9f-72e9-9b23-7b93d3324a31	2	-2527470.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	8	2026-08-10	JO/BDG/2026/VIII/0006 BKK/BDG/2026/VIII/0002: Pembayaran listrik & air kantor dan kandang inti	01a106ad-dfb1-7711-8e75-18a3275ebc9d	2	-7450000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	9	2026-08-15	JO/BDG/2026/VIII/0009 Pembayaran PV/BDG/2026/VIII/0003 kepada PT Charoen Pokphand Indonesia	01a106ad-e35d-7eee-a33c-b507e3eb5270	2	-33300000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	10	2026-08-15	JO/BDG/2026/VIII/0010 Pembayaran PV/BDG/2026/VIII/0004 kepada PT Japfa Comfeed Indonesia	01a106ad-e3af-70c9-8886-b9da7f060c32	2	-106332500.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	11	2026-08-15	JO/BDG/2026/VIII/0011 Pembayaran PV/BDG/2026/VIII/0005 kepada PT Medion Farma Jaya	01a106ad-e3ff-7f44-b70c-d95bbc3d9d0f	2	-2527470.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	12	2026-08-22	JO/BDG/2026/VIII/0017 Penerimaan RCV/BDG/2026/VIII/0001 dari PT Sumber Berkah Unggas (RPA)	01a106ad-edb2-7bcf-9765-1137cfac49ca	1	63115280.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	13	2026-08-23	JO/BDG/2026/VIII/0020 Penerimaan RCV/BDG/2026/VIII/0002 dari PT Sumber Berkah Unggas (RPA)	01a106ad-ef67-714d-9893-a4ab237caf34	1	66450760.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	14	2026-08-24	JO/BDG/2026/VIII/0022 Penerimaan RCV/BDG/2026/VIII/0003 dari PT Sumber Berkah Unggas (RPA)	01a106ad-f0ea-7c2e-8354-eea43424a08f	1	71453860.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	15	2026-08-25	JO/BDG/2026/VIII/0024 BKK/BDG/2026/VIII/0006: Gaji & tunjangan karyawan	01a106ad-f290-717d-880d-3699f1ad9967	3	-86500000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	16	2026-08-26	JO/BDG/2026/VIII/0025 Pembayaran PV/BDG/2026/VIII/0006 kepada H. Ahmad Suryadi	01a106ad-f3d7-7e9f-b3a3-4cbca1ddbb86	2	-32560691.10
01a106ae-184d-7e79-8d29-13a0f6b2f781	17	2026-08-31	JO/BDG/2026/VIII/0034 Penerimaan RCV/BDG/2026/VIII/0004 dari UD Jaya Abadi (Bakul)	01a106ad-f9ae-790f-b859-fd27a56a0630	1	45609000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	18	2026-09-01	JO/BDG/2026/IX/0001 Pemindahbukuan TRF/BDG/2026/IX/0001: Bank BCA Bandung ke Kas Kecil Bandung	01a106ad-fb62-79ad-a4bd-9d9e39f32971	2	-10000000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	19	2026-09-02	JO/BDG/2026/IX/0002 Penerimaan RCV/BDG/2026/IX/0001 dari UD Jaya Abadi (Bakul)	01a106ad-fc0c-7c20-bf49-6f4d9b71a167	1	51247000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	20	2026-09-10	JO/BDG/2026/IX/0007 BKK/BDG/2026/IX/0002: Pembayaran listrik & air kantor dan kandang inti	01a106ae-02e4-7fc6-839d-6c7f7436a56b	2	-7450000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	21	2026-09-19	JO/BDG/2026/IX/0019 Pembayaran PV/BDG/2026/IX/0001 kepada PT Charoen Pokphand Indonesia	01a106ae-09ec-7718-a4e0-6a7fb6da6035	2	-191500000.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	22	2026-09-19	JO/BDG/2026/IX/0020 Pembayaran PV/BDG/2026/IX/0002 kepada PT Medion Farma Jaya	01a106ae-0a51-74db-abba-fa4860348b89	2	-2862690.00
01a106ae-184d-7e79-8d29-13a0f6b2f781	23	2026-09-25	JO/BDG/2026/IX/0022 BKK/BDG/2026/IX/0005: Gaji & tunjangan karyawan	01a106ae-0ddc-768a-aaf3-243ff1d02fb8	3	-86500000.00
\.


--
-- Data for Name: bank_transfers; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.bank_transfers (id, number, branch_id, from_cash_bank_account_id, to_cash_bank_account_id, date, reference, notes, amount, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-c3d0-7907-a4b2-a8c2ee291819	TRF/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	01a106ad-bc27-79f6-990e-0a9e61e58b0e	2026-07-01	\N	Saldo awal kas kecil	15000000.00	2026-10-04 18:30:13.599998+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c474-722b-b16b-f07cb4eb3e41	TRF/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	2026-07-01	\N	Saldo awal kas kecil	15000000.00	2026-10-04 18:30:13.74854+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-d8d8-7a9c-a216-4b36c299f9fa	TRF/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	01a106ad-bc27-79f6-990e-0a9e61e58b0e	2026-08-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:18.969189+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-d900-70b5-b8dd-f66ed931a827	TRF/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	2026-08-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:19.008629+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-fb53-79b4-8551-6efc939d31a9	TRF/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	01a106ad-bc27-79f6-990e-0a9e61e58b0e	2026-09-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:27.795194+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-fb75-7cd2-ae6f-136641869f21	TRF/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	2026-09-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:27.829168+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ae-1424-7fdd-9d0b-354de397630e	TRF/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	01a106ad-bc27-79f6-990e-0a9e61e58b0e	2026-10-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:34.148599+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ae-1447-713b-b007-4166f3656038	TRF/CJR/2026/X/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	2026-10-01	\N	Pengisian kas kecil	10000000.00	2026-10-04 18:30:34.183558+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: cash_bank_accounts; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.cash_bank_accounts (id, code, name, type, branch_id, account_id, bank_name, account_number, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bbe7-72d3-94ae-3a3d5f945c2e	KAS-BDG	Kas Besar Bandung	Cash	01a106ad-b536-7f76-bf02-1115db4d6aff	01a0f50c-aafa-75da-a609-c4447cb81362	\N	\N	t	2026-10-04 18:30:11.598938+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bc27-79f6-990e-0a9e61e58b0e	KK-BDG	Kas Kecil Bandung	PettyCash	01a106ad-b536-7f76-bf02-1115db4d6aff	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	t	2026-10-04 18:30:11.627934+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bc39-79a9-9d61-e784da7d1598	BCA-BDG	Bank BCA Bandung	Bank	01a106ad-b536-7f76-bf02-1115db4d6aff	01a0f50c-aafa-7b82-ab79-855a87a43e6a	BCA	7770123456	t	2026-10-04 18:30:11.64464+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bc48-7072-9b2f-fce0c9401466	KAS-CJR	Kas Besar Cianjur	Cash	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-ba9c-728f-b38e-3fa3e2849611	\N	\N	t	2026-10-04 18:30:11.659495+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bc56-75cf-a1b4-c9ee34ce7114	KK-CJR	Kas Kecil Cianjur	PettyCash	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	t	2026-10-04 18:30:11.674657+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	BRI-CJR	Bank BRI Cianjur	Bank	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-baca-7b66-8d17-aa3d450ffb52	BRI	0412-01-000987-30-5	t	2026-10-04 18:30:11.689096+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: cash_transaction_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.cash_transaction_lines (cash_transaction_id, line_number, account_id, cost_center_id, description, amount) FROM stdin;
01a106ad-c4b5-7491-95e0-15863c52ff8c	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-c4b5-7491-95e0-15863c52ff8c	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ad-c584-7c38-b34d-8c5434d2cf4d	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-c584-7c38-b34d-8c5434d2cf4d	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ad-c5d8-7957-bc71-fe91e116249e	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	7450000.00
01a106ad-c624-796b-8b34-0b7f901d8aa7	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	5180000.00
01a106ad-c878-768e-9b3d-190b670f41bb	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-c878-768e-9b3d-190b670f41bb	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ad-c8b8-732f-b108-db0e07ef5559	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-c8b8-732f-b108-db0e07ef5559	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ad-ca69-7399-87e7-557b1b2228fb	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ad-ca9c-741d-8ba1-354b097fa37b	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ad-ce74-744a-a8dc-0a7bf0721a72	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-ce74-744a-a8dc-0a7bf0721a72	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ad-cefd-7b1d-aa05-19dae4305228	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-cefd-7b1d-aa05-19dae4305228	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ad-d18d-7091-a98b-c880ad4fdb86	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	56225000.00
01a106ad-d18d-7091-a98b-c880ad4fdb86	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	30275000.00
01a106ad-d1d3-78d4-aae6-b53870ff6918	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	39780000.00
01a106ad-d1d3-78d4-aae6-b53870ff6918	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	21420000.00
01a106ad-d3ab-7b03-88f3-769246490768	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-d3ab-7b03-88f3-769246490768	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	355000.00
01a106ad-d3f4-7d2d-bfa8-6086d9babd41	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-d3f4-7d2d-bfa8-6086d9babd41	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	355000.00
01a106ad-da3a-7a71-b08b-d7964e6d8232	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-da3a-7a71-b08b-d7964e6d8232	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	235000.00
01a106ad-da7f-7691-b9e0-35543cc0403f	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-da7f-7691-b9e0-35543cc0403f	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	235000.00
01a106ad-df6a-7011-920b-f71238fea0ee	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	7450000.00
01a106ad-dfcd-7c13-841f-3015bbd11511	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-dfcd-7c13-841f-3015bbd11511	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ad-e012-7de5-af97-7692f235abe7	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	5180000.00
01a106ad-e051-7f9d-865d-4df514262b7d	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-e051-7f9d-865d-4df514262b7d	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ad-e273-76e4-8b49-3b21706558d4	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ad-e2b9-754f-b649-3199ae737ee4	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ad-e58a-77b6-ad07-77c957398613	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-e58a-77b6-ad07-77c957398613	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ad-e5cb-7c39-bbcc-05161ef91f86	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-e5cb-7c39-bbcc-05161ef91f86	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ad-f049-73aa-a57a-fae76d3cbeb1	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-f049-73aa-a57a-fae76d3cbeb1	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ad-f087-714c-a708-74825c699f6c	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-f087-714c-a708-74825c699f6c	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ad-f267-7992-ae91-ba97a4decb94	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	56225000.00
01a106ad-f267-7992-ae91-ba97a4decb94	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	30275000.00
01a106ad-f2a7-74d1-a2f6-61b4e9051c7e	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	39780000.00
01a106ad-f2a7-74d1-a2f6-61b4e9051c7e	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	21420000.00
01a106ad-f8ba-736a-a425-932ed8595a38	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-f8ba-736a-a425-932ed8595a38	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	355000.00
01a106ad-f926-74e8-8349-63285d1f77f2	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ad-f926-74e8-8349-63285d1f77f2	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	355000.00
01a106ae-007b-7591-82b9-19fa554b3ff7	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-007b-7591-82b9-19fa554b3ff7	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	235000.00
01a106ae-00c4-7757-b345-d41c71a47167	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-00c4-7757-b345-d41c71a47167	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	235000.00
01a106ae-02ba-7700-831b-a880d12dfc87	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	7450000.00
01a106ae-02f8-78a9-8289-ceb1824a7512	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	5180000.00
01a106ae-061a-735c-aa26-ced227719ea4	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-061a-735c-aa26-ced227719ea4	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ae-0653-7037-b7d0-d463cef1f588	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-0653-7037-b7d0-d463cef1f588	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	315000.00
01a106ae-06e1-7167-810d-c915cbc4efcf	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ae-0723-755d-afb4-7a861862a333	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00
01a106ae-0b57-747e-8f52-1af3cd63f0fa	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-0b57-747e-8f52-1af3cd63f0fa	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ae-0b90-7193-a2d9-965f1358e7d7	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-0b90-7193-a2d9-965f1358e7d7	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	395000.00
01a106ae-0db5-734d-b34d-889cf46a1e2b	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	56225000.00
01a106ae-0db5-734d-b34d-889cf46a1e2b	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	30275000.00
01a106ae-0def-71b8-a9fe-a9e0177f013a	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	39780000.00
01a106ae-0def-71b8-a9fe-a9e0177f013a	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	21420000.00
01a106ae-10a8-761f-881e-648b6f39dea3	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-10a8-761f-881e-648b6f39dea3	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ae-10e2-7471-a55e-52e35fcd01dc	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	1575000.00
01a106ae-10e2-7471-a55e-52e35fcd01dc	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	275000.00
01a106ae-1a63-798b-b476-c531d71f810a	1	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bad9-761b-8906-ad19ad45bda1	Sekam 2 truk	2400000.00
\.


--
-- Data for Name: cash_transactions; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.cash_transactions (id, number, branch_id, cash_bank_account_id, direction, date, description, reference, status, approved_by, approved_at_utc, posted_by, posted_at_utc, cancellation_reason, amount, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-d1d3-78d4-aae6-b53870ff6918	BKK/CJR/2026/VII/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-07-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.183331+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.199491+07	\N	61200000.00	2026-10-04 18:30:17.171764+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.19954+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c4b5-7491-95e0-15863c52ff8c	BKK/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-07-06	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:13.916414+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.94206+07	\N	1890000.00	2026-10-04 18:30:13.85295+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.94257+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c584-7c38-b34d-8c5434d2cf4d	BKK/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-07-06	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.032426+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.052884+07	\N	1890000.00	2026-10-04 18:30:14.021059+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.052917+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-dfcd-7c13-841f-3015bbd11511	BKK/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-08-10	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.761928+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.779271+07	\N	1890000.00	2026-10-04 18:30:20.749995+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.77929+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c5d8-7957-bc71-fe91e116249e	BKK/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-07-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.117574+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.137916+07	\N	7450000.00	2026-10-04 18:30:14.104983+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.137974+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d3ab-7b03-88f3-769246490768	BKK/BDG/2026/VII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-07-27	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.655775+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.675663+07	\N	1930000.00	2026-10-04 18:30:17.644207+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.675691+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c624-796b-8b34-0b7f901d8aa7	BKK/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-07-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.193583+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.211887+07	\N	5180000.00	2026-10-04 18:30:14.181403+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.211914+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c878-768e-9b3d-190b670f41bb	BKK/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-07-13	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.787215+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.802497+07	\N	1970000.00	2026-10-04 18:30:14.776568+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.802529+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c8b8-732f-b108-db0e07ef5559	BKK/CJR/2026/VII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-07-13	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.852481+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.867745+07	\N	1970000.00	2026-10-04 18:30:14.841221+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.86777+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-ca69-7399-87e7-557b1b2228fb	BKM/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bbe7-72d3-94ae-3a3d5f945c2e	In	2026-07-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.288981+07	\N	1450000.00	2026-10-04 18:30:15.273449+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.289004+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-ca9c-741d-8ba1-354b097fa37b	BKM/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc48-7072-9b2f-fce0c9401466	In	2026-07-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.341522+07	\N	1450000.00	2026-10-04 18:30:15.324341+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.341549+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d3f4-7d2d-bfa8-6086d9babd41	BKK/CJR/2026/VII/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-07-27	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.72895+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.745571+07	\N	1930000.00	2026-10-04 18:30:17.716904+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.745594+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-ce74-744a-a8dc-0a7bf0721a72	BKK/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-07-20	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:16.320354+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.335831+07	\N	1850000.00	2026-10-04 18:30:16.308687+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.335857+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-cefd-7b1d-aa05-19dae4305228	BKK/CJR/2026/VII/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-07-20	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:16.456568+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.472458+07	\N	1850000.00	2026-10-04 18:30:16.445697+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.47248+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e2b9-754f-b649-3199ae737ee4	BKM/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc48-7072-9b2f-fce0c9401466	In	2026-08-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.518467+07	\N	1450000.00	2026-10-04 18:30:21.497526+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.518493+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d18d-7091-a98b-c880ad4fdb86	BKK/BDG/2026/VII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-07-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.113193+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.130436+07	\N	86500000.00	2026-10-04 18:30:17.101403+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.130461+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e012-7de5-af97-7692f235abe7	BKK/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-08-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.828392+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.845053+07	\N	5180000.00	2026-10-04 18:30:20.818664+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.845072+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-da3a-7a71-b08b-d7964e6d8232	BKK/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-08-03	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:19.333932+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.351363+07	\N	1810000.00	2026-10-04 18:30:19.323071+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.351385+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-da7f-7691-b9e0-35543cc0403f	BKK/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-08-03	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:19.402061+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.418084+07	\N	1810000.00	2026-10-04 18:30:19.392104+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.418103+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-df6a-7011-920b-f71238fea0ee	BKK/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-08-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.660632+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.686393+07	\N	7450000.00	2026-10-04 18:30:20.651197+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.68642+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e5cb-7c39-bbcc-05161ef91f86	BKK/CJR/2026/VIII/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-08-17	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:22.304299+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.336614+07	\N	1970000.00	2026-10-04 18:30:22.283703+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.336637+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e051-7f9d-865d-4df514262b7d	BKK/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-08-10	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.89183+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.908104+07	\N	1890000.00	2026-10-04 18:30:20.881307+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.908127+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e273-76e4-8b49-3b21706558d4	BKM/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bbe7-72d3-94ae-3a3d5f945c2e	In	2026-08-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.455117+07	\N	1450000.00	2026-10-04 18:30:21.428042+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.455145+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e58a-77b6-ad07-77c957398613	BKK/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-08-17	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:22.228681+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.245122+07	\N	1970000.00	2026-10-04 18:30:22.219058+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.245141+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f049-73aa-a57a-fae76d3cbeb1	BKK/BDG/2026/VIII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-08-24	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:24.979307+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.994353+07	\N	1850000.00	2026-10-04 18:30:24.969552+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.99438+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f087-714c-a708-74825c699f6c	BKK/CJR/2026/VIII/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-08-24	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.042935+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.057974+07	\N	1850000.00	2026-10-04 18:30:25.032025+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.057994+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f267-7992-ae91-ba97a4decb94	BKK/BDG/2026/VIII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-08-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.522151+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.536151+07	\N	86500000.00	2026-10-04 18:30:25.51191+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.536174+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f2a7-74d1-a2f6-61b4e9051c7e	BKK/CJR/2026/VIII/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-08-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.586437+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.601252+07	\N	61200000.00	2026-10-04 18:30:25.576177+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.601276+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f8ba-736a-a425-932ed8595a38	BKK/BDG/2026/VIII/0007	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-08-31	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:27.140113+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.156496+07	\N	1930000.00	2026-10-04 18:30:27.130265+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.156525+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f926-74e8-8349-63285d1f77f2	BKK/CJR/2026/VIII/0007	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-08-31	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:27.249569+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.265972+07	\N	1930000.00	2026-10-04 18:30:27.238878+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.266+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-007b-7591-82b9-19fa554b3ff7	BKK/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-09-07	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.125763+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.141328+07	\N	1810000.00	2026-10-04 18:30:29.115978+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.141352+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-00c4-7757-b345-d41c71a47167	BKK/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-09-07	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.206358+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.227057+07	\N	1810000.00	2026-10-04 18:30:29.189178+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.227091+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-02ba-7700-831b-a880d12dfc87	BKK/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-09-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.700908+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.715776+07	\N	7450000.00	2026-10-04 18:30:29.690801+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.7158+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-02f8-78a9-8289-ceb1824a7512	BKK/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-09-10	Pembayaran listrik & air kantor dan kandang inti	PLN/PDAM	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.762841+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.77715+07	\N	5180000.00	2026-10-04 18:30:29.752808+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.777173+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-061a-735c-aa26-ced227719ea4	BKK/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-09-14	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:30.564574+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.57822+07	\N	1890000.00	2026-10-04 18:30:30.555098+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.57824+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0653-7037-b7d0-d463cef1f588	BKK/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-09-14	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:30.62024+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.633966+07	\N	1890000.00	2026-10-04 18:30:30.611375+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.633995+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0723-755d-afb4-7a861862a333	BKM/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc48-7072-9b2f-fce0c9401466	In	2026-09-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.833827+07	\N	1450000.00	2026-10-04 18:30:30.819673+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.833849+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-10a8-761f-881e-648b6f39dea3	BKK/BDG/2026/IX/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-09-28	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:33.265774+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.279574+07	\N	1850000.00	2026-10-04 18:30:33.256486+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.279593+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-10e2-7471-a55e-52e35fcd01dc	BKK/CJR/2026/IX/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-09-28	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:33.32441+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.338579+07	\N	1850000.00	2026-10-04 18:30:33.31503+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.338597+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1a63-798b-b476-c531d71f810a	\N	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-10-03	Pembelian sekam padi untuk alas kandang	\N	Draft	\N	\N	\N	\N	\N	2400000.00	2026-10-04 18:30:35.74742+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-06e1-7167-810d-c915cbc4efcf	BKM/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bbe7-72d3-94ae-3a3d5f945c2e	In	2026-09-15	Penjualan karung pakan bekas	\N	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.775544+07	\N	1450000.00	2026-10-04 18:30:30.754034+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.775574+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0b57-747e-8f52-1af3cd63f0fa	BKK/BDG/2026/IX/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc27-79f6-990e-0a9e61e58b0e	Out	2026-09-21	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.905859+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.919399+07	\N	1970000.00	2026-10-04 18:30:31.895175+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.919423+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0b90-7193-a2d9-965f1358e7d7	BKK/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc56-75cf-a1b4-c9ee34ce7114	Out	2026-09-21	BBM & transport PPL lapangan	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.962889+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.978081+07	\N	1970000.00	2026-10-04 18:30:31.952934+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.978102+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0db5-734d-b34d-889cf46a1e2b	BKK/BDG/2026/IX/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bc39-79a9-9d61-e784da7d1598	Out	2026-09-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:32.511667+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.52672+07	\N	86500000.00	2026-10-04 18:30:32.501967+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.526739+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0def-71b8-a9fe-a9e0177f013a	BKK/CJR/2026/IX/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Out	2026-09-25	Gaji & tunjangan karyawan	Payroll	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:32.569722+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.583943+07	\N	61200000.00	2026-10-04 18:30:32.559567+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.583969+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
\.


--
-- Data for Name: cost_centers; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.cost_centers (id, code, name, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bad9-761b-8906-ad19ad45bda1	PRD	Produksi	t	2026-10-04 18:30:11.315504+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bb01-7105-baf6-b9d4d6e43035	ADM	Administrasi & Umum	t	2026-10-04 18:30:11.332592+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bb0c-73e5-8470-f55cf1f77166	MKT	Pemasaran	t	2026-10-04 18:30:11.344015+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: customer_advance_applications; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.customer_advance_applications (id, customer_receipt_id, sales_invoice_id, date, amount) FROM stdin;
01a106ae-11e1-7db7-b31a-e4ba63387b40	01a106ae-0eb2-7e2e-8ae3-e03501f0a745	01a106ae-1177-79f1-9dab-ecd0831503a9	2026-09-28	50000000.00
\.


--
-- Data for Name: customer_receipt_allocations; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.customer_receipt_allocations (customer_receipt_id, sales_invoice_id, amount) FROM stdin;
01a106ad-ed54-70d0-a419-4f17a1e62f8a	01a106ad-e851-7815-872f-3eee4106b6c0	63115280.00
01a106ad-ef52-76b0-bc3c-1f9582692a76	01a106ad-eb3e-7840-8bd9-c308839c5897	66450760.00
01a106ad-f0d4-7029-a94d-5b00e1f3097e	01a106ad-ebc3-784b-93be-5919195da558	71453860.00
01a106ad-f999-7d5f-ab28-ea8b09d7216b	01a106ad-f592-755f-870c-754df282fed9	45609000.00
01a106ad-fbf8-75b3-9fec-536538abc1b0	01a106ad-f785-70ba-9a35-aaa06a2eaa01	51247000.00
01a106ad-ffc3-77bc-a775-24cd57b8ec8a	01a106ad-fd40-7640-a517-a313d071cc33	61995250.00
01a106ae-013a-75ef-a556-ed8b25179089	01a106ad-fea7-70cb-af7e-26337a183541	65149300.00
01a106ae-0196-7c18-8152-cc1a334ef261	01a106ad-ff5a-73aa-bb0b-f6a7a6cff728	68288300.00
01a106ae-14a1-7f7a-b0ef-4b0f307eb679	01a106ae-1177-79f1-9dab-ecd0831503a9	29436000.00
01a106ae-15b8-700b-af83-753cfbb10a2a	01a106ae-12c6-7315-9f02-2fa6cd84672d	62666000.00
01a106ae-17f8-7f8a-88cf-3bf2a729f0c9	01a106ae-13c7-7e8d-bea5-901363b90855	65525000.00
\.


--
-- Data for Name: customer_receipts; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.customer_receipts (id, number, branch_id, customer_id, receipt_date, cash_account_id, reference, notes, amount, created_at_utc, created_by, modified_at_utc, modified_by, advance_amount, applied_advance_amount, cash_bank_account_id, status, void_date, void_reason, documents) FROM stdin;
01a106ad-ed54-70d0-a419-4f17a1e62f8a	RCV/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-22	01a0f50c-aafa-7b82-ab79-855a87a43e6a	TRF pelunasan	\N	63115280.00	2026-10-04 18:30:24.24521+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc39-79a9-9d61-e784da7d1598	Posted	\N	\N	{}
01a106ad-ef52-76b0-bc3c-1f9582692a76	RCV/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-23	01a0f50c-aafa-7b82-ab79-855a87a43e6a	TRF pelunasan	\N	66450760.00	2026-10-04 18:30:24.722993+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc39-79a9-9d61-e784da7d1598	Posted	\N	\N	{}
01a106ad-f0d4-7029-a94d-5b00e1f3097e	RCV/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-24	01a0f50c-aafa-7b82-ab79-855a87a43e6a	TRF pelunasan	\N	71453860.00	2026-10-04 18:30:25.108487+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc39-79a9-9d61-e784da7d1598	Posted	\N	\N	{}
01a106ad-f999-7d5f-ab28-ea8b09d7216b	RCV/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-31	01a0f50c-aafa-7b82-ab79-855a87a43e6a	TRF pelunasan	\N	45609000.00	2026-10-04 18:30:27.353604+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc39-79a9-9d61-e784da7d1598	Posted	\N	\N	{}
01a106ad-fbf8-75b3-9fec-536538abc1b0	RCV/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-09-02	01a0f50c-aafa-7b82-ab79-855a87a43e6a	TRF pelunasan	\N	51247000.00	2026-10-04 18:30:27.960468+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc39-79a9-9d61-e784da7d1598	Posted	\N	\N	{}
01a106ad-ffc3-77bc-a775-24cd57b8ec8a	RCV/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-06	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	61995250.00	2026-10-04 18:30:28.931472+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-013a-75ef-a556-ed8b25179089	RCV/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-07	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	65149300.00	2026-10-04 18:30:29.306731+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-0196-7c18-8152-cc1a334ef261	RCV/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-08	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	68288300.00	2026-10-04 18:30:29.398293+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-0eb2-7e2e-8ae3-e03501f0a745	RCV/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-25	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF uang muka	Uang muka panen KDG-CJR-INTI	50000000.00	2026-10-04 18:30:32.754284+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.570072+07	01a0f240-f921-75b0-972d-0f74522a6333	50000000.00	50000000.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-14a1-7f7a-b0ef-4b0f307eb679	RCV/CJR/2026/X/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-10-01	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	29436000.00	2026-10-04 18:30:34.273539+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-15b8-700b-af83-753cfbb10a2a	RCV/CJR/2026/X/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-10-02	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	62666000.00	2026-10-04 18:30:34.552669+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
01a106ae-17f8-7f8a-88cf-3bf2a729f0c9	RCV/CJR/2026/X/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-10-03	01a106ad-baca-7b66-8d17-aa3d450ffb52	TRF pelunasan	\N	65525000.00	2026-10-04 18:30:35.129112+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	0.00	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	Posted	\N	\N	{}
\.


--
-- Data for Name: fiscal_periods; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.fiscal_periods (id, year, month, start_date, end_date, status, closed_at_utc, closed_by, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-b978-70da-863a-5643ed741f03	2026	12	2026-12-01	2026-12-31	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-73a2-8880-2f8f8cbe95b0	2026	8	2026-08-01	2026-08-31	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-73ed-a7f3-b857cc57ce5f	2026	7	2026-07-01	2026-07-31	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-7972-9c00-020a4beabcf7	2026	11	2026-11-01	2026-11-30	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-7d86-9c33-decfaceb0c38	2026	9	2026-09-01	2026-09-30	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-7e63-8f7c-fe4d0b8f5e60	2026	10	2026-10-01	2026-10-31	Open	\N	\N	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b978-7854-ae0f-9128e5176fb1	2026	1	2026-01-01	2026-01-31	Closed	2026-10-04 18:30:11.104644+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.104889+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-b978-76e9-95f5-e8941fcfce0f	2026	2	2026-02-01	2026-02-28	Closed	2026-10-04 18:30:11.129978+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.129999+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-b978-7e4c-8aaf-e6758234b076	2026	3	2026-03-01	2026-03-31	Closed	2026-10-04 18:30:11.1465+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.14652+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-b978-7448-a79c-6dd2aff4cce1	2026	4	2026-04-01	2026-04-30	Closed	2026-10-04 18:30:11.161675+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.161703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-b978-7513-a4df-90f42f08be8d	2026	5	2026-05-01	2026-05-31	Closed	2026-10-04 18:30:11.179661+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.17969+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-b978-7504-917e-f74674036aa5	2026	6	2026-06-01	2026-06-30	Closed	2026-10-04 18:30:11.193255+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:10.957653+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:11.193288+07	01a0f240-f921-75b0-972d-0f74522a6333
\.


--
-- Data for Name: journal_entries; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_entries (id, number, branch_id, date, description, source, source_type, source_id, status, approved_by, approved_at_utc, posted_by, posted_at_utc, reversal_of_id, reversed_by_id, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-c2de-75df-b31f-49ff1b32abd1	JU/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-01	Saldo awal: setoran modal	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:13.462888+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.540858+07	\N	\N	2026-10-04 18:30:13.391993+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.541333+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c424-7c83-8f5c-756d6c810f2d	JO/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-01	Pemindahbukuan TRF/BDG/2026/VII/0001: Bank BCA Bandung ke Kas Kecil Bandung	Automatic	BankTransfer	01a106ad-c3d0-7907-a4b2-a8c2ee291819	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.682669+07	\N	\N	2026-10-04 18:30:13.683413+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c445-7d81-b732-0a2a80f68d53	JU/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-01	Saldo awal: setoran modal	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:13.714173+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.731288+07	\N	\N	2026-10-04 18:30:13.701987+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.731319+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c485-70d9-8223-fa71882a85aa	JO/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-01	Pemindahbukuan TRF/CJR/2026/VII/0001: Bank BRI Cianjur ke Kas Kecil Cianjur	Automatic	BankTransfer	01a106ad-c474-722b-b16b-f07cb4eb3e41	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.771537+07	\N	\N	2026-10-04 18:30:13.771865+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c56b-706f-8d43-1e873cf6097f	JO/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-06	BKK/BDG/2026/VII/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-c4b5-7491-95e0-15863c52ff8c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.001678+07	\N	\N	2026-10-04 18:30:14.002952+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c5b6-7474-9f11-92c3cfbfa142	JO/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-06	BKK/CJR/2026/VII/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-c584-7c38-b34d-8c5434d2cf4d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.076175+07	\N	\N	2026-10-04 18:30:14.076699+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c60e-78d0-ae7f-255cd0eaf991	JO/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-10	BKK/BDG/2026/VII/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ad-c5d8-7957-bc71-fe91e116249e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.164865+07	\N	\N	2026-10-04 18:30:14.165748+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c658-784b-a7f7-25f49abf2023	JO/CJR/2026/VII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-10	BKK/CJR/2026/VII/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ad-c624-796b-8b34-0b7f901d8aa7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.244647+07	\N	\N	2026-10-04 18:30:14.245204+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c8a4-770d-acc7-078e0f57fa99	JO/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-13	BKK/BDG/2026/VII/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-c878-768e-9b3d-190b670f41bb	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.826211+07	\N	\N	2026-10-04 18:30:14.826544+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c8e4-7a06-ac4d-a2d0c59f8759	JO/CJR/2026/VII/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-13	BKK/CJR/2026/VII/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-c8b8-732f-b108-db0e07ef5559	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:14.890266+07	\N	\N	2026-10-04 18:30:14.890612+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ca0b-7631-aaa1-6b0f7a4ec843	JO/BDG/2026/VII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-14	Penerimaan BPB/BDG/2026/VII/0001 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-c921-7abc-9538-f9aefa07df64	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.186479+07	\N	\N	2026-10-04 18:30:15.186888+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ca55-7f77-992d-3b6ec5c41b73	JO/BDG/2026/VII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-14	Penerimaan BPB/BDG/2026/VII/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.258806+07	\N	\N	2026-10-04 18:30:15.259123+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ca89-716b-bef3-1d82a5ee5b18	JO/BDG/2026/VII/0007	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-15	BKM/BDG/2026/VII/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ad-ca69-7399-87e7-557b1b2228fb	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.310444+07	\N	\N	2026-10-04 18:30:15.310757+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cabf-7cd0-a8b1-363292123dad	JO/CJR/2026/VII/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-15	BKM/CJR/2026/VII/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ad-ca9c-741d-8ba1-354b097fa37b	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.364343+07	\N	\N	2026-10-04 18:30:15.364977+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cb36-73ee-8d81-4f9166dc9ebd	JO/BDG/2026/VII/0008	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-16	Penerimaan BPB/BDG/2026/VII/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-caea-7afd-8a30-a75d8b846fd2	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.486222+07	\N	\N	2026-10-04 18:30:15.486792+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cb4f-716f-b776-4b8bd2d05bef	JO/BDG/2026/VII/0009	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-16	DOC langsung ke kandang BPB/BDG/2026/VII/0003	Automatic	StockTransferToCycle	01a106ad-caea-7afd-8a30-a75d8b846fd2	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.509838+07	\N	\N	2026-10-04 18:30:15.510161+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cc4e-7124-8ecc-52922aa61e5f	JO/BDG/2026/VII/0010	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-16	Kirim sapronak TRF/BDG/2026/VII/0002	Automatic	StockTransferToCycle	01a106ad-cbd7-752a-838d-978a3b279daf	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.76494+07	\N	\N	2026-10-04 18:30:15.765439+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ce43-7d55-bc58-6d3b0d034353	JO/BDG/2026/VII/0011	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-19	Tagihan VI/BDG/2026/VII/0001 (CPI/INV/2607/0001) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-cd6f-7c22-ae20-adc16848cca8	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.265959+07	\N	\N	2026-10-04 18:30:16.266301+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cea1-7f98-a52c-aca7128720ff	JO/BDG/2026/VII/0012	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-20	BKK/BDG/2026/VII/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-ce74-744a-a8dc-0a7bf0721a72	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.431133+07	\N	\N	2026-10-04 18:30:16.431627+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cf29-774c-aa91-2e1e58c7940d	JO/CJR/2026/VII/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-20	BKK/CJR/2026/VII/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-cefd-7b1d-aa05-19dae4305228	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.495445+07	\N	\N	2026-10-04 18:30:16.495763+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cf95-7fdb-853d-a8d574d4910b	JO/BDG/2026/VII/0013	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-21	Tagihan VI/BDG/2026/VII/0002 (CPI/INV/2607/0002) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-cf68-792c-a1d9-7d5ad8b45cad	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.60337+07	\N	\N	2026-10-04 18:30:16.603671+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cff8-7e51-9a7c-d0c0a058cb4a	JO/BDG/2026/VII/0014	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-21	Tagihan VI/BDG/2026/VII/0003 (MDN/INV/2607/0003) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.705589+07	\N	\N	2026-10-04 18:30:16.705944+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d134-7b8a-a418-4e8573fcb910	JO/BDG/2026/VII/0015	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-24	Penerimaan BPB/BDG/2026/VII/0004 dari PT Japfa Comfeed Indonesia	Automatic	PurchaseReceipt	01a106ad-d113-7702-95fa-9936fda3cb97	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.019447+07	\N	\N	2026-10-04 18:30:17.01974+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d174-7585-9b9b-34de98516846	JO/BDG/2026/VII/0016	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-24	Penerimaan BPB/BDG/2026/VII/0005 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ad-d150-7d46-aad6-09ab900856a2	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.083805+07	\N	\N	2026-10-04 18:30:17.084131+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d1bd-7768-b9e6-6a8e03483618	JO/BDG/2026/VII/0017	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-25	BKK/BDG/2026/VII/0005: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ad-d18d-7091-a98b-c880ad4fdb86	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.155609+07	\N	\N	2026-10-04 18:30:17.155915+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d202-7877-aaab-6b6a94598175	JO/CJR/2026/VII/0007	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-25	BKK/CJR/2026/VII/0005: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ad-d1d3-78d4-aae6-b53870ff6918	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.224443+07	\N	\N	2026-10-04 18:30:17.224783+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d271-7801-9fb7-fe26402d86aa	JO/BDG/2026/VII/0018	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-26	Penerimaan BPB/BDG/2026/VII/0006 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-d257-72fc-9fab-d8d93779a1f7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.3355+07	\N	\N	2026-10-04 18:30:17.335808+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d286-7c12-9339-4a95616a2063	JO/BDG/2026/VII/0019	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-26	DOC langsung ke kandang BPB/BDG/2026/VII/0006	Automatic	StockTransferToCycle	01a106ad-d257-72fc-9fab-d8d93779a1f7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.358024+07	\N	\N	2026-10-04 18:30:17.358318+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d30a-7c51-88ba-27dd372cb8c9	JO/BDG/2026/VII/0020	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-26	Kirim sapronak TRF/BDG/2026/VII/0003	Automatic	StockTransferToCycle	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.488894+07	\N	\N	2026-10-04 18:30:17.48921+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d3de-7d4b-9444-bc2080f2a6c0	JO/BDG/2026/VII/0021	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-27	BKK/BDG/2026/VII/0006: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-d3ab-7b03-88f3-769246490768	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.700485+07	\N	\N	2026-10-04 18:30:17.700812+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d424-7f64-bfc5-e923be8a57b5	JO/CJR/2026/VII/0008	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-27	BKK/CJR/2026/VII/0006: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-d3f4-7d2d-bfa8-6086d9babd41	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.771607+07	\N	\N	2026-10-04 18:30:17.771975+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d50a-746c-af0f-c4cb7cc4d68b	JO/BDG/2026/VII/0022	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-28	Kirim sapronak TRF/BDG/2026/VII/0004	Automatic	StockTransferToCycle	01a106ad-d4e9-76d3-825a-71bb220f2d77	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.000792+07	\N	\N	2026-10-04 18:30:18.001098+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d65b-7a68-9a4d-1898cad3cdd3	JO/BDG/2026/VII/0023	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-29	Tagihan VI/BDG/2026/VII/0004 (CPI/INV/2607/0004) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-d61f-7df9-b45c-7f4646da5272	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.338209+07	\N	\N	2026-10-04 18:30:18.338661+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d6ed-732b-befa-5bde51400a96	JO/CJR/2026/VII/0011	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-30	Penerimaan BPB/CJR/2026/VII/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-d6d0-7981-97bd-a98ead54697d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.483556+07	\N	\N	2026-10-04 18:30:18.483811+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d701-731c-80db-661b09b81633	JO/CJR/2026/VII/0012	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-30	DOC langsung ke kandang BPB/CJR/2026/VII/0003	Automatic	StockTransferToCycle	01a106ad-d6d0-7981-97bd-a98ead54697d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.504007+07	\N	\N	2026-10-04 18:30:18.504335+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d75f-753d-bb13-a5e580df1e32	JO/CJR/2026/VII/0013	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-30	Kirim sapronak TRF/CJR/2026/VII/0002	Automatic	StockTransferToCycle	01a106ad-d731-7973-9045-abaa69ca12a9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.597049+07	\N	\N	2026-10-04 18:30:18.597356+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d7ae-763d-a218-153532287670	JU/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-31	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:18.684807+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.709579+07	\N	\N	2026-10-04 18:30:18.671117+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.709606+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d8ea-7715-a431-24d7e4113148	JO/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-01	Pemindahbukuan TRF/BDG/2026/VIII/0001: Bank BCA Bandung ke Kas Kecil Bandung	Automatic	BankTransfer	01a106ad-d8d8-7a9c-a216-4b36c299f9fa	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.991932+07	\N	\N	2026-10-04 18:30:18.992208+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-da0b-7e81-a046-ef89e52d5a37	JO/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-02	Tagihan VI/CJR/2026/VIII/0001 (CPI/INV/2608/0007) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-d9df-7964-ab27-9bf8d2096cd4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.284297+07	\N	\N	2026-10-04 18:30:19.28455+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-db91-7331-987a-0870812b7792	JO/CJR/2026/VIII/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-04	Tagihan VI/CJR/2026/VIII/0002 (CPI/INV/2608/0008) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-db66-79e0-9dbe-e4441b97acf9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.672637+07	\N	\N	2026-10-04 18:30:19.672966+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dbda-766d-8b9a-322bb4ad4f94	JO/CJR/2026/VIII/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-04	Tagihan VI/CJR/2026/VIII/0003 (MDN/INV/2608/0009) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ad-dbb0-72f2-be66-2505a4e57fc4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.74444+07	\N	\N	2026-10-04 18:30:19.744738+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dfb1-7711-8e75-18a3275ebc9d	JO/BDG/2026/VIII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-10	BKK/BDG/2026/VIII/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ad-df6a-7011-920b-f71238fea0ee	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.728667+07	\N	\N	2026-10-04 18:30:20.729015+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dffc-77e2-a82a-61bc4ab41938	JO/BDG/2026/VIII/0007	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-10	BKK/BDG/2026/VIII/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-dfcd-7c13-841f-3015bbd11511	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.803846+07	\N	\N	2026-10-04 18:30:20.804192+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e03e-7c20-be29-a4ecc44c959a	JO/CJR/2026/VIII/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-10	BKK/CJR/2026/VIII/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ad-e012-7de5-af97-7692f235abe7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.868482+07	\N	\N	2026-10-04 18:30:20.868746+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e07c-7514-8a8c-2b16d440f03c	JO/CJR/2026/VIII/0007	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-10	BKK/CJR/2026/VIII/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-e051-7f9d-865d-4df514262b7d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.92994+07	\N	\N	2026-10-04 18:30:20.930304+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e148-7561-9558-8bb74205c02f	JO/CJR/2026/VIII/0008	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-11	Kirim sapronak TRF/CJR/2026/VIII/0002	Automatic	StockTransferToCycle	01a106ad-e12c-7fed-8626-ef11d1a1aa64	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.136492+07	\N	\N	2026-10-04 18:30:21.137045+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e2e4-763d-af07-f1be49771ce4	JO/CJR/2026/VIII/0009	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-15	BKM/CJR/2026/VIII/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ad-e2b9-754f-b649-3199ae737ee4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.546658+07	\N	\N	2026-10-04 18:30:21.547098+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e5b5-71b0-8d19-cc5f390ba878	JO/BDG/2026/VIII/0012	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-17	BKK/BDG/2026/VIII/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-e58a-77b6-ad07-77c957398613	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.268082+07	\N	\N	2026-10-04 18:30:22.268391+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e61c-7f90-bd0b-ca8a888fbf7a	JO/CJR/2026/VIII/0010	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-17	BKK/CJR/2026/VIII/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-e5cb-7c39-bbcc-05161ef91f86	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.37207+07	\N	\N	2026-10-04 18:30:22.373218+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e9d4-7a35-931f-17232db5e5d6	JO/CJR/2026/VIII/0011	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-19	Pembayaran PV/CJR/2026/VIII/0001 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ad-e9a0-7549-af11-75b80e8d8502	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.322373+07	\N	\N	2026-10-04 18:30:23.322713+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ea27-7aed-bd54-4875da11b3c0	JO/CJR/2026/VIII/0012	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-19	Pembayaran PV/CJR/2026/VIII/0002 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ad-e9f1-769f-9227-f19e69f1c2ef	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.405226+07	\N	\N	2026-10-04 18:30:23.405496+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-eb76-74e2-a654-a417c81ac15a	JO/BDG/2026/VIII/0014	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-20	Penjualan INV/BDG/2026/VIII/0002 kepada PT Sumber Berkah Unggas (RPA)	Automatic	SalesInvoice	01a106ad-eb3e-7840-8bd9-c308839c5897	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.741097+07	\N	\N	2026-10-04 18:30:23.741412+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ebf4-7205-9cce-47b3000e0f2f	JO/BDG/2026/VIII/0015	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-21	Penjualan INV/BDG/2026/VIII/0003 kepada PT Sumber Berkah Unggas (RPA)	Automatic	SalesInvoice	01a106ad-ebc3-784b-93be-5919195da558	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.866377+07	\N	\N	2026-10-04 18:30:23.866664+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ec6b-757c-a321-259e1e5a5e21	JO/BDG/2026/VIII/0016	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-21	Nota kredit CN/BDG/2026/VIII/0001 atas INV/BDG/2026/VIII/0001: Klaim susut timbang di RPA	Automatic	SalesCreditNote	01a106ad-ec1a-7e4b-9c45-bc2b557665a5	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.984647+07	\N	\N	2026-10-04 18:30:23.984904+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-efcc-7fcc-865b-eec0bd50e112	JO/CJR/2026/VIII/0015	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-23	Penerimaan BPB/CJR/2026/VIII/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.850395+07	\N	\N	2026-10-04 18:30:24.850714+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-efe1-7fc0-a130-bd2aeeeb5ad7	JO/CJR/2026/VIII/0016	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-23	DOC langsung ke kandang BPB/CJR/2026/VIII/0003	Automatic	StockTransferToCycle	01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.871549+07	\N	\N	2026-10-04 18:30:24.871857+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f035-7ae5-9f06-9dd2daf6c907	JO/CJR/2026/VIII/0017	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-23	Kirim sapronak TRF/CJR/2026/VIII/0003	Automatic	StockTransferToCycle	01a106ad-f00d-7930-be85-9e425747d0fa	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.955418+07	\N	\N	2026-10-04 18:30:24.955884+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f073-7434-8612-1c045fcd87d6	JO/BDG/2026/VIII/0021	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-24	BKK/BDG/2026/VIII/0005: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-f049-73aa-a57a-fae76d3cbeb1	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.016582+07	\N	\N	2026-10-04 18:30:25.016931+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f0b3-7ba6-b6a7-4ebe97e7feb0	JO/CJR/2026/VIII/0018	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-24	BKK/CJR/2026/VIII/0005: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-f087-714c-a708-74825c699f6c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.082504+07	\N	\N	2026-10-04 18:30:25.0828+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d584-7347-b05d-db9e642044d7	JO/CJR/2026/VII/0009	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-28	Penerimaan BPB/CJR/2026/VII/0001 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-d565-74c5-97c1-753986941b7f	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.122964+07	\N	\N	2026-10-04 18:30:18.123288+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d5cf-7c8a-b18b-21998dc2722f	JO/CJR/2026/VII/0010	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-07-28	Penerimaan BPB/CJR/2026/VII/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ad-d5a9-715f-82a1-c024dce8f91c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.196147+07	\N	\N	2026-10-04 18:30:18.196418+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d777-7e7c-9acf-cd10cc9fef9c	JU/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-31	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:18.638635+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.656422+07	\N	\N	2026-10-04 18:30:18.616154+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.656468+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d83b-76d3-b51b-7acf5de4f84c	JO/BDG/2026/VII/0024	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-31	Tagihan VI/BDG/2026/VII/0005 (JPF/INV/2607/0005) dari PT Japfa Comfeed Indonesia	Automatic	VendorInvoice	01a106ad-d80c-7812-8300-34c0e8e0c918	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.818636+07	\N	\N	2026-10-04 18:30:18.818957+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d88d-7f93-9c71-8e78e7d21810	JO/BDG/2026/VII/0025	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-07-31	Tagihan VI/BDG/2026/VII/0006 (MDN/INV/2607/0006) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ad-d85e-7c5f-af62-abde8aa20340	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.900249+07	\N	\N	2026-10-04 18:30:18.900547+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d928-77f6-9154-02389f15388b	JO/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-01	Pemindahbukuan TRF/CJR/2026/VIII/0001: Bank BRI Cianjur ke Kas Kecil Cianjur	Automatic	BankTransfer	01a106ad-d900-70b5-b8dd-f66ed931a827	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.058183+07	\N	\N	2026-10-04 18:30:19.058948+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-da69-7b3e-80d7-3e5bf2ddf804	JO/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-03	BKK/BDG/2026/VIII/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-da3a-7a71-b08b-d7964e6d8232	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.375944+07	\N	\N	2026-10-04 18:30:19.376236+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-daab-792a-b629-7ce566e79605	JO/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-03	BKK/CJR/2026/VIII/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-da7f-7691-b9e0-35543cc0403f	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.44035+07	\N	\N	2026-10-04 18:30:19.440689+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dd4e-787f-bc44-64aa3fc3cee3	JO/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-05	Pembayaran PV/BDG/2026/VIII/0001 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ad-dc76-7acc-9172-4993fcd6d732	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.116472+07	\N	\N	2026-10-04 18:30:20.11675+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dd9f-72e9-9b23-7b93d3324a31	JO/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-05	Pembayaran PV/BDG/2026/VIII/0002 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ad-dd6a-7585-a741-9eb19ff1427f	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.196389+07	\N	\N	2026-10-04 18:30:20.196695+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de91-7779-8853-d49a6028029f	JO/BDG/2026/VIII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-07	Kirim sapronak TRF/BDG/2026/VIII/0002	Automatic	StockTransferToCycle	01a106ad-de6e-7454-87f3-422062f82097	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.440132+07	\N	\N	2026-10-04 18:30:20.440409+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e2a3-7ee7-833d-cf92f7fda271	JO/BDG/2026/VIII/0008	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-15	BKM/BDG/2026/VIII/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ad-e273-76e4-8b49-3b21706558d4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.482856+07	\N	\N	2026-10-04 18:30:21.483121+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e35d-7eee-a33c-b507e3eb5270	JO/BDG/2026/VIII/0009	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-15	Pembayaran PV/BDG/2026/VIII/0003 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ad-e322-7e5f-ad9e-f18ee24956cf	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.668456+07	\N	\N	2026-10-04 18:30:21.668731+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e3af-70c9-8886-b9da7f060c32	JO/BDG/2026/VIII/0010	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-15	Pembayaran PV/BDG/2026/VIII/0004 kepada PT Japfa Comfeed Indonesia	Automatic	VendorPayment	01a106ad-e37a-7d67-9ca9-d476a5ba9b61	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.749941+07	\N	\N	2026-10-04 18:30:21.750213+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e3ff-7f44-b70c-d95bbc3d9d0f	JO/BDG/2026/VIII/0011	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-15	Pembayaran PV/BDG/2026/VIII/0005 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ad-e3cb-7993-9fb3-1074d78c9ccd	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.828401+07	\N	\N	2026-10-04 18:30:21.828713+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e961-7cea-b7d9-eec09af3cf3c	JO/BDG/2026/VIII/0013	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-19	Penjualan INV/BDG/2026/VIII/0001 kepada PT Sumber Berkah Unggas (RPA)	Automatic	SalesInvoice	01a106ad-e851-7815-872f-3eee4106b6c0	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.208351+07	\N	\N	2026-10-04 18:30:23.208857+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-eccf-791e-8ce1-0e705d33d373	JO/CJR/2026/VIII/0013	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-21	Penerimaan BPB/CJR/2026/VIII/0001 dari PT Japfa Comfeed Indonesia	Automatic	PurchaseReceipt	01a106ad-ecb2-7cf5-bc99-7307f161b5bc	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.084192+07	\N	\N	2026-10-04 18:30:24.084458+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ed0a-79cd-8ec4-89707c0de8bc	JO/CJR/2026/VIII/0014	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-21	Penerimaan BPB/CJR/2026/VIII/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ad-ece9-7ff1-aad4-f1d48c18557f	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.145006+07	\N	\N	2026-10-04 18:30:24.145284+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-edb2-7bcf-9765-1137cfac49ca	JO/BDG/2026/VIII/0017	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-22	Penerimaan RCV/BDG/2026/VIII/0001 dari PT Sumber Berkah Unggas (RPA)	Automatic	CustomerReceipt	01a106ad-ed54-70d0-a419-4f17a1e62f8a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.312549+07	\N	\N	2026-10-04 18:30:24.312901+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ee6c-7755-ba01-c69159dae764	JO/BDG/2026/VIII/0018	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-22	Retur sapronak RTR/BDG/2026/VIII/0001: Sisa sapronak akhir siklus	Automatic	StockReturnFromCycle	01a106ad-ede7-7914-bc12-88b477649acb	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.498081+07	\N	\N	2026-10-04 18:30:24.498466+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef09-79b0-a677-5566eaae74e2	JO/BDG/2026/VIII/0019	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-20	Penyesuaian HPP tutup siklus SKL/BDG/2026/VII/0001	Automatic	CycleCostAdjustment	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.655113+07	\N	\N	2026-10-04 18:30:24.655378+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef67-714d-9893-a4ab237caf34	JO/BDG/2026/VIII/0020	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-23	Penerimaan RCV/BDG/2026/VIII/0002 dari PT Sumber Berkah Unggas (RPA)	Automatic	CustomerReceipt	01a106ad-ef52-76b0-bc3c-1f9582692a76	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.74984+07	\N	\N	2026-10-04 18:30:24.750076+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f290-717d-880d-3699f1ad9967	JO/BDG/2026/VIII/0024	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-25	BKK/BDG/2026/VIII/0006: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ad-f267-7992-ae91-ba97a4decb94	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.560382+07	\N	\N	2026-10-04 18:30:25.56087+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f2d1-78a6-b098-ca93c0f34226	JO/CJR/2026/VIII/0019	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-25	BKK/CJR/2026/VIII/0006: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ad-f2a7-74d1-a2f6-61b4e9051c7e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.623564+07	\N	\N	2026-10-04 18:30:25.623836+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f3d7-7e9f-b3a3-4cbca1ddbb86	JO/BDG/2026/VIII/0025	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-26	Pembayaran PV/BDG/2026/VIII/0006 kepada H. Ahmad Suryadi	Automatic	PlasmaPayment	01a106ad-f387-7df5-b3a2-f33f2933d8d4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.885055+07	\N	\N	2026-10-04 18:30:25.885296+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f4bf-7afc-a903-e9caa72d7e2e	JO/CJR/2026/VIII/0020	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-26	Tagihan VI/CJR/2026/VIII/0004 (CPI/INV/2608/0010) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-f492-7328-b925-8c257fbccde7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.12365+07	\N	\N	2026-10-04 18:30:26.123962+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f5c8-7754-988d-972f0e49c0ac	JO/BDG/2026/VIII/0026	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-28	Penjualan INV/BDG/2026/VIII/0004 kepada UD Jaya Abadi (Bakul)	Automatic	SalesInvoice	01a106ad-f592-755f-870c-754df282fed9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.382404+07	\N	\N	2026-10-04 18:30:26.382796+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f6a3-7055-8a36-9d50e4d7b53a	JO/CJR/2026/VIII/0021	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-28	Tagihan VI/CJR/2026/VIII/0005 (JPF/INV/2608/0011) dari PT Japfa Comfeed Indonesia	Automatic	VendorInvoice	01a106ad-f67d-7994-8047-8c285fd5bfe9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.599913+07	\N	\N	2026-10-04 18:30:26.600158+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f6e8-75d2-8259-2d4660a8bdc9	JO/CJR/2026/VIII/0022	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-28	Tagihan VI/CJR/2026/VIII/0006 (MDN/INV/2608/0012) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ad-f6c0-7485-ac3f-0303994bd9b1	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.669202+07	\N	\N	2026-10-04 18:30:26.669672+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f0ea-7c2e-8354-eea43424a08f	JO/BDG/2026/VIII/0022	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-24	Penerimaan RCV/BDG/2026/VIII/0003 dari PT Sumber Berkah Unggas (RPA)	Automatic	CustomerReceipt	01a106ad-f0d4-7029-a94d-5b00e1f3097e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.135527+07	\N	\N	2026-10-04 18:30:25.135765+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f212-70d6-ac42-ceae631ca7be	JO/BDG/2026/VIII/0023	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-24	Settlement STL/BDG/2026/VIII/0001 plasma H. Ahmad Suryadi	Automatic	PlasmaSettlement	01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	Posted	\N	\N	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.431886+07	\N	\N	2026-10-04 18:30:25.432202+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	\N	\N	{}
01a106ad-f605-7fae-aabe-a1f6f12a4b11	JO/BDG/2026/VIII/0027	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-28	Penerimaan BPB/BDG/2026/VIII/0001 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-f5e8-7414-89fa-95e6aee15d5c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.444277+07	\N	\N	2026-10-04 18:30:26.444524+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f644-78c7-b744-61f2b6a1fbef	JO/BDG/2026/VIII/0028	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-28	Penerimaan BPB/BDG/2026/VIII/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ad-f623-7d55-9b20-a1b5d900f045	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.506376+07	\N	\N	2026-10-04 18:30:26.506693+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f807-76c6-9389-b37ec33708ed	JO/BDG/2026/VIII/0030	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-30	Penerimaan BPB/BDG/2026/VIII/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ad-f7e8-755b-9182-950be853462c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.959181+07	\N	\N	2026-10-04 18:30:26.959537+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f81e-78d8-ab1a-6e8e793dadab	JO/BDG/2026/VIII/0031	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-30	DOC langsung ke kandang BPB/BDG/2026/VIII/0003	Automatic	StockTransferToCycle	01a106ad-f7e8-755b-9182-950be853462c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.980882+07	\N	\N	2026-10-04 18:30:26.981145+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f878-7517-bcc2-ceb24a7bfb1c	JO/BDG/2026/VIII/0032	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-30	Kirim sapronak TRF/BDG/2026/VIII/0003	Automatic	StockTransferToCycle	01a106ad-f84d-7a1b-8cac-41c2469803d4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.070255+07	\N	\N	2026-10-04 18:30:27.070597+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f8e6-7273-8e49-7beba48d05f5	JO/BDG/2026/VIII/0033	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	BKK/BDG/2026/VIII/0007: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-f8ba-736a-a425-932ed8595a38	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.180656+07	\N	\N	2026-10-04 18:30:27.181052+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f969-72c5-b6ff-5350ecde3fe2	JU/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-31	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:27.317178+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.333112+07	\N	\N	2026-10-04 18:30:27.306259+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.333132+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f9ae-790f-b859-fd27a56a0630	JO/BDG/2026/VIII/0034	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	Penerimaan RCV/BDG/2026/VIII/0004 dari UD Jaya Abadi (Bakul)	Automatic	CustomerReceipt	01a106ad-f999-7d5f-ab28-ea8b09d7216b	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.381871+07	\N	\N	2026-10-04 18:30:27.382106+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fa6a-753e-ba91-bc5dd3931c1e	JO/BDG/2026/VIII/0037	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	Retur sapronak RTR/BDG/2026/VIII/0003: Sisa sapronak akhir siklus	Automatic	StockReturnFromCycle	01a106ad-fa43-7c1d-9eb8-33609a30db39	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.569468+07	\N	\N	2026-10-04 18:30:27.569843+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-faa0-7b4e-bd53-7aca600c5ab0	JO/BDG/2026/VIII/0038	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-29	Penyesuaian HPP tutup siklus SKL/BDG/2026/VII/0002	Automatic	CycleCostAdjustment	01a106ad-d05a-759c-a6ff-c3e67df7f41b	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.622775+07	\N	\N	2026-10-04 18:30:27.623035+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fb84-7f23-8e32-cb62f00a03f9	JO/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-01	Pemindahbukuan TRF/CJR/2026/IX/0001: Bank BRI Cianjur ke Kas Kecil Cianjur	Automatic	BankTransfer	01a106ad-fb75-7cd2-ae6f-136641869f21	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.849844+07	\N	\N	2026-10-04 18:30:27.850093+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fc0c-7c20-bf49-6f4d9b71a167	JO/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-02	Penerimaan RCV/BDG/2026/IX/0001 dari UD Jaya Abadi (Bakul)	Automatic	CustomerReceipt	01a106ad-fbf8-75b3-9fec-536538abc1b0	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.987178+07	\N	\N	2026-10-04 18:30:27.987435+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fd6d-7802-9116-80c610542410	JO/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-03	Penjualan INV/CJR/2026/IX/0001 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ad-fd40-7640-a517-a313d071cc33	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.339448+07	\N	\N	2026-10-04 18:30:28.3397+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fed8-7db6-9dc0-20e930e7b2b9	JO/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-04	Penjualan INV/CJR/2026/IX/0002 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ad-fea7-70cb-af7e-26337a183541	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.702493+07	\N	\N	2026-10-04 18:30:28.703033+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-00a5-7372-89db-e96f100c0254	JO/BDG/2026/IX/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-07	BKK/BDG/2026/IX/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-007b-7591-82b9-19fa554b3ff7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.163641+07	\N	\N	2026-10-04 18:30:29.163876+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0105-7000-a2b5-7c7ec14a98ab	JO/CJR/2026/IX/0009	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-07	BKK/CJR/2026/IX/0001: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-00c4-7757-b345-d41c71a47167	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.258825+07	\N	\N	2026-10-04 18:30:29.259047+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-014f-7ee2-a553-003f7b8b0f76	JO/CJR/2026/IX/0010	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-07	Penerimaan RCV/CJR/2026/IX/0002 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-013a-75ef-a556-ed8b25179089	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.333029+07	\N	\N	2026-10-04 18:30:29.333216+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-01a7-7064-8e4b-cfd5d0ec1bca	JO/CJR/2026/IX/0011	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-08	Penerimaan RCV/CJR/2026/IX/0003 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-0196-7c18-8152-cc1a334ef261	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.421458+07	\N	\N	2026-10-04 18:30:29.421673+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-01f9-7dce-a036-139c96a56bea	JO/CJR/2026/IX/0012	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-08	Settlement STL/CJR/2026/IX/0001 plasma Ujang Hermawan	Automatic	PlasmaSettlement	01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	Posted	\N	\N	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.502557+07	\N	\N	2026-10-04 18:30:29.502805+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	\N	\N	{}
01a106ae-03a1-7fa6-9fa1-82e28c4c8796	JO/BDG/2026/IX/0008	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-11	Kirim sapronak TRF/BDG/2026/IX/0002	Automatic	StockTransferToCycle	01a106ae-0386-7605-83ed-efc121166d55	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.927335+07	\N	\N	2026-10-04 18:30:29.927677+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0595-772e-9fe6-b077e3c48794	JO/BDG/2026/IX/0011	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-13	Penerimaan BPB/BDG/2026/IX/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ae-057e-79b5-8334-150eb98cd9a0	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.427454+07	\N	\N	2026-10-04 18:30:30.427643+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-05a6-75a0-bb64-41c283809d3e	JO/BDG/2026/IX/0012	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-13	DOC langsung ke kandang BPB/BDG/2026/IX/0003	Automatic	StockTransferToCycle	01a106ae-057e-79b5-8334-150eb98cd9a0	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.444458+07	\N	\N	2026-10-04 18:30:30.444627+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-05f2-717c-a2bf-af8a5601441c	JO/BDG/2026/IX/0013	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-13	Kirim sapronak TRF/BDG/2026/IX/0003	Automatic	StockTransferToCycle	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.520664+07	\N	\N	2026-10-04 18:30:30.520849+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0742-7dcd-b168-1188647a8b77	JO/CJR/2026/IX/0018	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-15	BKM/CJR/2026/IX/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ae-0723-755d-afb4-7a861862a333	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.856745+07	\N	\N	2026-10-04 18:30:30.856957+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-08aa-7373-b8d1-bac9b4ad3359	JO/BDG/2026/IX/0017	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-18	Tagihan VI/BDG/2026/IX/0005 (JPF/INV/2609/0017) dari PT Japfa Comfeed Indonesia	Automatic	VendorInvoice	01a106ae-0884-733e-afe1-4bc7b7c64b7a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.216939+07	\N	\N	2026-10-04 18:30:31.217123+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-08ef-75e1-a541-50f16deea0ce	JO/BDG/2026/IX/0018	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-18	Tagihan VI/BDG/2026/IX/0006 (MDN/INV/2609/0018) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ae-08c9-7e22-be84-4e4ff54c1e08	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.284541+07	\N	\N	2026-10-04 18:30:31.284772+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-09ec-7718-a4e0-6a7fb6da6035	JO/BDG/2026/IX/0019	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-19	Pembayaran PV/BDG/2026/IX/0001 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ae-099b-79ca-a72e-a4ed31416c6c	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.540401+07	\N	\N	2026-10-04 18:30:31.540581+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0a51-74db-abba-fa4860348b89	JO/BDG/2026/IX/0020	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-19	Pembayaran PV/BDG/2026/IX/0002 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ae-0a09-7aae-b43a-b8080ee12f35	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.639779+07	\N	\N	2026-10-04 18:30:31.639972+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f7ba-7993-aab9-5860172bd406	JO/BDG/2026/VIII/0029	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-30	Penjualan INV/BDG/2026/VIII/0005 kepada UD Jaya Abadi (Bakul)	Automatic	SalesInvoice	01a106ad-f785-70ba-9a35-aaa06a2eaa01	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.888057+07	\N	\N	2026-10-04 18:30:26.888495+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f8fb-7149-8708-5206ed379cfd	JU/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:27.206787+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.225385+07	\N	\N	2026-10-04 18:30:27.195828+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.225413+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f954-7887-b551-273b9e5a2bf4	JO/CJR/2026/VIII/0023	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-08-31	BKK/CJR/2026/VIII/0007: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ad-f926-74e8-8349-63285d1f77f2	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.291952+07	\N	\N	2026-10-04 18:30:27.292287+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fa0b-7a28-8d9b-b39b05d718cc	JO/BDG/2026/VIII/0035	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	Retur sapronak RTR/BDG/2026/VIII/0002: Sisa pakan dipindah ke kandang yang masih berjalan	Automatic	StockReturnFromCycle	01a106ad-f9df-72b6-8bd3-82399696b6f7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.474144+07	\N	\N	2026-10-04 18:30:27.474397+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fa28-75b5-9006-a50428667447	JO/BDG/2026/VIII/0036	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-08-31	Kirim sapronak TRF/BDG/2026/VIII/0004	Automatic	StockTransferToCycle	01a106ad-f9e3-77ab-a80a-c5fa58f74613	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.502002+07	\N	\N	2026-10-04 18:30:27.502255+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fb62-79ad-a4bd-9d9e39f32971	JO/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-01	Pemindahbukuan TRF/BDG/2026/IX/0001: Bank BCA Bandung ke Kas Kecil Bandung	Automatic	BankTransfer	01a106ad-fb53-79b4-8551-6efc939d31a9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.816462+07	\N	\N	2026-10-04 18:30:27.816704+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fc73-7d33-b8eb-db807c642ffc	JO/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-02	Tagihan VI/BDG/2026/IX/0001 (CPI/INV/2609/0013) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-fc4d-79a2-aa5d-f3a2316a240d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.090551+07	\N	\N	2026-10-04 18:30:28.090785+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fde1-7d54-8079-79b1ceb20de4	JO/BDG/2026/IX/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-04	Tagihan VI/BDG/2026/IX/0002 (CPI/INV/2609/0014) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ad-fda0-719c-b3f0-c5c94cd22245	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.456307+07	\N	\N	2026-10-04 18:30:28.456776+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fe34-7757-9761-885d79b044c5	JO/BDG/2026/IX/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-04	Tagihan VI/BDG/2026/IX/0003 (MDN/INV/2609/0015) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ad-fe07-7901-b2df-291722977f96	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.539707+07	\N	\N	2026-10-04 18:30:28.539973+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ff0f-751b-9b28-931d96f22b16	JO/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-04	Kirim sapronak TRF/CJR/2026/IX/0002	Automatic	StockTransferToCycle	01a106ad-fef5-7741-ac7c-fa61f92a0a1e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.757788+07	\N	\N	2026-10-04 18:30:28.75801+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ff81-7290-9431-7059f7c9daf4	JO/CJR/2026/IX/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-05	Penjualan INV/CJR/2026/IX/0003 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ad-ff5a-73aa-bb0b-f6a7a6cff728	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.871096+07	\N	\N	2026-10-04 18:30:28.871473+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ffd4-7897-b241-e3b5a8e65b80	JO/CJR/2026/IX/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-06	Penerimaan RCV/CJR/2026/IX/0001 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ad-ffc3-77bc-a775-24cd57b8ec8a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.954371+07	\N	\N	2026-10-04 18:30:28.954599+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-001d-7e84-a783-4d0c2be0172e	JO/CJR/2026/IX/0007	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-06	Retur sapronak RTR/CJR/2026/IX/0001: Sisa sapronak akhir siklus	Automatic	StockReturnFromCycle	01a106ad-fff3-7186-9838-80f0249d67e4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.02778+07	\N	\N	2026-10-04 18:30:29.028231+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0053-716b-a329-b6533e697c99	JO/CJR/2026/IX/0008	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-04	Penyesuaian HPP tutup siklus SKL/CJR/2026/VII/0001	Automatic	CycleCostAdjustment	01a106ad-d332-7f38-9c34-260051b68ef3	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.081257+07	\N	\N	2026-10-04 18:30:29.081451+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-02e4-7fc6-839d-6c7f7436a56b	JO/BDG/2026/IX/0007	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-10	BKK/BDG/2026/IX/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ae-02ba-7700-831b-a880d12dfc87	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.739208+07	\N	\N	2026-10-04 18:30:29.739439+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0320-734d-b9f0-27f7753d22ae	JO/CJR/2026/IX/0013	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-10	BKK/CJR/2026/IX/0002: Pembayaran listrik & air kantor dan kandang inti	Automatic	CashTransaction	01a106ae-02f8-78a9-8289-ceb1824a7512	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.797825+07	\N	\N	2026-10-04 18:30:29.798076+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0401-7b20-a576-5be500d84c7d	JO/BDG/2026/IX/0009	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-11	Penerimaan BPB/BDG/2026/IX/0001 dari PT Japfa Comfeed Indonesia	Automatic	PurchaseReceipt	01a106ae-03dc-71bc-8c80-5622ee97290b	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.024872+07	\N	\N	2026-10-04 18:30:30.025226+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-043e-73c9-9b24-7810ed975aa6	JO/BDG/2026/IX/0010	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-11	Penerimaan BPB/BDG/2026/IX/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ae-041e-7b65-83ea-b89df6274edb	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.083008+07	\N	\N	2026-10-04 18:30:30.083195+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-04af-7c5c-a900-da81ee876be0	JO/CJR/2026/IX/0014	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-12	Pembayaran PV/CJR/2026/IX/0002 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ae-0481-70c6-ab29-7becb50fede2	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.197098+07	\N	\N	2026-10-04 18:30:30.197351+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-04f9-73ac-ac22-5fc475241132	JO/CJR/2026/IX/0015	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-12	Pembayaran PV/CJR/2026/IX/0003 kepada PT Japfa Comfeed Indonesia	Automatic	VendorPayment	01a106ae-04c9-72f8-9b03-0e6b93c00653	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.271511+07	\N	\N	2026-10-04 18:30:30.2718+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-053e-7dc2-b547-f51a986f6465	JO/CJR/2026/IX/0016	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-12	Pembayaran PV/CJR/2026/IX/0004 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ae-0512-7698-bcc9-2bffa6c8c0a1	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.340011+07	\N	\N	2026-10-04 18:30:30.340266+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0641-75a8-b57e-d0907784a9e4	JO/BDG/2026/IX/0014	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-14	BKK/BDG/2026/IX/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-061a-735c-aa26-ced227719ea4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.59901+07	\N	\N	2026-10-04 18:30:30.599195+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-067b-7f72-9027-f0dcdc774c26	JO/CJR/2026/IX/0017	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-14	BKK/CJR/2026/IX/0003: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-0653-7037-b7d0-d463cef1f588	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.657646+07	\N	\N	2026-10-04 18:30:30.657835+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0711-75c9-903c-8d13b8c8f888	JO/BDG/2026/IX/0015	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-15	BKM/BDG/2026/IX/0001: Penjualan karung pakan bekas	Automatic	CashTransaction	01a106ae-06e1-7167-810d-c915cbc4efcf	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.806974+07	\N	\N	2026-10-04 18:30:30.807191+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-07dd-74a6-8b0d-0fea453851cf	JO/BDG/2026/IX/0016	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-16	Tagihan VI/BDG/2026/IX/0004 (CPI/INV/2609/0016) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ae-07b7-73d5-accf-3820983cc082	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.011518+07	\N	\N	2026-10-04 18:30:31.011716+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ec3-7213-b825-1e01530a1905	JO/CJR/2026/IX/0026	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-25	Penerimaan RCV/CJR/2026/IX/0004 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-0eb2-7e2e-8ae3-e03501f0a745	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.777006+07	\N	\N	2026-10-04 18:30:32.777165+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-10cf-72ba-a3d7-f51a4821f94f	JO/BDG/2026/IX/0024	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-28	BKK/BDG/2026/IX/0006: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-10a8-761f-881e-648b6f39dea3	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.301767+07	\N	\N	2026-10-04 18:30:33.301932+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-110a-736f-a7b4-7b4f1aefac69	JO/CJR/2026/IX/0030	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-28	BKK/CJR/2026/IX/0006: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-10e2-7471-a55e-52e35fcd01dc	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.359093+07	\N	\N	2026-10-04 18:30:33.359238+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-11a4-7884-8543-49fc53361329	JO/CJR/2026/IX/0031	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-28	Penjualan INV/CJR/2026/IX/0004 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ae-1177-79f1-9dab-ecd0831503a9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.51376+07	\N	\N	2026-10-04 18:30:33.51393+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-13f6-75bd-b085-f42eb268a806	JO/CJR/2026/IX/0034	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-30	Penjualan INV/CJR/2026/IX/0006 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ae-13c7-7e8d-bea5-901363b90855	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.107581+07	\N	\N	2026-10-04 18:30:34.10773+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0b0d-737b-b417-13d57deb26be	JO/CJR/2026/IX/0019	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-20	Penerimaan BPB/CJR/2026/IX/0001 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ae-0af1-7541-a16c-02d04ad0ab29	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.825696+07	\N	\N	2026-10-04 18:30:31.825856+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0b44-7d6a-a342-5a0d3a0d1ab2	JO/CJR/2026/IX/0020	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-20	Penerimaan BPB/CJR/2026/IX/0002 dari PT Medion Farma Jaya	Automatic	PurchaseReceipt	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.881788+07	\N	\N	2026-10-04 18:30:31.88198+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0b7e-777a-8993-c95a6c22ac32	JO/BDG/2026/IX/0021	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-21	BKK/BDG/2026/IX/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-0b57-747e-8f52-1af3cd63f0fa	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.941285+07	\N	\N	2026-10-04 18:30:31.941542+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0bb7-71ff-9fbd-1205c7025acd	JO/CJR/2026/IX/0021	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-21	BKK/CJR/2026/IX/0004: BBM & transport PPL lapangan	Automatic	CashTransaction	01a106ae-0b90-7193-a2d9-965f1358e7d7	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.99704+07	\N	\N	2026-10-04 18:30:31.997499+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c65-71ac-bd0c-77c1df781790	JO/CJR/2026/IX/0022	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-22	Penerimaan BPB/CJR/2026/IX/0003 dari PT Charoen Pokphand Indonesia	Automatic	PurchaseReceipt	01a106ae-0c4e-7273-b6e0-82abb0d929f9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.17209+07	\N	\N	2026-10-04 18:30:32.172256+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c7a-75d9-b854-19e2cd456642	JO/CJR/2026/IX/0023	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-22	DOC langsung ke kandang BPB/CJR/2026/IX/0003	Automatic	StockTransferToCycle	01a106ae-0c4e-7273-b6e0-82abb0d929f9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.192291+07	\N	\N	2026-10-04 18:30:32.192477+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0cd1-7dbb-b4ae-a888efe02084	JO/CJR/2026/IX/0024	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-22	Kirim sapronak TRF/CJR/2026/IX/0003	Automatic	StockTransferToCycle	01a106ae-0ca1-70ed-af31-ee7d25077d50	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.284652+07	\N	\N	2026-10-04 18:30:32.285928+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ddc-768a-aaf3-243ff1d02fb8	JO/BDG/2026/IX/0022	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-25	BKK/BDG/2026/IX/0005: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ae-0db5-734d-b34d-889cf46a1e2b	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.546185+07	\N	\N	2026-10-04 18:30:32.546363+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0e16-7b72-9519-4096b36ff242	JO/CJR/2026/IX/0025	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-25	BKK/CJR/2026/IX/0005: Gaji & tunjangan karyawan	Automatic	CashTransaction	01a106ae-0def-71b8-a9fe-a9e0177f013a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.604818+07	\N	\N	2026-10-04 18:30:32.604981+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0e5e-78c7-95f3-65758e5216fb	JO/BDG/2026/IX/0023	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-25	Kirim sapronak TRF/BDG/2026/IX/0004	Automatic	StockTransferToCycle	01a106ae-0e45-7ed6-a7fc-6ba1fe729f41	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.675728+07	\N	\N	2026-10-04 18:30:32.675894+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f14-743b-a82b-c273bf78dc57	JO/CJR/2026/IX/0027	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-25	Tagihan VI/CJR/2026/IX/0001 (CPI/INV/2609/0019) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ae-0ef2-7183-a407-2e028076c45e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.857859+07	\N	\N	2026-10-04 18:30:32.858011+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-103e-7429-b03c-e3fa46dc0175	JO/CJR/2026/IX/0028	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-27	Tagihan VI/CJR/2026/IX/0002 (CPI/INV/2609/0020) dari PT Charoen Pokphand Indonesia	Automatic	VendorInvoice	01a106ae-1018-720e-97c2-1501dfda537f	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.156906+07	\N	\N	2026-10-04 18:30:33.157051+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1081-7bff-83f4-6f69a43d747f	JO/CJR/2026/IX/0029	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-27	Tagihan VI/CJR/2026/IX/0003 (MDN/INV/2609/0021) dari PT Medion Farma Jaya	Automatic	VendorInvoice	01a106ae-105c-738b-9922-d9f3438c009a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.223758+07	\N	\N	2026-10-04 18:30:33.223933+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-121e-7d99-a3a4-2a4deac68e2f	JO/CJR/2026/IX/0032	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-28	Uang muka RCV/CJR/2026/IX/0004 untuk INV/CJR/2026/IX/0004	Automatic	CustomerAdvanceApplied	01a106ae-11e1-7db7-b31a-e4ba63387b40	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.6353+07	\N	\N	2026-10-04 18:30:33.635475+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-12f3-7fda-840e-c18e77d9ab8e	JO/CJR/2026/IX/0033	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-29	Penjualan INV/CJR/2026/IX/0005 kepada CV Unggas Makmur Cianjur	Automatic	SalesInvoice	01a106ae-12c6-7315-9f02-2fa6cd84672d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.849886+07	\N	\N	2026-10-04 18:30:33.850045+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-131d-77c3-a3a4-ae65293f4cac	JU/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-09-30	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:33.896744+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.911576+07	\N	\N	2026-10-04 18:30:33.886052+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.911614+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1345-7911-b8a2-b88a993a24c7	JU/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-30	Penyusutan aset tetap bulanan	Manual	\N	\N	Posted	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:33.93654+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.952546+07	\N	\N	2026-10-04 18:30:33.925649+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.952569+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1433-76c3-8598-7153bdbf54a9	JO/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-01	Pemindahbukuan TRF/BDG/2026/X/0001: Bank BCA Bandung ke Kas Kecil Bandung	Automatic	BankTransfer	01a106ae-1424-7fdd-9d0b-354de397630e	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.169934+07	\N	\N	2026-10-04 18:30:34.170131+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-14b7-7c6f-bbb1-b34d56fe8294	JO/CJR/2026/X/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-10-01	Penerimaan RCV/CJR/2026/X/0001 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-14a1-7f7a-b0ef-4b0f307eb679	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.300154+07	\N	\N	2026-10-04 18:30:34.300388+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-14fa-7e61-b38a-d35290c8695e	JO/CJR/2026/X/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-10-01	Retur sapronak RTR/CJR/2026/X/0001: Sisa sapronak akhir siklus	Automatic	StockReturnFromCycle	01a106ae-14d0-79b6-b4e1-bfafb1945a08	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.368111+07	\N	\N	2026-10-04 18:30:34.368266+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-152e-70ae-8f19-0fe67f707c50	JO/CJR/2026/IX/0035	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-09-29	Penyesuaian HPP tutup siklus SKL/CJR/2026/VIII/0001	Automatic	CycleCostAdjustment	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.41978+07	\N	\N	2026-10-04 18:30:34.419915+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-180a-7e1f-901c-24a1eccd7505	JO/CJR/2026/X/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-10-03	Penerimaan RCV/CJR/2026/X/0003 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-17f8-7f8a-88cf-3bf2a729f0c9	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.15105+07	\N	\N	2026-10-04 18:30:35.151209+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1456-70e7-93dc-230ef5292b34	JO/CJR/2026/X/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-10-01	Pemindahbukuan TRF/CJR/2026/X/0001: Bank BRI Cianjur ke Kas Kecil Cianjur	Automatic	BankTransfer	01a106ae-1447-713b-b007-4166f3656038	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.204388+07	\N	\N	2026-10-04 18:30:34.204606+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-15dc-745c-a5ad-b5f5c775dd6c	JO/CJR/2026/X/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	2026-10-02	Penerimaan RCV/CJR/2026/X/0002 dari CV Unggas Makmur Cianjur	Automatic	CustomerReceipt	01a106ae-15b8-700b-af83-753cfbb10a2a	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.596303+07	\N	\N	2026-10-04 18:30:34.596572+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1687-7b73-b756-edf8c637ef02	JO/BDG/2026/X/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-03	Penjualan INV/BDG/2026/X/0001 kepada PT Sumber Berkah Unggas (RPA)	Automatic	SalesInvoice	01a106ae-1653-7ba7-b963-74958f58aa0d	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.765797+07	\N	\N	2026-10-04 18:30:34.766026+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-16d8-74f2-9603-432da6f51ea2	JO/BDG/2026/X/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-03	Pembayaran PV/BDG/2026/X/0001 kepada PT Charoen Pokphand Indonesia	Automatic	VendorPayment	01a106ae-16a4-7f5a-a671-9f125c3c9dde	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.846496+07	\N	\N	2026-10-04 18:30:34.846658+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1720-7030-af18-6eb72a8d08cc	JO/BDG/2026/X/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-03	Pembayaran PV/BDG/2026/X/0002 kepada PT Japfa Comfeed Indonesia	Automatic	VendorPayment	01a106ae-16f1-74b8-90a5-710acc7d3d70	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.918379+07	\N	\N	2026-10-04 18:30:34.918551+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1766-73a3-a3c7-763cacf696b6	JO/BDG/2026/X/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-03	Pembayaran PV/BDG/2026/X/0003 kepada PT Medion Farma Jaya	Automatic	VendorPayment	01a106ae-1738-70ac-ae0b-b009733f4567	Posted	\N	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.987783+07	\N	\N	2026-10-04 18:30:34.987967+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1a57-782c-802f-2ca41827eb8c	\N	01a106ad-b536-7f76-bf02-1115db4d6aff	2026-10-03	Reklasifikasi biaya ATK ke beban umum	Manual	\N	\N	Draft	\N	\N	\N	\N	\N	\N	2026-10-04 18:30:35.735769+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: journal_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_lines (journal_entry_id, line_number, account_id, cost_center_id, description, credit, debit) FROM stdin;
01a106ad-c2de-75df-b31f-49ff1b32abd1	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	Saldo bank	0.00	2500000000.00
01a106ad-c2de-75df-b31f-49ff1b32abd1	2	01a0f50c-aafa-75da-a609-c4447cb81362	\N	Saldo kas besar	0.00	50000000.00
01a106ad-c2de-75df-b31f-49ff1b32abd1	3	01a0f50c-aafa-7f2c-85b5-58e6de59dbf0	\N	Bangunan kandang inti	0.00	1200000000.00
01a106ad-c2de-75df-b31f-49ff1b32abd1	4	01a0f50c-aafa-7d36-b6e8-42bbfeb41372	\N	Peralatan kandang inti	0.00	350000000.00
01a106ad-c2de-75df-b31f-49ff1b32abd1	5	01a0f50c-aafa-7271-94d9-5cce04e41d9b	\N	Modal disetor	4100000000.00	0.00
01a106ad-c424-7c83-8f5c-756d6c810f2d	1	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	0.00	15000000.00
01a106ad-c424-7c83-8f5c-756d6c810f2d	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	15000000.00	0.00
01a106ad-c445-7d81-b732-0a2a80f68d53	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	Saldo bank	0.00	1875000000.00
01a106ad-c445-7d81-b732-0a2a80f68d53	2	01a106ad-ba9c-728f-b38e-3fa3e2849611	\N	Saldo kas besar	0.00	37500000.00
01a106ad-c445-7d81-b732-0a2a80f68d53	3	01a0f50c-aafa-7f2c-85b5-58e6de59dbf0	\N	Bangunan kandang inti	0.00	900000000.00
01a106ad-c445-7d81-b732-0a2a80f68d53	4	01a0f50c-aafa-7d36-b6e8-42bbfeb41372	\N	Peralatan kandang inti	0.00	262500000.00
01a106ad-c445-7d81-b732-0a2a80f68d53	5	01a0f50c-aafa-7271-94d9-5cce04e41d9b	\N	Modal disetor	3075000000.00	0.00
01a106ad-c485-70d9-8223-fa71882a85aa	1	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	0.00	15000000.00
01a106ad-c485-70d9-8223-fa71882a85aa	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	15000000.00	0.00
01a106ad-c56b-706f-8d43-1e873cf6097f	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-c56b-706f-8d43-1e873cf6097f	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ad-c56b-706f-8d43-1e873cf6097f	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1890000.00	0.00
01a106ad-c5b6-7474-9f11-92c3cfbfa142	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-c5b6-7474-9f11-92c3cfbfa142	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ad-c5b6-7474-9f11-92c3cfbfa142	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1890000.00	0.00
01a106ad-c60e-78d0-ae7f-255cd0eaf991	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	7450000.00
01a106ad-c60e-78d0-ae7f-255cd0eaf991	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	7450000.00	0.00
01a106ad-c658-784b-a7f7-25f49abf2023	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	5180000.00
01a106ad-c658-784b-a7f7-25f49abf2023	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	5180000.00	0.00
01a106ad-c8a4-770d-acc7-078e0f57fa99	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-c8a4-770d-acc7-078e0f57fa99	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ad-c8a4-770d-acc7-078e0f57fa99	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1970000.00	0.00
01a106ad-c8e4-7a06-ac4d-a2d0c59f8759	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-c8e4-7a06-ac4d-a2d0c59f8759	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ad-c8e4-7a06-ac4d-a2d0c59f8759	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1970000.00	0.00
01a106ad-ca0b-7631-aaa1-6b0f7a4ec843	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	113732500.00
01a106ad-ca0b-7631-aaa1-6b0f7a4ec843	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	113732500.00	0.00
01a106ad-ca55-7f77-992d-3b6ec5c41b73	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	2277000.00
01a106ad-ca55-7f77-992d-3b6ec5c41b73	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	2277000.00	0.00
01a106ad-ca89-716b-bef3-1d82a5ee5b18	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ad-ca89-716b-bef3-1d82a5ee5b18	2	01a0f50c-aafa-75da-a609-c4447cb81362	\N	\N	0.00	1450000.00
01a106ad-cabf-7cd0-a8b1-363292123dad	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ad-cabf-7cd0-a8b1-363292123dad	2	01a106ad-ba9c-728f-b38e-3fa3e2849611	\N	\N	0.00	1450000.00
01a106ad-cb36-73ee-8d81-4f9166dc9ebd	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	37000000.00
01a106ad-cb36-73ee-8d81-4f9166dc9ebd	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	37000000.00	0.00
01a106ad-cb4f-716f-b776-4b8bd2d05bef	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	37000000.00
01a106ad-cb4f-716f-b776-4b8bd2d05bef	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	37000000.00	0.00
01a106ad-cc4e-7124-8ecc-52922aa61e5f	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	24857500.00
01a106ad-cc4e-7124-8ecc-52922aa61e5f	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	24857500.00	0.00
01a106ad-cc4e-7124-8ecc-52922aa61e5f	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	2277000.00
01a106ad-cc4e-7124-8ecc-52922aa61e5f	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	2277000.00	0.00
01a106ad-ce43-7d55-bc58-6d3b0d034353	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	37000000.00
01a106ad-ce43-7d55-bc58-6d3b0d034353	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	37000000.00	0.00
01a106ad-cea1-7f98-a52c-aca7128720ff	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-cea1-7f98-a52c-aca7128720ff	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ad-cea1-7f98-a52c-aca7128720ff	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1850000.00	0.00
01a106ad-cf29-774c-aa91-2e1e58c7940d	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-cf29-774c-aa91-2e1e58c7940d	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ad-cf29-774c-aa91-2e1e58c7940d	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1850000.00	0.00
01a106ad-cf95-7fdb-853d-a8d574d4910b	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	113732500.00
01a106ad-cf95-7fdb-853d-a8d574d4910b	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	113732500.00	0.00
01a106ad-cff8-7e51-9a7c-d0c0a058cb4a	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	2277000.00
01a106ad-cff8-7e51-9a7c-d0c0a058cb4a	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	2277000.00	0.00
01a106ad-cff8-7e51-9a7c-d0c0a058cb4a	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	250470.00
01a106ad-cff8-7e51-9a7c-d0c0a058cb4a	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	250470.00	0.00
01a106ad-d134-7b8a-a418-4e8573fcb910	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	106332500.00
01a106ad-d134-7b8a-a418-4e8573fcb910	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	106332500.00	0.00
01a106ad-d174-7585-9b9b-34de98516846	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	2277000.00
01a106ad-d174-7585-9b9b-34de98516846	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	2277000.00	0.00
01a106ad-d1bd-7768-b9e6-6a8e03483618	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	56225000.00
01a106ad-d1bd-7768-b9e6-6a8e03483618	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	30275000.00
01a106ad-d1bd-7768-b9e6-6a8e03483618	3	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	86500000.00	0.00
01a106ad-d202-7877-aaab-6b6a94598175	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	39780000.00
01a106ad-d202-7877-aaab-6b6a94598175	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	21420000.00
01a106ad-d202-7877-aaab-6b6a94598175	3	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	61200000.00	0.00
01a106ad-d271-7801-9fb7-fe26402d86aa	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	33300000.00
01a106ad-d271-7801-9fb7-fe26402d86aa	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	33300000.00	0.00
01a106ad-d286-7c12-9339-4a95616a2063	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	33300000.00
01a106ad-d286-7c12-9339-4a95616a2063	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	33300000.00	0.00
01a106ad-d30a-7c51-88ba-27dd372cb8c9	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	24300000.00
01a106ad-d30a-7c51-88ba-27dd372cb8c9	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	24300000.00	0.00
01a106ad-d30a-7c51-88ba-27dd372cb8c9	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	2277000.00
01a106ad-d30a-7c51-88ba-27dd372cb8c9	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	2277000.00	0.00
01a106ad-d3de-7d4b-9444-bc2080f2a6c0	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-d3de-7d4b-9444-bc2080f2a6c0	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	355000.00
01a106ad-d3de-7d4b-9444-bc2080f2a6c0	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1930000.00	0.00
01a106ad-d424-7f64-bfc5-e923be8a57b5	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-d424-7f64-bfc5-e923be8a57b5	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	355000.00
01a106ad-d424-7f64-bfc5-e923be8a57b5	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1930000.00	0.00
01a106ad-d50a-746c-af0f-c4cb7cc4d68b	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	88604118.66
01a106ad-d50a-746c-af0f-c4cb7cc4d68b	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	88604118.66	0.00
01a106ad-d584-7347-b05d-db9e642044d7	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	137940000.00
01a106ad-d584-7347-b05d-db9e642044d7	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	137940000.00	0.00
01a106ad-d5cf-7c8a-b18b-21998dc2722f	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	2277000.00
01a106ad-d5cf-7c8a-b18b-21998dc2722f	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	2277000.00	0.00
01a106ad-d65b-7a68-9a4d-1898cad3cdd3	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	33300000.00
01a106ad-d65b-7a68-9a4d-1898cad3cdd3	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	33300000.00	0.00
01a106ad-d6ed-732b-befa-5bde51400a96	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	37000000.00
01a106ad-d6ed-732b-befa-5bde51400a96	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	37000000.00	0.00
01a106ad-d701-731c-80db-661b09b81633	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	37000000.00
01a106ad-d701-731c-80db-661b09b81633	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	37000000.00	0.00
01a106ad-d75f-753d-bb13-a5e580df1e32	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	28525000.00
01a106ad-d75f-753d-bb13-a5e580df1e32	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	28525000.00	0.00
01a106ad-d75f-753d-bb13-a5e580df1e32	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	2277000.00
01a106ad-d75f-753d-bb13-a5e580df1e32	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	2277000.00	0.00
01a106ad-d7ae-763d-a218-153532287670	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	8742857.14
01a106ad-d7ae-763d-a218-153532287670	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	8742857.14	0.00
01a106ad-d8ea-7715-a431-24d7e4113148	1	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	0.00	10000000.00
01a106ad-d8ea-7715-a431-24d7e4113148	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	10000000.00	0.00
01a106ad-da0b-7e81-a046-ef89e52d5a37	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	37000000.00
01a106ad-da0b-7e81-a046-ef89e52d5a37	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	37000000.00	0.00
01a106ad-db91-7331-987a-0870812b7792	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	137940000.00
01a106ad-db91-7331-987a-0870812b7792	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	137940000.00	0.00
01a106ad-dbda-766d-8b9a-322bb4ad4f94	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	2277000.00
01a106ad-dbda-766d-8b9a-322bb4ad4f94	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	2277000.00	0.00
01a106ad-dbda-766d-8b9a-322bb4ad4f94	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	250470.00
01a106ad-dbda-766d-8b9a-322bb4ad4f94	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	250470.00	0.00
01a106ad-dfb1-7711-8e75-18a3275ebc9d	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	7450000.00
01a106ad-dfb1-7711-8e75-18a3275ebc9d	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	7450000.00	0.00
01a106ad-dffc-77e2-a82a-61bc4ab41938	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-dffc-77e2-a82a-61bc4ab41938	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ad-dffc-77e2-a82a-61bc4ab41938	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1890000.00	0.00
01a106ad-e03e-7c20-be29-a4ecc44c959a	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	5180000.00
01a106ad-e03e-7c20-be29-a4ecc44c959a	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	5180000.00	0.00
01a106ad-e07c-7514-8a8c-2b16d440f03c	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-e07c-7514-8a8c-2b16d440f03c	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ad-e07c-7514-8a8c-2b16d440f03c	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1890000.00	0.00
01a106ad-e148-7561-9558-8bb74205c02f	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	109415000.00
01a106ad-e148-7561-9558-8bb74205c02f	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	109415000.00	0.00
01a106ad-e2e4-763d-af07-f1be49771ce4	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ad-e2e4-763d-af07-f1be49771ce4	2	01a106ad-ba9c-728f-b38e-3fa3e2849611	\N	\N	0.00	1450000.00
01a106ad-e5b5-71b0-8d19-cc5f390ba878	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-e5b5-71b0-8d19-cc5f390ba878	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ad-e5b5-71b0-8d19-cc5f390ba878	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1970000.00	0.00
01a106ad-e61c-7f90-bd0b-ca8a888fbf7a	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-e61c-7f90-bd0b-ca8a888fbf7a	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ad-e61c-7f90-bd0b-ca8a888fbf7a	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1970000.00	0.00
01a106ad-e9d4-7a35-931f-17232db5e5d6	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	174940000.00
01a106ad-e9d4-7a35-931f-17232db5e5d6	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	174940000.00	0.00
01a106ad-ea27-7aed-bd54-4875da11b3c0	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	2527470.00
01a106ad-ea27-7aed-bd54-4875da11b3c0	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	2527470.00	0.00
01a106ad-eb76-74e2-a654-a417c81ac15a	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	66450760.00
01a106ad-eb76-74e2-a654-a417c81ac15a	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	66450760.00	0.00
01a106ad-eb76-74e2-a654-a417c81ac15a	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	48276599.07
01a106ad-eb76-74e2-a654-a417c81ac15a	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	48276599.07	0.00
01a106ad-ebf4-7205-9cce-47b3000e0f2f	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	71453860.00
01a106ad-ebf4-7205-9cce-47b3000e0f2f	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	71453860.00	0.00
01a106ad-ebf4-7205-9cce-47b3000e0f2f	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	51911360.40
01a106ad-ebf4-7205-9cce-47b3000e0f2f	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	51911360.40	0.00
01a106ad-ec6b-757c-a321-259e1e5a5e21	1	01a0f50c-aafa-7a19-b189-7edcb4dec59d	\N	\N	0.00	750000.00
01a106ad-ec6b-757c-a321-259e1e5a5e21	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	750000.00	0.00
01a106ad-efcc-7fcc-865b-eec0bd50e112	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	51800000.00
01a106ad-efcc-7fcc-865b-eec0bd50e112	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	51800000.00	0.00
01a106ad-efe1-7fc0-a130-bd2aeeeb5ad7	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	51800000.00
01a106ad-efe1-7fc0-a130-bd2aeeeb5ad7	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	51800000.00	0.00
01a106ad-f035-7ae5-9f06-9dd2daf6c907	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	36855000.00
01a106ad-f035-7ae5-9f06-9dd2daf6c907	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	36855000.00	0.00
01a106ad-f035-7ae5-9f06-9dd2daf6c907	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	3091000.00
01a106ad-f035-7ae5-9f06-9dd2daf6c907	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	3091000.00	0.00
01a106ad-f073-7434-8612-1c045fcd87d6	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-f073-7434-8612-1c045fcd87d6	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ad-f073-7434-8612-1c045fcd87d6	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1850000.00	0.00
01a106ad-f0b3-7ba6-b6a7-4ebe97e7feb0	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-f0b3-7ba6-b6a7-4ebe97e7feb0	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ad-f0b3-7ba6-b6a7-4ebe97e7feb0	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1850000.00	0.00
01a106ad-f0ea-7c2e-8354-eea43424a08f	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	0.00	71453860.00
01a106ad-f0ea-7c2e-8354-eea43424a08f	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	71453860.00	0.00
01a106ad-f212-70d6-ac42-ceae631ca7be	1	01a0f50c-aafa-72d3-acd7-b789ac25dc81	\N	\N	0.00	33225195.00
01a106ad-f212-70d6-ac42-ceae631ca7be	2	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N	\N	33225195.00	0.00
01a106ad-f212-70d6-ac42-ceae631ca7be	3	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N	\N	0.00	664503.90
01a106ad-f212-70d6-ac42-ceae631ca7be	4	01a0f50c-aafa-7f88-bf00-8dfd3adebe85	\N	\N	664503.90	0.00
01a106ad-f605-7fae-aabe-a1f6f12a4b11	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	147100000.00
01a106ad-f605-7fae-aabe-a1f6f12a4b11	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	147100000.00	0.00
01a106ad-f644-78c7-b744-61f2b6a1fbef	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	2579000.00
01a106ad-f644-78c7-b744-61f2b6a1fbef	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	2579000.00	0.00
01a106ad-f807-76c6-9389-b37ec33708ed	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	44400000.00
01a106ad-f807-76c6-9389-b37ec33708ed	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	44400000.00	0.00
01a106ad-f81e-78d8-ab1a-6e8e793dadab	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	44400000.00
01a106ad-f81e-78d8-ab1a-6e8e793dadab	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	44400000.00	0.00
01a106ad-f878-7517-bcc2-ceb24a7bfb1c	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	30970000.00
01a106ad-f878-7517-bcc2-ceb24a7bfb1c	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	30970000.00	0.00
01a106ad-f878-7517-bcc2-ceb24a7bfb1c	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	2579000.00
01a106ad-f878-7517-bcc2-ceb24a7bfb1c	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	2579000.00	0.00
01a106ad-f8e6-7273-8e49-7beba48d05f5	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-f8e6-7273-8e49-7beba48d05f5	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	355000.00
01a106ad-f8e6-7273-8e49-7beba48d05f5	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1930000.00	0.00
01a106ad-f969-72c5-b6ff-5350ecde3fe2	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	8742857.14
01a106ad-f969-72c5-b6ff-5350ecde3fe2	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	8742857.14	0.00
01a106ad-f9ae-790f-b859-fd27a56a0630	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	0.00	45609000.00
01a106ad-f9ae-790f-b859-fd27a56a0630	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	45609000.00	0.00
01a106ad-fa6a-753e-ba91-bc5dd3931c1e	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	137000.00
01a106ad-fa6a-753e-ba91-bc5dd3931c1e	2	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	137000.00	0.00
01a106ad-faa0-7b4e-bd53-7aca600c5ab0	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	1082569.19
01a106ad-faa0-7b4e-bd53-7aca600c5ab0	2	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	1082569.19	0.00
01a106ad-fb84-7f23-8e32-cb62f00a03f9	1	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	0.00	10000000.00
01a106ad-fb84-7f23-8e32-cb62f00a03f9	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	10000000.00	0.00
01a106ad-fc0c-7c20-bf49-6f4d9b71a167	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	0.00	51247000.00
01a106ad-fc0c-7c20-bf49-6f4d9b71a167	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	51247000.00	0.00
01a106ad-fd6d-7802-9116-80c610542410	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	61995250.00
01a106ad-fd6d-7802-9116-80c610542410	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	61995250.00	0.00
01a106ad-fd6d-7802-9116-80c610542410	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	52991723.10
01a106ad-fd6d-7802-9116-80c610542410	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	52991723.10	0.00
01a106ad-fed8-7db6-9dc0-20e930e7b2b9	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	65149300.00
01a106ad-fed8-7db6-9dc0-20e930e7b2b9	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	65149300.00	0.00
01a106ad-fed8-7db6-9dc0-20e930e7b2b9	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	55656713.67
01a106ad-fed8-7db6-9dc0-20e930e7b2b9	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	55656713.67	0.00
01a106ad-d777-7e7c-9acf-cd10cc9fef9c	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	12357142.86
01a106ad-d777-7e7c-9acf-cd10cc9fef9c	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	12357142.86	0.00
01a106ad-d83b-76d3-b51b-7acf5de4f84c	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	106332500.00
01a106ad-d83b-76d3-b51b-7acf5de4f84c	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	106332500.00	0.00
01a106ad-d88d-7f93-9c71-8e78e7d21810	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	2277000.00
01a106ad-d88d-7f93-9c71-8e78e7d21810	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	2277000.00	0.00
01a106ad-d88d-7f93-9c71-8e78e7d21810	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	250470.00
01a106ad-d88d-7f93-9c71-8e78e7d21810	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	250470.00	0.00
01a106ad-d928-77f6-9154-02389f15388b	1	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	0.00	10000000.00
01a106ad-d928-77f6-9154-02389f15388b	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	10000000.00	0.00
01a106ad-da69-7b3e-80d7-3e5bf2ddf804	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-da69-7b3e-80d7-3e5bf2ddf804	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	235000.00
01a106ad-da69-7b3e-80d7-3e5bf2ddf804	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1810000.00	0.00
01a106ad-daab-792a-b629-7ce566e79605	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-daab-792a-b629-7ce566e79605	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	235000.00
01a106ad-daab-792a-b629-7ce566e79605	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1810000.00	0.00
01a106ad-dd4e-787f-bc44-64aa3fc3cee3	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	150732500.00
01a106ad-dd4e-787f-bc44-64aa3fc3cee3	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	150732500.00	0.00
01a106ad-dd9f-72e9-9b23-7b93d3324a31	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	2527470.00
01a106ad-dd9f-72e9-9b23-7b93d3324a31	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	2527470.00	0.00
01a106ad-de91-7779-8853-d49a6028029f	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	82303381.34
01a106ad-de91-7779-8853-d49a6028029f	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	82303381.34	0.00
01a106ad-e2a3-7ee7-833d-cf92f7fda271	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ad-e2a3-7ee7-833d-cf92f7fda271	2	01a0f50c-aafa-75da-a609-c4447cb81362	\N	\N	0.00	1450000.00
01a106ad-e35d-7eee-a33c-b507e3eb5270	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	33300000.00
01a106ad-e35d-7eee-a33c-b507e3eb5270	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	33300000.00	0.00
01a106ad-e3af-70c9-8886-b9da7f060c32	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	106332500.00
01a106ad-e3af-70c9-8886-b9da7f060c32	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	106332500.00	0.00
01a106ad-e3ff-7f44-b70c-d95bbc3d9d0f	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	2527470.00
01a106ad-e3ff-7f44-b70c-d95bbc3d9d0f	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	2527470.00	0.00
01a106ad-e961-7cea-b7d9-eec09af3cf3c	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	63865280.00
01a106ad-e961-7cea-b7d9-eec09af3cf3c	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	63865280.00	0.00
01a106ad-e961-7cea-b7d9-eec09af3cf3c	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	47035313.92
01a106ad-e961-7cea-b7d9-eec09af3cf3c	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	47035313.92	0.00
01a106ad-eccf-791e-8ce1-0e705d33d373	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	188360000.00
01a106ad-eccf-791e-8ce1-0e705d33d373	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	188360000.00	0.00
01a106ad-ed0a-79cd-8ec4-89707c0de8bc	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	3091000.00
01a106ad-ed0a-79cd-8ec4-89707c0de8bc	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	3091000.00	0.00
01a106ad-edb2-7bcf-9765-1137cfac49ca	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	0.00	63115280.00
01a106ad-edb2-7bcf-9765-1137cfac49ca	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	63115280.00	0.00
01a106ad-ee6c-7755-ba01-c69159dae764	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	6015436.85
01a106ad-ee6c-7755-ba01-c69159dae764	2	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	6015436.85	0.00
01a106ad-ee6c-7755-ba01-c69159dae764	3	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	137000.00
01a106ad-ee6c-7755-ba01-c69159dae764	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	137000.00	0.00
01a106ad-ef09-79b0-a677-5566eaae74e2	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	637091.58
01a106ad-ef09-79b0-a677-5566eaae74e2	2	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	637091.58	0.00
01a106ad-ef67-714d-9893-a4ab237caf34	1	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	0.00	66450760.00
01a106ad-ef67-714d-9893-a4ab237caf34	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	66450760.00	0.00
01a106ad-f290-717d-880d-3699f1ad9967	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	56225000.00
01a106ad-f290-717d-880d-3699f1ad9967	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	30275000.00
01a106ad-f290-717d-880d-3699f1ad9967	3	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	86500000.00	0.00
01a106ad-f2d1-78a6-b098-ca93c0f34226	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	39780000.00
01a106ad-f2d1-78a6-b098-ca93c0f34226	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	21420000.00
01a106ad-f2d1-78a6-b098-ca93c0f34226	3	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	61200000.00	0.00
01a106ad-f3d7-7e9f-b3a3-4cbca1ddbb86	1	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N	\N	0.00	32560691.10
01a106ad-f3d7-7e9f-b3a3-4cbca1ddbb86	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	32560691.10	0.00
01a106ad-f4bf-7afc-a903-e9caa72d7e2e	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	51800000.00
01a106ad-f4bf-7afc-a903-e9caa72d7e2e	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	51800000.00	0.00
01a106ad-f5c8-7754-988d-972f0e49c0ac	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	76015440.00
01a106ad-f5c8-7754-988d-972f0e49c0ac	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	76015440.00	0.00
01a106ad-f5c8-7754-988d-972f0e49c0ac	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	65278419.70
01a106ad-f5c8-7754-988d-972f0e49c0ac	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	65278419.70	0.00
01a106ad-f6a3-7055-8a36-9d50e4d7b53a	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	188360000.00
01a106ad-f6a3-7055-8a36-9d50e4d7b53a	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	188360000.00	0.00
01a106ad-f6e8-75d2-8259-2d4660a8bdc9	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	3091000.00
01a106ad-f6e8-75d2-8259-2d4660a8bdc9	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	3091000.00	0.00
01a106ad-f6e8-75d2-8259-2d4660a8bdc9	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	340010.00
01a106ad-f6e8-75d2-8259-2d4660a8bdc9	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	340010.00	0.00
01a106ad-f7ba-7993-aab9-5860172bd406	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	85410870.00
01a106ad-f7ba-7993-aab9-5860172bd406	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	85410870.00	0.00
01a106ad-f7ba-7993-aab9-5860172bd406	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	72130361.89
01a106ad-f7ba-7993-aab9-5860172bd406	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	72130361.89	0.00
01a106ad-f8fb-7149-8708-5206ed379cfd	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	12357142.86
01a106ad-f8fb-7149-8708-5206ed379cfd	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	12357142.86	0.00
01a106ad-f954-7887-b551-273b9e5a2bf4	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ad-f954-7887-b551-273b9e5a2bf4	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	355000.00
01a106ad-f954-7887-b551-273b9e5a2bf4	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1930000.00	0.00
01a106ad-fa0b-7a28-8d9b-b39b05d718cc	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	5717168.94
01a106ad-fa0b-7a28-8d9b-b39b05d718cc	2	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	5717168.94	0.00
01a106ad-fa28-75b5-9006-a50428667447	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	5733876.47
01a106ad-fa28-75b5-9006-a50428667447	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	5733876.47	0.00
01a106ad-fb62-79ad-a4bd-9d9e39f32971	1	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	0.00	10000000.00
01a106ad-fb62-79ad-a4bd-9d9e39f32971	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	10000000.00	0.00
01a106ad-fc73-7d33-b8eb-db807c642ffc	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	44400000.00
01a106ad-fc73-7d33-b8eb-db807c642ffc	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	44400000.00	0.00
01a106ad-fde1-7d54-8079-79b1ceb20de4	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	147100000.00
01a106ad-fde1-7d54-8079-79b1ceb20de4	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	147100000.00	0.00
01a106ad-fe34-7757-9761-885d79b044c5	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	2579000.00
01a106ad-fe34-7757-9761-885d79b044c5	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	2579000.00	0.00
01a106ad-fe34-7757-9761-885d79b044c5	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	283690.00
01a106ad-fe34-7757-9761-885d79b044c5	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	283690.00	0.00
01a106ad-ff0f-751b-9b28-931d96f22b16	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	151505000.00
01a106ad-ff0f-751b-9b28-931d96f22b16	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	151505000.00	0.00
01a106ad-ff81-7290-9431-7059f7c9daf4	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	68288300.00
01a106ad-ff81-7290-9431-7059f7c9daf4	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	68288300.00	0.00
01a106ad-ff81-7290-9431-7059f7c9daf4	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	58338345.31
01a106ad-ff81-7290-9431-7059f7c9daf4	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	58338345.31	0.00
01a106ad-ffd4-7897-b241-e3b5a8e65b80	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	61995250.00
01a106ad-ffd4-7897-b241-e3b5a8e65b80	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	61995250.00	0.00
01a106ae-001d-7e84-a783-4d0c2be0172e	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	10122755.00
01a106ae-001d-7e84-a783-4d0c2be0172e	2	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	10122755.00	0.00
01a106ae-001d-7e84-a783-4d0c2be0172e	3	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	137000.00
01a106ae-001d-7e84-a783-4d0c2be0172e	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	137000.00	0.00
01a106ae-0053-716b-a329-b6533e697c99	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	29537.08
01a106ae-0053-716b-a329-b6533e697c99	2	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	29537.08	0.00
01a106ae-02e4-7fc6-839d-6c7f7436a56b	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	7450000.00
01a106ae-02e4-7fc6-839d-6c7f7436a56b	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	7450000.00	0.00
01a106ae-0320-734d-b9f0-27f7753d22ae	1	01a0f50c-aafa-78a4-9541-e7668ffaffcd	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	5180000.00
01a106ae-0320-734d-b9f0-27f7753d22ae	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	5180000.00	0.00
01a106ae-0401-7b20-a576-5be500d84c7d	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	215212500.00
01a106ae-0401-7b20-a576-5be500d84c7d	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	215212500.00	0.00
01a106ae-043e-73c9-9b24-7810ed975aa6	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	3393000.00
01a106ae-043e-73c9-9b24-7810ed975aa6	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	3393000.00	0.00
01a106ae-04af-7c5c-a900-da81ee876be0	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	51800000.00
01a106ae-04af-7c5c-a900-da81ee876be0	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	51800000.00	0.00
01a106ae-04f9-73ac-ac22-5fc475241132	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	188360000.00
01a106ae-04f9-73ac-ac22-5fc475241132	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	188360000.00	0.00
01a106ae-053e-7dc2-b547-f51a986f6465	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	3431010.00
01a106ae-053e-7dc2-b547-f51a986f6465	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	3431010.00	0.00
01a106ae-0641-75a8-b57e-d0907784a9e4	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-0641-75a8-b57e-d0907784a9e4	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ae-0641-75a8-b57e-d0907784a9e4	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1890000.00	0.00
01a106ae-067b-7f72-9027-f0dcdc774c26	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-067b-7f72-9027-f0dcdc774c26	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	315000.00
01a106ae-067b-7f72-9027-f0dcdc774c26	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1890000.00	0.00
01a106ae-0711-75c9-903c-8d13b8c8f888	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ae-0711-75c9-903c-8d13b8c8f888	2	01a0f50c-aafa-75da-a609-c4447cb81362	\N	\N	0.00	1450000.00
01a106ae-07dd-74a6-8b0d-0fea453851cf	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	59200000.00
01a106ae-07dd-74a6-8b0d-0fea453851cf	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	59200000.00	0.00
01a106ae-0ec3-7213-b825-1e01530a1905	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	50000000.00
01a106ae-0ec3-7213-b825-1e01530a1905	2	01a0f50c-aafa-7969-8d0c-bf5f3dc5e227	\N	\N	50000000.00	0.00
01a106ae-10cf-72ba-a3d7-f51a4821f94f	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-10cf-72ba-a3d7-f51a4821f94f	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ae-10cf-72ba-a3d7-f51a4821f94f	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1850000.00	0.00
01a106ae-110a-736f-a7b4-7b4f1aefac69	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-110a-736f-a7b4-7b4f1aefac69	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	275000.00
01a106ae-110a-736f-a7b4-7b4f1aefac69	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1850000.00	0.00
01a106ae-11a4-7884-8543-49fc53361329	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	99060270.00
01a106ae-11a4-7884-8543-49fc53361329	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	99060270.00	0.00
01a106ae-11a4-7884-8543-49fc53361329	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	73985220.75
01a106ae-11a4-7884-8543-49fc53361329	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	73985220.75	0.00
01a106ae-1345-7911-b8a2-b88a993a24c7	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	8742857.14
01a106ae-1345-7911-b8a2-b88a993a24c7	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	8742857.14	0.00
01a106ae-13f6-75bd-b085-f42eb268a806	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	109208730.00
01a106ae-13f6-75bd-b085-f42eb268a806	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	109208730.00	0.00
01a106ae-13f6-75bd-b085-f42eb268a806	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	81391720.59
01a106ae-13f6-75bd-b085-f42eb268a806	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	81391720.59	0.00
01a106ae-1456-70e7-93dc-230ef5292b34	1	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	0.00	10000000.00
01a106ae-1456-70e7-93dc-230ef5292b34	2	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	10000000.00	0.00
01a106ae-15dc-745c-a5ad-b5f5c775dd6c	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	62666000.00
01a106ae-15dc-745c-a5ad-b5f5c775dd6c	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	62666000.00	0.00
01a106ae-1687-7b73-b756-edf8c637ef02	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	58531850.00
01a106ae-1687-7b73-b756-edf8c637ef02	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	58531850.00	0.00
01a106ae-1687-7b73-b756-edf8c637ef02	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	43780340.64
01a106ae-1687-7b73-b756-edf8c637ef02	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	43780340.64	0.00
01a106ae-16d8-74f2-9603-432da6f51ea2	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	59200000.00
01a106ae-16d8-74f2-9603-432da6f51ea2	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	59200000.00	0.00
01a106ae-1720-7030-af18-6eb72a8d08cc	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	215212500.00
01a106ae-1720-7030-af18-6eb72a8d08cc	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	215212500.00	0.00
01a106ae-1766-73a3-a3c7-763cacf696b6	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	3766230.00
01a106ae-1766-73a3-a3c7-763cacf696b6	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	3766230.00	0.00
01a106ae-1a57-782c-802f-2ca41827eb8c	1	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	ATK kantor	0.00	1275000.00
01a106ae-1a57-782c-802f-2ca41827eb8c	2	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	Salah akun	1275000.00	0.00
01a106ae-00a5-7372-89db-e96f100c0254	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-00a5-7372-89db-e96f100c0254	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	235000.00
01a106ae-00a5-7372-89db-e96f100c0254	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1810000.00	0.00
01a106ae-0105-7000-a2b5-7c7ec14a98ab	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-0105-7000-a2b5-7c7ec14a98ab	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	235000.00
01a106ae-0105-7000-a2b5-7c7ec14a98ab	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1810000.00	0.00
01a106ae-014f-7ee2-a553-003f7b8b0f76	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	65149300.00
01a106ae-014f-7ee2-a553-003f7b8b0f76	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	65149300.00	0.00
01a106ae-01a7-7064-8e4b-cfd5d0ec1bca	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	68288300.00
01a106ae-01a7-7064-8e4b-cfd5d0ec1bca	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	68288300.00	0.00
01a106ae-01f9-7dce-a036-139c96a56bea	1	01a0f50c-aafa-72d3-acd7-b789ac25dc81	\N	\N	0.00	6662370.00
01a106ae-01f9-7dce-a036-139c96a56bea	2	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N	\N	6662370.00	0.00
01a106ae-01f9-7dce-a036-139c96a56bea	3	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N	\N	0.00	133247.40
01a106ae-01f9-7dce-a036-139c96a56bea	4	01a0f50c-aafa-7f88-bf00-8dfd3adebe85	\N	\N	133247.40	0.00
01a106ae-03a1-7fa6-9fa1-82e28c4c8796	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	116103235.22
01a106ae-03a1-7fa6-9fa1-82e28c4c8796	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	116103235.22	0.00
01a106ae-0595-772e-9fe6-b077e3c48794	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	59200000.00
01a106ae-0595-772e-9fe6-b077e3c48794	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	59200000.00	0.00
01a106ae-05a6-75a0-bb64-41c283809d3e	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	59200000.00
01a106ae-05a6-75a0-bb64-41c283809d3e	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	59200000.00	0.00
01a106ae-05f2-717c-a2bf-af8a5601441c	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	42124012.13
01a106ae-05f2-717c-a2bf-af8a5601441c	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	42124012.13	0.00
01a106ae-05f2-717c-a2bf-af8a5601441c	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	3393000.00
01a106ae-05f2-717c-a2bf-af8a5601441c	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	3393000.00	0.00
01a106ae-0742-7dcd-b168-1188647a8b77	1	01a0f50c-aafa-7841-94a9-8bb205222c6d	01a106ad-bb0c-73e5-8470-f55cf1f77166	Karung bekas	1450000.00	0.00
01a106ae-0742-7dcd-b168-1188647a8b77	2	01a106ad-ba9c-728f-b38e-3fa3e2849611	\N	\N	0.00	1450000.00
01a106ae-08aa-7373-b8d1-bac9b4ad3359	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	215212500.00
01a106ae-08aa-7373-b8d1-bac9b4ad3359	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	215212500.00	0.00
01a106ae-08ef-75e1-a541-50f16deea0ce	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	3393000.00
01a106ae-08ef-75e1-a541-50f16deea0ce	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	3393000.00	0.00
01a106ae-08ef-75e1-a541-50f16deea0ce	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	373230.00
01a106ae-08ef-75e1-a541-50f16deea0ce	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	373230.00	0.00
01a106ae-09ec-7718-a4e0-6a7fb6da6035	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	191500000.00
01a106ae-09ec-7718-a4e0-6a7fb6da6035	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	191500000.00	0.00
01a106ae-0a51-74db-abba-fa4860348b89	1	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	0.00	2862690.00
01a106ae-0a51-74db-abba-fa4860348b89	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	2862690.00	0.00
01a106ae-0b0d-737b-b417-13d57deb26be	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	92610000.00
01a106ae-0b0d-737b-b417-13d57deb26be	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	92610000.00	0.00
01a106ae-0b44-7d6a-a342-5a0d3a0d1ab2	1	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	1765000.00
01a106ae-0b44-7d6a-a342-5a0d3a0d1ab2	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	1765000.00	0.00
01a106ae-0b7e-777a-8993-c95a6c22ac32	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-0b7e-777a-8993-c95a6c22ac32	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ae-0b7e-777a-8993-c95a6c22ac32	3	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	1970000.00	0.00
01a106ae-0bb7-71ff-9fbd-1205c7025acd	1	01a0f50c-aafa-756e-bd62-b8e331981f15	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	1575000.00
01a106ae-0bb7-71ff-9fbd-1205c7025acd	2	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	395000.00
01a106ae-0bb7-71ff-9fbd-1205c7025acd	3	01a106ad-babd-7fe2-be29-f318a2585da5	\N	\N	1970000.00	0.00
01a106ae-0c65-71ac-bd0c-77c1df781790	1	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	0.00	25900000.00
01a106ae-0c65-71ac-bd0c-77c1df781790	2	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	25900000.00	0.00
01a106ae-0c7a-75d9-b854-19e2cd456642	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	25900000.00
01a106ae-0c7a-75d9-b854-19e2cd456642	2	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N	\N	25900000.00	0.00
01a106ae-0cd1-7dbb-b4ae-a888efe02084	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	18745000.00
01a106ae-0cd1-7dbb-b4ae-a888efe02084	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	18745000.00	0.00
01a106ae-0cd1-7dbb-b4ae-a888efe02084	3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	1765000.00
01a106ae-0cd1-7dbb-b4ae-a888efe02084	4	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	1765000.00	0.00
01a106ae-0ddc-768a-aaf3-243ff1d02fb8	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	56225000.00
01a106ae-0ddc-768a-aaf3-243ff1d02fb8	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	30275000.00
01a106ae-0ddc-768a-aaf3-243ff1d02fb8	3	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	86500000.00	0.00
01a106ae-0e16-7b72-9519-4096b36ff242	1	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bb01-7105-baf6-b9d4d6e43035	\N	0.00	39780000.00
01a106ae-0e16-7b72-9519-4096b36ff242	2	01a0f50c-aafa-7546-9629-1b0bf34ff2ce	01a106ad-bad9-761b-8906-ad19ad45bda1	\N	0.00	21420000.00
01a106ae-0e16-7b72-9519-4096b36ff242	3	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	61200000.00	0.00
01a106ae-0e5e-78c7-95f3-65758e5216fb	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	173120956.76
01a106ae-0e5e-78c7-95f3-65758e5216fb	2	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	173120956.76	0.00
01a106ae-0f14-743b-a82b-c273bf78dc57	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	25900000.00
01a106ae-0f14-743b-a82b-c273bf78dc57	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	25900000.00	0.00
01a106ae-103e-7429-b03c-e3fa46dc0175	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	92610000.00
01a106ae-103e-7429-b03c-e3fa46dc0175	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	92610000.00	0.00
01a106ae-1081-7bff-83f4-6f69a43d747f	1	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N	\N	0.00	1765000.00
01a106ae-1081-7bff-83f4-6f69a43d747f	2	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	1765000.00	0.00
01a106ae-1081-7bff-83f4-6f69a43d747f	3	01a0f50c-aafa-703c-acba-a25e7ca9c550	\N	\N	0.00	194150.00
01a106ae-1081-7bff-83f4-6f69a43d747f	4	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N	\N	194150.00	0.00
01a106ae-121e-7d99-a3a4-2a4deac68e2f	1	01a0f50c-aafa-7969-8d0c-bf5f3dc5e227	\N	\N	0.00	50000000.00
01a106ae-121e-7d99-a3a4-2a4deac68e2f	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	50000000.00	0.00
01a106ae-12f3-7fda-840e-c18e77d9ab8e	1	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	0.00	104443290.00
01a106ae-12f3-7fda-840e-c18e77d9ab8e	2	01a0f50c-aafa-796a-887e-d8e839992c26	\N	\N	104443290.00	0.00
01a106ae-12f3-7fda-840e-c18e77d9ab8e	3	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	0.00	77840105.62
01a106ae-12f3-7fda-840e-c18e77d9ab8e	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	77840105.62	0.00
01a106ae-131d-77c3-a3a4-ae65293f4cac	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Beban penyusutan	0.00	12357142.86
01a106ae-131d-77c3-a3a4-ae65293f4cac	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Akumulasi penyusutan	12357142.86	0.00
01a106ae-1433-76c3-8598-7153bdbf54a9	1	01a0f50c-aafa-7030-8e4c-c2348618a4e5	\N	\N	0.00	10000000.00
01a106ae-1433-76c3-8598-7153bdbf54a9	2	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N	\N	10000000.00	0.00
01a106ae-14b7-7c6f-bbb1-b34d56fe8294	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	29436000.00
01a106ae-14b7-7c6f-bbb1-b34d56fe8294	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	29436000.00	0.00
01a106ae-14fa-7e61-b38a-d35290c8695e	1	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N	\N	0.00	10054010.00
01a106ae-14fa-7e61-b38a-d35290c8695e	2	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	10054010.00	0.00
01a106ae-14fa-7e61-b38a-d35290c8695e	3	01a0f50c-aafa-758f-9816-8e78405403d8	\N	\N	0.00	137000.00
01a106ae-14fa-7e61-b38a-d35290c8695e	4	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	137000.00	0.00
01a106ae-152e-70ae-8f19-0fe67f707c50	1	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N	\N	0.00	157056.96
01a106ae-152e-70ae-8f19-0fe67f707c50	2	01a0f50c-aafa-7e36-91cb-d19960d0fee3	\N	\N	157056.96	0.00
01a106ae-180a-7e1f-901c-24a1eccd7505	1	01a106ad-baca-7b66-8d17-aa3d450ffb52	\N	\N	0.00	65525000.00
01a106ae-180a-7e1f-901c-24a1eccd7505	2	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N	\N	65525000.00	0.00
\.


--
-- Data for Name: journal_mapping_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_mapping_lines (journal_mapping_id, component, debit_account_id, credit_account_id, cost_center_id) FROM stdin;
01a0f50c-ab5e-701b-be59-0d9397f31591	GoodsValue	01a0f50c-aafa-7b24-9853-0576b8e351ac	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N
01a0f50c-ab5e-701b-be59-0d9397f31591	PriceVariance	01a0f50c-aafa-7a40-afee-18f70c6634f7	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N
01a0f50c-ab60-7eb8-abf6-1dcb821585c9	PlasmaIncome	01a0f50c-aafa-72d3-acd7-b789ac25dc81	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	\N
01a0f50c-ab40-7cff-bb35-499fbe471ccb	DocReceived	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N
01a0f50c-ab40-7cff-bb35-499fbe471ccb	FeedReceived	01a0f50c-aafa-72fd-904e-b18026f5acfe	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N
01a0f50c-ab40-7cff-bb35-499fbe471ccb	OvkReceived	01a0f50c-aafa-758f-9816-8e78405403d8	01a0f50c-aafa-7b24-9853-0576b8e351ac	\N
01a0f50c-ab5e-701b-be59-0d9397f31591	IncomeTaxWithheld	01a0f50c-aafa-717b-b756-a81bb2b0f91e	01a0f50c-aafa-7f88-bf00-8dfd3adebe85	\N
01a0f50c-ab5e-701b-be59-0d9397f31591	InputVat	01a0f50c-aafa-703c-acba-a25e7ca9c550	01a0f50c-aafa-717b-b756-a81bb2b0f91e	\N
01a0f50c-ab5f-7d53-9135-d95ba7feb52e	Paid	01a0f50c-aafa-717b-b756-a81bb2b0f91e	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N
01a0f50c-ab60-7083-8317-419027a2ec3a	DocIssued	01a0f50c-aafa-7adb-92b7-886c99b8e875	01a0f50c-aafa-7b67-aa7f-6aabbd66d329	\N
01a0f50c-ab60-7083-8317-419027a2ec3a	FeedIssued	01a0f50c-aafa-7adb-92b7-886c99b8e875	01a0f50c-aafa-72fd-904e-b18026f5acfe	\N
01a0f50c-ab60-7083-8317-419027a2ec3a	OvkIssued	01a0f50c-aafa-7adb-92b7-886c99b8e875	01a0f50c-aafa-758f-9816-8e78405403d8	\N
01a0f50c-ab60-70ce-9fe6-c08ae8ca072d	CostOfGoodsSold	01a0f50c-aafa-7e36-91cb-d19960d0fee3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N
01a0f50c-ab60-70ce-9fe6-c08ae8ca072d	LiveBirdSales	01a0f50c-aafa-7b57-8c6f-842c7cea9323	01a0f50c-aafa-796a-887e-d8e839992c26	\N
01a0f50c-ab60-70ce-9fe6-c08ae8ca072d	OutputVat	01a0f50c-aafa-7b57-8c6f-842c7cea9323	01a0f50c-aafa-7854-93ac-7da131cbf13c	\N
01a0f50c-ab60-7147-b8f7-2a6705deec9e	FeedReturned	01a0f50c-aafa-72fd-904e-b18026f5acfe	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N
01a0f50c-ab60-7147-b8f7-2a6705deec9e	OvkReturned	01a0f50c-aafa-758f-9816-8e78405403d8	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N
01a0f50c-ab60-718b-b150-fcd763bbbb38	OutputVat	01a0f50c-aafa-7854-93ac-7da131cbf13c	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N
01a0f50c-ab60-718b-b150-fcd763bbbb38	SalesReturn	01a0f50c-aafa-7a19-b189-7edcb4dec59d	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N
01a0f50c-ab60-7248-9a8c-13506db0aa73	Applied	01a0f50c-aafa-7969-8d0c-bf5f3dc5e227	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N
01a0f50c-ab60-72c0-8ccf-e593ee1dc8f4	CostOfGoodsSold	01a0f50c-aafa-7e36-91cb-d19960d0fee3	01a0f50c-aafa-7adb-92b7-886c99b8e875	\N
01a0f50c-ab60-7866-85da-8569b95206a6	Paid	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	01a0f50c-aafa-7b82-ab79-855a87a43e6a	\N
01a0f50c-ab60-7b8d-9f9b-4606eb29d0db	Advance	01a0f50c-aafa-7b82-ab79-855a87a43e6a	01a0f50c-aafa-7969-8d0c-bf5f3dc5e227	\N
01a0f50c-ab60-7b8d-9f9b-4606eb29d0db	Received	01a0f50c-aafa-7b82-ab79-855a87a43e6a	01a0f50c-aafa-7b57-8c6f-842c7cea9323	\N
01a0f50c-ab60-7eb8-abf6-1dcb821585c9	Deduction	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	01a0f50c-aafa-7ab4-b53b-3b479632dc20	\N
01a0f50c-ab60-7eb8-abf6-1dcb821585c9	IncomeTaxWithheld	01a0f50c-aafa-7fd7-bacd-51de6995f4d7	01a0f50c-aafa-7f88-bf00-8dfd3adebe85	\N
01a0f50c-ab60-7eb8-abf6-1dcb821585c9	PlasmaDeficit	01a0f50c-aafa-7ab4-b53b-3b479632dc20	01a0f50c-aafa-72d3-acd7-b789ac25dc81	\N
01a106a7-e725-74c5-9257-27a32b207c42	NetIncome	01a0f50c-aafa-7d51-b9b0-42b0a870bee3	01a0f50c-aafa-74f4-8fd8-fc94e13ee79f	\N
\.


--
-- Data for Name: journal_mappings; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_mappings (id, event_type, branch_id, description, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a0f50c-ab40-7cff-bb35-499fbe471ccb	PurchaseReceipt	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab5e-701b-be59-0d9397f31591	VendorInvoice	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab5f-7d53-9135-d95ba7feb52e	VendorPayment	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7083-8317-419027a2ec3a	StockTransferToCycle	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-70ce-9fe6-c08ae8ca072d	SalesInvoice	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7147-b8f7-2a6705deec9e	StockReturnFromCycle	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-718b-b150-fcd763bbbb38	SalesCreditNote	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7248-9a8c-13506db0aa73	CustomerAdvanceApplied	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-72c0-8ccf-e593ee1dc8f4	CycleCostAdjustment	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7866-85da-8569b95206a6	PlasmaPayment	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7b8d-9f9b-4606eb29d0db	CustomerReceipt	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-ab60-7eb8-abf6-1dcb821585c9	PlasmaSettlement	\N	Mapping default (seed)	t	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a106a7-e725-74c5-9257-27a32b207c42	YearEndClosing	\N	Mapping default (seed)	t	2026-10-04 18:23:49.441466+07	\N	\N	\N
\.


--
-- Data for Name: journal_template_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_template_lines (journal_template_id, line_number, account_id, cost_center_id, side, description) FROM stdin;
01a106ad-bb5a-7eaa-a869-a75362a334ea	1	01a0f50c-aafa-7838-9ee0-77eda0e19df1	01a106ad-bad9-761b-8906-ad19ad45bda1	Debit	Beban penyusutan
01a106ad-bb5a-7eaa-a869-a75362a334ea	2	01a0f50c-aafa-799e-939d-838c47f88333	\N	Credit	Akumulasi penyusutan
01a106ad-bb91-7a09-896c-a8f3a174efa3	1	01a0f50c-aafa-72a2-adf9-810612b1dc3c	01a106ad-bb01-7105-baf6-b9d4d6e43035	Debit	\N
01a106ad-bb91-7a09-896c-a8f3a174efa3	2	01a0f50c-aafa-78ba-aeee-c1256d236027	\N	Credit	\N
\.


--
-- Data for Name: journal_templates; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.journal_templates (id, name, description, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bb5a-7eaa-a869-a75362a334ea	Penyusutan aset tetap	Penyusutan bulanan kandang & peralatan inti	t	2026-10-04 18:30:11.444393+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bb91-7a09-896c-a8f3a174efa3	Biaya dibayar dimuka	Pembayaran biaya yang belum ditagih	t	2026-10-04 18:30:11.473516+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: payment_voucher_allocations; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.payment_voucher_allocations (payment_voucher_id, vendor_invoice_id, amount) FROM stdin;
01a106ad-dc76-7acc-9172-4993fcd6d732	01a106ad-cd6f-7c22-ae20-adc16848cca8	37000000.00
01a106ad-dc76-7acc-9172-4993fcd6d732	01a106ad-cf68-792c-a1d9-7d5ad8b45cad	113732500.00
01a106ad-dd6a-7585-a741-9eb19ff1427f	01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	2527470.00
01a106ad-e322-7e5f-ad9e-f18ee24956cf	01a106ad-d61f-7df9-b45c-7f4646da5272	33300000.00
01a106ad-e37a-7d67-9ca9-d476a5ba9b61	01a106ad-d80c-7812-8300-34c0e8e0c918	106332500.00
01a106ad-e3cb-7993-9fb3-1074d78c9ccd	01a106ad-d85e-7c5f-af62-abde8aa20340	2527470.00
01a106ad-e9a0-7549-af11-75b80e8d8502	01a106ad-d9df-7964-ab27-9bf8d2096cd4	37000000.00
01a106ad-e9a0-7549-af11-75b80e8d8502	01a106ad-db66-79e0-9dbe-e4441b97acf9	137940000.00
01a106ad-e9f1-769f-9227-f19e69f1c2ef	01a106ad-dbb0-72f2-be66-2505a4e57fc4	2527470.00
01a106ae-0481-70c6-ab29-7becb50fede2	01a106ad-f492-7328-b925-8c257fbccde7	51800000.00
01a106ae-04c9-72f8-9b03-0e6b93c00653	01a106ad-f67d-7994-8047-8c285fd5bfe9	188360000.00
01a106ae-0512-7698-bcc9-2bffa6c8c0a1	01a106ad-f6c0-7485-ac3f-0303994bd9b1	3431010.00
01a106ae-099b-79ca-a72e-a4ed31416c6c	01a106ad-fc4d-79a2-aa5d-f3a2316a240d	44400000.00
01a106ae-099b-79ca-a72e-a4ed31416c6c	01a106ad-fda0-719c-b3f0-c5c94cd22245	147100000.00
01a106ae-0a09-7aae-b43a-b8080ee12f35	01a106ad-fe07-7901-b2df-291722977f96	2862690.00
01a106ae-16a4-7f5a-a671-9f125c3c9dde	01a106ae-07b7-73d5-accf-3820983cc082	59200000.00
01a106ae-16f1-74b8-90a5-710acc7d3d70	01a106ae-0884-733e-afe1-4bc7b7c64b7a	215212500.00
01a106ae-1738-70ac-ae0b-b009733f4567	01a106ae-08c9-7e22-be84-4e4ff54c1e08	3766230.00
\.


--
-- Data for Name: payment_voucher_settlement_allocations; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.payment_voucher_settlement_allocations (payment_voucher_id, plasma_settlement_id, amount) FROM stdin;
01a106ad-f387-7df5-b3a2-f33f2933d8d4	01a106ad-f14e-7f35-82b0-7f8b0fcf95fa	32560691.10
01a106ae-0352-7c9f-a46a-5f6d213f3964	01a106ae-01d0-7e6d-882c-ea0c9d6b45c3	6529122.60
\.


--
-- Data for Name: payment_vouchers; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.payment_vouchers (id, number, branch_id, vendor_id, cash_bank_account_id, payment_date, reference, notes, status, approved_by, approved_at_utc, paid_by, paid_at_utc, cancellation_reason, amount, created_at_utc, created_by, modified_at_utc, modified_by, farmer_id, payee_type, documents) FROM stdin;
01a106ad-dc76-7acc-9172-4993fcd6d732	PV/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-05	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.008575+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.05295+07	\N	150732500.00	2026-10-04 18:30:19.931506+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.055579+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-dd6a-7585-a741-9eb19ff1427f	PV/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-05	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:20.153465+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.170499+07	\N	2527470.00	2026-10-04 18:30:20.139202+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.170608+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-e322-7e5f-ad9e-f18ee24956cf	PV/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-15	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:21.617571+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.637622+07	\N	33300000.00	2026-10-04 18:30:21.602367+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.637796+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-e37a-7d67-9ca9-d476a5ba9b61	PV/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-15	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:21.703566+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.721159+07	\N	106332500.00	2026-10-04 18:30:21.690623+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.721221+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-e3cb-7993-9fb3-1074d78c9ccd	PV/BDG/2026/VIII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-15	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:21.785012+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.801596+07	\N	2527470.00	2026-10-04 18:30:21.771961+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.801653+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-e9a0-7549-af11-75b80e8d8502	PV/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-08-19	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:23.278383+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.294116+07	\N	174940000.00	2026-10-04 18:30:23.264702+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.294172+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-e9f1-769f-9227-f19e69f1c2ef	PV/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-08-19	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:23.356837+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.3753+07	\N	2527470.00	2026-10-04 18:30:23.345472+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.375425+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ad-f387-7df5-b3a2-f33f2933d8d4	PV/BDG/2026/VIII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	\N	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-08-26	Transfer ke rekening plasma	Hasil siklus KDG-BDG-01	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.819134+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.849159+07	\N	32560691.10	2026-10-04 18:30:25.804485+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.849672+07	01a0f240-f921-75b0-972d-0f74522a6333	01a106ad-bf87-7c98-8271-811bb311416f	Farmer	{}
01a106ae-0352-7c9f-a46a-5f6d213f3964	PV/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	\N	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-09-10	Transfer ke rekening plasma	Hasil siklus KDG-CJR-01	Approved	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.854239+07	\N	\N	\N	6529122.60	2026-10-04 18:30:29.842282+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.854256+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	01a106ad-bfb4-7c14-9f15-02079aeddd09	Farmer	{}
01a106ae-0481-70c6-ab29-7becb50fede2	PV/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-09-12	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:30.155728+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.171044+07	\N	51800000.00	2026-10-04 18:30:30.145482+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.1711+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-04c9-72f8-9b03-0e6b93c00653	PV/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-09-12	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:30.229726+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.245054+07	\N	188360000.00	2026-10-04 18:30:30.217588+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.245109+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-0512-7698-bcc9-2bffa6c8c0a1	PV/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc64-7ac6-827f-f9f76a1f8a7f	2026-09-12	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:30.302011+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.316088+07	\N	3431010.00	2026-10-04 18:30:30.29087+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.316135+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-099b-79ca-a72e-a4ed31416c6c	PV/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-09-19	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.463598+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.496434+07	\N	191500000.00	2026-10-04 18:30:31.451304+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.496523+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-0a09-7aae-b43a-b8080ee12f35	PV/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-09-19	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.572155+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.58692+07	\N	2862690.00	2026-10-04 18:30:31.561533+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.586992+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-16a4-7f5a-a671-9f125c3c9dde	PV/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-10-03	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:34.803265+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.820431+07	\N	59200000.00	2026-10-04 18:30:34.78891+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.820512+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-16f1-74b8-90a5-710acc7d3d70	PV/BDG/2026/X/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-10-03	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:34.878443+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.893106+07	\N	215212500.00	2026-10-04 18:30:34.865979+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.893179+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
01a106ae-1738-70ac-ae0b-b009733f4567	PV/BDG/2026/X/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bc39-79a9-9d61-e784da7d1598	2026-10-03	Transfer BCA/BRI	Pembayaran sapronak	Paid	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:34.947793+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.962359+07	\N	3766230.00	2026-10-04 18:30:34.93634+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.962422+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	Vendor	{}
\.


--
-- Data for Name: vendor_invoice_lines; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.vendor_invoice_lines (vendor_invoice_id, line_number, goods_receipt_id, goods_receipt_line_number, purchase_order_id, purchase_order_line_number, item_id, uom_id, quantity, tax_code_id, vat_rate_percent, price_deviation_percent, amount, goods_value, order_unit_price, unit_price, vat_amount, vat_tax_base) FROM stdin;
01a106ad-cd6f-7c22-ae20-adc16848cca8	1	01a106ad-caea-7afd-8a30-a75d8b846fd2	1	01a106ad-c7b6-76e6-8710-e2ea3f456d89	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	37000000.00	37000000.00	7400.00	7400.00	0.00	0.00
01a106ad-cf68-792c-a1d9-7d5ad8b45cad	1	01a106ad-c921-7abc-9538-f9aefa07df64	1	01a106ad-c831-7346-9239-cc17246be1df	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	61.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	24857500.00	24857500.00	407500.00	407500.00	0.00	0.00
01a106ad-cf68-792c-a1d9-7d5ad8b45cad	2	01a106ad-c921-7abc-9538-f9aefa07df64	2	01a106ad-c831-7346-9239-cc17246be1df	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	225.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	88875000.00	88875000.00	395000.00	395000.00	0.00	0.00
01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	1	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	1	01a106ad-c85a-70d9-a08a-193ddff2454c	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1045000.00	1045000.00	95000.00	95000.00	114950.00	1045000.00
01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	2	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	2	01a106ad-c85a-70d9-a08a-193ddff2454c	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	560000.00	560000.00	112000.00	112000.00	61600.00	560000.00
01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	3	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	3	01a106ad-c85a-70d9-a08a-193ddff2454c	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	672000.00	672000.00	42000.00	42000.00	73920.00	672000.00
01a106ad-d61f-7df9-b45c-7f4646da5272	1	01a106ad-d257-72fc-9fab-d8d93779a1f7	1	01a106ad-d074-7321-9aa3-c7bf0d824568	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	4500.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	33300000.00	33300000.00	7400.00	7400.00	0.00	0.00
01a106ad-d80c-7812-8300-34c0e8e0c918	1	01a106ad-d113-7702-95fa-9936fda3cb97	1	01a106ad-d099-7926-9854-587dcf0ccdec	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	60.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	24300000.00	24300000.00	405000.00	405000.00	0.00	0.00
01a106ad-d80c-7812-8300-34c0e8e0c918	2	01a106ad-d113-7702-95fa-9936fda3cb97	2	01a106ad-d099-7926-9854-587dcf0ccdec	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	209.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	82032500.00	82032500.00	392500.00	392500.00	0.00	0.00
01a106ad-d85e-7c5f-af62-abde8aa20340	1	01a106ad-d150-7d46-aad6-09ab900856a2	1	01a106ad-d0bb-7a33-9e48-09d160670a90	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1045000.00	1045000.00	95000.00	95000.00	114950.00	1045000.00
01a106ad-d85e-7c5f-af62-abde8aa20340	2	01a106ad-d150-7d46-aad6-09ab900856a2	2	01a106ad-d0bb-7a33-9e48-09d160670a90	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	560000.00	560000.00	112000.00	112000.00	61600.00	560000.00
01a106ad-d85e-7c5f-af62-abde8aa20340	3	01a106ad-d150-7d46-aad6-09ab900856a2	3	01a106ad-d0bb-7a33-9e48-09d160670a90	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	672000.00	672000.00	42000.00	42000.00	73920.00	672000.00
01a106ad-d9df-7964-ab27-9bf8d2096cd4	1	01a106ad-d6d0-7981-97bd-a98ead54697d	1	01a106ad-d349-7a6c-b9fb-cad4ed3bbd16	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	37000000.00	37000000.00	7400.00	7400.00	0.00	0.00
01a106ad-db66-79e0-9dbe-e4441b97acf9	1	01a106ad-d565-74c5-97c1-753986941b7f	1	01a106ad-d36b-7ff1-8a59-90fecddb2138	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	70.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	28525000.00	28525000.00	407500.00	407500.00	0.00	0.00
01a106ad-db66-79e0-9dbe-e4441b97acf9	2	01a106ad-d565-74c5-97c1-753986941b7f	2	01a106ad-d36b-7ff1-8a59-90fecddb2138	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	277.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	109415000.00	109415000.00	395000.00	395000.00	0.00	0.00
01a106ad-dbb0-72f2-be66-2505a4e57fc4	1	01a106ad-d5a9-715f-82a1-c024dce8f91c	1	01a106ad-d38e-7e14-a5ea-e2b6d7da859c	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1045000.00	1045000.00	95000.00	95000.00	114950.00	1045000.00
01a106ad-dbb0-72f2-be66-2505a4e57fc4	2	01a106ad-d5a9-715f-82a1-c024dce8f91c	2	01a106ad-d38e-7e14-a5ea-e2b6d7da859c	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	560000.00	560000.00	112000.00	112000.00	61600.00	560000.00
01a106ad-dbb0-72f2-be66-2505a4e57fc4	3	01a106ad-d5a9-715f-82a1-c024dce8f91c	3	01a106ad-d38e-7e14-a5ea-e2b6d7da859c	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	672000.00	672000.00	42000.00	42000.00	73920.00	672000.00
01a106ad-f492-7328-b925-8c257fbccde7	1	01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	1	01a106ad-ea88-75de-a783-288fe4369f7a	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	7000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	51800000.00	51800000.00	7400.00	7400.00	0.00	0.00
01a106ad-f67d-7994-8047-8c285fd5bfe9	1	01a106ad-ecb2-7cf5-bc99-7307f161b5bc	1	01a106ad-eab1-7ef8-90a5-b905cf6caae4	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	91.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	36855000.00	36855000.00	405000.00	405000.00	0.00	0.00
01a106ad-f67d-7994-8047-8c285fd5bfe9	2	01a106ad-ecb2-7cf5-bc99-7307f161b5bc	2	01a106ad-eab1-7ef8-90a5-b905cf6caae4	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	386.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	151505000.00	151505000.00	392500.00	392500.00	0.00	0.00
01a106ad-f6c0-7485-ac3f-0303994bd9b1	1	01a106ad-ece9-7ff1-aad4-f1d48c18557f	1	01a106ad-ead4-7c8d-8b03-cd56500d7e26	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	15.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1425000.00	1425000.00	95000.00	95000.00	156750.00	1425000.00
01a106ad-f6c0-7485-ac3f-0303994bd9b1	2	01a106ad-ece9-7ff1-aad4-f1d48c18557f	2	01a106ad-ead4-7c8d-8b03-cd56500d7e26	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	784000.00	784000.00	112000.00	112000.00	86240.00	784000.00
01a106ad-f6c0-7485-ac3f-0303994bd9b1	3	01a106ad-ece9-7ff1-aad4-f1d48c18557f	3	01a106ad-ead4-7c8d-8b03-cd56500d7e26	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	882000.00	882000.00	42000.00	42000.00	97020.00	882000.00
01a106ad-fc4d-79a2-aa5d-f3a2316a240d	1	01a106ad-f7e8-755b-9182-950be853462c	1	01a106ad-f422-79ea-93d5-1409a717f0aa	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	6000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	44400000.00	44400000.00	7400.00	7400.00	0.00	0.00
01a106ad-fda0-719c-b3f0-c5c94cd22245	1	01a106ad-f5e8-7414-89fa-95e6aee15d5c	1	01a106ad-f43c-7c60-8ee7-d72ea304cd19	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	76.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	30970000.00	30970000.00	407500.00	407500.00	0.00	0.00
01a106ad-fda0-719c-b3f0-c5c94cd22245	2	01a106ad-f5e8-7414-89fa-95e6aee15d5c	2	01a106ad-f43c-7c60-8ee7-d72ea304cd19	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	294.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	116130000.00	116130000.00	395000.00	395000.00	0.00	0.00
01a106ad-fe07-7901-b2df-291722977f96	1	01a106ad-f623-7d55-9b20-a1b5d900f045	1	01a106ad-f458-7c58-8cbf-94cd8e8f6530	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	13.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1235000.00	1235000.00	95000.00	95000.00	135850.00	1235000.00
01a106ad-fe07-7901-b2df-291722977f96	2	01a106ad-f623-7d55-9b20-a1b5d900f045	2	01a106ad-f458-7c58-8cbf-94cd8e8f6530	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	672000.00	672000.00	112000.00	112000.00	73920.00	672000.00
01a106ad-fe07-7901-b2df-291722977f96	3	01a106ad-f623-7d55-9b20-a1b5d900f045	3	01a106ad-f458-7c58-8cbf-94cd8e8f6530	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	672000.00	672000.00	42000.00	42000.00	73920.00	672000.00
01a106ae-07b7-73d5-accf-3820983cc082	1	01a106ae-057e-79b5-8334-150eb98cd9a0	1	01a106ae-0254-741a-8076-a451dd9c06ea	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	8000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	59200000.00	59200000.00	7400.00	7400.00	0.00	0.00
01a106ae-0884-733e-afe1-4bc7b7c64b7a	1	01a106ae-03dc-71bc-8c80-5622ee97290b	1	01a106ae-0270-7a0a-8abb-ff9657ebf0f5	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	104.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	42120000.00	42120000.00	405000.00	405000.00	0.00	0.00
01a106ae-0884-733e-afe1-4bc7b7c64b7a	2	01a106ae-03dc-71bc-8c80-5622ee97290b	2	01a106ae-0270-7a0a-8abb-ff9657ebf0f5	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	441.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	173092500.00	173092500.00	392500.00	392500.00	0.00	0.00
01a106ae-08c9-7e22-be84-4e4ff54c1e08	1	01a106ae-041e-7b65-83ea-b89df6274edb	1	01a106ae-028f-7000-addf-42d5fb63efd9	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	17.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	1615000.00	1615000.00	95000.00	95000.00	177650.00	1615000.00
01a106ae-08c9-7e22-be84-4e4ff54c1e08	2	01a106ae-041e-7b65-83ea-b89df6274edb	2	01a106ae-028f-7000-addf-42d5fb63efd9	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	896000.00	896000.00	112000.00	112000.00	98560.00	896000.00
01a106ae-08c9-7e22-be84-4e4ff54c1e08	3	01a106ae-041e-7b65-83ea-b89df6274edb	3	01a106ae-028f-7000-addf-42d5fb63efd9	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	882000.00	882000.00	42000.00	42000.00	97020.00	882000.00
01a106ae-0ef2-7183-a407-2e028076c45e	1	01a106ae-0c4e-7273-b6e0-82abb0d929f9	1	01a106ae-0947-7c24-b029-dd550944e800	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	3500.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	25900000.00	25900000.00	7400.00	7400.00	0.00	0.00
01a106ae-1018-720e-97c2-1501dfda537f	1	01a106ae-0af1-7541-a16c-02d04ad0ab29	1	01a106ae-0962-7ac7-b614-41ab07d1f304	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	46.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	18745000.00	18745000.00	407500.00	407500.00	0.00	0.00
01a106ae-1018-720e-97c2-1501dfda537f	2	01a106ae-0af1-7541-a16c-02d04ad0ab29	2	01a106ae-0962-7ac7-b614-41ab07d1f304	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	187.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	0.0000	73865000.00	73865000.00	395000.00	395000.00	0.00	0.00
01a106ae-105c-738b-9922-d9f3438c009a	1	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	1	01a106ae-097f-777c-9170-865377bfb9c2	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	9.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	855000.00	855000.00	95000.00	95000.00	94050.00	855000.00
01a106ae-105c-738b-9922-d9f3438c009a	2	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	2	01a106ae-097f-777c-9170-865377bfb9c2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	448000.00	448000.00	112000.00	112000.00	49280.00	448000.00
01a106ae-105c-738b-9922-d9f3438c009a	3	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	3	01a106ae-097f-777c-9170-865377bfb9c2	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	0.0000	462000.00	462000.00	42000.00	42000.00	50820.00	462000.00
\.


--
-- Data for Name: vendor_invoices; Type: TABLE DATA; Schema: finance; Owner: postgres
--

COPY finance.vendor_invoices (id, number, branch_id, vendor_id, vendor_invoice_number, tax_invoice_number, invoice_date, due_date, status, notes, income_tax_code_id, income_tax_rate_percent, max_price_deviation_percent, price_variance_approval_reason, posted_by, posted_at_utc, cancellation_reason, goods_value, income_tax_amount, paid_amount, subtotal, total, vat_amount, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-cd6f-7c22-ae20-adc16848cca8	VI/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2607/0001	\N	2026-07-19	2026-08-18	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.211766+07	\N	37000000.00	0.00	37000000.00	37000000.00	37000000.00	0.00	2026-10-04 18:30:16.100478+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.055579+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-cf68-792c-a1d9-7d5ad8b45cad	VI/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2607/0002	\N	2026-07-21	2026-08-20	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.576296+07	\N	113732500.00	0.00	113732500.00	113732500.00	113732500.00	0.00	2026-10-04 18:30:16.552353+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.055579+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-cfb6-7c64-a77d-6297bcaf3bf5	VI/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2607/0003	040022600000003	2026-07-21	2026-08-04	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.661896+07	\N	2277000.00	0.00	2527470.00	2277000.00	2527470.00	250470.00	2026-10-04 18:30:16.631089+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.170608+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d61f-7df9-b45c-7f4646da5272	VI/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2607/0004	\N	2026-07-29	2026-08-28	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.310202+07	\N	33300000.00	0.00	33300000.00	33300000.00	33300000.00	0.00	2026-10-04 18:30:18.272372+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.637796+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d80c-7812-8300-34c0e8e0c918	VI/BDG/2026/VII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	JPF/INV/2607/0005	\N	2026-07-31	2026-08-30	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.787977+07	\N	106332500.00	0.00	106332500.00	106332500.00	106332500.00	0.00	2026-10-04 18:30:18.765295+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.721221+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d85e-7c5f-af62-abde8aa20340	VI/BDG/2026/VII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2607/0006	040022600000006	2026-07-31	2026-08-14	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.870644+07	\N	2277000.00	0.00	2527470.00	2277000.00	2527470.00	250470.00	2026-10-04 18:30:18.847042+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.801653+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d9df-7964-ab27-9bf8d2096cd4	VI/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2608/0007	\N	2026-08-02	2026-09-01	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.254899+07	\N	37000000.00	0.00	37000000.00	37000000.00	37000000.00	0.00	2026-10-04 18:30:19.231578+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.294172+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-db66-79e0-9dbe-e4441b97acf9	VI/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2608/0008	\N	2026-08-04	2026-09-03	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.645678+07	\N	137940000.00	0.00	137940000.00	137940000.00	137940000.00	0.00	2026-10-04 18:30:19.622624+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.294172+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-dbb0-72f2-be66-2505a4e57fc4	VI/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2608/0009	040022600000009	2026-08-04	2026-08-18	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:19.719801+07	\N	2277000.00	0.00	2527470.00	2277000.00	2527470.00	250470.00	2026-10-04 18:30:19.696469+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.375425+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f492-7328-b925-8c257fbccde7	VI/CJR/2026/VIII/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2608/0010	\N	2026-08-26	2026-09-25	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.084394+07	\N	51800000.00	0.00	51800000.00	51800000.00	51800000.00	0.00	2026-10-04 18:30:26.066401+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.1711+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f67d-7994-8047-8c285fd5bfe9	VI/CJR/2026/VIII/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be80-70ef-bc7b-798de012dccf	JPF/INV/2608/0011	\N	2026-08-28	2026-09-27	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.577728+07	\N	188360000.00	0.00	188360000.00	188360000.00	188360000.00	0.00	2026-10-04 18:30:26.557404+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.245109+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f6c0-7485-ac3f-0303994bd9b1	VI/CJR/2026/VIII/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2608/0012	040022600000012	2026-08-28	2026-09-11	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.645248+07	\N	3091000.00	0.00	3431010.00	3091000.00	3431010.00	340010.00	2026-10-04 18:30:26.624754+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.316135+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fc4d-79a2-aa5d-f3a2316a240d	VI/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2609/0013	\N	2026-09-02	2026-10-02	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.064335+07	\N	44400000.00	0.00	44400000.00	44400000.00	44400000.00	0.00	2026-10-04 18:30:28.045883+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.496523+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fda0-719c-b3f0-c5c94cd22245	VI/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2609/0014	\N	2026-09-04	2026-10-04	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.421272+07	\N	147100000.00	0.00	147100000.00	147100000.00	147100000.00	0.00	2026-10-04 18:30:28.385057+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.496523+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fe07-7901-b2df-291722977f96	VI/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2609/0015	040022600000015	2026-09-04	2026-09-18	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.511041+07	\N	2579000.00	0.00	2862690.00	2579000.00	2862690.00	283690.00	2026-10-04 18:30:28.487891+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.586992+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0ef2-7183-a407-2e028076c45e	VI/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2609/0019	\N	2026-09-25	2026-10-25	Posted	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.836408+07	\N	25900000.00	0.00	0.00	25900000.00	25900000.00	0.00	2026-10-04 18:30:32.819062+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.836442+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1018-720e-97c2-1501dfda537f	VI/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2609/0020	\N	2026-09-27	2026-10-27	Posted	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.132805+07	\N	92610000.00	0.00	0.00	92610000.00	92610000.00	0.00	2026-10-04 18:30:33.112779+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.132824+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-105c-738b-9922-d9f3438c009a	VI/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2609/0021	040022600000021	2026-09-27	2026-10-11	Posted	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.199829+07	\N	1765000.00	0.00	0.00	1765000.00	1959150.00	194150.00	2026-10-04 18:30:33.181175+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.199847+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-07b7-73d5-accf-3820983cc082	VI/BDG/2026/IX/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	CPI/INV/2609/0016	\N	2026-09-16	2026-10-16	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.986881+07	\N	59200000.00	0.00	59200000.00	59200000.00	59200000.00	0.00	2026-10-04 18:30:30.968097+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.820512+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0884-733e-afe1-4bc7b7c64b7a	VI/BDG/2026/IX/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	JPF/INV/2609/0017	\N	2026-09-18	2026-10-18	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.192973+07	\N	215212500.00	0.00	215212500.00	215212500.00	215212500.00	0.00	2026-10-04 18:30:31.172658+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.893179+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-08c9-7e22-be84-4e4ff54c1e08	VI/BDG/2026/IX/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	MDN/INV/2609/0018	040022600000018	2026-09-18	2026-10-02	Paid	\N	\N	0.0000	0.0000	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.261301+07	\N	3393000.00	0.00	3766230.00	3393000.00	3766230.00	373230.00	2026-10-04 18:30:31.241263+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.962422+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
\.


--
-- Data for Name: branch_access_profile_branches; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.branch_access_profile_branches (profile_id, branch_id) FROM stdin;
01a106ad-b803-7abc-a19a-20a53d1f9bf4	01a106ad-b536-7f76-bf02-1115db4d6aff
\.


--
-- Data for Name: branch_access_profiles; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.branch_access_profiles (id, name, description, all_branches, is_system, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
0199a3c0-0000-7000-8000-000000000002	All Branches	Every branch, including branches created later (system profile).	t	t	2026-10-03 06:15:56.165171+07	\N	\N	\N
01a106ad-b803-7abc-a19a-20a53d1f9bf4	Cabang Bandung	Hanya data Cabang Bandung (contoh akses cabang)	f	f	2026-10-04 18:30:10.582233+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: menu_access_profile_items; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.menu_access_profile_items (profile_id, menu_id, can_view, can_create, can_edit, can_delete, can_export) FROM stdin;
\.


--
-- Data for Name: menu_access_profiles; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.menu_access_profiles (id, name, description, is_system, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
0199a3c0-0000-7000-8000-000000000001	Full Access	Every right on every menu (system profile).	t	2026-10-03 06:15:56.165171+07	\N	\N	\N
\.


--
-- Data for Name: menus; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.menus (id, code, parent_code, name, default_name, icon, route, sort_order, is_active, is_available, in_catalog, supports_create, supports_edit, supports_delete, supports_export, is_customized, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a10068-f8de-73c9-a55a-daec3722764c	master	\N	Master Data	Master Data	ti ti-database	\N	20	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f933-749b-8f5d-120749a96efe	inventory	\N	Inventory	Inventory	ti ti-building-warehouse	\N	50	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f933-7bc8-980a-d165025851b1	partnership	\N	Partnership	Partnership	ti ti-users-group	\N	30	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f933-7ebf-b005-e8f1f775786a	procurement	\N	Procurement	Procurement	ti ti-shopping-cart	\N	40	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7466-a79c-b4dbd37f9d9d	costing	\N	Costing	Costing	ti ti-calculator	\N	90	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7496-a8e7-465190359cca	admin.menu-access	admin	Menu Access	Menu Access	\N	/Admin/MenuAccess	20	t	t	t	t	t	t	t	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7569-a237-f3881b4042ce	admin	\N	Administration	Administration	ti ti-settings	\N	110	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-758e-a399-21904626e859	reports	\N	Reports	Reports	ti ti-report-analytics	\N	100	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7649-8424-47da61aa6762	admin.api-roles	admin	API Roles	API Roles	\N	/Admin/ApiRoles	50	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7848-8423-8bbd95e658a2	admin.users	admin	Users	Users	\N	/Admin/Users	10	t	t	t	t	t	t	t	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7971-b089-0a91d84f3785	admin.branches	admin	Branches	Branches	\N	/Admin/Branches	60	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7a61-af69-d73449b13f88	finance	\N	Finance	Finance	ti ti-building-bank	\N	80	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7a7d-9164-83223780b8bc	admin.menus	admin	Menus	Menus	\N	/Admin/Menus	40	t	t	t	f	t	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7a7e-964c-02340cac21c9	production	\N	Production	Production	ti ti-egg	\N	60	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f934-7ab4-b28a-f3778f281fd7	sales	\N	Sales	Sales	ti ti-receipt	\N	70	t	t	t	f	f	f	f	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f933-7689-8be6-7c64eb4fa3ae	inventory.stock-transfers	inventory	Stock Transfers	Stock Transfers	\N	/Inventory/StockTransfers	20	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f933-7976-80f2-27894d2bd9f6	procurement.purchase-orders	procurement	Purchase Orders	Purchase Orders	\N	/Procurement/PurchaseOrders	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f933-7b9f-bedc-66dc905e610b	inventory.stock-returns	inventory	Stock Returns	Stock Returns	\N	/Inventory/StockReturns	30	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f933-7d63-b268-c8c70b53f461	inventory.goods-receipts	inventory	Goods Receipts	Goods Receipts	\N	/Inventory/GoodsReceipts	10	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-740a-9b6e-338100fc5974	finance.accounts	finance	Chart of Accounts	Chart of Accounts	\N	/Finance/Accounts	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-74b3-9b29-40d5b8f4aa9c	finance.journal-templates	finance	Journal Templates	Journal Templates	\N	/Finance/JournalTemplates	40	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-75b9-ac63-7095847c0eb1	finance.cash-bank-accounts	finance	Cash/Bank Accounts	Cash/Bank Accounts	\N	/Finance/CashBankAccounts	60	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-705e-ba5e-ff345a9fe2da	sales.receivables	sales	Receivable Ledger & Aging	Receivable Ledger & Aging	\N	/Sales/Receivables	60	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-72bc-8edd-77d07a56131b	reports.income-statement	reports	Income Statement	Income Statement	\N	/Reports/IncomeStatement	30	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7386-9c20-ea4a0722636b	sales.orders	sales	Sales Orders	Sales Orders	\N	/Sales/SalesOrders	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-73b2-822a-8ca1be9d5429	reports.general-ledger	reports	General Ledger	General Ledger	\N	/Reports/GeneralLedger	10	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-74a6-b85e-b78db32bfe5b	costing.cycle-costs	costing	Cycle Cost	Cycle Cost	\N	/Costing/CycleCosts	10	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7672-8594-7dc6aab36417	finance.cash-transactions	finance	Cash In/Out	Cash In/Out	\N	/Finance/CashTransactions	90	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7672-9e98-ac986e030054	sales.invoices	sales	Sales Invoices	Sales Invoices	\N	/Sales/SalesInvoices	30	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-783f-93df-4f08c8fb0919	finance.journals	finance	Journals	Journals	\N	/Finance/Journals	130	t	t	t	t	t	t	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7955-b1c9-d9c7b9bd6032	finance.payables	finance	Payable Ledger & Aging	Payable Ledger & Aging	\N	/Finance/Payables	120	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-79d3-8fe1-b0d63653501e	admin.failed-events	admin	Failed Events	Failed Events	\N	/Admin/FailedEvents	70	t	t	t	f	t	f	f	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-79f9-b77a-f1fed506413d	sales.credit-notes	sales	Credit Notes	Credit Notes	\N	/Sales/CreditNotes	40	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7a57-ad08-a0f8532ff00c	reports.balance-sheet	reports	Balance Sheet	Balance Sheet	\N	/Reports/BalanceSheet	40	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7ab2-b694-97f58dd9f948	reports.profitability	reports	Profitability	Profitability	\N	/Reports/Profitability	60	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7e1b-9e50-7cc001eafbd6	admin.branch-access	admin	Branch Access	Branch Access	\N	/Admin/BranchAccess	30	t	t	t	t	t	t	t	f	2026-10-03 13:17:21.979+07	\N	\N	\N
01a10068-f932-7307-92e7-97fc1fa3f602	master.uoms	master	Units of Measure	Units of Measure	\N	/MasterData/Uoms	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7205-975b-0d2bfde54b1d	master.vendors	master	Vendors	Vendors	\N	/MasterData/Vendors	50	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7221-8065-58e800eb6890	partnership.farmers	partnership	Farmers	Farmers	\N	/Partnership/Farmers	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-743a-b822-a5280e7f9cd4	master.tax-codes	master	Tax Codes	Tax Codes	\N	/MasterData/TaxCodes	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7821-916d-539aadc853d3	partnership.contracts	partnership	Contracts	Contracts	\N	/Partnership/Contracts	30	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-78d8-acbd-14b42f747708	master.customers	master	Customers	Customers	\N	/MasterData/Customers	60	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7962-b99a-31c857828dea	partnership.coops	partnership	Coops	Coops	\N	/Partnership/Coops	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7a68-b616-94be9949b0ec	master.items	master	Items	Items	\N	/MasterData/Items	30	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f933-7c8c-8004-a69d7c651630	master.warehouses	master	Warehouses	Warehouses	\N	/MasterData/Warehouses	40	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 14:11:49.837661+07	\N
01a10068-f934-7683-a555-1a94d51183ba	finance.fiscal-periods	finance	Fiscal Periods	Fiscal Periods	\N	/Finance/FiscalPeriods	30	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-76cd-b1bf-8d262f25945d	production.harvests	production	Harvests	Harvests	\N	/Production/Harvests	30	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7875-a04a-bce3f2b661ef	inventory.feed-mutations	inventory	Feed Mutations	Feed Mutations	\N	/Inventory/FeedMutations	40	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-788e-a3fd-744bcdcdb2b7	finance.journal-mappings	finance	Auto Journal Mappings	Auto Journal Mappings	\N	/Finance/JournalMappings	50	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7b11-9a86-d3d459d8dd35	inventory.stock	inventory	Stock Balance & Card	Stock Balance & Card	\N	/Inventory/Stock	50	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7b44-8aac-629db8a59930	production.recordings	production	Daily Recordings	Daily Recordings	\N	/Production/Recordings	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7d62-8d74-24e84df9302e	finance.cost-centers	finance	Cost Centers	Cost Centers	\N	/Finance/CostCenters	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7ea8-8eb0-5258e4cdab78	production.cycles	production	Cycles & Chick-in	Cycles & Chick-in	\N	/Production/Cycles	10	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-03 19:20:32.913107+07	\N
01a10068-f934-7ab3-8d30-10d98a0df0bc	sales.deliveries	sales	Delivery Orders	Delivery Orders	\N	/Sales/DeliveryOrders	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7b75-826f-8b6b54606da4	finance.vendor-invoices	finance	Vendor Invoices	Vendor Invoices	\N	/Finance/VendorInvoices	70	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7c37-a404-dd5202b7bfc4	finance.payment-vouchers	finance	Payment Vouchers	Payment Vouchers	\N	/Finance/PaymentVouchers	80	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7d05-a117-7dc9c59b11f1	reports.cash-flow	reports	Cash Flow	Cash Flow	\N	/Reports/CashFlow	50	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7d15-9fe5-0e3a12cac2e1	costing.settlements	costing	Plasma Settlements	Plasma Settlements	\N	/Costing/Settlements	20	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7d40-ac74-cf15181e74f3	sales.receipts	sales	Customer Receipts	Customer Receipts	\N	/Sales/Receipts	50	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7d5c-9f08-96d43fa21669	reports.trial-balance	reports	Trial Balance	Trial Balance	\N	/Reports/TrialBalance	20	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7f23-a19a-50bed03dd64e	finance.bank-transfers	finance	Bank Transfers	Bank Transfers	\N	/Finance/BankTransfers	100	t	t	t	t	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7fa3-8b14-32cbc4e19fb3	reports.tax	reports	Tax Recap	Tax Recap	\N	/Reports/Tax	70	t	t	t	f	f	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a10068-f934-7ff3-b7eb-5567c9416480	finance.bank-reconciliations	finance	Bank Reconciliations	Bank Reconciliations	\N	/Finance/BankReconciliations	110	t	t	t	t	t	f	t	f	2026-10-03 13:17:21.979+07	\N	2026-10-04 03:18:13.02848+07	\N
01a1036a-cb9e-7d2e-b3f4-62780f4f9921	admin.audit-logs	admin	Audit Log	Audit Log	\N	/Admin/AuditLogs	80	t	t	t	f	f	f	t	f	2026-10-04 03:18:13.02848+07	\N	\N	\N
\.


--
-- Data for Name: refresh_tokens; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.refresh_tokens (id, token, user_id, expires_on_utc) FROM stdin;
\.


--
-- Data for Name: role_permissions; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.role_permissions (role_id, permission) FROM stdin;
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	roles:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	roles:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	users:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	users:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	branches:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	branches:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cash-bank:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cash-bank:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cash-bank:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cash-bank:reconcile
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	contracts:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	contracts:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	costing:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cycles:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	cycles:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	farmers:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	farmers:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	finance-reports:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	finance-setup:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	finance-setup:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	fiscal-periods:close
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	inventory:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	inventory:receive
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	inventory:return
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	inventory:transfer
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	journals:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	journals:create
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	journals:post
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	journals:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	master-data:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	master-data:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	payables:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	payables:approve-variance
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	payables:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	payables:pay
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	payables:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	production:close
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	production:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	production:record
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	production:revise
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	purchasing:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	purchasing:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	purchasing:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	receivables:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	receivables:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	receivables:void
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:credit-override
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:deliver
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:invoice
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	sales:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	settlements:approve
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	settlements:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	warehouses:manage
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	warehouses:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	attachments:delete
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	attachments:read
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	attachments:upload
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	system:outbox
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	tax-reports:read
\.


--
-- Data for Name: roles; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.roles (id, name, description, is_system, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a0f240-f7a6-7484-868c-9da0bc7ed0c8	Administrator	Full access to every module.	t	2026-09-30 19:18:59.516345+07	\N	\N	\N
\.


--
-- Data for Name: user_old; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.user_old (id, user_id, email, first_name, last_name, password_hash, is_active, menu_access_profile_id, branch_access_profile_id, default_branch_id, role_ids, role_names, created_at_utc, created_by, modified_at_utc, modified_by, deleted_at_utc, deleted_by, reason) FROM stdin;
\.


--
-- Data for Name: user_roles; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.user_roles (user_id, role_id) FROM stdin;
01a0f240-f921-75b0-972d-0f74522a6333	01a0f240-f7a6-7484-868c-9da0bc7ed0c8
01a106ad-b782-7dd8-9680-2dd57b9573c4	01a0f240-f7a6-7484-868c-9da0bc7ed0c8
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: identity; Owner: postgres
--

COPY identity.users (id, email, first_name, last_name, password_hash, created_at_utc, created_by, modified_at_utc, modified_by, is_active, security_stamp, branch_access_profile_id, default_branch_id, menu_access_profile_id, access_failed_count, lockout_end_utc) FROM stdin;
01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	System	Administrator	E63DFE63299FCC7CC60948AC384176D457B8D9C8D8DDDB793D1F11A2A564B2FF-5804283739C279721E0D7C17B8B746D1	2026-09-30 19:18:59.516345+07	\N	\N	\N	t	7462e2882eb44e6e98da1b5fd5f757fe	0199a3c0-0000-7000-8000-000000000002	\N	0199a3c0-0000-7000-8000-000000000001	0	\N
01a106ad-b782-7dd8-9680-2dd57b9573c4	checker@intiplasma.local	Siti	Rahmawati	0FCCBFBF546901C241E06A3FD5E9B730A7456DABFB64EB895DE4B2F5D01F3AD7-2DD0E28BEB733080E2F536F81E72D206	2026-10-04 18:30:10.4437+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	t	37fc25eecd2d4390af2bb735b3db691d	0199a3c0-0000-7000-8000-000000000002	01a106ad-b536-7f76-bf02-1115db4d6aff	0199a3c0-0000-7000-8000-000000000001	0	\N
01a106ad-b93f-7644-8f8a-4d7bfe6cf1e3	staff.bdg@intiplasma.local	Budi	Santoso	72C60C8A4ECAE9F78E50407B648C5C7EBADBA46D8328662F5F4BBB413F17BBCF-1FB8559C6948043A657C2D8DB17C12D1	2026-10-04 18:30:10.880347+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	t	94dda4bd448645bcb372c7efc613f762	01a106ad-b803-7abc-a19a-20a53d1f9bf4	01a106ad-b536-7f76-bf02-1115db4d6aff	0199a3c0-0000-7000-8000-000000000001	0	\N
\.


--
-- Data for Name: audit_logs; Type: TABLE DATA; Schema: infrastructure; Owner: postgres
--

COPY infrastructure.audit_logs (id, occurred_at_utc, category, action, user_id, user_email, entity_type, entity_id, summary, details, ip_address, source) FROM stdin;
01a1036b-8aa9-749a-a42a-f8866639c954	2026-10-04 03:19:01.92945+07	SignIn	SignedIn	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	User	01a0f240-f921-75b0-972d-0f74522a6333	Signed in	\N	127.0.0.1	Web.App
01a106a9-4f02-7737-b5b8-43815cf53e93	2026-10-04 18:25:21.537725+07	SignIn	SignedIn	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	User	01a0f240-f921-75b0-972d-0f74522a6333	Signed in	\N	127.0.0.1	Web.App
01a106ad-b5ba-7674-8b2f-fd0b4ef5535d	2026-10-04 18:30:09.960486+07	Access	CreateBranch	01a0f240-f921-75b0-972d-0f74522a6333	\N	Branch	01a106ad-b536-7f76-bf02-1115db4d6aff	Create branch (Branch)	{"code": "BDG", "name": "Cabang Bandung", "phone": "022-7501234", "address": "Jl. Soekarno-Hatta No. 120, Bandung"}	\N	Web.Api
01a106ad-b5e2-7f9e-9da4-90b9f56cf283	2026-10-04 18:30:10.01789+07	Access	CreateBranch	01a0f240-f921-75b0-972d-0f74522a6333	\N	Branch	01a106ad-b5da-7c16-8a1e-37a0998b9705	Create branch (Branch)	{"code": "CJR", "name": "Cabang Cianjur", "phone": "0263-261234", "address": "Jl. Raya Bandung KM 5, Cianjur"}	\N	Web.Api
01a106ad-b7cb-78ce-8936-564e2d29142c	2026-10-04 18:30:10.499304+07	Access	CreateUser	01a0f240-f921-75b0-972d-0f74522a6333	\N	User	01a106ad-b782-7dd8-9680-2dd57b9573c4	Create user (User)	{"email": "checker@intiplasma.local", "roleIds": ["01a0f240-f7a6-7484-868c-9da0bc7ed0c8"], "lastName": "Rahmawati", "password": "***", "firstName": "Siti", "defaultBranchId": "01a106ad-b536-7f76-bf02-1115db4d6aff", "menuAccessProfileId": "0199a3c0-0000-7000-8000-000000000001", "branchAccessProfileId": "0199a3c0-0000-7000-8000-000000000002"}	\N	Web.Api
01a106ad-b822-7ba5-bd30-acc349e3ce38	2026-10-04 18:30:10.593023+07	Access	CreateBranchAccessProfile	01a0f240-f921-75b0-972d-0f74522a6333	\N	BranchAccessProfile	01a106ad-b803-7abc-a19a-20a53d1f9bf4	Create branch access profile (BranchAccessProfile)	{"name": "Cabang Bandung", "branchIds": ["01a106ad-b536-7f76-bf02-1115db4d6aff"], "allBranches": false, "description": "Hanya data Cabang Bandung (contoh akses cabang)"}	\N	Web.Api
01a106ad-b94c-7345-a041-4d3d3ce32944	2026-10-04 18:30:10.892702+07	Access	CreateUser	01a0f240-f921-75b0-972d-0f74522a6333	\N	User	01a106ad-b93f-7644-8f8a-4d7bfe6cf1e3	Create user (User)	{"email": "staff.bdg@intiplasma.local", "roleIds": [], "lastName": "Santoso", "password": "***", "firstName": "Budi", "defaultBranchId": "01a106ad-b536-7f76-bf02-1115db4d6aff", "menuAccessProfileId": "0199a3c0-0000-7000-8000-000000000001", "branchAccessProfileId": "01a106ad-b803-7abc-a19a-20a53d1f9bf4"}	\N	Web.Api
01a106af-825b-7705-a42a-e0f0cf7bc682	2026-10-04 18:32:07.887962+07	Export	PDF	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	procurement.purchase-orders	01a106ae-1a83-7450-beaa-1665838fec1a	PDF print of Purchase Order	{"menu": "procurement.purchase-orders", "rows": 1, "title": "Purchase Order", "branch": "Cabang Bandung", "filters": ["No. PO/BDG/2026/X/0004", "Status: Draft"]}	127.0.0.1	Web.App
01a106b2-7d51-7c2f-8744-35b7ec1e843f	2026-10-04 18:35:23.217321+07	Export	PDF	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	procurement.purchase-orders	01a106ae-17ae-7df4-8664-2a0e8d2c9877	PDF print of Purchase Order	{"menu": "procurement.purchase-orders", "rows": 1, "title": "Purchase Order", "branch": "Cabang Bandung", "filters": ["No. PO/BDG/2026/X/0001", "Status: Approved"]}	127.0.0.1	Web.App
01a106b5-9c58-76e1-a38f-dc360462e808	2026-10-04 18:38:47.767941+07	Export	PDF	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	production.cycles	01a106ad-d05a-759c-a6ff-c3e67df7f41b	PDF print of Cycle Closing Summary	{"menu": "production.cycles", "rows": 1, "title": "Cycle Closing Summary", "branch": "Cabang Bandung", "filters": ["No. SKL/BDG/2026/VII/0002", "Status: Closed"]}	127.0.0.1	Web.App
01a106e5-96b0-7e3e-8bdd-1e41bc792737	2026-10-04 19:31:12.047555+07	SignIn	SignedIn	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	User	01a0f240-f921-75b0-972d-0f74522a6333	Signed in	\N	127.0.0.1	Web.App
01a106eb-3c3b-70b3-947a-db500398ca2d	2026-10-04 19:37:22.107097+07	SignIn	SignedIn	01a0f240-f921-75b0-972d-0f74522a6333	admin@intiplasma.local	User	01a0f240-f921-75b0-972d-0f74522a6333	Signed in	\N	127.0.0.1	Web.App
\.


--
-- Data for Name: data_protection_keys; Type: TABLE DATA; Schema: infrastructure; Owner: postgres
--

COPY infrastructure.data_protection_keys (id, friendly_name, xml) FROM stdin;
1	key-7249e3d1-1d12-4f7e-aaa2-e092cf267f5e	<key id="7249e3d1-1d12-4f7e-aaa2-e092cf267f5e" version="1"><creationDate>2026-10-03T20:18:11.0051644Z</creationDate><activationDate>2026-10-03T20:18:11.0051644Z</activationDate><expirationDate>2027-01-01T20:18:11.0051644Z</expirationDate><descriptor deserializerType="Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60"><descriptor><encryption algorithm="AES_256_CBC" /><validation algorithm="HMACSHA256" /><masterKey p4:requiresEncryption="true" xmlns:p4="http://schemas.asp.net/2015/03/dataProtection"><!-- Warning: the key below is in an unencrypted form. --><value>rcNK0vLNFQe7mD7ZEgQQ27M3JQSShy9Rde1wCO1tcDeFF0k7L/mqmO7KVE2sAXqjvkosBsvpd5fcVQa9TwZIcQ==</value></masterKey></descriptor></descriptor></key>
\.


--
-- Data for Name: document_sequences; Type: TABLE DATA; Schema: infrastructure; Owner: postgres
--

COPY infrastructure.document_sequences (key, last_value) FROM stdin;
STL/BDG/2026/IX	1
RCV/CJR/2026/X	3
PV/CJR/2026/VIII	2
SKL/CJR/2026/VIII	1
JO/CJR/2026/X	5
PO/CJR/2026/VIII	3
BKM/BDG/2026/VII	1
PO/BDG/2026/X	4
BKM/CJR/2026/VII	1
BPB/CJR/2026/IX	3
CN/BDG/2026/VIII	1
TRF/CJR/2026/IX	3
SKL/BDG/2026/VII	2
RTR/CJR/2026/IX	1
PO/BDG/2026/VII	6
BPB/CJR/2026/VIII	3
TRF/BDG/2026/IX	4
TRF/CJR/2026/VIII	3
SO/CJR/2026/IX	1
BPB/BDG/2026/VII	6
RCV/CJR/2026/IX	4
STL/BDG/2026/VIII	1
SKL/CJR/2026/VII	1
PO/CJR/2026/VII	3
BKK/BDG/2026/VII	6
STL/CJR/2026/IX	1
BKK/CJR/2026/VII	6
TRF/BDG/2026/VII	4
SKL/BDG/2026/IX	1
SO/BDG/2026/VIII	2
PV/BDG/2026/VIII	6
SKL/BDG/2026/VIII	1
BPB/CJR/2026/VII	3
PO/BDG/2026/IX	3
TRF/CJR/2026/VII	2
JO/CJR/2026/VII	13
JU/BDG/2026/VII	2
JU/CJR/2026/VII	2
PO/BDG/2026/VIII	3
VI/BDG/2026/VII	6
JO/BDG/2026/VII	25
VI/CJR/2026/IX	3
BKK/BDG/2026/IX	6
JO/BDG/2026/IX	24
BKK/CJR/2026/IX	6
VI/CJR/2026/VIII	6
DO/BDG/2026/VIII	5
INV/BDG/2026/VIII	5
BPB/BDG/2026/VIII	3
DO/CJR/2026/IX	6
PV/CJR/2026/IX	4
BPB/BDG/2026/IX	3
BKM/BDG/2026/VIII	1
BKM/CJR/2026/VIII	1
JU/BDG/2026/IX	1
BKK/BDG/2026/VIII	7
JU/CJR/2026/IX	1
JU/BDG/2026/VIII	1
SO/BDG/2026/IX	1
BKK/CJR/2026/VIII	7
JO/CJR/2026/VIII	23
JU/CJR/2026/VIII	1
RCV/BDG/2026/VIII	4
INV/CJR/2026/IX	6
BKM/BDG/2026/IX	1
TRF/BDG/2026/VIII	4
BKM/CJR/2026/IX	1
RTR/BDG/2026/VIII	3
TRF/BDG/2026/X	1
JO/BDG/2026/VIII	38
SO/CJR/2026/VIII	1
RCV/BDG/2026/IX	1
TRF/CJR/2026/X	1
VI/BDG/2026/IX	6
SKL/CJR/2026/IX	1
RTR/CJR/2026/X	1
PO/CJR/2026/IX	3
PV/BDG/2026/IX	2
JO/CJR/2026/IX	35
DO/BDG/2026/X	2
INV/BDG/2026/X	1
PV/BDG/2026/X	3
JO/BDG/2026/X	5
SKL/BDG/2026/X	1
\.


--
-- Data for Name: outbox_messages; Type: TABLE DATA; Schema: infrastructure; Owner: postgres
--

COPY infrastructure.outbox_messages (id, type, content, occurred_on_utc, processed_on_utc, attempts, error) FROM stdin;
01a0f240-f7a8-720a-b770-f6310d5a548f	Domain.Roles.RolePermissionsChangedDomainEvent	{"id": "01a0f240-f7a8-720a-b770-f6310d5a548f", "roleId": "01a0f240-f7a6-7484-868c-9da0bc7ed0c8", "occurredOnUtc": "2026-09-30T12:18:59.1125948Z"}	2026-09-30 19:18:59.112594+07	2026-09-30 19:18:59.873105+07	1	\N
01a0f240-f922-70f7-9c3e-b2066b64eab3	Domain.Users.UserRegisteredDomainEvent	{"id": "01a0f240-f922-70f7-9c3e-b2066b64eab3", "userId": "01a0f240-f921-75b0-972d-0f74522a6333", "occurredOnUtc": "2026-09-30T12:18:59.4900982Z"}	2026-09-30 19:18:59.490098+07	2026-09-30 19:18:59.894888+07	1	\N
01a0f240-f925-7224-8574-f22b65b7798f	Domain.Users.UserRolesChangedDomainEvent	{"id": "01a0f240-f925-7224-8574-f22b65b7798f", "userId": "01a0f240-f921-75b0-972d-0f74522a6333", "occurredOnUtc": "2026-09-30T12:18:59.4930152Z"}	2026-09-30 19:18:59.493015+07	2026-09-30 19:18:59.89854+07	1	\N
01a0f50c-aa93-7c8e-b621-433e3aad1dd5	Domain.Roles.RolePermissionsChangedDomainEvent	{"id": "01a0f50c-aa93-7c8e-b621-433e3aad1dd5", "roleId": "01a0f240-f7a6-7484-868c-9da0bc7ed0c8", "occurredOnUtc": "2026-10-01T01:20:43.155722Z"}	2026-10-01 08:20:43.155722+07	2026-10-01 08:20:43.77295+07	1	\N
01a106a7-e62d-7491-938d-8dfce96e5cdb	Domain.Roles.RolePermissionsChangedDomainEvent	{"id": "01a106a7-e62d-7491-938d-8dfce96e5cdb", "roleId": "01a0f240-f7a6-7484-868c-9da0bc7ed0c8", "occurredOnUtc": "2026-10-04T11:23:49.1657701Z"}	2026-10-04 18:23:49.16577+07	2026-10-04 18:24:59.029436+07	1	\N
01a106ad-b782-771c-a558-04add2590528	Domain.Users.UserRegisteredDomainEvent	{"id": "01a106ad-b782-771c-a558-04add2590528", "userId": "01a106ad-b782-7dd8-9680-2dd57b9573c4", "occurredOnUtc": "2026-10-04T11:30:10.434489Z"}	2026-10-04 18:30:10.434489+07	2026-10-04 18:30:10.523888+07	1	\N
01a106ad-b784-7de5-8126-5672ef92d50a	Domain.Users.UserRolesChangedDomainEvent	{"id": "01a106ad-b784-7de5-8126-5672ef92d50a", "userId": "01a106ad-b782-7dd8-9680-2dd57b9573c4", "occurredOnUtc": "2026-10-04T11:30:10.4368274Z"}	2026-10-04 18:30:10.436827+07	2026-10-04 18:30:10.538062+07	1	\N
01a106ad-b93f-7dd9-bd0a-6995b6698b28	Domain.Users.UserRegisteredDomainEvent	{"id": "01a106ad-b93f-7dd9-bd0a-6995b6698b28", "userId": "01a106ad-b93f-7644-8f8a-4d7bfe6cf1e3", "occurredOnUtc": "2026-10-04T11:30:10.8799038Z"}	2026-10-04 18:30:10.879903+07	2026-10-04 18:30:10.898596+07	1	\N
01a106ad-b940-7efe-949a-e6bdb658f376	Domain.Users.UserRolesChangedDomainEvent	{"id": "01a106ad-b940-7efe-949a-e6bdb658f376", "userId": "01a106ad-b93f-7644-8f8a-4d7bfe6cf1e3", "occurredOnUtc": "2026-10-04T11:30:10.8800246Z"}	2026-10-04 18:30:10.880024+07	2026-10-04 18:30:10.899193+07	1	\N
01a106ad-bfed-7fd7-84fa-d2a55eb732d2	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-bfed-7fd7-84fa-d2a55eb732d2", "coopId": "01a106ad-bfec-7b37-a51a-18c521c8bce6", "occurredOnUtc": "2026-10-04T11:30:12.5891817Z"}	2026-10-04 18:30:12.589181+07	2026-10-04 18:30:12.735141+07	1	\N
01a106ad-c093-7bfc-8187-c24648b43f6e	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c093-7bfc-8187-c24648b43f6e", "coopId": "01a106ad-c093-725b-9c5f-bb53c87c1551", "occurredOnUtc": "2026-10-04T11:30:12.7558785Z"}	2026-10-04 18:30:12.755878+07	2026-10-04 18:30:12.773456+07	1	\N
01a106ad-c0b0-70d6-b592-d1d9e8130bcf	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c0b0-70d6-b592-d1d9e8130bcf", "coopId": "01a106ad-c0b0-728b-bb71-3e70df15f86d", "occurredOnUtc": "2026-10-04T11:30:12.784451Z"}	2026-10-04 18:30:12.784451+07	2026-10-04 18:30:12.804447+07	1	\N
01a106ad-c0cc-777c-8db2-2ff275ab6995	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c0cc-777c-8db2-2ff275ab6995", "coopId": "01a106ad-c0cc-775f-96fa-cd8dda0154d7", "occurredOnUtc": "2026-10-04T11:30:12.8129633Z"}	2026-10-04 18:30:12.812963+07	2026-10-04 18:30:12.829278+07	1	\N
01a106ad-c0e5-75fd-b778-6ff96fe251e7	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c0e5-75fd-b778-6ff96fe251e7", "coopId": "01a106ad-c0e5-768d-a018-7fac5d24b72d", "occurredOnUtc": "2026-10-04T11:30:12.8378079Z"}	2026-10-04 18:30:12.837807+07	2026-10-04 18:30:12.85559+07	1	\N
01a106ad-c0fe-72d4-b2e6-442d645fe4e1	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c0fe-72d4-b2e6-442d645fe4e1", "coopId": "01a106ad-c0fe-7ef7-883a-6874a1e63d54", "occurredOnUtc": "2026-10-04T11:30:12.8621268Z"}	2026-10-04 18:30:12.862126+07	2026-10-04 18:30:12.889587+07	1	\N
01a106ad-c120-7f9d-8bec-9b7a86a6a3f8	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c120-7f9d-8bec-9b7a86a6a3f8", "coopId": "01a106ad-c120-72e3-bae4-d2248cc584d3", "occurredOnUtc": "2026-10-04T11:30:12.8961361Z"}	2026-10-04 18:30:12.896136+07	2026-10-04 18:30:12.914064+07	1	\N
01a106ad-c139-7071-a8fc-ecc7b79fe846	Domain.MasterData.Coops.CoopCreatedDomainEvent	{"id": "01a106ad-c139-7071-a8fc-ecc7b79fe846", "coopId": "01a106ad-c139-7a07-b8e4-9ea7321c38b7", "occurredOnUtc": "2026-10-04T11:30:12.9216783Z"}	2026-10-04 18:30:12.921678+07	2026-10-04 18:30:12.938404+07	1	\N
01a106ad-c267-78d2-bc12-c261ff3b8984	Domain.Partnership.Contracts.ContractActivatedDomainEvent	{"id": "01a106ad-c267-78d2-bc12-c261ff3b8984", "contractId": "01a106ad-c18d-7237-a0e3-d935699c85c6", "occurredOnUtc": "2026-10-04T11:30:13.2239849Z"}	2026-10-04 18:30:13.223984+07	2026-10-04 18:30:13.232224+07	1	\N
01a106ad-c28e-7004-89aa-846dc2cfd506	Domain.Partnership.Contracts.ContractActivatedDomainEvent	{"id": "01a106ad-c28e-7004-89aa-846dc2cfd506", "contractId": "01a106ad-c27b-708d-99cc-89e0647e1680", "occurredOnUtc": "2026-10-04T11:30:13.2625028Z"}	2026-10-04 18:30:13.262502+07	2026-10-04 18:30:13.26747+07	1	\N
01a106ad-c2b8-72fc-adad-688d2f74f3e4	Domain.Partnership.Contracts.ContractActivatedDomainEvent	{"id": "01a106ad-c2b8-72fc-adad-688d2f74f3e4", "contractId": "01a106ad-c29f-73ee-9803-d9755e1eef7f", "occurredOnUtc": "2026-10-04T11:30:13.3042562Z"}	2026-10-04 18:30:13.304256+07	2026-10-04 18:30:13.309415+07	1	\N
01a106ad-c3a5-7c6d-829e-37ae3caa09a3	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c3a5-7c6d-829e-37ae3caa09a3", "journalId": "01a106ad-c2de-75df-b31f-49ff1b32abd1", "occurredOnUtc": "2026-10-04T11:30:13.5413036Z"}	2026-10-04 18:30:13.541303+07	2026-10-04 18:30:13.552572+07	1	\N
01a106ad-c3d0-7d4d-87d7-b6a8fa616c1a	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-c3d0-7d4d-87d7-b6a8fa616c1a", "occurredOnUtc": "2026-10-04T11:30:13.5849369Z", "bankTransferId": "01a106ad-c3d0-7907-a4b2-a8c2ee291819"}	2026-10-04 18:30:13.584936+07	2026-10-04 18:30:13.689811+07	1	\N
01a106ad-c432-7a94-ae01-67b419520bcb	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c432-7a94-ae01-67b419520bcb", "journalId": "01a106ad-c424-7c83-8f5c-756d6c810f2d", "occurredOnUtc": "2026-10-04T11:30:13.6826813Z"}	2026-10-04 18:30:13.682681+07	2026-10-04 18:30:13.690743+07	1	\N
01a106ad-c463-726a-b673-16aa21430812	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c463-726a-b673-16aa21430812", "journalId": "01a106ad-c445-7d81-b732-0a2a80f68d53", "occurredOnUtc": "2026-10-04T11:30:13.7312973Z"}	2026-10-04 18:30:13.731297+07	2026-10-04 18:30:13.73708+07	1	\N
01a106ad-c474-7925-b5ac-2c932f1bdfb8	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-c474-7925-b5ac-2c932f1bdfb8", "occurredOnUtc": "2026-10-04T11:30:13.7481865Z", "bankTransferId": "01a106ad-c474-722b-b16b-f07cb4eb3e41"}	2026-10-04 18:30:13.748186+07	2026-10-04 18:30:13.775645+07	1	\N
01a106ad-c48b-779e-a4e5-6a8b6e9ada18	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c48b-779e-a4e5-6a8b6e9ada18", "journalId": "01a106ad-c485-70d9-8223-fa71882a85aa", "occurredOnUtc": "2026-10-04T11:30:13.771546Z"}	2026-10-04 18:30:13.771546+07	2026-10-04 18:30:13.776488+07	1	\N
01a106ad-c536-7dc0-a029-7dc18245ce7e	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c536-7dc0-a029-7dc18245ce7e", "occurredOnUtc": "2026-10-04T11:30:13.9425433Z", "cashTransactionId": "01a106ad-c4b5-7491-95e0-15863c52ff8c"}	2026-10-04 18:30:13.942543+07	2026-10-04 18:30:14.008724+07	1	\N
01a106ad-c571-760b-959a-55025d56e0e9	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c571-760b-959a-55025d56e0e9", "journalId": "01a106ad-c56b-706f-8d43-1e873cf6097f", "occurredOnUtc": "2026-10-04T11:30:14.0016869Z"}	2026-10-04 18:30:14.001686+07	2026-10-04 18:30:14.009704+07	1	\N
01a106ad-c5bc-7886-8731-956d780f7e47	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c5bc-7886-8731-956d780f7e47", "journalId": "01a106ad-c5b6-7474-9f11-92c3cfbfa142", "occurredOnUtc": "2026-10-04T11:30:14.0761839Z"}	2026-10-04 18:30:14.076183+07	2026-10-04 18:30:14.09219+07	1	\N
01a106ad-c5f9-739e-a02c-2ae65186406f	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c5f9-739e-a02c-2ae65186406f", "occurredOnUtc": "2026-10-04T11:30:14.1379286Z", "cashTransactionId": "01a106ad-c5d8-7957-bc71-fe91e116249e"}	2026-10-04 18:30:14.137928+07	2026-10-04 18:30:14.170627+07	1	\N
01a106ad-c614-70c9-bee9-4b42714fc49d	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c614-70c9-bee9-4b42714fc49d", "journalId": "01a106ad-c60e-78d0-ae7f-255cd0eaf991", "occurredOnUtc": "2026-10-04T11:30:14.1649192Z"}	2026-10-04 18:30:14.164919+07	2026-10-04 18:30:14.172018+07	1	\N
01a106ad-c643-7713-9146-5b26d2f5670e	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c643-7713-9146-5b26d2f5670e", "occurredOnUtc": "2026-10-04T11:30:14.211896Z", "cashTransactionId": "01a106ad-c624-796b-8b34-0b7f901d8aa7"}	2026-10-04 18:30:14.211896+07	2026-10-04 18:30:14.253636+07	1	\N
01a106ad-c664-756e-811f-b15524d91e95	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c664-756e-811f-b15524d91e95", "journalId": "01a106ad-c658-784b-a7f7-25f49abf2023", "occurredOnUtc": "2026-10-04T11:30:14.2446583Z"}	2026-10-04 18:30:14.244658+07	2026-10-04 18:30:14.255131+07	1	\N
01a106ad-c6f1-75b4-a479-7578f9de111c	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ad-c6f1-75b4-a479-7578f9de111c", "cycleId": "01a106ad-c6f1-7f5f-8d11-f2ccfb191671", "occurredOnUtc": "2026-10-04T11:30:14.3852451Z"}	2026-10-04 18:30:14.385245+07	2026-10-04 18:30:14.507297+07	1	\N
01a106ad-c892-7153-aaff-95657194b1df	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c892-7153-aaff-95657194b1df", "occurredOnUtc": "2026-10-04T11:30:14.8025066Z", "cashTransactionId": "01a106ad-c878-768e-9b3d-190b670f41bb"}	2026-10-04 18:30:14.802506+07	2026-10-04 18:30:14.830541+07	1	\N
01a106ad-c8aa-7cc4-bb37-10ef8284cc5a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c8aa-7cc4-bb37-10ef8284cc5a", "journalId": "01a106ad-c8a4-770d-acc7-078e0f57fa99", "occurredOnUtc": "2026-10-04T11:30:14.8262195Z"}	2026-10-04 18:30:14.826219+07	2026-10-04 18:30:14.831937+07	1	\N
01a106ad-c8d3-78e2-ae42-e31661f76eaa	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c8d3-78e2-ae42-e31661f76eaa", "occurredOnUtc": "2026-10-04T11:30:14.8677527Z", "cashTransactionId": "01a106ad-c8b8-732f-b108-db0e07ef5559"}	2026-10-04 18:30:14.867752+07	2026-10-04 18:30:14.89513+07	1	\N
01a106ad-c8ea-7dc9-9e95-45941ec4acb7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-c8ea-7dc9-9e95-45941ec4acb7", "journalId": "01a106ad-c8e4-7a06-ac4d-a2d0c59f8759", "occurredOnUtc": "2026-10-04T11:30:14.8902754Z"}	2026-10-04 18:30:14.890275+07	2026-10-04 18:30:14.895982+07	1	\N
01a106ad-ca78-7a11-8a89-91e71746de74	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-ca78-7a11-8a89-91e71746de74", "occurredOnUtc": "2026-10-04T11:30:15.2889874Z", "cashTransactionId": "01a106ad-ca69-7399-87e7-557b1b2228fb"}	2026-10-04 18:30:15.288987+07	2026-10-04 18:30:15.314729+07	1	\N
01a106ad-ca8e-7531-8694-6094b87239ce	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ca8e-7531-8694-6094b87239ce", "journalId": "01a106ad-ca89-716b-bef3-1d82a5ee5b18", "occurredOnUtc": "2026-10-04T11:30:15.3104523Z"}	2026-10-04 18:30:15.310452+07	2026-10-04 18:30:15.315986+07	1	\N
01a106ad-caea-7fdd-9fe1-9460cea8bc02	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-caea-7fdd-9fe1-9460cea8bc02", "occurredOnUtc": "2026-10-04T11:30:15.4027036Z", "goodsReceiptId": "01a106ad-caea-7afd-8a30-a75d8b846fd2"}	2026-10-04 18:30:15.402703+07	2026-10-04 18:30:15.514807+07	1	\N
01a106ad-cb3e-7b51-8038-67209dd8cded	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cb3e-7b51-8038-67209dd8cded", "journalId": "01a106ad-cb36-73ee-8d81-4f9166dc9ebd", "occurredOnUtc": "2026-10-04T11:30:15.4862368Z"}	2026-10-04 18:30:15.486236+07	2026-10-04 18:30:15.516008+07	1	\N
01a106ad-cb55-73fc-a7f3-5c302bd2b088	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cb55-73fc-a7f3-5c302bd2b088", "journalId": "01a106ad-cb4f-716f-b776-4b8bd2d05bef", "occurredOnUtc": "2026-10-04T11:30:15.5098477Z"}	2026-10-04 18:30:15.509847+07	2026-10-04 18:30:15.516285+07	1	\N
01a106ad-cb8b-7b5c-9ba8-b0bb279a0e8c	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ad-cb8b-7b5c-9ba8-b0bb279a0e8c", "cycleId": "01a106ad-c6f1-7f5f-8d11-f2ccfb191671", "occurredOnUtc": "2026-10-04T11:30:15.5636892Z"}	2026-10-04 18:30:15.563689+07	2026-10-04 18:30:15.587255+07	1	\N
01a106ad-cbd7-7930-9181-954024f73db6	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-cbd7-7930-9181-954024f73db6", "occurredOnUtc": "2026-10-04T11:30:15.6395097Z", "stockTransferId": "01a106ad-cbd7-752a-838d-978a3b279daf"}	2026-10-04 18:30:15.639509+07	2026-10-04 18:30:15.771108+07	1	\N
01a106ad-cc54-7855-8359-d94f5e97e632	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cc54-7855-8359-d94f5e97e632", "journalId": "01a106ad-cc4e-7124-8ecc-52922aa61e5f", "occurredOnUtc": "2026-10-04T11:30:15.764949Z"}	2026-10-04 18:30:15.764949+07	2026-10-04 18:30:15.772169+07	1	\N
01a106ad-cc8f-74d2-9aff-a6c92908f349	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-cc8f-74d2-9aff-a6c92908f349", "occurredOnUtc": "2026-10-04T11:30:15.8235974Z", "dailyRecordingId": "01a106ad-cc5e-7fa7-90b9-50b3cfd71995"}	2026-10-04 18:30:15.823597+07	2026-10-04 18:30:15.884837+07	1	\N
01a106ad-ccdf-7c81-a6b7-d15edb245ae5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ccdf-7c81-a6b7-d15edb245ae5", "occurredOnUtc": "2026-10-04T11:30:15.9039299Z", "dailyRecordingId": "01a106ad-ccce-7f34-a0ee-cfecd67d2f84"}	2026-10-04 18:30:15.903929+07	2026-10-04 18:30:15.920005+07	1	\N
01a106ad-ce8f-7843-9e5a-b44ecfa2d4d5	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-ce8f-7843-9e5a-b44ecfa2d4d5", "occurredOnUtc": "2026-10-04T11:30:16.3358391Z", "cashTransactionId": "01a106ad-ce74-744a-a8dc-0a7bf0721a72"}	2026-10-04 18:30:16.335839+07	2026-10-04 18:30:16.43646+07	1	\N
01a106ad-ceef-70db-9474-4ebcbf81ab80	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ceef-70db-9474-4ebcbf81ab80", "journalId": "01a106ad-cea1-7f98-a52c-aca7128720ff", "occurredOnUtc": "2026-10-04T11:30:16.4311604Z"}	2026-10-04 18:30:16.43116+07	2026-10-04 18:30:16.43774+07	1	\N
01a106ad-cf18-7ff6-9e40-5d18bff01238	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-cf18-7ff6-9e40-5d18bff01238", "occurredOnUtc": "2026-10-04T11:30:16.4724635Z", "cashTransactionId": "01a106ad-cefd-7b1d-aa05-19dae4305228"}	2026-10-04 18:30:16.472463+07	2026-10-04 18:30:16.500128+07	1	\N
01a106ad-cf2f-77a7-9d99-fd987b323c09	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cf2f-77a7-9d99-fd987b323c09", "journalId": "01a106ad-cf29-774c-aa91-2e1e58c7940d", "occurredOnUtc": "2026-10-04T11:30:16.4954527Z"}	2026-10-04 18:30:16.495452+07	2026-10-04 18:30:16.501031+07	1	\N
01a106ad-cf41-7e77-af60-4daf22bce285	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-cf41-7e77-af60-4daf22bce285", "occurredOnUtc": "2026-10-04T11:30:16.5138989Z", "dailyRecordingId": "01a106ad-cf36-79b5-8c8f-4c13577bfec5"}	2026-10-04 18:30:16.513898+07	2026-10-04 18:30:16.531449+07	1	\N
01a106ad-d05a-7ef3-aef7-d792fed099ab	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ad-d05a-7ef3-aef7-d792fed099ab", "cycleId": "01a106ad-d05a-759c-a6ff-c3e67df7f41b", "occurredOnUtc": "2026-10-04T11:30:16.794632Z"}	2026-10-04 18:30:16.794632+07	2026-10-04 18:30:16.800946+07	1	\N
01a106ad-d0d7-74c7-9549-1ea867472222	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d0d7-74c7-9549-1ea867472222", "occurredOnUtc": "2026-10-04T11:30:16.9195675Z", "dailyRecordingId": "01a106ad-d0cb-72a0-9381-697ced243e40"}	2026-10-04 18:30:16.919567+07	2026-10-04 18:30:16.929934+07	1	\N
01a106ad-c5a4-7af4-a16c-e0185e72b422	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-c5a4-7af4-a16c-e0185e72b422", "occurredOnUtc": "2026-10-04T11:30:14.0528922Z", "cashTransactionId": "01a106ad-c584-7c38-b34d-8c5434d2cf4d"}	2026-10-04 18:30:14.052892+07	2026-10-04 18:30:14.082528+07	1	\N
01a106ad-c921-7c28-b4da-52ca6ef8f3c7	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-c921-7c28-b4da-52ca6ef8f3c7", "occurredOnUtc": "2026-10-04T11:30:14.9459706Z", "goodsReceiptId": "01a106ad-c921-7abc-9538-f9aefa07df64"}	2026-10-04 18:30:14.94597+07	2026-10-04 18:30:15.191035+07	1	\N
01a106ad-ca12-79b1-8f5a-4176d95d1cfd	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ca12-79b1-8f5a-4176d95d1cfd", "journalId": "01a106ad-ca0b-7631-aaa1-6b0f7a4ec843", "occurredOnUtc": "2026-10-04T11:30:15.1864885Z"}	2026-10-04 18:30:15.186488+07	2026-10-04 18:30:15.191927+07	1	\N
01a106ad-ca2d-74d1-b384-dc92ea132357	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-ca2d-74d1-b384-dc92ea132357", "occurredOnUtc": "2026-10-04T11:30:15.213576Z", "goodsReceiptId": "01a106ad-ca2d-7c57-8ba4-d49b50adc0b1"}	2026-10-04 18:30:15.213576+07	2026-10-04 18:30:15.263376+07	1	\N
01a106ad-ca5a-7219-9810-97fde71ec857	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ca5a-7219-9810-97fde71ec857", "journalId": "01a106ad-ca55-7f77-992d-3b6ec5c41b73", "occurredOnUtc": "2026-10-04T11:30:15.2588138Z"}	2026-10-04 18:30:15.258813+07	2026-10-04 18:30:15.264491+07	1	\N
01a106ad-caad-7760-a78c-74948129f45a	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-caad-7760-a78c-74948129f45a", "occurredOnUtc": "2026-10-04T11:30:15.3415303Z", "cashTransactionId": "01a106ad-ca9c-741d-8ba1-354b097fa37b"}	2026-10-04 18:30:15.34153+07	2026-10-04 18:30:15.369489+07	1	\N
01a106ad-cac4-7ed8-bf51-3d79789ecfe2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cac4-7ed8-bf51-3d79789ecfe2", "journalId": "01a106ad-cabf-7cd0-a8b1-363292123dad", "occurredOnUtc": "2026-10-04T11:30:15.3643602Z"}	2026-10-04 18:30:15.36436+07	2026-10-04 18:30:15.37039+07	1	\N
01a106ad-ce14-7780-8ed3-ac783b257be6	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-ce14-7780-8ed3-ac783b257be6", "occurredOnUtc": "2026-10-04T11:30:16.2122641Z", "vendorInvoiceId": "01a106ad-cd6f-7c22-ae20-adc16848cca8"}	2026-10-04 18:30:16.212264+07	2026-10-04 18:30:16.270675+07	1	\N
01a106ad-ce49-71ff-8e6d-2ad0697a999e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ce49-71ff-8e6d-2ad0697a999e", "journalId": "01a106ad-ce43-7d55-bc58-6d3b0d034353", "occurredOnUtc": "2026-10-04T11:30:16.2659732Z"}	2026-10-04 18:30:16.265973+07	2026-10-04 18:30:16.271651+07	1	\N
01a106ad-ce5f-7d9f-a084-415b32ad5c9d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ce5f-7d9f-a084-415b32ad5c9d", "occurredOnUtc": "2026-10-04T11:30:16.2876451Z", "dailyRecordingId": "01a106ad-ce50-7f61-91ce-5a67a6545d4f"}	2026-10-04 18:30:16.287645+07	2026-10-04 18:30:16.299962+07	1	\N
01a106ad-cf80-787a-91b3-1ffed99c98f6	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-cf80-787a-91b3-1ffed99c98f6", "occurredOnUtc": "2026-10-04T11:30:16.5763034Z", "vendorInvoiceId": "01a106ad-cf68-792c-a1d9-7d5ad8b45cad"}	2026-10-04 18:30:16.576303+07	2026-10-04 18:30:16.607667+07	1	\N
01a106ad-cf9b-776b-a5c3-4c7775ada76a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-cf9b-776b-a5c3-4c7775ada76a", "journalId": "01a106ad-cf95-7fdb-853d-a8d574d4910b", "occurredOnUtc": "2026-10-04T11:30:16.6033773Z"}	2026-10-04 18:30:16.603377+07	2026-10-04 18:30:16.608573+07	1	\N
01a106ad-cfd5-7944-ab78-05567bcb0979	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-cfd5-7944-ab78-05567bcb0979", "occurredOnUtc": "2026-10-04T11:30:16.6619138Z", "vendorInvoiceId": "01a106ad-cfb6-7c64-a77d-6297bcaf3bf5"}	2026-10-04 18:30:16.661913+07	2026-10-04 18:30:16.713024+07	1	\N
01a106ad-d001-7a27-a832-9f89f2e9b05f	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d001-7a27-a832-9f89f2e9b05f", "journalId": "01a106ad-cff8-7e51-9a7c-d0c0a058cb4a", "occurredOnUtc": "2026-10-04T11:30:16.705599Z"}	2026-10-04 18:30:16.705599+07	2026-10-04 18:30:16.716181+07	1	\N
01a106ad-d01b-7d05-8b92-17ddedf081f7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d01b-7d05-8b92-17ddedf081f7", "occurredOnUtc": "2026-10-04T11:30:16.7317509Z", "dailyRecordingId": "01a106ad-d00d-7069-8e1a-4653e2c1b488"}	2026-10-04 18:30:16.73175+07	2026-10-04 18:30:16.743084+07	1	\N
01a106ad-d034-7560-b694-e9a587ac9910	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d034-7560-b694-e9a587ac9910", "occurredOnUtc": "2026-10-04T11:30:16.7569515Z", "dailyRecordingId": "01a106ad-d027-7dc3-9716-afd16516d48c"}	2026-10-04 18:30:16.756951+07	2026-10-04 18:30:16.771871+07	1	\N
01a106ad-d0ef-76c4-89ce-64de048305ef	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d0ef-76c4-89ce-64de048305ef", "occurredOnUtc": "2026-10-04T11:30:16.9436052Z", "dailyRecordingId": "01a106ad-d0e3-771a-9f12-829d30bfb43f"}	2026-10-04 18:30:16.943605+07	2026-10-04 18:30:16.959109+07	1	\N
01a106ad-d113-79dd-b02e-4298af29f327	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d113-79dd-b02e-4298af29f327", "occurredOnUtc": "2026-10-04T11:30:16.9791541Z", "goodsReceiptId": "01a106ad-d113-7702-95fa-9936fda3cb97"}	2026-10-04 18:30:16.979154+07	2026-10-04 18:30:17.024127+07	1	\N
01a106ad-d4ab-7701-bbd4-1009d8614cc1	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d4ab-7701-bbd4-1009d8614cc1", "occurredOnUtc": "2026-10-04T11:30:17.899261Z", "dailyRecordingId": "01a106ad-d49f-779f-b620-5eb282c8da5a"}	2026-10-04 18:30:17.899261+07	2026-10-04 18:30:17.918308+07	1	\N
01a106ad-d524-7de8-b1a6-635fd2de212b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d524-7de8-b1a6-635fd2de212b", "occurredOnUtc": "2026-10-04T11:30:18.0203023Z", "dailyRecordingId": "01a106ad-d517-7d09-ae16-0720df9f4314"}	2026-10-04 18:30:18.020302+07	2026-10-04 18:30:18.035495+07	1	\N
01a106ad-d541-7be0-9218-d517da2af56b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d541-7be0-9218-d517da2af56b", "occurredOnUtc": "2026-10-04T11:30:18.0495765Z", "dailyRecordingId": "01a106ad-d534-7171-b77e-3df14dc3dd74"}	2026-10-04 18:30:18.049576+07	2026-10-04 18:30:18.067094+07	1	\N
01a106ad-d565-7e34-9b29-81979dcd91a0	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d565-7e34-9b29-81979dcd91a0", "occurredOnUtc": "2026-10-04T11:30:18.0854612Z", "goodsReceiptId": "01a106ad-d565-74c5-97c1-753986941b7f"}	2026-10-04 18:30:18.085461+07	2026-10-04 18:30:18.13492+07	1	\N
01a106ad-d58a-7486-9559-cc276e94d110	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d58a-7486-9559-cc276e94d110", "journalId": "01a106ad-d584-7347-b05d-db9e642044d7", "occurredOnUtc": "2026-10-04T11:30:18.1229721Z"}	2026-10-04 18:30:18.122972+07	2026-10-04 18:30:18.136386+07	1	\N
01a106ad-d5a9-7fe6-a9b2-81e9564a8ddc	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d5a9-7fe6-a9b2-81e9564a8ddc", "occurredOnUtc": "2026-10-04T11:30:18.1538715Z", "goodsReceiptId": "01a106ad-d5a9-715f-82a1-c024dce8f91c"}	2026-10-04 18:30:18.153871+07	2026-10-04 18:30:18.201006+07	1	\N
01a106ad-d5d4-7744-8da8-3b45676b4ba1	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d5d4-7744-8da8-3b45676b4ba1", "journalId": "01a106ad-d5cf-7c8a-b18b-21998dc2722f", "occurredOnUtc": "2026-10-04T11:30:18.196153Z"}	2026-10-04 18:30:18.196153+07	2026-10-04 18:30:18.202146+07	1	\N
01a106ad-d5e8-791d-9071-754e5cc6d372	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d5e8-791d-9071-754e5cc6d372", "occurredOnUtc": "2026-10-04T11:30:18.2164437Z", "dailyRecordingId": "01a106ad-d5db-7b92-96c1-5d808cfd6b93"}	2026-10-04 18:30:18.216443+07	2026-10-04 18:30:18.230954+07	1	\N
01a106ad-d13b-7cf1-badb-cb98386c10f2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d13b-7cf1-badb-cb98386c10f2", "journalId": "01a106ad-d134-7b8a-a418-4e8573fcb910", "occurredOnUtc": "2026-10-04T11:30:17.0194541Z"}	2026-10-04 18:30:17.019454+07	2026-10-04 18:30:17.025121+07	1	\N
01a106ad-d150-7a47-9d7d-5636ebae1305	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d150-7a47-9d7d-5636ebae1305", "occurredOnUtc": "2026-10-04T11:30:17.0408308Z", "goodsReceiptId": "01a106ad-d150-7d46-aad6-09ab900856a2"}	2026-10-04 18:30:17.04083+07	2026-10-04 18:30:17.089232+07	1	\N
01a106ad-d17b-79c1-aea7-8fd62e92d9c5	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d17b-79c1-aea7-8fd62e92d9c5", "journalId": "01a106ad-d174-7585-9b9b-34de98516846", "occurredOnUtc": "2026-10-04T11:30:17.0838156Z"}	2026-10-04 18:30:17.083815+07	2026-10-04 18:30:17.090264+07	1	\N
01a106ad-d1aa-7215-86d0-7e387a9eb3bb	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-d1aa-7215-86d0-7e387a9eb3bb", "occurredOnUtc": "2026-10-04T11:30:17.1304443Z", "cashTransactionId": "01a106ad-d18d-7091-a98b-c880ad4fdb86"}	2026-10-04 18:30:17.130444+07	2026-10-04 18:30:17.160218+07	1	\N
01a106ad-d1c3-7d5d-aa74-5566764e89fe	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d1c3-7d5d-aa74-5566764e89fe", "journalId": "01a106ad-d1bd-7768-b9e6-6a8e03483618", "occurredOnUtc": "2026-10-04T11:30:17.1556151Z"}	2026-10-04 18:30:17.155615+07	2026-10-04 18:30:17.16134+07	1	\N
01a106ad-d1ef-7b63-9fcf-89e3e407907d	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-d1ef-7b63-9fcf-89e3e407907d", "occurredOnUtc": "2026-10-04T11:30:17.1995046Z", "cashTransactionId": "01a106ad-d1d3-78d4-aae6-b53870ff6918"}	2026-10-04 18:30:17.199504+07	2026-10-04 18:30:17.229745+07	1	\N
01a106ad-d208-77e4-881e-8eb5733e7e38	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d208-77e4-881e-8eb5733e7e38", "journalId": "01a106ad-d202-7877-aaab-6b6a94598175", "occurredOnUtc": "2026-10-04T11:30:17.2244518Z"}	2026-10-04 18:30:17.224451+07	2026-10-04 18:30:17.230901+07	1	\N
01a106ad-d21e-7df3-9d00-d1fcd38137aa	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d21e-7df3-9d00-d1fcd38137aa", "occurredOnUtc": "2026-10-04T11:30:17.246004Z", "dailyRecordingId": "01a106ad-d210-7471-89e2-885621735f5a"}	2026-10-04 18:30:17.246004+07	2026-10-04 18:30:17.256747+07	1	\N
01a106ad-d235-704d-b138-62cab8dfd013	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d235-704d-b138-62cab8dfd013", "occurredOnUtc": "2026-10-04T11:30:17.2690569Z", "dailyRecordingId": "01a106ad-d229-72b8-a0cb-13574523872a"}	2026-10-04 18:30:17.269056+07	2026-10-04 18:30:17.284334+07	1	\N
01a106ad-d257-78d7-9e6b-62496234309b	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d257-78d7-9e6b-62496234309b", "occurredOnUtc": "2026-10-04T11:30:17.3033904Z", "goodsReceiptId": "01a106ad-d257-72fc-9fab-d8d93779a1f7"}	2026-10-04 18:30:17.30339+07	2026-10-04 18:30:17.363058+07	1	\N
01a106ad-d277-71ae-b244-d62c90fd6032	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d277-71ae-b244-d62c90fd6032", "journalId": "01a106ad-d271-7801-9fb7-fe26402d86aa", "occurredOnUtc": "2026-10-04T11:30:17.3355095Z"}	2026-10-04 18:30:17.335509+07	2026-10-04 18:30:17.36409+07	1	\N
01a106ad-d28e-7bad-b5b2-618b044b6d97	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d28e-7bad-b5b2-618b044b6d97", "journalId": "01a106ad-d286-7c12-9339-4a95616a2063", "occurredOnUtc": "2026-10-04T11:30:17.3580327Z"}	2026-10-04 18:30:17.358032+07	2026-10-04 18:30:17.364586+07	1	\N
01a106ad-d29f-7f6d-9d81-4b6f29beec4f	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ad-d29f-7f6d-9d81-4b6f29beec4f", "cycleId": "01a106ad-d05a-759c-a6ff-c3e67df7f41b", "occurredOnUtc": "2026-10-04T11:30:17.3752775Z"}	2026-10-04 18:30:17.375277+07	2026-10-04 18:30:17.385847+07	1	\N
01a106ad-d2c3-77c6-8f9b-fab6b91f7fed	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-d2c3-77c6-8f9b-fab6b91f7fed", "occurredOnUtc": "2026-10-04T11:30:17.4113353Z", "stockTransferId": "01a106ad-d2c3-71d7-a28b-b7b7599ed6fc"}	2026-10-04 18:30:17.411335+07	2026-10-04 18:30:17.494722+07	1	\N
01a106ad-d310-7f77-b946-0ef6ff21e1db	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d310-7f77-b946-0ef6ff21e1db", "journalId": "01a106ad-d30a-7c51-88ba-27dd372cb8c9", "occurredOnUtc": "2026-10-04T11:30:17.4889009Z"}	2026-10-04 18:30:17.4889+07	2026-10-04 18:30:17.495761+07	1	\N
01a106ad-d332-7b1c-9ef6-a731a042f5f0	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ad-d332-7b1c-9ef6-a731a042f5f0", "cycleId": "01a106ad-d332-7f38-9c34-260051b68ef3", "occurredOnUtc": "2026-10-04T11:30:17.5226451Z"}	2026-10-04 18:30:17.522645+07	2026-10-04 18:30:17.527989+07	1	\N
01a106ad-d3cb-796d-93ca-8bfbee7b4cd6	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-d3cb-796d-93ca-8bfbee7b4cd6", "occurredOnUtc": "2026-10-04T11:30:17.6756715Z", "cashTransactionId": "01a106ad-d3ab-7b03-88f3-769246490768"}	2026-10-04 18:30:17.675671+07	2026-10-04 18:30:17.705863+07	1	\N
01a106ad-d3e4-7025-ac5c-44db01e61725	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d3e4-7025-ac5c-44db01e61725", "journalId": "01a106ad-d3de-7d4b-9444-bc2080f2a6c0", "occurredOnUtc": "2026-10-04T11:30:17.700493Z"}	2026-10-04 18:30:17.700493+07	2026-10-04 18:30:17.707035+07	1	\N
01a106ad-d411-75c8-a12c-5538dc49b1ff	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-d411-75c8-a12c-5538dc49b1ff", "occurredOnUtc": "2026-10-04T11:30:17.7455785Z", "cashTransactionId": "01a106ad-d3f4-7d2d-bfa8-6086d9babd41"}	2026-10-04 18:30:17.745578+07	2026-10-04 18:30:17.777664+07	1	\N
01a106ad-d42b-7e69-9551-251aef38929d	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d42b-7e69-9551-251aef38929d", "journalId": "01a106ad-d424-7f64-bfc5-e923be8a57b5", "occurredOnUtc": "2026-10-04T11:30:17.7716187Z"}	2026-10-04 18:30:17.771618+07	2026-10-04 18:30:17.778531+07	1	\N
01a106ad-d474-7119-834d-443a1af15945	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d474-7119-834d-443a1af15945", "occurredOnUtc": "2026-10-04T11:30:17.8447965Z", "dailyRecordingId": "01a106ad-d229-72b8-a0cb-13574523872a"}	2026-10-04 18:30:17.844796+07	2026-10-04 18:30:17.886516+07	1	\N
01a106ad-d4cb-7cd1-8a16-7a73f5171392	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d4cb-7cd1-8a16-7a73f5171392", "occurredOnUtc": "2026-10-04T11:30:17.9314819Z", "dailyRecordingId": "01a106ad-d4bf-7b1f-aeba-04d9e885f75d"}	2026-10-04 18:30:17.931481+07	2026-10-04 18:30:17.944001+07	1	\N
01a106ad-d4e9-70b4-abd1-fed1f3455e7d	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-d4e9-70b4-abd1-fed1f3455e7d", "occurredOnUtc": "2026-10-04T11:30:17.9617931Z", "stockTransferId": "01a106ad-d4e9-76d3-825a-71bb220f2d77"}	2026-10-04 18:30:17.961793+07	2026-10-04 18:30:18.005592+07	1	\N
01a106ad-d510-7742-9e3a-9c6c5f3a1a59	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d510-7742-9e3a-9c6c5f3a1a59", "journalId": "01a106ad-d50a-746c-af0f-c4cb7cc4d68b", "occurredOnUtc": "2026-10-04T11:30:18.0008015Z"}	2026-10-04 18:30:18.000801+07	2026-10-04 18:30:18.006547+07	1	\N
01a106ad-d646-71b5-ba04-3a41cf546a00	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-d646-71b5-ba04-3a41cf546a00", "occurredOnUtc": "2026-10-04T11:30:18.3102095Z", "vendorInvoiceId": "01a106ad-d61f-7df9-b45c-7f4646da5272"}	2026-10-04 18:30:18.310209+07	2026-10-04 18:30:18.342975+07	1	\N
01a106ad-d662-70de-b951-b012e266e88a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d662-70de-b951-b012e266e88a", "journalId": "01a106ad-d65b-7a68-9a4d-1898cad3cdd3", "occurredOnUtc": "2026-10-04T11:30:18.3382174Z"}	2026-10-04 18:30:18.338217+07	2026-10-04 18:30:18.343993+07	1	\N
01a106ad-d675-7a3f-83ed-45c2289bcb04	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d675-7a3f-83ed-45c2289bcb04", "occurredOnUtc": "2026-10-04T11:30:18.3575299Z", "dailyRecordingId": "01a106ad-d668-7bd2-ba2f-19feb1499afd"}	2026-10-04 18:30:18.357529+07	2026-10-04 18:30:18.368585+07	1	\N
01a106ad-d68e-7403-8620-dc3d5e5084c8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d68e-7403-8620-dc3d5e5084c8", "occurredOnUtc": "2026-10-04T11:30:18.3827892Z", "dailyRecordingId": "01a106ad-d681-753f-aa82-920dbbe0e044"}	2026-10-04 18:30:18.382789+07	2026-10-04 18:30:18.395136+07	1	\N
01a106ad-d6a8-7e8c-9735-5640ed35d34a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d6a8-7e8c-9735-5640ed35d34a", "occurredOnUtc": "2026-10-04T11:30:18.4086912Z", "dailyRecordingId": "01a106ad-d69b-79ef-9de6-106dbe430481"}	2026-10-04 18:30:18.408691+07	2026-10-04 18:30:18.427653+07	1	\N
01a106ad-d6d0-7a81-b5a8-39a8f380bf4b	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-d6d0-7a81-b5a8-39a8f380bf4b", "occurredOnUtc": "2026-10-04T11:30:18.448207Z", "goodsReceiptId": "01a106ad-d6d0-7981-97bd-a98ead54697d"}	2026-10-04 18:30:18.448207+07	2026-10-04 18:30:18.508508+07	1	\N
01a106ad-d6f3-7d55-8d53-73a31b86ff95	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d6f3-7d55-8d53-73a31b86ff95", "journalId": "01a106ad-d6ed-732b-befa-5bde51400a96", "occurredOnUtc": "2026-10-04T11:30:18.4835652Z"}	2026-10-04 18:30:18.483565+07	2026-10-04 18:30:18.509477+07	1	\N
01a106ad-d708-7850-bbb5-107ad80f7a02	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d708-7850-bbb5-107ad80f7a02", "journalId": "01a106ad-d701-731c-80db-661b09b81633", "occurredOnUtc": "2026-10-04T11:30:18.5040204Z"}	2026-10-04 18:30:18.50402+07	2026-10-04 18:30:18.509714+07	1	\N
01a106ad-d719-70c6-87ba-d4ff96d05ec7	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ad-d719-70c6-87ba-d4ff96d05ec7", "cycleId": "01a106ad-d332-7f38-9c34-260051b68ef3", "occurredOnUtc": "2026-10-04T11:30:18.5216562Z"}	2026-10-04 18:30:18.521656+07	2026-10-04 18:30:18.52993+07	1	\N
01a106ad-d731-7938-ae18-365181b6d0ee	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-d731-7938-ae18-365181b6d0ee", "occurredOnUtc": "2026-10-04T11:30:18.5452334Z", "stockTransferId": "01a106ad-d731-7973-9045-abaa69ca12a9"}	2026-10-04 18:30:18.545233+07	2026-10-04 18:30:18.603105+07	1	\N
01a106ad-d765-7963-9e1d-bf7518cd7500	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d765-7963-9e1d-bf7518cd7500", "journalId": "01a106ad-d75f-753d-bb13-a5e580df1e32", "occurredOnUtc": "2026-10-04T11:30:18.5970569Z"}	2026-10-04 18:30:18.597056+07	2026-10-04 18:30:18.604184+07	1	\N
01a106ad-d7a0-75f0-930c-9f1333258e19	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d7a0-75f0-930c-9f1333258e19", "journalId": "01a106ad-d777-7e7c-9acf-cd10cc9fef9c", "occurredOnUtc": "2026-10-04T11:30:18.6564391Z"}	2026-10-04 18:30:18.656439+07	2026-10-04 18:30:18.660983+07	1	\N
01a106ad-d7d5-7f98-a481-15529edd4173	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d7d5-7f98-a481-15529edd4173", "journalId": "01a106ad-d7ae-763d-a218-153532287670", "occurredOnUtc": "2026-10-04T11:30:18.709589Z"}	2026-10-04 18:30:18.709589+07	2026-10-04 18:30:18.714903+07	1	\N
01a106ad-d7ea-7c2c-a98e-33ca68def5b2	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d7ea-7c2c-a98e-33ca68def5b2", "occurredOnUtc": "2026-10-04T11:30:18.7304941Z", "dailyRecordingId": "01a106ad-d7dc-793b-a59f-7494399fdf3c"}	2026-10-04 18:30:18.730494+07	2026-10-04 18:30:18.742131+07	1	\N
01a106ad-d823-7364-85bc-4b1cdc8bf587	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-d823-7364-85bc-4b1cdc8bf587", "occurredOnUtc": "2026-10-04T11:30:18.7879889Z", "vendorInvoiceId": "01a106ad-d80c-7812-8300-34c0e8e0c918"}	2026-10-04 18:30:18.787988+07	2026-10-04 18:30:18.823841+07	1	\N
01a106ad-d8c0-790c-b193-388fb699d61b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d8c0-790c-b193-388fb699d61b", "occurredOnUtc": "2026-10-04T11:30:18.9446032Z", "dailyRecordingId": "01a106ad-d8b3-7a82-99b1-107355891e58"}	2026-10-04 18:30:18.944603+07	2026-10-04 18:30:18.955595+07	1	\N
01a106ad-d8d8-7dc4-81ea-33e9e5394745	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-d8d8-7dc4-81ea-33e9e5394745", "occurredOnUtc": "2026-10-04T11:30:18.9688963Z", "bankTransferId": "01a106ad-d8d8-7a9c-a216-4b36c299f9fa"}	2026-10-04 18:30:18.968896+07	2026-10-04 18:30:18.996624+07	1	\N
01a106ad-d8ef-7248-954b-dd7b3047b4e6	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d8ef-7248-954b-dd7b3047b4e6", "journalId": "01a106ad-d8ea-7715-a431-24d7e4113148", "occurredOnUtc": "2026-10-04T11:30:18.9919414Z"}	2026-10-04 18:30:18.991941+07	2026-10-04 18:30:18.997876+07	1	\N
01a106ad-d9a8-7ec4-bed0-35f2b0ddd4c0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d9a8-7ec4-bed0-35f2b0ddd4c0", "occurredOnUtc": "2026-10-04T11:30:19.1763552Z", "dailyRecordingId": "01a106ad-d99a-7e7b-854e-21c0e67e5a59"}	2026-10-04 18:30:19.176355+07	2026-10-04 18:30:19.187911+07	1	\N
01a106ad-d9f6-70ed-9932-7665640da000	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-d9f6-70ed-9932-7665640da000", "occurredOnUtc": "2026-10-04T11:30:19.2549105Z", "vendorInvoiceId": "01a106ad-d9df-7964-ab27-9bf8d2096cd4"}	2026-10-04 18:30:19.25491+07	2026-10-04 18:30:19.288648+07	1	\N
01a106ad-da14-72e3-b83c-ee3eaf2ba168	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-da14-72e3-b83c-ee3eaf2ba168", "journalId": "01a106ad-da0b-7e81-a046-ef89e52d5a37", "occurredOnUtc": "2026-10-04T11:30:19.2843065Z"}	2026-10-04 18:30:19.284306+07	2026-10-04 18:30:19.289874+07	1	\N
01a106ad-da27-7770-a952-4de29b26d4e8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-da27-7770-a952-4de29b26d4e8", "occurredOnUtc": "2026-10-04T11:30:19.3038687Z", "dailyRecordingId": "01a106ad-da1a-71a5-be59-96a512736a24"}	2026-10-04 18:30:19.303868+07	2026-10-04 18:30:19.314274+07	1	\N
01a106ad-dac3-7b66-9328-e62c7a6876b6	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-dac3-7b66-9328-e62c7a6876b6", "occurredOnUtc": "2026-10-04T11:30:19.4590981Z", "dailyRecordingId": "01a106ad-dab6-75b9-ba82-58926ae9de49"}	2026-10-04 18:30:19.459098+07	2026-10-04 18:30:19.487347+07	1	\N
01a106ad-daee-76d7-82fc-d3001644fd05	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-daee-76d7-82fc-d3001644fd05", "occurredOnUtc": "2026-10-04T11:30:19.5027193Z", "dailyRecordingId": "01a106ad-dae0-7c16-b9c7-e90c1053e113"}	2026-10-04 18:30:19.502719+07	2026-10-04 18:30:19.518037+07	1	\N
01a106ad-db2b-7d27-8449-246ffe515e33	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-db2b-7d27-8449-246ffe515e33", "occurredOnUtc": "2026-10-04T11:30:19.5637022Z", "dailyRecordingId": "01a106ad-db1e-72cc-9abe-d8e672245066"}	2026-10-04 18:30:19.563702+07	2026-10-04 18:30:19.574819+07	1	\N
01a106ad-db7d-79d1-8c2d-b820348dc2a5	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-db7d-79d1-8c2d-b820348dc2a5", "occurredOnUtc": "2026-10-04T11:30:19.645685Z", "vendorInvoiceId": "01a106ad-db66-79e0-9dbe-e4441b97acf9"}	2026-10-04 18:30:19.645685+07	2026-10-04 18:30:19.677407+07	1	\N
01a106ad-db98-7ea1-9ed1-2f5838de22dd	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-db98-7ea1-9ed1-2f5838de22dd", "journalId": "01a106ad-db91-7331-987a-0870812b7792", "occurredOnUtc": "2026-10-04T11:30:19.6726474Z"}	2026-10-04 18:30:19.672647+07	2026-10-04 18:30:19.678302+07	1	\N
01a106ad-dbc7-7b91-98d8-1951bc6d361e	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-dbc7-7b91-98d8-1951bc6d361e", "occurredOnUtc": "2026-10-04T11:30:19.7198109Z", "vendorInvoiceId": "01a106ad-dbb0-72f2-be66-2505a4e57fc4"}	2026-10-04 18:30:19.71981+07	2026-10-04 18:30:19.749403+07	1	\N
01a106ad-dbe0-7e8a-8def-23aaf1cc8da7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-dbe0-7e8a-8def-23aaf1cc8da7", "journalId": "01a106ad-dbda-766d-8b9a-322bb4ad4f94", "occurredOnUtc": "2026-10-04T11:30:19.7444483Z"}	2026-10-04 18:30:19.744448+07	2026-10-04 18:30:19.750507+07	1	\N
01a106ad-dbf3-71e2-992c-2b6506f56e88	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-dbf3-71e2-992c-2b6506f56e88", "occurredOnUtc": "2026-10-04T11:30:19.7638686Z", "dailyRecordingId": "01a106ad-dbe7-7177-aac8-6decd4265310"}	2026-10-04 18:30:19.763868+07	2026-10-04 18:30:19.7767+07	1	\N
01a106ad-de55-74c6-8f81-08f15c597e59	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-de55-74c6-8f81-08f15c597e59", "occurredOnUtc": "2026-10-04T11:30:20.3739003Z", "dailyRecordingId": "01a106ad-de49-7b55-9483-061ba2ed0676"}	2026-10-04 18:30:20.3739+07	2026-10-04 18:30:20.38489+07	1	\N
01a106ad-d842-7f06-b9b0-a0b0b55a89f1	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d842-7f06-b9b0-a0b0b55a89f1", "journalId": "01a106ad-d83b-76d3-b51b-7acf5de4f84c", "occurredOnUtc": "2026-10-04T11:30:18.8186473Z"}	2026-10-04 18:30:18.818647+07	2026-10-04 18:30:18.824795+07	1	\N
01a106ad-d876-779c-b174-7ac50ec73777	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-d876-779c-b174-7ac50ec73777", "occurredOnUtc": "2026-10-04T11:30:18.8706526Z", "vendorInvoiceId": "01a106ad-d85e-7c5f-af62-abde8aa20340"}	2026-10-04 18:30:18.870652+07	2026-10-04 18:30:18.905143+07	1	\N
01a106ad-d894-74c6-abfd-b7dc51d99892	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d894-74c6-abfd-b7dc51d99892", "journalId": "01a106ad-d88d-7f93-9c71-8e78e7d21810", "occurredOnUtc": "2026-10-04T11:30:18.9002587Z"}	2026-10-04 18:30:18.900258+07	2026-10-04 18:30:18.906182+07	1	\N
01a106ad-d8a7-7c64-a3a4-91f7f2fb6e8d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d8a7-7c64-a3a4-91f7f2fb6e8d", "occurredOnUtc": "2026-10-04T11:30:18.9194389Z", "dailyRecordingId": "01a106ad-d89a-7ccb-93bc-e0c4f3eb6802"}	2026-10-04 18:30:18.919438+07	2026-10-04 18:30:18.930446+07	1	\N
01a106ad-d900-7ea0-b3c2-13afb2de6977	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-d900-7ea0-b3c2-13afb2de6977", "occurredOnUtc": "2026-10-04T11:30:19.008468Z", "bankTransferId": "01a106ad-d900-70b5-b8dd-f66ed931a827"}	2026-10-04 18:30:19.008468+07	2026-10-04 18:30:19.065525+07	1	\N
01a106ad-d932-739d-aec6-b5f3bc5cb5b7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-d932-739d-aec6-b5f3bc5cb5b7", "journalId": "01a106ad-d928-77f6-9154-02389f15388b", "occurredOnUtc": "2026-10-04T11:30:19.058194Z"}	2026-10-04 18:30:19.058194+07	2026-10-04 18:30:19.067063+07	1	\N
01a106ad-d951-7c52-80b2-026fef255b16	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d951-7c52-80b2-026fef255b16", "occurredOnUtc": "2026-10-04T11:30:19.0892599Z", "dailyRecordingId": "01a106ad-d93b-7666-b912-3aa7ef54a2f7"}	2026-10-04 18:30:19.089259+07	2026-10-04 18:30:19.103459+07	1	\N
01a106ad-d96d-79a0-9a4c-9f72ed1f6368	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d96d-79a0-9a4c-9f72ed1f6368", "occurredOnUtc": "2026-10-04T11:30:19.1172248Z", "dailyRecordingId": "01a106ad-d960-7bc9-86c4-568869e29b05"}	2026-10-04 18:30:19.117224+07	2026-10-04 18:30:19.13229+07	1	\N
01a106ad-d98a-727b-a5eb-53e9200ead89	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d98a-727b-a5eb-53e9200ead89", "occurredOnUtc": "2026-10-04T11:30:19.1462157Z", "dailyRecordingId": "01a106ad-d97d-7253-a71c-fccdbc2b6b1d"}	2026-10-04 18:30:19.146215+07	2026-10-04 18:30:19.160938+07	1	\N
01a106ad-d9c1-7ca3-a15d-1e080658ec6e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-d9c1-7ca3-a15d-1e080658ec6e", "occurredOnUtc": "2026-10-04T11:30:19.2012644Z", "dailyRecordingId": "01a106ad-d9b4-7094-a2dc-0318f02069f9"}	2026-10-04 18:30:19.201264+07	2026-10-04 18:30:19.211751+07	1	\N
01a106ad-da57-78cd-b159-4f196ea3c317	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-da57-78cd-b159-4f196ea3c317", "occurredOnUtc": "2026-10-04T11:30:19.3513704Z", "cashTransactionId": "01a106ad-da3a-7a71-b08b-d7964e6d8232"}	2026-10-04 18:30:19.35137+07	2026-10-04 18:30:19.381196+07	1	\N
01a106ad-da6f-7f17-ba7d-dd7a89b30f82	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-da6f-7f17-ba7d-dd7a89b30f82", "journalId": "01a106ad-da69-7b3e-80d7-3e5bf2ddf804", "occurredOnUtc": "2026-10-04T11:30:19.3759526Z"}	2026-10-04 18:30:19.375952+07	2026-10-04 18:30:19.382587+07	1	\N
01a106ad-da9a-741c-9094-b92bc5eff7c3	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-da9a-741c-9094-b92bc5eff7c3", "occurredOnUtc": "2026-10-04T11:30:19.4180911Z", "cashTransactionId": "01a106ad-da7f-7691-b9e0-35543cc0403f"}	2026-10-04 18:30:19.418091+07	2026-10-04 18:30:19.444797+07	1	\N
01a106ad-dab0-719b-a6d8-0b24eaf2fcc9	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-dab0-719b-a6d8-0b24eaf2fcc9", "journalId": "01a106ad-daab-792a-b629-7ce566e79605", "occurredOnUtc": "2026-10-04T11:30:19.4403621Z"}	2026-10-04 18:30:19.440362+07	2026-10-04 18:30:19.445721+07	1	\N
01a106ad-db0b-7155-8c1e-2e3ef71fa5ba	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-db0b-7155-8c1e-2e3ef71fa5ba", "occurredOnUtc": "2026-10-04T11:30:19.5318382Z", "dailyRecordingId": "01a106ad-daff-7358-b859-682e3fe7ac5c"}	2026-10-04 18:30:19.531838+07	2026-10-04 18:30:19.549608+07	1	\N
01a106ad-db45-7300-bc7b-af4b2b7504c1	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-db45-7300-bc7b-af4b2b7504c1", "occurredOnUtc": "2026-10-04T11:30:19.5896298Z", "dailyRecordingId": "01a106ad-db37-7a15-bf21-c18e8dcb0718"}	2026-10-04 18:30:19.589629+07	2026-10-04 18:30:19.601007+07	1	\N
01a106ad-dd17-7d04-b43c-ef8c569cc1fd	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-dd17-7d04-b43c-ef8c569cc1fd", "occurredOnUtc": "2026-10-04T11:30:20.0555571Z", "paymentVoucherId": "01a106ad-dc76-7acc-9172-4993fcd6d732"}	2026-10-04 18:30:20.055557+07	2026-10-04 18:30:20.120582+07	1	\N
01a106ad-dd54-7aec-911c-8f7210d4d62e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-dd54-7aec-911c-8f7210d4d62e", "journalId": "01a106ad-dd4e-787f-bc44-64aa3fc3cee3", "occurredOnUtc": "2026-10-04T11:30:20.116481Z"}	2026-10-04 18:30:20.116481+07	2026-10-04 18:30:20.121636+07	1	\N
01a106ad-dd8a-72d7-b313-8eb5d0096295	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-dd8a-72d7-b313-8eb5d0096295", "occurredOnUtc": "2026-10-04T11:30:20.1705958Z", "paymentVoucherId": "01a106ad-dd6a-7585-a741-9eb19ff1427f"}	2026-10-04 18:30:20.170595+07	2026-10-04 18:30:20.200332+07	1	\N
01a106ad-dda4-7357-a710-986ee468b2a1	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-dda4-7357-a710-986ee468b2a1", "journalId": "01a106ad-dd9f-72e9-9b23-7b93d3324a31", "occurredOnUtc": "2026-10-04T11:30:20.1963986Z"}	2026-10-04 18:30:20.196398+07	2026-10-04 18:30:20.201275+07	1	\N
01a106ad-ddb5-7738-9d45-161f0db31e3a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ddb5-7738-9d45-161f0db31e3a", "occurredOnUtc": "2026-10-04T11:30:20.2131811Z", "dailyRecordingId": "01a106ad-ddaa-793c-a1dd-3c8cc15e60eb"}	2026-10-04 18:30:20.213181+07	2026-10-04 18:30:20.225229+07	1	\N
01a106ad-ddd1-74e2-9212-6ab1e67b21d0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ddd1-74e2-9212-6ab1e67b21d0", "occurredOnUtc": "2026-10-04T11:30:20.2417179Z", "dailyRecordingId": "01a106ad-ddc2-7cc4-b0e4-774809cba35b"}	2026-10-04 18:30:20.241717+07	2026-10-04 18:30:20.257527+07	1	\N
01a106ad-ddef-726b-9fc0-7141cf4f9957	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ddef-726b-9fc0-7141cf4f9957", "occurredOnUtc": "2026-10-04T11:30:20.2713211Z", "dailyRecordingId": "01a106ad-dde2-718d-a538-f59490e33d84"}	2026-10-04 18:30:20.271321+07	2026-10-04 18:30:20.284304+07	1	\N
01a106ad-de08-7094-8adf-0c8a59f7927d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-de08-7094-8adf-0c8a59f7927d", "occurredOnUtc": "2026-10-04T11:30:20.2968027Z", "dailyRecordingId": "01a106ad-ddfd-7461-9b07-1e06cbfdb108"}	2026-10-04 18:30:20.296802+07	2026-10-04 18:30:20.309216+07	1	\N
01a106ad-de23-7071-bfb9-631ad3107d08	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-de23-7071-bfb9-631ad3107d08", "occurredOnUtc": "2026-10-04T11:30:20.323805Z", "dailyRecordingId": "01a106ad-de16-7ea8-95b8-0fe918dd6b9c"}	2026-10-04 18:30:20.323805+07	2026-10-04 18:30:20.334334+07	1	\N
01a106ad-de3c-7832-8e8d-dfa20f6e42f9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-de3c-7832-8e8d-dfa20f6e42f9", "occurredOnUtc": "2026-10-04T11:30:20.3482716Z", "dailyRecordingId": "01a106ad-de2f-70ec-b894-ec4a5db80950"}	2026-10-04 18:30:20.348271+07	2026-10-04 18:30:20.360953+07	1	\N
01a106ad-de98-73b0-8612-b7c0332a0415	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-de98-73b0-8612-b7c0332a0415", "journalId": "01a106ad-de91-7779-8853-d49a6028029f", "occurredOnUtc": "2026-10-04T11:30:20.4401401Z"}	2026-10-04 18:30:20.44014+07	2026-10-04 18:30:20.445202+07	1	\N
01a106ad-dedf-72ca-8233-250da0fc621c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-dedf-72ca-8233-250da0fc621c", "occurredOnUtc": "2026-10-04T11:30:20.5118656Z", "dailyRecordingId": "01a106ad-ded3-7406-af79-a4c2522ec215"}	2026-10-04 18:30:20.511865+07	2026-10-04 18:30:20.523881+07	1	\N
01a106ad-df0f-77f4-9953-54e27f30b9e0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-df0f-77f4-9953-54e27f30b9e0", "occurredOnUtc": "2026-10-04T11:30:20.5597238Z", "dailyRecordingId": "01a106ad-df03-722c-87f7-1998f097527d"}	2026-10-04 18:30:20.559723+07	2026-10-04 18:30:20.569881+07	1	\N
01a106ad-df3c-79fe-a6a8-d81feef35120	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-df3c-79fe-a6a8-d81feef35120", "occurredOnUtc": "2026-10-04T11:30:20.6043509Z", "dailyRecordingId": "01a106ad-df30-7706-b1cc-d066ae3d2ea6"}	2026-10-04 18:30:20.60435+07	2026-10-04 18:30:20.615215+07	1	\N
01a106ad-df54-76f1-8a93-93101d780ec3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-df54-76f1-8a93-93101d780ec3", "occurredOnUtc": "2026-10-04T11:30:20.6286492Z", "dailyRecordingId": "01a106ad-df48-7f6d-8c4e-ed4151ca4050"}	2026-10-04 18:30:20.628649+07	2026-10-04 18:30:20.641556+07	1	\N
01a106ad-e0b5-7c6d-a211-206e71306635	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e0b5-7c6d-a211-206e71306635", "occurredOnUtc": "2026-10-04T11:30:20.9815591Z", "dailyRecordingId": "01a106ad-e0a9-739d-a7d5-7ffdf7944ed8"}	2026-10-04 18:30:20.981559+07	2026-10-04 18:30:20.992099+07	1	\N
01a106ad-e0fb-7d52-9a35-57fb4eb74925	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e0fb-7d52-9a35-57fb4eb74925", "occurredOnUtc": "2026-10-04T11:30:21.0511236Z", "dailyRecordingId": "01a106ad-e0ee-7d65-845c-fd49fb8c5aee"}	2026-10-04 18:30:21.051123+07	2026-10-04 18:30:21.061725+07	1	\N
01a106ad-e162-7f0c-b875-1ae5aad8a53b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e162-7f0c-b875-1ae5aad8a53b", "occurredOnUtc": "2026-10-04T11:30:21.1545506Z", "dailyRecordingId": "01a106ad-e156-7fe8-a5d0-13f30df81435"}	2026-10-04 18:30:21.15455+07	2026-10-04 18:30:21.16781+07	1	\N
01a106ad-e191-7197-b366-d209267f83bf	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e191-7197-b366-d209267f83bf", "occurredOnUtc": "2026-10-04T11:30:21.2014527Z", "dailyRecordingId": "01a106ad-e186-7c90-9e1a-ebc0165bfbf3"}	2026-10-04 18:30:21.201452+07	2026-10-04 18:30:21.211826+07	1	\N
01a106ad-e1be-7b73-832c-33f7f1d4c0d7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e1be-7b73-832c-33f7f1d4c0d7", "occurredOnUtc": "2026-10-04T11:30:21.2469312Z", "dailyRecordingId": "01a106ad-e1b4-7f1e-9560-274181fe5d35"}	2026-10-04 18:30:21.246931+07	2026-10-04 18:30:21.259839+07	1	\N
01a106ad-e1db-7334-8316-c3e33e87aa01	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e1db-7334-8316-c3e33e87aa01", "occurredOnUtc": "2026-10-04T11:30:21.2758104Z", "dailyRecordingId": "01a106ad-e1cc-7eb2-b0e9-13d64e1f9436"}	2026-10-04 18:30:21.27581+07	2026-10-04 18:30:21.290303+07	1	\N
01a106ad-e219-724e-8028-c16a2072e037	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e219-724e-8028-c16a2072e037", "occurredOnUtc": "2026-10-04T11:30:21.3377789Z", "dailyRecordingId": "01a106ad-e20d-7232-94bc-fcc00d40190d"}	2026-10-04 18:30:21.337778+07	2026-10-04 18:30:21.347408+07	1	\N
01a106ad-e24f-7e30-8cc2-ca8fd4834264	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e24f-7e30-8cc2-ca8fd4834264", "occurredOnUtc": "2026-10-04T11:30:21.3917993Z", "dailyRecordingId": "01a106ad-e23b-7dbe-a0a4-91826cf607c3"}	2026-10-04 18:30:21.391799+07	2026-10-04 18:30:21.411252+07	1	\N
01a106ad-e28f-7030-93ee-acd31b044792	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e28f-7030-93ee-acd31b044792", "occurredOnUtc": "2026-10-04T11:30:21.455125Z", "cashTransactionId": "01a106ad-e273-76e4-8b49-3b21706558d4"}	2026-10-04 18:30:21.455125+07	2026-10-04 18:30:21.487904+07	1	\N
01a106ad-e2aa-7593-9f72-205288781106	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e2aa-7593-9f72-205288781106", "journalId": "01a106ad-e2a3-7ee7-833d-cf92f7fda271", "occurredOnUtc": "2026-10-04T11:30:21.4828641Z"}	2026-10-04 18:30:21.482864+07	2026-10-04 18:30:21.488917+07	1	\N
01a106ad-e345-7c83-85b2-96e61ebe982f	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-e345-7c83-85b2-96e61ebe982f", "occurredOnUtc": "2026-10-04T11:30:21.6377425Z", "paymentVoucherId": "01a106ad-e322-7e5f-ad9e-f18ee24956cf"}	2026-10-04 18:30:21.637742+07	2026-10-04 18:30:21.672866+07	1	\N
01a106ad-e364-70ce-a853-6f5f5251a3d2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e364-70ce-a853-6f5f5251a3d2", "journalId": "01a106ad-e35d-7eee-a33c-b507e3eb5270", "occurredOnUtc": "2026-10-04T11:30:21.6684637Z"}	2026-10-04 18:30:21.668463+07	2026-10-04 18:30:21.673722+07	1	\N
01a106ad-e399-76bb-95f8-174e25f95b24	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-e399-76bb-95f8-174e25f95b24", "occurredOnUtc": "2026-10-04T11:30:21.7211979Z", "paymentVoucherId": "01a106ad-e37a-7d67-9ca9-d476a5ba9b61"}	2026-10-04 18:30:21.721197+07	2026-10-04 18:30:21.754407+07	1	\N
01a106ad-e3b5-7ba6-82af-6e330e64edef	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e3b5-7ba6-82af-6e330e64edef", "journalId": "01a106ad-e3af-70c9-8886-b9da7f060c32", "occurredOnUtc": "2026-10-04T11:30:21.7499483Z"}	2026-10-04 18:30:21.749948+07	2026-10-04 18:30:21.755396+07	1	\N
01a106ad-e3e9-72af-87f6-69bd4a5267e6	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-e3e9-72af-87f6-69bd4a5267e6", "occurredOnUtc": "2026-10-04T11:30:21.8016378Z", "paymentVoucherId": "01a106ad-e3cb-7993-9fb3-1074d78c9ccd"}	2026-10-04 18:30:21.801637+07	2026-10-04 18:30:21.83401+07	1	\N
01a106ad-e404-7a45-8b15-036fe8be4cc7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e404-7a45-8b15-036fe8be4cc7", "journalId": "01a106ad-e3ff-7f44-b70c-d95bbc3d9d0f", "occurredOnUtc": "2026-10-04T11:30:21.8284064Z"}	2026-10-04 18:30:21.828406+07	2026-10-04 18:30:21.835238+07	1	\N
01a106ad-e418-790c-8e68-641a3296ac87	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e418-790c-8e68-641a3296ac87", "occurredOnUtc": "2026-10-04T11:30:21.8486794Z", "dailyRecordingId": "01a106ad-e40c-73c3-b6b5-04b13e2ea61d"}	2026-10-04 18:30:21.848679+07	2026-10-04 18:30:21.8607+07	1	\N
01a106ad-e54b-7161-aed3-c7e62009f3d0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e54b-7161-aed3-c7e62009f3d0", "occurredOnUtc": "2026-10-04T11:30:22.1554311Z", "dailyRecordingId": "01a106ad-e53f-760c-9de6-c4c97b670251"}	2026-10-04 18:30:22.155431+07	2026-10-04 18:30:22.165486+07	1	\N
01a106ad-e561-7c37-8307-d05a305fd312	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e561-7c37-8307-d05a305fd312", "occurredOnUtc": "2026-10-04T11:30:22.1773364Z", "dailyRecordingId": "01a106ad-e556-7750-af63-982615c773a3"}	2026-10-04 18:30:22.177336+07	2026-10-04 18:30:22.187946+07	1	\N
01a106ad-e577-7fe5-97cc-0502cf7c5322	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e577-7fe5-97cc-0502cf7c5322", "occurredOnUtc": "2026-10-04T11:30:22.199868Z", "dailyRecordingId": "01a106ad-e56d-7a82-87bc-a60b421b2dcc"}	2026-10-04 18:30:22.199868+07	2026-10-04 18:30:22.209546+07	1	\N
01a106ad-e5a5-7d13-aba5-9f1752656dab	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e5a5-7d13-aba5-9f1752656dab", "occurredOnUtc": "2026-10-04T11:30:22.2451274Z", "cashTransactionId": "01a106ad-e58a-77b6-ad07-77c957398613"}	2026-10-04 18:30:22.245127+07	2026-10-04 18:30:22.27357+07	1	\N
01a106ad-de6e-76b1-8236-64fae0f17608	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-de6e-76b1-8236-64fae0f17608", "occurredOnUtc": "2026-10-04T11:30:20.3986038Z", "stockTransferId": "01a106ad-de6e-7454-87f3-422062f82097"}	2026-10-04 18:30:20.398603+07	2026-10-04 18:30:20.444235+07	1	\N
01a106ad-dea9-74c4-89f5-c6b7d7063cfe	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-dea9-74c4-89f5-c6b7d7063cfe", "occurredOnUtc": "2026-10-04T11:30:20.4574708Z", "dailyRecordingId": "01a106ad-de9d-7045-bae7-5ff5c4901e79"}	2026-10-04 18:30:20.45747+07	2026-10-04 18:30:20.470964+07	1	\N
01a106ad-dec3-72b1-8202-cd7bfc47b852	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-dec3-72b1-8202-cd7bfc47b852", "occurredOnUtc": "2026-10-04T11:30:20.4834009Z", "dailyRecordingId": "01a106ad-deb7-7b1b-84dd-c55f9a2e5486"}	2026-10-04 18:30:20.4834+07	2026-10-04 18:30:20.498456+07	1	\N
01a106ad-def8-7471-9006-03b6d2e56fb4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-def8-7471-9006-03b6d2e56fb4", "occurredOnUtc": "2026-10-04T11:30:20.5365873Z", "dailyRecordingId": "01a106ad-deec-7768-8789-357dffb98706"}	2026-10-04 18:30:20.536587+07	2026-10-04 18:30:20.546167+07	1	\N
01a106ad-df25-7873-be58-a690471e2dfe	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-df25-7873-be58-a690471e2dfe", "occurredOnUtc": "2026-10-04T11:30:20.5811977Z", "dailyRecordingId": "01a106ad-df1a-7883-9ff4-b0e5088b610f"}	2026-10-04 18:30:20.581197+07	2026-10-04 18:30:20.591259+07	1	\N
01a106ad-df8e-7110-9027-b758cc328005	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-df8e-7110-9027-b758cc328005", "occurredOnUtc": "2026-10-04T11:30:20.6864009Z", "cashTransactionId": "01a106ad-df6a-7011-920b-f71238fea0ee"}	2026-10-04 18:30:20.6864+07	2026-10-04 18:30:20.734432+07	1	\N
01a106ad-dfb8-7aad-89a9-b2c14db730b2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-dfb8-7aad-89a9-b2c14db730b2", "journalId": "01a106ad-dfb1-7711-8e75-18a3275ebc9d", "occurredOnUtc": "2026-10-04T11:30:20.7286737Z"}	2026-10-04 18:30:20.728673+07	2026-10-04 18:30:20.737266+07	1	\N
01a106ad-dfeb-7026-9797-6d382f9d9859	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-dfeb-7026-9797-6d382f9d9859", "occurredOnUtc": "2026-10-04T11:30:20.7792766Z", "cashTransactionId": "01a106ad-dfcd-7c13-841f-3015bbd11511"}	2026-10-04 18:30:20.779276+07	2026-10-04 18:30:20.808507+07	1	\N
01a106ad-e003-76b9-8856-45438dda77c0	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e003-76b9-8856-45438dda77c0", "journalId": "01a106ad-dffc-77e2-a82a-61bc4ab41938", "occurredOnUtc": "2026-10-04T11:30:20.8038571Z"}	2026-10-04 18:30:20.803857+07	2026-10-04 18:30:20.809537+07	1	\N
01a106ad-e02d-794a-815a-94621156578a	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e02d-794a-815a-94621156578a", "occurredOnUtc": "2026-10-04T11:30:20.8450588Z", "cashTransactionId": "01a106ad-e012-7de5-af97-7692f235abe7"}	2026-10-04 18:30:20.845058+07	2026-10-04 18:30:20.872341+07	1	\N
01a106ad-e044-7565-9569-2a997137d6fc	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e044-7565-9569-2a997137d6fc", "journalId": "01a106ad-e03e-7c20-be29-a4ecc44c959a", "occurredOnUtc": "2026-10-04T11:30:20.8684888Z"}	2026-10-04 18:30:20.868488+07	2026-10-04 18:30:20.873238+07	1	\N
01a106ad-e06c-7dc5-aaed-bb17eb84c811	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e06c-7dc5-aaed-bb17eb84c811", "occurredOnUtc": "2026-10-04T11:30:20.9081114Z", "cashTransactionId": "01a106ad-e051-7f9d-865d-4df514262b7d"}	2026-10-04 18:30:20.908111+07	2026-10-04 18:30:20.935168+07	1	\N
01a106ad-e081-7a1c-90e5-d7f456989660	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e081-7a1c-90e5-d7f456989660", "journalId": "01a106ad-e07c-7514-8a8c-2b16d440f03c", "occurredOnUtc": "2026-10-04T11:30:20.9299506Z"}	2026-10-04 18:30:20.92995+07	2026-10-04 18:30:20.936408+07	1	\N
01a106ad-e095-7a62-a516-2b1cef58bbc8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e095-7a62-a516-2b1cef58bbc8", "occurredOnUtc": "2026-10-04T11:30:20.9498164Z", "dailyRecordingId": "01a106ad-e089-7a12-aea8-7f44cc0d14c4"}	2026-10-04 18:30:20.949816+07	2026-10-04 18:30:20.968608+07	1	\N
01a106ad-e0cd-7ac0-a7a6-67166962a90a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e0cd-7ac0-a7a6-67166962a90a", "occurredOnUtc": "2026-10-04T11:30:21.0057685Z", "dailyRecordingId": "01a106ad-e0c1-7c9f-b9d6-0b398cc41cf2"}	2026-10-04 18:30:21.005768+07	2026-10-04 18:30:21.037877+07	1	\N
01a106ad-e112-7f63-9f8f-c75c4de8d5fc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e112-7f63-9f8f-c75c4de8d5fc", "occurredOnUtc": "2026-10-04T11:30:21.0742193Z", "dailyRecordingId": "01a106ad-e106-71a3-9216-7abf48ba6b83"}	2026-10-04 18:30:21.074219+07	2026-10-04 18:30:21.084561+07	1	\N
01a106ad-e12c-7853-94b3-5bb2da627341	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-e12c-7853-94b3-5bb2da627341", "occurredOnUtc": "2026-10-04T11:30:21.1004327Z", "stockTransferId": "01a106ad-e12c-7fed-8626-ef11d1a1aa64"}	2026-10-04 18:30:21.100432+07	2026-10-04 18:30:21.141004+07	1	\N
01a106ad-e150-758f-84ad-688bdb75a3c8	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e150-758f-84ad-688bdb75a3c8", "journalId": "01a106ad-e148-7561-9558-8bb74205c02f", "occurredOnUtc": "2026-10-04T11:30:21.136502Z"}	2026-10-04 18:30:21.136502+07	2026-10-04 18:30:21.142023+07	1	\N
01a106ad-e17b-7b73-acb4-aa20ddbdcc71	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e17b-7b73-acb4-aa20ddbdcc71", "occurredOnUtc": "2026-10-04T11:30:21.1795307Z", "dailyRecordingId": "01a106ad-e170-7fea-bb81-240761e9556b"}	2026-10-04 18:30:21.17953+07	2026-10-04 18:30:21.18947+07	1	\N
01a106ad-e1a9-7073-8b11-052a41966c06	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e1a9-7073-8b11-052a41966c06", "occurredOnUtc": "2026-10-04T11:30:21.2250183Z", "dailyRecordingId": "01a106ad-e19c-7a9b-827a-5ed1103ae12d"}	2026-10-04 18:30:21.225018+07	2026-10-04 18:30:21.234741+07	1	\N
01a106ad-e1fa-7c86-9b0b-8e9c488662d5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e1fa-7c86-9b0b-8e9c488662d5", "occurredOnUtc": "2026-10-04T11:30:21.3062041Z", "dailyRecordingId": "01a106ad-e1eb-7783-ba8c-2f9734451ab4"}	2026-10-04 18:30:21.306204+07	2026-10-04 18:30:21.324598+07	1	\N
01a106ad-e230-75d8-9a36-7f6abfa6c7f9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e230-75d8-9a36-7f6abfa6c7f9", "occurredOnUtc": "2026-10-04T11:30:21.3603705Z", "dailyRecordingId": "01a106ad-e224-78c6-a952-812cec87f27c"}	2026-10-04 18:30:21.36037+07	2026-10-04 18:30:21.370993+07	1	\N
01a106ad-e2ce-7b28-8b15-63df1b8280e2	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e2ce-7b28-8b15-63df1b8280e2", "occurredOnUtc": "2026-10-04T11:30:21.5184728Z", "cashTransactionId": "01a106ad-e2b9-754f-b649-3199ae737ee4"}	2026-10-04 18:30:21.518472+07	2026-10-04 18:30:21.552669+07	1	\N
01a106ad-e2ea-7c2d-b067-02874ddcd90a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e2ea-7c2d-b067-02874ddcd90a", "journalId": "01a106ad-e2e4-763d-af07-f1be49771ce4", "occurredOnUtc": "2026-10-04T11:30:21.5466712Z"}	2026-10-04 18:30:21.546671+07	2026-10-04 18:30:21.554125+07	1	\N
01a106ad-e2ff-7ebd-b653-4fd4929ef2d6	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e2ff-7ebd-b653-4fd4929ef2d6", "occurredOnUtc": "2026-10-04T11:30:21.5673855Z", "dailyRecordingId": "01a106ad-e2f2-7cc5-9ec1-12b227be53fc"}	2026-10-04 18:30:21.567385+07	2026-10-04 18:30:21.579312+07	1	\N
01a106ad-e432-706d-9dfa-eb1518b7dfbe	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e432-706d-9dfa-eb1518b7dfbe", "occurredOnUtc": "2026-10-04T11:30:21.8742764Z", "dailyRecordingId": "01a106ad-e425-79d1-8235-5ab8ed4b57a6"}	2026-10-04 18:30:21.874276+07	2026-10-04 18:30:21.884928+07	1	\N
01a106ad-e5bc-73fd-9287-2ebdad72dd22	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e5bc-73fd-9287-2ebdad72dd22", "journalId": "01a106ad-e5b5-71b0-8d19-cc5f390ba878", "occurredOnUtc": "2026-10-04T11:30:22.268089Z"}	2026-10-04 18:30:22.268089+07	2026-10-04 18:30:22.274864+07	1	\N
01a106ad-e600-738d-a4f4-e793c6e69feb	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-e600-738d-a4f4-e793c6e69feb", "occurredOnUtc": "2026-10-04T11:30:22.3366207Z", "cashTransactionId": "01a106ad-e5cb-7c39-bbcc-05161ef91f86"}	2026-10-04 18:30:22.33662+07	2026-10-04 18:30:22.378198+07	1	\N
01a106ad-e624-7fe0-a034-7393f5e3be73	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e624-7fe0-a034-7393f5e3be73", "journalId": "01a106ad-e61c-7f90-bd0b-ca8a888fbf7a", "occurredOnUtc": "2026-10-04T11:30:22.3720859Z"}	2026-10-04 18:30:22.372085+07	2026-10-04 18:30:22.379199+07	1	\N
01a106ad-e639-7bdd-a765-519802dcef95	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e639-7bdd-a765-519802dcef95", "occurredOnUtc": "2026-10-04T11:30:22.3937221Z", "dailyRecordingId": "01a106ad-e62b-7087-b227-8793ecf528fb"}	2026-10-04 18:30:22.393722+07	2026-10-04 18:30:22.404925+07	1	\N
01a106ad-e654-770a-9048-fe7ff1cbcdcf	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e654-770a-9048-fe7ff1cbcdcf", "occurredOnUtc": "2026-10-04T11:30:22.4201272Z", "dailyRecordingId": "01a106ad-e646-7b54-aeb1-696da1189bb7"}	2026-10-04 18:30:22.420127+07	2026-10-04 18:30:22.433543+07	1	\N
01a106ad-e671-7954-9b71-0f12a1828259	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e671-7954-9b71-0f12a1828259", "occurredOnUtc": "2026-10-04T11:30:22.4499102Z", "dailyRecordingId": "01a106ad-e662-7e9b-85f2-527be0a0c88c"}	2026-10-04 18:30:22.44991+07	2026-10-04 18:30:22.466722+07	1	\N
01a106ad-e691-718f-94ec-9df9f8fbe334	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e691-718f-94ec-9df9f8fbe334", "occurredOnUtc": "2026-10-04T11:30:22.4817314Z", "dailyRecordingId": "01a106ad-e683-7cf3-a6bd-7cad8559e7f7"}	2026-10-04 18:30:22.481731+07	2026-10-04 18:30:22.494692+07	1	\N
01a106ad-e790-7bb3-94af-852db4bd0c80	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e790-7bb3-94af-852db4bd0c80", "occurredOnUtc": "2026-10-04T11:30:22.7364982Z", "dailyRecordingId": "01a106ad-e782-7b39-87ef-cf0b3cf3fcbe"}	2026-10-04 18:30:22.736498+07	2026-10-04 18:30:22.754119+07	1	\N
01a106ad-e7b3-7a5b-8ee9-847f51595179	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e7b3-7a5b-8ee9-847f51595179", "occurredOnUtc": "2026-10-04T11:30:22.7712466Z", "dailyRecordingId": "01a106ad-e7a3-71cd-b902-0decbcda1f55"}	2026-10-04 18:30:22.771246+07	2026-10-04 18:30:22.785942+07	1	\N
01a106ad-e7d3-7403-937c-6224441272b0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e7d3-7403-937c-6224441272b0", "occurredOnUtc": "2026-10-04T11:30:22.8039348Z", "dailyRecordingId": "01a106ad-e7c3-7674-a271-ed7cfc16e36a"}	2026-10-04 18:30:22.803934+07	2026-10-04 18:30:22.81727+07	1	\N
01a106ad-e925-74c6-8839-b51913a88600	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-e925-74c6-8839-b51913a88600", "occurredOnUtc": "2026-10-04T11:30:23.14167Z", "salesInvoiceId": "01a106ad-e851-7815-872f-3eee4106b6c0"}	2026-10-04 18:30:23.14167+07	2026-10-04 18:30:23.213277+07	1	\N
01a106ad-e9be-7c79-8923-d12c579543b4	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-e9be-7c79-8923-d12c579543b4", "occurredOnUtc": "2026-10-04T11:30:23.2941573Z", "paymentVoucherId": "01a106ad-e9a0-7549-af11-75b80e8d8502"}	2026-10-04 18:30:23.294157+07	2026-10-04 18:30:23.327239+07	1	\N
01a106ad-e9da-7ba4-bea9-aee43e9eec75	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e9da-7ba4-bea9-aee43e9eec75", "journalId": "01a106ad-e9d4-7a35-931f-17232db5e5d6", "occurredOnUtc": "2026-10-04T11:30:23.3223836Z"}	2026-10-04 18:30:23.322383+07	2026-10-04 18:30:23.328233+07	1	\N
01a106ad-ea0f-7b3d-a4be-3f7ab7e5234e	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-ea0f-7b3d-a4be-3f7ab7e5234e", "occurredOnUtc": "2026-10-04T11:30:23.3754092Z", "paymentVoucherId": "01a106ad-e9f1-769f-9227-f19e69f1c2ef"}	2026-10-04 18:30:23.375409+07	2026-10-04 18:30:23.409078+07	1	\N
01a106ad-ea2d-7a77-8d8a-3fcc5f18b8cd	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ea2d-7a77-8d8a-3fcc5f18b8cd", "journalId": "01a106ad-ea27-7aed-bd54-4875da11b3c0", "occurredOnUtc": "2026-10-04T11:30:23.4052332Z"}	2026-10-04 18:30:23.405233+07	2026-10-04 18:30:23.41043+07	1	\N
01a106ad-ea41-7a0e-95a9-3ebcb5409b9c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ea41-7a0e-95a9-3ebcb5409b9c", "occurredOnUtc": "2026-10-04T11:30:23.425969Z", "dailyRecordingId": "01a106ad-ea33-7b0c-b2b0-a3259ecd9ea0"}	2026-10-04 18:30:23.425969+07	2026-10-04 18:30:23.437623+07	1	\N
01a106ad-ea62-744a-9f0f-bd3362265c1f	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ad-ea62-744a-9f0f-bd3362265c1f", "cycleId": "01a106ad-ea62-7df6-aaaa-1359b7c73fa1", "occurredOnUtc": "2026-10-04T11:30:23.4583875Z"}	2026-10-04 18:30:23.458387+07	2026-10-04 18:30:23.466338+07	1	\N
01a106ad-eaef-77c6-9c1f-f733b301b785	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-eaef-77c6-9c1f-f733b301b785", "occurredOnUtc": "2026-10-04T11:30:23.5997255Z", "dailyRecordingId": "01a106ad-eae3-7142-bb16-27832515c562"}	2026-10-04 18:30:23.599725+07	2026-10-04 18:30:23.611816+07	1	\N
01a106ad-eb5a-7e56-bca6-e375bdd2c558	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-eb5a-7e56-bca6-e375bdd2c558", "occurredOnUtc": "2026-10-04T11:30:23.7067337Z", "salesInvoiceId": "01a106ad-eb3e-7840-8bd9-c308839c5897"}	2026-10-04 18:30:23.706733+07	2026-10-04 18:30:23.746198+07	1	\N
01a106ad-eb7d-7b66-8ea7-d905520d7b78	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-eb7d-7b66-8ea7-d905520d7b78", "journalId": "01a106ad-eb76-74e2-a654-a417c81ac15a", "occurredOnUtc": "2026-10-04T11:30:23.7411036Z"}	2026-10-04 18:30:23.741103+07	2026-10-04 18:30:23.747285+07	1	\N
01a106ad-eb91-78b4-860c-cae0fe24a427	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-eb91-78b4-860c-cae0fe24a427", "occurredOnUtc": "2026-10-04T11:30:23.7615825Z", "dailyRecordingId": "01a106ad-eb84-718e-954b-eea4a9013bc0"}	2026-10-04 18:30:23.761582+07	2026-10-04 18:30:23.77199+07	1	\N
01a106ad-ebdf-7fcf-afa0-d858181e2aa5	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-ebdf-7fcf-afa0-d858181e2aa5", "occurredOnUtc": "2026-10-04T11:30:23.8391308Z", "salesInvoiceId": "01a106ad-ebc3-784b-93be-5919195da558"}	2026-10-04 18:30:23.83913+07	2026-10-04 18:30:23.87056+07	1	\N
01a106ad-ebfa-725d-b48e-4576ad757dd5	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ebfa-725d-b48e-4576ad757dd5", "journalId": "01a106ad-ebf4-7205-9cce-47b3000e0f2f", "occurredOnUtc": "2026-10-04T11:30:23.8663839Z"}	2026-10-04 18:30:23.866383+07	2026-10-04 18:30:23.871851+07	1	\N
01a106ad-ec1a-7b25-a4ef-193a05a1c0d0	Domain.Sales.CreditNotes.SalesCreditNotePostedDomainEvent	{"id": "01a106ad-ec1a-7b25-a4ef-193a05a1c0d0", "occurredOnUtc": "2026-10-04T11:30:23.8986921Z", "salesCreditNoteId": "01a106ad-ec1a-7e4b-9c45-bc2b557665a5"}	2026-10-04 18:30:23.898692+07	2026-10-04 18:30:23.988547+07	1	\N
01a106ad-ec70-7bed-bdd6-260e6b017a28	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ec70-7bed-bdd6-260e6b017a28", "journalId": "01a106ad-ec6b-757c-a321-259e1e5a5e21", "occurredOnUtc": "2026-10-04T11:30:23.9846535Z"}	2026-10-04 18:30:23.984653+07	2026-10-04 18:30:23.989631+07	1	\N
01a106ad-ec80-73e2-b8a0-3eabbdec63b7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ec80-73e2-b8a0-3eabbdec63b7", "occurredOnUtc": "2026-10-04T11:30:24.0009416Z", "dailyRecordingId": "01a106ad-ec76-7ec6-9643-136d98207d00"}	2026-10-04 18:30:24.000941+07	2026-10-04 18:30:24.011109+07	1	\N
01a106ad-e968-7ced-ac73-3db8dc61b8b4	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-e968-7ced-ac73-3db8dc61b8b4", "journalId": "01a106ad-e961-7cea-b7d9-eec09af3cf3c", "occurredOnUtc": "2026-10-04T11:30:23.2083587Z"}	2026-10-04 18:30:23.208358+07	2026-10-04 18:30:23.215303+07	1	\N
01a106ad-e97d-7e6a-b529-6d4d3c8b2a42	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-e97d-7e6a-b529-6d4d3c8b2a42", "occurredOnUtc": "2026-10-04T11:30:23.229749Z", "dailyRecordingId": "01a106ad-e970-70d4-b497-f263237ef95c"}	2026-10-04 18:30:23.229749+07	2026-10-04 18:30:23.248119+07	1	\N
01a106ad-eba9-7135-b143-e9aed63be705	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-eba9-7135-b143-e9aed63be705", "occurredOnUtc": "2026-10-04T11:30:23.78501Z", "dailyRecordingId": "01a106ad-eb9d-7ae2-b878-5808d3e51052"}	2026-10-04 18:30:23.78501+07	2026-10-04 18:30:23.795464+07	1	\N
01a106ad-ec98-7e0e-b800-8f9c011a30a4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ec98-7e0e-b800-8f9c011a30a4", "occurredOnUtc": "2026-10-04T11:30:24.0240219Z", "dailyRecordingId": "01a106ad-ec8c-711c-a16d-b89bca847c32"}	2026-10-04 18:30:24.024021+07	2026-10-04 18:30:24.033958+07	1	\N
01a106ad-ecb2-7b3a-97d4-a1a9497646a5	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-ecb2-7b3a-97d4-a1a9497646a5", "occurredOnUtc": "2026-10-04T11:30:24.0506568Z", "goodsReceiptId": "01a106ad-ecb2-7cf5-bc99-7307f161b5bc"}	2026-10-04 18:30:24.050656+07	2026-10-04 18:30:24.08842+07	1	\N
01a106ad-ecd4-7714-bbbd-b4bf2141ea15	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ecd4-7714-bbbd-b4bf2141ea15", "journalId": "01a106ad-eccf-791e-8ce1-0e705d33d373", "occurredOnUtc": "2026-10-04T11:30:24.0841984Z"}	2026-10-04 18:30:24.084198+07	2026-10-04 18:30:24.089449+07	1	\N
01a106ad-ece9-7aaf-823d-06f241f6d871	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-ece9-7aaf-823d-06f241f6d871", "occurredOnUtc": "2026-10-04T11:30:24.1052477Z", "goodsReceiptId": "01a106ad-ece9-7ff1-aad4-f1d48c18557f"}	2026-10-04 18:30:24.105247+07	2026-10-04 18:30:24.149278+07	1	\N
01a106ad-ed11-7789-8469-a8700fb6edcc	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ed11-7789-8469-a8700fb6edcc", "journalId": "01a106ad-ed0a-79cd-8ec4-89707c0de8bc", "occurredOnUtc": "2026-10-04T11:30:24.1450135Z"}	2026-10-04 18:30:24.145013+07	2026-10-04 18:30:24.150693+07	1	\N
01a106ad-ed54-769b-bd0e-d72cdd04365f	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-ed54-769b-bd0e-d72cdd04365f", "occurredOnUtc": "2026-10-04T11:30:24.2127412Z", "customerReceiptId": "01a106ad-ed54-70d0-a419-4f17a1e62f8a"}	2026-10-04 18:30:24.212741+07	2026-10-04 18:30:24.316574+07	1	\N
01a106ad-edb8-731d-bf5e-972343156aa9	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-edb8-731d-bf5e-972343156aa9", "journalId": "01a106ad-edb2-7bcf-9765-1137cfac49ca", "occurredOnUtc": "2026-10-04T11:30:24.3125579Z"}	2026-10-04 18:30:24.312557+07	2026-10-04 18:30:24.31752+07	1	\N
01a106ad-ede7-7aff-940f-478c0838c31a	Domain.Inventory.StockReturns.StockReturnPostedDomainEvent	{"id": "01a106ad-ede7-7aff-940f-478c0838c31a", "occurredOnUtc": "2026-10-04T11:30:24.359901Z", "stockReturnId": "01a106ad-ede7-7914-bc12-88b477649acb"}	2026-10-04 18:30:24.359901+07	2026-10-04 18:30:24.502716+07	1	\N
01a106ad-ee72-78ce-aa1c-dbe5b7e77b2a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ee72-78ce-aa1c-dbe5b7e77b2a", "journalId": "01a106ad-ee6c-7755-ba01-c69159dae764", "occurredOnUtc": "2026-10-04T11:30:24.4980875Z"}	2026-10-04 18:30:24.498087+07	2026-10-04 18:30:24.503835+07	1	\N
01a106ad-eec8-7408-89a7-c099b253c97c	Domain.Partnership.Cycles.CycleClosedDomainEvent	{"id": "01a106ad-eec8-7408-89a7-c099b253c97c", "cycleId": "01a106ad-c6f1-7f5f-8d11-f2ccfb191671", "occurredOnUtc": "2026-10-04T11:30:24.5848364Z"}	2026-10-04 18:30:24.584836+07	2026-10-04 18:30:24.659199+07	1	\N
01a106ad-ef0f-76e1-9da8-9cd604570d5d	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ef0f-76e1-9da8-9cd604570d5d", "journalId": "01a106ad-ef09-79b0-a677-5566eaae74e2", "occurredOnUtc": "2026-10-04T11:30:24.655121Z"}	2026-10-04 18:30:24.655121+07	2026-10-04 18:30:24.660394+07	1	\N
01a106ad-ef21-7b8b-b932-12dd5f2487be	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ef21-7b8b-b932-12dd5f2487be", "occurredOnUtc": "2026-10-04T11:30:24.6735375Z", "dailyRecordingId": "01a106ad-ef15-7d1f-94b2-f9150c6240ee"}	2026-10-04 18:30:24.673537+07	2026-10-04 18:30:24.684013+07	1	\N
01a106ad-ef38-718c-8465-d7a934cc4333	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ef38-718c-8465-d7a934cc4333", "occurredOnUtc": "2026-10-04T11:30:24.6968682Z", "dailyRecordingId": "01a106ad-ef2c-7dc3-9401-0b50b567af2d"}	2026-10-04 18:30:24.696868+07	2026-10-04 18:30:24.706487+07	1	\N
01a106ad-ef52-7c61-84ca-0587a310a3a1	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-ef52-7c61-84ca-0587a310a3a1", "occurredOnUtc": "2026-10-04T11:30:24.7227714Z", "customerReceiptId": "01a106ad-ef52-76b0-bc3c-1f9582692a76"}	2026-10-04 18:30:24.722771+07	2026-10-04 18:30:24.753247+07	1	\N
01a106ad-ef6d-7c82-a0c7-06e48ac93b15	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ef6d-7c82-a0c7-06e48ac93b15", "journalId": "01a106ad-ef67-714d-9893-a4ab237caf34", "occurredOnUtc": "2026-10-04T11:30:24.749846Z"}	2026-10-04 18:30:24.749846+07	2026-10-04 18:30:24.754704+07	1	\N
01a106ad-ef7f-7232-b54d-b3cb1a1c1b5f	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ef7f-7232-b54d-b3cb1a1c1b5f", "occurredOnUtc": "2026-10-04T11:30:24.7671299Z", "dailyRecordingId": "01a106ad-ef74-730a-a234-b1934d1010fa"}	2026-10-04 18:30:24.767129+07	2026-10-04 18:30:24.777497+07	1	\N
01a106ad-ef95-7104-a9ae-5283c5b16789	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ef95-7104-a9ae-5283c5b16789", "occurredOnUtc": "2026-10-04T11:30:24.7894913Z", "dailyRecordingId": "01a106ad-ef8a-762f-b87b-4dfa0cc11dfa"}	2026-10-04 18:30:24.789491+07	2026-10-04 18:30:24.79964+07	1	\N
01a106ad-efb0-76f8-8160-f3165275a83c	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-efb0-76f8-8160-f3165275a83c", "occurredOnUtc": "2026-10-04T11:30:24.8169171Z", "goodsReceiptId": "01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d"}	2026-10-04 18:30:24.816917+07	2026-10-04 18:30:24.876167+07	1	\N
01a106ad-f23e-7a15-a19b-50557d414e44	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f23e-7a15-a19b-50557d414e44", "occurredOnUtc": "2026-10-04T11:30:25.4707288Z", "dailyRecordingId": "01a106ad-f232-7573-8599-5c09641649d8"}	2026-10-04 18:30:25.470728+07	2026-10-04 18:30:25.480767+07	1	\N
01a106ad-f280-7a4e-826b-8d7400b5bffb	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f280-7a4e-826b-8d7400b5bffb", "occurredOnUtc": "2026-10-04T11:30:25.5361584Z", "cashTransactionId": "01a106ad-f267-7992-ae91-ba97a4decb94"}	2026-10-04 18:30:25.536158+07	2026-10-04 18:30:25.565218+07	1	\N
01a106ad-f298-7454-aad2-72f7f55636a5	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f298-7454-aad2-72f7f55636a5", "journalId": "01a106ad-f290-717d-880d-3699f1ad9967", "occurredOnUtc": "2026-10-04T11:30:25.5603932Z"}	2026-10-04 18:30:25.560393+07	2026-10-04 18:30:25.566102+07	1	\N
01a106ad-f2c1-7f01-aa3b-b4561355d7a4	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f2c1-7f01-aa3b-b4561355d7a4", "occurredOnUtc": "2026-10-04T11:30:25.6012585Z", "cashTransactionId": "01a106ad-f2a7-74d1-a2f6-61b4e9051c7e"}	2026-10-04 18:30:25.601258+07	2026-10-04 18:30:25.627795+07	1	\N
01a106ad-f2d7-7473-bbc8-0ca1d2f8121b	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f2d7-7473-bbc8-0ca1d2f8121b", "journalId": "01a106ad-f2d1-78a6-b098-ca93c0f34226", "occurredOnUtc": "2026-10-04T11:30:25.6235707Z"}	2026-10-04 18:30:25.62357+07	2026-10-04 18:30:25.628758+07	1	\N
01a106ad-f3dd-7b74-bd89-2ba47958696a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f3dd-7b74-bd89-2ba47958696a", "journalId": "01a106ad-f3d7-7e9f-b3a3-4cbca1ddbb86", "occurredOnUtc": "2026-10-04T11:30:25.8850623Z"}	2026-10-04 18:30:25.885062+07	2026-10-04 18:30:25.890006+07	1	\N
01a106ad-efd2-725c-a66c-8775008126cf	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-efd2-725c-a66c-8775008126cf", "journalId": "01a106ad-efcc-7fcc-865b-eec0bd50e112", "occurredOnUtc": "2026-10-04T11:30:24.8504032Z"}	2026-10-04 18:30:24.850403+07	2026-10-04 18:30:24.877856+07	1	\N
01a106ad-efe7-74e2-ab87-a12e6112373e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-efe7-74e2-ab87-a12e6112373e", "journalId": "01a106ad-efe1-7fc0-a130-bd2aeeeb5ad7", "occurredOnUtc": "2026-10-04T11:30:24.8715583Z"}	2026-10-04 18:30:24.871558+07	2026-10-04 18:30:24.878198+07	1	\N
01a106ad-eff6-74bc-b479-e15c222b4ddb	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ad-eff6-74bc-b479-e15c222b4ddb", "cycleId": "01a106ad-ea62-7df6-aaaa-1359b7c73fa1", "occurredOnUtc": "2026-10-04T11:30:24.8862623Z"}	2026-10-04 18:30:24.886262+07	2026-10-04 18:30:24.895428+07	1	\N
01a106ad-f00d-720f-a77e-99c086e593f1	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-f00d-720f-a77e-99c086e593f1", "occurredOnUtc": "2026-10-04T11:30:24.9093986Z", "stockTransferId": "01a106ad-f00d-7930-be85-9e425747d0fa"}	2026-10-04 18:30:24.909398+07	2026-10-04 18:30:24.96126+07	1	\N
01a106ad-f03b-71ad-a74d-167df8f8ed55	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f03b-71ad-a74d-167df8f8ed55", "journalId": "01a106ad-f035-7ae5-9f06-9dd2daf6c907", "occurredOnUtc": "2026-10-04T11:30:24.9554354Z"}	2026-10-04 18:30:24.955435+07	2026-10-04 18:30:24.962339+07	1	\N
01a106ad-f062-7593-bd49-ab8932ed853f	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f062-7593-bd49-ab8932ed853f", "occurredOnUtc": "2026-10-04T11:30:24.9943591Z", "cashTransactionId": "01a106ad-f049-73aa-a57a-fae76d3cbeb1"}	2026-10-04 18:30:24.994359+07	2026-10-04 18:30:25.021436+07	1	\N
01a106ad-f078-781d-800b-da5d299f9132	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f078-781d-800b-da5d299f9132", "journalId": "01a106ad-f073-7434-8612-1c045fcd87d6", "occurredOnUtc": "2026-10-04T11:30:25.0165935Z"}	2026-10-04 18:30:25.016593+07	2026-10-04 18:30:25.022835+07	1	\N
01a106ad-f0a1-7675-9a73-d2a95f129c6a	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f0a1-7675-9a73-d2a95f129c6a", "occurredOnUtc": "2026-10-04T11:30:25.0579797Z", "cashTransactionId": "01a106ad-f087-714c-a708-74825c699f6c"}	2026-10-04 18:30:25.057979+07	2026-10-04 18:30:25.086523+07	1	\N
01a106ad-f0ba-7ad3-adcb-230fdb9f8721	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f0ba-7ad3-adcb-230fdb9f8721", "journalId": "01a106ad-f0b3-7ba6-b6a7-4ebe97e7feb0", "occurredOnUtc": "2026-10-04T11:30:25.0825119Z"}	2026-10-04 18:30:25.082511+07	2026-10-04 18:30:25.088068+07	1	\N
01a106ad-f0d4-7e23-9bd0-92f5dc440857	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-f0d4-7e23-9bd0-92f5dc440857", "occurredOnUtc": "2026-10-04T11:30:25.108225Z", "customerReceiptId": "01a106ad-f0d4-7029-a94d-5b00e1f3097e"}	2026-10-04 18:30:25.108225+07	2026-10-04 18:30:25.13943+07	1	\N
01a106ad-f0ef-7d5a-9d8b-497fbc85d0eb	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f0ef-7d5a-9d8b-497fbc85d0eb", "journalId": "01a106ad-f0ea-7c2e-8354-eea43424a08f", "occurredOnUtc": "2026-10-04T11:30:25.1355329Z"}	2026-10-04 18:30:25.135532+07	2026-10-04 18:30:25.140381+07	1	\N
01a106ad-f1e3-7249-a24e-581e830268bc	Domain.Costing.PlasmaSettlements.PlasmaSettlementApprovedDomainEvent	{"id": "01a106ad-f1e3-7249-a24e-581e830268bc", "occurredOnUtc": "2026-10-04T11:30:25.3790347Z", "plasmaSettlementId": "01a106ad-f14e-7f35-82b0-7f8b0fcf95fa"}	2026-10-04 18:30:25.379034+07	2026-10-04 18:30:25.435771+07	1	\N
01a106ad-f217-762e-b67b-f6b918b35b14	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f217-762e-b67b-f6b918b35b14", "journalId": "01a106ad-f212-70d6-ac42-ceae631ca7be", "occurredOnUtc": "2026-10-04T11:30:25.431893Z"}	2026-10-04 18:30:25.431893+07	2026-10-04 18:30:25.436643+07	1	\N
01a106ad-f228-7a8e-8ab0-5324f40b7754	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f228-7a8e-8ab0-5324f40b7754", "occurredOnUtc": "2026-10-04T11:30:25.4484044Z", "dailyRecordingId": "01a106ad-f21d-729c-aed3-eb1298a6ae34"}	2026-10-04 18:30:25.448404+07	2026-10-04 18:30:25.45781+07	1	\N
01a106ad-f255-78a2-9ac8-5bcec28a0a2a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f255-78a2-9ac8-5bcec28a0a2a", "occurredOnUtc": "2026-10-04T11:30:25.4934792Z", "dailyRecordingId": "01a106ad-f249-7711-bf44-ca51c35a89d5"}	2026-10-04 18:30:25.493479+07	2026-10-04 18:30:25.50291+07	1	\N
01a106ad-f311-7cc4-b2e9-75c4978de445	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f311-7cc4-b2e9-75c4978de445", "occurredOnUtc": "2026-10-04T11:30:25.6817758Z", "dailyRecordingId": "01a106ad-f306-7f5f-86ce-626ab2cdf147"}	2026-10-04 18:30:25.681775+07	2026-10-04 18:30:25.691545+07	1	\N
01a106ad-f327-7e88-ad30-0127ab02a4ba	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f327-7e88-ad30-0127ab02a4ba", "occurredOnUtc": "2026-10-04T11:30:25.703781Z", "dailyRecordingId": "01a106ad-f31c-7d26-a64f-c2ee00ca8c01"}	2026-10-04 18:30:25.703781+07	2026-10-04 18:30:25.715464+07	1	\N
01a106ad-f340-72e5-bce6-0a24b1931f8e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f340-72e5-bce6-0a24b1931f8e", "occurredOnUtc": "2026-10-04T11:30:25.7284686Z", "dailyRecordingId": "01a106ad-f334-7b2b-9d13-3a1c9e1852c7"}	2026-10-04 18:30:25.728468+07	2026-10-04 18:30:25.741614+07	1	\N
01a106ad-f3b9-728b-939f-eacee42ab3c2	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ad-f3b9-728b-939f-eacee42ab3c2", "occurredOnUtc": "2026-10-04T11:30:25.8496508Z", "paymentVoucherId": "01a106ad-f387-7df5-b3a2-f33f2933d8d4"}	2026-10-04 18:30:25.84965+07	2026-10-04 18:30:25.888821+07	1	\N
01a106ad-f40d-72b6-ac93-300068557da5	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ad-f40d-72b6-ac93-300068557da5", "cycleId": "01a106ad-f40d-7a20-b733-56168fcec265", "occurredOnUtc": "2026-10-04T11:30:25.9335736Z"}	2026-10-04 18:30:25.933573+07	2026-10-04 18:30:25.938587+07	1	\N
01a106ad-f474-75f4-bb90-543fc51c72f0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f474-75f4-bb90-543fc51c72f0", "occurredOnUtc": "2026-10-04T11:30:26.0360593Z", "dailyRecordingId": "01a106ad-f467-7e62-b402-597ceab70307"}	2026-10-04 18:30:26.036059+07	2026-10-04 18:30:26.046728+07	1	\N
01a106ad-f503-7218-9cb9-bfca8c6a7264	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f503-7218-9cb9-bfca8c6a7264", "occurredOnUtc": "2026-10-04T11:30:26.1790702Z", "dailyRecordingId": "01a106ad-f4f6-7e81-a33a-0bcbb7962ab5"}	2026-10-04 18:30:26.17907+07	2026-10-04 18:30:26.189223+07	1	\N
01a106ad-f55b-7be3-925f-9cb9e3a4b0c7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f55b-7be3-925f-9cb9e3a4b0c7", "occurredOnUtc": "2026-10-04T11:30:26.2672553Z", "dailyRecordingId": "01a106ad-f54e-7895-b6b7-43aa74cdb435"}	2026-10-04 18:30:26.267255+07	2026-10-04 18:30:26.284162+07	1	\N
01a106ad-f5e8-7231-baf6-95e0e19b8e6c	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-f5e8-7231-baf6-95e0e19b8e6c", "occurredOnUtc": "2026-10-04T11:30:26.4080833Z", "goodsReceiptId": "01a106ad-f5e8-7414-89fa-95e6aee15d5c"}	2026-10-04 18:30:26.408083+07	2026-10-04 18:30:26.448052+07	1	\N
01a106ad-f60c-7ca1-9be1-d10788a2f847	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f60c-7ca1-9be1-d10788a2f847", "journalId": "01a106ad-f605-7fae-aabe-a1f6f12a4b11", "occurredOnUtc": "2026-10-04T11:30:26.4442856Z"}	2026-10-04 18:30:26.444285+07	2026-10-04 18:30:26.449179+07	1	\N
01a106ad-f64a-7d10-b2fd-79c71a37d390	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f64a-7d10-b2fd-79c71a37d390", "journalId": "01a106ad-f644-78c7-b744-61f2b6a1fbef", "occurredOnUtc": "2026-10-04T11:30:26.5063856Z"}	2026-10-04 18:30:26.506385+07	2026-10-04 18:30:26.512111+07	1	\N
01a106ad-f3ee-78ac-aba2-c3eb9be25f74	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f3ee-78ac-aba2-c3eb9be25f74", "occurredOnUtc": "2026-10-04T11:30:25.9020567Z", "dailyRecordingId": "01a106ad-f3e2-7d84-8967-62c819b50a45"}	2026-10-04 18:30:25.902056+07	2026-10-04 18:30:25.912474+07	1	\N
01a106ad-f4a4-7040-af91-df2625b68985	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-f4a4-7040-af91-df2625b68985", "occurredOnUtc": "2026-10-04T11:30:26.0844009Z", "vendorInvoiceId": "01a106ad-f492-7328-b925-8c257fbccde7"}	2026-10-04 18:30:26.0844+07	2026-10-04 18:30:26.128481+07	1	\N
01a106ad-f4cb-7891-80a8-a0d098fde64e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f4cb-7891-80a8-a0d098fde64e", "journalId": "01a106ad-f4bf-7afc-a903-e9caa72d7e2e", "occurredOnUtc": "2026-10-04T11:30:26.1236597Z"}	2026-10-04 18:30:26.123659+07	2026-10-04 18:30:26.130435+07	1	\N
01a106ad-f4e5-789a-ac45-29ddeb7a9513	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f4e5-789a-ac45-29ddeb7a9513", "occurredOnUtc": "2026-10-04T11:30:26.1490648Z", "dailyRecordingId": "01a106ad-f4d4-7ace-a351-6b1924b17cbd"}	2026-10-04 18:30:26.149064+07	2026-10-04 18:30:26.164846+07	1	\N
01a106ad-f53f-7bf3-bbf4-71cdaeb43c52	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f53f-7bf3-bbf4-71cdaeb43c52", "occurredOnUtc": "2026-10-04T11:30:26.2399924Z", "dailyRecordingId": "01a106ad-f532-7382-a010-a2640ea58141"}	2026-10-04 18:30:26.239992+07	2026-10-04 18:30:26.253413+07	1	\N
01a106ad-f579-7707-8bb7-96276d27704a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f579-7707-8bb7-96276d27704a", "occurredOnUtc": "2026-10-04T11:30:26.2976923Z", "dailyRecordingId": "01a106ad-f56d-7fcf-afdf-c0d3f34ddc19"}	2026-10-04 18:30:26.297692+07	2026-10-04 18:30:26.308169+07	1	\N
01a106ad-f5b0-7394-bf64-34e72dc19827	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-f5b0-7394-bf64-34e72dc19827", "occurredOnUtc": "2026-10-04T11:30:26.3529609Z", "salesInvoiceId": "01a106ad-f592-755f-870c-754df282fed9"}	2026-10-04 18:30:26.35296+07	2026-10-04 18:30:26.388263+07	1	\N
01a106ad-f5ce-7e04-a5d7-c1950f5b5530	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f5ce-7e04-a5d7-c1950f5b5530", "journalId": "01a106ad-f5c8-7754-988d-972f0e49c0ac", "occurredOnUtc": "2026-10-04T11:30:26.3824142Z"}	2026-10-04 18:30:26.382414+07	2026-10-04 18:30:26.389162+07	1	\N
01a106ad-f624-7041-bc38-5881d3770744	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-f624-7041-bc38-5881d3770744", "occurredOnUtc": "2026-10-04T11:30:26.4680132Z", "goodsReceiptId": "01a106ad-f623-7d55-9b20-a1b5d900f045"}	2026-10-04 18:30:26.468013+07	2026-10-04 18:30:26.511136+07	1	\N
01a106ad-f65d-74d3-b1e2-f80bab01b501	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f65d-74d3-b1e2-f80bab01b501", "occurredOnUtc": "2026-10-04T11:30:26.5250563Z", "dailyRecordingId": "01a106ad-f650-75c7-9fba-80ac5ac0a46f"}	2026-10-04 18:30:26.525056+07	2026-10-04 18:30:26.53505+07	1	\N
01a106ad-f691-70b2-bb4f-976a0605379e	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-f691-70b2-bb4f-976a0605379e", "occurredOnUtc": "2026-10-04T11:30:26.5777372Z", "vendorInvoiceId": "01a106ad-f67d-7994-8047-8c285fd5bfe9"}	2026-10-04 18:30:26.577737+07	2026-10-04 18:30:26.603769+07	1	\N
01a106ad-f6a7-7b22-aad0-5d1c20a34ba2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f6a7-7b22-aad0-5d1c20a34ba2", "journalId": "01a106ad-f6a3-7055-8a36-9d50e4d7b53a", "occurredOnUtc": "2026-10-04T11:30:26.5999189Z"}	2026-10-04 18:30:26.599918+07	2026-10-04 18:30:26.604663+07	1	\N
01a106ad-f6d5-7997-b853-44ac3862ff9d	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-f6d5-7997-b853-44ac3862ff9d", "occurredOnUtc": "2026-10-04T11:30:26.6452554Z", "vendorInvoiceId": "01a106ad-f6c0-7485-ac3f-0303994bd9b1"}	2026-10-04 18:30:26.645255+07	2026-10-04 18:30:26.673813+07	1	\N
01a106ad-f6ed-7702-9c32-abf85cd5743e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f6ed-7702-9c32-abf85cd5743e", "journalId": "01a106ad-f6e8-75d2-8259-2d4660a8bdc9", "occurredOnUtc": "2026-10-04T11:30:26.6692155Z"}	2026-10-04 18:30:26.669215+07	2026-10-04 18:30:26.675097+07	1	\N
01a106ad-f6fe-740b-9bd3-0b9345a47128	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f6fe-740b-9bd3-0b9345a47128", "occurredOnUtc": "2026-10-04T11:30:26.6865291Z", "dailyRecordingId": "01a106ad-f6f4-757b-8841-bd32218bf1a7"}	2026-10-04 18:30:26.686529+07	2026-10-04 18:30:26.696832+07	1	\N
01a106ad-f715-76ca-9d14-9c5c3776beeb	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f715-76ca-9d14-9c5c3776beeb", "occurredOnUtc": "2026-10-04T11:30:26.7092346Z", "dailyRecordingId": "01a106ad-f709-7db8-aca5-9deaf815b5f3"}	2026-10-04 18:30:26.709234+07	2026-10-04 18:30:26.71972+07	1	\N
01a106ad-f754-736e-9af1-a10f4ea5d348	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f754-736e-9af1-a10f4ea5d348", "occurredOnUtc": "2026-10-04T11:30:26.7721866Z", "dailyRecordingId": "01a106ad-f749-7f43-9f65-0d6bbdfebc35"}	2026-10-04 18:30:26.772186+07	2026-10-04 18:30:26.782348+07	1	\N
01a106ad-f76b-7a0b-a322-f46fb2e6c457	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f76b-7a0b-a322-f46fb2e6c457", "occurredOnUtc": "2026-10-04T11:30:26.795805Z", "dailyRecordingId": "01a106ad-f75f-7fef-9bb9-77c088b6c641"}	2026-10-04 18:30:26.795805+07	2026-10-04 18:30:26.80861+07	1	\N
01a106ad-f79d-79c2-9606-2c9ac739fd61	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-f79d-79c2-9606-2c9ac739fd61", "occurredOnUtc": "2026-10-04T11:30:26.8454398Z", "salesInvoiceId": "01a106ad-f785-70ba-9a35-aaa06a2eaa01"}	2026-10-04 18:30:26.845439+07	2026-10-04 18:30:26.895341+07	1	\N
01a106ad-f7c8-725b-b990-02422fd7e1b4	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f7c8-725b-b990-02422fd7e1b4", "journalId": "01a106ad-f7ba-7993-aab9-5860172bd406", "occurredOnUtc": "2026-10-04T11:30:26.8880705Z"}	2026-10-04 18:30:26.88807+07	2026-10-04 18:30:26.897199+07	1	\N
01a106ad-f7e8-7285-86e3-2a6281c46c79	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ad-f7e8-7285-86e3-2a6281c46c79", "occurredOnUtc": "2026-10-04T11:30:26.9206041Z", "goodsReceiptId": "01a106ad-f7e8-755b-9182-950be853462c"}	2026-10-04 18:30:26.920604+07	2026-10-04 18:30:26.984911+07	1	\N
01a106ad-f8a7-7ad5-958a-e3fb2d1cf73d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f8a7-7ad5-958a-e3fb2d1cf73d", "occurredOnUtc": "2026-10-04T11:30:27.1110926Z", "dailyRecordingId": "01a106ad-f89b-7a6d-b469-a34685d639d1"}	2026-10-04 18:30:27.111092+07	2026-10-04 18:30:27.121145+07	1	\N
01a106ad-f941-76cf-a3b2-480209037a61	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f941-76cf-a3b2-480209037a61", "occurredOnUtc": "2026-10-04T11:30:27.2659793Z", "cashTransactionId": "01a106ad-f926-74e8-8349-63285d1f77f2"}	2026-10-04 18:30:27.265979+07	2026-10-04 18:30:27.296884+07	1	\N
01a106ad-f95b-7685-9bdb-8ed07c39901a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f95b-7685-9bdb-8ed07c39901a", "journalId": "01a106ad-f954-7887-b551-273b9e5a2bf4", "occurredOnUtc": "2026-10-04T11:30:27.2919616Z"}	2026-10-04 18:30:27.291961+07	2026-10-04 18:30:27.297688+07	1	\N
01a106ad-f985-7ea8-9185-cc5df40696af	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f985-7ea8-9185-cc5df40696af", "journalId": "01a106ad-f969-72c5-b6ff-5350ecde3fe2", "occurredOnUtc": "2026-10-04T11:30:27.3331188Z"}	2026-10-04 18:30:27.333118+07	2026-10-04 18:30:27.33786+07	1	\N
01a106ad-f80f-7433-8932-acbe7d3734e0	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f80f-7433-8932-acbe7d3734e0", "journalId": "01a106ad-f807-76c6-9389-b37ec33708ed", "occurredOnUtc": "2026-10-04T11:30:26.9591923Z"}	2026-10-04 18:30:26.959192+07	2026-10-04 18:30:26.986136+07	1	\N
01a106ad-f824-70e8-92bb-1d4cef443776	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f824-70e8-92bb-1d4cef443776", "journalId": "01a106ad-f81e-78d8-ab1a-6e8e793dadab", "occurredOnUtc": "2026-10-04T11:30:26.9808893Z"}	2026-10-04 18:30:26.980889+07	2026-10-04 18:30:26.9864+07	1	\N
01a106ad-f834-7ae8-af1d-967c23312a9a	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ad-f834-7ae8-af1d-967c23312a9a", "cycleId": "01a106ad-f40d-7a20-b733-56168fcec265", "occurredOnUtc": "2026-10-04T11:30:26.9961057Z"}	2026-10-04 18:30:26.996105+07	2026-10-04 18:30:27.005314+07	1	\N
01a106ad-f84d-7a36-9468-e92b9dcc4296	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-f84d-7a36-9468-e92b9dcc4296", "occurredOnUtc": "2026-10-04T11:30:27.0217116Z", "stockTransferId": "01a106ad-f84d-7a1b-8cac-41c2469803d4"}	2026-10-04 18:30:27.021711+07	2026-10-04 18:30:27.074855+07	1	\N
01a106ad-f87e-70ef-a537-064405177245	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f87e-70ef-a537-064405177245", "journalId": "01a106ad-f878-7517-bcc2-ceb24a7bfb1c", "occurredOnUtc": "2026-10-04T11:30:27.0702688Z"}	2026-10-04 18:30:27.070268+07	2026-10-04 18:30:27.075729+07	1	\N
01a106ad-f890-7392-8fbf-23d8771f0c21	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-f890-7392-8fbf-23d8771f0c21", "occurredOnUtc": "2026-10-04T11:30:27.0885904Z", "dailyRecordingId": "01a106ad-f884-7c25-b2e0-734cf1afa507"}	2026-10-04 18:30:27.08859+07	2026-10-04 18:30:27.09876+07	1	\N
01a106ad-f8d4-7e41-a1f9-bda21eaa1777	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ad-f8d4-7e41-a1f9-bda21eaa1777", "occurredOnUtc": "2026-10-04T11:30:27.1565026Z", "cashTransactionId": "01a106ad-f8ba-736a-a425-932ed8595a38"}	2026-10-04 18:30:27.156502+07	2026-10-04 18:30:27.18554+07	1	\N
01a106ad-f8ec-71be-a1aa-1a1a032aaf17	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f8ec-71be-a1aa-1a1a032aaf17", "journalId": "01a106ad-f8e6-7273-8e49-7beba48d05f5", "occurredOnUtc": "2026-10-04T11:30:27.1806645Z"}	2026-10-04 18:30:27.180664+07	2026-10-04 18:30:27.18641+07	1	\N
01a106ad-f919-7680-9adf-f4f09b48502a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f919-7680-9adf-f4f09b48502a", "journalId": "01a106ad-f8fb-7149-8708-5206ed379cfd", "occurredOnUtc": "2026-10-04T11:30:27.2253942Z"}	2026-10-04 18:30:27.225394+07	2026-10-04 18:30:27.230159+07	1	\N
01a106ad-f999-7191-8774-59e719b3d6ba	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-f999-7191-8774-59e719b3d6ba", "occurredOnUtc": "2026-10-04T11:30:27.353363Z", "customerReceiptId": "01a106ad-f999-7d5f-ab28-ea8b09d7216b"}	2026-10-04 18:30:27.353363+07	2026-10-04 18:30:27.386144+07	1	\N
01a106ad-f9b5-7789-8d93-b5234803a5db	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-f9b5-7789-8d93-b5234803a5db", "journalId": "01a106ad-f9ae-790f-b859-fd27a56a0630", "occurredOnUtc": "2026-10-04T11:30:27.3818776Z"}	2026-10-04 18:30:27.381877+07	2026-10-04 18:30:27.387445+07	1	\N
01a106ad-f9df-7216-a706-a95f088d8650	Domain.Inventory.StockReturns.StockReturnPostedDomainEvent	{"id": "01a106ad-f9df-7216-a706-a95f088d8650", "occurredOnUtc": "2026-10-04T11:30:27.423258Z", "stockReturnId": "01a106ad-f9df-72b6-8bd3-82399696b6f7"}	2026-10-04 18:30:27.423258+07	2026-10-04 18:30:27.477921+07	1	\N
01a106ad-f9e3-7e33-a906-08e5dd537eaf	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-f9e3-7e33-a906-08e5dd537eaf", "occurredOnUtc": "2026-10-04T11:30:27.4271754Z", "stockTransferId": "01a106ad-f9e3-77ab-a80a-c5fa58f74613"}	2026-10-04 18:30:27.427175+07	2026-10-04 18:30:27.506043+07	1	\N
01a106ad-fa43-753a-88fa-80ea632f1345	Domain.Inventory.StockReturns.StockReturnPostedDomainEvent	{"id": "01a106ad-fa43-753a-88fa-80ea632f1345", "occurredOnUtc": "2026-10-04T11:30:27.5231097Z", "stockReturnId": "01a106ad-fa43-7c1d-9eb8-33609a30db39"}	2026-10-04 18:30:27.523109+07	2026-10-04 18:30:27.573776+07	1	\N
01a106ad-fa71-7819-bd47-43e00833c1b6	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fa71-7819-bd47-43e00833c1b6", "journalId": "01a106ad-fa6a-753e-ba91-bc5dd3931c1e", "occurredOnUtc": "2026-10-04T11:30:27.5694752Z"}	2026-10-04 18:30:27.569475+07	2026-10-04 18:30:27.574783+07	1	\N
01a106ad-fa8e-7c74-92f8-695865ff205f	Domain.Partnership.Cycles.CycleClosedDomainEvent	{"id": "01a106ad-fa8e-7c74-92f8-695865ff205f", "cycleId": "01a106ad-d05a-759c-a6ff-c3e67df7f41b", "occurredOnUtc": "2026-10-04T11:30:27.598322Z"}	2026-10-04 18:30:27.598322+07	2026-10-04 18:30:27.627566+07	1	\N
01a106ad-faa6-72ed-afb0-37b84d1a7f41	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-faa6-72ed-afb0-37b84d1a7f41", "journalId": "01a106ad-faa0-7b4e-bd53-7aca600c5ab0", "occurredOnUtc": "2026-10-04T11:30:27.6227838Z"}	2026-10-04 18:30:27.622783+07	2026-10-04 18:30:27.629165+07	1	\N
01a106ad-fabf-7e0d-b64e-5ba399f208bc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fabf-7e0d-b64e-5ba399f208bc", "occurredOnUtc": "2026-10-04T11:30:27.6477968Z", "dailyRecordingId": "01a106ad-fab0-7a61-b55c-ecf6a30f1748"}	2026-10-04 18:30:27.647796+07	2026-10-04 18:30:27.679937+07	1	\N
01a106ad-fb75-7b5b-bd24-c840fb103aed	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-fb75-7b5b-bd24-c840fb103aed", "occurredOnUtc": "2026-10-04T11:30:27.8290142Z", "bankTransferId": "01a106ad-fb75-7cd2-ae6f-136641869f21"}	2026-10-04 18:30:27.829014+07	2026-10-04 18:30:27.854486+07	1	\N
01a106ad-fb89-74f9-a15e-383973de0fb7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fb89-74f9-a15e-383973de0fb7", "journalId": "01a106ad-fb84-7f23-8e32-cb62f00a03f9", "occurredOnUtc": "2026-10-04T11:30:27.849851Z"}	2026-10-04 18:30:27.849851+07	2026-10-04 18:30:27.855454+07	1	\N
01a106ad-fbc5-7493-90b8-f27251dce875	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fbc5-7493-90b8-f27251dce875", "occurredOnUtc": "2026-10-04T11:30:27.9094499Z", "dailyRecordingId": "01a106ad-fbb7-70e1-a993-e0a2516e5837"}	2026-10-04 18:30:27.909449+07	2026-10-04 18:30:27.920781+07	1	\N
01a106ad-fbf8-7c7b-8a6c-99bed52f73e7	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-fbf8-7c7b-8a6c-99bed52f73e7", "occurredOnUtc": "2026-10-04T11:30:27.9602524Z", "customerReceiptId": "01a106ad-fbf8-75b3-9fec-536538abc1b0"}	2026-10-04 18:30:27.960252+07	2026-10-04 18:30:27.992084+07	1	\N
01a106ad-fc13-7d2f-a6a7-23799b16281c	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fc13-7d2f-a6a7-23799b16281c", "journalId": "01a106ad-fc0c-7c20-bf49-6f4d9b71a167", "occurredOnUtc": "2026-10-04T11:30:27.987186Z"}	2026-10-04 18:30:27.987186+07	2026-10-04 18:30:27.9932+07	1	\N
01a106ad-fca1-70b0-8d44-9fb499834ab6	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fca1-70b0-8d44-9fb499834ab6", "occurredOnUtc": "2026-10-04T11:30:28.1292974Z", "dailyRecordingId": "01a106ad-fc95-7669-9952-a41217db083d"}	2026-10-04 18:30:28.129297+07	2026-10-04 18:30:28.138498+07	1	\N
01a106ad-fcd7-7ffc-a942-7ad4022797f5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fcd7-7ffc-a942-7ad4022797f5", "occurredOnUtc": "2026-10-04T11:30:28.1836269Z", "dailyRecordingId": "01a106ad-fccb-7a7f-ae91-dd67ac698fe8"}	2026-10-04 18:30:28.183626+07	2026-10-04 18:30:28.196776+07	1	\N
01a106ad-fd0b-77a2-8f6e-5c421f9d6de5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fd0b-77a2-8f6e-5c421f9d6de5", "occurredOnUtc": "2026-10-04T11:30:28.2350747Z", "dailyRecordingId": "01a106ad-fd00-7887-b6a2-16b1538dc55c"}	2026-10-04 18:30:28.235074+07	2026-10-04 18:30:28.246307+07	1	\N
01a106ad-fd73-715a-a32c-2518829b4e1c	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fd73-715a-a32c-2518829b4e1c", "journalId": "01a106ad-fd6d-7802-9116-80c610542410", "occurredOnUtc": "2026-10-04T11:30:28.3394547Z"}	2026-10-04 18:30:28.339454+07	2026-10-04 18:30:28.34378+07	1	\N
01a106ad-fa12-751d-93a4-b44486f17f1d	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fa12-751d-93a4-b44486f17f1d", "journalId": "01a106ad-fa0b-7a28-8d9b-b39b05d718cc", "occurredOnUtc": "2026-10-04T11:30:27.4741522Z"}	2026-10-04 18:30:27.474152+07	2026-10-04 18:30:27.506851+07	1	\N
01a106ad-fa2e-7fd1-a46f-d1fe71f50e4b	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fa2e-7fd1-a46f-d1fe71f50e4b", "journalId": "01a106ad-fa28-75b5-9006-a50428667447", "occurredOnUtc": "2026-10-04T11:30:27.5020087Z"}	2026-10-04 18:30:27.502008+07	2026-10-04 18:30:27.507315+07	1	\N
01a106ad-fb24-7095-a5cf-0d5b65aa4563	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fb24-7095-a5cf-0d5b65aa4563", "occurredOnUtc": "2026-10-04T11:30:27.7488216Z", "dailyRecordingId": "01a106ad-fb17-72d4-95e8-2ae27e0b4db8"}	2026-10-04 18:30:27.748821+07	2026-10-04 18:30:27.760176+07	1	\N
01a106ad-fb3c-7fc8-ab96-f61f71e1d000	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fb3c-7fc8-ab96-f61f71e1d000", "occurredOnUtc": "2026-10-04T11:30:27.7728512Z", "dailyRecordingId": "01a106ad-fb31-75b1-ad43-630959b993b1"}	2026-10-04 18:30:27.772851+07	2026-10-04 18:30:27.786173+07	1	\N
01a106ad-fb53-7a25-b924-b6ed08d59c5b	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ad-fb53-7a25-b924-b6ed08d59c5b", "occurredOnUtc": "2026-10-04T11:30:27.7950714Z", "bankTransferId": "01a106ad-fb53-79b4-8551-6efc939d31a9"}	2026-10-04 18:30:27.795071+07	2026-10-04 18:30:27.820241+07	1	\N
01a106ad-fb68-7f4e-8843-2a65b24e5d5e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fb68-7f4e-8843-2a65b24e5d5e", "journalId": "01a106ad-fb62-79ad-a4bd-9d9e39f32971", "occurredOnUtc": "2026-10-04T11:30:27.8164688Z"}	2026-10-04 18:30:27.816468+07	2026-10-04 18:30:27.821104+07	1	\N
01a106ad-fb9d-712f-b13e-0ef74aa58cb3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fb9d-712f-b13e-0ef74aa58cb3", "occurredOnUtc": "2026-10-04T11:30:27.8695377Z", "dailyRecordingId": "01a106ad-fb90-7002-89c5-5a70073c5a9f"}	2026-10-04 18:30:27.869537+07	2026-10-04 18:30:27.895024+07	1	\N
01a106ad-fbde-701a-b107-12f23800236d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fbde-701a-b107-12f23800236d", "occurredOnUtc": "2026-10-04T11:30:27.9342977Z", "dailyRecordingId": "01a106ad-fbd1-7df8-be16-f9b104681e11"}	2026-10-04 18:30:27.934297+07	2026-10-04 18:30:27.94436+07	1	\N
01a106ad-fc60-796c-8ec8-fd630c914e0f	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-fc60-796c-8ec8-fd630c914e0f", "occurredOnUtc": "2026-10-04T11:30:28.0643427Z", "vendorInvoiceId": "01a106ad-fc4d-79a2-aa5d-f3a2316a240d"}	2026-10-04 18:30:28.064342+07	2026-10-04 18:30:28.094334+07	1	\N
01a106ad-fc7a-7f18-be9d-ba44f02e6c62	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fc7a-7f18-be9d-ba44f02e6c62", "journalId": "01a106ad-fc73-7d33-b8eb-db807c642ffc", "occurredOnUtc": "2026-10-04T11:30:28.0905581Z"}	2026-10-04 18:30:28.090558+07	2026-10-04 18:30:28.095138+07	1	\N
01a106ad-fc8b-7c21-b61e-cc36fa19f0cc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fc8b-7c21-b61e-cc36fa19f0cc", "occurredOnUtc": "2026-10-04T11:30:28.1079147Z", "dailyRecordingId": "01a106ad-fc7f-765a-9179-ca596c6ec270"}	2026-10-04 18:30:28.107914+07	2026-10-04 18:30:28.117039+07	1	\N
01a106ad-fcf1-79f1-a2cf-b2ab4f88eca4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fcf1-79f1-a2cf-b2ab4f88eca4", "occurredOnUtc": "2026-10-04T11:30:28.2096889Z", "dailyRecordingId": "01a106ad-fce5-7862-abc0-cd4a846194fc"}	2026-10-04 18:30:28.209688+07	2026-10-04 18:30:28.224221+07	1	\N
01a106ad-fd58-7c97-b027-591a65bdb828	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-fd58-7c97-b027-591a65bdb828", "occurredOnUtc": "2026-10-04T11:30:28.312887Z", "salesInvoiceId": "01a106ad-fd40-7640-a517-a313d071cc33"}	2026-10-04 18:30:28.312887+07	2026-10-04 18:30:28.342772+07	1	\N
01a106ad-fd84-7d81-9fbe-8bfc7b3060a0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fd84-7d81-9fbe-8bfc7b3060a0", "occurredOnUtc": "2026-10-04T11:30:28.3564161Z", "dailyRecordingId": "01a106ad-fd78-7862-aeca-710b6952e364"}	2026-10-04 18:30:28.356416+07	2026-10-04 18:30:28.366319+07	1	\N
01a106ad-fdc5-73d1-aff3-b1520cf2b868	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-fdc5-73d1-aff3-b1520cf2b868", "occurredOnUtc": "2026-10-04T11:30:28.4212819Z", "vendorInvoiceId": "01a106ad-fda0-719c-b3f0-c5c94cd22245"}	2026-10-04 18:30:28.421281+07	2026-10-04 18:30:28.464322+07	1	\N
01a106ad-fde8-7162-8204-f97b90a4ba1b	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fde8-7162-8204-f97b90a4ba1b", "journalId": "01a106ad-fde1-7d54-8079-79b1ceb20de4", "occurredOnUtc": "2026-10-04T11:30:28.4563229Z"}	2026-10-04 18:30:28.456322+07	2026-10-04 18:30:28.465575+07	1	\N
01a106ad-fe1f-7f7d-96e3-bc623fc64d3c	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ad-fe1f-7f7d-96e3-bc623fc64d3c", "occurredOnUtc": "2026-10-04T11:30:28.5110476Z", "vendorInvoiceId": "01a106ad-fe07-7901-b2df-291722977f96"}	2026-10-04 18:30:28.511047+07	2026-10-04 18:30:28.545259+07	1	\N
01a106ad-fe3b-727d-a3df-20a98f416104	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fe3b-727d-a3df-20a98f416104", "journalId": "01a106ad-fe34-7757-9761-885d79b044c5", "occurredOnUtc": "2026-10-04T11:30:28.5397158Z"}	2026-10-04 18:30:28.539715+07	2026-10-04 18:30:28.54614+07	1	\N
01a106ad-fe4f-7093-99f5-fef5053af1d3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fe4f-7093-99f5-fef5053af1d3", "occurredOnUtc": "2026-10-04T11:30:28.5596288Z", "dailyRecordingId": "01a106ad-fe42-7bd8-97e1-d6e9352b6ba9"}	2026-10-04 18:30:28.559628+07	2026-10-04 18:30:28.570326+07	1	\N
01a106ad-fe67-7c4d-999f-a1fdcb48cad4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-fe67-7c4d-999f-a1fdcb48cad4", "occurredOnUtc": "2026-10-04T11:30:28.5831606Z", "dailyRecordingId": "01a106ad-fe5b-7f78-8851-e48768b1eaa2"}	2026-10-04 18:30:28.58316+07	2026-10-04 18:30:28.594238+07	1	\N
01a106ad-fec1-732a-be2d-accb6e0944e6	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-fec1-732a-be2d-accb6e0944e6", "occurredOnUtc": "2026-10-04T11:30:28.6734452Z", "salesInvoiceId": "01a106ad-fea7-70cb-af7e-26337a183541"}	2026-10-04 18:30:28.673445+07	2026-10-04 18:30:28.708798+07	1	\N
01a106ad-fef5-72a3-9f3a-b17c58b0a419	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ad-fef5-72a3-9f3a-b17c58b0a419", "occurredOnUtc": "2026-10-04T11:30:28.7258571Z", "stockTransferId": "01a106ad-fef5-7741-ac7c-fa61f92a0a1e"}	2026-10-04 18:30:28.725857+07	2026-10-04 18:30:28.761345+07	1	\N
01a106ad-ff15-72db-8ade-5b9b9e697022	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ff15-72db-8ade-5b9b9e697022", "journalId": "01a106ad-ff0f-751b-9b28-931d96f22b16", "occurredOnUtc": "2026-10-04T11:30:28.7577957Z"}	2026-10-04 18:30:28.757795+07	2026-10-04 18:30:28.762125+07	1	\N
01a106ad-ff6f-7a18-8600-7c479ad6d5e5	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ad-ff6f-7a18-8600-7c479ad6d5e5", "occurredOnUtc": "2026-10-04T11:30:28.8475878Z", "salesInvoiceId": "01a106ad-ff5a-73aa-bb0b-f6a7a6cff728"}	2026-10-04 18:30:28.847587+07	2026-10-04 18:30:28.875453+07	1	\N
01a106ad-ff87-7cdd-b2a6-5bca0139b8ff	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ff87-7cdd-b2a6-5bca0139b8ff", "journalId": "01a106ad-ff81-7290-9431-7059f7c9daf4", "occurredOnUtc": "2026-10-04T11:30:28.8711047Z"}	2026-10-04 18:30:28.871104+07	2026-10-04 18:30:28.876398+07	1	\N
01a106ad-ff97-7a8c-a5bc-76d5ad3e433a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ff97-7a8c-a5bc-76d5ad3e433a", "occurredOnUtc": "2026-10-04T11:30:28.8873561Z", "dailyRecordingId": "01a106ad-ff8d-78d2-b1a6-c0063ec87f49"}	2026-10-04 18:30:28.887356+07	2026-10-04 18:30:28.896037+07	1	\N
01a106ad-fede-7bf2-ac02-19e161d7ce39	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-fede-7bf2-ac02-19e161d7ce39", "journalId": "01a106ad-fed8-7db6-9dc0-20e930e7b2b9", "occurredOnUtc": "2026-10-04T11:30:28.7025082Z"}	2026-10-04 18:30:28.702508+07	2026-10-04 18:30:28.710275+07	1	\N
01a106ad-ff26-7d3c-9bdb-8bbffa4ad8a8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ff26-7d3c-9bdb-8bbffa4ad8a8", "occurredOnUtc": "2026-10-04T11:30:28.7749867Z", "dailyRecordingId": "01a106ad-ff1a-723a-bd71-f4a0c33f6f7b"}	2026-10-04 18:30:28.774986+07	2026-10-04 18:30:28.787348+07	1	\N
01a106ad-ff3f-7dbe-9aad-e5b135b5d56e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ff3f-7dbe-9aad-e5b135b5d56e", "occurredOnUtc": "2026-10-04T11:30:28.7997496Z", "dailyRecordingId": "01a106ad-ff34-7ae4-834e-f02e7b6ebd31"}	2026-10-04 18:30:28.799749+07	2026-10-04 18:30:28.812569+07	1	\N
01a106ad-ffac-75e1-8a69-5bda554b07b2	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ad-ffac-75e1-8a69-5bda554b07b2", "occurredOnUtc": "2026-10-04T11:30:28.9081584Z", "dailyRecordingId": "01a106ad-ffa0-73f8-a95e-0e84bd106901"}	2026-10-04 18:30:28.908158+07	2026-10-04 18:30:28.917064+07	1	\N
01a106ad-ffc3-70c0-a525-a6f47f5d62a6	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ad-ffc3-70c0-a525-a6f47f5d62a6", "occurredOnUtc": "2026-10-04T11:30:28.9312697Z", "customerReceiptId": "01a106ad-ffc3-77bc-a775-24cd57b8ec8a"}	2026-10-04 18:30:28.931269+07	2026-10-04 18:30:28.958181+07	1	\N
01a106ae-0095-78a1-856c-b899f624ba9a	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0095-78a1-856c-b899f624ba9a", "occurredOnUtc": "2026-10-04T11:30:29.1413357Z", "cashTransactionId": "01a106ae-007b-7591-82b9-19fa554b3ff7"}	2026-10-04 18:30:29.141335+07	2026-10-04 18:30:29.172965+07	1	\N
01a106ae-00ab-73da-a52e-c3acccca2b48	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-00ab-73da-a52e-c3acccca2b48", "journalId": "01a106ae-00a5-7372-89db-e96f100c0254", "occurredOnUtc": "2026-10-04T11:30:29.1636482Z"}	2026-10-04 18:30:29.163648+07	2026-10-04 18:30:29.174355+07	1	\N
01a106ae-00eb-7523-b105-507ca050ec0d	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-00eb-7523-b105-507ca050ec0d", "occurredOnUtc": "2026-10-04T11:30:29.2270661Z", "cashTransactionId": "01a106ae-00c4-7757-b345-d41c71a47167"}	2026-10-04 18:30:29.227066+07	2026-10-04 18:30:29.262423+07	1	\N
01a106ae-010a-7cb2-a0d3-f29ef5aec4fa	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-010a-7cb2-a0d3-f29ef5aec4fa", "journalId": "01a106ae-0105-7000-a2b5-7c7ec14a98ab", "occurredOnUtc": "2026-10-04T11:30:29.2588326Z"}	2026-10-04 18:30:29.258832+07	2026-10-04 18:30:29.263354+07	1	\N
01a106ae-013a-7943-bb15-69174699340e	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-013a-7943-bb15-69174699340e", "occurredOnUtc": "2026-10-04T11:30:29.3065447Z", "customerReceiptId": "01a106ae-013a-75ef-a556-ed8b25179089"}	2026-10-04 18:30:29.306544+07	2026-10-04 18:30:29.337143+07	1	\N
01a106ae-0155-7329-9a68-88c703aebb00	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0155-7329-9a68-88c703aebb00", "journalId": "01a106ae-014f-7ee2-a553-003f7b8b0f76", "occurredOnUtc": "2026-10-04T11:30:29.3330345Z"}	2026-10-04 18:30:29.333034+07	2026-10-04 18:30:29.338005+07	1	\N
01a106ae-0166-7fd2-a46c-e5c3af40b158	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0166-7fd2-a46c-e5c3af40b158", "occurredOnUtc": "2026-10-04T11:30:29.3502879Z", "dailyRecordingId": "01a106ae-015a-794c-9313-0df96d4c3d43"}	2026-10-04 18:30:29.350287+07	2026-10-04 18:30:29.360029+07	1	\N
01a106ae-0196-77f8-bd74-bd7e5e415d15	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-0196-77f8-bd74-bd7e5e415d15", "occurredOnUtc": "2026-10-04T11:30:29.3981255Z", "customerReceiptId": "01a106ae-0196-7c18-8152-cc1a334ef261"}	2026-10-04 18:30:29.398125+07	2026-10-04 18:30:29.425595+07	1	\N
01a106ae-01ad-7ec3-b6a2-fcd94e81a5b3	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-01ad-7ec3-b6a2-fcd94e81a5b3", "journalId": "01a106ae-01a7-7064-8e4b-cfd5d0ec1bca", "occurredOnUtc": "2026-10-04T11:30:29.4214657Z"}	2026-10-04 18:30:29.421465+07	2026-10-04 18:30:29.426526+07	1	\N
01a106ae-01e3-722a-81c2-b7480f12c963	Domain.Costing.PlasmaSettlements.PlasmaSettlementApprovedDomainEvent	{"id": "01a106ae-01e3-722a-81c2-b7480f12c963", "occurredOnUtc": "2026-10-04T11:30:29.4757543Z", "plasmaSettlementId": "01a106ae-01d0-7e6d-882c-ea0c9d6b45c3"}	2026-10-04 18:30:29.475754+07	2026-10-04 18:30:29.506753+07	1	\N
01a106ae-01fe-7b54-93cc-5a9aea6bdaa3	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-01fe-7b54-93cc-5a9aea6bdaa3", "journalId": "01a106ae-01f9-7dce-a036-139c96a56bea", "occurredOnUtc": "2026-10-04T11:30:29.5025673Z"}	2026-10-04 18:30:29.502567+07	2026-10-04 18:30:29.507847+07	1	\N
01a106ae-0210-74ff-93e1-ac1f2cf83dc7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0210-74ff-93e1-ac1f2cf83dc7", "occurredOnUtc": "2026-10-04T11:30:29.520773Z", "dailyRecordingId": "01a106ae-0204-78c2-96bb-80d88307bca4"}	2026-10-04 18:30:29.520773+07	2026-10-04 18:30:29.531123+07	1	\N
01a106ae-0227-7159-8e5d-6a1e045e3118	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0227-7159-8e5d-6a1e045e3118", "occurredOnUtc": "2026-10-04T11:30:29.5432928Z", "dailyRecordingId": "01a106ae-021b-73a8-92ee-9e9f2e435605"}	2026-10-04 18:30:29.543292+07	2026-10-04 18:30:29.555935+07	1	\N
01a106ae-023f-7f6b-a180-bf254a7d630c	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ae-023f-7f6b-a180-bf254a7d630c", "cycleId": "01a106ae-023f-704e-8a47-6e438951d196", "occurredOnUtc": "2026-10-04T11:30:29.567733Z"}	2026-10-04 18:30:29.567733+07	2026-10-04 18:30:29.572992+07	1	\N
01a106ae-02a8-7926-abb9-a11b5739819c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-02a8-7926-abb9-a11b5739819c", "occurredOnUtc": "2026-10-04T11:30:29.6723644Z", "dailyRecordingId": "01a106ae-029d-76e2-aba8-c69e90634772"}	2026-10-04 18:30:29.672364+07	2026-10-04 18:30:29.682088+07	1	\N
01a106ae-036b-763d-8f01-6d954de14cd4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-036b-763d-8f01-6d954de14cd4", "occurredOnUtc": "2026-10-04T11:30:29.8678432Z", "dailyRecordingId": "01a106ae-0361-767e-b8dd-182694843147"}	2026-10-04 18:30:29.867843+07	2026-10-04 18:30:29.880937+07	1	\N
01a106ae-0386-7414-bbae-449d82b2e259	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ae-0386-7414-bbae-449d82b2e259", "occurredOnUtc": "2026-10-04T11:30:29.8948789Z", "stockTransferId": "01a106ae-0386-7605-83ed-efc121166d55"}	2026-10-04 18:30:29.894878+07	2026-10-04 18:30:29.930916+07	1	\N
01a106ae-03a7-7332-b0ad-f8f1ce00384b	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-03a7-7332-b0ad-f8f1ce00384b", "journalId": "01a106ae-03a1-7fa6-9fa1-82e28c4c8796", "occurredOnUtc": "2026-10-04T11:30:29.9273424Z"}	2026-10-04 18:30:29.927342+07	2026-10-04 18:30:29.93214+07	1	\N
01a106ae-0469-7be1-9fd3-dedb840889f4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0469-7be1-9fd3-dedb840889f4", "occurredOnUtc": "2026-10-04T11:30:30.1212083Z", "dailyRecordingId": "01a106ae-045e-7bf5-9832-d2d0ed28357a"}	2026-10-04 18:30:30.121208+07	2026-10-04 18:30:30.130908+07	1	\N
01a106ae-0565-7849-8a37-c1afefaec9dd	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0565-7849-8a37-c1afefaec9dd", "occurredOnUtc": "2026-10-04T11:30:30.3736497Z", "dailyRecordingId": "01a106ae-055c-7886-b9b5-51dc49315d90"}	2026-10-04 18:30:30.373649+07	2026-10-04 18:30:30.381995+07	1	\N
01a106ae-059b-72af-9ddb-2b929bdcb580	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-059b-72af-9ddb-2b929bdcb580", "journalId": "01a106ae-0595-772e-9fe6-b077e3c48794", "occurredOnUtc": "2026-10-04T11:30:30.4274616Z"}	2026-10-04 18:30:30.427461+07	2026-10-04 18:30:30.4483+07	1	\N
01a106ad-ffda-7214-9c5b-81db3c6370a5	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ad-ffda-7214-9c5b-81db3c6370a5", "journalId": "01a106ad-ffd4-7897-b241-e3b5a8e65b80", "occurredOnUtc": "2026-10-04T11:30:28.9543791Z"}	2026-10-04 18:30:28.954379+07	2026-10-04 18:30:28.959046+07	1	\N
01a106ad-fff3-72f4-bee3-ac89056838d1	Domain.Inventory.StockReturns.StockReturnPostedDomainEvent	{"id": "01a106ad-fff3-72f4-bee3-ac89056838d1", "occurredOnUtc": "2026-10-04T11:30:28.9793457Z", "stockReturnId": "01a106ad-fff3-7186-9838-80f0249d67e4"}	2026-10-04 18:30:28.979345+07	2026-10-04 18:30:29.031854+07	1	\N
01a106ae-0023-7e97-99dd-9f7e04c5de83	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0023-7e97-99dd-9f7e04c5de83", "journalId": "01a106ae-001d-7e84-a783-4d0c2be0172e", "occurredOnUtc": "2026-10-04T11:30:29.027789Z"}	2026-10-04 18:30:29.027789+07	2026-10-04 18:30:29.0326+07	1	\N
01a106ae-0041-7180-8568-0d2cbc063cb1	Domain.Partnership.Cycles.CycleClosedDomainEvent	{"id": "01a106ae-0041-7180-8568-0d2cbc063cb1", "cycleId": "01a106ad-d332-7f38-9c34-260051b68ef3", "occurredOnUtc": "2026-10-04T11:30:29.0571882Z"}	2026-10-04 18:30:29.057188+07	2026-10-04 18:30:29.084611+07	1	\N
01a106ae-0059-7114-80ec-817d83f83e80	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0059-7114-80ec-817d83f83e80", "journalId": "01a106ae-0053-716b-a329-b6533e697c99", "occurredOnUtc": "2026-10-04T11:30:29.0812629Z"}	2026-10-04 18:30:29.081262+07	2026-10-04 18:30:29.08598+07	1	\N
01a106ae-0069-7998-9f7b-452c60ad7b50	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0069-7998-9f7b-452c60ad7b50", "occurredOnUtc": "2026-10-04T11:30:29.0978335Z", "dailyRecordingId": "01a106ae-005f-7097-a4cb-5815027dc7fd"}	2026-10-04 18:30:29.097833+07	2026-10-04 18:30:29.108094+07	1	\N
01a106ae-011c-7b4d-9bce-e6fb3504c6d5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-011c-7b4d-9bce-e6fb3504c6d5", "occurredOnUtc": "2026-10-04T11:30:29.2761593Z", "dailyRecordingId": "01a106ae-010f-7aa1-9509-6d09c5f5b4fc"}	2026-10-04 18:30:29.276159+07	2026-10-04 18:30:29.290495+07	1	\N
01a106ae-017c-77a1-8aec-640af971c74f	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-017c-77a1-8aec-640af971c74f", "occurredOnUtc": "2026-10-04T11:30:29.3729766Z", "dailyRecordingId": "01a106ae-0170-7ed9-88d0-573ae9d49cff"}	2026-10-04 18:30:29.372976+07	2026-10-04 18:30:29.382579+07	1	\N
01a106ae-02d3-7adf-b6dc-701ef3110805	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-02d3-7adf-b6dc-701ef3110805", "occurredOnUtc": "2026-10-04T11:30:29.7157825Z", "cashTransactionId": "01a106ae-02ba-7700-831b-a880d12dfc87"}	2026-10-04 18:30:29.715782+07	2026-10-04 18:30:29.743382+07	1	\N
01a106ae-02eb-7d51-a476-2dfa5d29a41e	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-02eb-7d51-a476-2dfa5d29a41e", "journalId": "01a106ae-02e4-7fc6-839d-6c7f7436a56b", "occurredOnUtc": "2026-10-04T11:30:29.7392169Z"}	2026-10-04 18:30:29.739216+07	2026-10-04 18:30:29.744433+07	1	\N
01a106ae-0311-74e9-8e5e-8e139bfd8a83	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0311-74e9-8e5e-8e139bfd8a83", "occurredOnUtc": "2026-10-04T11:30:29.7771569Z", "cashTransactionId": "01a106ae-02f8-78a9-8289-ceb1824a7512"}	2026-10-04 18:30:29.777156+07	2026-10-04 18:30:29.80217+07	1	\N
01a106ae-0325-75cf-bd37-58ec7f0c58e1	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0325-75cf-bd37-58ec7f0c58e1", "journalId": "01a106ae-0320-734d-b9f0-27f7753d22ae", "occurredOnUtc": "2026-10-04T11:30:29.7978345Z"}	2026-10-04 18:30:29.797834+07	2026-10-04 18:30:29.803647+07	1	\N
01a106ae-0338-76b2-ad06-78b75fd82fcc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0338-76b2-ad06-78b75fd82fcc", "occurredOnUtc": "2026-10-04T11:30:29.8161353Z", "dailyRecordingId": "01a106ae-032c-700a-a492-30a350f591a8"}	2026-10-04 18:30:29.816135+07	2026-10-04 18:30:29.826982+07	1	\N
01a106ae-03b8-7847-8d32-6298e33928fe	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-03b8-7847-8d32-6298e33928fe", "occurredOnUtc": "2026-10-04T11:30:29.9445871Z", "dailyRecordingId": "01a106ae-03ac-79dd-b23e-b3d746fa5b3f"}	2026-10-04 18:30:29.944587+07	2026-10-04 18:30:29.956486+07	1	\N
01a106ae-03dc-77a7-8669-8467dc961bc4	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-03dc-77a7-8669-8467dc961bc4", "occurredOnUtc": "2026-10-04T11:30:29.9806541Z", "goodsReceiptId": "01a106ae-03dc-71bc-8c80-5622ee97290b"}	2026-10-04 18:30:29.980654+07	2026-10-04 18:30:30.029752+07	1	\N
01a106ae-0408-76be-9fc5-0a5bf2185e59	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0408-76be-9fc5-0a5bf2185e59", "journalId": "01a106ae-0401-7b20-a576-5be500d84c7d", "occurredOnUtc": "2026-10-04T11:30:30.0248848Z"}	2026-10-04 18:30:30.024884+07	2026-10-04 18:30:30.031461+07	1	\N
01a106ae-041e-7b63-8a4b-b51e7c89fa6e	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-041e-7b63-8a4b-b51e7c89fa6e", "occurredOnUtc": "2026-10-04T11:30:30.0461262Z", "goodsReceiptId": "01a106ae-041e-7b65-83ea-b89df6274edb"}	2026-10-04 18:30:30.046126+07	2026-10-04 18:30:30.086838+07	1	\N
01a106ae-0443-7035-b19b-c777c0d92f6a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0443-7035-b19b-c777c0d92f6a", "journalId": "01a106ae-043e-73c9-9b24-7810ed975aa6", "occurredOnUtc": "2026-10-04T11:30:30.0830128Z"}	2026-10-04 18:30:30.083012+07	2026-10-04 18:30:30.087786+07	1	\N
01a106ae-0453-79eb-bb04-9e265ac5b976	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0453-79eb-bb04-9e265ac5b976", "occurredOnUtc": "2026-10-04T11:30:30.099014Z", "dailyRecordingId": "01a106ae-0448-7c6a-b799-54205ec2ae35"}	2026-10-04 18:30:30.099014+07	2026-10-04 18:30:30.109382+07	1	\N
01a106ae-049b-78fe-8d36-4adf674fa038	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-049b-78fe-8d36-4adf674fa038", "occurredOnUtc": "2026-10-04T11:30:30.1710849Z", "paymentVoucherId": "01a106ae-0481-70c6-ab29-7becb50fede2"}	2026-10-04 18:30:30.171084+07	2026-10-04 18:30:30.200177+07	1	\N
01a106ae-04b5-71df-8b41-62d606da1bab	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-04b5-71df-8b41-62d606da1bab", "journalId": "01a106ae-04af-7c5c-a900-da81ee876be0", "occurredOnUtc": "2026-10-04T11:30:30.1971068Z"}	2026-10-04 18:30:30.197106+07	2026-10-04 18:30:30.201091+07	1	\N
01a106ae-04e5-70d1-8365-78809ec64af1	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-04e5-70d1-8365-78809ec64af1", "occurredOnUtc": "2026-10-04T11:30:30.2450942Z", "paymentVoucherId": "01a106ae-04c9-72f8-9b03-0e6b93c00653"}	2026-10-04 18:30:30.245094+07	2026-10-04 18:30:30.276012+07	1	\N
01a106ae-04ff-7d61-9926-c0ebd05b5115	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-04ff-7d61-9926-c0ebd05b5115", "journalId": "01a106ae-04f9-73ac-ac22-5fc475241132", "occurredOnUtc": "2026-10-04T11:30:30.2715192Z"}	2026-10-04 18:30:30.271519+07	2026-10-04 18:30:30.277013+07	1	\N
01a106ae-052c-70cf-b368-daf76c6471e3	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-052c-70cf-b368-daf76c6471e3", "occurredOnUtc": "2026-10-04T11:30:30.316122Z", "paymentVoucherId": "01a106ae-0512-7698-bcc9-2bffa6c8c0a1"}	2026-10-04 18:30:30.316122+07	2026-10-04 18:30:30.343339+07	1	\N
01a106ae-0544-711b-957e-dcd62b48d08a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0544-711b-957e-dcd62b48d08a", "journalId": "01a106ae-053e-7dc2-b547-f51a986f6465", "occurredOnUtc": "2026-10-04T11:30:30.3400205Z"}	2026-10-04 18:30:30.34002+07	2026-10-04 18:30:30.344296+07	1	\N
01a106ae-0552-7426-a0e9-487ecf37dd86	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0552-7426-a0e9-487ecf37dd86", "occurredOnUtc": "2026-10-04T11:30:30.3548438Z", "dailyRecordingId": "01a106ae-0548-775f-a6f5-56e4863fa3b2"}	2026-10-04 18:30:30.354843+07	2026-10-04 18:30:30.363368+07	1	\N
01a106ae-057e-7181-a719-2c59362b5d5b	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-057e-7181-a719-2c59362b5d5b", "occurredOnUtc": "2026-10-04T11:30:30.398981Z", "goodsReceiptId": "01a106ae-057e-79b5-8334-150eb98cd9a0"}	2026-10-04 18:30:30.398981+07	2026-10-04 18:30:30.44756+07	1	\N
01a106ae-05ac-7d21-b550-8dfad1f06f9a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-05ac-7d21-b550-8dfad1f06f9a", "journalId": "01a106ae-05a6-75a0-bb64-41c283809d3e", "occurredOnUtc": "2026-10-04T11:30:30.4444647Z"}	2026-10-04 18:30:30.444464+07	2026-10-04 18:30:30.448473+07	1	\N
01a106ae-05b8-7cd6-88c0-479bf11c86cb	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ae-05b8-7cd6-88c0-479bf11c86cb", "cycleId": "01a106ae-023f-704e-8a47-6e438951d196", "occurredOnUtc": "2026-10-04T11:30:30.456169Z"}	2026-10-04 18:30:30.456169+07	2026-10-04 18:30:30.463833+07	1	\N
01a106ae-05cc-79fe-9163-4646afac09f3	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ae-05cc-79fe-9163-4646afac09f3", "occurredOnUtc": "2026-10-04T11:30:30.4767426Z", "stockTransferId": "01a106ae-05cc-7acb-b7a1-4ff93a12d78c"}	2026-10-04 18:30:30.476742+07	2026-10-04 18:30:30.525003+07	1	\N
01a106ae-05f8-709e-b89c-e46c78c79626	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-05f8-709e-b89c-e46c78c79626", "journalId": "01a106ae-05f2-717c-a2bf-af8a5601441c", "occurredOnUtc": "2026-10-04T11:30:30.5206716Z"}	2026-10-04 18:30:30.520671+07	2026-10-04 18:30:30.525838+07	1	\N
01a106ae-0609-7e80-964b-9e08f9f3565c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0609-7e80-964b-9e08f9f3565c", "occurredOnUtc": "2026-10-04T11:30:30.5378268Z", "dailyRecordingId": "01a106ae-05fe-721b-9d53-76ef30ada2e8"}	2026-10-04 18:30:30.537826+07	2026-10-04 18:30:30.546448+07	1	\N
01a106ae-0632-7488-b164-730659daf3df	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0632-7488-b164-730659daf3df", "occurredOnUtc": "2026-10-04T11:30:30.5782262Z", "cashTransactionId": "01a106ae-061a-735c-aa26-ced227719ea4"}	2026-10-04 18:30:30.578226+07	2026-10-04 18:30:30.602802+07	1	\N
01a106ae-06a8-7141-9347-21baefb07e48	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-06a8-7141-9347-21baefb07e48", "occurredOnUtc": "2026-10-04T11:30:30.6961066Z", "dailyRecordingId": "01a106ae-069d-7dd8-ba89-7e3a7e50e393"}	2026-10-04 18:30:30.696106+07	2026-10-04 18:30:30.704765+07	1	\N
01a106ae-0731-79d9-adc5-8a3e02d4f397	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0731-79d9-adc5-8a3e02d4f397", "occurredOnUtc": "2026-10-04T11:30:30.8338331Z", "cashTransactionId": "01a106ae-0723-755d-afb4-7a861862a333"}	2026-10-04 18:30:30.833833+07	2026-10-04 18:30:30.860392+07	1	\N
01a106ae-0748-720c-8605-c312a60d6312	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0748-720c-8605-c312a60d6312", "journalId": "01a106ae-0742-7dcd-b168-1188647a8b77", "occurredOnUtc": "2026-10-04T11:30:30.856754Z"}	2026-10-04 18:30:30.856754+07	2026-10-04 18:30:30.861152+07	1	\N
01a106ae-0758-7c34-ac8e-e2f80b5bddf9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0758-7c34-ac8e-e2f80b5bddf9", "occurredOnUtc": "2026-10-04T11:30:30.8721379Z", "dailyRecordingId": "01a106ae-074d-7370-91e2-14c452cfc5f0"}	2026-10-04 18:30:30.872137+07	2026-10-04 18:30:30.88148+07	1	\N
01a106ae-076d-7f3b-9269-aa7e21de630b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-076d-7f3b-9269-aa7e21de630b", "occurredOnUtc": "2026-10-04T11:30:30.8934001Z", "dailyRecordingId": "01a106ae-0762-7099-98f5-0b0394a98fd6"}	2026-10-04 18:30:30.8934+07	2026-10-04 18:30:30.904809+07	1	\N
01a106ae-079b-7ed1-b95a-c564ea41cd51	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-079b-7ed1-b95a-c564ea41cd51", "occurredOnUtc": "2026-10-04T11:30:30.9391567Z", "dailyRecordingId": "01a106ae-078f-72a8-9693-b4fd659b10da"}	2026-10-04 18:30:30.939156+07	2026-10-04 18:30:30.9492+07	1	\N
01a106ae-0808-7e8a-ad1b-e74b7275ee66	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0808-7e8a-ad1b-e74b7275ee66", "occurredOnUtc": "2026-10-04T11:30:31.0480062Z", "dailyRecordingId": "01a106ae-07fd-7667-8034-27b86e55460e"}	2026-10-04 18:30:31.048006+07	2026-10-04 18:30:31.058995+07	1	\N
01a106ae-081f-7a26-bd4f-c2d2549cde98	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-081f-7a26-bd4f-c2d2549cde98", "occurredOnUtc": "2026-10-04T11:30:31.0711498Z", "dailyRecordingId": "01a106ae-0813-70d6-919f-2373ea0ba65b"}	2026-10-04 18:30:31.071149+07	2026-10-04 18:30:31.082126+07	1	\N
01a106ae-0852-7563-9813-553347c200d9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0852-7563-9813-553347c200d9", "occurredOnUtc": "2026-10-04T11:30:31.1224276Z", "dailyRecordingId": "01a106ae-0847-7969-8acb-4e9c2655c245"}	2026-10-04 18:30:31.122427+07	2026-10-04 18:30:31.131856+07	1	\N
01a106ae-0898-7a0f-b28a-dbc53959b9c5	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-0898-7a0f-b28a-dbc53959b9c5", "occurredOnUtc": "2026-10-04T11:30:31.1929837Z", "vendorInvoiceId": "01a106ae-0884-733e-afe1-4bc7b7c64b7a"}	2026-10-04 18:30:31.192983+07	2026-10-04 18:30:31.221006+07	1	\N
01a106ae-08b0-7db2-a392-a8586a6cb1c6	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-08b0-7db2-a392-a8586a6cb1c6", "journalId": "01a106ae-08aa-7373-b8d1-bac9b4ad3359", "occurredOnUtc": "2026-10-04T11:30:31.2169461Z"}	2026-10-04 18:30:31.216946+07	2026-10-04 18:30:31.221761+07	1	\N
01a106ae-08dd-7854-b181-a0174e32d6f9	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-08dd-7854-b181-a0174e32d6f9", "occurredOnUtc": "2026-10-04T11:30:31.2613116Z", "vendorInvoiceId": "01a106ae-08c9-7e22-be84-4e4ff54c1e08"}	2026-10-04 18:30:31.261311+07	2026-10-04 18:30:31.288909+07	1	\N
01a106ae-08f4-7a5d-8e6d-928b0d55aed6	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-08f4-7a5d-8e6d-928b0d55aed6", "journalId": "01a106ae-08ef-75e1-a541-50f16deea0ce", "occurredOnUtc": "2026-10-04T11:30:31.2845496Z"}	2026-10-04 18:30:31.284549+07	2026-10-04 18:30:31.28998+07	1	\N
01a106ae-0904-7b90-993d-5c30508c8f78	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0904-7b90-993d-5c30508c8f78", "occurredOnUtc": "2026-10-04T11:30:31.3008221Z", "dailyRecordingId": "01a106ae-08fa-7281-ad5c-b5e0dc176cce"}	2026-10-04 18:30:31.300822+07	2026-10-04 18:30:31.310615+07	1	\N
01a106ae-0935-724b-8807-8b2da4e08657	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ae-0935-724b-8807-8b2da4e08657", "cycleId": "01a106ae-0935-71d7-8a79-528d2a90b371", "occurredOnUtc": "2026-10-04T11:30:31.3491988Z"}	2026-10-04 18:30:31.349198+07	2026-10-04 18:30:31.353849+07	1	\N
01a106ae-09c8-7937-878e-943e59d9ef8a	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-09c8-7937-878e-943e59d9ef8a", "occurredOnUtc": "2026-10-04T11:30:31.4965026Z", "paymentVoucherId": "01a106ae-099b-79ca-a72e-a4ed31416c6c"}	2026-10-04 18:30:31.496502+07	2026-10-04 18:30:31.544714+07	1	\N
01a106ae-09f4-7b7f-9ea0-a6afb43f73b2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-09f4-7b7f-9ea0-a6afb43f73b2", "journalId": "01a106ae-09ec-7718-a4e0-6a7fb6da6035", "occurredOnUtc": "2026-10-04T11:30:31.540409Z"}	2026-10-04 18:30:31.540409+07	2026-10-04 18:30:31.545757+07	1	\N
01a106ae-0a22-7255-a8d4-d5ba146afcc4	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-0a22-7255-a8d4-d5ba146afcc4", "occurredOnUtc": "2026-10-04T11:30:31.5869717Z", "paymentVoucherId": "01a106ae-0a09-7aae-b43a-b8080ee12f35"}	2026-10-04 18:30:31.586971+07	2026-10-04 18:30:31.643354+07	1	\N
01a106ae-0a57-72ab-a4a1-d1571f66ca1d	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0a57-72ab-a4a1-d1571f66ca1d", "journalId": "01a106ae-0a51-74db-abba-fa4860348b89", "occurredOnUtc": "2026-10-04T11:30:31.6397867Z"}	2026-10-04 18:30:31.639786+07	2026-10-04 18:30:31.644407+07	1	\N
01a106ae-0a67-785d-905f-f1cf0562b76c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0a67-785d-905f-f1cf0562b76c", "occurredOnUtc": "2026-10-04T11:30:31.6557117Z", "dailyRecordingId": "01a106ae-0a5d-7ea3-a3f4-e3e3fd08ab4d"}	2026-10-04 18:30:31.655711+07	2026-10-04 18:30:31.664233+07	1	\N
01a106ae-0647-7a13-b1a1-bf578335b7ba	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0647-7a13-b1a1-bf578335b7ba", "journalId": "01a106ae-0641-75a8-b57e-d0907784a9e4", "occurredOnUtc": "2026-10-04T11:30:30.5990164Z"}	2026-10-04 18:30:30.599016+07	2026-10-04 18:30:30.60377+07	1	\N
01a106ae-0669-7d76-8d10-93cfbc0cdf90	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0669-7d76-8d10-93cfbc0cdf90", "occurredOnUtc": "2026-10-04T11:30:30.6339734Z", "cashTransactionId": "01a106ae-0653-7037-b7d0-d463cef1f588"}	2026-10-04 18:30:30.633973+07	2026-10-04 18:30:30.660941+07	1	\N
01a106ae-0681-776d-9c72-83f44900566a	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0681-776d-9c72-83f44900566a", "journalId": "01a106ae-067b-7f72-9027-f0dcdc774c26", "occurredOnUtc": "2026-10-04T11:30:30.6576552Z"}	2026-10-04 18:30:30.657655+07	2026-10-04 18:30:30.661931+07	1	\N
01a106ae-0692-76ca-a630-8d8ec68def30	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0692-76ca-a630-8d8ec68def30", "occurredOnUtc": "2026-10-04T11:30:30.6743705Z", "dailyRecordingId": "01a106ae-0686-776f-aa77-3f6ec812b6da"}	2026-10-04 18:30:30.67437+07	2026-10-04 18:30:30.683984+07	1	\N
01a106ae-06c6-7119-935b-e4e7093c62c4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-06c6-7119-935b-e4e7093c62c4", "occurredOnUtc": "2026-10-04T11:30:30.7265631Z", "dailyRecordingId": "01a106ae-06b1-7ae2-bb62-82bb05d20a3f"}	2026-10-04 18:30:30.726563+07	2026-10-04 18:30:30.740653+07	1	\N
01a106ae-06f7-7dda-93eb-9e12b9b0634d	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-06f7-7dda-93eb-9e12b9b0634d", "occurredOnUtc": "2026-10-04T11:30:30.7755529Z", "cashTransactionId": "01a106ae-06e1-7167-810d-c915cbc4efcf"}	2026-10-04 18:30:30.775552+07	2026-10-04 18:30:30.810805+07	1	\N
01a106ae-0716-7da2-ab6a-948649bf6e53	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0716-7da2-ab6a-948649bf6e53", "journalId": "01a106ae-0711-75c9-903c-8d13b8c8f888", "occurredOnUtc": "2026-10-04T11:30:30.8069839Z"}	2026-10-04 18:30:30.806983+07	2026-10-04 18:30:30.811829+07	1	\N
01a106ae-0784-7bcc-bb0c-1ba892540ca7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0784-7bcc-bb0c-1ba892540ca7", "occurredOnUtc": "2026-10-04T11:30:30.916343Z", "dailyRecordingId": "01a106ae-0779-7971-9c4d-87a2fdd6ad0c"}	2026-10-04 18:30:30.916343+07	2026-10-04 18:30:30.926767+07	1	\N
01a106ae-07ca-73d5-b612-ffdfbc95444c	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-07ca-73d5-b612-ffdfbc95444c", "occurredOnUtc": "2026-10-04T11:30:30.9868911Z", "vendorInvoiceId": "01a106ae-07b7-73d5-accf-3820983cc082"}	2026-10-04 18:30:30.986891+07	2026-10-04 18:30:31.01511+07	1	\N
01a106ae-07e3-7121-90e2-179b4a02705c	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-07e3-7121-90e2-179b4a02705c", "journalId": "01a106ae-07dd-74a6-8b0d-0fea453851cf", "occurredOnUtc": "2026-10-04T11:30:31.0115272Z"}	2026-10-04 18:30:31.011527+07	2026-10-04 18:30:31.015854+07	1	\N
01a106ae-07f3-76fa-a3c8-63c8fda3dc64	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-07f3-76fa-a3c8-63c8fda3dc64", "occurredOnUtc": "2026-10-04T11:30:31.0279498Z", "dailyRecordingId": "01a106ae-07e8-70aa-8483-dde7e16f845e"}	2026-10-04 18:30:31.027949+07	2026-10-04 18:30:31.03651+07	1	\N
01a106ae-0838-7a1d-9d2f-f9fa91ed8581	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0838-7a1d-9d2f-f9fa91ed8581", "occurredOnUtc": "2026-10-04T11:30:31.0961079Z", "dailyRecordingId": "01a106ae-082a-70a0-bfb4-ff75d29c680d"}	2026-10-04 18:30:31.096107+07	2026-10-04 18:30:31.111116+07	1	\N
01a106ae-0868-7297-9e35-7f47cd1d0d78	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0868-7297-9e35-7f47cd1d0d78", "occurredOnUtc": "2026-10-04T11:30:31.1442146Z", "dailyRecordingId": "01a106ae-085c-712f-a271-039f95ca3b88"}	2026-10-04 18:30:31.144214+07	2026-10-04 18:30:31.153469+07	1	\N
01a106ae-0919-7477-b2c7-af653fe7fa65	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0919-7477-b2c7-af653fe7fa65", "occurredOnUtc": "2026-10-04T11:30:31.3213545Z", "dailyRecordingId": "01a106ae-090f-76fd-a291-b6b532ffc767"}	2026-10-04 18:30:31.321354+07	2026-10-04 18:30:31.329929+07	1	\N
01a106ae-0a7d-71b8-b62d-72042409619d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0a7d-71b8-b62d-72042409619d", "occurredOnUtc": "2026-10-04T11:30:31.6775025Z", "dailyRecordingId": "01a106ae-0a71-7407-a255-d2804cc8322c"}	2026-10-04 18:30:31.677502+07	2026-10-04 18:30:31.689582+07	1	\N
01a106ae-0a94-7bb4-bced-295a46adf2c3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0a94-7bb4-bced-295a46adf2c3", "occurredOnUtc": "2026-10-04T11:30:31.7007235Z", "dailyRecordingId": "01a106ae-0a8a-7c76-a7f2-005fe396f345"}	2026-10-04 18:30:31.700723+07	2026-10-04 18:30:31.710762+07	1	\N
01a106ae-0aab-7480-bf6e-395f38179bf5	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0aab-7480-bf6e-395f38179bf5", "occurredOnUtc": "2026-10-04T11:30:31.7237912Z", "dailyRecordingId": "01a106ae-0a9f-7ee6-8e45-5e1b57981e9f"}	2026-10-04 18:30:31.723791+07	2026-10-04 18:30:31.732151+07	1	\N
01a106ae-0ac0-7046-b58d-a279638ed3de	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0ac0-7046-b58d-a279638ed3de", "occurredOnUtc": "2026-10-04T11:30:31.7447478Z", "dailyRecordingId": "01a106ae-0ab4-7a29-8119-c4f4268fd80f"}	2026-10-04 18:30:31.744747+07	2026-10-04 18:30:31.754801+07	1	\N
01a106ae-0ad6-78ef-a446-20299d66ad2e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0ad6-78ef-a446-20299d66ad2e", "occurredOnUtc": "2026-10-04T11:30:31.7669981Z", "dailyRecordingId": "01a106ae-0acb-7dca-8e73-de3068249d61"}	2026-10-04 18:30:31.766998+07	2026-10-04 18:30:31.778605+07	1	\N
01a106ae-0af1-7304-9211-8cac146123ce	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-0af1-7304-9211-8cac146123ce", "occurredOnUtc": "2026-10-04T11:30:31.7938926Z", "goodsReceiptId": "01a106ae-0af1-7541-a16c-02d04ad0ab29"}	2026-10-04 18:30:31.793892+07	2026-10-04 18:30:31.829054+07	1	\N
01a106ae-0bf8-7502-9a1b-3ddbabf5dee1	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0bf8-7502-9a1b-3ddbabf5dee1", "occurredOnUtc": "2026-10-04T11:30:32.0562175Z", "dailyRecordingId": "01a106ae-0bed-7628-9f59-9dbe1f312708"}	2026-10-04 18:30:32.056217+07	2026-10-04 18:30:32.064993+07	1	\N
01a106ae-0c20-72ad-92cb-76987f8a0920	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0c20-72ad-92cb-76987f8a0920", "occurredOnUtc": "2026-10-04T11:30:32.0962722Z", "dailyRecordingId": "01a106ae-0c16-7f39-9ed0-06a7f0e90700"}	2026-10-04 18:30:32.096272+07	2026-10-04 18:30:32.104424+07	1	\N
01a106ae-0d2e-77ba-be27-1cbdaaae930e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d2e-77ba-be27-1cbdaaae930e", "occurredOnUtc": "2026-10-04T11:30:32.3668176Z", "dailyRecordingId": "01a106ae-0d23-7c75-823e-6741f4848f1a"}	2026-10-04 18:30:32.366817+07	2026-10-04 18:30:32.376644+07	1	\N
01a106ae-0d5b-7536-97f1-a6d95f4ca03a	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d5b-7536-97f1-a6d95f4ca03a", "occurredOnUtc": "2026-10-04T11:30:32.4116854Z", "dailyRecordingId": "01a106ae-0d4f-7f7c-be06-6a1a4ce3d083"}	2026-10-04 18:30:32.411685+07	2026-10-04 18:30:32.421826+07	1	\N
01a106ae-0d87-73b9-9e20-c8dde7cd2bc0	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d87-73b9-9e20-c8dde7cd2bc0", "occurredOnUtc": "2026-10-04T11:30:32.4556412Z", "dailyRecordingId": "01a106ae-0d7d-7469-bc0e-c08e85d36fbb"}	2026-10-04 18:30:32.455641+07	2026-10-04 18:30:32.464924+07	1	\N
01a106ae-0d9f-722e-8bc0-1d5826306714	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d9f-722e-8bc0-1d5826306714", "occurredOnUtc": "2026-10-04T11:30:32.4796087Z", "dailyRecordingId": "01a106ae-0d91-789b-aad9-9048c5bf8abb"}	2026-10-04 18:30:32.479608+07	2026-10-04 18:30:32.492926+07	1	\N
01a106ae-0e45-7167-bcb7-84a95ef4bbe1	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ae-0e45-7167-bcb7-84a95ef4bbe1", "occurredOnUtc": "2026-10-04T11:30:32.6452658Z", "stockTransferId": "01a106ae-0e45-7ed6-a7fc-6ba1fe729f41"}	2026-10-04 18:30:32.645265+07	2026-10-04 18:30:32.679064+07	1	\N
01a106ae-0b11-7b25-be93-78b7fd333512	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0b11-7b25-be93-78b7fd333512", "journalId": "01a106ae-0b0d-737b-b417-13d57deb26be", "occurredOnUtc": "2026-10-04T11:30:31.825701Z"}	2026-10-04 18:30:31.825701+07	2026-10-04 18:30:31.829925+07	1	\N
01a106ae-0b26-7740-b9b5-bacc00ad6ec7	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-0b26-7740-b9b5-bacc00ad6ec7", "occurredOnUtc": "2026-10-04T11:30:31.8467589Z", "goodsReceiptId": "01a106ae-0b26-7b4c-aa76-9672c7ce56d4"}	2026-10-04 18:30:31.846758+07	2026-10-04 18:30:31.885557+07	1	\N
01a106ae-0b49-7463-abd3-c1bf36da67e3	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0b49-7463-abd3-c1bf36da67e3", "journalId": "01a106ae-0b44-7d6a-a342-5a0d3a0d1ab2", "occurredOnUtc": "2026-10-04T11:30:31.8817956Z"}	2026-10-04 18:30:31.881795+07	2026-10-04 18:30:31.886671+07	1	\N
01a106ae-0b6f-796a-9f37-d94506e766b3	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0b6f-796a-9f37-d94506e766b3", "occurredOnUtc": "2026-10-04T11:30:31.9194071Z", "cashTransactionId": "01a106ae-0b57-747e-8f52-1af3cd63f0fa"}	2026-10-04 18:30:31.919407+07	2026-10-04 18:30:31.944685+07	1	\N
01a106ae-0b85-7535-b544-59c820920889	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0b85-7535-b544-59c820920889", "journalId": "01a106ae-0b7e-777a-8993-c95a6c22ac32", "occurredOnUtc": "2026-10-04T11:30:31.9412958Z"}	2026-10-04 18:30:31.941295+07	2026-10-04 18:30:31.945437+07	1	\N
01a106ae-0baa-72d9-bab8-e1ea9ecb1c9a	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0baa-72d9-bab8-e1ea9ecb1c9a", "occurredOnUtc": "2026-10-04T11:30:31.9780877Z", "cashTransactionId": "01a106ae-0b90-7193-a2d9-965f1358e7d7"}	2026-10-04 18:30:31.978087+07	2026-10-04 18:30:32.002161+07	1	\N
01a106ae-0bbd-7b47-a267-c10379a904b4	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0bbd-7b47-a267-c10379a904b4", "journalId": "01a106ae-0bb7-71ff-9fbd-1205c7025acd", "occurredOnUtc": "2026-10-04T11:30:31.9970807Z"}	2026-10-04 18:30:31.99708+07	2026-10-04 18:30:32.003377+07	1	\N
01a106ae-0bcd-7104-b15f-cb2e68889f83	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0bcd-7104-b15f-cb2e68889f83", "occurredOnUtc": "2026-10-04T11:30:32.013898Z", "dailyRecordingId": "01a106ae-0bc4-733f-98ed-a70ec3f5661c"}	2026-10-04 18:30:32.013898+07	2026-10-04 18:30:32.023878+07	1	\N
01a106ae-0be1-7ea2-9add-54cb51b09023	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0be1-7ea2-9add-54cb51b09023", "occurredOnUtc": "2026-10-04T11:30:32.0334066Z", "dailyRecordingId": "01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f"}	2026-10-04 18:30:32.033406+07	2026-10-04 18:30:32.044896+07	1	\N
01a106ae-0c0b-7fd8-81db-b7e6dd34adee	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0c0b-7fd8-81db-b7e6dd34adee", "occurredOnUtc": "2026-10-04T11:30:32.0756401Z", "dailyRecordingId": "01a106ae-0c01-7d34-92e3-55ed42fc7d93"}	2026-10-04 18:30:32.07564+07	2026-10-04 18:30:32.084677+07	1	\N
01a106ae-0c32-7007-b5b5-4ff88c600e19	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0c32-7007-b5b5-4ff88c600e19", "occurredOnUtc": "2026-10-04T11:30:32.1149527Z", "dailyRecordingId": "01a106ae-0c29-7f35-8143-a6df282f87cd"}	2026-10-04 18:30:32.114952+07	2026-10-04 18:30:32.12417+07	1	\N
01a106ae-0c4e-7961-9537-e84dd0bdc14c	Domain.Inventory.GoodsReceipts.GoodsReceiptPostedDomainEvent	{"id": "01a106ae-0c4e-7961-9537-e84dd0bdc14c", "occurredOnUtc": "2026-10-04T11:30:32.1425675Z", "goodsReceiptId": "01a106ae-0c4e-7273-b6e0-82abb0d929f9"}	2026-10-04 18:30:32.142567+07	2026-10-04 18:30:32.195485+07	1	\N
01a106ae-0c6c-70c8-bbb9-bf7520e703d7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0c6c-70c8-bbb9-bf7520e703d7", "journalId": "01a106ae-0c65-71ac-bd0c-77c1df781790", "occurredOnUtc": "2026-10-04T11:30:32.1720977Z"}	2026-10-04 18:30:32.172097+07	2026-10-04 18:30:32.19632+07	1	\N
01a106ae-0c80-7921-bcb8-e060fec6e996	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0c80-7921-bcb8-e060fec6e996", "journalId": "01a106ae-0c7a-75d9-b854-19e2cd456642", "occurredOnUtc": "2026-10-04T11:30:32.1923033Z"}	2026-10-04 18:30:32.192303+07	2026-10-04 18:30:32.196547+07	1	\N
01a106ae-0c8b-7783-b660-dcbed661e091	Domain.Partnership.Cycles.CycleStartedDomainEvent	{"id": "01a106ae-0c8b-7783-b660-dcbed661e091", "cycleId": "01a106ae-0935-71d7-8a79-528d2a90b371", "occurredOnUtc": "2026-10-04T11:30:32.2033401Z"}	2026-10-04 18:30:32.20334+07	2026-10-04 18:30:32.210853+07	1	\N
01a106ae-0ca1-74c3-9d7b-e0b4271fe815	Domain.Inventory.StockTransfers.StockTransferPostedDomainEvent	{"id": "01a106ae-0ca1-74c3-9d7b-e0b4271fe815", "occurredOnUtc": "2026-10-04T11:30:32.22542Z", "stockTransferId": "01a106ae-0ca1-70ed-af31-ee7d25077d50"}	2026-10-04 18:30:32.22542+07	2026-10-04 18:30:32.291386+07	1	\N
01a106ae-0cdc-7b43-9991-3210b34a5781	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0cdc-7b43-9991-3210b34a5781", "journalId": "01a106ae-0cd1-7dbb-b4ae-a888efe02084", "occurredOnUtc": "2026-10-04T11:30:32.2846687Z"}	2026-10-04 18:30:32.284668+07	2026-10-04 18:30:32.293398+07	1	\N
01a106ae-0cfb-7232-adff-e652dd30ba66	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0cfb-7232-adff-e652dd30ba66", "occurredOnUtc": "2026-10-04T11:30:32.3150376Z", "dailyRecordingId": "01a106ae-0ce6-7ed3-815d-d78acb8beb5a"}	2026-10-04 18:30:32.315037+07	2026-10-04 18:30:32.327209+07	1	\N
01a106ae-0d16-738d-958f-840a841aafd7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d16-738d-958f-840a841aafd7", "occurredOnUtc": "2026-10-04T11:30:32.3427688Z", "dailyRecordingId": "01a106ae-0d08-7312-8292-d25b316d3500"}	2026-10-04 18:30:32.342768+07	2026-10-04 18:30:32.354936+07	1	\N
01a106ae-0d46-7d56-acb3-6eb731127093	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d46-7d56-acb3-6eb731127093", "occurredOnUtc": "2026-10-04T11:30:32.3903252Z", "dailyRecordingId": "01a106ae-0d39-7f71-84bf-4f1de24ab449"}	2026-10-04 18:30:32.390325+07	2026-10-04 18:30:32.399237+07	1	\N
01a106ae-0d73-7c49-860e-02ac9247d8f7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0d73-7c49-860e-02ac9247d8f7", "occurredOnUtc": "2026-10-04T11:30:32.4352147Z", "dailyRecordingId": "01a106ae-0d66-7662-8183-b4e125081c37"}	2026-10-04 18:30:32.435214+07	2026-10-04 18:30:32.445079+07	1	\N
01a106ae-0dce-73f5-ad95-b5bd12e145ab	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0dce-73f5-ad95-b5bd12e145ab", "occurredOnUtc": "2026-10-04T11:30:32.5267258Z", "cashTransactionId": "01a106ae-0db5-734d-b34d-889cf46a1e2b"}	2026-10-04 18:30:32.526725+07	2026-10-04 18:30:32.549669+07	1	\N
01a106ae-0de2-772a-9c84-3e642ee52104	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0de2-772a-9c84-3e642ee52104", "journalId": "01a106ae-0ddc-768a-aaf3-243ff1d02fb8", "occurredOnUtc": "2026-10-04T11:30:32.5461947Z"}	2026-10-04 18:30:32.546194+07	2026-10-04 18:30:32.550472+07	1	\N
01a106ae-0e07-751c-99ae-cf4d4c1f0e8b	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-0e07-751c-99ae-cf4d4c1f0e8b", "occurredOnUtc": "2026-10-04T11:30:32.5839518Z", "cashTransactionId": "01a106ae-0def-71b8-a9fe-a9e0177f013a"}	2026-10-04 18:30:32.583951+07	2026-10-04 18:30:32.608417+07	1	\N
01a106ae-0e1c-764b-8a39-200866018fb5	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0e1c-764b-8a39-200866018fb5", "journalId": "01a106ae-0e16-7b72-9519-4096b36ff242", "occurredOnUtc": "2026-10-04T11:30:32.6048262Z"}	2026-10-04 18:30:32.604826+07	2026-10-04 18:30:32.609135+07	1	\N
01a106ae-0e2c-75de-a977-306c32620ff6	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0e2c-75de-a977-306c32620ff6", "occurredOnUtc": "2026-10-04T11:30:32.6206493Z", "dailyRecordingId": "01a106ae-0e21-7392-ac64-84e99d638855"}	2026-10-04 18:30:32.620649+07	2026-10-04 18:30:32.630489+07	1	\N
01a106ae-0e63-7be9-8601-7758c53e15a8	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0e63-7be9-8601-7758c53e15a8", "journalId": "01a106ae-0e5e-78c7-95f3-65758e5216fb", "occurredOnUtc": "2026-10-04T11:30:32.6757359Z"}	2026-10-04 18:30:32.675735+07	2026-10-04 18:30:32.679854+07	1	\N
01a106ae-0f04-7b30-97b3-88491e04a57d	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-0f04-7b30-97b3-88491e04a57d", "occurredOnUtc": "2026-10-04T11:30:32.8364191Z", "vendorInvoiceId": "01a106ae-0ef2-7183-a407-2e028076c45e"}	2026-10-04 18:30:32.836419+07	2026-10-04 18:30:32.861133+07	1	\N
01a106ae-0f19-72f2-8a79-9077b551764f	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0f19-72f2-8a79-9077b551764f", "journalId": "01a106ae-0f14-743b-a82b-c273bf78dc57", "occurredOnUtc": "2026-10-04T11:30:32.8578656Z"}	2026-10-04 18:30:32.857865+07	2026-10-04 18:30:32.86237+07	1	\N
01a106ae-0f2c-7a14-972e-ad53ddb834bc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f2c-7a14-972e-ad53ddb834bc", "occurredOnUtc": "2026-10-04T11:30:32.8765873Z", "dailyRecordingId": "01a106ae-0f1f-703b-9d0a-2464808cf5d8"}	2026-10-04 18:30:32.876587+07	2026-10-04 18:30:32.886356+07	1	\N
01a106ae-0f59-7697-9f24-da65cdbec3d2	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f59-7697-9f24-da65cdbec3d2", "occurredOnUtc": "2026-10-04T11:30:32.9214913Z", "dailyRecordingId": "01a106ae-0f4e-7a58-9227-731f2a961ad2"}	2026-10-04 18:30:32.921491+07	2026-10-04 18:30:32.931773+07	1	\N
01a106ae-0f86-761d-84ce-bce3b8ee359d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f86-761d-84ce-bce3b8ee359d", "occurredOnUtc": "2026-10-04T11:30:32.9661585Z", "dailyRecordingId": "01a106ae-0f7b-7665-8204-60f3af0c5e15"}	2026-10-04 18:30:32.966158+07	2026-10-04 18:30:32.98003+07	1	\N
01a106ae-0fb6-7e07-9c62-20a0a1af444c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0fb6-7e07-9c62-20a0a1af444c", "occurredOnUtc": "2026-10-04T11:30:33.0140025Z", "dailyRecordingId": "01a106ae-0fab-7019-82ca-0e75f9cb1722"}	2026-10-04 18:30:33.014002+07	2026-10-04 18:30:33.02572+07	1	\N
01a106ae-102c-70ee-b396-0a8d5da0af4b	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-102c-70ee-b396-0a8d5da0af4b", "occurredOnUtc": "2026-10-04T11:30:33.1328109Z", "vendorInvoiceId": "01a106ae-1018-720e-97c2-1501dfda537f"}	2026-10-04 18:30:33.13281+07	2026-10-04 18:30:33.160384+07	1	\N
01a106ae-1044-77d4-a877-aee1d4b0b9cb	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1044-77d4-a877-aee1d4b0b9cb", "journalId": "01a106ae-103e-7429-b03c-e3fa46dc0175", "occurredOnUtc": "2026-10-04T11:30:33.1569135Z"}	2026-10-04 18:30:33.156913+07	2026-10-04 18:30:33.161162+07	1	\N
01a106ae-106f-7275-8a60-2c560bfab3b8	Domain.Finance.Payables.VendorInvoicePostedDomainEvent	{"id": "01a106ae-106f-7275-8a60-2c560bfab3b8", "occurredOnUtc": "2026-10-04T11:30:33.1998354Z", "vendorInvoiceId": "01a106ae-105c-738b-9922-d9f3438c009a"}	2026-10-04 18:30:33.199835+07	2026-10-04 18:30:33.227364+07	1	\N
01a106ae-1087-7348-8864-97135df6eb05	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1087-7348-8864-97135df6eb05", "journalId": "01a106ae-1081-7bff-83f4-6f69a43d747f", "occurredOnUtc": "2026-10-04T11:30:33.2237663Z"}	2026-10-04 18:30:33.223766+07	2026-10-04 18:30:33.228089+07	1	\N
01a106ae-1097-7bee-a43d-c021298ef238	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1097-7bee-a43d-c021298ef238", "occurredOnUtc": "2026-10-04T11:30:33.2390142Z", "dailyRecordingId": "01a106ae-108c-76d1-be62-2df1049ed550"}	2026-10-04 18:30:33.239014+07	2026-10-04 18:30:33.248496+07	1	\N
01a106ae-1134-77d1-a587-5a3a2f48c7df	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1134-77d1-a587-5a3a2f48c7df", "occurredOnUtc": "2026-10-04T11:30:33.3964117Z", "dailyRecordingId": "01a106ae-1129-7ebc-9afa-8d83e87d51ff"}	2026-10-04 18:30:33.396411+07	2026-10-04 18:30:33.40439+07	1	\N
01a106ae-11e2-77d0-81ca-04ac1cb65a8a	Domain.Finance.Receivables.CustomerAdvanceAppliedDomainEvent	{"id": "01a106ae-11e2-77d0-81ca-04ac1cb65a8a", "applicationId": "01a106ae-11e1-7db7-b31a-e4ba63387b40", "occurredOnUtc": "2026-10-04T11:30:33.5700258Z", "customerReceiptId": "01a106ae-0eb2-7e2e-8ae3-e03501f0a745"}	2026-10-04 18:30:33.570025+07	2026-10-04 18:30:33.638562+07	1	\N
01a106ae-1223-7bc9-87c2-84264be5c491	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1223-7bc9-87c2-84264be5c491", "journalId": "01a106ae-121e-7d99-a3a4-2a4deac68e2f", "occurredOnUtc": "2026-10-04T11:30:33.6353106Z"}	2026-10-04 18:30:33.63531+07	2026-10-04 18:30:33.639587+07	1	\N
01a106ae-1246-75b1-8311-afeda307a9b3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1246-75b1-8311-afeda307a9b3", "occurredOnUtc": "2026-10-04T11:30:33.6708859Z", "dailyRecordingId": "01a106ae-123c-772e-bc9c-5f2206abc9a7"}	2026-10-04 18:30:33.670885+07	2026-10-04 18:30:33.67972+07	1	\N
01a106ae-126e-7f5d-b4e6-61e02f224420	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-126e-7f5d-b4e6-61e02f224420", "occurredOnUtc": "2026-10-04T11:30:33.710027Z", "dailyRecordingId": "01a106ae-1263-7f2f-bbea-b566851d80e8"}	2026-10-04 18:30:33.710027+07	2026-10-04 18:30:33.718337+07	1	\N
01a106ae-12de-7711-8347-704668c88c21	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ae-12de-7711-8347-704668c88c21", "occurredOnUtc": "2026-10-04T11:30:33.8221635Z", "salesInvoiceId": "01a106ae-12c6-7315-9f02-2fa6cd84672d"}	2026-10-04 18:30:33.822163+07	2026-10-04 18:30:33.854231+07	1	\N
01a106ae-12f9-703b-ab08-09a3c17453f3	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-12f9-703b-ab08-09a3c17453f3", "journalId": "01a106ae-12f3-7fda-840e-c18e77d9ab8e", "occurredOnUtc": "2026-10-04T11:30:33.8498946Z"}	2026-10-04 18:30:33.849894+07	2026-10-04 18:30:33.855115+07	1	\N
01a106ae-130b-7879-b56b-fb58d8ee9af3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-130b-7879-b56b-fb58d8ee9af3", "occurredOnUtc": "2026-10-04T11:30:33.8675954Z", "dailyRecordingId": "01a106ae-12ff-74c7-a6c7-edbba1405eb5"}	2026-10-04 18:30:33.867595+07	2026-10-04 18:30:33.877647+07	1	\N
01a106ae-1360-74f7-9fbe-3e7078531800	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1360-74f7-9fbe-3e7078531800", "journalId": "01a106ae-1345-7911-b8a2-b88a993a24c7", "occurredOnUtc": "2026-10-04T11:30:33.9525542Z"}	2026-10-04 18:30:33.952554+07	2026-10-04 18:30:33.956512+07	1	\N
01a106ae-13b0-76b3-952f-8d1318e0ba5b	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-13b0-76b3-952f-8d1318e0ba5b", "occurredOnUtc": "2026-10-04T11:30:34.0324663Z", "dailyRecordingId": "01a106ae-13a5-7ae1-8065-056bbe4af2e3"}	2026-10-04 18:30:34.032466+07	2026-10-04 18:30:34.041601+07	1	\N
01a106ae-140c-7833-ac4a-0d60a00d8de9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-140c-7833-ac4a-0d60a00d8de9", "occurredOnUtc": "2026-10-04T11:30:34.1248565Z", "dailyRecordingId": "01a106ae-1401-7239-9838-ed8e5556b525"}	2026-10-04 18:30:34.124856+07	2026-10-04 18:30:34.139282+07	1	\N
01a106ae-1424-7529-bd9b-41108ffb2e5e	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ae-1424-7529-bd9b-41108ffb2e5e", "occurredOnUtc": "2026-10-04T11:30:34.1485333Z", "bankTransferId": "01a106ae-1424-7fdd-9d0b-354de397630e"}	2026-10-04 18:30:34.148533+07	2026-10-04 18:30:34.174131+07	1	\N
01a106ae-1439-75be-bdcf-43c53d028641	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1439-75be-bdcf-43c53d028641", "journalId": "01a106ae-1433-76c3-8598-7153bdbf54a9", "occurredOnUtc": "2026-10-04T11:30:34.169947Z"}	2026-10-04 18:30:34.169947+07	2026-10-04 18:30:34.174924+07	1	\N
01a106ae-14bc-71b4-9f2c-7ba513cdc7cc	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-14bc-71b4-9f2c-7ba513cdc7cc", "journalId": "01a106ae-14b7-7c6f-bbb1-b34d56fe8294", "occurredOnUtc": "2026-10-04T11:30:34.3001657Z"}	2026-10-04 18:30:34.300165+07	2026-10-04 18:30:34.305619+07	1	\N
01a106ae-0e74-70b8-89a9-453deca68f62	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0e74-70b8-89a9-453deca68f62", "occurredOnUtc": "2026-10-04T11:30:32.6920973Z", "dailyRecordingId": "01a106ae-0e68-70ab-bacf-a1ba945c4e10"}	2026-10-04 18:30:32.692097+07	2026-10-04 18:30:32.704012+07	1	\N
01a106ae-0eb2-7ca5-a883-e3ff9da8296e	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-0eb2-7ca5-a883-e3ff9da8296e", "occurredOnUtc": "2026-10-04T11:30:32.7540715Z", "customerReceiptId": "01a106ae-0eb2-7e2e-8ae3-e03501f0a745"}	2026-10-04 18:30:32.754071+07	2026-10-04 18:30:32.780244+07	1	\N
01a106ae-0ec9-7499-834b-29e9fd861528	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-0ec9-7499-834b-29e9fd861528", "journalId": "01a106ae-0ec3-7213-b825-1e01530a1905", "occurredOnUtc": "2026-10-04T11:30:32.7770124Z"}	2026-10-04 18:30:32.777012+07	2026-10-04 18:30:32.781213+07	1	\N
01a106ae-0ed9-7f2b-8e7a-54efca018f14	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0ed9-7f2b-8e7a-54efca018f14", "occurredOnUtc": "2026-10-04T11:30:32.7932127Z", "dailyRecordingId": "01a106ae-0ece-76de-b34b-aa355877e294"}	2026-10-04 18:30:32.793212+07	2026-10-04 18:30:32.801818+07	1	\N
01a106ae-0f43-7392-b837-aaedfe5cf8b7	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f43-7392-b837-aaedfe5cf8b7", "occurredOnUtc": "2026-10-04T11:30:32.899392Z", "dailyRecordingId": "01a106ae-0f37-718b-9dbf-bd3c51be9432"}	2026-10-04 18:30:32.899392+07	2026-10-04 18:30:32.909453+07	1	\N
01a106ae-0f70-715d-bc27-5024ccdfca76	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f70-715d-bc27-5024ccdfca76", "occurredOnUtc": "2026-10-04T11:30:32.9444268Z", "dailyRecordingId": "01a106ae-0f64-7244-9653-6c258be05f86"}	2026-10-04 18:30:32.944426+07	2026-10-04 18:30:32.954247+07	1	\N
01a106ae-0f9f-7d47-ab15-ed988fd9d5b8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0f9f-7d47-ab15-ed988fd9d5b8", "occurredOnUtc": "2026-10-04T11:30:32.9918989Z", "dailyRecordingId": "01a106ae-0f94-7973-b72a-a1ee14ccfa40"}	2026-10-04 18:30:32.991898+07	2026-10-04 18:30:33.002155+07	1	\N
01a106ae-0fd1-7a70-9032-cda09e8e9996	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-0fd1-7a70-9032-cda09e8e9996", "occurredOnUtc": "2026-10-04T11:30:33.0411539Z", "dailyRecordingId": "01a106ae-0fc3-7696-8265-5a185d22ba80"}	2026-10-04 18:30:33.041153+07	2026-10-04 18:30:33.055049+07	1	\N
01a106ae-10bf-709e-abec-a4ae4949b3a8	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-10bf-709e-abec-a4ae4949b3a8", "occurredOnUtc": "2026-10-04T11:30:33.279579Z", "cashTransactionId": "01a106ae-10a8-761f-881e-648b6f39dea3"}	2026-10-04 18:30:33.279579+07	2026-10-04 18:30:33.305945+07	1	\N
01a106ae-10d5-78ce-8d1a-b2780f507e7c	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-10d5-78ce-8d1a-b2780f507e7c", "journalId": "01a106ae-10cf-72ba-a3d7-f51a4821f94f", "occurredOnUtc": "2026-10-04T11:30:33.3017776Z"}	2026-10-04 18:30:33.301777+07	2026-10-04 18:30:33.307074+07	1	\N
01a106ae-10fa-76e3-8db7-57b6bb01ab93	Domain.Finance.CashBank.CashTransactionPostedDomainEvent	{"id": "01a106ae-10fa-76e3-8db7-57b6bb01ab93", "occurredOnUtc": "2026-10-04T11:30:33.3385844Z", "cashTransactionId": "01a106ae-10e2-7471-a55e-52e35fcd01dc"}	2026-10-04 18:30:33.338584+07	2026-10-04 18:30:33.362412+07	1	\N
01a106ae-110f-7875-9a13-1ecfed58a3a7	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-110f-7875-9a13-1ecfed58a3a7", "journalId": "01a106ae-110a-736f-a7b4-7b4f1aefac69", "occurredOnUtc": "2026-10-04T11:30:33.359101Z"}	2026-10-04 18:30:33.359101+07	2026-10-04 18:30:33.363281+07	1	\N
01a106ae-111f-708f-8b98-b601f1f623a9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-111f-708f-8b98-b601f1f623a9", "occurredOnUtc": "2026-10-04T11:30:33.3758971Z", "dailyRecordingId": "01a106ae-1114-78be-b8d0-6cca6cbc5c44"}	2026-10-04 18:30:33.375897+07	2026-10-04 18:30:33.384739+07	1	\N
01a106ae-1147-70f7-b76d-04b254dadb36	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1147-70f7-b76d-04b254dadb36", "occurredOnUtc": "2026-10-04T11:30:33.4152571Z", "dailyRecordingId": "01a106ae-113d-78a3-aa8a-7d1fed49212a"}	2026-10-04 18:30:33.415257+07	2026-10-04 18:30:33.425383+07	1	\N
01a106ae-118f-792e-b9b5-8af856211172	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ae-118f-792e-b9b5-8af856211172", "occurredOnUtc": "2026-10-04T11:30:33.4872365Z", "salesInvoiceId": "01a106ae-1177-79f1-9dab-ecd0831503a9"}	2026-10-04 18:30:33.487236+07	2026-10-04 18:30:33.517508+07	1	\N
01a106ae-11a9-73cf-a4ff-64d8115d5cec	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-11a9-73cf-a4ff-64d8115d5cec", "journalId": "01a106ae-11a4-7884-8543-49fc53361329", "occurredOnUtc": "2026-10-04T11:30:33.5137684Z"}	2026-10-04 18:30:33.513768+07	2026-10-04 18:30:33.518897+07	1	\N
01a106ae-1230-7b3e-8026-ce65d15174a3	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1230-7b3e-8026-ce65d15174a3", "occurredOnUtc": "2026-10-04T11:30:33.6487768Z", "dailyRecordingId": "01a106ae-1228-76cc-b1a8-febe704cf404"}	2026-10-04 18:30:33.648776+07	2026-10-04 18:30:33.660017+07	1	\N
01a106ae-125a-7907-a3b1-ea501237272e	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-125a-7907-a3b1-ea501237272e", "occurredOnUtc": "2026-10-04T11:30:33.690335Z", "dailyRecordingId": "01a106ae-1250-7e96-8716-ed7c86794e39"}	2026-10-04 18:30:33.690335+07	2026-10-04 18:30:33.699129+07	1	\N
01a106ae-1337-7740-940d-80c14ecde108	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1337-7740-940d-80c14ecde108", "journalId": "01a106ae-131d-77c3-a3a4-ae65293f4cac", "occurredOnUtc": "2026-10-04T11:30:33.9115874Z"}	2026-10-04 18:30:33.911587+07	2026-10-04 18:30:33.915796+07	1	\N
01a106ae-1398-733a-9dc8-624c54806c4c	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1398-733a-9dc8-624c54806c4c", "occurredOnUtc": "2026-10-04T11:30:34.0089387Z", "dailyRecordingId": "01a106ae-138d-729d-b5c9-b2e50c465273"}	2026-10-04 18:30:34.008938+07	2026-10-04 18:30:34.019929+07	1	\N
01a106ae-13e0-7d19-b8bf-abacca7af403	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ae-13e0-7d19-b8bf-abacca7af403", "occurredOnUtc": "2026-10-04T11:30:34.0800785Z", "salesInvoiceId": "01a106ae-13c7-7e8d-bea5-901363b90855"}	2026-10-04 18:30:34.080078+07	2026-10-04 18:30:34.111143+07	1	\N
01a106ae-13fb-7da6-b90e-c9f09686c334	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-13fb-7da6-b90e-c9f09686c334", "journalId": "01a106ae-13f6-75bd-b085-f42eb268a806", "occurredOnUtc": "2026-10-04T11:30:34.1075867Z"}	2026-10-04 18:30:34.107586+07	2026-10-04 18:30:34.112365+07	1	\N
01a106ae-1447-7784-94a9-c14b2e6c6e66	Domain.Finance.CashBank.BankTransferPostedDomainEvent	{"id": "01a106ae-1447-7784-94a9-c14b2e6c6e66", "occurredOnUtc": "2026-10-04T11:30:34.1834039Z", "bankTransferId": "01a106ae-1447-713b-b007-4166f3656038"}	2026-10-04 18:30:34.183403+07	2026-10-04 18:30:34.208416+07	1	\N
01a106ae-145c-73dd-8fa8-b547972dbdf8	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-145c-73dd-8fa8-b547972dbdf8", "journalId": "01a106ae-1456-70e7-93dc-230ef5292b34", "occurredOnUtc": "2026-10-04T11:30:34.2043964Z"}	2026-10-04 18:30:34.204396+07	2026-10-04 18:30:34.209419+07	1	\N
01a106ae-146e-7794-a672-74beac9ac672	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-146e-7794-a672-74beac9ac672", "occurredOnUtc": "2026-10-04T11:30:34.2223328Z", "dailyRecordingId": "01a106ae-1462-76d1-a222-4ed44499544e"}	2026-10-04 18:30:34.222332+07	2026-10-04 18:30:34.231492+07	1	\N
01a106ae-1484-7f83-87de-e59d576fd6cc	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1484-7f83-87de-e59d576fd6cc", "occurredOnUtc": "2026-10-04T11:30:34.2440308Z", "dailyRecordingId": "01a106ae-1479-7cc5-90f2-0608efd0ff02"}	2026-10-04 18:30:34.24403+07	2026-10-04 18:30:34.257736+07	1	\N
01a106ae-14a1-724c-a255-3ff04af0845a	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-14a1-724c-a255-3ff04af0845a", "occurredOnUtc": "2026-10-04T11:30:34.2732836Z", "customerReceiptId": "01a106ae-14a1-7f7a-b0ef-4b0f307eb679"}	2026-10-04 18:30:34.273283+07	2026-10-04 18:30:34.304765+07	1	\N
01a106ae-1500-74b6-83c7-a40538f0a34c	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1500-74b6-83c7-a40538f0a34c", "journalId": "01a106ae-14fa-7e61-b38a-d35290c8695e", "occurredOnUtc": "2026-10-04T11:30:34.3681192Z"}	2026-10-04 18:30:34.368119+07	2026-10-04 18:30:34.374314+07	1	\N
01a106ae-151c-7550-a4bf-001279c39eb8	Domain.Partnership.Cycles.CycleClosedDomainEvent	{"id": "01a106ae-151c-7550-a4bf-001279c39eb8", "cycleId": "01a106ad-ea62-7df6-aaaa-1359b7c73fa1", "occurredOnUtc": "2026-10-04T11:30:34.39668Z"}	2026-10-04 18:30:34.39668+07	2026-10-04 18:30:34.422681+07	1	\N
01a106ae-1533-722d-9a16-b8bf42157af4	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1533-722d-9a16-b8bf42157af4", "journalId": "01a106ae-152e-70ae-8f19-0fe67f707c50", "occurredOnUtc": "2026-10-04T11:30:34.4197858Z"}	2026-10-04 18:30:34.419785+07	2026-10-04 18:30:34.423332+07	1	\N
01a106ae-1543-7772-8e77-7afaa0b8cce4	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1543-7772-8e77-7afaa0b8cce4", "occurredOnUtc": "2026-10-04T11:30:34.4351509Z", "dailyRecordingId": "01a106ae-1537-7acb-8120-b3e4a1c141b7"}	2026-10-04 18:30:34.43515+07	2026-10-04 18:30:34.445755+07	1	\N
01a106ae-158c-7627-af20-38c7fc8612ec	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-158c-7627-af20-38c7fc8612ec", "occurredOnUtc": "2026-10-04T11:30:34.5089278Z", "dailyRecordingId": "01a106ae-1582-7a1f-8b9d-61fc64e31cc9"}	2026-10-04 18:30:34.508927+07	2026-10-04 18:30:34.5185+07	1	\N
01a106ae-15fd-7a1c-aea2-be4bd5c9eaf8	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-15fd-7a1c-aea2-be4bd5c9eaf8", "occurredOnUtc": "2026-10-04T11:30:34.6214441Z", "dailyRecordingId": "01a106ae-15ef-767d-bad0-a41642b167b5"}	2026-10-04 18:30:34.621444+07	2026-10-04 18:30:34.634662+07	1	\N
01a106ae-179c-7f52-9457-c4ed39bd4772	Domain.Partnership.Cycles.CyclePlannedDomainEvent	{"id": "01a106ae-179c-7f52-9457-c4ed39bd4772", "cycleId": "01a106ae-179c-7392-bd9f-1274d80f58e8", "occurredOnUtc": "2026-10-04T11:30:35.0365583Z"}	2026-10-04 18:30:35.036558+07	2026-10-04 18:30:35.041084+07	1	\N
01a106ae-17f8-779d-9564-40cc20cf9e09	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-17f8-779d-9564-40cc20cf9e09", "occurredOnUtc": "2026-10-04T11:30:35.1289552Z", "customerReceiptId": "01a106ae-17f8-7f8a-88cf-3bf2a729f0c9"}	2026-10-04 18:30:35.128955+07	2026-10-04 18:30:35.153999+07	1	\N
01a106ae-180f-7e3a-9ca2-148d6e9c36b9	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-180f-7e3a-9ca2-148d6e9c36b9", "journalId": "01a106ae-180a-7e1f-901c-24a1eccd7505", "occurredOnUtc": "2026-10-04T11:30:35.1510572Z"}	2026-10-04 18:30:35.151057+07	2026-10-04 18:30:35.15481+07	1	\N
01a106ae-181d-7bd2-9597-4cd71b35159d	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-181d-7bd2-9597-4cd71b35159d", "occurredOnUtc": "2026-10-04T11:30:35.1659257Z", "dailyRecordingId": "01a106ae-1813-771c-984d-2678c0084126"}	2026-10-04 18:30:35.165925+07	2026-10-04 18:30:35.174004+07	1	\N
01a106ae-14d0-7f2f-8c96-72a2941d2b23	Domain.Inventory.StockReturns.StockReturnPostedDomainEvent	{"id": "01a106ae-14d0-7f2f-8c96-72a2941d2b23", "occurredOnUtc": "2026-10-04T11:30:34.3204843Z", "stockReturnId": "01a106ae-14d0-79b6-b4e1-bfafb1945a08"}	2026-10-04 18:30:34.320484+07	2026-10-04 18:30:34.373206+07	1	\N
01a106ae-155a-7eed-9258-82dbf38448a2	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-155a-7eed-9258-82dbf38448a2", "occurredOnUtc": "2026-10-04T11:30:34.4582392Z", "dailyRecordingId": "01a106ae-154e-7794-b56c-34eee5209750"}	2026-10-04 18:30:34.458239+07	2026-10-04 18:30:34.468096+07	1	\N
01a106ae-15b8-732e-ae90-3a531936230e	Domain.Finance.Receivables.CustomerReceiptPostedDomainEvent	{"id": "01a106ae-15b8-732e-ae90-3a531936230e", "occurredOnUtc": "2026-10-04T11:30:34.5524479Z", "customerReceiptId": "01a106ae-15b8-700b-af83-753cfbb10a2a"}	2026-10-04 18:30:34.552447+07	2026-10-04 18:30:34.603429+07	1	\N
01a106ae-15e4-7d13-81db-ae808ada6f96	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-15e4-7d13-81db-ae808ada6f96", "journalId": "01a106ae-15dc-745c-a5ad-b5f5c775dd6c", "occurredOnUtc": "2026-10-04T11:30:34.5963335Z"}	2026-10-04 18:30:34.596333+07	2026-10-04 18:30:34.606085+07	1	\N
01a106ae-1617-710a-94ab-f60292f1df1f	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-1617-710a-94ab-f60292f1df1f", "occurredOnUtc": "2026-10-04T11:30:34.647865Z", "dailyRecordingId": "01a106ae-160b-7082-b0ed-65317a471bed"}	2026-10-04 18:30:34.647865+07	2026-10-04 18:30:34.659878+07	1	\N
01a106ae-166f-7848-a6e2-dabe8b531d4f	Domain.Sales.SalesInvoices.SalesInvoicePostedDomainEvent	{"id": "01a106ae-166f-7848-a6e2-dabe8b531d4f", "occurredOnUtc": "2026-10-04T11:30:34.7350639Z", "salesInvoiceId": "01a106ae-1653-7ba7-b963-74958f58aa0d"}	2026-10-04 18:30:34.735063+07	2026-10-04 18:30:34.770501+07	1	\N
01a106ae-168d-7496-bdd6-14c4dad1c4c2	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-168d-7496-bdd6-14c4dad1c4c2", "journalId": "01a106ae-1687-7b73-b756-edf8c637ef02", "occurredOnUtc": "2026-10-04T11:30:34.7658171Z"}	2026-10-04 18:30:34.765817+07	2026-10-04 18:30:34.771456+07	1	\N
01a106ae-16c4-732d-8694-f4b056b51948	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-16c4-732d-8694-f4b056b51948", "occurredOnUtc": "2026-10-04T11:30:34.8204919Z", "paymentVoucherId": "01a106ae-16a4-7f5a-a671-9f125c3c9dde"}	2026-10-04 18:30:34.820491+07	2026-10-04 18:30:34.849951+07	1	\N
01a106ae-16de-786b-a123-5d0e88d67c6b	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-16de-786b-a123-5d0e88d67c6b", "journalId": "01a106ae-16d8-74f2-9603-432da6f51ea2", "occurredOnUtc": "2026-10-04T11:30:34.8465065Z"}	2026-10-04 18:30:34.846506+07	2026-10-04 18:30:34.850701+07	1	\N
01a106ae-170d-76c7-9303-b2ef9aed6f7a	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-170d-76c7-9303-b2ef9aed6f7a", "occurredOnUtc": "2026-10-04T11:30:34.8931634Z", "paymentVoucherId": "01a106ae-16f1-74b8-90a5-710acc7d3d70"}	2026-10-04 18:30:34.893163+07	2026-10-04 18:30:34.92157+07	1	\N
01a106ae-1726-7e66-b70c-745e450f1c86	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-1726-7e66-b70c-745e450f1c86", "journalId": "01a106ae-1720-7030-af18-6eb72a8d08cc", "occurredOnUtc": "2026-10-04T11:30:34.918387Z"}	2026-10-04 18:30:34.918387+07	2026-10-04 18:30:34.922398+07	1	\N
01a106ae-1752-721f-aeab-38673738dc55	Domain.Finance.Payables.PaymentVoucherPaidDomainEvent	{"id": "01a106ae-1752-721f-aeab-38673738dc55", "occurredOnUtc": "2026-10-04T11:30:34.9624096Z", "paymentVoucherId": "01a106ae-1738-70ac-ae0b-b009733f4567"}	2026-10-04 18:30:34.962409+07	2026-10-04 18:30:34.99155+07	1	\N
01a106ae-176b-7236-95d9-769210fc36af	Domain.Finance.Journals.JournalPostedDomainEvent	{"id": "01a106ae-176b-7236-95d9-769210fc36af", "journalId": "01a106ae-1766-73a3-a3c7-763cacf696b6", "occurredOnUtc": "2026-10-04T11:30:34.9877909Z"}	2026-10-04 18:30:34.98779+07	2026-10-04 18:30:34.992783+07	1	\N
01a106ae-177e-7cbf-9b87-e02ae1d3d0a9	Domain.Production.DailyRecordings.DailyRecordingSavedDomainEvent	{"id": "01a106ae-177e-7cbf-9b87-e02ae1d3d0a9", "occurredOnUtc": "2026-10-04T11:30:35.0066557Z", "dailyRecordingId": "01a106ae-1772-7905-8e52-59d851299e1f"}	2026-10-04 18:30:35.006655+07	2026-10-04 18:30:35.016973+07	1	\N
\.


--
-- Data for Name: goods_receipt_lines; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.goods_receipt_lines (goods_receipt_id, line_number, purchase_order_line_number, item_id, uom_id, quantity, base_quantity, unit_cost, value, quantity_invoiced, value_invoiced) FROM stdin;
01a106ad-caea-7afd-8a30-a75d8b846fd2	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	5000.0000	7400.000000	37000000.00	5000.0000	37000000.00
01a106ad-c921-7abc-9538-f9aefa07df64	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	61.0000	3050.0000	8150.000000	24857500.00	61.0000	24857500.00
01a106ad-c921-7abc-9538-f9aefa07df64	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	225.0000	11250.0000	7900.000000	88875000.00	225.0000	88875000.00
01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00	11.0000	1045000.00
01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00	5.0000	560000.00
01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00	16.0000	672000.00
01a106ad-d257-72fc-9fab-d8d93779a1f7	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	4500.0000	4500.0000	7400.000000	33300000.00	4500.0000	33300000.00
01a106ad-d113-7702-95fa-9936fda3cb97	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	60.0000	3000.0000	8100.000000	24300000.00	60.0000	24300000.00
01a106ad-d113-7702-95fa-9936fda3cb97	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	209.0000	10450.0000	7850.000000	82032500.00	209.0000	82032500.00
01a106ad-d150-7d46-aad6-09ab900856a2	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00	11.0000	1045000.00
01a106ad-d150-7d46-aad6-09ab900856a2	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00	5.0000	560000.00
01a106ad-d150-7d46-aad6-09ab900856a2	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00	16.0000	672000.00
01a106ad-d6d0-7981-97bd-a98ead54697d	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	5000.0000	7400.000000	37000000.00	5000.0000	37000000.00
01a106ad-d565-74c5-97c1-753986941b7f	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	70.0000	3500.0000	8150.000000	28525000.00	70.0000	28525000.00
01a106ad-d565-74c5-97c1-753986941b7f	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	277.0000	13850.0000	7900.000000	109415000.00	277.0000	109415000.00
01a106ad-d5a9-715f-82a1-c024dce8f91c	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00	11.0000	1045000.00
01a106ad-d5a9-715f-82a1-c024dce8f91c	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00	5.0000	560000.00
01a106ad-d5a9-715f-82a1-c024dce8f91c	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00	16.0000	672000.00
01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	7000.0000	7000.0000	7400.000000	51800000.00	7000.0000	51800000.00
01a106ad-ecb2-7cf5-bc99-7307f161b5bc	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	91.0000	4550.0000	8100.000000	36855000.00	91.0000	36855000.00
01a106ad-ecb2-7cf5-bc99-7307f161b5bc	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	386.0000	19300.0000	7850.000000	151505000.00	386.0000	151505000.00
01a106ad-ece9-7ff1-aad4-f1d48c18557f	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	15.0000	15.0000	95000.000000	1425000.00	15.0000	1425000.00
01a106ad-ece9-7ff1-aad4-f1d48c18557f	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	7.0000	112000.000000	784000.00	7.0000	784000.00
01a106ad-ece9-7ff1-aad4-f1d48c18557f	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	21.0000	42000.000000	882000.00	21.0000	882000.00
01a106ad-f7e8-755b-9182-950be853462c	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	6000.0000	6000.0000	7400.000000	44400000.00	6000.0000	44400000.00
01a106ad-f5e8-7414-89fa-95e6aee15d5c	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	76.0000	3800.0000	8150.000000	30970000.00	76.0000	30970000.00
01a106ad-f5e8-7414-89fa-95e6aee15d5c	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	294.0000	14700.0000	7900.000000	116130000.00	294.0000	116130000.00
01a106ad-f623-7d55-9b20-a1b5d900f045	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	13.0000	13.0000	95000.000000	1235000.00	13.0000	1235000.00
01a106ad-f623-7d55-9b20-a1b5d900f045	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	6.0000	112000.000000	672000.00	6.0000	672000.00
01a106ad-f623-7d55-9b20-a1b5d900f045	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00	16.0000	672000.00
01a106ae-057e-79b5-8334-150eb98cd9a0	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	8000.0000	8000.0000	7400.000000	59200000.00	8000.0000	59200000.00
01a106ae-03dc-71bc-8c80-5622ee97290b	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	104.0000	5200.0000	8100.000000	42120000.00	104.0000	42120000.00
01a106ae-03dc-71bc-8c80-5622ee97290b	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	441.0000	22050.0000	7850.000000	173092500.00	441.0000	173092500.00
01a106ae-041e-7b65-83ea-b89df6274edb	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	17.0000	17.0000	95000.000000	1615000.00	17.0000	1615000.00
01a106ae-041e-7b65-83ea-b89df6274edb	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	8.0000	112000.000000	896000.00	8.0000	896000.00
01a106ae-041e-7b65-83ea-b89df6274edb	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	21.0000	42000.000000	882000.00	21.0000	882000.00
01a106ae-0c4e-7273-b6e0-82abb0d929f9	1	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	3500.0000	3500.0000	7400.000000	25900000.00	3500.0000	25900000.00
01a106ae-0af1-7541-a16c-02d04ad0ab29	1	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	46.0000	2300.0000	8150.000000	18745000.00	46.0000	18745000.00
01a106ae-0af1-7541-a16c-02d04ad0ab29	2	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	187.0000	9350.0000	7900.000000	73865000.00	187.0000	73865000.00
01a106ae-0b26-7b4c-aa76-9672c7ce56d4	1	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	9.0000	9.0000	95000.000000	855000.00	9.0000	855000.00
01a106ae-0b26-7b4c-aa76-9672c7ce56d4	2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	4.0000	112000.000000	448000.00	4.0000	448000.00
01a106ae-0b26-7b4c-aa76-9672c7ce56d4	3	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	11.0000	11.0000	42000.000000	462000.00	11.0000	462000.00
\.


--
-- Data for Name: goods_receipts; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.goods_receipts (id, number, branch_id, purchase_order_id, vendor_id, warehouse_id, cycle_id, receipt_date, delivery_note_number, notes, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-c921-7abc-9538-f9aefa07df64	BPB/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c831-7346-9239-cc17246be1df	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-07-14	SJ-CPI-260714-101	\N	2026-10-04 18:30:15.02303+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	BPB/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c85a-70d9-a08a-193ddff2454c	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-07-14	SJ-MDN-260714-101	\N	2026-10-04 18:30:15.220529+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-caea-7afd-8a30-a75d8b846fd2	BPB/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c7b6-76e6-8710-e2ea3f456d89	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-07-16	SJ-CPI-260716-101	\N	2026-10-04 18:30:15.410283+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d113-7702-95fa-9936fda3cb97	BPB/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-d099-7926-9854-587dcf0ccdec	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-07-24	SJ-JPF-260724-104	\N	2026-10-04 18:30:16.984286+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d150-7d46-aad6-09ab900856a2	BPB/BDG/2026/VII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-d0bb-7a33-9e48-09d160670a90	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-07-24	SJ-MDN-260724-104	\N	2026-10-04 18:30:17.047763+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d257-72fc-9fab-d8d93779a1f7	BPB/BDG/2026/VII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-d074-7321-9aa3-c7bf0d824568	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-07-26	SJ-CPI-260726-104	\N	2026-10-04 18:30:17.305544+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d565-74c5-97c1-753986941b7f	BPB/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-d36b-7ff1-8a59-90fecddb2138	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-07-28	SJ-CPI-260728-104	\N	2026-10-04 18:30:18.089976+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d5a9-715f-82a1-c024dce8f91c	BPB/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-d38e-7e14-a5ea-e2b6d7da859c	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-07-28	SJ-MDN-260728-104	\N	2026-10-04 18:30:18.160152+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d6d0-7981-97bd-a98ead54697d	BPB/CJR/2026/VII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-d349-7a6c-b9fb-cad4ed3bbd16	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-d332-7f38-9c34-260051b68ef3	2026-07-30	SJ-CPI-260730-105	\N	2026-10-04 18:30:18.450503+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ecb2-7cf5-bc99-7307f161b5bc	BPB/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-eab1-7ef8-90a5-b905cf6caae4	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-08-21	SJ-JPF-260821-110	\N	2026-10-04 18:30:24.055029+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ece9-7ff1-aad4-f1d48c18557f	BPB/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-ead4-7c8d-8b03-cd56500d7e26	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-08-21	SJ-MDN-260821-110	\N	2026-10-04 18:30:24.111506+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	BPB/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-ea88-75de-a783-288fe4369f7a	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-08-23	SJ-CPI-260823-110	\N	2026-10-04 18:30:24.81891+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f5e8-7414-89fa-95e6aee15d5c	BPB/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-f43c-7c60-8ee7-d72ea304cd19	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-08-28	SJ-CPI-260828-111	\N	2026-10-04 18:30:26.412805+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f623-7d55-9b20-a1b5d900f045	BPB/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-f458-7c58-8cbf-94cd8e8f6530	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-08-28	SJ-MDN-260828-111	\N	2026-10-04 18:30:26.474678+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f7e8-755b-9182-950be853462c	BPB/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-f422-79ea-93d5-1409a717f0aa	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-f40d-7a20-b733-56168fcec265	2026-08-30	SJ-CPI-260830-113	\N	2026-10-04 18:30:26.923213+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-03dc-71bc-8c80-5622ee97290b	BPB/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ae-0270-7a0a-8abb-ff9657ebf0f5	01a106ad-be80-70ef-bc7b-798de012dccf	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-09-11	SJ-JPF-260911-116	\N	2026-10-04 18:30:29.987809+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-041e-7b65-83ea-b89df6274edb	BPB/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ae-028f-7000-addf-42d5fb63efd9	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-bded-7b6e-a743-823445f9eaa4	\N	2026-09-11	SJ-MDN-260911-116	\N	2026-10-04 18:30:30.05342+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-057e-79b5-8334-150eb98cd9a0	BPB/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ae-0254-741a-8076-a451dd9c06ea	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ae-023f-704e-8a47-6e438951d196	2026-09-13	SJ-CPI-260913-116	\N	2026-10-04 18:30:30.400549+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0af1-7541-a16c-02d04ad0ab29	BPB/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-0962-7ac7-b614-41ab07d1f304	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-09-20	SJ-CPI-260920-119	\N	2026-10-04 18:30:31.797589+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0b26-7b4c-aa76-9672c7ce56d4	BPB/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-097f-777c-9170-865377bfb9c2	01a106ad-be8d-7ecd-a218-155f48892806	01a106ad-be27-7b73-aba5-97b277c92753	\N	2026-09-20	SJ-MDN-260920-119	\N	2026-10-04 18:30:31.851954+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c4e-7273-b6e0-82abb0d929f9	BPB/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-0947-7c24-b029-dd550944e800	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ae-0935-71d7-8a79-528d2a90b371	2026-09-22	SJ-CPI-260922-119	\N	2026-10-04 18:30:32.144672+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: stock_balances; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_balances (id, warehouse_id, item_id, quantity, value, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-caef-7c40-bb3b-48ee81ba1284	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:15.410283+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.577435+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0c50-70ea-b8b2-270dc1d4a3d3	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:32.144672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.206398+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0e48-792c-b2af-5a6555683b14	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	17930.4000	140776780.19	2026-10-04 18:30:32.648783+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.011309+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-efb2-7bff-a50d-39a7bddb009a	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:24.81891+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.890557+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d6d2-7dc9-ba79-46c9802e6b82	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:18.450503+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.525464+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0ca4-774a-accd-7952fd9958fd	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	833.3000	6791395.00	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.16973+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f85e-74ed-8370-abace13828e2	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	1.0000	42000.00	2026-10-04 18:30:27.039226+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.549383+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d5ad-7c75-ad25-bf74089d4c15	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:18.160152+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d735-706e-bc08-f1e4e858a23b	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	0.0000	0.00	2026-10-04 18:30:18.562249+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.995973+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d73e-7637-bc35-20a51f5d838c	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:18.562249+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:21.160771+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d73a-73c3-9ced-8fa32259205b	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd9f-7179-9506-e6071b528b74	0.0000	0.00	2026-10-04 18:30:18.562249+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.995973+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f856-712c-bbbd-41df01e547ed	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd9f-7179-9506-e6071b528b74	1.0000	95000.00	2026-10-04 18:30:27.039226+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.076147+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d259-7cd3-9445-c7aecac7bc90	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:17.305544+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.380124+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d741-77ff-add1-59f73a3d774f	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	0.0000	0.00	2026-10-04 18:30:18.562249+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.995973+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f017-7128-997c-9b7f919b11ca	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:24.923923+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.781388+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0cae-75e8-9224-c1d29615348e	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	1.0000	42000.00	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.628715+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-cbe8-7406-946c-b7ba004e0ee5	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:15.685672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.027408+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d2d8-7e66-9ef3-90c503218bbc	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:17.440074+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:20.463568+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-e130-7997-8df0-8ebb26fa42e3	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	0.0000	0.00	2026-10-04 18:30:21.105338+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.995973+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f9f0-7287-ac2a-b18eaf99375a	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2075.7000	16394250.71	2026-10-04 18:30:27.440684+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.651914+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f852-75bb-9f89-ab893d249935	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	267.3000	2178298.67	2026-10-04 18:30:27.039226+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.377155+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-05da-7de7-92c1-39e2db6459a3	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	1.0000	42000.00	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.348634+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f7ea-740c-8900-2c8532cff200	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:26.923213+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.999962+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f85a-7709-abed-dad2f8f55fae	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:27.039226+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.950262+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0580-7916-aa3b-7dfd2c46cc2c	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd1e-7c26-a889-94b43fae1c60	0.0000	0.00	2026-10-04 18:30:30.400549+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.459422+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d2cb-763a-96b9-4bf6d08e94e9	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	0.0000	0.00	2026-10-04 18:30:17.440074+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.440684+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-de73-75c6-bebb-842afa624472	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	0.0000	0.00	2026-10-04 18:30:20.403879+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.440684+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-05d7-76b8-bf9d-ae15fc9ad946	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.698002+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-cbdd-7e90-b562-4ed1876229ac	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	0.0000	0.00	2026-10-04 18:30:15.685672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.415088+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-cbe3-7efd-b105-97ee8ec90709	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd9f-7179-9506-e6071b528b74	0.0000	0.00	2026-10-04 18:30:15.685672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.415088+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-cbec-736b-8c21-6d109f5cac15	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	0.0000	0.00	2026-10-04 18:30:15.685672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.415088+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d4ef-760e-93c5-311ea42d6fb7	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	0.0000	0.00	2026-10-04 18:30:17.967604+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.415088+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0ca7-7f92-8a00-a59e8b0e8bf4	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd9f-7179-9506-e6071b528b74	5.0000	475000.00	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.973966+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d567-7d32-984d-f9be5be5c7f4	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	404.1000	3283735.00	2026-10-04 18:30:18.089976+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-05d3-76a3-92d9-d3d460a6b821	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd9f-7179-9506-e6071b528b74	1.0000	95000.00	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.251916+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-05d0-78dc-9cf0-85b1c0516daf	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	230.1000	1863987.54	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.017408+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d2d2-7595-a3df-2fc6710a7744	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd9f-7179-9506-e6071b528b74	0.0000	0.00	2026-10-04 18:30:17.440074+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.531038+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d2df-7f99-82a9-8a59c7a2bfe9	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	0.0000	0.00	2026-10-04 18:30:17.440074+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.531038+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-c931-7299-a55c-cd59d022fd08	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	151.6000	1228076.97	2026-10-04 18:30:15.02303+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-ca2f-70a9-a8fb-f5e07880b25f	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2.0000	190000.00	2026-10-04 18:30:15.220529+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-ca31-7c67-b0f8-4c5e67b2ad44	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	0.0000	0.00	2026-10-04 18:30:15.220529+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-ca34-7d72-930c-9103132c50f5	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2.0000	84000.00	2026-10-04 18:30:15.220529+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ae-0caa-7ad2-a651-1e52be41adea	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdab-762c-ad3a-cd0613d5d1df	4.0000	448000.00	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c950-768a-9be0-98b2f9935e95	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	606.9000	4764948.24	2026-10-04 18:30:15.02303+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.648783+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d569-7079-90a9-a4c34b446951	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	11495.2000	90758030.00	2026-10-04 18:30:18.089976+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d5ab-74ee-b3e0-ef51b81c37f6	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2.0000	190000.00	2026-10-04 18:30:18.160152+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-d5af-7923-9f06-4226377a02c4	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2.0000	84000.00	2026-10-04 18:30:18.160152+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f010-7442-b5f7-ae8731cbab14	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	0.0000	0.00	2026-10-04 18:30:24.923923+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f014-738d-b80f-3087a8038ee6	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd9f-7179-9506-e6071b528b74	0.0000	0.00	2026-10-04 18:30:24.923923+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-f01b-77e3-ade5-5ec4362402d4	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	0.0000	0.00	2026-10-04 18:30:24.923923+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
01a106ad-fef9-7385-bb53-49dfc7765bf9	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	0.0000	0.00	2026-10-04 18:30:28.730039+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333
\.


--
-- Data for Name: stock_ledger_entries; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_ledger_entries (id, warehouse_id, item_id, date, type, source_type, source_id, source_number, cycle_id, quantity, unit_cost, balance_quantity, balance_value, value) FROM stdin;
01a106ad-c93c-7566-94fa-f494ffbcc730	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-14	Receipt	GoodsReceipt	01a106ad-c921-7abc-9538-f9aefa07df64	BPB/BDG/2026/VII/0001	\N	3050.0000	8150.000000	3050.0000	24857500.00	24857500.00
01a106ad-c950-7f45-bd9d-eb9e08be4bde	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-14	Receipt	GoodsReceipt	01a106ad-c921-7abc-9538-f9aefa07df64	BPB/BDG/2026/VII/0001	\N	11250.0000	7900.000000	11250.0000	88875000.00	88875000.00
01a106ad-ca2f-7be1-9c74-22cb6ad34454	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-14	Receipt	GoodsReceipt	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	BPB/BDG/2026/VII/0002	\N	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-ca31-7311-ab01-5607cd887fa4	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-14	Receipt	GoodsReceipt	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	BPB/BDG/2026/VII/0002	\N	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-ca34-796f-a5c3-9d66c623e5dc	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-14	Receipt	GoodsReceipt	01a106ad-ca2d-7c57-8ba4-d49b50adc0b1	BPB/BDG/2026/VII/0002	\N	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-caf0-77b4-8586-3b268f30772c	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-16	Receipt	GoodsReceipt	01a106ad-caea-7afd-8a30-a75d8b846fd2	BPB/BDG/2026/VII/0003	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	5000.0000	7400.000000	5000.0000	37000000.00	37000000.00
01a106ad-cb99-7f9f-beec-50d8e3982bd3	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-16	ChickIn	ProductionCycle	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	SKL/BDG/2026/VII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-5000.0000	7400.000000	0.0000	0.00	-37000000.00
01a106ad-cbdb-7e32-8d70-3a4e98c50899	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-16	TransferOut	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3050.0000	8150.000000	0.0000	0.00	-24857500.00
01a106ad-cbdd-7f51-bfe2-f7bf50b01dd5	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-16	TransferIn	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	3050.0000	8150.000000	3050.0000	24857500.00	24857500.00
01a106ad-cbe0-796f-93fd-05034f821b80	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-16	TransferOut	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-11.0000	95000.000000	0.0000	0.00	-1045000.00
01a106ad-cbe3-74bc-bd14-8347000039ba	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-16	TransferIn	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-cbe6-740c-be90-20176b990301	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-16	TransferOut	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-cbe8-7287-89b5-77ba12ece69d	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-16	TransferIn	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-cbea-7f87-a512-ff5e13f2c061	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-16	TransferOut	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-16.0000	42000.000000	0.0000	0.00	-672000.00
01a106ad-cbec-717b-ace5-57b0127090c2	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-16	TransferIn	StockTransfer	01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-cc95-793c-8b1a-e429e89b4d3d	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-17	Usage	DailyRecording	01a106ad-cc5e-7fa7-90b9-50b3cfd71995	SKL/BDG/2026/VII/0001 2026-07-17	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-80.4000	8150.000000	2969.6000	24202240.00	-655260.00
01a106ad-cce3-71ce-b0fd-e81b92d76c42	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-18	Usage	DailyRecording	01a106ad-ccce-7f34-a0ee-cfecd67d2f84	SKL/BDG/2026/VII/0001 2026-07-18	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-100.2000	8150.000000	2869.4000	23385610.00	-816630.00
01a106ad-cce6-7431-be31-0de4f0ba5a1e	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-18	Usage	DailyRecording	01a106ad-ccce-7f34-a0ee-cfecd67d2f84	SKL/BDG/2026/VII/0001 2026-07-18	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	13.0000	546000.00	-126000.00
01a106ad-ce63-7285-9d5b-560eaae7c30d	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-19	Usage	DailyRecording	01a106ad-ce50-7f61-91ce-5a67a6545d4f	SKL/BDG/2026/VII/0001 2026-07-19	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-120.0000	8150.000000	2749.4000	22407610.00	-978000.00
01a106ad-cf45-724a-b03f-78728b5e5a2f	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-20	Usage	DailyRecording	01a106ad-cf36-79b5-8c8f-4c13577bfec5	SKL/BDG/2026/VII/0001 2026-07-20	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-139.6000	8150.000000	2609.8000	21269870.00	-1137740.00
01a106ad-cf48-7f22-bb51-c6d4d9e61354	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-20	Usage	DailyRecording	01a106ad-cf36-79b5-8c8f-4c13577bfec5	SKL/BDG/2026/VII/0001 2026-07-20	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-5.0000	95000.000000	6.0000	570000.00	-475000.00
01a106ad-cf4a-7e1d-9fe3-12dd64bae1ee	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-20	Usage	DailyRecording	01a106ad-cf36-79b5-8c8f-4c13577bfec5	SKL/BDG/2026/VII/0001 2026-07-20	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	10.0000	420000.00	-126000.00
01a106ad-d020-70d4-aa95-a5e6451a1613	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-21	Usage	DailyRecording	01a106ad-d00d-7069-8e1a-4653e2c1b488	SKL/BDG/2026/VII/0001 2026-07-21	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-158.9000	8150.000000	2450.9000	19974835.00	-1295035.00
01a106ad-d039-70ce-8fa4-0312a885e86f	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-22	Usage	DailyRecording	01a106ad-d027-7dc3-9716-afd16516d48c	SKL/BDG/2026/VII/0001 2026-07-22	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-178.4000	8150.000000	2272.5000	18520875.00	-1453960.00
01a106ad-d03b-7ca5-ba9d-145e964ecdda	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-22	Usage	DailyRecording	01a106ad-d027-7dc3-9716-afd16516d48c	SKL/BDG/2026/VII/0001 2026-07-22	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	7.0000	294000.00	-126000.00
01a106ad-d0db-714a-aa32-5c2ab6e1f72e	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-23	Usage	DailyRecording	01a106ad-d0cb-72a0-9381-697ced243e40	SKL/BDG/2026/VII/0001 2026-07-23	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-197.4000	8150.000000	2075.1000	16912065.00	-1608810.00
01a106ad-d0f4-7807-8d00-cfdcd92955ff	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-24	Usage	DailyRecording	01a106ad-d0e3-771a-9f12-829d30bfb43f	SKL/BDG/2026/VII/0001 2026-07-24	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-216.5000	8150.000000	1858.6000	15147590.00	-1764475.00
01a106ad-d0f6-79d0-ba54-31abe1c9b680	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-24	Usage	DailyRecording	01a106ad-d0e3-771a-9f12-829d30bfb43f	SKL/BDG/2026/VII/0001 2026-07-24	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	4.0000	168000.00	-126000.00
01a106ad-d115-709e-9c10-6a77530a1a9b	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-24	Receipt	GoodsReceipt	01a106ad-d113-7702-95fa-9936fda3cb97	BPB/BDG/2026/VII/0004	\N	3000.0000	8100.000000	3000.0000	24300000.00	24300000.00
01a106ad-d118-768a-b824-2f1db84f9fb5	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-24	Receipt	GoodsReceipt	01a106ad-d113-7702-95fa-9936fda3cb97	BPB/BDG/2026/VII/0004	\N	10450.0000	7850.000000	21700.0000	170907500.00	82032500.00
01a106ad-d152-7262-9fa4-3e5f43c696f7	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-24	Receipt	GoodsReceipt	01a106ad-d150-7d46-aad6-09ab900856a2	BPB/BDG/2026/VII/0005	\N	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-d155-75cf-8156-a390bc01c71d	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-24	Receipt	GoodsReceipt	01a106ad-d150-7d46-aad6-09ab900856a2	BPB/BDG/2026/VII/0005	\N	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-d157-7fdd-add0-1bb413ed074d	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-24	Receipt	GoodsReceipt	01a106ad-d150-7d46-aad6-09ab900856a2	BPB/BDG/2026/VII/0005	\N	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-d222-70e2-a2b4-568ff18e0b1b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-25	Usage	DailyRecording	01a106ad-d210-7471-89e2-885621735f5a	SKL/BDG/2026/VII/0001 2026-07-25	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-236.1000	8150.000000	1622.5000	13223375.00	-1924215.00
01a106ad-d238-7bf5-9f1c-d7e69b450983	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-26	Usage	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-255.5000	8150.000000	1367.0000	11141050.00	-2082325.00
01a106ad-d23b-7026-87d1-0adc9ea186ca	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-26	Usage	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	1.0000	42000.00	-126000.00
01a106ad-d259-7686-b262-281ba7676c1c	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-26	Receipt	GoodsReceipt	01a106ad-d257-72fc-9fab-d8d93779a1f7	BPB/BDG/2026/VII/0006	01a106ad-d05a-759c-a6ff-c3e67df7f41b	4500.0000	7400.000000	4500.0000	33300000.00	33300000.00
01a106ad-d2a3-79dd-b8dc-b64bcf17145b	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-26	ChickIn	ProductionCycle	01a106ad-d05a-759c-a6ff-c3e67df7f41b	SKL/BDG/2026/VII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-4500.0000	7400.000000	0.0000	0.00	-33300000.00
01a106ad-d2c7-789b-9414-246a2b714600	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-26	TransferOut	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3000.0000	8100.000000	0.0000	0.00	-24300000.00
01a106ad-d2cb-79fc-9a1d-c68633f4baea	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-26	TransferIn	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	3000.0000	8100.000000	3000.0000	24300000.00	24300000.00
01a106ad-d2ce-7b1e-a6dd-c21691ae7675	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-26	TransferOut	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-11.0000	95000.000000	0.0000	0.00	-1045000.00
01a106ad-d2d2-74e7-9ee9-aa63d9f599d5	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-26	TransferIn	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-d2d5-78cb-ab29-4c191c876361	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-26	TransferOut	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-d2d8-7744-96ab-3f9f7fa8a64c	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-26	TransferIn	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-d2dc-7148-84d7-34610f9da704	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-26	TransferOut	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-16.0000	42000.000000	0.0000	0.00	-672000.00
01a106ad-d2df-7cc2-b146-478ada353b79	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-26	TransferIn	StockTransfer	01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-d47b-7bc3-aa8e-be7c94bc45ce	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-26	UsageReversal	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	255.5000	8150.000000	1622.5000	13223375.00	2082325.00
01a106ad-d47e-71fa-85e5-c6c473edf033	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-26	UsageReversal	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	3.0000	42000.000000	4.0000	168000.00	126000.00
01a106ad-d47f-72c4-b62f-4302df82f46f	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-26	Usage	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-3.0000	42000.000000	1.0000	42000.00	-126000.00
01a106ad-d47f-7839-bd48-b02d95fa7447	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-26	Usage	DailyRecording	01a106ad-d229-72b8-a0cb-13574523872a	SKL/BDG/2026/VII/0001 2026-07-26	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-255.5000	8150.000000	1367.0000	11141050.00	-2082325.00
01a106ad-d4d0-7773-9f1e-1ea6635aa976	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-27	Usage	DailyRecording	01a106ad-d4bf-7b1f-aeba-04d9e885f75d	SKL/BDG/2026/VII/0002 2026-07-27	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-80.2000	8100.000000	2919.8000	23650380.00	-649620.00
01a106ad-d4ec-76a8-ad67-ea80684842fd	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-28	TransferOut	StockTransfer	01a106ad-d4e9-76d3-825a-71bb220f2d77	TRF/BDG/2026/VII/0004	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-11250.0000	7875.921659	10450.0000	82303381.34	-88604118.66
01a106ad-d4ef-7c6b-a1d0-0a8edb3a7429	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-28	TransferIn	StockTransfer	01a106ad-d4e9-76d3-825a-71bb220f2d77	TRF/BDG/2026/VII/0004	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	11250.0000	7875.921659	11250.0000	88604118.66	88604118.66
01a106ad-d678-7cdc-a1ac-8a0c02b3618f	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-29	Usage	DailyRecording	01a106ad-d668-7bd2-ba2f-19feb1499afd	SKL/BDG/2026/VII/0002 2026-07-29	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-119.4000	8100.000000	2700.6000	21874860.00	-967140.00
01a106ad-d6ad-7e5f-adbb-f552e70730ef	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-30	Usage	DailyRecording	01a106ad-d69b-79ef-9de6-106dbe430481	SKL/BDG/2026/VII/0002 2026-07-30	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-138.5000	8100.000000	2562.1000	20753010.00	-1121850.00
01a106ad-d6b0-7a64-b280-a4a5a1015220	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-30	Usage	DailyRecording	01a106ad-d69b-79ef-9de6-106dbe430481	SKL/BDG/2026/VII/0002 2026-07-30	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-5.0000	95000.000000	6.0000	570000.00	-475000.00
01a106ad-d6b3-7817-aacc-bd18677cab5f	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-30	Usage	DailyRecording	01a106ad-d69b-79ef-9de6-106dbe430481	SKL/BDG/2026/VII/0002 2026-07-30	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3.0000	42000.000000	10.0000	420000.00	-126000.00
01a106ad-d6d2-7c57-9728-8d400175bae3	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-30	Receipt	GoodsReceipt	01a106ad-d6d0-7981-97bd-a98ead54697d	BPB/CJR/2026/VII/0003	01a106ad-d332-7f38-9c34-260051b68ef3	5000.0000	7400.000000	5000.0000	37000000.00	37000000.00
01a106ad-d71d-76e6-b1bf-e1b16490f1e6	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-07-30	ChickIn	ProductionCycle	01a106ad-d332-7f38-9c34-260051b68ef3	SKL/CJR/2026/VII/0001	01a106ad-d332-7f38-9c34-260051b68ef3	-5000.0000	7400.000000	0.0000	0.00	-37000000.00
01a106ad-d733-7d87-802f-ffc2fa94774a	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-30	TransferOut	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	-3500.0000	8150.000000	0.0000	0.00	-28525000.00
01a106ad-d735-7d2c-a32a-8653c3dcb171	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-30	TransferIn	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	3500.0000	8150.000000	3500.0000	28525000.00	28525000.00
01a106ad-d737-75dc-aa2a-cd93f893a4ce	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-30	TransferOut	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	-11.0000	95000.000000	0.0000	0.00	-1045000.00
01a106ad-d73a-742f-b1d9-3b68a62a602a	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-30	TransferIn	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-d73d-7e63-ab68-b36599407360	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-30	TransferOut	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-d73e-72ee-92a4-0cdc64bf7a80	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-30	TransferIn	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-d740-7f98-92b8-ccdf0e6acfda	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-30	TransferOut	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	-16.0000	42000.000000	0.0000	0.00	-672000.00
01a106ad-d742-758e-a85c-97a8c64fa967	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-30	TransferIn	StockTransfer	01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-d7ef-7690-a113-8366faa16f36	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-31	Usage	DailyRecording	01a106ad-d7dc-793b-a59f-7494399fdf3c	SKL/BDG/2026/VII/0001 2026-07-31	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-352.2000	7875.921659	10897.8000	85830219.05	-2773899.61
01a106ad-d8c4-7968-a9fb-75035e4771fb	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-31	Usage	DailyRecording	01a106ad-d8b3-7a82-99b1-107355891e58	SKL/CJR/2026/VII/0001 2026-07-31	01a106ad-d332-7f38-9c34-260051b68ef3	-92.6000	8150.000000	3407.4000	27770310.00	-754690.00
01a106ad-d9ac-7cf6-ac43-33be40f07119	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-02	Usage	DailyRecording	01a106ad-d99a-7e7b-854e-21c0e67e5a59	SKL/BDG/2026/VII/0001 2026-08-02	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-390.4000	7875.921659	10136.1000	79831129.52	-3074759.82
01a106ad-da2b-7e27-a6f7-163668291fe2	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-02	Usage	DailyRecording	01a106ad-da1a-71a5-be59-96a512736a24	SKL/CJR/2026/VII/0001 2026-08-02	01a106ad-d332-7f38-9c34-260051b68ef3	-137.4000	8150.000000	3155.1000	25714065.00	-1119810.00
01a106ad-dac8-72f6-a78e-62be3822ff0b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-03	Usage	DailyRecording	01a106ad-dab6-75b9-ba82-58926ae9de49	SKL/BDG/2026/VII/0001 2026-08-03	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-409.5000	7875.921658	9726.6000	76605939.60	-3225189.92
01a106ad-daca-7a72-9fe3-b3e59cbdbb7b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-03	Usage	DailyRecording	01a106ad-dab6-75b9-ba82-58926ae9de49	SKL/BDG/2026/VII/0001 2026-08-03	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-5.0000	95000.000000	1.0000	95000.00	-475000.00
01a106ad-daf3-7bce-9b2d-83a42695dd02	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-03	Usage	DailyRecording	01a106ad-dae0-7c16-b9c7-e90c1053e113	SKL/BDG/2026/VII/0002 2026-08-03	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-214.5000	8100.000000	1817.5000	14721750.00	-1737450.00
01a106ad-daf6-7a57-b5de-0921a117e58c	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-03	Usage	DailyRecording	01a106ad-dae0-7c16-b9c7-e90c1053e113	SKL/BDG/2026/VII/0002 2026-08-03	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3.0000	42000.000000	4.0000	168000.00	-126000.00
01a106ad-db30-7665-97c0-a633cb181e5b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-04	Usage	DailyRecording	01a106ad-db1e-72cc-9abe-d8e672245066	SKL/BDG/2026/VII/0001 2026-08-04	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-428.8000	7875.921658	9297.8000	73228744.39	-3377195.21
01a106ad-dbf8-728a-adb1-12945d67dc3f	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-04	Usage	DailyRecording	01a106ad-dbe7-7177-aac8-6decd4265310	SKL/CJR/2026/VII/0001 2026-08-04	01a106ad-d332-7f38-9c34-260051b68ef3	-180.9000	8150.000000	2815.0000	22942250.00	-1474335.00
01a106ad-de0e-7bff-b099-565bbaaa841d	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-06	Usage	DailyRecording	01a106ad-ddfd-7461-9b07-1e06cbfdb108	SKL/BDG/2026/VII/0001 2026-08-06	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-467.4000	7875.921658	8382.3000	66018338.12	-3681205.78
01a106ad-de40-78e0-b46b-697de52fba67	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-06	Usage	DailyRecording	01a106ad-de2f-70ec-b894-ec4a5db80950	SKL/CJR/2026/VII/0001 2026-08-06	01a106ad-d332-7f38-9c34-260051b68ef3	-224.1000	8150.000000	2388.6000	19467090.00	-1826415.00
01a106ad-dead-7198-8178-e86649342611	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-07	Usage	DailyRecording	01a106ad-de9d-7045-bae7-5ff5c4901e79	SKL/BDG/2026/VII/0002 2026-08-07	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-291.3000	8100.000000	767.1000	6213510.00	-2359530.00
01a106ad-deaf-7eaf-9390-380cb4a64ec9	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-07	Usage	DailyRecording	01a106ad-de9d-7045-bae7-5ff5c4901e79	SKL/BDG/2026/VII/0002 2026-08-07	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-dec8-7173-ad9a-8fde1cba1ce8	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-07	Usage	DailyRecording	01a106ad-deb7-7b1b-84dd-c55f9a2e5486	SKL/CJR/2026/VII/0001 2026-08-07	01a106ad-d332-7f38-9c34-260051b68ef3	-245.7000	8150.000000	2142.9000	17464635.00	-2002455.00
01a106ad-d4af-78be-b803-1f27d5e93349	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-27	Usage	DailyRecording	01a106ad-d49f-779f-b620-5eb282c8da5a	SKL/BDG/2026/VII/0001 2026-07-27	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-274.8000	8150.000000	1092.2000	8901430.00	-2239620.00
01a106ad-d529-768b-869a-5272cd8e8831	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-28	Usage	DailyRecording	01a106ad-d517-7d09-ae16-0720df9f4314	SKL/BDG/2026/VII/0001 2026-07-28	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-294.3000	8150.000000	797.9000	6502885.00	-2398545.00
01a106ad-d52b-765d-a34d-b02c826b88c0	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-28	Usage	DailyRecording	01a106ad-d517-7d09-ae16-0720df9f4314	SKL/BDG/2026/VII/0001 2026-07-28	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-d546-727f-9b49-31dc9c7fa1ab	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-28	Usage	DailyRecording	01a106ad-d534-7171-b77e-3df14dc3dd74	SKL/BDG/2026/VII/0002 2026-07-28	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-99.8000	8100.000000	2820.0000	22842000.00	-808380.00
01a106ad-d549-719a-84ed-e05394fe96cb	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-28	Usage	DailyRecording	01a106ad-d534-7171-b77e-3df14dc3dd74	SKL/BDG/2026/VII/0002 2026-07-28	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3.0000	42000.000000	13.0000	546000.00	-126000.00
01a106ad-d567-7477-b3cb-97fcfcb8997e	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-28	Receipt	GoodsReceipt	01a106ad-d565-74c5-97c1-753986941b7f	BPB/CJR/2026/VII/0001	\N	3500.0000	8150.000000	3500.0000	28525000.00	28525000.00
01a106ad-d569-709d-9c2d-26613c041eb9	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-07-28	Receipt	GoodsReceipt	01a106ad-d565-74c5-97c1-753986941b7f	BPB/CJR/2026/VII/0001	\N	13850.0000	7900.000000	13850.0000	109415000.00	109415000.00
01a106ad-d5ab-7fd8-a878-9e987aa0bca0	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-07-28	Receipt	GoodsReceipt	01a106ad-d5a9-715f-82a1-c024dce8f91c	BPB/CJR/2026/VII/0002	\N	11.0000	95000.000000	11.0000	1045000.00	1045000.00
01a106ad-d5ae-719f-8bec-a598b3cdacbf	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-07-28	Receipt	GoodsReceipt	01a106ad-d5a9-715f-82a1-c024dce8f91c	BPB/CJR/2026/VII/0002	\N	5.0000	112000.000000	5.0000	560000.00	560000.00
01a106ad-d5af-7163-b9b8-fa42ade27b26	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-07-28	Receipt	GoodsReceipt	01a106ad-d5a9-715f-82a1-c024dce8f91c	BPB/CJR/2026/VII/0002	\N	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-d5ed-7a25-b3d2-e0b4f0911fcf	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-29	Usage	DailyRecording	01a106ad-d5db-7b92-96c1-5d808cfd6b93	SKL/BDG/2026/VII/0001 2026-07-29	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-313.5000	8150.000000	484.4000	3947860.00	-2555025.00
01a106ad-d694-707c-9ead-4e6d838d33c0	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-30	Usage	DailyRecording	01a106ad-d681-753f-aa82-920dbbe0e044	SKL/BDG/2026/VII/0001 2026-07-30	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-332.8000	8150.000000	151.6000	1235540.00	-2712320.00
01a106ad-d8ab-77ef-8fb7-0926ab3bba62	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-07-31	Usage	DailyRecording	01a106ad-d89a-7ccb-93bc-e0c4f3eb6802	SKL/BDG/2026/VII/0002 2026-07-31	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-157.8000	8100.000000	2404.3000	19474830.00	-1278180.00
01a106ad-d956-75bb-96d7-5e132f614617	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-01	Usage	DailyRecording	01a106ad-d93b-7666-b912-3aa7ef54a2f7	SKL/BDG/2026/VII/0001 2026-08-01	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-371.3000	7875.921658	10526.5000	82905889.34	-2924329.71
01a106ad-d972-72de-8b9d-69235018f619	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-01	Usage	DailyRecording	01a106ad-d960-7bc9-86c4-568869e29b05	SKL/BDG/2026/VII/0002 2026-08-01	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-176.8000	8100.000000	2227.5000	18042750.00	-1432080.00
01a106ad-d974-73a4-964a-756b70f052e0	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-01	Usage	DailyRecording	01a106ad-d960-7bc9-86c4-568869e29b05	SKL/BDG/2026/VII/0002 2026-08-01	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3.0000	42000.000000	7.0000	294000.00	-126000.00
01a106ad-d98f-76b8-9f5a-267374a3cc8c	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-01	Usage	DailyRecording	01a106ad-d97d-7253-a71c-fccdbc2b6b1d	SKL/CJR/2026/VII/0001 2026-08-01	01a106ad-d332-7f38-9c34-260051b68ef3	-114.9000	8150.000000	3292.5000	26833875.00	-936435.00
01a106ad-d991-7d69-adf3-a5c821877144	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-01	Usage	DailyRecording	01a106ad-d97d-7253-a71c-fccdbc2b6b1d	SKL/CJR/2026/VII/0001 2026-08-01	01a106ad-d332-7f38-9c34-260051b68ef3	-3.0000	42000.000000	13.0000	546000.00	-126000.00
01a106ad-d9c5-75e8-9349-99af6a465282	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-02	Usage	DailyRecording	01a106ad-d9b4-7094-a2dc-0318f02069f9	SKL/BDG/2026/VII/0002 2026-08-02	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-195.5000	8100.000000	2032.0000	16459200.00	-1583550.00
01a106ad-db10-7d7f-b4e4-3ef8ce13b5db	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-03	Usage	DailyRecording	01a106ad-daff-7358-b859-682e3fe7ac5c	SKL/CJR/2026/VII/0001 2026-08-03	01a106ad-d332-7f38-9c34-260051b68ef3	-159.2000	8150.000000	2995.9000	24416585.00	-1297480.00
01a106ad-db12-72a3-85bd-a1246237cd2a	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-03	Usage	DailyRecording	01a106ad-daff-7358-b859-682e3fe7ac5c	SKL/CJR/2026/VII/0001 2026-08-03	01a106ad-d332-7f38-9c34-260051b68ef3	-5.0000	95000.000000	6.0000	570000.00	-475000.00
01a106ad-db14-7468-8440-496082e394a5	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-03	Usage	DailyRecording	01a106ad-daff-7358-b859-682e3fe7ac5c	SKL/CJR/2026/VII/0001 2026-08-03	01a106ad-d332-7f38-9c34-260051b68ef3	-3.0000	42000.000000	10.0000	420000.00	-126000.00
01a106ad-db4a-7ef1-83e5-47a3de690844	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-04	Usage	DailyRecording	01a106ad-db37-7a15-bf21-c18e8dcb0718	SKL/BDG/2026/VII/0002 2026-08-04	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-233.8000	8100.000000	1583.7000	12827970.00	-1893780.00
01a106ad-ddba-7df0-9248-81f7b281f0b8	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-05	Usage	DailyRecording	01a106ad-ddaa-793c-a1dd-3c8cc15e60eb	SKL/BDG/2026/VII/0001 2026-08-05	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-448.1000	7875.921658	8849.7000	69699543.90	-3529200.49
01a106ad-ddd6-755c-84eb-3b65a8547495	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-05	Usage	DailyRecording	01a106ad-ddc2-7cc4-b0e4-774809cba35b	SKL/BDG/2026/VII/0002 2026-08-05	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-253.1000	8100.000000	1330.6000	10777860.00	-2050110.00
01a106ad-ddd9-76a5-88bf-1cbe3fe3f442	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-05	Usage	DailyRecording	01a106ad-ddc2-7cc4-b0e4-774809cba35b	SKL/BDG/2026/VII/0002 2026-08-05	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-3.0000	42000.000000	1.0000	42000.00	-126000.00
01a106ad-ddf2-7898-a795-b3294315b5d6	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-05	Usage	DailyRecording	01a106ad-dde2-718d-a538-f59490e33d84	SKL/CJR/2026/VII/0001 2026-08-05	01a106ad-d332-7f38-9c34-260051b68ef3	-202.3000	8150.000000	2612.7000	21293505.00	-1648745.00
01a106ad-ddf4-7049-b0f6-dad7c6f3d3ed	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-05	Usage	DailyRecording	01a106ad-dde2-718d-a538-f59490e33d84	SKL/CJR/2026/VII/0001 2026-08-05	01a106ad-d332-7f38-9c34-260051b68ef3	-3.0000	42000.000000	7.0000	294000.00	-126000.00
01a106ad-de27-757b-b2d3-cf52211e6bf3	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-06	Usage	DailyRecording	01a106ad-de16-7ea8-95b8-0fe918dd6b9c	SKL/BDG/2026/VII/0002 2026-08-06	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-272.2000	8100.000000	1058.4000	8573040.00	-2204820.00
01a106ad-de59-715a-8291-e0aaec9ab92b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-07	Usage	DailyRecording	01a106ad-de49-7b55-9483-061ba2ed0676	SKL/BDG/2026/VII/0001 2026-08-07	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-486.4000	7875.921659	7895.9000	62187489.83	-3830848.29
01a106ad-de70-79f4-aa3b-b0fa3956e1bb	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-07	TransferOut	StockTransfer	01a106ad-de6e-7454-87f3-422062f82097	TRF/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-10450.0000	7875.921659	0.0000	0.00	-82303381.34
01a106ad-de73-7119-93da-ac8accb2d44f	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-07	TransferIn	StockTransfer	01a106ad-de6e-7454-87f3-422062f82097	TRF/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	10450.0000	7875.921659	10450.0000	82303381.34	82303381.34
01a106ad-dee4-7925-b8e1-4722e0d7954b	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-08	Usage	DailyRecording	01a106ad-ded3-7406-af79-a4c2522ec215	SKL/BDG/2026/VII/0001 2026-08-08	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-505.6000	7875.921659	7390.3000	58205423.84	-3982065.99
01a106ad-df13-78a3-b2bb-347a99c4bff1	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-08	Usage	DailyRecording	01a106ad-df03-722c-87f7-1998f097527d	SKL/CJR/2026/VII/0001 2026-08-08	01a106ad-d332-7f38-9c34-260051b68ef3	-267.6000	8150.000000	1875.3000	15283695.00	-2180940.00
01a106ad-df40-7295-94ca-33c1369077e8	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-09	Usage	DailyRecording	01a106ad-df30-7706-b1cc-d066ae3d2ea6	SKL/BDG/2026/VII/0002 2026-08-09	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-329.8000	8100.000000	126.7000	1026270.00	-2671380.00
01a106ad-df58-72ea-9783-2a675399ba4d	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-09	Usage	DailyRecording	01a106ad-df48-7f6d-8c4e-ed4151ca4050	SKL/CJR/2026/VII/0001 2026-08-09	01a106ad-d332-7f38-9c34-260051b68ef3	-289.5000	8150.000000	1585.8000	12924270.00	-2359425.00
01a106ad-df5b-76c6-8f19-d5e3fffb1c1f	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-09	Usage	DailyRecording	01a106ad-df48-7f6d-8c4e-ed4151ca4050	SKL/CJR/2026/VII/0001 2026-08-09	01a106ad-d332-7f38-9c34-260051b68ef3	-3.0000	42000.000000	1.0000	42000.00	-126000.00
01a106ad-e0b9-7e56-8b60-c9be5bee6a2f	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-10	Usage	DailyRecording	01a106ad-e0a9-739d-a7d5-7ffdf7944ed8	SKL/BDG/2026/VII/0002 2026-08-10	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-348.9000	7875.921659	10101.1000	79555472.27	-2747909.07
01a106ad-e0ff-71cd-ba2d-7c720afb42d2	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-11	Usage	DailyRecording	01a106ad-e0ee-7d65-845c-fd49fb8c5aee	SKL/BDG/2026/VII/0001 2026-08-11	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-561.3000	7875.921660	5762.4000	45384210.97	-4420754.83
01a106ad-e166-7365-8fc2-5e6af2df0235	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-11	Usage	DailyRecording	01a106ad-e156-7fe8-a5d0-13f30df81435	SKL/CJR/2026/VII/0001 2026-08-11	01a106ad-d332-7f38-9c34-260051b68ef3	-333.0000	8150.000000	941.6000	7674040.00	-2713950.00
01a106ad-deca-734f-9949-5e85b7e0e620	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-07	Usage	DailyRecording	01a106ad-deb7-7b1b-84dd-c55f9a2e5486	SKL/CJR/2026/VII/0001 2026-08-07	01a106ad-d332-7f38-9c34-260051b68ef3	-3.0000	42000.000000	4.0000	168000.00	-126000.00
01a106ad-defc-738c-9220-af90eb1f7aa8	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-08	Usage	DailyRecording	01a106ad-deec-7768-8789-357dffb98706	SKL/BDG/2026/VII/0002 2026-08-08	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-310.6000	8100.000000	456.5000	3697650.00	-2515860.00
01a106ad-df28-7808-ab0b-b77f6049a7e4	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-09	Usage	DailyRecording	01a106ad-df1a-7883-9ff4-b0e5088b610f	SKL/BDG/2026/VII/0001 2026-08-09	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-524.0000	7875.921659	6866.3000	54078440.89	-4126982.95
01a106ad-e099-7134-ac68-2bbf194a3a29	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-10	Usage	DailyRecording	01a106ad-e089-7a12-aea8-7f44cc0d14c4	SKL/BDG/2026/VII/0001 2026-08-10	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-542.6000	7875.921659	6323.7000	49804965.80	-4273475.09
01a106ad-e0d1-7cc9-870d-a4c32b4d82ce	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-10	Usage	DailyRecording	01a106ad-e0c1-7c9f-b9d6-0b398cc41cf2	SKL/CJR/2026/VII/0001 2026-08-10	01a106ad-d332-7f38-9c34-260051b68ef3	-311.2000	8150.000000	1274.6000	10387990.00	-2536280.00
01a106ad-e115-72c6-820f-d8b2d9b4601f	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-11	Usage	DailyRecording	01a106ad-e106-71a3-9216-7abf48ba6b83	SKL/BDG/2026/VII/0002 2026-08-11	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-368.0000	7875.921659	9733.1000	76657133.10	-2898339.17
01a106ad-e12e-79e1-9396-3b285e238a5b	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-11	TransferOut	StockTransfer	01a106ad-e12c-7fed-8626-ef11d1a1aa64	TRF/CJR/2026/VIII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	-13850.0000	7900.000000	0.0000	0.00	-109415000.00
01a106ad-e131-7345-8fed-dd71bba0d901	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-11	TransferIn	StockTransfer	01a106ad-e12c-7fed-8626-ef11d1a1aa64	TRF/CJR/2026/VIII/0002	01a106ad-d332-7f38-9c34-260051b68ef3	13850.0000	7900.000000	13850.0000	109415000.00	109415000.00
01a106ad-e17f-7609-acc0-9442f51efe0e	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-12	Usage	DailyRecording	01a106ad-e170-7fea-bb81-240761e9556b	SKL/BDG/2026/VII/0001 2026-08-12	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-579.9000	7875.921659	5182.5000	40816964.00	-4567246.97
01a106ad-e1ac-79c8-8e6b-efffdef788bf	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-12	Usage	DailyRecording	01a106ad-e19c-7a9b-827a-5ed1103ae12d	SKL/CJR/2026/VII/0001 2026-08-12	01a106ad-d332-7f38-9c34-260051b68ef3	-354.8000	8150.000000	586.8000	4782420.00	-2891620.00
01a106ad-e1fe-7b50-b191-d0989556bf03	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-13	Usage	DailyRecording	01a106ad-e1eb-7783-ba8c-2f9734451ab4	SKL/CJR/2026/VII/0001 2026-08-13	01a106ad-d332-7f38-9c34-260051b68ef3	-376.3000	8150.000000	210.5000	1715575.00	-3066845.00
01a106ad-e233-7e57-8335-96cbb0f5537a	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-14	Usage	DailyRecording	01a106ad-e224-78c6-a952-812cec87f27c	SKL/BDG/2026/VII/0002 2026-08-14	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-425.4000	7875.921658	8514.3000	67057959.78	-3350417.07
01a106ad-e304-7a84-8e31-530ac14f0d19	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-15	Usage	DailyRecording	01a106ad-e2f2-7cc5-9ec1-12b227be53fc	SKL/BDG/2026/VII/0001 2026-08-15	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-634.4000	7875.921660	3333.8000	26256747.63	-4996484.70
01a106ad-e436-7c65-846e-79ff470325a2	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-15	Usage	DailyRecording	01a106ad-e425-79d1-8235-5ab8ed4b57a6	SKL/CJR/2026/VII/0001 2026-08-15	01a106ad-d332-7f38-9c34-260051b68ef3	-419.5000	7900.000000	13032.5000	102956750.00	-3314050.00
01a106ad-e565-71b0-aac7-0d13f18db6af	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-16	Usage	DailyRecording	01a106ad-e556-7750-af63-982615c773a3	SKL/BDG/2026/VII/0002 2026-08-16	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-463.3000	7875.921659	7606.8000	59910560.88	-3648914.50
01a106ad-e63d-7f16-8d80-e0c0fcfb1805	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-17	Usage	DailyRecording	01a106ad-e62b-7087-b227-8793ecf528fb	SKL/BDG/2026/VII/0001 2026-08-17	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-671.2000	7875.921659	2009.9000	15829814.94	-5286318.62
01a106ad-e697-774a-bcff-08915f364799	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-18	Usage	DailyRecording	01a106ad-e683-7cf3-a6bd-7cad8559e7f7	SKL/BDG/2026/VII/0001 2026-08-18	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-689.5000	7875.921658	1320.4000	10399366.96	-5430447.98
01a106ad-e7b9-7b44-bce5-d62775c9ea2c	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-18	Usage	DailyRecording	01a106ad-e7a3-71cd-b902-0decbcda1f55	SKL/CJR/2026/VII/0001 2026-08-18	01a106ad-d332-7f38-9c34-260051b68ef3	-483.9000	7900.000000	11644.7000	91993130.00	-3822810.00
01a106ad-ea47-76fb-8976-d5e3ccd24203	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-19	Usage	DailyRecording	01a106ad-ea33-7b0c-b2b0-a3259ecd9ea0	SKL/CJR/2026/VII/0001 2026-08-19	01a106ad-d332-7f38-9c34-260051b68ef3	-505.6000	7900.000000	11139.1000	87998890.00	-3994240.00
01a106ad-eaf4-7aa1-bddc-d2a181375b2a	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-20	Usage	DailyRecording	01a106ad-eae3-7142-bb16-27832515c562	SKL/BDG/2026/VII/0001 2026-08-20	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-241.8000	7875.921657	606.9000	4779896.85	-1904397.86
01a106ad-eb95-7e0d-8216-e98fbf3ca510	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-20	Usage	DailyRecording	01a106ad-eb84-718e-954b-eea4a9013bc0	SKL/BDG/2026/VII/0002 2026-08-20	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-537.7000	7875.921659	5567.0000	43845255.87	-4234883.08
01a106ad-ec84-7267-aa5e-416296ffcbe1	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-21	Usage	DailyRecording	01a106ad-ec76-7ec6-9643-136d98207d00	SKL/BDG/2026/VII/0002 2026-08-21	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-556.1000	7875.921658	5010.9000	39465455.84	-4379800.03
01a106ad-ef3c-7726-aff1-a6018747e234	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-22	Usage	DailyRecording	01a106ad-ef2c-7dc3-9401-0b50b567af2d	SKL/CJR/2026/VII/0001 2026-08-22	01a106ad-d332-7f38-9c34-260051b68ef3	-568.7000	7900.000000	9495.7000	75016030.00	-4492730.00
01a106ad-ef99-79db-800d-29d0d50e63b9	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-23	Usage	DailyRecording	01a106ad-ef8a-762f-b87b-4dfa0cc11dfa	SKL/CJR/2026/VII/0001 2026-08-23	01a106ad-d332-7f38-9c34-260051b68ef3	-589.3000	7900.000000	8906.4000	70360560.00	-4655470.00
01a106ad-efb2-7180-b063-526c2866da54	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-08-23	Receipt	GoodsReceipt	01a106ad-efb0-7ccf-a2ae-1b8ed8e23d9d	BPB/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	7000.0000	7400.000000	7000.0000	51800000.00	51800000.00
01a106ad-effa-721a-8800-46178b1dc902	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-08-23	ChickIn	ProductionCycle	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	SKL/CJR/2026/VIII/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-7000.0000	7400.000000	0.0000	0.00	-51800000.00
01a106ad-f00f-7e70-af50-c37739363ae8	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-23	TransferOut	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4550.0000	8100.000000	0.0000	0.00	-36855000.00
01a106ad-f011-7fc8-9f9d-ece7abb0725e	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-23	TransferIn	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	4550.0000	8100.000000	4550.0000	36855000.00	36855000.00
01a106ad-f013-7259-9980-f4e945b2c65e	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-23	TransferOut	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-15.0000	95000.000000	0.0000	0.00	-1425000.00
01a106ad-f014-7704-af28-91078fc091f5	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-23	TransferIn	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	15.0000	95000.000000	15.0000	1425000.00	1425000.00
01a106ad-f016-736d-ab7d-db8cb75290d2	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-23	TransferOut	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-7.0000	112000.000000	0.0000	0.00	-784000.00
01a106ad-f018-746e-a433-9ab190e65250	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-23	TransferIn	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	7.0000	112000.000000	7.0000	784000.00	784000.00
01a106ad-f019-7914-930e-613fa2a168e6	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-23	TransferOut	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-21.0000	42000.000000	0.0000	0.00	-882000.00
01a106ad-f01b-7d4d-ae93-5977887188c1	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-23	TransferIn	StockTransfer	01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	21.0000	42000.000000	21.0000	882000.00	882000.00
01a106ad-f22b-7568-89c9-23325e6fb5ee	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-24	Usage	DailyRecording	01a106ad-f21d-729c-aed3-eb1298a6ae34	SKL/BDG/2026/VII/0002 2026-08-24	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-611.3000	7875.921659	3231.9000	25454191.21	-4814550.91
01a106ad-f258-758a-8901-9be72d32900c	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-24	Usage	DailyRecording	01a106ad-f249-7711-bf44-ca51c35a89d5	SKL/CJR/2026/VIII/0001 2026-08-24	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-121.1000	8100.000000	4428.9000	35874090.00	-980910.00
01a106ad-f32c-7012-8918-01f1bf0a3076	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-25	Usage	DailyRecording	01a106ad-f31c-7d26-a64f-c2ee00ca8c01	SKL/CJR/2026/VII/0001 2026-08-25	01a106ad-d332-7f38-9c34-260051b68ef3	-631.1000	7900.000000	7664.9000	60552710.00	-4985690.00
01a106ad-f344-717e-84e0-1f3461b511ad	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-25	Usage	DailyRecording	01a106ad-f334-7b2b-9d13-3a1c9e1852c7	SKL/CJR/2026/VIII/0001 2026-08-25	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-150.6000	8100.000000	4278.3000	34654230.00	-1219860.00
01a106ad-f346-7d09-bbf3-c8913885238d	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-25	Usage	DailyRecording	01a106ad-f334-7b2b-9d13-3a1c9e1852c7	SKL/CJR/2026/VIII/0001 2026-08-25	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4.0000	42000.000000	17.0000	714000.00	-168000.00
01a106ad-f479-7632-9552-4cc9a8430426	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-26	Usage	DailyRecording	01a106ad-f467-7e62-b402-597ceab70307	SKL/CJR/2026/VII/0001 2026-08-26	01a106ad-d332-7f38-9c34-260051b68ef3	-651.2000	7900.000000	7013.7000	55408230.00	-5144480.00
01a106ad-e168-7806-be0c-af390b20f60a	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-11	Usage	DailyRecording	01a106ad-e156-7fe8-a5d0-13f30df81435	SKL/CJR/2026/VII/0001 2026-08-11	01a106ad-d332-7f38-9c34-260051b68ef3	-5.0000	112000.000000	0.0000	0.00	-560000.00
01a106ad-e195-7cfa-9847-85593a5d077e	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-12	Usage	DailyRecording	01a106ad-e186-7c90-9e1a-ebc0165bfbf3	SKL/BDG/2026/VII/0002 2026-08-12	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-387.2000	7875.921659	9345.9000	73607576.23	-3049556.87
01a106ad-e1c4-7f15-b8d3-f18fc1e1b2b0	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-13	Usage	DailyRecording	01a106ad-e1b4-7f1e-9560-274181fe5d35	SKL/BDG/2026/VII/0001 2026-08-13	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-597.9000	7875.921659	4584.6000	36107950.44	-4709013.56
01a106ad-e1df-71be-97c3-192809ce8000	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-13	Usage	DailyRecording	01a106ad-e1cc-7eb2-b0e9-13d64e1f9436	SKL/BDG/2026/VII/0002 2026-08-13	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-406.2000	7875.921659	8939.7000	70408376.85	-3199199.38
01a106ad-e1e1-7445-8e77-6e9d41882db3	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-13	Usage	DailyRecording	01a106ad-e1cc-7eb2-b0e9-13d64e1f9436	SKL/BDG/2026/VII/0002 2026-08-13	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-5.0000	95000.000000	1.0000	95000.00	-475000.00
01a106ad-e21d-7ca6-9f23-66117a94c9ed	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-14	Usage	DailyRecording	01a106ad-e20d-7232-94bc-fcc00d40190d	SKL/BDG/2026/VII/0001 2026-08-14	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-616.4000	7875.921659	3968.2000	31253232.33	-4854718.11
01a106ad-e255-727a-a3eb-c6b08d4da717	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-14	Usage	DailyRecording	01a106ad-e23b-7dbe-a0a4-91826cf607c3	SKL/CJR/2026/VII/0001 2026-08-14	01a106ad-d332-7f38-9c34-260051b68ef3	-398.0000	7900.000000	13452.0000	106270800.00	-3144200.00
01a106ad-e41d-70e0-82a1-c2d87075fee0	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-15	Usage	DailyRecording	01a106ad-e40c-73c3-b6b5-04b13e2ea61d	SKL/BDG/2026/VII/0002 2026-08-15	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-444.2000	7875.921659	8070.1000	63559475.38	-3498484.40
01a106ad-e54e-75de-b113-1b65b3e27562	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-16	Usage	DailyRecording	01a106ad-e53f-760c-9de6-c4c97b670251	SKL/BDG/2026/VII/0001 2026-08-16	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-652.7000	7875.921660	2681.1000	21116133.56	-5140614.07
01a106ad-e57b-790f-8361-b6843820470f	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-16	Usage	DailyRecording	01a106ad-e56d-7a82-87bc-a60b421b2dcc	SKL/CJR/2026/VII/0001 2026-08-16	01a106ad-d332-7f38-9c34-260051b68ef3	-441.2000	7900.000000	12591.3000	99471270.00	-3485480.00
01a106ad-e658-7017-93a4-c4885ee02567	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-17	Usage	DailyRecording	01a106ad-e646-7b54-aeb1-696da1189bb7	SKL/BDG/2026/VII/0002 2026-08-17	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-482.1000	7875.921660	7124.7000	56113579.05	-3796981.83
01a106ad-e676-71ed-b3bd-cd41d1d691b3	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-17	Usage	DailyRecording	01a106ad-e662-7e9b-85f2-527be0a0c88c	SKL/CJR/2026/VII/0001 2026-08-17	01a106ad-d332-7f38-9c34-260051b68ef3	-462.7000	7900.000000	12128.6000	95815940.00	-3655330.00
01a106ad-e679-76db-8eed-bc9336836829	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-17	Usage	DailyRecording	01a106ad-e662-7e9b-85f2-527be0a0c88c	SKL/CJR/2026/VII/0001 2026-08-17	01a106ad-d332-7f38-9c34-260051b68ef3	-5.0000	95000.000000	1.0000	95000.00	-475000.00
01a106ad-e797-7b4c-8290-dc94b844a154	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-18	Usage	DailyRecording	01a106ad-e782-7b39-87ef-cf0b3cf3fcbe	SKL/BDG/2026/VII/0002 2026-08-18	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-500.7000	7875.921660	6624.0000	52170105.07	-3943473.98
01a106ad-e7d9-7e08-859c-cbb66dfa5a16	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-19	Usage	DailyRecording	01a106ad-e7c3-7674-a271-ed7cfc16e36a	SKL/BDG/2026/VII/0001 2026-08-19	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-471.7000	7875.921660	848.7000	6684294.71	-3715072.25
01a106ad-e982-7d9f-9915-d6ff75a68a44	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-19	Usage	DailyRecording	01a106ad-e970-70d4-b497-f263237ef95c	SKL/BDG/2026/VII/0002 2026-08-19	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-519.3000	7875.921659	6104.7000	48080138.95	-4089966.12
01a106ad-ebad-717f-8828-37f603f9561f	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-20	Usage	DailyRecording	01a106ad-eb9d-7ae2-b878-5808d3e51052	SKL/CJR/2026/VII/0001 2026-08-20	01a106ad-d332-7f38-9c34-260051b68ef3	-526.7000	7900.000000	10612.4000	83837960.00	-4160930.00
01a106ad-ec9c-7a58-bd13-d0577be0d3c8	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-21	Usage	DailyRecording	01a106ad-ec8c-711c-a16d-b89bca847c32	SKL/CJR/2026/VII/0001 2026-08-21	01a106ad-d332-7f38-9c34-260051b68ef3	-548.0000	7900.000000	10064.4000	79508760.00	-4329200.00
01a106ad-ecb4-7e67-ac44-60bf10798672	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-21	Receipt	GoodsReceipt	01a106ad-ecb2-7cf5-bc99-7307f161b5bc	BPB/CJR/2026/VIII/0001	\N	4550.0000	8100.000000	4550.0000	36855000.00	36855000.00
01a106ad-ecb6-7b46-953d-7527d17fb6c2	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-21	Receipt	GoodsReceipt	01a106ad-ecb2-7cf5-bc99-7307f161b5bc	BPB/CJR/2026/VIII/0001	\N	19300.0000	7850.000000	19300.0000	151505000.00	151505000.00
01a106ad-eceb-7a24-842b-f4f239888360	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-21	Receipt	GoodsReceipt	01a106ad-ece9-7ff1-aad4-f1d48c18557f	BPB/CJR/2026/VIII/0002	\N	15.0000	95000.000000	15.0000	1425000.00	1425000.00
01a106ad-eced-7d86-874a-9dd9c621aeb5	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-21	Receipt	GoodsReceipt	01a106ad-ece9-7ff1-aad4-f1d48c18557f	BPB/CJR/2026/VIII/0002	\N	7.0000	112000.000000	7.0000	784000.00	784000.00
01a106ad-ecef-7b49-b5d5-9759c367a6c9	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-21	Receipt	GoodsReceipt	01a106ad-ece9-7ff1-aad4-f1d48c18557f	BPB/CJR/2026/VIII/0002	\N	21.0000	42000.000000	21.0000	882000.00	882000.00
01a106ad-edeb-7c93-b2b6-0290471429a7	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-22	ReturnOut	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-151.6000	8150.000000	0.0000	0.00	-1235540.00
01a106ad-eded-7f05-94fe-a60cd8138f73	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-22	ReturnIn	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	151.6000	8150.000000	151.6000	1235540.00	1235540.00
01a106ad-edef-7dba-8d70-133229aee0f3	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-22	ReturnOut	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-1.0000	95000.000000	0.0000	0.00	-95000.00
01a106ad-edf1-78be-b22b-2612ba45eec1	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-22	ReturnIn	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1.0000	95000.000000	1.0000	95000.00	95000.00
01a106ad-edf3-7d5e-a66b-2e4fede5195e	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-22	ReturnOut	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-1.0000	42000.000000	0.0000	0.00	-42000.00
01a106ad-edf5-738c-a59f-2fe00a1a97df	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-22	ReturnIn	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1.0000	42000.000000	1.0000	42000.00	42000.00
01a106ad-edf7-7ec0-864a-b6e5399b2d29	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-22	ReturnOut	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	-606.9000	7875.921651	0.0000	0.00	-4779896.85
01a106ad-edf9-74aa-8f08-e8d9f8d76cf7	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-22	ReturnIn	StockReturn	01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	606.9000	7875.921651	606.9000	4779896.85	4779896.85
01a106ad-ef26-7573-856b-557dfb9233df	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-22	Usage	DailyRecording	01a106ad-ef15-7d1f-94b2-f9150c6240ee	SKL/BDG/2026/VII/0002 2026-08-22	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-574.7000	7875.921659	4436.2000	34939163.66	-4526292.18
01a106ad-ef83-7c0e-ad8f-0e4b6cc44c51	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-23	Usage	DailyRecording	01a106ad-ef74-730a-a234-b1934d1010fa	SKL/BDG/2026/VII/0002 2026-08-23	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-593.0000	7875.921658	3843.2000	30268742.12	-4670421.54
01a106ad-f242-796d-a4ae-2b100504a729	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-24	Usage	DailyRecording	01a106ad-f232-7573-8599-5c09641649d8	SKL/CJR/2026/VII/0001 2026-08-24	01a106ad-d332-7f38-9c34-260051b68ef3	-610.4000	7900.000000	8296.0000	65538400.00	-4822160.00
01a106ad-f315-7a73-9aa8-7b36be5912ed	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-25	Usage	DailyRecording	01a106ad-f306-7f5f-86ce-626ab2cdf147	SKL/BDG/2026/VII/0002 2026-08-25	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-629.6000	7875.921659	2602.3000	20495510.93	-4958680.28
01a106ad-f3f2-7386-8299-b104fdf698bd	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-26	Usage	DailyRecording	01a106ad-f3e2-7d84-8967-62c819b50a45	SKL/BDG/2026/VII/0002 2026-08-26	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-647.8000	7875.921658	1954.5000	15393488.88	-5102022.05
01a106ad-f4ea-7af0-bf9e-d5253a1471da	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-26	Usage	DailyRecording	01a106ad-f4d4-7ace-a351-6b1924b17cbd	SKL/CJR/2026/VIII/0001 2026-08-26	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-180.2000	8100.000000	4098.1000	33194610.00	-1459620.00
01a106ad-f545-7a03-97a4-27501d5fb136	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-27	Usage	DailyRecording	01a106ad-f532-7382-a010-a2640ea58141	SKL/CJR/2026/VII/0001 2026-08-27	01a106ad-d332-7f38-9c34-260051b68ef3	-671.7000	7900.000000	6342.0000	50101800.00	-5306430.00
01a106ad-f57d-73d9-9588-b8387fffffcf	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-28	Usage	DailyRecording	01a106ad-f56d-7fcf-afdf-c0d3f34ddc19	SKL/BDG/2026/VII/0002 2026-08-28	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-342.1000	7875.921660	946.0000	7450621.89	-2694352.80
01a106ad-f702-7169-8c71-4cf57277157d	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-28	Usage	DailyRecording	01a106ad-f6f4-757b-8841-bd32218bf1a7	SKL/CJR/2026/VIII/0001 2026-08-28	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-238.9000	8100.000000	3649.8000	29563380.00	-1935090.00
01a106ad-f507-7988-8275-6b00459c008d	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-27	Usage	DailyRecording	01a106ad-f4f6-7e81-a33a-0bcbb7962ab5	SKL/BDG/2026/VII/0002 2026-08-27	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-666.4000	7875.921658	1288.1000	10144974.69	-5248514.19
01a106ad-f55f-726e-a9d0-3d7f2c9b903b	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-27	Usage	DailyRecording	01a106ad-f54e-7895-b6b7-43aa74cdb435	SKL/CJR/2026/VIII/0001 2026-08-27	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-209.4000	8100.000000	3888.7000	31498470.00	-1696140.00
01a106ad-f562-7182-a2b9-2ec99a00ab95	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-27	Usage	DailyRecording	01a106ad-f54e-7895-b6b7-43aa74cdb435	SKL/CJR/2026/VIII/0001 2026-08-27	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-7.0000	95000.000000	8.0000	760000.00	-665000.00
01a106ad-f564-7574-aa39-8a4733431baf	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-27	Usage	DailyRecording	01a106ad-f54e-7895-b6b7-43aa74cdb435	SKL/CJR/2026/VIII/0001 2026-08-27	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4.0000	42000.000000	13.0000	546000.00	-168000.00
01a106ad-f5e9-725f-9749-44f8aa1ea362	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-28	Receipt	GoodsReceipt	01a106ad-f5e8-7414-89fa-95e6aee15d5c	BPB/BDG/2026/VIII/0001	\N	3800.0000	8150.000000	3951.6000	32205540.00	30970000.00
01a106ad-f5ec-7606-a351-20092ff56ed4	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-28	Receipt	GoodsReceipt	01a106ad-f5e8-7414-89fa-95e6aee15d5c	BPB/BDG/2026/VIII/0001	\N	14700.0000	7900.000000	15306.9000	120909896.85	116130000.00
01a106ad-f626-7132-aa37-f33dc10c7ad6	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-28	Receipt	GoodsReceipt	01a106ad-f623-7d55-9b20-a1b5d900f045	BPB/BDG/2026/VIII/0002	\N	13.0000	95000.000000	14.0000	1330000.00	1235000.00
01a106ad-f628-7ae2-aef6-3d2b6680e1d5	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-28	Receipt	GoodsReceipt	01a106ad-f623-7d55-9b20-a1b5d900f045	BPB/BDG/2026/VIII/0002	\N	6.0000	112000.000000	6.0000	672000.00	672000.00
01a106ad-f62a-759d-85fb-a2dce3d9f1b2	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-28	Receipt	GoodsReceipt	01a106ad-f623-7d55-9b20-a1b5d900f045	BPB/BDG/2026/VIII/0002	\N	16.0000	42000.000000	17.0000	714000.00	672000.00
01a106ad-f661-7521-adc9-694fa5146e21	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-28	Usage	DailyRecording	01a106ad-f650-75c7-9fba-80ac5ac0a46f	SKL/CJR/2026/VII/0001 2026-08-28	01a106ad-d332-7f38-9c34-260051b68ef3	-692.2000	7900.000000	5649.8000	44633420.00	-5468380.00
01a106ad-f719-7c70-bc4f-52f03ed0c3e8	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-29	Usage	DailyRecording	01a106ad-f709-7db8-aca5-9deaf815b5f3	SKL/BDG/2026/VII/0002 2026-08-29	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-350.4000	7875.921660	595.6000	4690898.94	-2759722.95
01a106ad-f758-7d0e-8457-1ef516f8c181	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-29	Usage	DailyRecording	01a106ad-f749-7f43-9f65-0d6bbdfebc35	SKL/CJR/2026/VII/0001 2026-08-29	01a106ad-d332-7f38-9c34-260051b68ef3	-712.4000	7900.000000	4937.4000	39005460.00	-5627960.00
01a106ad-f76f-7714-a407-0a46f82fc150	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-29	Usage	DailyRecording	01a106ad-f75f-7fef-9bb9-77c088b6c641	SKL/CJR/2026/VIII/0001 2026-08-29	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-268.1000	8100.000000	3381.7000	27391770.00	-2171610.00
01a106ad-f770-7b8d-9cb2-cb6e3cdcec36	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-29	Usage	DailyRecording	01a106ad-f75f-7fef-9bb9-77c088b6c641	SKL/CJR/2026/VIII/0001 2026-08-29	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4.0000	42000.000000	9.0000	378000.00	-168000.00
01a106ad-f7ea-73f3-b119-403fddf7eed9	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-08-30	Receipt	GoodsReceipt	01a106ad-f7e8-755b-9182-950be853462c	BPB/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	6000.0000	7400.000000	6000.0000	44400000.00	44400000.00
01a106ad-f837-752a-9162-9d3b54af9a32	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-08-30	ChickIn	ProductionCycle	01a106ad-f40d-7a20-b733-56168fcec265	SKL/BDG/2026/VIII/0001	01a106ad-f40d-7a20-b733-56168fcec265	-6000.0000	7400.000000	0.0000	0.00	-44400000.00
01a106ad-f850-7441-98a0-4e7819811c3a	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-30	TransferOut	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	-3800.0000	8150.000000	151.6000	1235540.00	-30970000.00
01a106ad-f852-7c56-9e8f-9d30a1491f28	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-30	TransferIn	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	3800.0000	8150.000000	3800.0000	30970000.00	30970000.00
01a106ad-f854-7e41-a5bf-ffed551f28e4	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-30	TransferOut	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	-13.0000	95000.000000	1.0000	95000.00	-1235000.00
01a106ad-f856-79e8-b549-78b768f58ab2	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-30	TransferIn	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	13.0000	95000.000000	13.0000	1235000.00	1235000.00
01a106ad-f858-749e-8d90-f5c5be196355	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-30	TransferOut	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	-6.0000	112000.000000	0.0000	0.00	-672000.00
01a106ad-f85a-730b-b2c4-e8adba98325f	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-08-30	TransferIn	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	6.0000	112000.000000	6.0000	672000.00	672000.00
01a106ad-f85c-7c09-9edb-2a067cb793ab	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-30	TransferOut	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	-16.0000	42000.000000	1.0000	42000.00	-672000.00
01a106ad-f85e-761c-a374-f36264b560d5	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-30	TransferIn	StockTransfer	01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-f40d-7a20-b733-56168fcec265	16.0000	42000.000000	16.0000	672000.00	672000.00
01a106ad-f894-7751-93ad-e70bce300f05	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-30	Usage	DailyRecording	01a106ad-f884-7c25-b2e0-734cf1afa507	SKL/CJR/2026/VII/0001 2026-08-30	01a106ad-d332-7f38-9c34-260051b68ef3	-733.2000	7900.000000	4204.2000	33213180.00	-5792280.00
01a106ad-fa45-7fcf-86df-0c5973f1db60	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-31	ReturnOut	StockReturn	01a106ad-fa43-7c1d-9eb8-33609a30db39	RTR/BDG/2026/VIII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-1.0000	95000.000000	0.0000	0.00	-95000.00
01a106ad-fa46-71f3-a976-4e6a4bd013e0	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-08-31	ReturnIn	StockReturn	01a106ad-fa43-7c1d-9eb8-33609a30db39	RTR/BDG/2026/VIII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	1.0000	95000.000000	2.0000	190000.00	95000.00
01a106ad-fa48-7295-92eb-f9f62d1408f7	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-31	ReturnOut	StockReturn	01a106ad-fa43-7c1d-9eb8-33609a30db39	RTR/BDG/2026/VIII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-1.0000	42000.000000	0.0000	0.00	-42000.00
01a106ad-fa4a-75dc-9dd3-7b14a80e104a	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-31	ReturnIn	StockReturn	01a106ad-fa43-7c1d-9eb8-33609a30db39	RTR/BDG/2026/VIII/0003	01a106ad-d05a-759c-a6ff-c3e67df7f41b	1.0000	42000.000000	2.0000	84000.00	42000.00
01a106ad-faca-7034-aed2-4032f518b3b2	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	Usage	DailyRecording	01a106ad-fab0-7a61-b55c-ecf6a30f1748	SKL/BDG/2026/VIII/0001 2026-08-31	01a106ad-f40d-7a20-b733-56168fcec265	-101.7000	8149.265516	3825.0000	31170940.60	-828780.30
01a106ad-fbca-731a-a05b-f1f935028492	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-01	Usage	DailyRecording	01a106ad-fbb7-70e1-a993-e0a2516e5837	SKL/CJR/2026/VII/0001 2026-09-01	01a106ad-d332-7f38-9c34-260051b68ef3	-773.5000	7900.000000	2677.2000	21149880.00	-6110650.00
01a106ad-fca4-7ce8-b69d-495b414a8cd7	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-02	Usage	DailyRecording	01a106ad-fc95-7669-9952-a41217db083d	SKL/CJR/2026/VII/0001 2026-09-02	01a106ad-d332-7f38-9c34-260051b68ef3	-793.6000	7900.000000	1883.6000	14880440.00	-6269440.00
01a106ad-fcdc-7b99-9785-e117d0ce6f01	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-02	Usage	DailyRecording	01a106ad-fccb-7a7f-ae91-dd67ac698fe8	SKL/CJR/2026/VIII/0001 2026-09-02	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-383.8000	8100.000000	2020.4000	16365240.00	-3108780.00
01a106ad-fcde-7d8b-97a9-b1385a6eadda	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-02	Usage	DailyRecording	01a106ad-fccb-7a7f-ae91-dd67ac698fe8	SKL/CJR/2026/VIII/0001 2026-09-02	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4.0000	42000.000000	1.0000	42000.00	-168000.00
01a106ad-fd0e-7705-8b52-214925fea875	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-03	Usage	DailyRecording	01a106ad-fd00-7887-b6a2-16b1538dc55c	SKL/CJR/2026/VII/0001 2026-09-03	01a106ad-d332-7f38-9c34-260051b68ef3	-541.8000	7900.000000	1341.8000	10600220.00	-4280220.00
01a106ad-fd88-7618-935c-ac9375d86368	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-03	Usage	DailyRecording	01a106ad-fd78-7862-aeca-710b6952e364	SKL/CJR/2026/VIII/0001 2026-09-03	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-413.0000	8100.000000	1607.4000	13019940.00	-3345300.00
01a106ad-fe6b-7897-91d0-2fc60a81e1d2	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-04	Usage	DailyRecording	01a106ad-fe5b-7f78-8851-e48768b1eaa2	SKL/CJR/2026/VII/0001 2026-09-04	01a106ad-d332-7f38-9c34-260051b68ef3	-277.6000	7900.000000	1064.2000	8407180.00	-2193040.00
01a106ad-ff2a-7240-8e0c-bf5c0cfdabf8	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-04	Usage	DailyRecording	01a106ad-ff1a-723a-bd71-f4a0c33f6f7b	SKL/CJR/2026/VIII/0001 2026-09-04	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-442.2000	8100.000000	1165.2000	9438120.00	-3581820.00
01a106ad-ff2d-7c70-bf73-ab78ec1068c8	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-04	Usage	DailyRecording	01a106ad-ff1a-723a-bd71-f4a0c33f6f7b	SKL/CJR/2026/VIII/0001 2026-09-04	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-7.0000	112000.000000	0.0000	0.00	-784000.00
01a106ad-ff43-7496-a09c-9fda1f51ec80	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-05	Usage	DailyRecording	01a106ad-ff34-7ae4-834e-f02e7b6ebd31	SKL/BDG/2026/VIII/0001 2026-09-05	01a106ad-f40d-7a20-b733-56168fcec265	-225.4000	8149.265516	2944.5000	23995512.31	-1836844.45
01a106ad-ff45-7c86-99c6-00dc974fcb01	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-05	Usage	DailyRecording	01a106ad-ff34-7ae4-834e-f02e7b6ebd31	SKL/BDG/2026/VIII/0001 2026-09-05	01a106ad-f40d-7a20-b733-56168fcec265	-3.0000	42000.000000	7.0000	294000.00	-126000.00
01a106ad-f8ab-7fba-961c-9622c9ca773e	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-30	Usage	DailyRecording	01a106ad-f89b-7a6d-b469-a34685d639d1	SKL/CJR/2026/VIII/0001 2026-08-30	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-297.3000	8100.000000	3084.4000	24983640.00	-2408130.00
01a106ad-f9e5-71df-a626-d7799239121d	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	ReturnOut	StockReturn	01a106ad-f9df-72b6-8bd3-82399696b6f7	RTR/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-126.7000	8100.000000	0.0000	0.00	-1026270.00
01a106ad-f9e7-7bc6-9e37-3370811e067b	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	ReturnIn	StockReturn	01a106ad-f9df-72b6-8bd3-82399696b6f7	RTR/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	126.7000	8100.000000	278.3000	2261810.00	1026270.00
01a106ad-f9e9-7df4-964d-1fad29804bbf	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-31	ReturnOut	StockReturn	01a106ad-f9df-72b6-8bd3-82399696b6f7	RTR/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	-595.6000	7875.921659	0.0000	0.00	-4690898.94
01a106ad-f9eb-7841-8fa0-712eb432bff6	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	TransferOut	StockTransfer	01a106ad-f9e3-77ab-a80a-c5fa58f74613	TRF/BDG/2026/VIII/0004	01a106ad-f40d-7a20-b733-56168fcec265	-126.7000	8127.236795	151.6000	1232089.10	-1029720.90
01a106ad-f9eb-7d9c-b9e8-a076b5d25e54	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-31	ReturnIn	StockReturn	01a106ad-f9df-72b6-8bd3-82399696b6f7	RTR/BDG/2026/VIII/0002	01a106ad-d05a-759c-a6ff-c3e67df7f41b	595.6000	7875.921659	15902.5000	125600795.79	4690898.94
01a106ad-f9ee-7672-9891-7b2ac17c3834	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	TransferIn	StockTransfer	01a106ad-f9e3-77ab-a80a-c5fa58f74613	TRF/BDG/2026/VIII/0004	01a106ad-f40d-7a20-b733-56168fcec265	126.7000	8127.236780	3926.7000	31999720.90	1029720.90
01a106ad-f9ee-7bd6-b60f-e4dcf84703cd	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-31	TransferOut	StockTransfer	01a106ad-f9e3-77ab-a80a-c5fa58f74613	TRF/BDG/2026/VIII/0004	01a106ad-f40d-7a20-b733-56168fcec265	-595.6000	7898.179267	15306.9000	120896640.22	-4704155.57
01a106ad-f9f0-7f0c-a918-b2bbce079d3d	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-31	TransferIn	StockTransfer	01a106ad-f9e3-77ab-a80a-c5fa58f74613	TRF/BDG/2026/VIII/0004	01a106ad-f40d-7a20-b733-56168fcec265	595.6000	7898.179265	595.6000	4704155.57	4704155.57
01a106ad-fb29-7f7a-a847-b555be147b0e	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-08-31	Usage	DailyRecording	01a106ad-fb17-72d4-95e8-2ae27e0b4db8	SKL/CJR/2026/VII/0001 2026-08-31	01a106ad-d332-7f38-9c34-260051b68ef3	-753.5000	7900.000000	3450.7000	27260530.00	-5952650.00
01a106ad-fb41-7c76-af18-7c334f78943f	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-08-31	Usage	DailyRecording	01a106ad-fb31-75b1-ad43-630959b993b1	SKL/CJR/2026/VIII/0001 2026-08-31	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-325.5000	8100.000000	2758.9000	22347090.00	-2636550.00
01a106ad-fb43-7e2e-b39e-e352ea83c8c2	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-08-31	Usage	DailyRecording	01a106ad-fb31-75b1-ad43-630959b993b1	SKL/CJR/2026/VIII/0001 2026-08-31	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-4.0000	42000.000000	5.0000	210000.00	-168000.00
01a106ad-fba2-749c-a8d2-1bf3b14cffa1	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-01	Usage	DailyRecording	01a106ad-fb90-7002-89c5-5a70073c5a9f	SKL/BDG/2026/VIII/0001 2026-09-01	01a106ad-f40d-7a20-b733-56168fcec265	-126.7000	8149.265516	3698.3000	30138428.66	-1032511.94
01a106ad-fba5-76f0-ab54-4ecb63ebe2d5	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-01	Usage	DailyRecording	01a106ad-fb90-7002-89c5-5a70073c5a9f	SKL/BDG/2026/VIII/0001 2026-09-01	01a106ad-f40d-7a20-b733-56168fcec265	-3.0000	42000.000000	13.0000	546000.00	-126000.00
01a106ad-fbe2-74f0-ad17-4f7d8b83d533	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-01	Usage	DailyRecording	01a106ad-fbd1-7df8-be16-f9b104681e11	SKL/CJR/2026/VIII/0001 2026-09-01	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-354.7000	8100.000000	2404.2000	19474020.00	-2873070.00
01a106ad-fc8f-7bd3-af8d-490e3ee4a2e8	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-02	Usage	DailyRecording	01a106ad-fc7f-765a-9179-ca596c6ec270	SKL/BDG/2026/VIII/0001 2026-09-02	01a106ad-f40d-7a20-b733-56168fcec265	-151.5000	8149.265517	3546.8000	28903814.93	-1234613.73
01a106ad-fcf5-7222-8c3f-28e8bbfb2ba5	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-03	Usage	DailyRecording	01a106ad-fce5-7862-abc0-cd4a846194fc	SKL/BDG/2026/VIII/0001 2026-09-03	01a106ad-f40d-7a20-b733-56168fcec265	-176.2000	8149.265515	3370.6000	27467914.35	-1435900.58
01a106ad-fcf7-7a00-984e-1e7455197187	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-03	Usage	DailyRecording	01a106ad-fce5-7862-abc0-cd4a846194fc	SKL/BDG/2026/VIII/0001 2026-09-03	01a106ad-f40d-7a20-b733-56168fcec265	-6.0000	95000.000000	7.0000	665000.00	-570000.00
01a106ad-fcf9-72c2-a5e8-f6b6419d9f99	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-03	Usage	DailyRecording	01a106ad-fce5-7862-abc0-cd4a846194fc	SKL/BDG/2026/VIII/0001 2026-09-03	01a106ad-f40d-7a20-b733-56168fcec265	-3.0000	42000.000000	10.0000	420000.00	-126000.00
01a106ad-fe53-74a7-96b1-33b4727a1ff5	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-04	Usage	DailyRecording	01a106ad-fe42-7bd8-97e1-d6e9352b6ba9	SKL/BDG/2026/VIII/0001 2026-09-04	01a106ad-f40d-7a20-b733-56168fcec265	-200.7000	8149.265517	3169.9000	25832356.76	-1635557.59
01a106ad-fef7-75dd-9c85-4a5b226c3d2a	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-04	TransferOut	StockTransfer	01a106ad-fef5-7741-ac7c-fa61f92a0a1e	TRF/CJR/2026/IX/0002	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-19300.0000	7850.000000	0.0000	0.00	-151505000.00
01a106ad-fef9-7164-a5bf-90afd6144245	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-04	TransferIn	StockTransfer	01a106ad-fef5-7741-ac7c-fa61f92a0a1e	TRF/CJR/2026/IX/0002	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	19300.0000	7850.000000	19300.0000	151505000.00	151505000.00
01a106ad-ff9a-7762-8f68-d6e97f536a24	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-05	Usage	DailyRecording	01a106ad-ff8d-78d2-b1a6-c0063ec87f49	SKL/CJR/2026/VIII/0001 2026-09-05	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-471.3000	8100.000000	693.9000	5620590.00	-3817530.00
01a106ad-fff5-7c0d-82f5-c6f842ac7eb6	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-06	ReturnOut	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	-1064.2000	7900.000000	0.0000	0.00	-8407180.00
01a106ad-fff6-72c9-9d59-8d97df3ca86b	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-06	ReturnIn	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	1064.2000	7900.000000	1064.2000	8407180.00	8407180.00
01a106ad-fff8-7742-ba62-9ae5796eb738	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-06	ReturnOut	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	-210.5000	8150.000000	0.0000	0.00	-1715575.00
01a106ad-fffb-7780-8756-9e987e4fb9c7	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-06	ReturnIn	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	210.5000	8150.000000	210.5000	1715575.00	1715575.00
01a106ad-fffd-72ce-a7d2-4eb5d51b14ab	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-06	ReturnOut	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	-1.0000	95000.000000	0.0000	0.00	-95000.00
01a106ad-ffff-768a-b725-b1333df78ada	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-06	ReturnIn	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	1.0000	95000.000000	1.0000	95000.00	95000.00
01a106ae-0002-732d-9720-36d99379aaf3	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-06	ReturnOut	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	-1.0000	42000.000000	0.0000	0.00	-42000.00
01a106ae-0003-72a1-881c-901d169911ca	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-06	ReturnIn	StockReturn	01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-d332-7f38-9c34-260051b68ef3	1.0000	42000.000000	1.0000	42000.00	42000.00
01a106ae-006d-74cc-b67c-e2c493377630	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-06	Usage	DailyRecording	01a106ae-005f-7097-a4cb-5815027dc7fd	SKL/CJR/2026/VIII/0001 2026-09-06	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-500.3000	8100.000000	193.6000	1568160.00	-4052430.00
01a106ae-0121-7a52-bc07-19056db225f4	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-07	Usage	DailyRecording	01a106ae-010f-7aa1-9509-6d09c5f5b4fc	SKL/BDG/2026/VIII/0001 2026-09-07	01a106ad-f40d-7a20-b733-56168fcec265	-273.2000	8149.265515	2421.9000	19736706.15	-2226379.34
01a106ae-0123-7c26-9225-a93e8c1ca56f	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-07	Usage	DailyRecording	01a106ae-010f-7aa1-9509-6d09c5f5b4fc	SKL/BDG/2026/VIII/0001 2026-09-07	01a106ad-f40d-7a20-b733-56168fcec265	-3.0000	42000.000000	4.0000	168000.00	-126000.00
01a106ae-0180-7b63-b90d-41df9f6416de	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-08	Usage	DailyRecording	01a106ae-0170-7ed9-88d0-573ae9d49cff	SKL/BDG/2026/VIII/0001 2026-09-08	01a106ad-f40d-7a20-b733-56168fcec265	-297.9000	8149.265515	2124.0000	17309039.95	-2427666.20
01a106ae-033c-708f-88fc-0d057b811f16	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-10	Usage	DailyRecording	01a106ae-032c-700a-a492-30a350f591a8	SKL/BDG/2026/VIII/0001 2026-09-10	01a106ad-f40d-7a20-b733-56168fcec265	-347.0000	8149.265512	1454.5000	11853106.69	-2827795.13
01a106ae-03bc-7d65-83f8-d099a4152af8	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-11	Usage	DailyRecording	01a106ae-03ac-79dd-b23e-b3d746fa5b3f	SKL/BDG/2026/VIII/0001 2026-09-11	01a106ad-f40d-7a20-b733-56168fcec265	-371.3000	8149.265514	1083.2000	8827284.40	-3025822.29
01a106ae-03be-7032-855d-08ba24aec344	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-11	Usage	DailyRecording	01a106ae-03ac-79dd-b23e-b3d746fa5b3f	SKL/BDG/2026/VIII/0001 2026-09-11	01a106ad-f40d-7a20-b733-56168fcec265	-6.0000	112000.000000	0.0000	0.00	-672000.00
01a106ae-03df-7d27-bc63-153675dedae6	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-11	Receipt	GoodsReceipt	01a106ae-03dc-71bc-8c80-5622ee97290b	BPB/BDG/2026/IX/0001	\N	5200.0000	8100.000000	5351.6000	43352089.10	42120000.00
01a106ae-03e3-72aa-88cb-fe28ce1726cb	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-11	Receipt	GoodsReceipt	01a106ae-03dc-71bc-8c80-5622ee97290b	BPB/BDG/2026/IX/0001	\N	22050.0000	7850.000000	22656.9000	177885905.00	173092500.00
01a106ae-0420-7c44-a94a-ebbf70527ba4	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-11	Receipt	GoodsReceipt	01a106ae-041e-7b65-83ea-b89df6274edb	BPB/BDG/2026/IX/0002	\N	17.0000	95000.000000	19.0000	1805000.00	1615000.00
01a106ad-ffb0-7e36-ba74-3e918a48b2fa	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-06	Usage	DailyRecording	01a106ad-ffa0-73f8-a95e-0e84bd106901	SKL/BDG/2026/VIII/0001 2026-09-06	01a106ad-f40d-7a20-b733-56168fcec265	-249.4000	8149.265515	2695.1000	21963085.49	-2032426.82
01a106ae-016a-7771-8be0-92c7b982cd16	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-07	Usage	DailyRecording	01a106ae-015a-794c-9313-0df96d4c3d43	SKL/CJR/2026/VIII/0001 2026-09-07	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-529.2000	7850.000000	18770.8000	147350780.00	-4154220.00
01a106ae-0215-755a-8090-a82dd6e878fc	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-08	Usage	DailyRecording	01a106ae-0204-78c2-96bb-80d88307bca4	SKL/CJR/2026/VIII/0001 2026-09-08	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-558.2000	7850.000000	18212.6000	142968910.00	-4381870.00
01a106ae-022b-7c20-bc7d-481f893d860a	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-09	Usage	DailyRecording	01a106ae-021b-73a8-92ee-9e9f2e435605	SKL/BDG/2026/VIII/0001 2026-09-09	01a106ad-f40d-7a20-b733-56168fcec265	-322.5000	8149.265513	1801.5000	14680901.82	-2628138.13
01a106ae-022d-7c5e-b764-f77892181ce1	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-09	Usage	DailyRecording	01a106ae-021b-73a8-92ee-9e9f2e435605	SKL/BDG/2026/VIII/0001 2026-09-09	01a106ad-f40d-7a20-b733-56168fcec265	-3.0000	42000.000000	1.0000	42000.00	-126000.00
01a106ae-02ac-7ad4-a483-38ad47075b6e	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-09	Usage	DailyRecording	01a106ae-029d-76e2-aba8-c69e90634772	SKL/CJR/2026/VIII/0001 2026-09-09	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-587.1000	7850.000000	17625.5000	138360175.00	-4608735.00
01a106ae-0370-7d4c-bfbe-2b41f7551fba	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-10	Usage	DailyRecording	01a106ae-0361-767e-b8dd-182694843147	SKL/CJR/2026/VIII/0001 2026-09-10	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-615.8000	7850.000000	17009.7000	133526145.00	-4834030.00
01a106ae-0372-748f-9e55-c724563e312e	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-10	Usage	DailyRecording	01a106ae-0361-767e-b8dd-182694843147	SKL/CJR/2026/VIII/0001 2026-09-10	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-7.0000	95000.000000	1.0000	95000.00	-665000.00
01a106ae-0388-7eb4-a0ef-f58400a42b31	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-11	TransferOut	StockTransfer	01a106ae-0386-7605-83ed-efc121166d55	TRF/BDG/2026/IX/0002	01a106ad-f40d-7a20-b733-56168fcec265	-14700.0000	7898.179267	606.9000	4793405.00	-116103235.22
01a106ae-038b-79fb-8e9d-d9089d5c9b76	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-11	TransferIn	StockTransfer	01a106ae-0386-7605-83ed-efc121166d55	TRF/BDG/2026/IX/0002	01a106ad-f40d-7a20-b733-56168fcec265	14700.0000	7898.179267	15295.6000	120807390.79	116103235.22
01a106ae-046c-7f82-a4bb-a0c2b10b239a	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-12	Usage	DailyRecording	01a106ae-045e-7bf5-9832-d2d0ed28357a	SKL/BDG/2026/VIII/0001 2026-09-12	01a106ad-f40d-7a20-b733-56168fcec265	-395.7000	8149.265510	687.5000	5602620.04	-3224664.36
01a106ae-0568-75a5-b315-e35d763dee1f	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-13	Usage	DailyRecording	01a106ae-055c-7886-b9b5-51dc49315d90	SKL/BDG/2026/VIII/0001 2026-09-13	01a106ad-f40d-7a20-b733-56168fcec265	-420.2000	8149.265513	267.3000	2178298.67	-3424321.37
01a106ae-0580-74a7-a843-c139abba21ef	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-09-13	Receipt	GoodsReceipt	01a106ae-057e-79b5-8334-150eb98cd9a0	BPB/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	8000.0000	7400.000000	8000.0000	59200000.00	59200000.00
01a106ae-05bb-7ac5-9dc6-04d900aee941	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-09-13	ChickIn	ProductionCycle	01a106ae-023f-704e-8a47-6e438951d196	SKL/BDG/2026/IX/0001	01a106ae-023f-704e-8a47-6e438951d196	-8000.0000	7400.000000	0.0000	0.00	-59200000.00
01a106ae-05ce-7767-abc9-aa584db8af4d	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-13	TransferOut	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	-5200.0000	8100.771564	151.6000	1228076.97	-42124012.13
01a106ae-05d0-7b73-9ec6-999dd51b64c5	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-13	TransferIn	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	5200.0000	8100.771563	5200.0000	42124012.13	42124012.13
01a106ae-05d2-749f-9951-e212dbbe5b5c	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-13	TransferOut	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	-17.0000	95000.000000	2.0000	190000.00	-1615000.00
01a106ae-05d3-78c7-bf4f-4bedd5e15148	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-13	TransferIn	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	17.0000	95000.000000	17.0000	1615000.00	1615000.00
01a106ae-05d5-770a-9b9d-2c27501dc227	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-13	TransferOut	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	-8.0000	112000.000000	0.0000	0.00	-896000.00
01a106ae-05d7-7cb5-8e6a-43309ed39a18	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-13	TransferIn	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	8.0000	112000.000000	8.0000	896000.00	896000.00
01a106ae-05d8-73fd-ab63-ccf9944f5f46	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-13	TransferOut	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	-21.0000	42000.000000	2.0000	84000.00	-882000.00
01a106ae-05da-740f-914b-5d05deae8a41	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-13	TransferIn	StockTransfer	01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ae-023f-704e-8a47-6e438951d196	21.0000	42000.000000	21.0000	882000.00	882000.00
01a106ae-060d-72f6-ab88-c59160a148c0	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-13	Usage	DailyRecording	01a106ae-05fe-721b-9d53-76ef30ada2e8	SKL/CJR/2026/VIII/0001 2026-09-13	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-701.3000	7850.000000	14990.6000	117676210.00	-5505205.00
01a106ae-06ab-77df-be3d-66122bfa26fc	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-14	Usage	DailyRecording	01a106ae-069d-7dd8-ba89-7e3a7e50e393	SKL/BDG/2026/IX/0001 2026-09-14	01a106ae-023f-704e-8a47-6e438951d196	-138.4000	8100.771563	5061.6000	41002865.35	-1121146.78
01a106ae-075b-7d0e-93e5-429f8f18b2b4	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-15	Usage	DailyRecording	01a106ae-074d-7370-91e2-14c452cfc5f0	SKL/BDG/2026/VIII/0001 2026-09-15	01a106ad-f40d-7a20-b733-56168fcec265	-469.0000	7898.179267	14382.0000	113591614.21	-3704246.08
01a106ae-0771-7908-82c7-31741cbd6efd	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-15	Usage	DailyRecording	01a106ae-0762-7099-98f5-0b0394a98fd6	SKL/BDG/2026/IX/0001 2026-09-15	01a106ae-023f-704e-8a47-6e438951d196	-172.3000	8100.771564	4889.3000	39607102.41	-1395762.94
01a106ae-0772-78cc-aa11-5e2ad474f1f0	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-15	Usage	DailyRecording	01a106ae-0762-7099-98f5-0b0394a98fd6	SKL/BDG/2026/IX/0001 2026-09-15	01a106ae-023f-704e-8a47-6e438951d196	-4.0000	42000.000000	17.0000	714000.00	-168000.00
01a106ae-079f-75d0-976a-20d025a4ef0e	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-16	Usage	DailyRecording	01a106ae-078f-72a8-9693-b4fd659b10da	SKL/BDG/2026/VIII/0001 2026-09-16	01a106ad-f40d-7a20-b733-56168fcec265	-493.4000	7898.179266	13888.6000	109694652.56	-3896961.65
01a106ae-080d-7879-905d-e75bca7307c8	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-16	Usage	DailyRecording	01a106ae-07fd-7667-8034-27b86e55460e	SKL/CJR/2026/VIII/0001 2026-09-16	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-786.7000	7850.000000	12715.9000	99819815.00	-6175595.00
01a106ae-0822-798d-a567-eb3968184d91	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-17	Usage	DailyRecording	01a106ae-0813-70d6-919f-2373ea0ba65b	SKL/BDG/2026/VIII/0001 2026-09-17	01a106ad-f40d-7a20-b733-56168fcec265	-517.7000	7898.179266	13370.9000	105605765.15	-4088887.41
01a106ae-0824-787e-a32f-75487fe8b1d1	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-17	Usage	DailyRecording	01a106ae-0813-70d6-919f-2373ea0ba65b	SKL/BDG/2026/VIII/0001 2026-09-17	01a106ad-f40d-7a20-b733-56168fcec265	-6.0000	95000.000000	1.0000	95000.00	-570000.00
01a106ae-0856-731f-9bae-5c899fe4e5a5	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-17	Usage	DailyRecording	01a106ae-0847-7969-8acb-4e9c2655c245	SKL/CJR/2026/VIII/0001 2026-09-17	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-814.7000	7850.000000	11901.2000	93424420.00	-6395395.00
01a106ae-0908-782e-8d30-f5d1679f72f6	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-18	Usage	DailyRecording	01a106ae-08fa-7281-ad5c-b5e0dc176cce	SKL/BDG/2026/IX/0001 2026-09-18	01a106ae-023f-704e-8a47-6e438951d196	-272.4000	8100.771564	4171.6000	33793178.66	-2206650.17
01a106ae-0a6a-711d-b9d1-8324c3b10fcc	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-19	Usage	DailyRecording	01a106ae-0a5d-7ea3-a3f4-e3e3fd08ab4d	SKL/BDG/2026/VIII/0001 2026-09-19	01a106ad-f40d-7a20-b733-56168fcec265	-566.2000	7898.179266	12262.6000	96852213.07	-4471949.10
01a106ae-0a81-7ee2-90f5-9125babc6db3	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-19	Usage	DailyRecording	01a106ae-0a71-7407-a255-d2804cc8322c	SKL/BDG/2026/IX/0001 2026-09-19	01a106ae-023f-704e-8a47-6e438951d196	-305.4000	8100.771565	3866.2000	31319203.02	-2473975.64
01a106ae-0a82-7777-8461-d8105b0e414d	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-19	Usage	DailyRecording	01a106ae-0a71-7407-a255-d2804cc8322c	SKL/BDG/2026/IX/0001 2026-09-19	01a106ae-023f-704e-8a47-6e438951d196	-4.0000	42000.000000	9.0000	378000.00	-168000.00
01a106ae-0aaf-749d-8f9e-d5ce3c3b86ce	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-20	Usage	DailyRecording	01a106ae-0a9f-7ee6-8e45-5e1b57981e9f	SKL/BDG/2026/VIII/0001 2026-09-20	01a106ad-f40d-7a20-b733-56168fcec265	-590.3000	7898.179266	11672.3000	92189917.85	-4662295.22
01a106ae-0adb-72f8-8050-3db028746123	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-20	Usage	DailyRecording	01a106ae-0acb-7dca-8e73-de3068249d61	SKL/CJR/2026/VIII/0001 2026-09-20	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-899.0000	7850.000000	9287.5000	72906875.00	-7057150.00
01a106ae-0af3-79aa-b801-fcec19a66874	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-20	Receipt	GoodsReceipt	01a106ae-0af1-7541-a16c-02d04ad0ab29	BPB/CJR/2026/IX/0001	\N	2300.0000	8150.000000	2510.5000	20460575.00	18745000.00
01a106ae-0af5-7963-a14f-9e027e0f1a77	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-20	Receipt	GoodsReceipt	01a106ae-0af1-7541-a16c-02d04ad0ab29	BPB/CJR/2026/IX/0001	\N	9350.0000	7900.000000	10414.2000	82272180.00	73865000.00
01a106ae-0422-7908-8a1a-553a6943ba3f	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-11	Receipt	GoodsReceipt	01a106ae-041e-7b65-83ea-b89df6274edb	BPB/BDG/2026/IX/0002	\N	8.0000	112000.000000	8.0000	896000.00	896000.00
01a106ae-0425-7518-b248-edb6de92748e	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-11	Receipt	GoodsReceipt	01a106ae-041e-7b65-83ea-b89df6274edb	BPB/BDG/2026/IX/0002	\N	21.0000	42000.000000	23.0000	966000.00	882000.00
01a106ae-0457-7295-8f71-864f8d43f44d	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-11	Usage	DailyRecording	01a106ae-0448-7c6a-b799-54205ec2ae35	SKL/CJR/2026/VIII/0001 2026-09-11	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-644.7000	7850.000000	16365.0000	128465250.00	-5060895.00
01a106ae-0556-7327-972e-be19d9596962	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-12	Usage	DailyRecording	01a106ae-0548-775f-a6f5-56e4863fa3b2	SKL/CJR/2026/VIII/0001 2026-09-12	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-673.1000	7850.000000	15691.9000	123181415.00	-5283835.00
01a106ae-0696-7ab0-bb13-43e09e394690	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-14	Usage	DailyRecording	01a106ae-0686-776f-aa77-3f6ec812b6da	SKL/BDG/2026/VIII/0001 2026-09-14	01a106ad-f40d-7a20-b733-56168fcec265	-444.6000	7898.179267	14851.0000	117295860.29	-3511530.50
01a106ae-06cc-7e69-8d94-94cbf2fd9d03	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-14	Usage	DailyRecording	01a106ae-06b1-7ae2-bb62-82bb05d20a3f	SKL/CJR/2026/VIII/0001 2026-09-14	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-730.0000	7850.000000	14260.6000	111945710.00	-5730500.00
01a106ae-0788-721a-a9b7-e1fa718310e1	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-15	Usage	DailyRecording	01a106ae-0779-7971-9c4d-87a2fdd6ad0c	SKL/CJR/2026/VIII/0001 2026-09-15	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-758.0000	7850.000000	13502.6000	105995410.00	-5950300.00
01a106ae-07f7-7f25-9690-197764b5df14	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-16	Usage	DailyRecording	01a106ae-07e8-70aa-8483-dde7e16f845e	SKL/BDG/2026/IX/0001 2026-09-16	01a106ae-023f-704e-8a47-6e438951d196	-205.9000	8100.771564	4683.4000	37939153.54	-1667948.87
01a106ae-083b-7afd-81f2-fde32a3a7b6c	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-17	Usage	DailyRecording	01a106ae-082a-70a0-bfb4-ff75d29c680d	SKL/BDG/2026/IX/0001 2026-09-17	01a106ae-023f-704e-8a47-6e438951d196	-239.4000	8100.771563	4444.0000	35999828.83	-1939324.71
01a106ae-083d-71a0-abe5-2344f704989e	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-17	Usage	DailyRecording	01a106ae-082a-70a0-bfb4-ff75d29c680d	SKL/BDG/2026/IX/0001 2026-09-17	01a106ae-023f-704e-8a47-6e438951d196	-8.0000	95000.000000	9.0000	855000.00	-760000.00
01a106ae-083f-79b6-ad60-5fa47cd8cfaf	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-17	Usage	DailyRecording	01a106ae-082a-70a0-bfb4-ff75d29c680d	SKL/BDG/2026/IX/0001 2026-09-17	01a106ae-023f-704e-8a47-6e438951d196	-4.0000	42000.000000	13.0000	546000.00	-168000.00
01a106ae-086b-7a8a-82f4-f1b07bb94941	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-18	Usage	DailyRecording	01a106ae-085c-712f-a271-039f95ca3b88	SKL/BDG/2026/VIII/0001 2026-09-18	01a106ad-f40d-7a20-b733-56168fcec265	-542.1000	7898.179266	12828.8000	101324162.17	-4281602.98
01a106ae-091d-773d-84b4-0b34df9d7831	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-18	Usage	DailyRecording	01a106ae-090f-76fd-a291-b6b532ffc767	SKL/CJR/2026/VIII/0001 2026-09-18	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-843.1000	7850.000000	11058.1000	86806085.00	-6618335.00
01a106ae-0a99-71e3-8a75-99d2294f759e	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-19	Usage	DailyRecording	01a106ae-0a8a-7c76-a7f2-005fe396f345	SKL/CJR/2026/VIII/0001 2026-09-19	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-871.6000	7850.000000	10186.5000	79964025.00	-6842060.00
01a106ae-0ac4-7a6e-9ba7-2cda9fe8df60	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-20	Usage	DailyRecording	01a106ae-0ab4-7a29-8119-c4f4268fd80f	SKL/BDG/2026/IX/0001 2026-09-20	01a106ae-023f-704e-8a47-6e438951d196	-338.7000	8100.771564	3527.5000	28575471.69	-2743731.33
01a106ae-0bfc-7d2d-af01-8fec940587ee	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-21	Usage	DailyRecording	01a106ae-0bed-7628-9f59-9dbe1f312708	SKL/CJR/2026/VIII/0001 2026-09-21	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-926.9000	7850.000000	8360.6000	65630710.00	-7276165.00
01a106ae-0c23-7354-89fd-120caa7ddbd7	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-22	Usage	DailyRecording	01a106ae-0c16-7f39-9ed0-06a7f0e90700	SKL/BDG/2026/IX/0001 2026-09-22	01a106ae-023f-704e-8a47-6e438951d196	-404.6000	8100.771563	2751.8000	22291703.19	-3277572.17
01a106ae-0d33-70f3-a201-e25cdd3aa9d2	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-23	Usage	DailyRecording	01a106ae-0d23-7c75-823e-6741f4848f1a	SKL/CJR/2026/VIII/0001 2026-09-23	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-983.1000	7850.000000	6422.5000	50416625.00	-7717335.00
01a106ae-0d5f-73a6-9fa3-156365ea6953	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-24	Usage	DailyRecording	01a106ae-0d4f-7f7c-be06-6a1a4ce3d083	SKL/BDG/2026/VIII/0001 2026-09-24	01a106ad-f40d-7a20-b733-56168fcec265	-684.7000	7898.179267	9075.0000	71675976.85	-5407883.34
01a106ae-0d8b-757f-b3f8-49dd158b7875	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-24	Usage	DailyRecording	01a106ae-0d7d-7469-bc0e-c08e85d36fbb	SKL/CJR/2026/VIII/0001 2026-09-24	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1011.1000	7850.000000	5411.4000	42479490.00	-7937135.00
01a106ae-0da3-732b-a279-4d4c6ff28d6a	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-24	Usage	DailyRecording	01a106ae-0d91-789b-aad9-9048c5bf8abb	SKL/CJR/2026/IX/0001 2026-09-24	01a106ae-0935-71d7-8a79-528d2a90b371	-75.3000	8150.000000	2164.1000	17637415.00	-613695.00
01a106ae-0da6-7402-9890-6e5d1903f99d	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-24	Usage	DailyRecording	01a106ae-0d91-789b-aad9-9048c5bf8abb	SKL/CJR/2026/IX/0001 2026-09-24	01a106ae-0935-71d7-8a79-528d2a90b371	-2.0000	42000.000000	9.0000	378000.00	-84000.00
01a106ae-0e78-7044-97f4-d9eb2bdd879a	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-25	Usage	DailyRecording	01a106ae-0e68-70ab-bacf-a1ba945c4e10	SKL/BDG/2026/IX/0001 2026-09-25	01a106ae-023f-704e-8a47-6e438951d196	-504.3000	8100.771567	1338.6000	10843692.82	-4085219.10
01a106ae-0e79-7519-9335-42ce2dacb044	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-25	Usage	DailyRecording	01a106ae-0e68-70ab-bacf-a1ba945c4e10	SKL/BDG/2026/IX/0001 2026-09-25	01a106ae-023f-704e-8a47-6e438951d196	-8.0000	112000.000000	0.0000	0.00	-896000.00
01a106ae-0edc-77c2-964b-7c443e40a4d1	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-25	Usage	DailyRecording	01a106ae-0ece-76de-b34b-aa355877e294	SKL/CJR/2026/VIII/0001 2026-09-25	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1039.1000	7850.000000	4372.3000	34322555.00	-8156935.00
01a106ae-0f48-77c9-bfde-337efc99f8ac	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-26	Usage	DailyRecording	01a106ae-0f37-718b-9dbf-bd3c51be9432	SKL/BDG/2026/VIII/0001 2026-09-26	01a106ad-f40d-7a20-b733-56168fcec265	-731.9000	7898.179267	7635.0000	60302598.70	-5780677.41
01a106ae-0f73-7b15-8495-8e6b130c87e7	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-26	Usage	DailyRecording	01a106ae-0f64-7244-9653-6c258be05f86	SKL/CJR/2026/VIII/0001 2026-09-26	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1067.0000	7850.000000	3305.3000	25946605.00	-8375950.00
01a106ae-0fa4-7868-be89-9e1cd64657c1	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-27	Usage	DailyRecording	01a106ae-0f94-7973-b72a-a1ee14ccfa40	SKL/BDG/2026/VIII/0001 2026-09-27	01a106ad-f40d-7a20-b733-56168fcec265	-755.4000	7898.179267	6879.6000	54336314.08	-5966284.62
01a106ae-0fd8-7bfa-a4a5-a6202c720f09	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-27	Usage	DailyRecording	01a106ae-0fc3-7696-8265-5a185d22ba80	SKL/CJR/2026/VIII/0001 2026-09-27	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1094.0000	7850.000000	2211.3000	17358705.00	-8587900.00
01a106ae-1123-7b08-8a1e-daec9a30a967	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-28	Usage	DailyRecording	01a106ae-1114-78be-b8d0-6cca6cbc5c44	SKL/BDG/2026/VIII/0001 2026-09-28	01a106ad-f40d-7a20-b733-56168fcec265	-779.2000	7898.179266	6100.4000	48182052.80	-6154261.28
01a106ae-114b-7c8a-9c3a-3d562d07e4f8	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-28	Usage	DailyRecording	01a106ae-113d-78a3-aa8a-7d1fed49212a	SKL/CJR/2026/VIII/0001 2026-09-28	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-747.4000	7850.000000	1463.9000	11491615.00	-5867090.00
01a106ae-1234-7ed1-b635-98cf1a10f38f	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-28	Usage	DailyRecording	01a106ae-1228-76cc-b1a8-febe704cf404	SKL/CJR/2026/IX/0001 2026-09-28	01a106ae-0935-71d7-8a79-528d2a90b371	-133.5000	8150.000000	1716.8000	13991920.00	-1088025.00
01a106ae-1236-7689-859c-d740c3a5ca97	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-28	Usage	DailyRecording	01a106ae-1228-76cc-b1a8-febe704cf404	SKL/CJR/2026/IX/0001 2026-09-28	01a106ae-0935-71d7-8a79-528d2a90b371	-2.0000	42000.000000	5.0000	210000.00	-84000.00
01a106ae-125e-7e01-a4e4-d34e2175cae1	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-29	Usage	DailyRecording	01a106ae-1250-7e96-8716-ed7c86794e39	SKL/BDG/2026/IX/0001 2026-09-29	01a106ae-023f-704e-8a47-6e438951d196	-637.1000	7851.290556	20808.8000	163375934.93	-5002057.21
01a106ae-139d-7eae-a655-479fd2016a2f	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-30	Usage	DailyRecording	01a106ae-138d-729d-b5c9-b2e50c465273	SKL/BDG/2026/VIII/0001 2026-09-30	01a106ad-f40d-7a20-b733-56168fcec265	-826.4000	7898.179267	4471.0000	35312759.50	-6527055.35
01a106ae-1472-782a-a790-309b8b573404	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-01	Usage	DailyRecording	01a106ae-1462-76d1-a222-4ed44499544e	SKL/BDG/2026/VIII/0001 2026-10-01	01a106ad-f40d-7a20-b733-56168fcec265	-849.7000	7898.179266	3621.3000	28601676.58	-6711082.92
01a106ae-1489-7ba1-a6d2-d99499687608	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-01	Usage	DailyRecording	01a106ae-1479-7cc5-90f2-0608efd0ff02	SKL/BDG/2026/IX/0001 2026-10-01	01a106ae-023f-704e-8a47-6e438951d196	-703.0000	7851.290556	19435.5000	152593757.61	-5519457.26
01a106ae-148b-7c47-af8d-cb436f1ec960	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd9f-7179-9506-e6071b528b74	2026-10-01	Usage	DailyRecording	01a106ae-1479-7cc5-90f2-0608efd0ff02	SKL/BDG/2026/IX/0001 2026-10-01	01a106ae-023f-704e-8a47-6e438951d196	-8.0000	95000.000000	1.0000	95000.00	-760000.00
01a106ae-155e-741e-bed0-8499046ffcdb	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-02	Usage	DailyRecording	01a106ae-154e-7794-b56c-34eee5209750	SKL/BDG/2026/VIII/0001 2026-10-02	01a106ad-f40d-7a20-b733-56168fcec265	-873.0000	7898.179267	2748.3000	21706566.08	-6895110.50
01a106ae-0b28-7cfc-aad3-62d5cfd8f953	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-20	Receipt	GoodsReceipt	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	BPB/CJR/2026/IX/0002	\N	9.0000	95000.000000	10.0000	950000.00	855000.00
01a106ae-0b29-7c91-abf7-b273a9a23a2f	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-20	Receipt	GoodsReceipt	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	BPB/CJR/2026/IX/0002	\N	4.0000	112000.000000	4.0000	448000.00	448000.00
01a106ae-0b2b-7b0e-b94d-cc2dd7a0a8e7	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-20	Receipt	GoodsReceipt	01a106ae-0b26-7b4c-aa76-9672c7ce56d4	BPB/CJR/2026/IX/0002	\N	11.0000	42000.000000	12.0000	504000.00	462000.00
01a106ae-0bd1-74b4-b69c-641d72a04b9e	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-21	Usage	DailyRecording	01a106ae-0bc4-733f-98ed-a70ec3f5661c	SKL/BDG/2026/VIII/0001 2026-09-21	01a106ad-f40d-7a20-b733-56168fcec265	-613.9000	7898.179266	11058.4000	87341225.60	-4848692.25
01a106ae-0be5-7839-a7e7-efef099ef44f	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-21	Usage	DailyRecording	01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f	SKL/BDG/2026/IX/0001 2026-09-21	01a106ae-023f-704e-8a47-6e438951d196	-371.1000	8100.771563	3156.4000	25569275.36	-3006196.33
01a106ae-0be7-7ed4-95ee-fcfc751161ba	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-21	Usage	DailyRecording	01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f	SKL/BDG/2026/IX/0001 2026-09-21	01a106ae-023f-704e-8a47-6e438951d196	-4.0000	42000.000000	5.0000	210000.00	-168000.00
01a106ae-0c0e-756a-a3b0-f0ac456383ed	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-22	Usage	DailyRecording	01a106ae-0c01-7d34-92e3-55ed42fc7d93	SKL/BDG/2026/VIII/0001 2026-09-22	01a106ad-f40d-7a20-b733-56168fcec265	-637.6000	7898.179266	10420.8000	82305346.50	-5035879.10
01a106ae-0c36-7b13-af82-69de00abc32f	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-22	Usage	DailyRecording	01a106ae-0c29-7f35-8143-a6df282f87cd	SKL/CJR/2026/VIII/0001 2026-09-22	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-955.0000	7850.000000	7405.6000	58133960.00	-7496750.00
01a106ae-0c50-7e0d-a705-2129b3370ed4	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-09-22	Receipt	GoodsReceipt	01a106ae-0c4e-7273-b6e0-82abb0d929f9	BPB/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	3500.0000	7400.000000	3500.0000	25900000.00	25900000.00
01a106ae-0c8e-79b7-b628-3205622128c9	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd1e-7c26-a889-94b43fae1c60	2026-09-22	ChickIn	ProductionCycle	01a106ae-0935-71d7-8a79-528d2a90b371	SKL/CJR/2026/IX/0001	01a106ae-0935-71d7-8a79-528d2a90b371	-3500.0000	7400.000000	0.0000	0.00	-25900000.00
01a106ae-0ca3-73ea-9cf1-6fd02bafd741	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-22	TransferOut	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	-2300.0000	8150.000000	210.5000	1715575.00	-18745000.00
01a106ae-0ca4-7a48-8404-255eb183cb03	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-22	TransferIn	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	2300.0000	8150.000000	2300.0000	18745000.00	18745000.00
01a106ae-0ca6-7621-b124-dc055ec230f7	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-22	TransferOut	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	-9.0000	95000.000000	1.0000	95000.00	-855000.00
01a106ae-0ca7-7ba3-83fb-11262e4d9900	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-22	TransferIn	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	9.0000	95000.000000	9.0000	855000.00	855000.00
01a106ae-0ca8-7cef-8a20-a57fc849c3e1	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-22	TransferOut	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	-4.0000	112000.000000	0.0000	0.00	-448000.00
01a106ae-0caa-7b7a-84ec-0053e19aef63	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdab-762c-ad3a-cd0613d5d1df	2026-09-22	TransferIn	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	4.0000	112000.000000	4.0000	448000.00	448000.00
01a106ae-0cac-7c8c-8aee-35dfccb3ae92	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-22	TransferOut	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	-11.0000	42000.000000	1.0000	42000.00	-462000.00
01a106ae-0cae-7746-903e-6d8dd58f4c96	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-22	TransferIn	StockTransfer	01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ae-0935-71d7-8a79-528d2a90b371	11.0000	42000.000000	11.0000	462000.00	462000.00
01a106ae-0cff-7471-b550-92ef1133b80a	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-23	Usage	DailyRecording	01a106ae-0ce6-7ed3-815d-d78acb8beb5a	SKL/BDG/2026/VIII/0001 2026-09-23	01a106ad-f40d-7a20-b733-56168fcec265	-661.1000	7898.179266	9759.7000	77083860.19	-5221486.31
01a106ae-0d1a-715f-920f-afcdf5ba7266	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-23	Usage	DailyRecording	01a106ae-0d08-7312-8292-d25b316d3500	SKL/BDG/2026/IX/0001 2026-09-23	01a106ae-023f-704e-8a47-6e438951d196	-437.8000	8100.771564	2314.0000	18745185.40	-3546517.79
01a106ae-0d1c-7be8-88b3-507b2572571f	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-23	Usage	DailyRecording	01a106ae-0d08-7312-8292-d25b316d3500	SKL/BDG/2026/IX/0001 2026-09-23	01a106ae-023f-704e-8a47-6e438951d196	-4.0000	42000.000000	1.0000	42000.00	-168000.00
01a106ae-0d4a-726f-9801-c31010770999	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-23	Usage	DailyRecording	01a106ae-0d39-7f71-84bf-4f1de24ab449	SKL/CJR/2026/IX/0001 2026-09-23	01a106ae-0935-71d7-8a79-528d2a90b371	-60.6000	8150.000000	2239.4000	18251110.00	-493890.00
01a106ae-0d77-7b3d-a6d8-8808d15ba924	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-24	Usage	DailyRecording	01a106ae-0d66-7662-8183-b4e125081c37	SKL/BDG/2026/IX/0001 2026-09-24	01a106ae-023f-704e-8a47-6e438951d196	-471.1000	8100.771564	1842.9000	14928911.92	-3816273.48
01a106ae-0e30-766b-916c-cdcb6b2b9d7e	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-25	Usage	DailyRecording	01a106ae-0e21-7392-ac64-84e99d638855	SKL/BDG/2026/VIII/0001 2026-09-25	01a106ad-f40d-7a20-b733-56168fcec265	-708.1000	7898.179267	8366.9000	66083276.11	-5592700.74
01a106ae-0e47-7b24-8a61-8869c293c3ec	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-25	TransferOut	StockTransfer	01a106ae-0e45-7ed6-a7fc-6ba1fe729f41	TRF/BDG/2026/IX/0004	01a106ae-023f-704e-8a47-6e438951d196	-22050.0000	7851.290556	606.9000	4764948.24	-173120956.76
01a106ae-0e48-78b2-940a-4190cd927ffe	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-25	TransferIn	StockTransfer	01a106ae-0e45-7ed6-a7fc-6ba1fe729f41	TRF/BDG/2026/IX/0004	01a106ae-023f-704e-8a47-6e438951d196	22050.0000	7851.290556	22050.0000	173120956.76	173120956.76
01a106ae-0f30-746d-811e-4e965d3ac0c2	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-25	Usage	DailyRecording	01a106ae-0f1f-703b-9d0a-2464808cf5d8	SKL/CJR/2026/IX/0001 2026-09-25	01a106ae-0935-71d7-8a79-528d2a90b371	-90.0000	8150.000000	2074.1000	16903915.00	-733500.00
01a106ae-0f5e-7e4e-86cc-b22334b66737	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-26	Usage	DailyRecording	01a106ae-0f4e-7a58-9227-731f2a961ad2	SKL/BDG/2026/IX/0001 2026-09-26	01a106ae-023f-704e-8a47-6e438951d196	-537.6000	8100.771567	801.0000	6488718.03	-4354974.79
01a106ae-0f8a-75b7-b4d9-c140677f7cc7	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-26	Usage	DailyRecording	01a106ae-0f7b-7665-8204-60f3af0c5e15	SKL/CJR/2026/IX/0001 2026-09-26	01a106ae-0935-71d7-8a79-528d2a90b371	-104.6000	8150.000000	1969.5000	16051425.00	-852490.00
01a106ae-0f8c-72d3-bb41-e247b0a77ce3	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd9f-7179-9506-e6071b528b74	2026-09-26	Usage	DailyRecording	01a106ae-0f7b-7665-8204-60f3af0c5e15	SKL/CJR/2026/IX/0001 2026-09-26	01a106ae-0935-71d7-8a79-528d2a90b371	-4.0000	95000.000000	5.0000	475000.00	-380000.00
01a106ae-0f8d-7b55-8493-6c203561e8c9	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-26	Usage	DailyRecording	01a106ae-0f7b-7665-8204-60f3af0c5e15	SKL/CJR/2026/IX/0001 2026-09-26	01a106ae-0935-71d7-8a79-528d2a90b371	-2.0000	42000.000000	7.0000	294000.00	-84000.00
01a106ae-0fb9-7552-9e32-2da24101fe10	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-27	Usage	DailyRecording	01a106ae-0fab-7019-82ca-0e75f9cb1722	SKL/BDG/2026/IX/0001 2026-09-27	01a106ae-023f-704e-8a47-6e438951d196	-570.9000	8100.771573	230.1000	1863987.54	-4624730.49
01a106ae-109a-71e7-98c4-21395b38599a	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-27	Usage	DailyRecording	01a106ae-108c-76d1-be62-2df1049ed550	SKL/CJR/2026/IX/0001 2026-09-27	01a106ae-0935-71d7-8a79-528d2a90b371	-119.2000	8150.000000	1850.3000	15079945.00	-971480.00
01a106ae-1137-721e-92b0-901b27a9f309	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-28	Usage	DailyRecording	01a106ae-1129-7ebc-9afa-8d83e87d51ff	SKL/BDG/2026/IX/0001 2026-09-28	01a106ae-023f-704e-8a47-6e438951d196	-604.1000	7851.290556	21445.9000	168377992.14	-4742964.62
01a106ae-124a-70cf-997f-82c247c424c7	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-29	Usage	DailyRecording	01a106ae-123c-772e-bc9c-5f2206abc9a7	SKL/BDG/2026/VIII/0001 2026-09-29	01a106ad-f40d-7a20-b733-56168fcec265	-803.0000	7898.179267	5297.4000	41839814.85	-6342237.95
01a106ae-1271-7037-b866-ccd2ea35c92f	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-29	Usage	DailyRecording	01a106ae-1263-7f2f-bbea-b566851d80e8	SKL/CJR/2026/VIII/0001 2026-09-29	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-382.9000	7850.000000	1081.0000	8485850.00	-3005765.00
01a106ae-130f-7bfb-a0e1-c3a38dc26b52	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-29	Usage	DailyRecording	01a106ae-12ff-74c7-a6c7-edbba1405eb5	SKL/CJR/2026/IX/0001 2026-09-29	01a106ae-0935-71d7-8a79-528d2a90b371	-148.0000	8150.000000	1568.8000	12785720.00	-1206200.00
01a106ae-13b4-760d-b929-a1ab3bb5b8e2	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-09-30	Usage	DailyRecording	01a106ae-13a5-7ae1-8065-056bbe4af2e3	SKL/BDG/2026/IX/0001 2026-09-30	01a106ae-023f-704e-8a47-6e438951d196	-670.3000	7851.290556	20138.5000	158113214.87	-5262720.06
01a106ae-1412-7cef-abb0-0c9604ba448d	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-09-30	Usage	DailyRecording	01a106ae-1401-7239-9838-ed8e5556b525	SKL/CJR/2026/IX/0001 2026-09-30	01a106ae-0935-71d7-8a79-528d2a90b371	-162.1000	8150.000000	1406.7000	11464605.00	-1321115.00
01a106ae-1414-7571-959c-957a0d22e75e	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-09-30	Usage	DailyRecording	01a106ae-1401-7239-9838-ed8e5556b525	SKL/CJR/2026/IX/0001 2026-09-30	01a106ae-0935-71d7-8a79-528d2a90b371	-2.0000	42000.000000	3.0000	126000.00	-84000.00
01a106ae-14d2-716c-aecb-884b7d929c51	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-10-01	ReturnOut	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-193.6000	8100.000000	0.0000	0.00	-1568160.00
01a106ae-14d3-72d4-b917-e5d378176d8b	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-10-01	ReturnIn	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	193.6000	8100.000000	404.1000	3283735.00	1568160.00
01a106ae-14d5-77b4-b6f2-aa0ab88590af	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd9f-7179-9506-e6071b528b74	2026-10-01	ReturnOut	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1.0000	95000.000000	0.0000	0.00	-95000.00
01a106ae-14d6-74a6-ad4a-933be5150cda	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd9f-7179-9506-e6071b528b74	2026-10-01	ReturnIn	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	1.0000	95000.000000	2.0000	190000.00	95000.00
01a106ae-14d8-7e8b-81af-840b4693cd64	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-10-01	ReturnOut	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1.0000	42000.000000	0.0000	0.00	-42000.00
01a106ae-14db-7cf3-b309-3f1ed58d176f	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-10-01	ReturnIn	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	1.0000	42000.000000	2.0000	84000.00	42000.00
01a106ae-14dd-726b-b63c-4d7efc7d0585	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-01	ReturnOut	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	-1081.0000	7850.000000	0.0000	0.00	-8485850.00
01a106ae-14df-7043-acf6-467f67d880b7	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-01	ReturnIn	StockReturn	01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	1081.0000	7850.000000	11495.2000	90758030.00	8485850.00
01a106ae-1546-7ae0-bed7-2897fe49d64f	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-10-01	Usage	DailyRecording	01a106ae-1537-7acb-8120-b3e4a1c141b7	SKL/CJR/2026/IX/0001 2026-10-01	01a106ae-0935-71d7-8a79-528d2a90b371	-176.6000	8150.000000	1230.1000	10025315.00	-1439290.00
01a106ae-1591-7cf4-a8c1-067c1bea859e	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-02	Usage	DailyRecording	01a106ae-1582-7a1f-8b9d-61fc64e31cc9	SKL/BDG/2026/IX/0001 2026-10-02	01a106ae-023f-704e-8a47-6e438951d196	-736.1000	7851.290556	18699.4000	146814422.63	-5779334.98
01a106ae-1601-7f79-a18f-ace69169e791	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-10-02	Usage	DailyRecording	01a106ae-15ef-767d-bad0-a41642b167b5	SKL/CJR/2026/IX/0001 2026-10-02	01a106ae-0935-71d7-8a79-528d2a90b371	-191.1000	8150.000000	1039.0000	8467850.00	-1557465.00
01a106ae-1604-7d54-ab5a-600279b74356	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bdb8-7c78-af20-a4a6f3355778	2026-10-02	Usage	DailyRecording	01a106ae-15ef-767d-bad0-a41642b167b5	SKL/CJR/2026/IX/0001 2026-10-02	01a106ae-0935-71d7-8a79-528d2a90b371	-2.0000	42000.000000	1.0000	42000.00	-84000.00
01a106ae-1821-721a-a2d6-7b317c74ba37	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ad-bd76-76e3-9971-2ce1aae02eb4	2026-10-03	Usage	DailyRecording	01a106ae-1813-771c-984d-2678c0084126	SKL/CJR/2026/IX/0001 2026-10-03	01a106ae-0935-71d7-8a79-528d2a90b371	-205.7000	8150.000000	833.3000	6791395.00	-1676455.00
01a106ae-161b-7111-8aca-07f8d8513b86	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-03	Usage	DailyRecording	01a106ae-160b-7082-b0ed-65317a471bed	SKL/BDG/2026/VIII/0001 2026-10-03	01a106ad-f40d-7a20-b733-56168fcec265	-672.6000	7898.179267	2075.7000	16394250.71	-5312315.37
01a106ae-1783-7bc8-8bca-9ca6d53c2a28	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	2026-10-03	Usage	DailyRecording	01a106ae-1772-7905-8e52-59d851299e1f	SKL/BDG/2026/IX/0001 2026-10-03	01a106ae-023f-704e-8a47-6e438951d196	-769.0000	7851.290556	17930.4000	140776780.19	-6037642.44
\.


--
-- Data for Name: stock_return_lines; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_return_lines (stock_return_id, line_number, item_id, uom_id, quantity, base_quantity, unit_cost, value) FROM stdin;
01a106ad-ede7-7914-bc12-88b477649acb	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	151.6000	151.6000	8150.000000	1235540.00
01a106ad-ede7-7914-bc12-88b477649acb	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	1.0000	1.0000	95000.000000	95000.00
01a106ad-ede7-7914-bc12-88b477649acb	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	1.0000	1.0000	42000.000000	42000.00
01a106ad-ede7-7914-bc12-88b477649acb	4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	606.9000	606.9000	7875.921651	4779896.85
01a106ad-f9df-72b6-8bd3-82399696b6f7	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	126.7000	126.7000	8100.000000	1026270.00
01a106ad-f9df-72b6-8bd3-82399696b6f7	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	595.6000	595.6000	7875.921659	4690898.94
01a106ad-fa43-7c1d-9eb8-33609a30db39	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	1.0000	1.0000	95000.000000	95000.00
01a106ad-fa43-7c1d-9eb8-33609a30db39	2	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	1.0000	1.0000	42000.000000	42000.00
01a106ad-fff3-7186-9838-80f0249d67e4	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1064.2000	1064.2000	7900.000000	8407180.00
01a106ad-fff3-7186-9838-80f0249d67e4	2	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	210.5000	210.5000	8150.000000	1715575.00
01a106ad-fff3-7186-9838-80f0249d67e4	3	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	1.0000	1.0000	95000.000000	95000.00
01a106ad-fff3-7186-9838-80f0249d67e4	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	1.0000	1.0000	42000.000000	42000.00
01a106ae-14d0-79b6-b4e1-bfafb1945a08	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	193.6000	193.6000	8100.000000	1568160.00
01a106ae-14d0-79b6-b4e1-bfafb1945a08	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	1.0000	1.0000	95000.000000	95000.00
01a106ae-14d0-79b6-b4e1-bfafb1945a08	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	1.0000	1.0000	42000.000000	42000.00
01a106ae-14d0-79b6-b4e1-bfafb1945a08	4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1081.0000	1081.0000	7850.000000	8485850.00
\.


--
-- Data for Name: stock_returns; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_returns (id, number, branch_id, from_warehouse_id, to_warehouse_id, cycle_id, return_date, reason, notes, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-ede7-7914-bc12-88b477649acb	RTR/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-08-22	Sisa sapronak akhir siklus	\N	2026-10-04 18:30:24.415088+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f9df-72b6-8bd3-82399696b6f7	RTR/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-08-31	Sisa pakan dipindah ke kandang yang masih berjalan	Mutasi pakan: Sisa pakan dipindah ke kandang yang masih berjalan	2026-10-04 18:30:27.440684+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fa43-7c1d-9eb8-33609a30db39	RTR/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-08-31	Sisa sapronak akhir siklus	\N	2026-10-04 18:30:27.531038+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fff3-7186-9838-80f0249d67e4	RTR/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-d332-7f38-9c34-260051b68ef3	2026-09-06	Sisa sapronak akhir siklus	\N	2026-10-04 18:30:28.995973+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-14d0-79b6-b4e1-bfafb1945a08	RTR/CJR/2026/X/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-10-01	Sisa sapronak akhir siklus	\N	2026-10-04 18:30:34.335703+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: stock_transfer_lines; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_transfer_lines (stock_transfer_id, line_number, item_id, uom_id, quantity, base_quantity, unit_cost, value) FROM stdin;
01a106ad-cbd7-752a-838d-978a3b279daf	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	61.0000	3050.0000	8150.000000	24857500.00
01a106ad-cbd7-752a-838d-978a3b279daf	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00
01a106ad-cbd7-752a-838d-978a3b279daf	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00
01a106ad-cbd7-752a-838d-978a3b279daf	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00
01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	60.0000	3000.0000	8100.000000	24300000.00
01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00
01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00
01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00
01a106ad-d4e9-76d3-825a-71bb220f2d77	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	225.0000	11250.0000	7875.921659	88604118.66
01a106ad-d731-7973-9045-abaa69ca12a9	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	70.0000	3500.0000	8150.000000	28525000.00
01a106ad-d731-7973-9045-abaa69ca12a9	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	11.0000	95000.000000	1045000.00
01a106ad-d731-7973-9045-abaa69ca12a9	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	112000.000000	560000.00
01a106ad-d731-7973-9045-abaa69ca12a9	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00
01a106ad-de6e-7454-87f3-422062f82097	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	209.0000	10450.0000	7875.921659	82303381.34
01a106ad-e12c-7fed-8626-ef11d1a1aa64	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	277.0000	13850.0000	7900.000000	109415000.00
01a106ad-f00d-7930-be85-9e425747d0fa	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	91.0000	4550.0000	8100.000000	36855000.00
01a106ad-f00d-7930-be85-9e425747d0fa	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	15.0000	15.0000	95000.000000	1425000.00
01a106ad-f00d-7930-be85-9e425747d0fa	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	7.0000	112000.000000	784000.00
01a106ad-f00d-7930-be85-9e425747d0fa	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	21.0000	42000.000000	882000.00
01a106ad-f84d-7a1b-8cac-41c2469803d4	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	76.0000	3800.0000	8150.000000	30970000.00
01a106ad-f84d-7a1b-8cac-41c2469803d4	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	13.0000	13.0000	95000.000000	1235000.00
01a106ad-f84d-7a1b-8cac-41c2469803d4	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	6.0000	112000.000000	672000.00
01a106ad-f84d-7a1b-8cac-41c2469803d4	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	16.0000	42000.000000	672000.00
01a106ad-f9e3-77ab-a80a-c5fa58f74613	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	126.7000	126.7000	8127.236780	1029720.90
01a106ad-f9e3-77ab-a80a-c5fa58f74613	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	595.6000	595.6000	7898.179265	4704155.57
01a106ad-fef5-7741-ac7c-fa61f92a0a1e	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	386.0000	19300.0000	7850.000000	151505000.00
01a106ae-0386-7605-83ed-efc121166d55	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	294.0000	14700.0000	7898.179267	116103235.22
01a106ae-05cc-7acb-b7a1-4ff93a12d78c	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	104.0000	5200.0000	8100.771563	42124012.13
01a106ae-05cc-7acb-b7a1-4ff93a12d78c	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	17.0000	17.0000	95000.000000	1615000.00
01a106ae-05cc-7acb-b7a1-4ff93a12d78c	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	8.0000	112000.000000	896000.00
01a106ae-05cc-7acb-b7a1-4ff93a12d78c	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	21.0000	42000.000000	882000.00
01a106ae-0ca1-70ed-af31-ee7d25077d50	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	46.0000	2300.0000	8150.000000	18745000.00
01a106ae-0ca1-70ed-af31-ee7d25077d50	2	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	9.0000	9.0000	95000.000000	855000.00
01a106ae-0ca1-70ed-af31-ee7d25077d50	3	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	4.0000	112000.000000	448000.00
01a106ae-0ca1-70ed-af31-ee7d25077d50	4	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	11.0000	11.0000	42000.000000	462000.00
01a106ae-0e45-7ed6-a7fc-6ba1fe729f41	1	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	441.0000	22050.0000	7851.290556	173120956.76
\.


--
-- Data for Name: stock_transfers; Type: TABLE DATA; Schema: inventory; Owner: postgres
--

COPY inventory.stock_transfers (id, number, branch_id, from_warehouse_id, to_warehouse_id, cycle_id, transfer_date, notes, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-cbd7-752a-838d-978a3b279daf	TRF/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-07-16	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:15.685672+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d2c3-71d7-a28b-b7b7599ed6fc	TRF/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-07-26	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:17.440074+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d4e9-76d3-825a-71bb220f2d77	TRF/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0a2-7310-b911-248470df72a1	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-07-28	Kirim pakan finisher BR-2	2026-10-04 18:30:17.967604+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d731-7973-9045-abaa69ca12a9	TRF/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-d332-7f38-9c34-260051b68ef3	2026-07-30	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:18.562249+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de6e-7454-87f3-422062f82097	TRF/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0da-7689-8015-b09639d9462c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-08-07	Kirim pakan finisher BR-2	2026-10-04 18:30:20.403879+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e12c-7fed-8626-ef11d1a1aa64	TRF/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-c12f-721a-8cd8-5302c3693dfc	01a106ad-d332-7f38-9c34-260051b68ef3	2026-08-11	Kirim pakan finisher BR-2	2026-10-04 18:30:21.105338+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f00d-7930-be85-9e425747d0fa	TRF/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-08-23	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:24.923923+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f84d-7a1b-8cac-41c2469803d4	TRF/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-f40d-7a20-b733-56168fcec265	2026-08-30	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:27.039226+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f9e3-77ab-a80a-c5fa58f74613	TRF/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-f40d-7a20-b733-56168fcec265	2026-08-31	Mutasi pakan: Sisa pakan dipindah ke kandang yang masih berjalan	2026-10-04 18:30:27.440684+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fef5-7741-ac7c-fa61f92a0a1e	TRF/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-c116-7a51-a7ff-5ca20670d066	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-09-04	Kirim pakan finisher BR-2	2026-10-04 18:30:28.730039+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0386-7605-83ed-efc121166d55	TRF/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c0c0-7e76-a5d1-996befd8d731	01a106ad-f40d-7a20-b733-56168fcec265	2026-09-11	Kirim pakan finisher BR-2	2026-10-04 18:30:29.899252+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-05cc-7acb-b7a1-4ff93a12d78c	TRF/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ae-023f-704e-8a47-6e438951d196	2026-09-13	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:30.490953+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ca1-70ed-af31-ee7d25077d50	TRF/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be27-7b73-aba5-97b277c92753	01a106ad-c147-7b87-8dc1-7feab314ee28	01a106ae-0935-71d7-8a79-528d2a90b371	2026-09-22	Kirim pakan starter & OVK saat chick-in	2026-10-04 18:30:32.2384+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0e45-7ed6-a7fc-6ba1fe729f41	TRF/BDG/2026/IX/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bded-7b6e-a743-823445f9eaa4	01a106ad-c077-7467-a5fb-63fcc28f2f8e	01a106ae-023f-704e-8a47-6e438951d196	2026-09-25	Kirim pakan finisher BR-2	2026-10-04 18:30:32.648783+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: branches; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.branches (id, code, name, address, phone, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-b536-7f76-bf02-1115db4d6aff	BDG	Cabang Bandung	Jl. Soekarno-Hatta No. 120, Bandung	022-7501234	t	2026-10-04 18:30:09.899942+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-b5da-7c16-8a1e-37a0998b9705	CJR	Cabang Cianjur	Jl. Raya Bandung KM 5, Cianjur	0263-261234	t	2026-10-04 18:30:10.014866+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: coops; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.coops (id, code, name, farmer_id, branch_id, capacity, house_type, address, latitude, longitude, is_active, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-bfec-7b37-a51a-18c521c8bce6	KDG-BDG-INTI	Kandang Inti Lembang	01a106ad-bf47-77b6-b40b-b3c7d7c5d703	01a106ad-b536-7f76-bf02-1115db4d6aff	10000	ClosedHouse	\N	-6.813200	107.617500	t	2026-10-04 18:30:12.62377+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c093-725b-9c5f-bb53c87c1551	KDG-BDG-01	Kandang Ahmad 1	01a106ad-bf87-7c98-8271-811bb311416f	01a106ad-b536-7f76-bf02-1115db4d6aff	6000	ClosedHouse	\N	-6.976500	107.640200	t	2026-10-04 18:30:12.758343+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c0b0-728b-bb71-3e70df15f86d	KDG-BDG-02	Kandang Ahmad 2	01a106ad-bf87-7c98-8271-811bb311416f	01a106ad-b536-7f76-bf02-1115db4d6aff	7000	ClosedHouse	\N	-6.977100	107.641800	t	2026-10-04 18:30:12.786503+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c0cc-775f-96fa-cd8dda0154d7	KDG-BDG-03	Kandang Dedi	01a106ad-bf95-77e6-aff7-4d707e0c4d91	01a106ad-b536-7f76-bf02-1115db4d6aff	5000	OpenHouse	\N	-6.882000	107.519400	t	2026-10-04 18:30:12.815293+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c0e5-768d-a018-7fac5d24b72d	KDG-BDG-04	Kandang Asep	01a106ad-bf9f-7c11-a999-b0777c39c3e4	01a106ad-b536-7f76-bf02-1115db4d6aff	4000	OpenHouse	\N	-7.176300	107.571300	t	2026-10-04 18:30:12.839832+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c0fe-7ef7-883a-6874a1e63d54	KDG-CJR-INTI	Kandang Inti Cipanas	01a106ad-bfaa-73de-a52f-85ac9a8f56c9	01a106ad-b5da-7c16-8a1e-37a0998b9705	8000	ClosedHouse	\N	-6.735000	107.041200	t	2026-10-04 18:30:12.864667+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c120-72e3-bae4-d2248cc584d3	KDG-CJR-01	Kandang Ujang	01a106ad-bfb4-7c14-9f15-02079aeddd09	01a106ad-b5da-7c16-8a1e-37a0998b9705	5000	ClosedHouse	\N	-6.805100	107.100300	t	2026-10-04 18:30:12.898549+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-c139-7a07-b8e4-9ea7321c38b7	KDG-CJR-02	Kandang Nining	01a106ad-bfbe-7056-8fed-880c859337b3	01a106ad-b5da-7c16-8a1e-37a0998b9705	4000	OpenHouse	\N	-6.701200	107.049800	t	2026-10-04 18:30:12.923407+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: customers; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.customers (id, code, name, address, phone, email, payment_term_days, is_active, credit_limit, tax_identity_is_pkp, tax_identity_nitku, tax_identity_npwp, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-bee3-776e-8294-c067db6f4750	C-RPA-SB	PT Sumber Berkah Unggas (RPA)	Jl. Raya Cileunyi No. 45, Bandung	022-7798811	purchasing@sbu.example.co.id	14	t	1500000000.00	t	0213456789428000000000	0213456789428000	2026-10-04 18:30:12.358706+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	C-BKL-JA	UD Jaya Abadi (Bakul)	Pasar Ciroyom Blok C-12, Bandung	0813-2111-7788	\N	7	t	400000000.00	f	\N	\N	2026-10-04 18:30:12.381515+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf25-7367-9282-969a0e7d56d9	C-UMC	CV Unggas Makmur Cianjur	Jl. Dr. Muwardi No. 18, Cianjur	0263-270011	admin@umc.example.co.id	14	t	900000000.00	t	0314567890406000000000	0314567890406000	2026-10-04 18:30:12.392617+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf2e-7185-bf48-d258c9737111	C-WRG-01	Warung Ayam Bu Imas	Jl. Siliwangi No. 3, Cianjur	0857-1122-3344	\N	0	t	0.00	f	\N	\N	2026-10-04 18:30:12.401312+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: farmers; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.farmers (id, code, name, type, branch_id, nik, address, phone, is_active, bank_account_account_holder_name, bank_account_account_number, bank_account_bank_name, tax_identity_is_pkp, tax_identity_nitku, tax_identity_npwp, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-bf47-77b6-b40b-b3c7d7c5d703	PTN-BDG-INTI	Farm Inti Lembang	Inti	01a106ad-b536-7f76-bf02-1115db4d6aff	\N	Jl. Raya Lembang KM 9, Bandung Barat	0812-NTI0-1234	t	Farm Inti Lembang	3301-01-NTI123-50-7	BRI	f	\N	\N	2026-10-04 18:30:12.461552+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf87-7c98-8271-811bb311416f	PTN-BDG-001	H. Ahmad Suryadi	Plasma	01a106ad-b536-7f76-bf02-1115db4d6aff	3204011203750001	Kp. Cikoneng RT 02/05, Bojongsoang	0812-0010-1234	t	H. Ahmad Suryadi	330101001123507	BRI	f	\N	\N	2026-10-04 18:30:12.490446+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf95-77e6-aff7-4d707e0c4d91	PTN-BDG-002	Dedi Kurniawan	Plasma	01a106ad-b536-7f76-bf02-1115db4d6aff	3204022508820003	Ds. Cilame RT 01/03, Ngamprah	0812-0020-1234	t	Dedi Kurniawan	330101002123507	BRI	f	\N	\N	2026-10-04 18:30:12.504353+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bf9f-7c11-a999-b0777c39c3e4	PTN-BDG-003	Asep Saepudin	Plasma	01a106ad-b536-7f76-bf02-1115db4d6aff	3204031707880002	Ds. Pangalengan RT 04/01, Pangalengan	0812-0030-1234	t	Asep Saepudin	330101003123507	BRI	f	\N	\N	2026-10-04 18:30:12.513778+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bfaa-73de-a52f-85ac9a8f56c9	PTN-CJR-INTI	Farm Inti Cipanas	Inti	01a106ad-b5da-7c16-8a1e-37a0998b9705	\N	Jl. Raya Cipanas No. 77, Cianjur	0812-NTI0-1234	t	Farm Inti Cipanas	3301-01-NTI123-50-7	BRI	f	\N	\N	2026-10-04 18:30:12.524606+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bfb4-7c14-9f15-02079aeddd09	PTN-CJR-001	Ujang Hermawan	Plasma	01a106ad-b5da-7c16-8a1e-37a0998b9705	3203010505800004	Kp. Sukamaju RT 03/02, Cugenang	0812-0010-1234	t	Ujang Hermawan	330101001123507	BRI	f	\N	\N	2026-10-04 18:30:12.534614+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-bfbe-7056-8fed-880c859337b3	PTN-CJR-002	Nining Sumarni	Plasma	01a106ad-b5da-7c16-8a1e-37a0998b9705	3203024411850001	Ds. Sukaresmi RT 01/04, Sukaresmi	0812-0020-1234	t	Nining Sumarni	330101002123507	BRI	f	\N	\N	2026-10-04 18:30:12.545123+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: item_uom_conversions; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.item_uom_conversions (item_id, uom_id, factor) FROM stdin;
01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	50.000000
01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	50.000000
\.


--
-- Data for Name: items; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.items (id, code, name, category, base_uom_id, tax_code_id, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bd1e-7c26-a889-94b43fae1c60	DOC-CP707	DOC Broiler CP 707	Doc	01a0f50c-aaca-7a3c-a9ca-424657b27654	01a106ad-bcf7-75f7-995a-8a39126ee3b0	t	2026-10-04 18:30:11.937244+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bd76-76e3-9971-2ce1aae02eb4	PKN-BR1	Pakan Starter BR-1 Crumble	Feed	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	01a106ad-bcf7-75f7-995a-8a39126ee3b0	t	2026-10-04 18:30:11.972368+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bd91-75b9-8797-63a66fb4ba5d	PKN-BR2	Pakan Finisher BR-2 Pellet	Feed	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	01a106ad-bcf7-75f7-995a-8a39126ee3b0	t	2026-10-04 18:30:11.993239+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bd9f-7179-9506-e6071b528b74	VKS-NDIB	Vaksin ND-IB Live 1.000 ds	Ovk	01a0f50c-aaea-7a32-8492-6efb11e274c5	01a106ad-bc9e-7d36-a8a3-65c27114ce69	t	2026-10-04 18:30:12.006454+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bdab-762c-ad3a-cd0613d5d1df	VKS-GMB	Vaksin Gumboro 1.000 ds	Ovk	01a0f50c-aaea-7a32-8492-6efb11e274c5	01a106ad-bc9e-7d36-a8a3-65c27114ce69	t	2026-10-04 18:30:12.018972+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bdb8-7c78-af20-a4a6f3355778	VIT-ELK	Vitamin & Elektrolit 250 gr	Ovk	01a0f50c-aaea-7c81-a226-2ef043004aee	01a106ad-bc9e-7d36-a8a3-65c27114ce69	t	2026-10-04 18:30:12.030542+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bdc5-76d0-acbf-ed1ce41a860a	AYAM-HIDUP	Ayam Broiler Hidup	LiveBird	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	01a106ad-bcf7-75f7-995a-8a39126ee3b0	t	2026-10-04 18:30:12.043527+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bdd1-7940-b069-4c166d1c8c29	SKM-KRG	Sekam Padi (alas kandang)	Other	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	\N	t	2026-10-04 18:30:12.054502+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: tax_codes; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.tax_codes (id, code, name, type, vat_treatment, income_tax_article, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bc9e-7d36-a8a3-65c27114ce69	PPN-11	PPN 12% DPP nilai lain (efektif 11%)	Vat	Taxable	\N	t	2026-10-04 18:30:11.799688+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bcf7-75f7-995a-8a39126ee3b0	PPN-BBS	PPN dibebaskan (DOC, pakan, ayam hidup)	Vat	Exempt	\N	t	2026-10-04 18:30:11.835194+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-bd06-72df-8a06-c944cfed04a6	PPH23-2	PPh Pasal 23 (2%)	IncomeTax	\N	Pph23	t	2026-10-04 18:30:11.850208+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: tax_rates; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.tax_rates (tax_code_id, effective_from, rate_percent, tax_base_ratio) FROM stdin;
01a106ad-bc9e-7d36-a8a3-65c27114ce69	2025-01-01	11.0000	1.00000000
01a106ad-bcf7-75f7-995a-8a39126ee3b0	2025-01-01	0.0000	1.00000000
01a106ad-bd06-72df-8a06-c944cfed04a6	2025-01-01	2.0000	1.00000000
\.


--
-- Data for Name: uoms; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.uoms (id, code, name, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a0f50c-aaca-7a3c-a9ca-424657b27654	EKOR	Ekor	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aae9-7688-bfe5-92fdb4b95fff	KG	Kilogram	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-72c8-81d2-cd12265a422a	GR	Gram	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	SAK	Sak	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-766f-8bcf-11a6a1227d4b	PCS	Pcs	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-7a32-8492-6efb11e274c5	VIAL	Vial	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-7aa8-9c9b-e574a4464deb	LTR	Liter	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-7c57-9a3f-d0b7da329d05	ML	Mililiter	2026-10-01 08:20:43.36438+07	\N	\N	\N
01a0f50c-aaea-7c81-a226-2ef043004aee	BTL	Botol	2026-10-01 08:20:43.36438+07	\N	\N	\N
\.


--
-- Data for Name: vendors; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.vendors (id, code, name, address, phone, email, payment_term_days, is_active, bank_account_account_holder_name, bank_account_account_number, bank_account_bank_name, tax_identity_is_pkp, tax_identity_nitku, tax_identity_npwp, created_at_utc, created_by, modified_at_utc, modified_by, price_tolerance_percent, documents) FROM stdin;
01a106ad-be40-7cd9-91c5-8d2f6d22faa9	V-CPI	PT Charoen Pokphand Indonesia	Jl. Ancol VIII No. 1, Jakarta Utara	021-6919999	sales@cp.example.co.id	30	t	PT Charoen Pokphand Indonesia	0353012345	BCA	t	0013090721092000000000	0013090721092000	2026-10-04 18:30:12.199932+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	{}
01a106ad-be80-70ef-bc7b-798de012dccf	V-JPF	PT Japfa Comfeed Indonesia	Jl. Daan Mogot KM 12, Jakarta Barat	021-28545680	order@japfa.example.co.id	30	t	PT Japfa Comfeed Indonesia	1180009876543	Mandiri	t	0010615074092000000000	0010615074092000	2026-10-04 18:30:12.227913+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	{}
01a106ad-be8d-7ecd-a218-155f48892806	V-MDN	PT Medion Farma Jaya	Jl. Babakan Ciparay No. 282, Bandung	022-6036000	cs@medion.example.co.id	14	t	PT Medion Farma Jaya	0098765432	BNI	t	0013245698441000000000	0013245698441000	2026-10-04 18:30:12.240666+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	2.00	{}
01a106ad-beb7-7950-b38c-4948b42ee796	V-SKM	UD Sekam Jaya	Kp. Cibeber, Cianjur	0812-2233-4455	\N	0	t	Ujang Sekam	412301002233501	BRI	f	\N	\N	2026-10-04 18:30:12.283067+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0.00	{}
\.


--
-- Data for Name: warehouses; Type: TABLE DATA; Schema: master; Owner: postgres
--

COPY master.warehouses (id, code, name, branch_id, type, coop_id, address, is_active, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-bded-7b6e-a743-823445f9eaa4	GI-BDG	Gudang Induk Bandung	01a106ad-b536-7f76-bf02-1115db4d6aff	Central	\N	Jl. Soekarno-Hatta No. 120, Bandung	t	2026-10-04 18:30:12.113261+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-be27-7b73-aba5-97b277c92753	GI-CJR	Gudang Induk Cianjur	01a106ad-b5da-7c16-8a1e-37a0998b9705	Central	\N	Jl. Raya Bandung KM 5, Cianjur	t	2026-10-04 18:30:12.137018+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c077-7467-a5fb-63fcc28f2f8e	GK-KDG-BDG-INTI	Gudang Kandang Inti Lembang	01a106ad-b536-7f76-bf02-1115db4d6aff	Coop	01a106ad-bfec-7b37-a51a-18c521c8bce6	\N	t	2026-10-04 18:30:12.728467+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c0a2-7310-b911-248470df72a1	GK-KDG-BDG-01	Gudang Kandang Ahmad 1	01a106ad-b536-7f76-bf02-1115db4d6aff	Coop	01a106ad-c093-725b-9c5f-bb53c87c1551	\N	t	2026-10-04 18:30:12.771024+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c0c0-7e76-a5d1-996befd8d731	GK-KDG-BDG-02	Gudang Kandang Ahmad 2	01a106ad-b536-7f76-bf02-1115db4d6aff	Coop	01a106ad-c0b0-728b-bb71-3e70df15f86d	\N	t	2026-10-04 18:30:12.800858+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c0da-7689-8015-b09639d9462c	GK-KDG-BDG-03	Gudang Kandang Dedi	01a106ad-b536-7f76-bf02-1115db4d6aff	Coop	01a106ad-c0cc-775f-96fa-cd8dda0154d7	\N	t	2026-10-04 18:30:12.826788+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c0f4-7681-8044-983c7e33d046	GK-KDG-BDG-04	Gudang Kandang Asep	01a106ad-b536-7f76-bf02-1115db4d6aff	Coop	01a106ad-c0e5-768d-a018-7fac5d24b72d	\N	t	2026-10-04 18:30:12.852522+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c116-7a51-a7ff-5ca20670d066	GK-KDG-CJR-INTI	Gudang Kandang Inti Cipanas	01a106ad-b5da-7c16-8a1e-37a0998b9705	Coop	01a106ad-c0fe-7ef7-883a-6874a1e63d54	\N	t	2026-10-04 18:30:12.886584+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c12f-721a-8cd8-5302c3693dfc	GK-KDG-CJR-01	Gudang Kandang Ujang	01a106ad-b5da-7c16-8a1e-37a0998b9705	Coop	01a106ad-c120-72e3-bae4-d2248cc584d3	\N	t	2026-10-04 18:30:12.911483+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
01a106ad-c147-7b87-8dc1-7feab314ee28	GK-KDG-CJR-02	Gudang Kandang Nining	01a106ad-b5da-7c16-8a1e-37a0998b9705	Coop	01a106ad-c139-7a07-b8e4-9ea7321c38b7	\N	t	2026-10-04 18:30:12.935865+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: contract_incentives; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.contract_incentives (contract_id, line_number, name, kind, metric, range_from, range_to, basis, amount) FROM stdin;
01a106ad-c18d-7237-a0e3-d935699c85c6	1	Bonus FCR ≤ 1,50	Bonus	Fcr	0.0000	1.5000	PerKg	150.00
01a106ad-c18d-7237-a0e3-d935699c85c6	2	Bonus IP ≥ 380	Bonus	Ip	380.0000	999.0000	PerKg	100.00
01a106ad-c18d-7237-a0e3-d935699c85c6	3	Potongan deplesi > 6%	Deduction	Depletion	6.0100	100.0000	PerBird	150.00
01a106ad-c27b-708d-99cc-89e0647e1680	1	Bonus FCR ≤ 1,50	Bonus	Fcr	0.0000	1.5000	PerKg	150.00
01a106ad-c27b-708d-99cc-89e0647e1680	2	Bonus IP ≥ 380	Bonus	Ip	380.0000	999.0000	PerKg	100.00
01a106ad-c27b-708d-99cc-89e0647e1680	3	Potongan deplesi > 6%	Deduction	Depletion	6.0100	100.0000	PerBird	150.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	1	Bonus FCR ≤ 1,50	Bonus	Fcr	0.0000	1.5000	PerKg	150.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	2	Bonus IP ≥ 380	Bonus	Ip	380.0000	999.0000	PerKg	100.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	3	Potongan deplesi > 6%	Deduction	Depletion	6.0100	100.0000	PerBird	150.00
\.


--
-- Data for Name: contract_input_prices; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.contract_input_prices (contract_id, item_id, price) FROM stdin;
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bd1e-7c26-a889-94b43fae1c60	7900.00
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bd76-76e3-9971-2ce1aae02eb4	8600.00
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bd91-75b9-8797-63a66fb4ba5d	8300.00
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bd9f-7179-9506-e6071b528b74	100000.00
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bdab-762c-ad3a-cd0613d5d1df	118000.00
01a106ad-c18d-7237-a0e3-d935699c85c6	01a106ad-bdb8-7c78-af20-a4a6f3355778	47500.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bd1e-7c26-a889-94b43fae1c60	7900.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	8600.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	8300.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bd9f-7179-9506-e6071b528b74	100000.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bdab-762c-ad3a-cd0613d5d1df	118000.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	01a106ad-bdb8-7c78-af20-a4a6f3355778	47500.00
\.


--
-- Data for Name: contract_live_bird_prices; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.contract_live_bird_prices (contract_id, min_weight_kg, max_weight_kg, price_per_kg) FROM stdin;
01a106ad-c18d-7237-a0e3-d935699c85c6	0.500	1.600	21000.00
01a106ad-c18d-7237-a0e3-d935699c85c6	1.600	1.800	20600.00
01a106ad-c18d-7237-a0e3-d935699c85c6	1.800	2.000	20300.00
01a106ad-c18d-7237-a0e3-d935699c85c6	2.000	2.200	20000.00
01a106ad-c18d-7237-a0e3-d935699c85c6	2.200	5.000	19600.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	0.500	1.600	21000.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	1.600	1.800	20600.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	1.800	2.000	20300.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	2.000	2.200	20000.00
01a106ad-c29f-73ee-9803-d9755e1eef7f	2.200	5.000	19600.00
\.


--
-- Data for Name: contracts; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.contracts (id, code, name, branch_id, scheme, status, valid_from, valid_to, plasma_profit_share_percent, income_tax_code_id, notes, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-c18d-7237-a0e3-d935699c85c6	KTR-BDG-HK26	Harga Kontrak Bandung 2026	01a106ad-b536-7f76-bf02-1115db4d6aff	PriceContract	Active	2026-07-01	2027-06-30	\N	01a106ad-bd06-72df-8a06-c944cfed04a6	Harga sapronak & jaminan harga ayam per rentang BW	2026-10-04 18:30:13.099417+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.224012+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c27b-708d-99cc-89e0647e1680	KTR-BDG-BH26	Bagi Hasil Bandung 2026	01a106ad-b536-7f76-bf02-1115db4d6aff	ProfitSharing	Active	2026-07-01	2027-06-30	40.0000	01a106ad-bd06-72df-8a06-c944cfed04a6	Bagi hasil 40% laba siklus untuk plasma; rugi ditanggung inti	2026-10-04 18:30:13.246374+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.262518+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c29f-73ee-9803-d9755e1eef7f	KTR-CJR-HK26	Harga Kontrak Cianjur 2026	01a106ad-b5da-7c16-8a1e-37a0998b9705	PriceContract	Active	2026-07-01	2027-06-30	\N	01a106ad-bd06-72df-8a06-c944cfed04a6	Harga sapronak & jaminan harga ayam per rentang BW	2026-10-04 18:30:13.28216+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:13.304276+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
\.


--
-- Data for Name: cycle_harvests; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.cycle_harvests (id, cycle_id, date, age_days, birds, weight_kg, notes, documents) FROM stdin;
01a106ad-e6cf-79c6-a3af-8de25a7c2971	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-08-18	33	1592	2929.600	Truk D 8101 BL, timbang di kandang	{}
01a106ad-e7ec-70a3-be77-51b1185b72df	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-08-19	34	1589	3048.200	Truk D 8102 CM, timbang di kandang	{}
01a106ad-eb06-710f-936f-b244c0e36501	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	2026-08-20	35	1588	3277.700	Truk D 8103 DN, timbang di kandang	{}
01a106ad-f515-7090-a53f-d79b5ce4066d	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-08-27	32	2143	3568.800	Truk D 8104 EO, timbang di kandang	{}
01a106ad-f726-7880-9328-a878c2ebc091	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2026-08-29	34	2135	4009.900	Truk D 8105 FP, timbang di kandang	{}
01a106ad-fcb1-71bc-883b-63a739c5d666	01a106ad-d332-7f38-9c34-260051b68ef3	2026-09-02	34	1548	2883.500	Truk F 8106 GQ, timbang di kandang	{}
01a106ad-fd1c-724e-afc0-ffe52c0261c1	01a106ad-d332-7f38-9c34-260051b68ef3	2026-09-03	35	1546	3030.200	Truk F 8107 HR, timbang di kandang	{}
01a106ad-fe78-7e03-81e0-fb7a7830ce07	01a106ad-d332-7f38-9c34-260051b68ef3	2026-09-04	36	1542	3176.200	Truk F 8108 IS, timbang di kandang	{}
01a106ae-0fe8-7cf8-b580-4a8a6fd20929	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-09-27	35	2226	4523.300	Truk F 8109 JT, timbang di kandang	{}
01a106ae-1157-77d2-885f-259c6d236f59	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-09-28	36	2224	4769.100	Truk F 8110 KK, timbang di kandang	{}
01a106ae-127c-7fa9-bfa9-7e5af93096dd	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2026-09-29	37	2223	4986.700	Truk F 8111 LL, timbang di kandang	{}
01a106ae-156a-768d-81c8-0eaa781b8ab8	01a106ad-f40d-7a20-b733-56168fcec265	2026-10-02	33	1436	2648.500	Truk D 8112 MM, timbang di kandang	{}
01a106ae-1629-7b4b-8b45-bfd631bd907c	01a106ad-f40d-7a20-b733-56168fcec265	2026-10-03	34	1434	2777.200	Truk D 8113 NN, timbang di kandang	{}
\.


--
-- Data for Name: production_cycles; Type: TABLE DATA; Schema: partnership; Owner: postgres
--

COPY partnership.production_cycles (id, number, branch_id, farmer_id, coop_id, contract_id, contract_snapshot, status, planned_chick_in_date, planned_population, chick_in_date, initial_population, notes, cancellation_reason, created_at_utc, created_by, modified_at_utc, modified_by, closed_date, closing_performance, harvested_birds, harvested_weight_kg, total_culling, total_mortality, closing_cost, documents) FROM stdin;
01a106ad-d05a-759c-a6ff-c3e67df7f41b	SKL/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf95-77e6-aff7-4d707e0c4d91	01a106ad-c0cc-775f-96fa-cd8dda0154d7	01a106ad-c27b-708d-99cc-89e0647e1680	{"scheme": "ProfitSharing", "contractId": "01a106ad-c27b-708d-99cc-89e0647e1680", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [], "contractCode": "KTR-BDG-BH26", "liveBirdPrices": [], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": 40.0000}	Closed	2026-07-26	4500	2026-07-26	4500	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:16.794854+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.59834+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-08-29	{"ip": 303.905, "fcr": 1.679, "feedKg": 12727.7000, "adgGram": 53.686, "ageDays": 32.998, "culling": 6, "mortality": 216, "population": 0, "liveWeightKg": 7578.700, "harvestedBirds": 4278, "averageWeightKg": 1.772, "depletionPercent": 4.933, "harvestedWeightKg": 7578.700, "initialPopulation": 4500}	4278	7578.700	6	216	{"docCost": 33300000.00, "ovkCost": 2140000.00, "feedCost": 100886212.40, "costPerKg": 17988.07, "totalCost": 136326212.40, "adjustment": -1082569.19, "costPerBird": 31866.81, "harvestedBirds": 4278, "recognizedCost": 137408781.59, "harvestedWeightKg": 7578.700}	{}
01a106ad-ea62-7df6-aaaa-1359b7c73fa1	SKL/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bfaa-73de-a52f-85ac9a8f56c9	01a106ad-c0fe-7ef7-883a-6874a1e63d54	\N	\N	Closed	2026-08-23	7000	2026-08-23	7000	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:23.458773+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.396699+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-09-29	{"ip": 358.403, "fcr": 1.581, "feedKg": 22575.4000, "adgGram": 59.441, "ageDays": 36.000, "culling": 23, "mortality": 304, "population": 0, "liveWeightKg": 14279.100, "harvestedBirds": 6673, "averageWeightKg": 2.140, "depletionPercent": 4.671, "harvestedWeightKg": 14279.100, "initialPopulation": 7000}	6673	14279.100	23	304	{"docCost": 51800000.00, "ovkCost": 2954000.00, "feedCost": 178305990.00, "costPerKg": 16321.76, "totalCost": 233059990.00, "adjustment": -157056.96, "costPerBird": 34925.82, "harvestedBirds": 6673, "recognizedCost": 233217046.96, "harvestedWeightKg": 14279.100}	{}
01a106ad-c6f1-7f5f-8d11-f2ccfb191671	SKL/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf87-7c98-8271-811bb311416f	01a106ad-c093-725b-9c5f-bb53c87c1551	01a106ad-c18d-7237-a0e3-d935699c85c6	{"scheme": "PriceContract", "contractId": "01a106ad-c18d-7237-a0e3-d935699c85c6", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [{"price": {"amount": 7900.00}, "itemId": "01a106ad-bd1e-7c26-a889-94b43fae1c60"}, {"price": {"amount": 8600.00}, "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4"}, {"price": {"amount": 8300.00}, "itemId": "01a106ad-bd91-75b9-8797-63a66fb4ba5d"}, {"price": {"amount": 100000.00}, "itemId": "01a106ad-bd9f-7179-9506-e6071b528b74"}, {"price": {"amount": 118000.00}, "itemId": "01a106ad-bdab-762c-ad3a-cd0613d5d1df"}, {"price": {"amount": 47500.00}, "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778"}], "contractCode": "KTR-BDG-HK26", "liveBirdPrices": [{"pricePerKg": {"amount": 21000.00}, "maxWeightKg": 1.600, "minWeightKg": 0.500}, {"pricePerKg": {"amount": 20600.00}, "maxWeightKg": 1.800, "minWeightKg": 1.600}, {"pricePerKg": {"amount": 20300.00}, "maxWeightKg": 2.000, "minWeightKg": 1.800}, {"pricePerKg": {"amount": 20000.00}, "maxWeightKg": 2.200, "minWeightKg": 2.000}, {"pricePerKg": {"amount": 19600.00}, "maxWeightKg": 5.000, "minWeightKg": 2.200}], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": null}	Settled	2026-07-16	5000	2026-07-16	5000	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:14.446672+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.379055+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-08-20	{"ip": 372.130, "fcr": 1.463, "feedKg": 13541.5000, "adgGram": 57.083, "ageDays": 33.999, "culling": 21, "mortality": 210, "population": 0, "liveWeightKg": 9255.500, "harvestedBirds": 4769, "averageWeightKg": 1.941, "depletionPercent": 4.62, "harvestedWeightKg": 9255.500, "initialPopulation": 5000}	4769	9255.500	21	210	{"docCost": 37000000.00, "ovkCost": 2140000.00, "feedCost": 107446181.81, "costPerKg": 15837.74, "totalCost": 146586181.81, "adjustment": -637091.58, "costPerBird": 30737.30, "harvestedBirds": 4769, "recognizedCost": 147223273.39, "harvestedWeightKg": 9255.500}	{}
01a106ad-d332-7f38-9c34-260051b68ef3	SKL/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bfb4-7c14-9f15-02079aeddd09	01a106ad-c120-72e3-bae4-d2248cc584d3	01a106ad-c29f-73ee-9803-d9755e1eef7f	{"scheme": "PriceContract", "contractId": "01a106ad-c29f-73ee-9803-d9755e1eef7f", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [{"price": {"amount": 7900.00}, "itemId": "01a106ad-bd1e-7c26-a889-94b43fae1c60"}, {"price": {"amount": 8600.00}, "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4"}, {"price": {"amount": 8300.00}, "itemId": "01a106ad-bd91-75b9-8797-63a66fb4ba5d"}, {"price": {"amount": 100000.00}, "itemId": "01a106ad-bd9f-7179-9506-e6071b528b74"}, {"price": {"amount": 118000.00}, "itemId": "01a106ad-bdab-762c-ad3a-cd0613d5d1df"}, {"price": {"amount": 47500.00}, "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778"}], "contractCode": "KTR-CJR-HK26", "liveBirdPrices": [{"pricePerKg": {"amount": 21000.00}, "maxWeightKg": 1.600, "minWeightKg": 0.500}, {"pricePerKg": {"amount": 20600.00}, "maxWeightKg": 1.800, "minWeightKg": 1.600}, {"pricePerKg": {"amount": 20300.00}, "maxWeightKg": 2.000, "minWeightKg": 1.800}, {"pricePerKg": {"amount": 20000.00}, "maxWeightKg": 2.200, "minWeightKg": 2.000}, {"pricePerKg": {"amount": 19600.00}, "maxWeightKg": 5.000, "minWeightKg": 2.200}], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": null}	Settled	2026-07-30	5000	2026-07-30	5000	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:17.522867+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.475769+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-09-04	{"ip": 293.722, "fcr": 1.768, "feedKg": 16075.3000, "adgGram": 56.023, "ageDays": 34.999, "culling": 9, "mortality": 355, "population": 0, "liveWeightKg": 9089.900, "harvestedBirds": 4636, "averageWeightKg": 1.961, "depletionPercent": 7.28, "harvestedWeightKg": 9089.900, "initialPopulation": 5000}	4636	9089.900	9	355	{"docCost": 37000000.00, "ovkCost": 2140000.00, "feedCost": 127817245.00, "costPerKg": 18367.34, "totalCost": 166957245.00, "adjustment": -29537.08, "costPerBird": 36013.21, "harvestedBirds": 4636, "recognizedCost": 166986782.08, "harvestedWeightKg": 9089.900}	{}
01a106ae-023f-704e-8a47-6e438951d196	SKL/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf47-77b6-b40b-b3c7d7c5d703	01a106ad-bfec-7b37-a51a-18c521c8bce6	\N	\N	Active	2026-09-13	8000	2026-09-13	8000	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:29.567901+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.011309+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0	0.000	3	235	\N	{}
01a106ad-f40d-7a20-b733-56168fcec265	SKL/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf87-7c98-8271-811bb311416f	01a106ad-c0b0-728b-bb71-3e70df15f86d	01a106ad-c18d-7237-a0e3-d935699c85c6	{"scheme": "PriceContract", "contractId": "01a106ad-c18d-7237-a0e3-d935699c85c6", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [{"price": {"amount": 7900.00}, "itemId": "01a106ad-bd1e-7c26-a889-94b43fae1c60"}, {"price": {"amount": 8600.00}, "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4"}, {"price": {"amount": 8300.00}, "itemId": "01a106ad-bd91-75b9-8797-63a66fb4ba5d"}, {"price": {"amount": 100000.00}, "itemId": "01a106ad-bd9f-7179-9506-e6071b528b74"}, {"price": {"amount": 118000.00}, "itemId": "01a106ad-bdab-762c-ad3a-cd0613d5d1df"}, {"price": {"amount": 47500.00}, "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778"}], "contractCode": "KTR-BDG-HK26", "liveBirdPrices": [{"pricePerKg": {"amount": 21000.00}, "maxWeightKg": 1.600, "minWeightKg": 0.500}, {"pricePerKg": {"amount": 20600.00}, "maxWeightKg": 1.800, "minWeightKg": 1.600}, {"pricePerKg": {"amount": 20300.00}, "maxWeightKg": 2.000, "minWeightKg": 1.800}, {"pricePerKg": {"amount": 20000.00}, "maxWeightKg": 2.200, "minWeightKg": 2.000}, {"pricePerKg": {"amount": 19600.00}, "maxWeightKg": 5.000, "minWeightKg": 2.200}], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": null}	Harvesting	2026-08-30	6000	2026-08-30	6000	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:25.933795+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.665632+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	2870	5425.700	20	241	\N	{}
01a106ae-179c-7392-bd9f-1274d80f58e8	SKL/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf9f-7c11-a999-b0777c39c3e4	01a106ad-c0e5-768d-a018-7fac5d24b72d	01a106ad-c18d-7237-a0e3-d935699c85c6	{"scheme": "PriceContract", "contractId": "01a106ad-c18d-7237-a0e3-d935699c85c6", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [{"price": {"amount": 7900.00}, "itemId": "01a106ad-bd1e-7c26-a889-94b43fae1c60"}, {"price": {"amount": 8600.00}, "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4"}, {"price": {"amount": 8300.00}, "itemId": "01a106ad-bd91-75b9-8797-63a66fb4ba5d"}, {"price": {"amount": 100000.00}, "itemId": "01a106ad-bd9f-7179-9506-e6071b528b74"}, {"price": {"amount": 118000.00}, "itemId": "01a106ad-bdab-762c-ad3a-cd0613d5d1df"}, {"price": {"amount": 47500.00}, "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778"}], "contractCode": "KTR-BDG-HK26", "liveBirdPrices": [{"pricePerKg": {"amount": 21000.00}, "maxWeightKg": 1.600, "minWeightKg": 0.500}, {"pricePerKg": {"amount": 20600.00}, "maxWeightKg": 1.800, "minWeightKg": 1.600}, {"pricePerKg": {"amount": 20300.00}, "maxWeightKg": 2.000, "minWeightKg": 1.800}, {"pricePerKg": {"amount": 20000.00}, "maxWeightKg": 2.200, "minWeightKg": 2.000}, {"pricePerKg": {"amount": 19600.00}, "maxWeightKg": 5.000, "minWeightKg": 2.200}], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": null}	Planned	2026-10-07	4000	\N	\N	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:35.036698+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	\N	\N	0	0.000	0	0	\N	{}
01a106ae-0935-71d7-8a79-528d2a90b371	SKL/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bfbe-7056-8fed-880c859337b3	01a106ad-c139-7a07-b8e4-9ea7321c38b7	01a106ad-c29f-73ee-9803-d9755e1eef7f	{"scheme": "PriceContract", "contractId": "01a106ad-c29f-73ee-9803-d9755e1eef7f", "incentives": [{"kind": "Bonus", "name": "Bonus FCR ≤ 1,50", "basis": "PerKg", "amount": {"amount": 150.00}, "metric": "Fcr", "rangeTo": 1.5000, "rangeFrom": 0.0000}, {"kind": "Bonus", "name": "Bonus IP ≥ 380", "basis": "PerKg", "amount": {"amount": 100.00}, "metric": "Ip", "rangeTo": 999.0000, "rangeFrom": 380.0000}, {"kind": "Deduction", "name": "Potongan deplesi > 6%", "basis": "PerBird", "amount": {"amount": 150.00}, "metric": "Depletion", "rangeTo": 100.0000, "rangeFrom": 6.0100}], "inputPrices": [{"price": {"amount": 7900.00}, "itemId": "01a106ad-bd1e-7c26-a889-94b43fae1c60"}, {"price": {"amount": 8600.00}, "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4"}, {"price": {"amount": 8300.00}, "itemId": "01a106ad-bd91-75b9-8797-63a66fb4ba5d"}, {"price": {"amount": 100000.00}, "itemId": "01a106ad-bd9f-7179-9506-e6071b528b74"}, {"price": {"amount": 118000.00}, "itemId": "01a106ad-bdab-762c-ad3a-cd0613d5d1df"}, {"price": {"amount": 47500.00}, "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778"}], "contractCode": "KTR-CJR-HK26", "liveBirdPrices": [{"pricePerKg": {"amount": 21000.00}, "maxWeightKg": 1.600, "minWeightKg": 0.500}, {"pricePerKg": {"amount": 20600.00}, "maxWeightKg": 1.800, "minWeightKg": 1.600}, {"pricePerKg": {"amount": 20300.00}, "maxWeightKg": 2.000, "minWeightKg": 1.800}, {"pricePerKg": {"amount": 20000.00}, "maxWeightKg": 2.200, "minWeightKg": 2.000}, {"pricePerKg": {"amount": 19600.00}, "maxWeightKg": 5.000, "minWeightKg": 2.200}], "incomeTaxCodeId": "01a106ad-bd06-72df-8a06-c944cfed04a6", "plasmaProfitSharePercent": null}	Active	2026-09-22	3500	2026-09-22	3500	Rencana chick-in DOC CP 707	\N	2026-10-04 18:30:31.349312+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.16973+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	0	0.000	0	91	\N	{}
\.


--
-- Data for Name: purchase_order_lines; Type: TABLE DATA; Schema: procurement; Owner: postgres
--

COPY procurement.purchase_order_lines (purchase_order_id, line_number, item_id, uom_id, quantity, tax_code_id, quantity_received, unit_price) FROM stdin;
01a106ad-c831-7346-9239-cc17246be1df	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	61.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	61.0000	407500.00
01a106ad-c831-7346-9239-cc17246be1df	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	225.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	225.0000	395000.00
01a106ad-c85a-70d9-a08a-193ddff2454c	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	95000.00
01a106ad-c85a-70d9-a08a-193ddff2454c	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	5.0000	112000.00
01a106ad-c85a-70d9-a08a-193ddff2454c	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	16.0000	42000.00
01a106ad-c7b6-76e6-8710-e2ea3f456d89	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	5000.0000	7400.00
01a106ad-d099-7926-9854-587dcf0ccdec	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	60.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	60.0000	405000.00
01a106ad-d099-7926-9854-587dcf0ccdec	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	209.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	209.0000	392500.00
01a106ad-d0bb-7a33-9e48-09d160670a90	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	95000.00
01a106ad-d0bb-7a33-9e48-09d160670a90	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	5.0000	112000.00
01a106ad-d0bb-7a33-9e48-09d160670a90	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	16.0000	42000.00
01a106ad-d074-7321-9aa3-c7bf0d824568	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	4500.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	4500.0000	7400.00
01a106ad-d36b-7ff1-8a59-90fecddb2138	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	70.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	70.0000	407500.00
01a106ad-d36b-7ff1-8a59-90fecddb2138	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	277.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	277.0000	395000.00
01a106ad-d38e-7e14-a5ea-e2b6d7da859c	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	95000.00
01a106ad-d38e-7e14-a5ea-e2b6d7da859c	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	5.0000	112000.00
01a106ad-d38e-7e14-a5ea-e2b6d7da859c	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	16.0000	42000.00
01a106ad-d349-7a6c-b9fb-cad4ed3bbd16	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	5000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	5000.0000	7400.00
01a106ad-eab1-7ef8-90a5-b905cf6caae4	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	91.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	91.0000	405000.00
01a106ad-eab1-7ef8-90a5-b905cf6caae4	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	386.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	386.0000	392500.00
01a106ad-ead4-7c8d-8b03-cd56500d7e26	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	15.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	15.0000	95000.00
01a106ad-ead4-7c8d-8b03-cd56500d7e26	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	7.0000	112000.00
01a106ad-ead4-7c8d-8b03-cd56500d7e26	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	21.0000	42000.00
01a106ad-ea88-75de-a783-288fe4369f7a	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	7000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	7000.0000	7400.00
01a106ad-f43c-7c60-8ee7-d72ea304cd19	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	76.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	76.0000	407500.00
01a106ad-f43c-7c60-8ee7-d72ea304cd19	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	294.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	294.0000	395000.00
01a106ad-f458-7c58-8cbf-94cd8e8f6530	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	13.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	13.0000	95000.00
01a106ad-f458-7c58-8cbf-94cd8e8f6530	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	6.0000	112000.00
01a106ad-f458-7c58-8cbf-94cd8e8f6530	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	16.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	16.0000	42000.00
01a106ad-f422-79ea-93d5-1409a717f0aa	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	6000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	6000.0000	7400.00
01a106ae-0270-7a0a-8abb-ff9657ebf0f5	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	104.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	104.0000	405000.00
01a106ae-0270-7a0a-8abb-ff9657ebf0f5	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	441.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	441.0000	392500.00
01a106ae-028f-7000-addf-42d5fb63efd9	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	17.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	17.0000	95000.00
01a106ae-028f-7000-addf-42d5fb63efd9	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	8.0000	112000.00
01a106ae-028f-7000-addf-42d5fb63efd9	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	21.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	21.0000	42000.00
01a106ae-0254-741a-8076-a451dd9c06ea	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	8000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	8000.0000	7400.00
01a106ae-0962-7ac7-b614-41ab07d1f304	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	46.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	46.0000	407500.00
01a106ae-0962-7ac7-b614-41ab07d1f304	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	187.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	187.0000	395000.00
01a106ae-097f-777c-9170-865377bfb9c2	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	9.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	9.0000	95000.00
01a106ae-097f-777c-9170-865377bfb9c2	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	4.0000	112000.00
01a106ae-097f-777c-9170-865377bfb9c2	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	11.0000	42000.00
01a106ae-0947-7c24-b029-dd550944e800	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	3500.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	3500.0000	7400.00
01a106ae-17ae-7df4-8664-2a0e8d2c9877	1	01a106ad-bd1e-7c26-a889-94b43fae1c60	01a0f50c-aaca-7a3c-a9ca-424657b27654	4000.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	7400.00
01a106ae-17c6-734c-b815-1496cd1d2fe3	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	52.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	407500.00
01a106ae-17c6-734c-b815-1496cd1d2fe3	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	214.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	395000.00
01a106ae-17e0-7387-9263-9f7d619577b7	1	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	9.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	0.0000	95000.00
01a106ae-17e0-7387-9263-9f7d619577b7	2	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	0.0000	112000.00
01a106ae-17e0-7387-9263-9f7d619577b7	3	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	11.0000	01a106ad-bc9e-7d36-a8a3-65c27114ce69	0.0000	42000.00
01a106ae-1a83-7450-beaa-1665838fec1a	1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	120.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	405000.00
01a106ae-1a83-7450-beaa-1665838fec1a	2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aaea-73f8-b8fd-6b2ddb7c7135	300.0000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	392500.00
\.


--
-- Data for Name: purchase_orders; Type: TABLE DATA; Schema: procurement; Owner: postgres
--

COPY procurement.purchase_orders (id, number, branch_id, vendor_id, order_date, expected_date, status, notes, approved_by, approved_at_utc, cancellation_reason, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ae-0270-7a0a-8abb-ff9657ebf0f5	PO/BDG/2026/IX/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	2026-09-09	2026-09-12	Received	Pakan siklus SKL/BDG/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.626407+07	\N	2026-10-04 18:30:29.616849+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.987809+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-eab1-7ef8-90a5-b905cf6caae4	PO/CJR/2026/VIII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be80-70ef-bc7b-798de012dccf	2026-08-19	2026-08-22	Received	Pakan siklus SKL/CJR/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:23.551245+07	\N	2026-10-04 18:30:23.538152+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.055029+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-ead4-7c8d-8b03-cd56500d7e26	PO/CJR/2026/VIII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	2026-08-19	2026-08-22	Received	OVK siklus SKL/CJR/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:23.58427+07	\N	2026-10-04 18:30:23.572602+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.111506+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c831-7346-9239-cc17246be1df	PO/BDG/2026/VII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-07-12	2026-07-15	Received	Pakan siklus SKL/BDG/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.722414+07	\N	2026-10-04 18:30:14.706237+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.02303+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c85a-70d9-a08a-193ddff2454c	PO/BDG/2026/VII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	2026-07-12	2026-07-15	Received	OVK siklus SKL/BDG/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.759934+07	\N	2026-10-04 18:30:14.746361+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.220529+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-c7b6-76e6-8710-e2ea3f456d89	PO/BDG/2026/VII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-07-12	2026-07-15	Received	DOC siklus SKL/BDG/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:14.674228+07	\N	2026-10-04 18:30:14.615154+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:15.410283+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-ea88-75de-a783-288fe4369f7a	PO/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-08-19	2026-08-22	Received	DOC siklus SKL/CJR/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:23.510955+07	\N	2026-10-04 18:30:23.496673+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.81891+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-028f-7000-addf-42d5fb63efd9	PO/BDG/2026/IX/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	2026-09-09	2026-09-12	Received	OVK siklus SKL/BDG/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.658099+07	\N	2026-10-04 18:30:29.647233+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.05342+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d099-7926-9854-587dcf0ccdec	PO/BDG/2026/VII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	2026-07-22	2026-07-25	Received	Pakan siklus SKL/BDG/2026/VII/0002	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:16.869939+07	\N	2026-10-04 18:30:16.8578+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:16.984286+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d0bb-7a33-9e48-09d160670a90	PO/BDG/2026/VII/0006	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	2026-07-22	2026-07-25	Received	OVK siklus SKL/BDG/2026/VII/0002	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:16.903473+07	\N	2026-10-04 18:30:16.891664+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.047763+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d074-7321-9aa3-c7bf0d824568	PO/BDG/2026/VII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-07-22	2026-07-25	Received	DOC siklus SKL/BDG/2026/VII/0002	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:16.833098+07	\N	2026-10-04 18:30:16.821355+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.305544+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0254-741a-8076-a451dd9c06ea	PO/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-09-09	2026-09-12	Received	DOC siklus SKL/BDG/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:29.598507+07	\N	2026-10-04 18:30:29.588982+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:30.400549+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d36b-7ff1-8a59-90fecddb2138	PO/CJR/2026/VII/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-07-26	2026-07-29	Received	Pakan siklus SKL/CJR/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.59166+07	\N	2026-10-04 18:30:17.579789+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.089976+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d38e-7e14-a5ea-e2b6d7da859c	PO/CJR/2026/VII/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	2026-07-26	2026-07-29	Received	OVK siklus SKL/CJR/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.628914+07	\N	2026-10-04 18:30:17.615216+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.160152+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d349-7a6c-b9fb-cad4ed3bbd16	PO/CJR/2026/VII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-07-26	2026-07-29	Received	DOC siklus SKL/CJR/2026/VII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:17.558145+07	\N	2026-10-04 18:30:17.54609+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:18.450503+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f43c-7c60-8ee7-d72ea304cd19	PO/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-08-26	2026-08-29	Received	Pakan siklus SKL/BDG/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.990351+07	\N	2026-10-04 18:30:25.980539+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.412805+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f458-7c58-8cbf-94cd8e8f6530	PO/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	2026-08-26	2026-08-29	Received	OVK siklus SKL/BDG/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:26.020128+07	\N	2026-10-04 18:30:26.008521+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.474678+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f422-79ea-93d5-1409a717f0aa	PO/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-08-26	2026-08-29	Received	DOC siklus SKL/BDG/2026/VIII/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.964266+07	\N	2026-10-04 18:30:25.954444+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.923213+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-17ae-7df4-8664-2a0e8d2c9877	PO/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-10-03	2026-10-06	Approved	DOC siklus SKL/BDG/2026/X/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:35.063201+07	\N	2026-10-04 18:30:35.055033+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.063212+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	{}
01a106ae-17c6-734c-b815-1496cd1d2fe3	PO/BDG/2026/X/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-10-03	2026-10-06	Approved	Pakan siklus SKL/BDG/2026/X/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:35.08761+07	\N	2026-10-04 18:30:35.078624+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.087621+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	{}
01a106ae-17e0-7387-9263-9f7d619577b7	PO/BDG/2026/X/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be8d-7ecd-a218-155f48892806	2026-10-03	2026-10-06	Approved	OVK siklus SKL/BDG/2026/X/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:35.113557+07	\N	2026-10-04 18:30:35.104458+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.113569+07	01a106ad-b782-7dd8-9680-2dd57b9573c4	{}
01a106ae-0962-7ac7-b614-41ab07d1f304	PO/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-09-18	2026-09-21	Received	Pakan siklus SKL/CJR/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.404099+07	\N	2026-10-04 18:30:31.39471+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.797589+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-097f-777c-9170-865377bfb9c2	PO/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be8d-7ecd-a218-155f48892806	2026-09-18	2026-09-21	Received	OVK siklus SKL/CJR/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.434288+07	\N	2026-10-04 18:30:31.424008+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:31.851954+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0947-7c24-b029-dd550944e800	PO/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-be40-7cd9-91c5-8d2f6d22faa9	2026-09-18	2026-09-21	Received	DOC siklus SKL/CJR/2026/IX/0001	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:31.376834+07	\N	2026-10-04 18:30:31.367639+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:32.144672+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1a83-7450-beaa-1665838fec1a	PO/BDG/2026/X/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-be80-70ef-bc7b-798de012dccf	2026-10-03	2026-10-09	Draft	Stok pakan gudang induk (draft)	\N	\N	\N	2026-10-04 18:30:35.78004+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: daily_recording_revisions; Type: TABLE DATA; Schema: production; Owner: postgres
--

COPY production.daily_recording_revisions (daily_recording_id, revision_number, reason, previous_values, revised_by, revised_at_utc, documents) FROM stdin;
01a106ad-d229-72b8-a0cb-13574523872a	1	Koreksi PPL: 3 ekor mati di sudut kandang belum tercatat	{"notes": null, "usages": [{"uomId": "01a0f50c-aae9-7688-bfe5-92fdb4b95fff", "itemId": "01a106ad-bd76-76e3-9971-2ce1aae02eb4", "quantity": 255.5000, "baseQuantity": 255.5000}, {"uomId": "01a0f50c-aaea-7c81-a226-2ef043004aee", "itemId": "01a106ad-bdb8-7c78-af20-a4a6f3355778", "quantity": 3.0000, "baseQuantity": 3.0000}], "culling": 0, "mortality": 3, "averageBodyWeightGram": 209.00}	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.840452+07	{}
\.


--
-- Data for Name: daily_recording_usages; Type: TABLE DATA; Schema: production; Owner: postgres
--

COPY production.daily_recording_usages (daily_recording_id, item_id, uom_id, quantity, base_quantity, value) FROM stdin;
01a106ad-cc5e-7fa7-90b9-50b3cfd71995	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	80.4000	80.4000	655260.00
01a106ad-ccce-7f34-a0ee-cfecd67d2f84	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	100.2000	100.2000	816630.00
01a106ad-ccce-7f34-a0ee-cfecd67d2f84	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-ce50-7f61-91ce-5a67a6545d4f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	120.0000	120.0000	978000.00
01a106ad-cf36-79b5-8c8f-4c13577bfec5	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	139.6000	139.6000	1137740.00
01a106ad-cf36-79b5-8c8f-4c13577bfec5	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-cf36-79b5-8c8f-4c13577bfec5	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d00d-7069-8e1a-4653e2c1b488	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	158.9000	158.9000	1295035.00
01a106ad-d027-7dc3-9716-afd16516d48c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	178.4000	178.4000	1453960.00
01a106ad-d027-7dc3-9716-afd16516d48c	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d0cb-72a0-9381-697ced243e40	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	197.4000	197.4000	1608810.00
01a106ad-d0e3-771a-9f12-829d30bfb43f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	216.5000	216.5000	1764475.00
01a106ad-d0e3-771a-9f12-829d30bfb43f	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d210-7471-89e2-885621735f5a	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	236.1000	236.1000	1924215.00
01a106ad-d229-72b8-a0cb-13574523872a	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	255.5000	255.5000	2082325.00
01a106ad-d229-72b8-a0cb-13574523872a	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d49f-779f-b620-5eb282c8da5a	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	274.8000	274.8000	2239620.00
01a106ad-d4bf-7b1f-aeba-04d9e885f75d	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	80.2000	80.2000	649620.00
01a106ad-d517-7d09-ae16-0720df9f4314	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	294.3000	294.3000	2398545.00
01a106ad-d517-7d09-ae16-0720df9f4314	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	560000.00
01a106ad-d534-7171-b77e-3df14dc3dd74	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	99.8000	99.8000	808380.00
01a106ad-d534-7171-b77e-3df14dc3dd74	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d5db-7b92-96c1-5d808cfd6b93	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	313.5000	313.5000	2555025.00
01a106ad-d668-7bd2-ba2f-19feb1499afd	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	119.4000	119.4000	967140.00
01a106ad-d681-753f-aa82-920dbbe0e044	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	332.8000	332.8000	2712320.00
01a106ad-d69b-79ef-9de6-106dbe430481	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	138.5000	138.5000	1121850.00
01a106ad-d69b-79ef-9de6-106dbe430481	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-d69b-79ef-9de6-106dbe430481	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d7dc-793b-a59f-7494399fdf3c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	352.2000	352.2000	2773899.61
01a106ad-d89a-7ccb-93bc-e0c4f3eb6802	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	157.8000	157.8000	1278180.00
01a106ad-d8b3-7a82-99b1-107355891e58	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	92.6000	92.6000	754690.00
01a106ad-d93b-7666-b912-3aa7ef54a2f7	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	371.3000	371.3000	2924329.71
01a106ad-d960-7bc9-86c4-568869e29b05	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	176.8000	176.8000	1432080.00
01a106ad-d960-7bc9-86c4-568869e29b05	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d97d-7253-a71c-fccdbc2b6b1d	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	114.9000	114.9000	936435.00
01a106ad-d97d-7253-a71c-fccdbc2b6b1d	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-d99a-7e7b-854e-21c0e67e5a59	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	390.4000	390.4000	3074759.82
01a106ad-d9b4-7094-a2dc-0318f02069f9	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	195.5000	195.5000	1583550.00
01a106ad-da1a-71a5-be59-96a512736a24	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	137.4000	137.4000	1119810.00
01a106ad-dab6-75b9-ba82-58926ae9de49	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	409.5000	409.5000	3225189.92
01a106ad-dab6-75b9-ba82-58926ae9de49	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-dae0-7c16-b9c7-e90c1053e113	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	214.5000	214.5000	1737450.00
01a106ad-dae0-7c16-b9c7-e90c1053e113	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-daff-7358-b859-682e3fe7ac5c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	159.2000	159.2000	1297480.00
01a106ad-daff-7358-b859-682e3fe7ac5c	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-daff-7358-b859-682e3fe7ac5c	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-db1e-72cc-9abe-d8e672245066	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	428.8000	428.8000	3377195.21
01a106ad-db37-7a15-bf21-c18e8dcb0718	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	233.8000	233.8000	1893780.00
01a106ad-dbe7-7177-aac8-6decd4265310	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	180.9000	180.9000	1474335.00
01a106ad-ddaa-793c-a1dd-3c8cc15e60eb	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	448.1000	448.1000	3529200.49
01a106ad-ddc2-7cc4-b0e4-774809cba35b	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	253.1000	253.1000	2050110.00
01a106ad-ddc2-7cc4-b0e4-774809cba35b	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-dde2-718d-a538-f59490e33d84	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	202.3000	202.3000	1648745.00
01a106ad-dde2-718d-a538-f59490e33d84	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-ddfd-7461-9b07-1e06cbfdb108	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	467.4000	467.4000	3681205.78
01a106ad-de16-7ea8-95b8-0fe918dd6b9c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	272.2000	272.2000	2204820.00
01a106ad-de2f-70ec-b894-ec4a5db80950	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	224.1000	224.1000	1826415.00
01a106ad-de49-7b55-9483-061ba2ed0676	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	486.4000	486.4000	3830848.29
01a106ad-de9d-7045-bae7-5ff5c4901e79	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	291.3000	291.3000	2359530.00
01a106ad-de9d-7045-bae7-5ff5c4901e79	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	560000.00
01a106ad-deb7-7b1b-84dd-c55f9a2e5486	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	245.7000	245.7000	2002455.00
01a106ad-deb7-7b1b-84dd-c55f9a2e5486	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-ded3-7406-af79-a4c2522ec215	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	505.6000	505.6000	3982065.99
01a106ad-deec-7768-8789-357dffb98706	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	310.6000	310.6000	2515860.00
01a106ad-df03-722c-87f7-1998f097527d	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	267.6000	267.6000	2180940.00
01a106ad-df1a-7883-9ff4-b0e5088b610f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	524.0000	524.0000	4126982.95
01a106ad-df30-7706-b1cc-d066ae3d2ea6	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	329.8000	329.8000	2671380.00
01a106ad-df48-7f6d-8c4e-ed4151ca4050	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	289.5000	289.5000	2359425.00
01a106ad-df48-7f6d-8c4e-ed4151ca4050	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-e089-7a12-aea8-7f44cc0d14c4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	542.6000	542.6000	4273475.09
01a106ad-e0a9-739d-a7d5-7ffdf7944ed8	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	348.9000	348.9000	2747909.07
01a106ad-e0c1-7c9f-b9d6-0b398cc41cf2	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	311.2000	311.2000	2536280.00
01a106ad-e0ee-7d65-845c-fd49fb8c5aee	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	561.3000	561.3000	4420754.83
01a106ad-e106-71a3-9216-7abf48ba6b83	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	368.0000	368.0000	2898339.17
01a106ad-e156-7fe8-a5d0-13f30df81435	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	333.0000	333.0000	2713950.00
01a106ad-e156-7fe8-a5d0-13f30df81435	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	560000.00
01a106ad-e170-7fea-bb81-240761e9556b	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	579.9000	579.9000	4567246.97
01a106ad-e186-7c90-9e1a-ebc0165bfbf3	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	387.2000	387.2000	3049556.87
01a106ad-e19c-7a9b-827a-5ed1103ae12d	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	354.8000	354.8000	2891620.00
01a106ad-e1b4-7f1e-9560-274181fe5d35	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	597.9000	597.9000	4709013.56
01a106ad-e1cc-7eb2-b0e9-13d64e1f9436	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	406.2000	406.2000	3199199.38
01a106ad-e1cc-7eb2-b0e9-13d64e1f9436	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-e20d-7232-94bc-fcc00d40190d	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	616.4000	616.4000	4854718.11
01a106ad-e23b-7dbe-a0a4-91826cf607c3	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	398.0000	398.0000	3144200.00
01a106ad-e40c-73c3-b6b5-04b13e2ea61d	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	444.2000	444.2000	3498484.40
01a106ad-e53f-760c-9de6-c4c97b670251	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	652.7000	652.7000	5140614.07
01a106ad-e56d-7a82-87bc-a60b421b2dcc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	441.2000	441.2000	3485480.00
01a106ad-e646-7b54-aeb1-696da1189bb7	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	482.1000	482.1000	3796981.83
01a106ad-e662-7e9b-85f2-527be0a0c88c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	462.7000	462.7000	3655330.00
01a106ad-e662-7e9b-85f2-527be0a0c88c	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	5.0000	5.0000	475000.00
01a106ad-e782-7b39-87ef-cf0b3cf3fcbe	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	500.7000	500.7000	3943473.98
01a106ad-e7c3-7674-a271-ed7cfc16e36a	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	471.7000	471.7000	3715072.25
01a106ad-e970-70d4-b497-f263237ef95c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	519.3000	519.3000	4089966.12
01a106ad-eb9d-7ae2-b878-5808d3e51052	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	526.7000	526.7000	4160930.00
01a106ad-ec8c-711c-a16d-b89bca847c32	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	548.0000	548.0000	4329200.00
01a106ad-ef15-7d1f-94b2-f9150c6240ee	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	574.7000	574.7000	4526292.18
01a106ad-ef74-730a-a234-b1934d1010fa	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	593.0000	593.0000	4670421.54
01a106ad-f232-7573-8599-5c09641649d8	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	610.4000	610.4000	4822160.00
01a106ad-f306-7f5f-86ce-626ab2cdf147	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	629.6000	629.6000	4958680.28
01a106ad-f3e2-7d84-8967-62c819b50a45	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	647.8000	647.8000	5102022.05
01a106ad-f4d4-7ace-a351-6b1924b17cbd	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	180.2000	180.2000	1459620.00
01a106ad-f532-7382-a010-a2640ea58141	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	671.7000	671.7000	5306430.00
01a106ad-f56d-7fcf-afdf-c0d3f34ddc19	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	342.1000	342.1000	2694352.80
01a106ad-f6f4-757b-8841-bd32218bf1a7	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	238.9000	238.9000	1935090.00
01a106ad-f89b-7a6d-b469-a34685d639d1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	297.3000	297.3000	2408130.00
01a106ad-fb17-72d4-95e8-2ae27e0b4db8	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	753.5000	753.5000	5952650.00
01a106ad-fb31-75b1-ad43-630959b993b1	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	325.5000	325.5000	2636550.00
01a106ad-fb31-75b1-ad43-630959b993b1	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ad-fb90-7002-89c5-5a70073c5a9f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	126.7000	126.7000	1032511.94
01a106ad-fb90-7002-89c5-5a70073c5a9f	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-fbd1-7df8-be16-f9b104681e11	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	354.7000	354.7000	2873070.00
01a106ad-fc7f-765a-9179-ca596c6ec270	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	151.5000	151.5000	1234613.73
01a106ad-fce5-7862-abc0-cd4a846194fc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	176.2000	176.2000	1435900.58
01a106ad-fce5-7862-abc0-cd4a846194fc	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	6.0000	570000.00
01a106ad-fce5-7862-abc0-cd4a846194fc	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-fe42-7bd8-97e1-d6e9352b6ba9	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	200.7000	200.7000	1635557.59
01a106ad-ff8d-78d2-b1a6-c0063ec87f49	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	471.3000	471.3000	3817530.00
01a106ae-005f-7097-a4cb-5815027dc7fd	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	500.3000	500.3000	4052430.00
01a106ae-010f-7aa1-9509-6d09c5f5b4fc	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	273.2000	273.2000	2226379.34
01a106ae-010f-7aa1-9509-6d09c5f5b4fc	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ae-0170-7ed9-88d0-573ae9d49cff	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	297.9000	297.9000	2427666.20
01a106ae-032c-700a-a492-30a350f591a8	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	347.0000	347.0000	2827795.13
01a106ae-03ac-79dd-b23e-b3d746fa5b3f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	371.3000	371.3000	3025822.29
01a106ae-03ac-79dd-b23e-b3d746fa5b3f	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	6.0000	672000.00
01a106ae-0448-7c6a-b799-54205ec2ae35	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	644.7000	644.7000	5060895.00
01a106ae-0548-775f-a6f5-56e4863fa3b2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	673.1000	673.1000	5283835.00
01a106ae-0686-776f-aa77-3f6ec812b6da	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	444.6000	444.6000	3511530.50
01a106ae-06b1-7ae2-bb62-82bb05d20a3f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	730.0000	730.0000	5730500.00
01a106ae-0779-7971-9c4d-87a2fdd6ad0c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	758.0000	758.0000	5950300.00
01a106ae-07e8-70aa-8483-dde7e16f845e	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	205.9000	205.9000	1667948.87
01a106ae-082a-70a0-bfb4-ff75d29c680d	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	239.4000	239.4000	1939324.71
01a106ae-082a-70a0-bfb4-ff75d29c680d	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	8.0000	760000.00
01a106ae-082a-70a0-bfb4-ff75d29c680d	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ae-085c-712f-a271-039f95ca3b88	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	542.1000	542.1000	4281602.98
01a106ae-090f-76fd-a291-b6b532ffc767	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	843.1000	843.1000	6618335.00
01a106ae-0a8a-7c76-a7f2-005fe396f345	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	871.6000	871.6000	6842060.00
01a106ae-0ab4-7a29-8119-c4f4268fd80f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	338.7000	338.7000	2743731.33
01a106ae-0bed-7628-9f59-9dbe1f312708	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	926.9000	926.9000	7276165.00
01a106ae-0c16-7f39-9ed0-06a7f0e90700	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	404.6000	404.6000	3277572.17
01a106ae-0d23-7c75-823e-6741f4848f1a	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	983.1000	983.1000	7717335.00
01a106ae-0d4f-7f7c-be06-6a1a4ce3d083	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	684.7000	684.7000	5407883.34
01a106ae-0d7d-7469-bc0e-c08e85d36fbb	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1011.1000	1011.1000	7937135.00
01a106ae-0d91-789b-aad9-9048c5bf8abb	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	75.3000	75.3000	613695.00
01a106ae-0d91-789b-aad9-9048c5bf8abb	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	2.0000	2.0000	84000.00
01a106ae-0e68-70ab-bacf-a1ba945c4e10	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	504.3000	504.3000	4085219.10
01a106ae-0e68-70ab-bacf-a1ba945c4e10	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	8.0000	896000.00
01a106ae-0ece-76de-b34b-aa355877e294	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1039.1000	1039.1000	8156935.00
01a106ae-0f37-718b-9dbf-bd3c51be9432	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	731.9000	731.9000	5780677.41
01a106ae-0f64-7244-9653-6c258be05f86	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1067.0000	1067.0000	8375950.00
01a106ae-0f94-7973-b72a-a1ee14ccfa40	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	755.4000	755.4000	5966284.62
01a106ae-0fc3-7696-8265-5a185d22ba80	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	1094.0000	1094.0000	8587900.00
01a106ae-1114-78be-b8d0-6cca6cbc5c44	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	779.2000	779.2000	6154261.28
01a106ae-113d-78a3-aa8a-7d1fed49212a	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	747.4000	747.4000	5867090.00
01a106ae-1228-76cc-b1a8-febe704cf404	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	133.5000	133.5000	1088025.00
01a106ae-1228-76cc-b1a8-febe704cf404	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	2.0000	2.0000	84000.00
01a106ae-1250-7e96-8716-ed7c86794e39	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	637.1000	637.1000	5002057.21
01a106ae-138d-729d-b5c9-b2e50c465273	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	826.4000	826.4000	6527055.35
01a106ae-1462-76d1-a222-4ed44499544e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	849.7000	849.7000	6711082.92
01a106ae-1479-7cc5-90f2-0608efd0ff02	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	703.0000	703.0000	5519457.26
01a106ae-1479-7cc5-90f2-0608efd0ff02	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	8.0000	8.0000	760000.00
01a106ae-154e-7794-b56c-34eee5209750	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	873.0000	873.0000	6895110.50
01a106ae-160b-7082-b0ed-65317a471bed	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	672.6000	672.6000	5312315.37
01a106ad-e1eb-7783-ba8c-2f9734451ab4	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	376.3000	376.3000	3066845.00
01a106ad-e224-78c6-a952-812cec87f27c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	425.4000	425.4000	3350417.07
01a106ad-e2f2-7cc5-9ec1-12b227be53fc	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	634.4000	634.4000	4996484.70
01a106ad-e425-79d1-8235-5ab8ed4b57a6	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	419.5000	419.5000	3314050.00
01a106ad-e556-7750-af63-982615c773a3	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	463.3000	463.3000	3648914.50
01a106ad-e62b-7087-b227-8793ecf528fb	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	671.2000	671.2000	5286318.62
01a106ad-e683-7cf3-a6bd-7cad8559e7f7	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	689.5000	689.5000	5430447.98
01a106ad-e7a3-71cd-b902-0decbcda1f55	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	483.9000	483.9000	3822810.00
01a106ad-ea33-7b0c-b2b0-a3259ecd9ea0	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	505.6000	505.6000	3994240.00
01a106ad-eae3-7142-bb16-27832515c562	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	241.8000	241.8000	1904397.86
01a106ad-eb84-718e-954b-eea4a9013bc0	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	537.7000	537.7000	4234883.08
01a106ad-ec76-7ec6-9643-136d98207d00	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	556.1000	556.1000	4379800.03
01a106ad-ef2c-7dc3-9401-0b50b567af2d	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	568.7000	568.7000	4492730.00
01a106ad-ef8a-762f-b87b-4dfa0cc11dfa	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	589.3000	589.3000	4655470.00
01a106ad-f21d-729c-aed3-eb1298a6ae34	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	611.3000	611.3000	4814550.91
01a106ad-f249-7711-bf44-ca51c35a89d5	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	121.1000	121.1000	980910.00
01a106ad-f31c-7d26-a64f-c2ee00ca8c01	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	631.1000	631.1000	4985690.00
01a106ad-f334-7b2b-9d13-3a1c9e1852c7	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	150.6000	150.6000	1219860.00
01a106ad-f334-7b2b-9d13-3a1c9e1852c7	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ad-f467-7e62-b402-597ceab70307	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	651.2000	651.2000	5144480.00
01a106ad-f4f6-7e81-a33a-0bcbb7962ab5	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	666.4000	666.4000	5248514.19
01a106ad-f54e-7895-b6b7-43aa74cdb435	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	209.4000	209.4000	1696140.00
01a106ad-f54e-7895-b6b7-43aa74cdb435	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	7.0000	665000.00
01a106ad-f54e-7895-b6b7-43aa74cdb435	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ad-f650-75c7-9fba-80ac5ac0a46f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	692.2000	692.2000	5468380.00
01a106ad-f709-7db8-aca5-9deaf815b5f3	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	350.4000	350.4000	2759722.95
01a106ad-f749-7f43-9f65-0d6bbdfebc35	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	712.4000	712.4000	5627960.00
01a106ad-f75f-7fef-9bb9-77c088b6c641	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	268.1000	268.1000	2171610.00
01a106ad-f75f-7fef-9bb9-77c088b6c641	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ad-f884-7c25-b2e0-734cf1afa507	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	733.2000	733.2000	5792280.00
01a106ad-fab0-7a61-b55c-ecf6a30f1748	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	101.7000	101.7000	828780.30
01a106ad-fbb7-70e1-a993-e0a2516e5837	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	773.5000	773.5000	6110650.00
01a106ad-fc95-7669-9952-a41217db083d	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	793.6000	793.6000	6269440.00
01a106ad-fccb-7a7f-ae91-dd67ac698fe8	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	383.8000	383.8000	3108780.00
01a106ad-fccb-7a7f-ae91-dd67ac698fe8	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ad-fd00-7887-b6a2-16b1538dc55c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	541.8000	541.8000	4280220.00
01a106ad-fd78-7862-aeca-710b6952e364	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	413.0000	413.0000	3345300.00
01a106ad-fe5b-7f78-8851-e48768b1eaa2	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	277.6000	277.6000	2193040.00
01a106ad-ff1a-723a-bd71-f4a0c33f6f7b	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	442.2000	442.2000	3581820.00
01a106ad-ff1a-723a-bd71-f4a0c33f6f7b	01a106ad-bdab-762c-ad3a-cd0613d5d1df	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	7.0000	784000.00
01a106ad-ff34-7ae4-834e-f02e7b6ebd31	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	225.4000	225.4000	1836844.45
01a106ad-ff34-7ae4-834e-f02e7b6ebd31	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ad-ffa0-73f8-a95e-0e84bd106901	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	249.4000	249.4000	2032426.82
01a106ae-015a-794c-9313-0df96d4c3d43	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	529.2000	529.2000	4154220.00
01a106ae-0204-78c2-96bb-80d88307bca4	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	558.2000	558.2000	4381870.00
01a106ae-021b-73a8-92ee-9e9f2e435605	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	322.5000	322.5000	2628138.13
01a106ae-021b-73a8-92ee-9e9f2e435605	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	3.0000	3.0000	126000.00
01a106ae-029d-76e2-aba8-c69e90634772	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	587.1000	587.1000	4608735.00
01a106ae-0361-767e-b8dd-182694843147	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	615.8000	615.8000	4834030.00
01a106ae-0361-767e-b8dd-182694843147	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	7.0000	7.0000	665000.00
01a106ae-045e-7bf5-9832-d2d0ed28357a	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	395.7000	395.7000	3224664.36
01a106ae-055c-7886-b9b5-51dc49315d90	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	420.2000	420.2000	3424321.37
01a106ae-05fe-721b-9d53-76ef30ada2e8	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	701.3000	701.3000	5505205.00
01a106ae-069d-7dd8-ba89-7e3a7e50e393	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	138.4000	138.4000	1121146.78
01a106ae-074d-7370-91e2-14c452cfc5f0	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	469.0000	469.0000	3704246.08
01a106ae-0762-7099-98f5-0b0394a98fd6	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	172.3000	172.3000	1395762.94
01a106ae-0762-7099-98f5-0b0394a98fd6	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ae-078f-72a8-9693-b4fd659b10da	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	493.4000	493.4000	3896961.65
01a106ae-07fd-7667-8034-27b86e55460e	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	786.7000	786.7000	6175595.00
01a106ae-0813-70d6-919f-2373ea0ba65b	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	517.7000	517.7000	4088887.41
01a106ae-0813-70d6-919f-2373ea0ba65b	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	6.0000	6.0000	570000.00
01a106ae-0847-7969-8acb-4e9c2655c245	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	814.7000	814.7000	6395395.00
01a106ae-08fa-7281-ad5c-b5e0dc176cce	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	272.4000	272.4000	2206650.17
01a106ae-0a5d-7ea3-a3f4-e3e3fd08ab4d	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	566.2000	566.2000	4471949.10
01a106ae-0a71-7407-a255-d2804cc8322c	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	305.4000	305.4000	2473975.64
01a106ae-0a71-7407-a255-d2804cc8322c	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ae-0a9f-7ee6-8e45-5e1b57981e9f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	590.3000	590.3000	4662295.22
01a106ae-0acb-7dca-8e73-de3068249d61	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	899.0000	899.0000	7057150.00
01a106ae-0bc4-733f-98ed-a70ec3f5661c	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	613.9000	613.9000	4848692.25
01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	371.1000	371.1000	3006196.33
01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ae-0c01-7d34-92e3-55ed42fc7d93	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	637.6000	637.6000	5035879.10
01a106ae-0c29-7f35-8143-a6df282f87cd	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	955.0000	955.0000	7496750.00
01a106ae-0ce6-7ed3-815d-d78acb8beb5a	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	661.1000	661.1000	5221486.31
01a106ae-0d08-7312-8292-d25b316d3500	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	437.8000	437.8000	3546517.79
01a106ae-0d08-7312-8292-d25b316d3500	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	4.0000	4.0000	168000.00
01a106ae-0d39-7f71-84bf-4f1de24ab449	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	60.6000	60.6000	493890.00
01a106ae-0d66-7662-8183-b4e125081c37	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	471.1000	471.1000	3816273.48
01a106ae-0e21-7392-ac64-84e99d638855	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	708.1000	708.1000	5592700.74
01a106ae-0f1f-703b-9d0a-2464808cf5d8	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	90.0000	90.0000	733500.00
01a106ae-0f4e-7a58-9227-731f2a961ad2	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	537.6000	537.6000	4354974.79
01a106ae-0f7b-7665-8204-60f3af0c5e15	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	104.6000	104.6000	852490.00
01a106ae-0f7b-7665-8204-60f3af0c5e15	01a106ad-bd9f-7179-9506-e6071b528b74	01a0f50c-aaea-7a32-8492-6efb11e274c5	4.0000	4.0000	380000.00
01a106ae-0f7b-7665-8204-60f3af0c5e15	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	2.0000	2.0000	84000.00
01a106ae-0fab-7019-82ca-0e75f9cb1722	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	570.9000	570.9000	4624730.49
01a106ae-108c-76d1-be62-2df1049ed550	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	119.2000	119.2000	971480.00
01a106ae-1129-7ebc-9afa-8d83e87d51ff	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	604.1000	604.1000	4742964.62
01a106ae-123c-772e-bc9c-5f2206abc9a7	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	803.0000	803.0000	6342237.95
01a106ae-1263-7f2f-bbea-b566851d80e8	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	382.9000	382.9000	3005765.00
01a106ae-12ff-74c7-a6c7-edbba1405eb5	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	148.0000	148.0000	1206200.00
01a106ae-13a5-7ae1-8065-056bbe4af2e3	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	670.3000	670.3000	5262720.06
01a106ae-1401-7239-9838-ed8e5556b525	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	162.1000	162.1000	1321115.00
01a106ae-1401-7239-9838-ed8e5556b525	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	2.0000	2.0000	84000.00
01a106ae-1537-7acb-8120-b3e4a1c141b7	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	176.6000	176.6000	1439290.00
01a106ae-1582-7a1f-8b9d-61fc64e31cc9	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	736.1000	736.1000	5779334.98
01a106ae-15ef-767d-bad0-a41642b167b5	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	191.1000	191.1000	1557465.00
01a106ae-15ef-767d-bad0-a41642b167b5	01a106ad-bdb8-7c78-af20-a4a6f3355778	01a0f50c-aaea-7c81-a226-2ef043004aee	2.0000	2.0000	84000.00
01a106ae-1813-771c-984d-2678c0084126	01a106ad-bd76-76e3-9971-2ce1aae02eb4	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	205.7000	205.7000	1676455.00
01a106ae-1772-7905-8e52-59d851299e1f	01a106ad-bd91-75b9-8797-63a66fb4ba5d	01a0f50c-aae9-7688-bfe5-92fdb4b95fff	769.0000	769.0000	6037642.44
\.


--
-- Data for Name: daily_recordings; Type: TABLE DATA; Schema: production; Owner: postgres
--

COPY production.daily_recordings (id, cycle_id, branch_id, coop_id, date, age_days, mortality, culling, average_body_weight_gram, notes, revision_number, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-cc5e-7fa7-90b9-50b3cfd71995	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-17	1	12	0	44.00	\N	0	2026-10-04 18:30:15.856682+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ccce-7f34-a0ee-cfecd67d2f84	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-18	2	8	0	49.00	\N	0	2026-10-04 18:30:15.91041+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ce50-7f61-91ce-5a67a6545d4f	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-19	3	9	0	58.00	\N	0	2026-10-04 18:30:16.291577+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-cf36-79b5-8c8f-4c13577bfec5	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-20	4	18	0	69.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:16.522618+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d00d-7069-8e1a-4653e2c1b488	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-21	5	8	0	83.00	\N	0	2026-10-04 18:30:16.736329+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d027-7dc3-9716-afd16516d48c	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-22	6	20	0	102.00	\N	0	2026-10-04 18:30:16.763271+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d0cb-72a0-9381-697ced243e40	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-23	7	13	0	122.00	\N	0	2026-10-04 18:30:16.923634+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d0e3-771a-9f12-829d30bfb43f	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-24	8	2	0	147.00	\N	0	2026-10-04 18:30:16.951158+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d210-7471-89e2-885621735f5a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-25	9	4	0	173.00	\N	0	2026-10-04 18:30:17.250331+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d229-72b8-a0cb-13574523872a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-26	10	6	0	209.00	\N	1	2026-10-04 18:30:17.275916+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:17.855397+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-d49f-779f-b620-5eb282c8da5a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-27	11	2	0	244.00	\N	0	2026-10-04 18:30:17.903571+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d4bf-7b1f-aeba-04d9e885f75d	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-07-27	1	14	0	43.00	\N	0	2026-10-04 18:30:17.936961+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d517-7d09-ae16-0720df9f4314	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-28	12	2	3	279.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:18.027408+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d534-7171-b77e-3df14dc3dd74	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-07-28	2	12	0	48.00	\N	0	2026-10-04 18:30:18.057392+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d5db-7b92-96c1-5d808cfd6b93	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-29	13	4	0	322.00	\N	0	2026-10-04 18:30:18.221316+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d668-7bd2-ba2f-19feb1499afd	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-07-29	3	22	0	56.00	\N	0	2026-10-04 18:30:18.3611+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d681-753f-aa82-920dbbe0e044	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-30	14	3	0	367.00	\N	0	2026-10-04 18:30:18.388528+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d69b-79ef-9de6-106dbe430481	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-07-30	4	10	0	67.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:18.41949+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d7dc-793b-a59f-7494399fdf3c	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-07-31	15	5	0	414.00	\N	0	2026-10-04 18:30:18.735643+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d89a-7ccb-93bc-e0c4f3eb6802	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-07-31	5	17	0	81.00	\N	0	2026-10-04 18:30:18.924229+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d8b3-7a82-99b1-107355891e58	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-07-31	1	28	0	43.00	\N	0	2026-10-04 18:30:18.949012+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d93b-7666-b912-3aa7ef54a2f7	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-01	16	5	0	455.00	\N	0	2026-10-04 18:30:19.095152+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d960-7bc9-86c4-568869e29b05	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-01	6	22	0	99.00	\N	0	2026-10-04 18:30:19.124786+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d97d-7253-a71c-fccdbc2b6b1d	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-01	2	13	0	47.00	\N	0	2026-10-04 18:30:19.153754+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d99a-7e7b-854e-21c0e67e5a59	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-02	17	5	0	517.00	\N	0	2026-10-04 18:30:19.180245+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-d9b4-7094-a2dc-0318f02069f9	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-02	7	10	0	120.00	\N	0	2026-10-04 18:30:19.205267+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-da1a-71a5-be59-96a512736a24	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-02	3	32	0	55.00	\N	0	2026-10-04 18:30:19.308205+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dab6-75b9-ba82-58926ae9de49	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-03	18	2	0	580.00	Booster ND-IB	0	2026-10-04 18:30:19.466967+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dae0-7c16-b9c7-e90c1053e113	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-03	8	3	0	142.00	\N	0	2026-10-04 18:30:19.510684+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-daff-7358-b859-682e3fe7ac5c	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-03	4	27	0	66.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:19.540729+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-db1e-72cc-9abe-d8e672245066	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-04	19	2	0	626.00	\N	0	2026-10-04 18:30:19.568399+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-db37-7a15-bf21-c18e8dcb0718	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-04	9	2	0	170.00	\N	0	2026-10-04 18:30:19.594282+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dbe7-7177-aac8-6decd4265310	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-04	5	26	0	79.00	\N	0	2026-10-04 18:30:19.768469+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ddaa-793c-a1dd-3c8cc15e60eb	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-05	20	2	0	691.00	\N	0	2026-10-04 18:30:20.2184+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ddc2-7cc4-b0e4-774809cba35b	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-05	10	5	0	200.00	\N	0	2026-10-04 18:30:20.250022+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-dde2-718d-a538-f59490e33d84	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-05	6	14	0	98.00	\N	0	2026-10-04 18:30:20.277141+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ddfd-7461-9b07-1e06cbfdb108	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-06	21	4	0	766.00	\N	0	2026-10-04 18:30:20.302546+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de16-7ea8-95b8-0fe918dd6b9c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-06	11	5	0	234.00	\N	0	2026-10-04 18:30:20.327428+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de2f-70ec-b894-ec4a5db80950	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-06	7	16	0	118.00	\N	0	2026-10-04 18:30:20.353048+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de49-7b55-9483-061ba2ed0676	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-07	22	3	0	833.00	\N	0	2026-10-04 18:30:20.377728+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-de9d-7045-bae7-5ff5c4901e79	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-07	12	2	0	273.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:20.463568+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-deb7-7b1b-84dd-c55f9a2e5486	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-07	8	7	0	142.00	\N	0	2026-10-04 18:30:20.490719+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ded3-7406-af79-a4c2522ec215	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-08	23	6	3	911.00	\N	0	2026-10-04 18:30:20.516943+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-deec-7768-8789-357dffb98706	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-08	13	2	0	313.00	\N	0	2026-10-04 18:30:20.540455+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-df03-722c-87f7-1998f097527d	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-08	9	6	0	166.00	\N	0	2026-10-04 18:30:20.563747+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-df1a-7883-9ff4-b0e5088b610f	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-09	24	7	0	1000.00	\N	0	2026-10-04 18:30:20.585267+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-df30-7706-b1cc-d066ae3d2ea6	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-09	14	3	0	353.00	\N	0	2026-10-04 18:30:20.608495+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-df48-7f6d-8c4e-ed4151ca4050	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-09	10	7	0	200.00	\N	0	2026-10-04 18:30:20.635459+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e0a9-739d-a7d5-7ffdf7944ed8	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-10	15	2	1	399.00	\N	0	2026-10-04 18:30:20.985961+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e0ee-7d65-845c-fd49fb8c5aee	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-11	26	6	0	1147.00	\N	0	2026-10-04 18:30:21.055679+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e156-7fe8-a5d0-13f30df81435	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-11	12	5	0	264.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:21.160771+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e186-7c90-9e1a-ebc0165bfbf3	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-12	17	3	0	495.00	\N	0	2026-10-04 18:30:21.205979+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e1b4-7f1e-9560-274181fe5d35	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-13	28	6	0	1334.00	\N	0	2026-10-04 18:30:21.25254+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e1cc-7eb2-b0e9-13d64e1f9436	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-13	18	2	0	554.00	Booster ND-IB	0	2026-10-04 18:30:21.281987+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e20d-7232-94bc-fcc00d40190d	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-14	29	6	3	1403.00	\N	0	2026-10-04 18:30:21.341803+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e23b-7dbe-a0a4-91826cf607c3	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-14	15	7	0	398.00	\N	0	2026-10-04 18:30:21.398375+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e40c-73c3-b6b5-04b13e2ea61d	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-15	20	2	0	666.00	\N	0	2026-10-04 18:30:21.85338+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e53f-760c-9de6-c4c97b670251	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-16	31	5	0	1598.00	\N	0	2026-10-04 18:30:22.158887+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e56d-7a82-87bc-a60b421b2dcc	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-16	17	6	0	492.00	\N	0	2026-10-04 18:30:22.204161+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e646-7b54-aeb1-696da1189bb7	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-17	22	6	0	807.00	\N	0	2026-10-04 18:30:22.424922+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e662-7e9b-85f2-527be0a0c88c	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-17	18	7	0	547.00	Booster ND-IB	0	2026-10-04 18:30:22.458819+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e782-7b39-87ef-cf0b3cf3fcbe	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-18	23	5	0	873.00	\N	0	2026-10-04 18:30:22.743918+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e7c3-7674-a271-ed7cfc16e36a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-19	34	3	4	1908.00	\N	0	2026-10-04 18:30:22.810057+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e970-70d4-b497-f263237ef95c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-19	24	7	0	959.00	\N	0	2026-10-04 18:30:23.234501+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-eb9d-7ae2-b878-5808d3e51052	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-20	21	5	0	735.00	\N	0	2026-10-04 18:30:23.789362+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ec8c-711c-a16d-b89bca847c32	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-21	22	10	0	794.00	\N	0	2026-10-04 18:30:24.028593+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef15-7d1f-94b2-f9150c6240ee	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-22	27	6	0	1204.00	\N	0	2026-10-04 18:30:24.678255+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef74-730a-a234-b1934d1010fa	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-23	28	6	0	1271.00	\N	0	2026-10-04 18:30:24.771925+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f232-7573-8599-5c09641649d8	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-24	25	8	0	1014.00	\N	0	2026-10-04 18:30:25.474929+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f306-7f5f-86ce-626ab2cdf147	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-25	30	6	0	1485.00	\N	0	2026-10-04 18:30:25.685451+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f3e2-7d84-8967-62c819b50a45	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-26	31	3	0	1579.00	\N	0	2026-10-04 18:30:25.907003+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f4d4-7ace-a351-6b1924b17cbd	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-26	3	23	0	57.00	\N	0	2026-10-04 18:30:26.1549+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f532-7382-a010-a2640ea58141	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-27	28	8	0	1283.00	\N	0	2026-10-04 18:30:26.245758+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f56d-7fcf-afdf-c0d3f34ddc19	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-28	33	3	4	1747.00	\N	0	2026-10-04 18:30:26.302263+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f6f4-757b-8841-bd32218bf1a7	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-28	5	13	0	82.00	\N	0	2026-10-04 18:30:26.690697+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f89b-7a6d-b469-a34685d639d1	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-30	7	30	0	123.00	\N	0	2026-10-04 18:30:27.115346+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fb17-72d4-95e8-2ae27e0b4db8	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-31	32	9	0	1637.00	\N	0	2026-10-04 18:30:27.753776+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fb31-75b1-ad43-630959b993b1	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-31	8	6	0	146.00	\N	0	2026-10-04 18:30:27.779465+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fb90-7002-89c5-5a70073c5a9f	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-01	2	15	0	49.00	\N	0	2026-10-04 18:30:27.8774+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fbd1-7df8-be16-f9b104681e11	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-01	9	7	0	174.00	\N	0	2026-10-04 18:30:27.938883+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fc7f-765a-9179-ca596c6ec270	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-02	3	18	0	59.00	\N	0	2026-10-04 18:30:28.111792+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fce5-7862-abc0-cd4a846194fc	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-03	4	16	0	70.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:28.21761+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fe42-7bd8-97e1-d6e9352b6ba9	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-04	5	9	0	84.00	\N	0	2026-10-04 18:30:28.5641+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ff8d-78d2-b1a6-c0063ec87f49	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-05	13	5	0	320.00	\N	0	2026-10-04 18:30:28.890837+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-005f-7097-a4cb-5815027dc7fd	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-06	14	6	0	361.00	\N	0	2026-10-04 18:30:29.101689+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-010f-7aa1-9509-6d09c5f5b4fc	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-07	8	3	0	147.00	\N	0	2026-10-04 18:30:29.283185+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0170-7ed9-88d0-573ae9d49cff	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-08	9	3	0	179.00	\N	0	2026-10-04 18:30:29.376821+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-032c-700a-a492-30a350f591a8	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-10	11	5	2	241.00	\N	0	2026-10-04 18:30:29.820699+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-03ac-79dd-b23e-b3d746fa5b3f	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-11	12	2	3	277.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:29.950262+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0448-7c6a-b799-54205ec2ae35	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-11	19	5	4	635.00	\N	0	2026-10-04 18:30:30.103387+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0548-775f-a6f5-56e4863fa3b2	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-12	20	7	3	695.00	\N	0	2026-10-04 18:30:30.358497+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0686-776f-aa77-3f6ec812b6da	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-14	15	4	0	413.00	\N	0	2026-10-04 18:30:30.678454+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e089-7a12-aea8-7f44cc0d14c4	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-10	25	6	0	1056.00	\N	0	2026-10-04 18:30:20.954053+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e0c1-7c9f-b9d6-0b398cc41cf2	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-10	11	5	2	230.00	\N	0	2026-10-04 18:30:21.009607+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e106-71a3-9216-7abf48ba6b83	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-11	16	2	0	441.00	\N	0	2026-10-04 18:30:21.077777+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e170-7fea-bb81-240761e9556b	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-12	27	7	4	1225.00	\N	0	2026-10-04 18:30:21.183562+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e19c-7a9b-827a-5ed1103ae12d	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-12	13	7	0	304.00	\N	0	2026-10-04 18:30:21.228666+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e1eb-7783-ba8c-2f9734451ab4	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-13	14	5	0	345.00	\N	0	2026-10-04 18:30:21.31054+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e224-78c6-a952-812cec87f27c	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-14	19	5	0	607.00	\N	0	2026-10-04 18:30:21.363984+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e2f2-7cc5-9ec1-12b227be53fc	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-15	30	5	2	1505.00	\N	0	2026-10-04 18:30:21.572921+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e425-79d1-8235-5ab8ed4b57a6	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-15	16	4	0	448.00	\N	0	2026-10-04 18:30:21.878376+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e556-7750-af63-982615c773a3	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-16	21	4	0	748.00	\N	0	2026-10-04 18:30:22.181636+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e62b-7087-b227-8793ecf528fb	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-17	32	6	0	1737.00	\N	0	2026-10-04 18:30:22.397635+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e683-7cf3-a6bd-7cad8559e7f7	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-18	33	6	2	1823.00	\N	0	2026-10-04 18:30:22.487413+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-e7a3-71cd-b902-0decbcda1f55	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-18	19	3	0	600.00	\N	0	2026-10-04 18:30:22.777843+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ea33-7b0c-b2b0-a3259ecd9ea0	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-19	20	8	0	663.00	\N	0	2026-10-04 18:30:23.431457+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-eae3-7142-bb16-27832515c562	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c093-725b-9c5f-bb53c87c1551	2026-08-20	35	2	0	2036.00	\N	0	2026-10-04 18:30:23.605247+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-eb84-718e-954b-eea4a9013bc0	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-20	25	6	0	1035.00	\N	0	2026-10-04 18:30:23.766133+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ec76-7ec6-9643-136d98207d00	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-21	26	4	0	1116.00	\N	0	2026-10-04 18:30:24.00516+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef2c-7dc3-9401-0b50b567af2d	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-22	23	10	0	868.00	\N	0	2026-10-04 18:30:24.700688+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ef8a-762f-b87b-4dfa0cc11dfa	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-23	24	6	0	940.00	\N	0	2026-10-04 18:30:24.794079+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f21d-729c-aed3-eb1298a6ae34	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-24	29	5	0	1389.00	\N	0	2026-10-04 18:30:25.451763+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f249-7711-bf44-ca51c35a89d5	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-24	1	26	0	43.00	\N	0	2026-10-04 18:30:25.497144+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f31c-7d26-a64f-c2ee00ca8c01	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-25	26	8	4	1089.00	\N	0	2026-10-04 18:30:25.708785+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f334-7b2b-9d13-3a1c9e1852c7	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-25	2	16	0	49.00	\N	0	2026-10-04 18:30:25.734272+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f467-7e62-b402-597ceab70307	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-26	27	8	0	1201.00	\N	0	2026-10-04 18:30:26.041336+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f4f6-7e81-a33a-0bcbb7962ab5	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-27	32	8	1	1690.00	\N	0	2026-10-04 18:30:26.183301+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f54e-7895-b6b7-43aa74cdb435	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-27	4	11	0	68.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:26.276739+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f650-75c7-9fba-80ac5ac0a46f	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-28	29	9	0	1377.00	\N	0	2026-10-04 18:30:26.529493+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f709-7db8-aca5-9deaf815b5f3	01a106ad-d05a-759c-a6ff-c3e67df7f41b	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0cc-775f-96fa-cd8dda0154d7	2026-08-29	34	2	0	1864.00	\N	0	2026-10-04 18:30:26.713823+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f749-7f43-9f65-0d6bbdfebc35	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-29	30	5	0	1439.00	\N	0	2026-10-04 18:30:26.776711+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f75f-7fef-9bb9-77c088b6c641	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-08-29	6	14	0	102.00	\N	0	2026-10-04 18:30:26.801266+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-f884-7c25-b2e0-734cf1afa507	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-08-30	31	8	0	1561.00	\N	0	2026-10-04 18:30:27.093066+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fab0-7a61-b55c-ecf6a30f1748	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-08-31	1	15	0	44.00	\N	0	2026-10-04 18:30:27.658569+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fbb7-70e1-a993-e0a2516e5837	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-09-01	33	8	0	1730.00	\N	0	2026-10-04 18:30:27.914278+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fc95-7669-9952-a41217db083d	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-09-02	34	11	3	1831.00	\N	0	2026-10-04 18:30:28.133067+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fccb-7a7f-ae91-dd67ac698fe8	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-02	10	5	0	203.00	\N	0	2026-10-04 18:30:28.190458+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fd00-7887-b6a2-16b1538dc55c	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-09-03	35	5	0	1974.00	\N	0	2026-10-04 18:30:28.239098+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fd78-7862-aeca-710b6952e364	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-03	11	4	0	236.00	\N	0	2026-10-04 18:30:28.361224+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-fe5b-7f78-8851-e48768b1eaa2	01a106ad-d332-7f38-9c34-260051b68ef3	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c120-72e3-bae4-d2248cc584d3	2026-09-04	36	4	0	2106.00	\N	0	2026-10-04 18:30:28.588055+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ff1a-723a-bd71-f4a0c33f6f7b	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-04	12	5	0	271.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:28.781388+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ff34-7ae4-834e-f02e7b6ebd31	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-05	6	23	0	103.00	\N	0	2026-10-04 18:30:28.806238+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ad-ffa0-73f8-a95e-0e84bd106901	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-06	7	22	0	123.00	\N	0	2026-10-04 18:30:28.912357+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-015a-794c-9313-0df96d4c3d43	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-07	15	4	1	407.00	\N	0	2026-10-04 18:30:29.354726+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0204-78c2-96bb-80d88307bca4	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-08	16	5	0	462.00	\N	0	2026-10-04 18:30:29.52535+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-021b-73a8-92ee-9e9f2e435605	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-09	10	4	0	206.00	\N	0	2026-10-04 18:30:29.549383+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-029d-76e2-aba8-c69e90634772	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-09	17	3	4	514.00	\N	0	2026-10-04 18:30:29.676881+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0361-767e-b8dd-182694843147	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-10	18	4	0	574.00	Booster ND-IB	0	2026-10-04 18:30:29.874502+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-045e-7bf5-9832-d2d0ed28357a	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-12	13	3	0	322.00	\N	0	2026-10-04 18:30:30.12505+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-055c-7886-b9b5-51dc49315d90	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-13	14	3	0	364.00	\N	0	2026-10-04 18:30:30.377155+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-05fe-721b-9d53-76ef30ada2e8	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-13	21	4	1	754.00	\N	0	2026-10-04 18:30:30.541607+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-069d-7dd8-ba89-7e3a7e50e393	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-14	1	24	0	44.00	\N	0	2026-10-04 18:30:30.699155+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-074d-7370-91e2-14c452cfc5f0	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-15	16	3	0	468.00	\N	0	2026-10-04 18:30:30.876123+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0762-7099-98f5-0b0394a98fd6	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-15	2	27	0	48.00	\N	0	2026-10-04 18:30:30.898812+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-078f-72a8-9693-b4fd659b10da	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-16	17	3	0	528.00	\N	0	2026-10-04 18:30:30.944083+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-07fd-7667-8034-27b86e55460e	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-16	24	9	0	980.00	\N	0	2026-10-04 18:30:31.053409+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0813-70d6-919f-2373ea0ba65b	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-17	18	3	0	582.00	Booster ND-IB	0	2026-10-04 18:30:31.076147+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0847-7969-8acb-4e9c2655c245	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-17	25	5	0	1045.00	\N	0	2026-10-04 18:30:31.126165+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-08fa-7281-ad5c-b5e0dc176cce	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-18	5	25	0	82.00	\N	0	2026-10-04 18:30:31.304958+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0a5d-7ea3-a3f4-e3e3fd08ab4d	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-19	20	5	0	697.00	\N	0	2026-10-04 18:30:31.659012+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0a71-7407-a255-d2804cc8322c	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-19	6	12	0	101.00	\N	0	2026-10-04 18:30:31.68289+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0a9f-7ee6-8e45-5e1b57981e9f	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-20	21	5	4	768.00	\N	0	2026-10-04 18:30:31.727651+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0acb-7dca-8e73-de3068249d61	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-20	28	8	0	1308.00	\N	0	2026-10-04 18:30:31.772166+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0bc4-733f-98ed-a70ec3f5661c	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-21	22	8	0	849.00	\N	0	2026-10-04 18:30:32.017306+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0bd8-7c8e-b5d3-cfc81f646f4f	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-21	8	5	0	144.00	\N	0	2026-10-04 18:30:32.039518+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c01-7d34-92e3-55ed42fc7d93	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-22	23	7	2	915.00	\N	0	2026-10-04 18:30:32.07914+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c29-7f35-8143-a6df282f87cd	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-22	30	5	0	1483.00	\N	0	2026-10-04 18:30:32.118915+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ce6-7ed3-815d-d78acb8beb5a	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-23	24	7	0	990.00	\N	0	2026-10-04 18:30:32.31972+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d08-7312-8292-d25b316d3500	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-23	10	5	0	205.00	\N	0	2026-10-04 18:30:32.348634+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d39-7f71-84bf-4f1de24ab449	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-23	1	15	0	44.00	\N	0	2026-10-04 18:30:32.394344+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d66-7662-8183-b4e125081c37	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-24	11	7	0	235.00	\N	0	2026-10-04 18:30:32.439866+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0e21-7392-ac64-84e99d638855	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-25	26	5	0	1157.00	\N	0	2026-10-04 18:30:32.624789+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f1f-703b-9d0a-2464808cf5d8	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-25	3	10	0	57.00	\N	0	2026-10-04 18:30:32.880727+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f4e-7a58-9227-731f2a961ad2	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-26	13	3	0	317.00	\N	0	2026-10-04 18:30:32.926257+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f7b-7665-8204-60f3af0c5e15	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-26	4	10	0	68.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:32.973966+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0fab-7019-82ca-0e75f9cb1722	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-27	14	4	0	356.00	\N	0	2026-10-04 18:30:33.017408+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-108c-76d1-be62-2df1049ed550	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-27	5	13	0	81.00	\N	0	2026-10-04 18:30:33.243054+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1129-7ebc-9afa-8d83e87d51ff	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-28	15	6	0	411.00	\N	0	2026-10-04 18:30:33.399611+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-123c-772e-bc9c-5f2206abc9a7	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-29	30	3	3	1527.00	\N	0	2026-10-04 18:30:33.674328+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1263-7f2f-bbea-b566851d80e8	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-29	37	2	0	2229.00	\N	0	2026-10-04 18:30:33.713398+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-12ff-74c7-a6c7-edbba1405eb5	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-29	7	14	0	121.00	\N	0	2026-10-04 18:30:33.871724+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-13a5-7ae1-8065-056bbe4af2e3	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-30	17	8	0	513.00	\N	0	2026-10-04 18:30:34.036457+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1401-7239-9838-ed8e5556b525	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-30	8	4	0	145.00	\N	0	2026-10-04 18:30:34.133093+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1537-7acb-8120-b3e4a1c141b7	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-10-01	9	2	0	176.00	\N	0	2026-10-04 18:30:34.439059+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1582-7a1f-8b9d-61fc64e31cc9	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-10-02	19	5	0	628.00	\N	0	2026-10-04 18:30:34.513397+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-15ef-767d-bad0-a41642b167b5	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-10-02	10	2	0	207.00	\N	0	2026-10-04 18:30:34.628715+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1813-771c-984d-2678c0084126	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-10-03	11	2	0	241.00	\N	0	2026-10-04 18:30:35.16973+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-06b1-7ae2-bb62-82bb05d20a3f	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-14	22	10	0	826.00	\N	0	2026-10-04 18:30:30.732289+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0779-7971-9c4d-87a2fdd6ad0c	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-15	23	4	0	893.00	\N	0	2026-10-04 18:30:30.920693+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-07e8-70aa-8483-dde7e16f845e	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-16	3	21	0	56.00	\N	0	2026-10-04 18:30:31.03128+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-082a-70a0-bfb4-ff75d29c680d	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-17	4	33	0	68.00	Vaksin ND-IB (tetes mata)	0	2026-10-04 18:30:31.103998+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-085c-712f-a271-039f95ca3b88	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-18	19	4	1	633.00	\N	0	2026-10-04 18:30:31.147861+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-090f-76fd-a291-b6b532ffc767	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-18	26	5	0	1145.00	\N	0	2026-10-04 18:30:31.325183+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0a8a-7c76-a7f2-005fe396f345	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-19	27	9	3	1223.00	\N	0	2026-10-04 18:30:31.705149+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ab4-7a29-8119-c4f4268fd80f	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-20	7	28	0	122.00	\N	0	2026-10-04 18:30:31.749059+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0bed-7628-9f59-9dbe1f312708	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-21	29	6	0	1425.00	\N	0	2026-10-04 18:30:32.060557+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0c16-7f39-9ed0-06a7f0e90700	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-22	9	7	0	172.00	\N	0	2026-10-04 18:30:32.099196+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d23-7c75-823e-6741f4848f1a	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-23	31	4	2	1575.00	\N	0	2026-10-04 18:30:32.371229+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d4f-7f7c-be06-6a1a4ce3d083	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-24	25	8	0	1082.00	\N	0	2026-10-04 18:30:32.415749+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d7d-7469-bc0e-c08e85d36fbb	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-24	32	6	0	1678.00	\N	0	2026-10-04 18:30:32.460006+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0d91-789b-aad9-9048c5bf8abb	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-24	2	11	0	49.00	\N	0	2026-10-04 18:30:32.486399+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0e68-70ab-bacf-a1ba945c4e10	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-25	12	4	0	274.00	Vaksin Gumboro (air minum)	0	2026-10-04 18:30:32.698002+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0ece-76de-b34b-aa355877e294	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-25	33	6	0	1833.00	\N	0	2026-10-04 18:30:32.796797+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f37-718b-9dbf-bd3c51be9432	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-26	27	7	0	1241.00	\N	0	2026-10-04 18:30:32.904221+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f64-7244-9653-6c258be05f86	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-26	34	7	4	1933.00	\N	0	2026-10-04 18:30:32.94803+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0f94-7973-b72a-a1ee14ccfa40	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-27	28	4	0	1326.00	\N	0	2026-10-04 18:30:32.996472+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-0fc3-7696-8265-5a185d22ba80	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-27	35	10	1	2022.00	\N	0	2026-10-04 18:30:33.048254+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1114-78be-b8d0-6cca6cbc5c44	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-28	29	4	0	1436.00	\N	0	2026-10-04 18:30:33.379664+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-113d-78a3-aa8a-7d1fed49212a	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c0fe-7ef7-883a-6874a1e63d54	2026-09-28	36	5	0	2145.00	\N	0	2026-10-04 18:30:33.419622+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1228-76cc-b1a8-febe704cf404	01a106ae-0935-71d7-8a79-528d2a90b371	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-c139-7a07-b8e4-9ea7321c38b7	2026-09-28	6	8	0	102.00	\N	0	2026-10-04 18:30:33.65445+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1250-7e96-8716-ed7c86794e39	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-09-29	16	4	0	463.00	\N	0	2026-10-04 18:30:33.69449+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-138d-729d-b5c9-b2e50c465273	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-09-30	31	7	0	1618.00	\N	0	2026-10-04 18:30:34.01319+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1462-76d1-a222-4ed44499544e	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-10-01	32	6	0	1754.00	\N	0	2026-10-04 18:30:34.226251+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1479-7cc5-90f2-0608efd0ff02	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-10-01	18	4	0	561.00	Booster ND-IB	0	2026-10-04 18:30:34.251916+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-154e-7794-b56c-34eee5209750	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-10-02	33	3	1	1849.00	\N	0	2026-10-04 18:30:34.462585+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-160b-7082-b0ed-65317a471bed	01a106ad-f40d-7a20-b733-56168fcec265	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-c0b0-728b-bb71-3e70df15f86d	2026-10-03	34	4	4	1927.00	\N	0	2026-10-04 18:30:34.651914+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-1772-7905-8e52-59d851299e1f	01a106ae-023f-704e-8a47-6e438951d196	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bfec-7b37-a51a-18c521c8bce6	2026-10-03	20	3	3	692.00	\N	0	2026-10-04 18:30:35.011309+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."__EFMigrationsHistory" (migration_id, product_version) FROM stdin;
20260930104420_Initial	10.0.9
20260930124847_Phase1_MasterData_Partnership	10.0.9
20260930134655_Phase2_FinanceCore	10.0.9
20260930141449_Phase3_ProcurementInventory	10.0.9
20260930143833_Phase4_Production	10.0.9
20260930221007_Phase5_SalesReceivables	10.0.9
20260930231213_Phase6_PayablesCashBank	10.0.9
20261001003432_Phase7_CostingSettlement	10.0.9
20261001014222_Phase8_ReportingClosing	10.0.9
20261001211036_Phase9_Attachments	10.0.9
20261002085239_PhaseW0_UserStatus	10.0.9
20261003054111_PhaseW1_AccessControl	10.0.9
20261003181926_PhaseW10_Hardening	10.0.9
\.


--
-- Data for Name: delivery_order_lines; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.delivery_order_lines (delivery_order_id, line_number, sales_order_line_number, item_id, harvest_id, cycle_id, birds, weight_kg, tax_code_id, is_cancelled, amount, price_per_kg) FROM stdin;
01a106ad-e73b-71ff-b88c-91fcb18277fe	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-e6cf-79c6-a3af-8de25a7c2971	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1592	2929.600	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	63865280.00	21800.00
01a106ad-e808-7139-9a2c-153c7f8a962b	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-e7ec-70a3-be77-51b1185b72df	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1589	3048.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	66450760.00	21800.00
01a106ad-eb1d-7610-a135-8e43de1e639f	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-eb06-710f-936f-b244c0e36501	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1588	3277.700	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	71453860.00	21800.00
01a106ad-f52c-7b73-9d2f-c9d43879d634	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-f515-7090-a53f-d79b5ce4066d	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2143	3568.800	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	76015440.00	21300.00
01a106ad-f73a-7a50-8be3-32bd14fa1401	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-f726-7880-9328-a878c2ebc091	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2135	4009.900	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	85410870.00	21300.00
01a106ad-fcc5-77b5-9e69-5454321ff9ea	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-fcb1-71bc-883b-63a739c5d666	01a106ad-d332-7f38-9c34-260051b68ef3	1548	2883.500	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	61995250.00	21500.00
01a106ad-fd2f-7b46-b0b6-c056e9ad2a75	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-fd1c-724e-afc0-ffe52c0261c1	01a106ad-d332-7f38-9c34-260051b68ef3	1546	3030.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	65149300.00	21500.00
01a106ad-fe8a-7040-a711-943ccafcbcbe	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-fe78-7e03-81e0-fb7a7830ce07	01a106ad-d332-7f38-9c34-260051b68ef3	1542	3176.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	68288300.00	21500.00
01a106ae-1000-76c0-bba5-785c58fe32b3	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ae-0fe8-7cf8-b580-4a8a6fd20929	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2226	4523.300	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	99060270.00	21900.00
01a106ae-1168-754c-9213-5494da6a4aec	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ae-1157-77d2-885f-259c6d236f59	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2224	4769.100	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	104443290.00	21900.00
01a106ae-129f-7429-9856-1b515856884a	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ae-127c-7fa9-bfa9-7e5af93096dd	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2223	4986.700	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	109208730.00	21900.00
01a106ae-157c-7adb-98f5-8b82bd09f771	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ae-156a-768d-81c8-0eaa781b8ab8	01a106ad-f40d-7a20-b733-56168fcec265	1436	2648.500	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	58531850.00	22100.00
01a106ae-1640-7ecb-8ef3-e2607b152264	1	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ae-1629-7b4b-8b45-bfd631bd907c	01a106ad-f40d-7a20-b733-56168fcec265	1434	2777.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	f	61376120.00	22100.00
\.


--
-- Data for Name: delivery_orders; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.delivery_orders (id, number, branch_id, sales_order_id, customer_id, delivery_date, vehicle_number, driver_name, notes, status, sales_invoice_id, cancellation_reason, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-e73b-71ff-b88c-91fcb18277fe	DO/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-e46d-75a1-bfd7-f210db2a4a91	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-18	D 8101 BL	Cecep Hidayat	\N	Invoiced	01a106ad-e851-7815-872f-3eee4106b6c0	\N	2026-10-04 18:30:22.690771+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:22.977444+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-e808-7139-9a2c-153c7f8a962b	DO/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-e46d-75a1-bfd7-f210db2a4a91	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-19	D 8102 CM	Jajang Nurjaman	\N	Invoiced	01a106ad-eb3e-7840-8bd9-c308839c5897	\N	2026-10-04 18:30:22.856565+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.678954+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-eb1d-7610-a135-8e43de1e639f	DO/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-e46d-75a1-bfd7-f210db2a4a91	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-20	D 8103 DN	Mamat Rahmat	\N	Invoiced	01a106ad-ebc3-784b-93be-5919195da558	\N	2026-10-04 18:30:23.645986+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.8124+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f52c-7b73-9d2f-c9d43879d634	DO/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-f2ed-7ece-bf70-cc80a96794f6	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-27	D 8104 EO	Yayan Sopyan	\N	Invoiced	01a106ad-f592-755f-870c-754df282fed9	\N	2026-10-04 18:30:26.221115+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.323224+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f73a-7a50-8be3-32bd14fa1401	DO/BDG/2026/VIII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-f2ed-7ece-bf70-cc80a96794f6	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-29	D 8105 FP	Ade Supriatna	\N	Invoiced	01a106ad-f785-70ba-9a35-aaa06a2eaa01	\N	2026-10-04 18:30:26.746266+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.822271+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fcc5-77b5-9e69-5454321ff9ea	DO/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-faf8-7a6e-846e-83813ba16a32	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-02	F 8106 GQ	Cecep Hidayat	\N	Invoiced	01a106ad-fd40-7640-a517-a313d071cc33	\N	2026-10-04 18:30:28.165833+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.288838+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fd2f-7b46-b0b6-c056e9ad2a75	DO/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-faf8-7a6e-846e-83813ba16a32	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-03	F 8107 HR	Jajang Nurjaman	\N	Invoiced	01a106ad-fea7-70cb-af7e-26337a183541	\N	2026-10-04 18:30:28.271864+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.648288+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-fe8a-7040-a711-943ccafcbcbe	DO/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-faf8-7a6e-846e-83813ba16a32	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-04	F 8108 IS	Mamat Rahmat	\N	Invoiced	01a106ad-ff5a-73aa-bb0b-f6a7a6cff728	\N	2026-10-04 18:30:28.618829+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.826737+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1000-76c0-bba5-785c58fe32b3	DO/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-0e8c-7b45-af99-1baa04032b04	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-27	F 8109 JT	Yayan Sopyan	\N	Invoiced	01a106ae-1177-79f1-9dab-ecd0831503a9	\N	2026-10-04 18:30:33.088759+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.464103+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1168-754c-9213-5494da6a4aec	DO/CJR/2026/IX/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-0e8c-7b45-af99-1baa04032b04	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-28	F 8110 KK	Ade Supriatna	\N	Invoiced	01a106ae-12c6-7315-9f02-2fa6cd84672d	\N	2026-10-04 18:30:33.448799+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.798578+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-129f-7429-9856-1b515856884a	DO/CJR/2026/IX/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ae-0e8c-7b45-af99-1baa04032b04	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-29	F 8111 LL	Cecep Hidayat	\N	Invoiced	01a106ae-13c7-7e8d-bea5-901363b90855	\N	2026-10-04 18:30:33.76029+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.055784+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1640-7ecb-8ef3-e2607b152264	DO/BDG/2026/X/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ae-1373-7a52-bbb4-9171a1ae428f	01a106ad-bee3-776e-8294-c067db6f4750	2026-10-03	D 8113 NN	Mamat Rahmat	\N	Delivered	\N	\N	2026-10-04 18:30:34.689077+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N	{}
01a106ae-157c-7adb-98f5-8b82bd09f771	DO/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ae-1373-7a52-bbb4-9171a1ae428f	01a106ad-bee3-776e-8294-c067db6f4750	2026-10-02	D 8112 MM	Jajang Nurjaman	\N	Invoiced	01a106ae-1653-7ba7-b963-74958f58aa0d	\N	2026-10-04 18:30:34.492526+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.707543+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
\.


--
-- Data for Name: sales_credit_note_lines; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_credit_note_lines (sales_credit_note_id, invoice_line_number, cycle_id, amount, vat_amount) FROM stdin;
01a106ad-ec1a-7e4b-9c45-bc2b557665a5	1	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	750000.00	0.00
\.


--
-- Data for Name: sales_credit_notes; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_credit_notes (id, number, branch_id, customer_id, sales_invoice_id, date, reason, subtotal, total, vat_amount, created_at_utc, created_by, modified_at_utc, modified_by) FROM stdin;
01a106ad-ec1a-7e4b-9c45-bc2b557665a5	CN/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	01a106ad-e851-7815-872f-3eee4106b6c0	2026-08-21	Klaim susut timbang di RPA	750000.00	750000.00	0.00	2026-10-04 18:30:23.923272+07	01a0f240-f921-75b0-972d-0f74522a6333	\N	\N
\.


--
-- Data for Name: sales_invoice_lines; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_invoice_lines (sales_invoice_id, line_number, delivery_order_id, delivery_order_line_number, item_id, cycle_id, birds, weight_kg, tax_code_id, vat_rate_percent, amount, price_per_kg, vat_amount, vat_tax_base, credited_amount, cost_amount) FROM stdin;
01a106ad-eb3e-7840-8bd9-c308839c5897	1	01a106ad-e808-7139-9a2c-153c7f8a962b	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1589	3048.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	66450760.00	21800.00	0.00	0.00	0.00	48276599.07
01a106ad-ebc3-784b-93be-5919195da558	1	01a106ad-eb1d-7610-a135-8e43de1e639f	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1588	3277.700	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	71453860.00	21800.00	0.00	0.00	0.00	51911360.40
01a106ad-e851-7815-872f-3eee4106b6c0	1	01a106ad-e73b-71ff-b88c-91fcb18277fe	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-c6f1-7f5f-8d11-f2ccfb191671	1592	2929.600	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	63865280.00	21800.00	0.00	0.00	750000.00	47035313.92
01a106ad-f592-755f-870c-754df282fed9	1	01a106ad-f52c-7b73-9d2f-c9d43879d634	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2143	3568.800	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	76015440.00	21300.00	0.00	0.00	0.00	65278419.70
01a106ad-f785-70ba-9a35-aaa06a2eaa01	1	01a106ad-f73a-7a50-8be3-32bd14fa1401	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-d05a-759c-a6ff-c3e67df7f41b	2135	4009.900	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	85410870.00	21300.00	0.00	0.00	0.00	72130361.89
01a106ad-fd40-7640-a517-a313d071cc33	1	01a106ad-fcc5-77b5-9e69-5454321ff9ea	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-d332-7f38-9c34-260051b68ef3	1548	2883.500	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	61995250.00	21500.00	0.00	0.00	0.00	52991723.10
01a106ad-fea7-70cb-af7e-26337a183541	1	01a106ad-fd2f-7b46-b0b6-c056e9ad2a75	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-d332-7f38-9c34-260051b68ef3	1546	3030.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	65149300.00	21500.00	0.00	0.00	0.00	55656713.67
01a106ad-ff5a-73aa-bb0b-f6a7a6cff728	1	01a106ad-fe8a-7040-a711-943ccafcbcbe	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-d332-7f38-9c34-260051b68ef3	1542	3176.200	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	68288300.00	21500.00	0.00	0.00	0.00	58338345.31
01a106ae-1177-79f1-9dab-ecd0831503a9	1	01a106ae-1000-76c0-bba5-785c58fe32b3	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2226	4523.300	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	99060270.00	21900.00	0.00	0.00	0.00	73985220.75
01a106ae-12c6-7315-9f02-2fa6cd84672d	1	01a106ae-1168-754c-9213-5494da6a4aec	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2224	4769.100	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	104443290.00	21900.00	0.00	0.00	0.00	77840105.62
01a106ae-13c7-7e8d-bea5-901363b90855	1	01a106ae-129f-7429-9856-1b515856884a	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-ea62-7df6-aaaa-1359b7c73fa1	2223	4986.700	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	109208730.00	21900.00	0.00	0.00	0.00	81391720.59
01a106ae-1653-7ba7-b963-74958f58aa0d	1	01a106ae-157c-7adb-98f5-8b82bd09f771	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	01a106ad-f40d-7a20-b733-56168fcec265	1436	2648.500	01a106ad-bcf7-75f7-995a-8a39126ee3b0	0.0000	58531850.00	22100.00	0.00	0.00	0.00	43780340.64
\.


--
-- Data for Name: sales_invoices; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_invoices (id, number, branch_id, customer_id, invoice_date, due_date, status, notes, posted_by, posted_at_utc, cancellation_reason, paid_amount, subtotal, total, vat_amount, created_at_utc, created_by, modified_at_utc, modified_by, credited_amount) FROM stdin;
01a106ae-13c7-7e8d-bea5-901363b90855	INV/CJR/2026/IX/0006	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-30	2026-10-14	PartiallyPaid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.080067+07	\N	65525000.00	109208730.00	109208730.00	0.00	2026-10-04 18:30:34.055784+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:35.129112+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-e851-7815-872f-3eee4106b6c0	INV/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-19	2026-09-02	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.140524+07	\N	63115280.00	63865280.00	63865280.00	0.00	2026-10-04 18:30:22.977444+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.24521+07	01a0f240-f921-75b0-972d-0f74522a6333	750000.00
01a106ad-eb3e-7840-8bd9-c308839c5897	INV/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-20	2026-09-03	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.706693+07	\N	66450760.00	66450760.00	66450760.00	0.00	2026-10-04 18:30:23.678954+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:24.722993+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-ebc3-784b-93be-5919195da558	INV/BDG/2026/VIII/0003	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-21	2026-09-04	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.839118+07	\N	71453860.00	71453860.00	71453860.00	0.00	2026-10-04 18:30:23.8124+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:25.108487+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-f592-755f-870c-754df282fed9	INV/BDG/2026/VIII/0004	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-28	2026-09-04	PartiallyPaid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.352948+07	\N	45609000.00	76015440.00	76015440.00	0.00	2026-10-04 18:30:26.323224+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.353604+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-f785-70ba-9a35-aaa06a2eaa01	INV/BDG/2026/VIII/0005	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-30	2026-09-06	PartiallyPaid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.84543+07	\N	51247000.00	85410870.00	85410870.00	0.00	2026-10-04 18:30:26.822271+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:27.960468+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-fd40-7640-a517-a313d071cc33	INV/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-03	2026-09-17	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.312876+07	\N	61995250.00	61995250.00	61995250.00	0.00	2026-10-04 18:30:28.288838+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.931472+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-fea7-70cb-af7e-26337a183541	INV/CJR/2026/IX/0002	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-04	2026-09-18	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.673436+07	\N	65149300.00	65149300.00	65149300.00	0.00	2026-10-04 18:30:28.648288+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.306731+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ad-ff5a-73aa-bb0b-f6a7a6cff728	INV/CJR/2026/IX/0003	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-05	2026-09-19	Paid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.847576+07	\N	68288300.00	68288300.00	68288300.00	0.00	2026-10-04 18:30:28.826737+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:29.398293+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ae-1177-79f1-9dab-ecd0831503a9	INV/CJR/2026/IX/0004	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-28	2026-10-12	PartiallyPaid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.487222+07	\N	79436000.00	99060270.00	99060270.00	0.00	2026-10-04 18:30:33.464103+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.273539+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ae-12c6-7315-9f02-2fa6cd84672d	INV/CJR/2026/IX/0005	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-29	2026-10-13	PartiallyPaid	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.822152+07	\N	62666000.00	104443290.00	104443290.00	0.00	2026-10-04 18:30:33.798578+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.552669+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
01a106ae-1653-7ba7-b963-74958f58aa0d	INV/BDG/2026/X/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-10-03	2026-10-17	Posted	\N	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.735048+07	\N	0.00	58531850.00	58531850.00	0.00	2026-10-04 18:30:34.707543+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.735083+07	01a0f240-f921-75b0-972d-0f74522a6333	0.00
\.


--
-- Data for Name: sales_order_lines; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_order_lines (sales_order_id, line_number, item_id, birds, estimated_weight_kg, tax_code_id, delivered_birds, delivered_weight_kg, price_per_kg) FROM stdin;
01a106ad-e46d-75a1-bfd7-f210db2a4a91	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	5000	9121.000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	4769	9255.500	21800.00
01a106ad-f2ed-7ece-bf70-cc80a96794f6	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	4500	7501.000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	4278	7578.700	21300.00
01a106ad-faf8-7a6e-846e-83813ba16a32	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	5000	9286.000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	4636	9089.900	21500.00
01a106ae-0e8c-7b45-af99-1baa04032b04	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	7000	14186.000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	6673	14279.100	21900.00
01a106ae-1373-7a52-bbb4-9171a1ae428f	1	01a106ad-bdc5-76d0-acbf-ed1ce41a860a	6000	11054.000	01a106ad-bcf7-75f7-995a-8a39126ee3b0	2870	5425.700	22100.00
\.


--
-- Data for Name: sales_orders; Type: TABLE DATA; Schema: sales; Owner: postgres
--

COPY sales.sales_orders (id, number, branch_id, customer_id, order_date, delivery_date, status, notes, approved_by, approved_at_utc, credit_override_reason, cancellation_reason, created_at_utc, created_by, modified_at_utc, modified_by, documents) FROM stdin;
01a106ad-e46d-75a1-bfd7-f210db2a4a91	SO/BDG/2026/VIII/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-08-16	2026-08-18	Closed	Panen KDG-BDG-01	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:22.135764+07	\N	\N	2026-10-04 18:30:21.965684+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:23.660807+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-f2ed-7ece-bf70-cc80a96794f6	SO/BDG/2026/VIII/0002	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bf1a-7a36-bde5-38f4f1ee95d7	2026-08-25	2026-08-27	Closed	Panen KDG-BDG-03	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:25.666273+07	\N	\N	2026-10-04 18:30:25.645856+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:26.758406+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ad-faf8-7a6e-846e-83813ba16a32	SO/CJR/2026/VIII/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-08-31	2026-09-02	Closed	Panen KDG-CJR-01	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:27.731144+07	\N	\N	2026-10-04 18:30:27.70497+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:28.631567+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-0e8c-7b45-af99-1baa04032b04	SO/CJR/2026/IX/0001	01a106ad-b5da-7c16-8a1e-37a0998b9705	01a106ad-bf25-7367-9282-969a0e7d56d9	2026-09-25	2026-09-27	Closed	Panen KDG-CJR-INTI	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:32.737186+07	\N	\N	2026-10-04 18:30:32.716226+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:33.777581+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
01a106ae-1373-7a52-bbb4-9171a1ae428f	SO/BDG/2026/IX/0001	01a106ad-b536-7f76-bf02-1115db4d6aff	01a106ad-bee3-776e-8294-c067db6f4750	2026-09-30	2026-10-02	PartiallyDelivered	Panen KDG-BDG-02	01a106ad-b782-7dd8-9680-2dd57b9573c4	2026-10-04 18:30:33.993607+07	\N	\N	2026-10-04 18:30:33.971796+07	01a0f240-f921-75b0-972d-0f74522a6333	2026-10-04 18:30:34.492526+07	01a0f240-f921-75b0-972d-0f74522a6333	{}
\.


--
-- Name: data_protection_keys_id_seq; Type: SEQUENCE SET; Schema: infrastructure; Owner: postgres
--

SELECT pg_catalog.setval('infrastructure.data_protection_keys_id_seq', 1, true);


--
-- Name: plasma_settlement_lines pk_plasma_settlement_lines; Type: CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlement_lines
    ADD CONSTRAINT pk_plasma_settlement_lines PRIMARY KEY (plasma_settlement_id, line_number);


--
-- Name: plasma_settlements pk_plasma_settlements; Type: CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT pk_plasma_settlements PRIMARY KEY (id);


--
-- Name: attachment_links pk_attachment_links; Type: CONSTRAINT; Schema: documents; Owner: postgres
--

ALTER TABLE ONLY documents.attachment_links
    ADD CONSTRAINT pk_attachment_links PRIMARY KEY (owner_type, owner_id, owner_key, attachment_id);


--
-- Name: attachments pk_attachments; Type: CONSTRAINT; Schema: documents; Owner: postgres
--

ALTER TABLE ONLY documents.attachments
    ADD CONSTRAINT pk_attachments PRIMARY KEY (id);


--
-- Name: accounts pk_accounts; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.accounts
    ADD CONSTRAINT pk_accounts PRIMARY KEY (id);


--
-- Name: bank_reconciliations pk_bank_reconciliations; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_reconciliations
    ADD CONSTRAINT pk_bank_reconciliations PRIMARY KEY (id);


--
-- Name: bank_statement_lines pk_bank_statement_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_statement_lines
    ADD CONSTRAINT pk_bank_statement_lines PRIMARY KEY (bank_reconciliation_id, line_number);


--
-- Name: bank_transfers pk_bank_transfers; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_transfers
    ADD CONSTRAINT pk_bank_transfers PRIMARY KEY (id);


--
-- Name: cash_bank_accounts pk_cash_bank_accounts; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_bank_accounts
    ADD CONSTRAINT pk_cash_bank_accounts PRIMARY KEY (id);


--
-- Name: cash_transaction_lines pk_cash_transaction_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transaction_lines
    ADD CONSTRAINT pk_cash_transaction_lines PRIMARY KEY (cash_transaction_id, line_number);


--
-- Name: cash_transactions pk_cash_transactions; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transactions
    ADD CONSTRAINT pk_cash_transactions PRIMARY KEY (id);


--
-- Name: cost_centers pk_cost_centers; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cost_centers
    ADD CONSTRAINT pk_cost_centers PRIMARY KEY (id);


--
-- Name: customer_advance_applications pk_customer_advance_applications; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_advance_applications
    ADD CONSTRAINT pk_customer_advance_applications PRIMARY KEY (id);


--
-- Name: customer_receipt_allocations pk_customer_receipt_allocations; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipt_allocations
    ADD CONSTRAINT pk_customer_receipt_allocations PRIMARY KEY (customer_receipt_id, sales_invoice_id);


--
-- Name: customer_receipts pk_customer_receipts; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipts
    ADD CONSTRAINT pk_customer_receipts PRIMARY KEY (id);


--
-- Name: fiscal_periods pk_fiscal_periods; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.fiscal_periods
    ADD CONSTRAINT pk_fiscal_periods PRIMARY KEY (id);


--
-- Name: journal_entries pk_journal_entries; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_entries
    ADD CONSTRAINT pk_journal_entries PRIMARY KEY (id);


--
-- Name: journal_lines pk_journal_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_lines
    ADD CONSTRAINT pk_journal_lines PRIMARY KEY (journal_entry_id, line_number);


--
-- Name: journal_mapping_lines pk_journal_mapping_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mapping_lines
    ADD CONSTRAINT pk_journal_mapping_lines PRIMARY KEY (journal_mapping_id, component);


--
-- Name: journal_mappings pk_journal_mappings; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mappings
    ADD CONSTRAINT pk_journal_mappings PRIMARY KEY (id);


--
-- Name: journal_template_lines pk_journal_template_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_template_lines
    ADD CONSTRAINT pk_journal_template_lines PRIMARY KEY (journal_template_id, line_number);


--
-- Name: journal_templates pk_journal_templates; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_templates
    ADD CONSTRAINT pk_journal_templates PRIMARY KEY (id);


--
-- Name: payment_voucher_allocations pk_payment_voucher_allocations; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_allocations
    ADD CONSTRAINT pk_payment_voucher_allocations PRIMARY KEY (payment_voucher_id, vendor_invoice_id);


--
-- Name: payment_voucher_settlement_allocations pk_payment_voucher_settlement_allocations; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_settlement_allocations
    ADD CONSTRAINT pk_payment_voucher_settlement_allocations PRIMARY KEY (payment_voucher_id, plasma_settlement_id);


--
-- Name: payment_vouchers pk_payment_vouchers; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_vouchers
    ADD CONSTRAINT pk_payment_vouchers PRIMARY KEY (id);


--
-- Name: vendor_invoice_lines pk_vendor_invoice_lines; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT pk_vendor_invoice_lines PRIMARY KEY (vendor_invoice_id, line_number);


--
-- Name: vendor_invoices pk_vendor_invoices; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoices
    ADD CONSTRAINT pk_vendor_invoices PRIMARY KEY (id);


--
-- Name: branch_access_profile_branches pk_branch_access_profile_branches; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.branch_access_profile_branches
    ADD CONSTRAINT pk_branch_access_profile_branches PRIMARY KEY (profile_id, branch_id);


--
-- Name: branch_access_profiles pk_branch_access_profiles; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.branch_access_profiles
    ADD CONSTRAINT pk_branch_access_profiles PRIMARY KEY (id);


--
-- Name: menu_access_profile_items pk_menu_access_profile_items; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.menu_access_profile_items
    ADD CONSTRAINT pk_menu_access_profile_items PRIMARY KEY (profile_id, menu_id);


--
-- Name: menu_access_profiles pk_menu_access_profiles; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.menu_access_profiles
    ADD CONSTRAINT pk_menu_access_profiles PRIMARY KEY (id);


--
-- Name: menus pk_menus; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.menus
    ADD CONSTRAINT pk_menus PRIMARY KEY (id);


--
-- Name: refresh_tokens pk_refresh_tokens; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.refresh_tokens
    ADD CONSTRAINT pk_refresh_tokens PRIMARY KEY (id);


--
-- Name: role_permissions pk_role_permissions; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.role_permissions
    ADD CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission);


--
-- Name: roles pk_roles; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.roles
    ADD CONSTRAINT pk_roles PRIMARY KEY (id);


--
-- Name: user_old pk_user_old; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.user_old
    ADD CONSTRAINT pk_user_old PRIMARY KEY (id);


--
-- Name: user_roles pk_user_roles; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id);


--
-- Name: users pk_users; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT pk_users PRIMARY KEY (id);


--
-- Name: audit_logs pk_audit_logs; Type: CONSTRAINT; Schema: infrastructure; Owner: postgres
--

ALTER TABLE ONLY infrastructure.audit_logs
    ADD CONSTRAINT pk_audit_logs PRIMARY KEY (id);


--
-- Name: data_protection_keys pk_data_protection_keys; Type: CONSTRAINT; Schema: infrastructure; Owner: postgres
--

ALTER TABLE ONLY infrastructure.data_protection_keys
    ADD CONSTRAINT pk_data_protection_keys PRIMARY KEY (id);


--
-- Name: document_sequences pk_document_sequences; Type: CONSTRAINT; Schema: infrastructure; Owner: postgres
--

ALTER TABLE ONLY infrastructure.document_sequences
    ADD CONSTRAINT pk_document_sequences PRIMARY KEY (key);


--
-- Name: outbox_messages pk_outbox_messages; Type: CONSTRAINT; Schema: infrastructure; Owner: postgres
--

ALTER TABLE ONLY infrastructure.outbox_messages
    ADD CONSTRAINT pk_outbox_messages PRIMARY KEY (id);


--
-- Name: goods_receipt_lines pk_goods_receipt_lines; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipt_lines
    ADD CONSTRAINT pk_goods_receipt_lines PRIMARY KEY (goods_receipt_id, line_number);


--
-- Name: goods_receipts pk_goods_receipts; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT pk_goods_receipts PRIMARY KEY (id);


--
-- Name: stock_balances pk_stock_balances; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_balances
    ADD CONSTRAINT pk_stock_balances PRIMARY KEY (id);


--
-- Name: stock_ledger_entries pk_stock_ledger_entries; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_ledger_entries
    ADD CONSTRAINT pk_stock_ledger_entries PRIMARY KEY (id);


--
-- Name: stock_return_lines pk_stock_return_lines; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_return_lines
    ADD CONSTRAINT pk_stock_return_lines PRIMARY KEY (stock_return_id, line_number);


--
-- Name: stock_returns pk_stock_returns; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_returns
    ADD CONSTRAINT pk_stock_returns PRIMARY KEY (id);


--
-- Name: stock_transfer_lines pk_stock_transfer_lines; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfer_lines
    ADD CONSTRAINT pk_stock_transfer_lines PRIMARY KEY (stock_transfer_id, line_number);


--
-- Name: stock_transfers pk_stock_transfers; Type: CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfers
    ADD CONSTRAINT pk_stock_transfers PRIMARY KEY (id);


--
-- Name: branches pk_branches; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.branches
    ADD CONSTRAINT pk_branches PRIMARY KEY (id);


--
-- Name: coops pk_coops; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.coops
    ADD CONSTRAINT pk_coops PRIMARY KEY (id);


--
-- Name: customers pk_customers; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.customers
    ADD CONSTRAINT pk_customers PRIMARY KEY (id);


--
-- Name: farmers pk_farmers; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.farmers
    ADD CONSTRAINT pk_farmers PRIMARY KEY (id);


--
-- Name: item_uom_conversions pk_item_uom_conversions; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.item_uom_conversions
    ADD CONSTRAINT pk_item_uom_conversions PRIMARY KEY (item_id, uom_id);


--
-- Name: items pk_items; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.items
    ADD CONSTRAINT pk_items PRIMARY KEY (id);


--
-- Name: tax_codes pk_tax_codes; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.tax_codes
    ADD CONSTRAINT pk_tax_codes PRIMARY KEY (id);


--
-- Name: tax_rates pk_tax_rates; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.tax_rates
    ADD CONSTRAINT pk_tax_rates PRIMARY KEY (tax_code_id, effective_from);


--
-- Name: uoms pk_uoms; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.uoms
    ADD CONSTRAINT pk_uoms PRIMARY KEY (id);


--
-- Name: vendors pk_vendors; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.vendors
    ADD CONSTRAINT pk_vendors PRIMARY KEY (id);


--
-- Name: warehouses pk_warehouses; Type: CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.warehouses
    ADD CONSTRAINT pk_warehouses PRIMARY KEY (id);


--
-- Name: contract_incentives pk_contract_incentives; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_incentives
    ADD CONSTRAINT pk_contract_incentives PRIMARY KEY (contract_id, line_number);


--
-- Name: contract_input_prices pk_contract_input_prices; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_input_prices
    ADD CONSTRAINT pk_contract_input_prices PRIMARY KEY (contract_id, item_id);


--
-- Name: contract_live_bird_prices pk_contract_live_bird_prices; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_live_bird_prices
    ADD CONSTRAINT pk_contract_live_bird_prices PRIMARY KEY (contract_id, min_weight_kg);


--
-- Name: contracts pk_contracts; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contracts
    ADD CONSTRAINT pk_contracts PRIMARY KEY (id);


--
-- Name: cycle_harvests pk_cycle_harvests; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.cycle_harvests
    ADD CONSTRAINT pk_cycle_harvests PRIMARY KEY (id);


--
-- Name: production_cycles pk_production_cycles; Type: CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.production_cycles
    ADD CONSTRAINT pk_production_cycles PRIMARY KEY (id);


--
-- Name: purchase_order_lines pk_purchase_order_lines; Type: CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_order_lines
    ADD CONSTRAINT pk_purchase_order_lines PRIMARY KEY (purchase_order_id, line_number);


--
-- Name: purchase_orders pk_purchase_orders; Type: CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_orders
    ADD CONSTRAINT pk_purchase_orders PRIMARY KEY (id);


--
-- Name: daily_recording_revisions pk_daily_recording_revisions; Type: CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_revisions
    ADD CONSTRAINT pk_daily_recording_revisions PRIMARY KEY (daily_recording_id, revision_number);


--
-- Name: daily_recording_usages pk_daily_recording_usages; Type: CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_usages
    ADD CONSTRAINT pk_daily_recording_usages PRIMARY KEY (daily_recording_id, item_id);


--
-- Name: daily_recordings pk_daily_recordings; Type: CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recordings
    ADD CONSTRAINT pk_daily_recordings PRIMARY KEY (id);


--
-- Name: __EFMigrationsHistory pk___ef_migrations_history; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id);


--
-- Name: delivery_order_lines pk_delivery_order_lines; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT pk_delivery_order_lines PRIMARY KEY (delivery_order_id, line_number);


--
-- Name: delivery_orders pk_delivery_orders; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_orders
    ADD CONSTRAINT pk_delivery_orders PRIMARY KEY (id);


--
-- Name: sales_credit_note_lines pk_sales_credit_note_lines; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_note_lines
    ADD CONSTRAINT pk_sales_credit_note_lines PRIMARY KEY (sales_credit_note_id, invoice_line_number);


--
-- Name: sales_credit_notes pk_sales_credit_notes; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_notes
    ADD CONSTRAINT pk_sales_credit_notes PRIMARY KEY (id);


--
-- Name: sales_invoice_lines pk_sales_invoice_lines; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT pk_sales_invoice_lines PRIMARY KEY (sales_invoice_id, line_number);


--
-- Name: sales_invoices pk_sales_invoices; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoices
    ADD CONSTRAINT pk_sales_invoices PRIMARY KEY (id);


--
-- Name: sales_order_lines pk_sales_order_lines; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_order_lines
    ADD CONSTRAINT pk_sales_order_lines PRIMARY KEY (sales_order_id, line_number);


--
-- Name: sales_orders pk_sales_orders; Type: CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_orders
    ADD CONSTRAINT pk_sales_orders PRIMARY KEY (id);


--
-- Name: ix_plasma_settlements_branch_id_settlement_date; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE INDEX ix_plasma_settlements_branch_id_settlement_date ON costing.plasma_settlements USING btree (branch_id, settlement_date);


--
-- Name: ix_plasma_settlements_contract_id; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE INDEX ix_plasma_settlements_contract_id ON costing.plasma_settlements USING btree (contract_id);


--
-- Name: ix_plasma_settlements_cycle_id_active; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE UNIQUE INDEX ix_plasma_settlements_cycle_id_active ON costing.plasma_settlements USING btree (cycle_id) WHERE ((status)::text <> 'Cancelled'::text);


--
-- Name: ix_plasma_settlements_farmer_id_status; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE INDEX ix_plasma_settlements_farmer_id_status ON costing.plasma_settlements USING btree (farmer_id, status);


--
-- Name: ix_plasma_settlements_income_tax_code_id; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE INDEX ix_plasma_settlements_income_tax_code_id ON costing.plasma_settlements USING btree (income_tax_code_id);


--
-- Name: ix_plasma_settlements_number; Type: INDEX; Schema: costing; Owner: postgres
--

CREATE UNIQUE INDEX ix_plasma_settlements_number ON costing.plasma_settlements USING btree (number);


--
-- Name: ix_attachment_links_attachment_id; Type: INDEX; Schema: documents; Owner: postgres
--

CREATE INDEX ix_attachment_links_attachment_id ON documents.attachment_links USING btree (attachment_id);


--
-- Name: ix_attachments_checksum; Type: INDEX; Schema: documents; Owner: postgres
--

CREATE INDEX ix_attachments_checksum ON documents.attachments USING btree (checksum);


--
-- Name: ix_attachments_status_unlinked_at_utc; Type: INDEX; Schema: documents; Owner: postgres
--

CREATE INDEX ix_attachments_status_unlinked_at_utc ON documents.attachments USING btree (status, unlinked_at_utc);


--
-- Name: ix_accounts_code; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_accounts_code ON finance.accounts USING btree (code);


--
-- Name: ix_accounts_parent_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_accounts_parent_id ON finance.accounts USING btree (parent_id);


--
-- Name: ix_bank_reconciliations_branch_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_bank_reconciliations_branch_id ON finance.bank_reconciliations USING btree (branch_id);


--
-- Name: ix_bank_reconciliations_cash_bank_account_id_in_progress; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_bank_reconciliations_cash_bank_account_id_in_progress ON finance.bank_reconciliations USING btree (cash_bank_account_id) WHERE ((status)::text = 'InProgress'::text);


--
-- Name: ix_bank_reconciliations_cash_bank_account_id_statement_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_bank_reconciliations_cash_bank_account_id_statement_date ON finance.bank_reconciliations USING btree (cash_bank_account_id, statement_date);


--
-- Name: ix_bank_statement_lines_matched_journal_entry_id_matched_journ; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_bank_statement_lines_matched_journal_entry_id_matched_journ ON finance.bank_statement_lines USING btree (matched_journal_entry_id, matched_journal_line_number) WHERE (matched_journal_entry_id IS NOT NULL);


--
-- Name: ix_bank_transfers_branch_id_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_bank_transfers_branch_id_date ON finance.bank_transfers USING btree (branch_id, date);


--
-- Name: ix_bank_transfers_from_cash_bank_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_bank_transfers_from_cash_bank_account_id ON finance.bank_transfers USING btree (from_cash_bank_account_id);


--
-- Name: ix_bank_transfers_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_bank_transfers_number ON finance.bank_transfers USING btree (number);


--
-- Name: ix_bank_transfers_to_cash_bank_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_bank_transfers_to_cash_bank_account_id ON finance.bank_transfers USING btree (to_cash_bank_account_id);


--
-- Name: ix_cash_bank_accounts_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_cash_bank_accounts_account_id ON finance.cash_bank_accounts USING btree (account_id);


--
-- Name: ix_cash_bank_accounts_branch_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_cash_bank_accounts_branch_id ON finance.cash_bank_accounts USING btree (branch_id);


--
-- Name: ix_cash_bank_accounts_code; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_cash_bank_accounts_code ON finance.cash_bank_accounts USING btree (code);


--
-- Name: ix_cash_transaction_lines_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_cash_transaction_lines_account_id ON finance.cash_transaction_lines USING btree (account_id);


--
-- Name: ix_cash_transaction_lines_cost_center_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_cash_transaction_lines_cost_center_id ON finance.cash_transaction_lines USING btree (cost_center_id);


--
-- Name: ix_cash_transactions_branch_id_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_cash_transactions_branch_id_date ON finance.cash_transactions USING btree (branch_id, date);


--
-- Name: ix_cash_transactions_cash_bank_account_id_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_cash_transactions_cash_bank_account_id_date ON finance.cash_transactions USING btree (cash_bank_account_id, date);


--
-- Name: ix_cash_transactions_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_cash_transactions_number ON finance.cash_transactions USING btree (number) WHERE (number IS NOT NULL);


--
-- Name: ix_cost_centers_code; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_cost_centers_code ON finance.cost_centers USING btree (code);


--
-- Name: ix_customer_advance_applications_customer_receipt_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_advance_applications_customer_receipt_id ON finance.customer_advance_applications USING btree (customer_receipt_id);


--
-- Name: ix_customer_advance_applications_sales_invoice_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_advance_applications_sales_invoice_id ON finance.customer_advance_applications USING btree (sales_invoice_id);


--
-- Name: ix_customer_receipt_allocations_sales_invoice_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_receipt_allocations_sales_invoice_id ON finance.customer_receipt_allocations USING btree (sales_invoice_id);


--
-- Name: ix_customer_receipts_branch_id_receipt_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_receipts_branch_id_receipt_date ON finance.customer_receipts USING btree (branch_id, receipt_date);


--
-- Name: ix_customer_receipts_cash_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_receipts_cash_account_id ON finance.customer_receipts USING btree (cash_account_id);


--
-- Name: ix_customer_receipts_cash_bank_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_receipts_cash_bank_account_id ON finance.customer_receipts USING btree (cash_bank_account_id);


--
-- Name: ix_customer_receipts_customer_id_receipt_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_customer_receipts_customer_id_receipt_date ON finance.customer_receipts USING btree (customer_id, receipt_date);


--
-- Name: ix_customer_receipts_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_customer_receipts_number ON finance.customer_receipts USING btree (number);


--
-- Name: ix_fiscal_periods_start_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_fiscal_periods_start_date ON finance.fiscal_periods USING btree (start_date);


--
-- Name: ix_fiscal_periods_year_month; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_fiscal_periods_year_month ON finance.fiscal_periods USING btree (year, month);


--
-- Name: ix_journal_entries_branch_id_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_entries_branch_id_date ON finance.journal_entries USING btree (branch_id, date);


--
-- Name: ix_journal_entries_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_journal_entries_number ON finance.journal_entries USING btree (number) WHERE (number IS NOT NULL);


--
-- Name: ix_journal_entries_reversal_of_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_entries_reversal_of_id ON finance.journal_entries USING btree (reversal_of_id);


--
-- Name: ix_journal_entries_reversed_by_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_entries_reversed_by_id ON finance.journal_entries USING btree (reversed_by_id);


--
-- Name: ix_journal_entries_source_type_source_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_journal_entries_source_type_source_id ON finance.journal_entries USING btree (source_type, source_id) WHERE (source_id IS NOT NULL);


--
-- Name: ix_journal_entries_status_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_entries_status_date ON finance.journal_entries USING btree (status, date);


--
-- Name: ix_journal_lines_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_lines_account_id ON finance.journal_lines USING btree (account_id);


--
-- Name: ix_journal_lines_cost_center_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_lines_cost_center_id ON finance.journal_lines USING btree (cost_center_id);


--
-- Name: ix_journal_mapping_lines_cost_center_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_mapping_lines_cost_center_id ON finance.journal_mapping_lines USING btree (cost_center_id);


--
-- Name: ix_journal_mapping_lines_credit_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_mapping_lines_credit_account_id ON finance.journal_mapping_lines USING btree (credit_account_id);


--
-- Name: ix_journal_mapping_lines_debit_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_mapping_lines_debit_account_id ON finance.journal_mapping_lines USING btree (debit_account_id);


--
-- Name: ix_journal_mappings_branch_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_mappings_branch_id ON finance.journal_mappings USING btree (branch_id);


--
-- Name: ix_journal_mappings_event_type_branch_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_journal_mappings_event_type_branch_id ON finance.journal_mappings USING btree (event_type, branch_id) NULLS NOT DISTINCT;


--
-- Name: ix_journal_template_lines_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_template_lines_account_id ON finance.journal_template_lines USING btree (account_id);


--
-- Name: ix_journal_template_lines_cost_center_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_journal_template_lines_cost_center_id ON finance.journal_template_lines USING btree (cost_center_id);


--
-- Name: ix_journal_templates_name; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_journal_templates_name ON finance.journal_templates USING btree (name);


--
-- Name: ix_payment_voucher_allocations_vendor_invoice_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_voucher_allocations_vendor_invoice_id ON finance.payment_voucher_allocations USING btree (vendor_invoice_id);


--
-- Name: ix_payment_voucher_settlement_allocations_plasma_settlement_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_voucher_settlement_allocations_plasma_settlement_id ON finance.payment_voucher_settlement_allocations USING btree (plasma_settlement_id);


--
-- Name: ix_payment_vouchers_branch_id_payment_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_vouchers_branch_id_payment_date ON finance.payment_vouchers USING btree (branch_id, payment_date);


--
-- Name: ix_payment_vouchers_cash_bank_account_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_vouchers_cash_bank_account_id ON finance.payment_vouchers USING btree (cash_bank_account_id);


--
-- Name: ix_payment_vouchers_farmer_id_status; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_vouchers_farmer_id_status ON finance.payment_vouchers USING btree (farmer_id, status);


--
-- Name: ix_payment_vouchers_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_payment_vouchers_number ON finance.payment_vouchers USING btree (number);


--
-- Name: ix_payment_vouchers_vendor_id_status; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_payment_vouchers_vendor_id_status ON finance.payment_vouchers USING btree (vendor_id, status);


--
-- Name: ix_vendor_invoice_lines_goods_receipt_id_goods_receipt_line_nu; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoice_lines_goods_receipt_id_goods_receipt_line_nu ON finance.vendor_invoice_lines USING btree (goods_receipt_id, goods_receipt_line_number);


--
-- Name: ix_vendor_invoice_lines_item_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoice_lines_item_id ON finance.vendor_invoice_lines USING btree (item_id);


--
-- Name: ix_vendor_invoice_lines_purchase_order_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoice_lines_purchase_order_id ON finance.vendor_invoice_lines USING btree (purchase_order_id);


--
-- Name: ix_vendor_invoice_lines_tax_code_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoice_lines_tax_code_id ON finance.vendor_invoice_lines USING btree (tax_code_id);


--
-- Name: ix_vendor_invoice_lines_uom_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoice_lines_uom_id ON finance.vendor_invoice_lines USING btree (uom_id);


--
-- Name: ix_vendor_invoices_branch_id_invoice_date; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoices_branch_id_invoice_date ON finance.vendor_invoices USING btree (branch_id, invoice_date);


--
-- Name: ix_vendor_invoices_income_tax_code_id; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoices_income_tax_code_id ON finance.vendor_invoices USING btree (income_tax_code_id);


--
-- Name: ix_vendor_invoices_number; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_vendor_invoices_number ON finance.vendor_invoices USING btree (number) WHERE (number IS NOT NULL);


--
-- Name: ix_vendor_invoices_vendor_id_status; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX ix_vendor_invoices_vendor_id_status ON finance.vendor_invoices USING btree (vendor_id, status);


--
-- Name: ix_vendor_invoices_vendor_id_vendor_invoice_number_active; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE UNIQUE INDEX ix_vendor_invoices_vendor_id_vendor_invoice_number_active ON finance.vendor_invoices USING btree (vendor_id, vendor_invoice_number) WHERE ((status)::text <> 'Cancelled'::text);


--
-- Name: ix_branch_access_profile_branches_branch_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_branch_access_profile_branches_branch_id ON identity.branch_access_profile_branches USING btree (branch_id);


--
-- Name: ix_branch_access_profiles_name; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_branch_access_profiles_name ON identity.branch_access_profiles USING btree (name);


--
-- Name: ix_menu_access_profile_items_menu_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_menu_access_profile_items_menu_id ON identity.menu_access_profile_items USING btree (menu_id);


--
-- Name: ix_menu_access_profiles_name; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_menu_access_profiles_name ON identity.menu_access_profiles USING btree (name);


--
-- Name: ix_menus_code; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_menus_code ON identity.menus USING btree (code);


--
-- Name: ix_refresh_tokens_token; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_refresh_tokens_token ON identity.refresh_tokens USING btree (token);


--
-- Name: ix_refresh_tokens_user_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_refresh_tokens_user_id ON identity.refresh_tokens USING btree (user_id);


--
-- Name: ix_roles_name; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_roles_name ON identity.roles USING btree (name);


--
-- Name: ix_user_old_email; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_user_old_email ON identity.user_old USING btree (email);


--
-- Name: ix_user_old_user_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_user_old_user_id ON identity.user_old USING btree (user_id);


--
-- Name: ix_user_roles_role_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_user_roles_role_id ON identity.user_roles USING btree (role_id);


--
-- Name: ix_users_branch_access_profile_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_users_branch_access_profile_id ON identity.users USING btree (branch_access_profile_id);


--
-- Name: ix_users_default_branch_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_users_default_branch_id ON identity.users USING btree (default_branch_id);


--
-- Name: ix_users_email; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE UNIQUE INDEX ix_users_email ON identity.users USING btree (email);


--
-- Name: ix_users_menu_access_profile_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX ix_users_menu_access_profile_id ON identity.users USING btree (menu_access_profile_id);


--
-- Name: ix_audit_logs_entity_type_entity_id; Type: INDEX; Schema: infrastructure; Owner: postgres
--

CREATE INDEX ix_audit_logs_entity_type_entity_id ON infrastructure.audit_logs USING btree (entity_type, entity_id);


--
-- Name: ix_audit_logs_occurred_at_utc; Type: INDEX; Schema: infrastructure; Owner: postgres
--

CREATE INDEX ix_audit_logs_occurred_at_utc ON infrastructure.audit_logs USING btree (occurred_at_utc DESC);


--
-- Name: ix_audit_logs_user_id_occurred_at_utc; Type: INDEX; Schema: infrastructure; Owner: postgres
--

CREATE INDEX ix_audit_logs_user_id_occurred_at_utc ON infrastructure.audit_logs USING btree (user_id, occurred_at_utc);


--
-- Name: ix_outbox_messages_unprocessed; Type: INDEX; Schema: infrastructure; Owner: postgres
--

CREATE INDEX ix_outbox_messages_unprocessed ON infrastructure.outbox_messages USING btree (occurred_on_utc) WHERE (processed_on_utc IS NULL);


--
-- Name: ix_goods_receipt_lines_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipt_lines_item_id ON inventory.goods_receipt_lines USING btree (item_id);


--
-- Name: ix_goods_receipt_lines_uom_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipt_lines_uom_id ON inventory.goods_receipt_lines USING btree (uom_id);


--
-- Name: ix_goods_receipts_branch_id_receipt_date; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipts_branch_id_receipt_date ON inventory.goods_receipts USING btree (branch_id, receipt_date);


--
-- Name: ix_goods_receipts_cycle_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipts_cycle_id ON inventory.goods_receipts USING btree (cycle_id);


--
-- Name: ix_goods_receipts_number; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE UNIQUE INDEX ix_goods_receipts_number ON inventory.goods_receipts USING btree (number);


--
-- Name: ix_goods_receipts_purchase_order_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipts_purchase_order_id ON inventory.goods_receipts USING btree (purchase_order_id);


--
-- Name: ix_goods_receipts_vendor_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipts_vendor_id ON inventory.goods_receipts USING btree (vendor_id);


--
-- Name: ix_goods_receipts_warehouse_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_goods_receipts_warehouse_id ON inventory.goods_receipts USING btree (warehouse_id);


--
-- Name: ix_stock_balances_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_balances_item_id ON inventory.stock_balances USING btree (item_id);


--
-- Name: ix_stock_balances_warehouse_id_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE UNIQUE INDEX ix_stock_balances_warehouse_id_item_id ON inventory.stock_balances USING btree (warehouse_id, item_id);


--
-- Name: ix_stock_ledger_entries_cycle_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_ledger_entries_cycle_id ON inventory.stock_ledger_entries USING btree (cycle_id);


--
-- Name: ix_stock_ledger_entries_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_ledger_entries_item_id ON inventory.stock_ledger_entries USING btree (item_id);


--
-- Name: ix_stock_ledger_entries_source_type_source_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_ledger_entries_source_type_source_id ON inventory.stock_ledger_entries USING btree (source_type, source_id);


--
-- Name: ix_stock_ledger_entries_warehouse_id_item_id_date; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_ledger_entries_warehouse_id_item_id_date ON inventory.stock_ledger_entries USING btree (warehouse_id, item_id, date);


--
-- Name: ix_stock_return_lines_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_return_lines_item_id ON inventory.stock_return_lines USING btree (item_id);


--
-- Name: ix_stock_return_lines_uom_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_return_lines_uom_id ON inventory.stock_return_lines USING btree (uom_id);


--
-- Name: ix_stock_returns_branch_id_return_date; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_returns_branch_id_return_date ON inventory.stock_returns USING btree (branch_id, return_date);


--
-- Name: ix_stock_returns_cycle_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_returns_cycle_id ON inventory.stock_returns USING btree (cycle_id);


--
-- Name: ix_stock_returns_from_warehouse_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_returns_from_warehouse_id ON inventory.stock_returns USING btree (from_warehouse_id);


--
-- Name: ix_stock_returns_number; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE UNIQUE INDEX ix_stock_returns_number ON inventory.stock_returns USING btree (number);


--
-- Name: ix_stock_returns_to_warehouse_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_returns_to_warehouse_id ON inventory.stock_returns USING btree (to_warehouse_id);


--
-- Name: ix_stock_transfer_lines_item_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfer_lines_item_id ON inventory.stock_transfer_lines USING btree (item_id);


--
-- Name: ix_stock_transfer_lines_uom_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfer_lines_uom_id ON inventory.stock_transfer_lines USING btree (uom_id);


--
-- Name: ix_stock_transfers_branch_id_transfer_date; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfers_branch_id_transfer_date ON inventory.stock_transfers USING btree (branch_id, transfer_date);


--
-- Name: ix_stock_transfers_cycle_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfers_cycle_id ON inventory.stock_transfers USING btree (cycle_id);


--
-- Name: ix_stock_transfers_from_warehouse_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfers_from_warehouse_id ON inventory.stock_transfers USING btree (from_warehouse_id);


--
-- Name: ix_stock_transfers_number; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE UNIQUE INDEX ix_stock_transfers_number ON inventory.stock_transfers USING btree (number);


--
-- Name: ix_stock_transfers_to_warehouse_id; Type: INDEX; Schema: inventory; Owner: postgres
--

CREATE INDEX ix_stock_transfers_to_warehouse_id ON inventory.stock_transfers USING btree (to_warehouse_id);


--
-- Name: ix_branches_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_branches_code ON master.branches USING btree (code);


--
-- Name: ix_coops_branch_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_coops_branch_id ON master.coops USING btree (branch_id);


--
-- Name: ix_coops_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_coops_code ON master.coops USING btree (code);


--
-- Name: ix_coops_farmer_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_coops_farmer_id ON master.coops USING btree (farmer_id);


--
-- Name: ix_customers_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_customers_code ON master.customers USING btree (code);


--
-- Name: ix_farmers_branch_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_farmers_branch_id ON master.farmers USING btree (branch_id);


--
-- Name: ix_farmers_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_farmers_code ON master.farmers USING btree (code);


--
-- Name: ix_item_uom_conversions_uom_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_item_uom_conversions_uom_id ON master.item_uom_conversions USING btree (uom_id);


--
-- Name: ix_items_base_uom_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_items_base_uom_id ON master.items USING btree (base_uom_id);


--
-- Name: ix_items_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_items_code ON master.items USING btree (code);


--
-- Name: ix_items_tax_code_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_items_tax_code_id ON master.items USING btree (tax_code_id);


--
-- Name: ix_tax_codes_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_tax_codes_code ON master.tax_codes USING btree (code);


--
-- Name: ix_uoms_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_uoms_code ON master.uoms USING btree (code);


--
-- Name: ix_vendors_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_vendors_code ON master.vendors USING btree (code);


--
-- Name: ix_warehouses_branch_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE INDEX ix_warehouses_branch_id ON master.warehouses USING btree (branch_id);


--
-- Name: ix_warehouses_code; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_warehouses_code ON master.warehouses USING btree (code);


--
-- Name: ix_warehouses_coop_id; Type: INDEX; Schema: master; Owner: postgres
--

CREATE UNIQUE INDEX ix_warehouses_coop_id ON master.warehouses USING btree (coop_id) WHERE (coop_id IS NOT NULL);


--
-- Name: ix_contract_input_prices_item_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_contract_input_prices_item_id ON partnership.contract_input_prices USING btree (item_id);


--
-- Name: ix_contracts_branch_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_contracts_branch_id ON partnership.contracts USING btree (branch_id);


--
-- Name: ix_contracts_code; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE UNIQUE INDEX ix_contracts_code ON partnership.contracts USING btree (code);


--
-- Name: ix_contracts_income_tax_code_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_contracts_income_tax_code_id ON partnership.contracts USING btree (income_tax_code_id);


--
-- Name: ix_cycle_harvests_cycle_id_date; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_cycle_harvests_cycle_id_date ON partnership.cycle_harvests USING btree (cycle_id, date);


--
-- Name: ix_production_cycles_branch_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_production_cycles_branch_id ON partnership.production_cycles USING btree (branch_id);


--
-- Name: ix_production_cycles_contract_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_production_cycles_contract_id ON partnership.production_cycles USING btree (contract_id);


--
-- Name: ix_production_cycles_coop_id_open; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE UNIQUE INDEX ix_production_cycles_coop_id_open ON partnership.production_cycles USING btree (coop_id) WHERE ((status)::text = ANY ((ARRAY['Planned'::character varying, 'Active'::character varying, 'Harvesting'::character varying])::text[]));


--
-- Name: ix_production_cycles_farmer_id; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE INDEX ix_production_cycles_farmer_id ON partnership.production_cycles USING btree (farmer_id);


--
-- Name: ix_production_cycles_number; Type: INDEX; Schema: partnership; Owner: postgres
--

CREATE UNIQUE INDEX ix_production_cycles_number ON partnership.production_cycles USING btree (number);


--
-- Name: ix_purchase_order_lines_item_id; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE INDEX ix_purchase_order_lines_item_id ON procurement.purchase_order_lines USING btree (item_id);


--
-- Name: ix_purchase_order_lines_tax_code_id; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE INDEX ix_purchase_order_lines_tax_code_id ON procurement.purchase_order_lines USING btree (tax_code_id);


--
-- Name: ix_purchase_order_lines_uom_id; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE INDEX ix_purchase_order_lines_uom_id ON procurement.purchase_order_lines USING btree (uom_id);


--
-- Name: ix_purchase_orders_branch_id_order_date; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE INDEX ix_purchase_orders_branch_id_order_date ON procurement.purchase_orders USING btree (branch_id, order_date);


--
-- Name: ix_purchase_orders_number; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE UNIQUE INDEX ix_purchase_orders_number ON procurement.purchase_orders USING btree (number);


--
-- Name: ix_purchase_orders_vendor_id; Type: INDEX; Schema: procurement; Owner: postgres
--

CREATE INDEX ix_purchase_orders_vendor_id ON procurement.purchase_orders USING btree (vendor_id);


--
-- Name: ix_daily_recording_usages_item_id; Type: INDEX; Schema: production; Owner: postgres
--

CREATE INDEX ix_daily_recording_usages_item_id ON production.daily_recording_usages USING btree (item_id);


--
-- Name: ix_daily_recording_usages_uom_id; Type: INDEX; Schema: production; Owner: postgres
--

CREATE INDEX ix_daily_recording_usages_uom_id ON production.daily_recording_usages USING btree (uom_id);


--
-- Name: ix_daily_recordings_branch_id_date; Type: INDEX; Schema: production; Owner: postgres
--

CREATE INDEX ix_daily_recordings_branch_id_date ON production.daily_recordings USING btree (branch_id, date);


--
-- Name: ix_daily_recordings_coop_id; Type: INDEX; Schema: production; Owner: postgres
--

CREATE INDEX ix_daily_recordings_coop_id ON production.daily_recordings USING btree (coop_id);


--
-- Name: ix_daily_recordings_cycle_id_date; Type: INDEX; Schema: production; Owner: postgres
--

CREATE UNIQUE INDEX ix_daily_recordings_cycle_id_date ON production.daily_recordings USING btree (cycle_id, date);


--
-- Name: ix_delivery_order_lines_cycle_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_order_lines_cycle_id ON sales.delivery_order_lines USING btree (cycle_id);


--
-- Name: ix_delivery_order_lines_harvest_id_active; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE UNIQUE INDEX ix_delivery_order_lines_harvest_id_active ON sales.delivery_order_lines USING btree (harvest_id) WHERE (is_cancelled = false);


--
-- Name: ix_delivery_order_lines_item_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_order_lines_item_id ON sales.delivery_order_lines USING btree (item_id);


--
-- Name: ix_delivery_order_lines_tax_code_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_order_lines_tax_code_id ON sales.delivery_order_lines USING btree (tax_code_id);


--
-- Name: ix_delivery_orders_branch_id_delivery_date; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_orders_branch_id_delivery_date ON sales.delivery_orders USING btree (branch_id, delivery_date);


--
-- Name: ix_delivery_orders_customer_id_status; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_orders_customer_id_status ON sales.delivery_orders USING btree (customer_id, status);


--
-- Name: ix_delivery_orders_number; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE UNIQUE INDEX ix_delivery_orders_number ON sales.delivery_orders USING btree (number);


--
-- Name: ix_delivery_orders_sales_invoice_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_orders_sales_invoice_id ON sales.delivery_orders USING btree (sales_invoice_id);


--
-- Name: ix_delivery_orders_sales_order_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_delivery_orders_sales_order_id ON sales.delivery_orders USING btree (sales_order_id);


--
-- Name: ix_sales_credit_note_lines_cycle_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_credit_note_lines_cycle_id ON sales.sales_credit_note_lines USING btree (cycle_id);


--
-- Name: ix_sales_credit_notes_branch_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_credit_notes_branch_id ON sales.sales_credit_notes USING btree (branch_id);


--
-- Name: ix_sales_credit_notes_customer_id_date; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_credit_notes_customer_id_date ON sales.sales_credit_notes USING btree (customer_id, date);


--
-- Name: ix_sales_credit_notes_number; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE UNIQUE INDEX ix_sales_credit_notes_number ON sales.sales_credit_notes USING btree (number);


--
-- Name: ix_sales_credit_notes_sales_invoice_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_credit_notes_sales_invoice_id ON sales.sales_credit_notes USING btree (sales_invoice_id);


--
-- Name: ix_sales_invoice_lines_cycle_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoice_lines_cycle_id ON sales.sales_invoice_lines USING btree (cycle_id);


--
-- Name: ix_sales_invoice_lines_delivery_order_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoice_lines_delivery_order_id ON sales.sales_invoice_lines USING btree (delivery_order_id);


--
-- Name: ix_sales_invoice_lines_item_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoice_lines_item_id ON sales.sales_invoice_lines USING btree (item_id);


--
-- Name: ix_sales_invoice_lines_tax_code_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoice_lines_tax_code_id ON sales.sales_invoice_lines USING btree (tax_code_id);


--
-- Name: ix_sales_invoices_branch_id_invoice_date; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoices_branch_id_invoice_date ON sales.sales_invoices USING btree (branch_id, invoice_date);


--
-- Name: ix_sales_invoices_customer_id_status; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_invoices_customer_id_status ON sales.sales_invoices USING btree (customer_id, status);


--
-- Name: ix_sales_invoices_number; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE UNIQUE INDEX ix_sales_invoices_number ON sales.sales_invoices USING btree (number) WHERE (number IS NOT NULL);


--
-- Name: ix_sales_order_lines_item_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_order_lines_item_id ON sales.sales_order_lines USING btree (item_id);


--
-- Name: ix_sales_order_lines_tax_code_id; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_order_lines_tax_code_id ON sales.sales_order_lines USING btree (tax_code_id);


--
-- Name: ix_sales_orders_branch_id_order_date; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_orders_branch_id_order_date ON sales.sales_orders USING btree (branch_id, order_date);


--
-- Name: ix_sales_orders_customer_id_status; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE INDEX ix_sales_orders_customer_id_status ON sales.sales_orders USING btree (customer_id, status);


--
-- Name: ix_sales_orders_number; Type: INDEX; Schema: sales; Owner: postgres
--

CREATE UNIQUE INDEX ix_sales_orders_number ON sales.sales_orders USING btree (number);


--
-- Name: plasma_settlement_lines fk_plasma_settlement_lines_plasma_settlements_plasma_settlemen; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlement_lines
    ADD CONSTRAINT fk_plasma_settlement_lines_plasma_settlements_plasma_settlemen FOREIGN KEY (plasma_settlement_id) REFERENCES costing.plasma_settlements(id) ON DELETE CASCADE;


--
-- Name: plasma_settlements fk_plasma_settlements_branches_branch_id; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT fk_plasma_settlements_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: plasma_settlements fk_plasma_settlements_contracts_contract_id; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT fk_plasma_settlements_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES partnership.contracts(id) ON DELETE RESTRICT;


--
-- Name: plasma_settlements fk_plasma_settlements_farmers_farmer_id; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT fk_plasma_settlements_farmers_farmer_id FOREIGN KEY (farmer_id) REFERENCES master.farmers(id) ON DELETE RESTRICT;


--
-- Name: plasma_settlements fk_plasma_settlements_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT fk_plasma_settlements_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: plasma_settlements fk_plasma_settlements_tax_codes_income_tax_code_id; Type: FK CONSTRAINT; Schema: costing; Owner: postgres
--

ALTER TABLE ONLY costing.plasma_settlements
    ADD CONSTRAINT fk_plasma_settlements_tax_codes_income_tax_code_id FOREIGN KEY (income_tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: attachment_links fk_attachment_links_attachments_attachment_id; Type: FK CONSTRAINT; Schema: documents; Owner: postgres
--

ALTER TABLE ONLY documents.attachment_links
    ADD CONSTRAINT fk_attachment_links_attachments_attachment_id FOREIGN KEY (attachment_id) REFERENCES documents.attachments(id) ON DELETE CASCADE;


--
-- Name: accounts fk_accounts_accounts_parent_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.accounts
    ADD CONSTRAINT fk_accounts_accounts_parent_id FOREIGN KEY (parent_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: bank_reconciliations fk_bank_reconciliations_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_reconciliations
    ADD CONSTRAINT fk_bank_reconciliations_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: bank_reconciliations fk_bank_reconciliations_cash_bank_accounts_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_reconciliations
    ADD CONSTRAINT fk_bank_reconciliations_cash_bank_accounts_cash_bank_account_id FOREIGN KEY (cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: bank_statement_lines fk_bank_statement_lines_bank_reconciliations_bank_reconciliati; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_statement_lines
    ADD CONSTRAINT fk_bank_statement_lines_bank_reconciliations_bank_reconciliati FOREIGN KEY (bank_reconciliation_id) REFERENCES finance.bank_reconciliations(id) ON DELETE CASCADE;


--
-- Name: bank_statement_lines fk_bank_statement_lines_journal_entries_matched_journal_entry_; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_statement_lines
    ADD CONSTRAINT fk_bank_statement_lines_journal_entries_matched_journal_entry_ FOREIGN KEY (matched_journal_entry_id) REFERENCES finance.journal_entries(id) ON DELETE RESTRICT;


--
-- Name: bank_transfers fk_bank_transfers_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_transfers
    ADD CONSTRAINT fk_bank_transfers_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: bank_transfers fk_bank_transfers_cash_bank_accounts_from_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_transfers
    ADD CONSTRAINT fk_bank_transfers_cash_bank_accounts_from_cash_bank_account_id FOREIGN KEY (from_cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: bank_transfers fk_bank_transfers_cash_bank_accounts_to_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.bank_transfers
    ADD CONSTRAINT fk_bank_transfers_cash_bank_accounts_to_cash_bank_account_id FOREIGN KEY (to_cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: cash_bank_accounts fk_cash_bank_accounts_accounts_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_bank_accounts
    ADD CONSTRAINT fk_cash_bank_accounts_accounts_account_id FOREIGN KEY (account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: cash_bank_accounts fk_cash_bank_accounts_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_bank_accounts
    ADD CONSTRAINT fk_cash_bank_accounts_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: cash_transaction_lines fk_cash_transaction_lines_accounts_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transaction_lines
    ADD CONSTRAINT fk_cash_transaction_lines_accounts_account_id FOREIGN KEY (account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: cash_transaction_lines fk_cash_transaction_lines_cash_transactions_cash_transaction_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transaction_lines
    ADD CONSTRAINT fk_cash_transaction_lines_cash_transactions_cash_transaction_id FOREIGN KEY (cash_transaction_id) REFERENCES finance.cash_transactions(id) ON DELETE CASCADE;


--
-- Name: cash_transaction_lines fk_cash_transaction_lines_cost_centers_cost_center_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transaction_lines
    ADD CONSTRAINT fk_cash_transaction_lines_cost_centers_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES finance.cost_centers(id) ON DELETE RESTRICT;


--
-- Name: cash_transactions fk_cash_transactions_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transactions
    ADD CONSTRAINT fk_cash_transactions_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: cash_transactions fk_cash_transactions_cash_bank_accounts_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.cash_transactions
    ADD CONSTRAINT fk_cash_transactions_cash_bank_accounts_cash_bank_account_id FOREIGN KEY (cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: customer_advance_applications fk_customer_advance_applications_customer_receipts_customer_re; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_advance_applications
    ADD CONSTRAINT fk_customer_advance_applications_customer_receipts_customer_re FOREIGN KEY (customer_receipt_id) REFERENCES finance.customer_receipts(id) ON DELETE CASCADE;


--
-- Name: customer_advance_applications fk_customer_advance_applications_sales_invoices_sales_invoice_; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_advance_applications
    ADD CONSTRAINT fk_customer_advance_applications_sales_invoices_sales_invoice_ FOREIGN KEY (sales_invoice_id) REFERENCES sales.sales_invoices(id) ON DELETE RESTRICT;


--
-- Name: customer_receipt_allocations fk_customer_receipt_allocations_customer_receipts_customer_rec; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipt_allocations
    ADD CONSTRAINT fk_customer_receipt_allocations_customer_receipts_customer_rec FOREIGN KEY (customer_receipt_id) REFERENCES finance.customer_receipts(id) ON DELETE CASCADE;


--
-- Name: customer_receipt_allocations fk_customer_receipt_allocations_sales_invoices_sales_invoice_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipt_allocations
    ADD CONSTRAINT fk_customer_receipt_allocations_sales_invoices_sales_invoice_id FOREIGN KEY (sales_invoice_id) REFERENCES sales.sales_invoices(id) ON DELETE RESTRICT;


--
-- Name: customer_receipts fk_customer_receipts_accounts_cash_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipts
    ADD CONSTRAINT fk_customer_receipts_accounts_cash_account_id FOREIGN KEY (cash_account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: customer_receipts fk_customer_receipts_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipts
    ADD CONSTRAINT fk_customer_receipts_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: customer_receipts fk_customer_receipts_cash_bank_accounts_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipts
    ADD CONSTRAINT fk_customer_receipts_cash_bank_accounts_cash_bank_account_id FOREIGN KEY (cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: customer_receipts fk_customer_receipts_customers_customer_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.customer_receipts
    ADD CONSTRAINT fk_customer_receipts_customers_customer_id FOREIGN KEY (customer_id) REFERENCES master.customers(id) ON DELETE RESTRICT;


--
-- Name: journal_entries fk_journal_entries_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_entries
    ADD CONSTRAINT fk_journal_entries_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: journal_entries fk_journal_entries_journal_entries_reversal_of_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_entries
    ADD CONSTRAINT fk_journal_entries_journal_entries_reversal_of_id FOREIGN KEY (reversal_of_id) REFERENCES finance.journal_entries(id) ON DELETE RESTRICT;


--
-- Name: journal_entries fk_journal_entries_journal_entries_reversed_by_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_entries
    ADD CONSTRAINT fk_journal_entries_journal_entries_reversed_by_id FOREIGN KEY (reversed_by_id) REFERENCES finance.journal_entries(id) ON DELETE RESTRICT;


--
-- Name: journal_lines fk_journal_lines_accounts_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_lines
    ADD CONSTRAINT fk_journal_lines_accounts_account_id FOREIGN KEY (account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: journal_lines fk_journal_lines_cost_centers_cost_center_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_lines
    ADD CONSTRAINT fk_journal_lines_cost_centers_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES finance.cost_centers(id) ON DELETE RESTRICT;


--
-- Name: journal_lines fk_journal_lines_journal_entries_journal_entry_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_lines
    ADD CONSTRAINT fk_journal_lines_journal_entries_journal_entry_id FOREIGN KEY (journal_entry_id) REFERENCES finance.journal_entries(id) ON DELETE CASCADE;


--
-- Name: journal_mapping_lines fk_journal_mapping_lines_accounts_credit_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mapping_lines
    ADD CONSTRAINT fk_journal_mapping_lines_accounts_credit_account_id FOREIGN KEY (credit_account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: journal_mapping_lines fk_journal_mapping_lines_accounts_debit_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mapping_lines
    ADD CONSTRAINT fk_journal_mapping_lines_accounts_debit_account_id FOREIGN KEY (debit_account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: journal_mapping_lines fk_journal_mapping_lines_cost_centers_cost_center_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mapping_lines
    ADD CONSTRAINT fk_journal_mapping_lines_cost_centers_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES finance.cost_centers(id) ON DELETE RESTRICT;


--
-- Name: journal_mapping_lines fk_journal_mapping_lines_journal_mappings_journal_mapping_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mapping_lines
    ADD CONSTRAINT fk_journal_mapping_lines_journal_mappings_journal_mapping_id FOREIGN KEY (journal_mapping_id) REFERENCES finance.journal_mappings(id) ON DELETE CASCADE;


--
-- Name: journal_mappings fk_journal_mappings_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_mappings
    ADD CONSTRAINT fk_journal_mappings_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: journal_template_lines fk_journal_template_lines_accounts_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_template_lines
    ADD CONSTRAINT fk_journal_template_lines_accounts_account_id FOREIGN KEY (account_id) REFERENCES finance.accounts(id) ON DELETE RESTRICT;


--
-- Name: journal_template_lines fk_journal_template_lines_cost_centers_cost_center_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_template_lines
    ADD CONSTRAINT fk_journal_template_lines_cost_centers_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES finance.cost_centers(id) ON DELETE RESTRICT;


--
-- Name: journal_template_lines fk_journal_template_lines_journal_templates_journal_template_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.journal_template_lines
    ADD CONSTRAINT fk_journal_template_lines_journal_templates_journal_template_id FOREIGN KEY (journal_template_id) REFERENCES finance.journal_templates(id) ON DELETE CASCADE;


--
-- Name: payment_voucher_allocations fk_payment_voucher_allocations_payment_vouchers_payment_vouche; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_allocations
    ADD CONSTRAINT fk_payment_voucher_allocations_payment_vouchers_payment_vouche FOREIGN KEY (payment_voucher_id) REFERENCES finance.payment_vouchers(id) ON DELETE CASCADE;


--
-- Name: payment_voucher_allocations fk_payment_voucher_allocations_vendor_invoices_vendor_invoice_; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_allocations
    ADD CONSTRAINT fk_payment_voucher_allocations_vendor_invoices_vendor_invoice_ FOREIGN KEY (vendor_invoice_id) REFERENCES finance.vendor_invoices(id) ON DELETE RESTRICT;


--
-- Name: payment_voucher_settlement_allocations fk_payment_voucher_settlement_allocations_payment_vouchers_pay; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_settlement_allocations
    ADD CONSTRAINT fk_payment_voucher_settlement_allocations_payment_vouchers_pay FOREIGN KEY (payment_voucher_id) REFERENCES finance.payment_vouchers(id) ON DELETE CASCADE;


--
-- Name: payment_voucher_settlement_allocations fk_payment_voucher_settlement_allocations_plasma_settlements_p; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_voucher_settlement_allocations
    ADD CONSTRAINT fk_payment_voucher_settlement_allocations_plasma_settlements_p FOREIGN KEY (plasma_settlement_id) REFERENCES costing.plasma_settlements(id) ON DELETE RESTRICT;


--
-- Name: payment_vouchers fk_payment_vouchers_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_vouchers
    ADD CONSTRAINT fk_payment_vouchers_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: payment_vouchers fk_payment_vouchers_cash_bank_accounts_cash_bank_account_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_vouchers
    ADD CONSTRAINT fk_payment_vouchers_cash_bank_accounts_cash_bank_account_id FOREIGN KEY (cash_bank_account_id) REFERENCES finance.cash_bank_accounts(id) ON DELETE RESTRICT;


--
-- Name: payment_vouchers fk_payment_vouchers_farmers_farmer_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_vouchers
    ADD CONSTRAINT fk_payment_vouchers_farmers_farmer_id FOREIGN KEY (farmer_id) REFERENCES master.farmers(id) ON DELETE RESTRICT;


--
-- Name: payment_vouchers fk_payment_vouchers_vendors_vendor_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.payment_vouchers
    ADD CONSTRAINT fk_payment_vouchers_vendors_vendor_id FOREIGN KEY (vendor_id) REFERENCES master.vendors(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_goods_receipts_goods_receipt_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_goods_receipts_goods_receipt_id FOREIGN KEY (goods_receipt_id) REFERENCES inventory.goods_receipts(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_items_item_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id) REFERENCES procurement.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_uoms_uom_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoice_lines fk_vendor_invoice_lines_vendor_invoices_vendor_invoice_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoice_lines
    ADD CONSTRAINT fk_vendor_invoice_lines_vendor_invoices_vendor_invoice_id FOREIGN KEY (vendor_invoice_id) REFERENCES finance.vendor_invoices(id) ON DELETE CASCADE;


--
-- Name: vendor_invoices fk_vendor_invoices_branches_branch_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoices
    ADD CONSTRAINT fk_vendor_invoices_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoices fk_vendor_invoices_tax_codes_income_tax_code_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoices
    ADD CONSTRAINT fk_vendor_invoices_tax_codes_income_tax_code_id FOREIGN KEY (income_tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: vendor_invoices fk_vendor_invoices_vendors_vendor_id; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.vendor_invoices
    ADD CONSTRAINT fk_vendor_invoices_vendors_vendor_id FOREIGN KEY (vendor_id) REFERENCES master.vendors(id) ON DELETE RESTRICT;


--
-- Name: branch_access_profile_branches fk_branch_access_profile_branches_branch_access_profiles_profi; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.branch_access_profile_branches
    ADD CONSTRAINT fk_branch_access_profile_branches_branch_access_profiles_profi FOREIGN KEY (profile_id) REFERENCES identity.branch_access_profiles(id) ON DELETE CASCADE;


--
-- Name: branch_access_profile_branches fk_branch_access_profile_branches_branches_branch_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.branch_access_profile_branches
    ADD CONSTRAINT fk_branch_access_profile_branches_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE CASCADE;


--
-- Name: menu_access_profile_items fk_menu_access_profile_items_menu_access_profiles_profile_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.menu_access_profile_items
    ADD CONSTRAINT fk_menu_access_profile_items_menu_access_profiles_profile_id FOREIGN KEY (profile_id) REFERENCES identity.menu_access_profiles(id) ON DELETE CASCADE;


--
-- Name: menu_access_profile_items fk_menu_access_profile_items_menus_menu_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.menu_access_profile_items
    ADD CONSTRAINT fk_menu_access_profile_items_menus_menu_id FOREIGN KEY (menu_id) REFERENCES identity.menus(id) ON DELETE CASCADE;


--
-- Name: refresh_tokens fk_refresh_tokens_users_user_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.refresh_tokens
    ADD CONSTRAINT fk_refresh_tokens_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users(id) ON DELETE CASCADE;


--
-- Name: role_permissions fk_role_permissions_roles_role_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.role_permissions
    ADD CONSTRAINT fk_role_permissions_roles_role_id FOREIGN KEY (role_id) REFERENCES identity.roles(id) ON DELETE CASCADE;


--
-- Name: user_roles fk_user_roles_roles_role_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT fk_user_roles_roles_role_id FOREIGN KEY (role_id) REFERENCES identity.roles(id) ON DELETE CASCADE;


--
-- Name: user_roles fk_user_roles_users_user_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT fk_user_roles_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users(id) ON DELETE CASCADE;


--
-- Name: users fk_users_branch_access_profiles_branch_access_profile_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT fk_users_branch_access_profiles_branch_access_profile_id FOREIGN KEY (branch_access_profile_id) REFERENCES identity.branch_access_profiles(id) ON DELETE RESTRICT;


--
-- Name: users fk_users_branches_default_branch_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT fk_users_branches_default_branch_id FOREIGN KEY (default_branch_id) REFERENCES master.branches(id) ON DELETE SET NULL;


--
-- Name: users fk_users_menu_access_profiles_menu_access_profile_id; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT fk_users_menu_access_profiles_menu_access_profile_id FOREIGN KEY (menu_access_profile_id) REFERENCES identity.menu_access_profiles(id) ON DELETE RESTRICT;


--
-- Name: goods_receipt_lines fk_goods_receipt_lines_goods_receipts_goods_receipt_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipt_lines
    ADD CONSTRAINT fk_goods_receipt_lines_goods_receipts_goods_receipt_id FOREIGN KEY (goods_receipt_id) REFERENCES inventory.goods_receipts(id) ON DELETE CASCADE;


--
-- Name: goods_receipt_lines fk_goods_receipt_lines_items_item_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipt_lines
    ADD CONSTRAINT fk_goods_receipt_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: goods_receipt_lines fk_goods_receipt_lines_uoms_uom_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipt_lines
    ADD CONSTRAINT fk_goods_receipt_lines_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: goods_receipts fk_goods_receipts_branches_branch_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT fk_goods_receipts_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: goods_receipts fk_goods_receipts_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT fk_goods_receipts_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: goods_receipts fk_goods_receipts_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT fk_goods_receipts_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id) REFERENCES procurement.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: goods_receipts fk_goods_receipts_vendors_vendor_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT fk_goods_receipts_vendors_vendor_id FOREIGN KEY (vendor_id) REFERENCES master.vendors(id) ON DELETE RESTRICT;


--
-- Name: goods_receipts fk_goods_receipts_warehouses_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.goods_receipts
    ADD CONSTRAINT fk_goods_receipts_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_balances fk_stock_balances_items_item_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_balances
    ADD CONSTRAINT fk_stock_balances_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: stock_balances fk_stock_balances_warehouses_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_balances
    ADD CONSTRAINT fk_stock_balances_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_ledger_entries fk_stock_ledger_entries_items_item_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_ledger_entries
    ADD CONSTRAINT fk_stock_ledger_entries_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: stock_ledger_entries fk_stock_ledger_entries_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_ledger_entries
    ADD CONSTRAINT fk_stock_ledger_entries_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: stock_ledger_entries fk_stock_ledger_entries_warehouses_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_ledger_entries
    ADD CONSTRAINT fk_stock_ledger_entries_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_return_lines fk_stock_return_lines_items_item_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_return_lines
    ADD CONSTRAINT fk_stock_return_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: stock_return_lines fk_stock_return_lines_stock_returns_stock_return_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_return_lines
    ADD CONSTRAINT fk_stock_return_lines_stock_returns_stock_return_id FOREIGN KEY (stock_return_id) REFERENCES inventory.stock_returns(id) ON DELETE CASCADE;


--
-- Name: stock_return_lines fk_stock_return_lines_uoms_uom_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_return_lines
    ADD CONSTRAINT fk_stock_return_lines_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: stock_returns fk_stock_returns_branches_branch_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_returns
    ADD CONSTRAINT fk_stock_returns_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: stock_returns fk_stock_returns_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_returns
    ADD CONSTRAINT fk_stock_returns_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: stock_returns fk_stock_returns_warehouses_from_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_returns
    ADD CONSTRAINT fk_stock_returns_warehouses_from_warehouse_id FOREIGN KEY (from_warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_returns fk_stock_returns_warehouses_to_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_returns
    ADD CONSTRAINT fk_stock_returns_warehouses_to_warehouse_id FOREIGN KEY (to_warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_transfer_lines fk_stock_transfer_lines_items_item_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfer_lines
    ADD CONSTRAINT fk_stock_transfer_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: stock_transfer_lines fk_stock_transfer_lines_stock_transfers_stock_transfer_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfer_lines
    ADD CONSTRAINT fk_stock_transfer_lines_stock_transfers_stock_transfer_id FOREIGN KEY (stock_transfer_id) REFERENCES inventory.stock_transfers(id) ON DELETE CASCADE;


--
-- Name: stock_transfer_lines fk_stock_transfer_lines_uoms_uom_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfer_lines
    ADD CONSTRAINT fk_stock_transfer_lines_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: stock_transfers fk_stock_transfers_branches_branch_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfers
    ADD CONSTRAINT fk_stock_transfers_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: stock_transfers fk_stock_transfers_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfers
    ADD CONSTRAINT fk_stock_transfers_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: stock_transfers fk_stock_transfers_warehouses_from_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfers
    ADD CONSTRAINT fk_stock_transfers_warehouses_from_warehouse_id FOREIGN KEY (from_warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: stock_transfers fk_stock_transfers_warehouses_to_warehouse_id; Type: FK CONSTRAINT; Schema: inventory; Owner: postgres
--

ALTER TABLE ONLY inventory.stock_transfers
    ADD CONSTRAINT fk_stock_transfers_warehouses_to_warehouse_id FOREIGN KEY (to_warehouse_id) REFERENCES master.warehouses(id) ON DELETE RESTRICT;


--
-- Name: coops fk_coops_branches_branch_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.coops
    ADD CONSTRAINT fk_coops_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: coops fk_coops_farmers_farmer_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.coops
    ADD CONSTRAINT fk_coops_farmers_farmer_id FOREIGN KEY (farmer_id) REFERENCES master.farmers(id) ON DELETE RESTRICT;


--
-- Name: farmers fk_farmers_branches_branch_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.farmers
    ADD CONSTRAINT fk_farmers_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: item_uom_conversions fk_item_uom_conversions_items_item_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.item_uom_conversions
    ADD CONSTRAINT fk_item_uom_conversions_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE CASCADE;


--
-- Name: item_uom_conversions fk_item_uom_conversions_uoms_uom_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.item_uom_conversions
    ADD CONSTRAINT fk_item_uom_conversions_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: items fk_items_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.items
    ADD CONSTRAINT fk_items_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: items fk_items_uoms_base_uom_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.items
    ADD CONSTRAINT fk_items_uoms_base_uom_id FOREIGN KEY (base_uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: tax_rates fk_tax_rates_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.tax_rates
    ADD CONSTRAINT fk_tax_rates_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE CASCADE;


--
-- Name: warehouses fk_warehouses_branches_branch_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.warehouses
    ADD CONSTRAINT fk_warehouses_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: warehouses fk_warehouses_coops_coop_id; Type: FK CONSTRAINT; Schema: master; Owner: postgres
--

ALTER TABLE ONLY master.warehouses
    ADD CONSTRAINT fk_warehouses_coops_coop_id FOREIGN KEY (coop_id) REFERENCES master.coops(id) ON DELETE RESTRICT;


--
-- Name: contract_incentives fk_contract_incentives_contracts_contract_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_incentives
    ADD CONSTRAINT fk_contract_incentives_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES partnership.contracts(id) ON DELETE CASCADE;


--
-- Name: contract_input_prices fk_contract_input_prices_contracts_contract_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_input_prices
    ADD CONSTRAINT fk_contract_input_prices_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES partnership.contracts(id) ON DELETE CASCADE;


--
-- Name: contract_input_prices fk_contract_input_prices_items_item_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_input_prices
    ADD CONSTRAINT fk_contract_input_prices_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: contract_live_bird_prices fk_contract_live_bird_prices_contracts_contract_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contract_live_bird_prices
    ADD CONSTRAINT fk_contract_live_bird_prices_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES partnership.contracts(id) ON DELETE CASCADE;


--
-- Name: contracts fk_contracts_branches_branch_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contracts
    ADD CONSTRAINT fk_contracts_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: contracts fk_contracts_tax_codes_income_tax_code_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.contracts
    ADD CONSTRAINT fk_contracts_tax_codes_income_tax_code_id FOREIGN KEY (income_tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: cycle_harvests fk_cycle_harvests_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.cycle_harvests
    ADD CONSTRAINT fk_cycle_harvests_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE CASCADE;


--
-- Name: production_cycles fk_production_cycles_branches_branch_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.production_cycles
    ADD CONSTRAINT fk_production_cycles_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: production_cycles fk_production_cycles_contracts_contract_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.production_cycles
    ADD CONSTRAINT fk_production_cycles_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES partnership.contracts(id) ON DELETE RESTRICT;


--
-- Name: production_cycles fk_production_cycles_coops_coop_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.production_cycles
    ADD CONSTRAINT fk_production_cycles_coops_coop_id FOREIGN KEY (coop_id) REFERENCES master.coops(id) ON DELETE RESTRICT;


--
-- Name: production_cycles fk_production_cycles_farmers_farmer_id; Type: FK CONSTRAINT; Schema: partnership; Owner: postgres
--

ALTER TABLE ONLY partnership.production_cycles
    ADD CONSTRAINT fk_production_cycles_farmers_farmer_id FOREIGN KEY (farmer_id) REFERENCES master.farmers(id) ON DELETE RESTRICT;


--
-- Name: purchase_order_lines fk_purchase_order_lines_items_item_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_order_lines
    ADD CONSTRAINT fk_purchase_order_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: purchase_order_lines fk_purchase_order_lines_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_order_lines
    ADD CONSTRAINT fk_purchase_order_lines_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id) REFERENCES procurement.purchase_orders(id) ON DELETE CASCADE;


--
-- Name: purchase_order_lines fk_purchase_order_lines_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_order_lines
    ADD CONSTRAINT fk_purchase_order_lines_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: purchase_order_lines fk_purchase_order_lines_uoms_uom_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_order_lines
    ADD CONSTRAINT fk_purchase_order_lines_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: purchase_orders fk_purchase_orders_branches_branch_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_orders
    ADD CONSTRAINT fk_purchase_orders_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: purchase_orders fk_purchase_orders_vendors_vendor_id; Type: FK CONSTRAINT; Schema: procurement; Owner: postgres
--

ALTER TABLE ONLY procurement.purchase_orders
    ADD CONSTRAINT fk_purchase_orders_vendors_vendor_id FOREIGN KEY (vendor_id) REFERENCES master.vendors(id) ON DELETE RESTRICT;


--
-- Name: daily_recording_revisions fk_daily_recording_revisions_daily_recordings_daily_recording_; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_revisions
    ADD CONSTRAINT fk_daily_recording_revisions_daily_recordings_daily_recording_ FOREIGN KEY (daily_recording_id) REFERENCES production.daily_recordings(id) ON DELETE CASCADE;


--
-- Name: daily_recording_usages fk_daily_recording_usages_daily_recordings_daily_recording_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_usages
    ADD CONSTRAINT fk_daily_recording_usages_daily_recordings_daily_recording_id FOREIGN KEY (daily_recording_id) REFERENCES production.daily_recordings(id) ON DELETE CASCADE;


--
-- Name: daily_recording_usages fk_daily_recording_usages_items_item_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_usages
    ADD CONSTRAINT fk_daily_recording_usages_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: daily_recording_usages fk_daily_recording_usages_uoms_uom_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recording_usages
    ADD CONSTRAINT fk_daily_recording_usages_uoms_uom_id FOREIGN KEY (uom_id) REFERENCES master.uoms(id) ON DELETE RESTRICT;


--
-- Name: daily_recordings fk_daily_recordings_branches_branch_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recordings
    ADD CONSTRAINT fk_daily_recordings_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: daily_recordings fk_daily_recordings_coops_coop_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recordings
    ADD CONSTRAINT fk_daily_recordings_coops_coop_id FOREIGN KEY (coop_id) REFERENCES master.coops(id) ON DELETE RESTRICT;


--
-- Name: daily_recordings fk_daily_recordings_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: production; Owner: postgres
--

ALTER TABLE ONLY production.daily_recordings
    ADD CONSTRAINT fk_daily_recordings_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: delivery_order_lines fk_delivery_order_lines_cycle_harvests_harvest_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT fk_delivery_order_lines_cycle_harvests_harvest_id FOREIGN KEY (harvest_id) REFERENCES partnership.cycle_harvests(id) ON DELETE RESTRICT;


--
-- Name: delivery_order_lines fk_delivery_order_lines_delivery_orders_delivery_order_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT fk_delivery_order_lines_delivery_orders_delivery_order_id FOREIGN KEY (delivery_order_id) REFERENCES sales.delivery_orders(id) ON DELETE CASCADE;


--
-- Name: delivery_order_lines fk_delivery_order_lines_items_item_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT fk_delivery_order_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: delivery_order_lines fk_delivery_order_lines_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT fk_delivery_order_lines_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: delivery_order_lines fk_delivery_order_lines_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_order_lines
    ADD CONSTRAINT fk_delivery_order_lines_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: delivery_orders fk_delivery_orders_branches_branch_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_orders
    ADD CONSTRAINT fk_delivery_orders_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: delivery_orders fk_delivery_orders_customers_customer_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_orders
    ADD CONSTRAINT fk_delivery_orders_customers_customer_id FOREIGN KEY (customer_id) REFERENCES master.customers(id) ON DELETE RESTRICT;


--
-- Name: delivery_orders fk_delivery_orders_sales_invoices_sales_invoice_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_orders
    ADD CONSTRAINT fk_delivery_orders_sales_invoices_sales_invoice_id FOREIGN KEY (sales_invoice_id) REFERENCES sales.sales_invoices(id) ON DELETE RESTRICT;


--
-- Name: delivery_orders fk_delivery_orders_sales_orders_sales_order_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.delivery_orders
    ADD CONSTRAINT fk_delivery_orders_sales_orders_sales_order_id FOREIGN KEY (sales_order_id) REFERENCES sales.sales_orders(id) ON DELETE RESTRICT;


--
-- Name: sales_credit_note_lines fk_sales_credit_note_lines_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_note_lines
    ADD CONSTRAINT fk_sales_credit_note_lines_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: sales_credit_note_lines fk_sales_credit_note_lines_sales_credit_notes_sales_credit_not; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_note_lines
    ADD CONSTRAINT fk_sales_credit_note_lines_sales_credit_notes_sales_credit_not FOREIGN KEY (sales_credit_note_id) REFERENCES sales.sales_credit_notes(id) ON DELETE CASCADE;


--
-- Name: sales_credit_notes fk_sales_credit_notes_branches_branch_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_notes
    ADD CONSTRAINT fk_sales_credit_notes_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: sales_credit_notes fk_sales_credit_notes_customers_customer_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_notes
    ADD CONSTRAINT fk_sales_credit_notes_customers_customer_id FOREIGN KEY (customer_id) REFERENCES master.customers(id) ON DELETE RESTRICT;


--
-- Name: sales_credit_notes fk_sales_credit_notes_sales_invoices_sales_invoice_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_credit_notes
    ADD CONSTRAINT fk_sales_credit_notes_sales_invoices_sales_invoice_id FOREIGN KEY (sales_invoice_id) REFERENCES sales.sales_invoices(id) ON DELETE RESTRICT;


--
-- Name: sales_invoice_lines fk_sales_invoice_lines_delivery_orders_delivery_order_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT fk_sales_invoice_lines_delivery_orders_delivery_order_id FOREIGN KEY (delivery_order_id) REFERENCES sales.delivery_orders(id) ON DELETE RESTRICT;


--
-- Name: sales_invoice_lines fk_sales_invoice_lines_items_item_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT fk_sales_invoice_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: sales_invoice_lines fk_sales_invoice_lines_production_cycles_cycle_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT fk_sales_invoice_lines_production_cycles_cycle_id FOREIGN KEY (cycle_id) REFERENCES partnership.production_cycles(id) ON DELETE RESTRICT;


--
-- Name: sales_invoice_lines fk_sales_invoice_lines_sales_invoices_sales_invoice_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT fk_sales_invoice_lines_sales_invoices_sales_invoice_id FOREIGN KEY (sales_invoice_id) REFERENCES sales.sales_invoices(id) ON DELETE CASCADE;


--
-- Name: sales_invoice_lines fk_sales_invoice_lines_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoice_lines
    ADD CONSTRAINT fk_sales_invoice_lines_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices fk_sales_invoices_branches_branch_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoices
    ADD CONSTRAINT fk_sales_invoices_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices fk_sales_invoices_customers_customer_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_invoices
    ADD CONSTRAINT fk_sales_invoices_customers_customer_id FOREIGN KEY (customer_id) REFERENCES master.customers(id) ON DELETE RESTRICT;


--
-- Name: sales_order_lines fk_sales_order_lines_items_item_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_order_lines
    ADD CONSTRAINT fk_sales_order_lines_items_item_id FOREIGN KEY (item_id) REFERENCES master.items(id) ON DELETE RESTRICT;


--
-- Name: sales_order_lines fk_sales_order_lines_sales_orders_sales_order_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_order_lines
    ADD CONSTRAINT fk_sales_order_lines_sales_orders_sales_order_id FOREIGN KEY (sales_order_id) REFERENCES sales.sales_orders(id) ON DELETE CASCADE;


--
-- Name: sales_order_lines fk_sales_order_lines_tax_codes_tax_code_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_order_lines
    ADD CONSTRAINT fk_sales_order_lines_tax_codes_tax_code_id FOREIGN KEY (tax_code_id) REFERENCES master.tax_codes(id) ON DELETE RESTRICT;


--
-- Name: sales_orders fk_sales_orders_branches_branch_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_orders
    ADD CONSTRAINT fk_sales_orders_branches_branch_id FOREIGN KEY (branch_id) REFERENCES master.branches(id) ON DELETE RESTRICT;


--
-- Name: sales_orders fk_sales_orders_customers_customer_id; Type: FK CONSTRAINT; Schema: sales; Owner: postgres
--

ALTER TABLE ONLY sales.sales_orders
    ADD CONSTRAINT fk_sales_orders_customers_customer_id FOREIGN KEY (customer_id) REFERENCES master.customers(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

