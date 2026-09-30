# Plan Implementasi — Aplikasi Peternakan Ayam Broiler (Inti-Plasma)

> Basis: template Clean Architecture .NET 10 yang sudah ada (SharedKernel, Domain, Application, Infrastructure, Web.Api) + flowchart `design-alur-aplikasi.jpg`.
> Pattern: **DDD** (aggregate, value object, domain event) + **CQRS** (command & query terpisah, lewat `ICommandHandler` / `IQueryHandler` yang sudah ada).

---

## 1. Ringkasan Temuan

### 1.1 Alur bisnis (dari gambar)
| # | Area | Isi utama |
|---|------|-----------|
| 1 | Struktur Peternakan | Peternak (Inti/Plasma) → 1..n Kandang → 1..n Siklus (masa produksi) |
| 2 | Pengadaan Sapronak | Pembelian DOC (kirim langsung ke kandang **atau** via gudang DOC induk), Pakan, OVK → masuk gudang induk |
| 3 | Gudang & Transfer | Gudang Pakan/OVK induk → transfer ke kandang (siklus) sesuai kebutuhan |
| 4 | Produksi | Recording harian (kematian, culling, bobot rata-rata, feed intake/FCR) → tutup siklus (total produksi, mortalitas, FCR, biaya) |
| 5 | Penjualan | Sales Order/Kontrak → Delivery Order (panen) → Sales Invoice (AR) → Penerimaan pembayaran |
| 6 | HPP & Settlement | HPP (pakan, DOC, OVK, lain-lain) per kg/ekor → settlement plasma (bagi hasil, bonus/insentif, potongan, hutang plasma) → jurnal |
| 7 | Finance & Accounting | Master finance (COA, cost center, fiscal period, cabang), General Journal, **Auto Journal Engine**, AP, AR, Cash & Bank, Production Costing, Settlement, GL, Laporan Keuangan |

### 1.2 Kondisi codebase saat ini
- CQRS ringan sudah ada: `ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler` + decorator Validation & Logging (Scrutor).
- `Result` / `Error` pattern di `SharedKernel`, domain event dispatch **setelah** `SaveChanges` (in-process, tidak reliable).
- Entity masih **anemic** (`TodoItem` public setter, tidak ada aggregate root/value object/strongly-typed id).
- Satu `ApplicationDbContext` untuk read & write, schema `public`, PostgreSQL + snake_case.
- `PermissionProvider` masih TODO (belum ada role/permission).
- Test: ArchitectureTests (NetArchTest), unit test (NSubstitute + InMemory), integration (Testcontainers).

---

## 2. Keputusan Arsitektur

### 2.1 Tetap monolith, dipecah per Bounded Context (folder + schema DB)
Tidak memecah menjadi banyak project dulu (menjaga konsistensi template). Setiap bounded context = folder di Domain/Application/Infrastructure/Web.Api **dan** schema PostgreSQL sendiri.

| Bounded Context | Schema | Aggregate utama |
|---|---|---|
| **MasterData** | `master` | Branch, Farmer (Peternak), Coop (Kandang), Item (DOC/Pakan/OVK/Ayam), Uom, Warehouse, Vendor, Customer |
| **Partnership** (Kemitraan) | `partnership` | ProductionCycle (Siklus), PartnershipContract (harga kontrak/skema bonus) |
| **Procurement** | `procurement` | PurchaseOrder, GoodsReceipt |
| **Inventory** | `inventory` | StockTransfer, StockLedger (kartu stok), StockBalance |
| **Production** | `production` | DailyRecording, CycleClosing |
| **Sales** | `sales` | SalesOrder, DeliveryOrder (panen/timbang), SalesInvoice |
| **Costing & Settlement** | `costing` | CycleCost (HPP), PlasmaSettlement |
| **Finance** | `finance` | Account (COA), FiscalPeriod, CostCenter, JournalEntry, JournalTemplate/Mapping, VendorInvoice (AP), Payment, CustomerReceipt (AR), BankAccount, BankReconciliation |
| **Identity** | `identity` | User, Role, Permission (sudah ada User, dilengkapi) |

Antar-context **hanya boleh**: (a) referensi by ID, (b) integration event via Outbox. Tidak ada navigasi EF lintas context.

### 2.2 CQRS
- **Write side**: Command → Handler → load Aggregate lewat repository (`IFarmerRepository`, dll.) → method domain → `IUnitOfWork.SaveChangesAsync`.
- **Read side**: Query → Handler → **Dapper + SQL** (lewat `IDbConnectionFactory`) atau EF `AsNoTracking` projection untuk query sederhana. Laporan berat (GL, Trial Balance, Aging, Laba Rugi, Neraca) pakai SQL view/materialized view.
- Cache HybridCache untuk master data (sudah ada infrastrukturnya).

### 2.3 Domain Events & Outbox (penting untuk Auto Journal)
Auto Journal Engine harus **pasti** terbentuk bila transaksi operasional tersimpan. Karena itu:
1. Domain event disimpan ke tabel `outbox_messages` **dalam transaksi yang sama**.
2. `BackgroundService` (atau Quartz) memproses outbox → publish ke handler (idempotent, tabel `outbox_message_consumers`).
3. Handler di Finance mengubah event → `JournalEntry` berdasarkan **Journal Mapping** (master data: event type → akun debit/kredit + cost center).

### 2.4 Perbaikan SharedKernel
- `Entity<TId>`, `AggregateRoot<TId>` (domain event hanya di aggregate root), `ValueObject`.
- Strongly-typed IDs (`FarmerId`, `CoopId`, `CycleId`, …) sebagai `record struct`.
- Value objects: `Money` (decimal, IDR), `Quantity` (+ `Uom`: ekor, kg, sak, botol), `DocumentNumber`, `Period` (tahun-bulan).
- `IAuditable` (CreatedAt/By, UpdatedAt/By) diisi otomatis lewat EF interceptor; `ISoftDeletable`; concurrency token (`xmin` PostgreSQL).
- Service `IDocumentNumberGenerator` (mis. `PO/BR01/2026/IX/0001`).

### 2.5 Aturan domain kunci (invariant)
- **Kandang** hanya boleh punya **1 siklus aktif**. Siklus: `Draft → ChickIn/Active → Harvesting → Closed → Settled`.
- Sapronak hanya bisa ditransfer/diterima ke siklus **Active**. DOC bisa langsung ke kandang (bypass gudang) — keduanya tetap tercatat di kartu stok.
- Stok tidak boleh negatif per gudang/item.
- Populasi = chick-in − mati − culling − panen; recording harian unik per (siklus, tanggal), tidak boleh melewati populasi.
- Tutup siklus: populasi = 0, semua DO sudah di-invoice. Output: total ekor & kg panen, deplesi %, FCR, ADG, umur rata-rata, **IP (Indeks Performa)**.
- Settlement hanya untuk siklus `Closed`; setelah settlement siklus `Settled` (terkunci).
- Jurnal: total debit = total kredit; jurnal `Posted` immutable (koreksi via **reverse**); tidak bisa posting ke `FiscalPeriod` yang `Closed`.
- AP/AR: pembayaran parsial diperbolehkan, tidak boleh melebihi outstanding.

### 2.6 Contoh mapping Auto Journal
| Event | Debit | Kredit |
|---|---|---|
| GoodsReceived (pakan/OVK/DOC) | Persediaan Sapronak | Hutang Belum Ditagih (GRNI) |
| VendorInvoicePosted | GRNI | Hutang Usaha (AP) |
| StockTransferredToCycle | Persediaan Ayam Dalam Proses (WIP siklus) | Persediaan Sapronak |
| SalesInvoicePosted | Piutang Usaha (AR) | Penjualan |
| CycleCostRecognized (per penjualan/tutup siklus) | HPP | WIP siklus |
| PlasmaSettlementApproved | Beban Kemitraan | Hutang Plasma (net potongan) |
| PaymentVoucherPaid | Hutang Usaha / Hutang Plasma | Kas/Bank |
| CustomerReceiptPosted | Kas/Bank | Piutang Usaha |

Akun di atas tidak di-hardcode; dikonfigurasi di tabel mapping per cabang.

---

## 3. Struktur Folder Target

```
src/
  SharedKernel/        AggregateRoot, Entity<TId>, ValueObject, Money, Quantity, Result, Error
  Domain/
    MasterData/        Farmers/, Coops/, Items/, Warehouses/, Vendors/, Customers/, Branches/
    Partnership/       ProductionCycles/, Contracts/
    Procurement/       PurchaseOrders/, GoodsReceipts/
    Inventory/         StockTransfers/, StockLedgers/
    Production/        DailyRecordings/, CycleClosings/
    Sales/             SalesOrders/, DeliveryOrders/, SalesInvoices/
    Costing/           CycleCosts/, PlasmaSettlements/
    Finance/           Accounts/, FiscalPeriods/, Journals/, JournalMappings/, Payables/, Receivables/, CashBank/
    Identity/          Users/, Roles/
  Application/
    Abstractions/      Messaging/, Data/ (IUnitOfWork, IDbConnectionFactory), Numbering/
    <Context>/<Aggregate>/<UseCase>/   Command + Handler + Validator | Query + Handler + Response
  Infrastructure/
    Database/          ApplicationDbContext, Schemas, Interceptors (Audit, Outbox), Migrations
    Outbox/            OutboxProcessor (BackgroundService)
    <Context>/         EntityConfigurations, Repositories, QueryServices (Dapper)
  Web.Api/
    Endpoints/<Context>/<Aggregate>/…
tests/
  Domain.UnitTests/    (baru) invariant aggregate
  Application.UnitTests/
  ArchitectureTests/   + aturan: tidak ada referensi lintas bounded context di Domain
  IntegrationTests/
```

---

## 4. Tahapan Implementasi (bertahap, tiap fase bisa di-demo)

### Fase 0 — Fondasi ✅ (selesai 2026-09-30, lihat §6)
- Rename solution (`IntiPlasma.slnx`), hapus sample `Todos` (domain, app, endpoint, test, migration → buat migration awal baru).
- SharedKernel: `AggregateRoot`, `ValueObject`, strongly-typed ID, `Money`, `Quantity`.
- `IUnitOfWork`, repository dasar, `IDbConnectionFactory` + Dapper (tambah package).
- EF interceptor: audit, outbox; `OutboxProcessor`.
- Role & Permission (Admin, Finance, Gudang, Technical Service/PPL, Sales, Plasma-viewer) → isi `PermissionProvider`.
- Document numbering, multi-cabang (BranchId di tiap transaksi).
- Pagination/filter standar untuk query.

### Fase 1 — Master Data & Kemitraan ✅ (selesai 2026-09-30, lihat §7)
- CRUD Branch, Farmer (tipe Inti/Plasma, NIK, rekening), Coop (kapasitas, tipe open/closed house), Item & UoM, Warehouse (Induk/Kandang), Vendor, Customer.
- PartnershipContract: harga kontrak DOC/pakan/OVK, harga jaminan ayam per range bobot, skema bonus (FCR, IP, mortalitas).
- ProductionCycle: buka siklus (kandang, kontrak, tanggal chick-in rencana), ubah status.

### Fase 2 — Finance Core (dibuat awal karena semua modul posting ke sini)
- COA (hierarki, group account, tipe: Asset/Liability/Equity/Revenue/Expense), Cost Center, Fiscal Period (open/close).
- JournalEntry: draft → approve → post → reverse; template jurnal; jurnal manual.
- Journal Mapping (Auto Journal Engine) + handler integration event.
- Query: Buku Besar, Ledger Mutation, Trial Balance.

### Fase 3 — Pengadaan & Gudang
- Purchase Order (DOC/Pakan/OVK) → Goods Receipt (ke gudang induk **atau** langsung ke siklus untuk DOC).
- Stock Transfer gudang induk → kandang/siklus; kartu stok; saldo stok; valuasi (moving average).
- Event → auto journal persediaan.

### Fase 4 — Produksi
- Chick-in (dari penerimaan DOC), Daily Recording (mati, culling, bobot rata-rata, pakan terpakai, OVK terpakai).
- Kalkulasi otomatis: populasi, deplesi, FCR, ADG, IP (query read model per siklus/hari).
- Tutup siklus (CycleClosing) + ringkasan performa.

### Fase 5 — Penjualan & AR
- Sales Order/Kontrak customer → Delivery Order (panen, timbangan ekor & kg, per truk) → Sales Invoice.
- Customer Receipt (bank/kas), partial payment, AR ledger & AR aging.

### Fase 6 — AP & Cash/Bank
- Vendor Invoice (3-way match PO–GR–Invoice), Payment Voucher, multi/partial payment, AP ledger & aging.
- Cash In/Out, Petty Cash, Bank Transfer, Bank Reconciliation, Cash & Bank Ledger.

### Fase 7 — HPP & Settlement Plasma
- CycleCost: akumulasi biaya per siklus (DOC, pakan, OVK, biaya lain, alokasi overhead) → HPP per kg & per ekor; HPP harian (estimasi berjalan).
- PlasmaSettlement: pendapatan plasma = nilai ayam @harga kontrak − sapronak @harga kontrak + bonus/insentif − potongan (hutang, denda) → Hutang Plasma → approval → auto journal → pembayaran lewat Payment Voucher.
- Slip settlement per plasma.

### Fase 8 — Laporan Keuangan & Analitik
- Laba Rugi, Neraca, Arus Kas, Analisa Profitabilitas (per siklus, per kandang, per peternak, per cabang).
- Tutup periode (period closing) + validasi.

Per fase, definisi selesai: domain + unit test invariant → command/query + validator → EF config + migration → endpoint + permission → integration test alur utama.

---

## 5. Keputusan (disepakati 2026-09-30)

| # | Topik | Keputusan | Dampak desain |
|---|-------|-----------|---------------|
| 1 | Skema kemitraan | **Diatur per kontrak** | `PartnershipContract.Scheme` = `PriceContract` (harga kontrak sapronak + harga jaminan ayam) atau `ProfitSharing` (persentase bagi hasil laba). Perhitungan settlement memakai *strategy* di domain (`ISettlementPolicy` per skema); bonus/insentif & potongan dikonfigurasi sebagai daftar komponen di kontrak. Kontrak di-*snapshot* ke siklus saat siklus dibuka agar perubahan kontrak tidak mengubah siklus berjalan. |
| 2 | Valuasi persediaan | **Moving Average** | `StockBalance` per (gudang, item) menyimpan qty & `AverageCost`; penerimaan menghitung ulang rata-rata, pengeluaran/transfer memakai rata-rata saat itu. Transfer ke siklus membawa nilai → WIP siklus. Update saldo memakai concurrency token/row lock. |
| 3 | Struktur usaha | **Satu perusahaan, banyak cabang** | COA satu untuk perusahaan; `BranchId` wajib di semua transaksi & baris jurnal (dimensi), bersama `CostCenterId`. Laporan keuangan bisa per cabang/konsolidasi. User punya daftar cabang yang boleh diakses (branch-scoped authorization). Penomoran dokumen per cabang per periode. |
| 4 | Tampilan | **API saja dulu** (menyusul: mobile app untuk PPL, MVC WebApp untuk Admin Office) | API didesain *client-agnostic*: ID boleh dibuat klien (untuk recording offline PPL), header `Idempotency-Key` pada command penting, endpoint sinkronisasi (`?modifiedSince=`), paging standar, API versioning (`/api/v1`). Permission dipisah per peran (PPL vs Admin Office). |
| 5 | Pajak | **Perlu PPN & PPh** | Master `TaxCode` (PPN Keluaran/Masukan, PPh 21/22/23/4(2), status *Dibebaskan/Tidak dipungut*) dengan tarif ber-tanggal-efektif; baris invoice pembelian/penjualan membawa `TaxCode`; NPWP/NIK/NITKU di Vendor, Customer, Peternak. Mapping jurnal pajak (PPN Masukan, PPN Keluaran, Hutang PPh). PPh atas pembayaran settlement plasma dikonfigurasi per kontrak/peternak. Ekspor e-Faktur/Coretax menyusul (Fase 8). ⚠️ Beberapa komoditas (DOC, pakan, ayam hidup) punya fasilitas PPN dibebaskan — tarif final wajib dikonfirmasi dengan konsultan pajak, karena itu semua tarif dibuat konfigurasi, bukan hardcode. |
| 6 | Sample `Todos` | **Dihapus** di Fase 0 | — |

### Tambahan ke fase
- **Fase 0**: + API versioning, idempotency, branch-scoped authorization.
- **Fase 1**: + master `TaxCode`, identitas pajak di Vendor/Customer/Peternak, kontrak dengan skema & komponen bonus/potongan.
- **Fase 3**: + perhitungan moving average di `StockBalance`.
- **Fase 5 & 6**: + PPN/PPh di invoice, jurnal pajak.
- **Fase 7**: + `ISettlementPolicy` (PriceContract / ProfitSharing), PPh atas settlement.
- **Fase 8**: + laporan pajak (rekap PPN masukan/keluaran, bukti potong PPh), ekspor e-Faktur/Coretax.

---

## 6. Realisasi Fase 0 & penyesuaian terhadap rencana

**Yang dibangun**
- `SharedKernel`: `Entity`, `AggregateRoot` (domain events + kolom audit), `DomainEvent` (punya `Id` & `OccurredOnUtc`), `IAuditable`, `ISoftDeletable`, `Money` (IDR, 2 desimal, *round half away from zero*), `PagedList<T>`.
- Identity: aggregate `User` (+ `UserRole`) dan `Role` (+ `RolePermission`), katalog `Permissions` di `Domain/Roles`, role sistem `Administrator` (read-only, otomatis mendapat semua permission baru). Endpoint: `roles` (GET/POST/PUT), `permissions` (GET), `users/{id}/roles` (PUT). `PermissionProvider` membaca DB + HybridCache (invalidasi saat role berubah).
- **Registrasi user hanya untuk admin** (`users:manage`) — cocok untuk sistem internal (akun PPL dibuat admin). Admin awal dibuat dari konfigurasi `Seed:Admin` (dev: `admin@intiplasma.local` / `Admin123!`).
- Outbox: `InsertOutboxMessagesInterceptor` (event → `infrastructure.outbox_messages` dalam transaksi yang sama) + `OutboxProcessor` (`FOR UPDATE SKIP LOCKED`, retry s.d. `Outbox:MaxAttempts`, lalu *dead letter*).
- `AuditableEntitiesInterceptor` (audit + soft delete), concurrency token `xmin` untuk semua aggregate, schema `identity` & `infrastructure`.
- Read side: `IDbConnectionFactory` + Dapper (`GetUserById`, `GetRoles`), satu `NpgsqlDataSource` dipakai EF & Dapper.
- `IDocumentNumberGenerator`: `PO/BR01/2026/IX/0001`, reset per bulan per (prefix, cabang), tanpa gap bila dipanggil dalam transaksi.
- API versioning `/api/v1`, `Idempotency-Key` (`.WithIdempotency()` pada endpoint create).
- Test: `Domain.UnitTests` (baru), architecture test (domain event harus `sealed` turunan `DomainEvent`, entity tanpa public setter), integration test Roles.

**Penyesuaian terhadap rencana awal**
| Rencana | Realisasi | Alasan |
|---|---|---|
| Strongly-typed IDs | `Guid` v7 (`Guid.CreateVersion7()`) | ID berurutan (index-friendly), bisa dibuat klien mobile saat offline, tanpa converter EF/Dapper/binding tambahan. |
| `IUnitOfWork` + repository per aggregate | `IApplicationDbContext` (DbSet = repository) | Konsisten dengan template, lebih sedikit abstraksi. Repository khusus ditambah bila query agregat jadi kompleks. |
| Tabel `outbox_message_consumers` | Idempotensi pakai `DomainEvent.Id` | Handler menyimpan `SourceEventId` unik (mis. di jurnal otomatis) — lebih kuat dari tabel consumer karena satu transaksi dengan data handler. |
| Branch-scoped authorization di Fase 0 | Dipindah ke **Fase 1** | Membutuhkan aggregate `Branch` yang baru dibuat di Fase 1. |
| `Quantity`/`Uom` value object | Dipindah ke **Fase 1/3** | Bergantung pada master UoM. |

**Catatan**
- Integration test butuh Docker (Testcontainers); belum dijalankan di mesin dev tanpa Docker. Alur yang sama sudah diverifikasi manual ke PostgreSQL lokal.
- `IdempotencyFilter` memakai HybridCache in-memory; tambahkan Redis (L2) sebelum API dijalankan multi-instance.

---

## 7. Realisasi Fase 1 — Master Data & Kemitraan

Migration: `Phase1_MasterData_Partnership` (schema `master` & `partnership`).

**Master data (schema `master`)**
| Aggregate | Endpoint (`/api/v1`) | Aturan utama |
|---|---|---|
| Branch (Cabang) | `branches` | Kode ≤10 alfanumerik, *immutable* (dipakai di nomor dokumen). |
| Uom (Satuan) | `uoms` | Seed otomatis: EKOR, KG, GR, SAK, BTL, VIAL, LTR, ML, PCS. |
| TaxCode | `tax-codes` | PPN (`Taxable`/`Exempt`/`NotCollected`) atau PPh (21/22/23/4(2)); tarif ber-tanggal-efektif + rasio DPP (mis. 11/12 untuk PPN 12% DPP nilai lain). **Tidak di-seed** — isi sesuai arahan konsultan pajak. |
| Item | `items` | Kategori DOC/Feed/OVK/LiveBird/Other; satuan dasar + konversi (1 SAK = 50 KG); default kode PPN. |
| Warehouse | `warehouses` | Gudang induk dibuat manual; **gudang kandang dibuat otomatis** (`GK-{kode kandang}`) lewat event `CoopCreated` + outbox. |
| Vendor / Customer | `vendors`, `customers` | Identitas pajak (NPWP 15/16 digit, NITKU 22 digit, PKP wajib NPWP), rekening bank, termin, credit limit. |
| Farmer (Peternak) | `farmers` | Inti/Plasma; Plasma wajib NIK 16 digit; cabang & tipe *immutable*. |
| Coop (Kandang) | `coops` | Milik satu peternak, cabang mengikuti peternak, kapasitas, open/closed house, koordinat. |

**Kemitraan (schema `partnership`)**
- `contracts`: skema `PriceContract` (harga kontrak sapronak per satuan dasar + harga jaminan ayam per rentang bobot, tidak boleh tumpang tindih) atau `ProfitSharing` (% bagi hasil plasma). Komponen bonus/potongan berbasis metrik FCR/IP/Deplesi/Bobot rata-rata (per kg, per ekor, atau lump sum). PPh atas settlement per kontrak. Siklus: `Draft → Active → Inactive`; hanya draft yang bisa diubah.
- `cycles`: rencana siklus → nomor `SKL/{cabang}/{yyyy}/{bulan romawi}/{nnnn}`; *snapshot* kontrak (jsonb) disimpan saat rencana dibuat. Aturan: plasma wajib kontrak aktif & berlaku di tanggal chick-in dan satu cabang; inti tidak boleh berkontrak; populasi ≤ kapasitas; **satu siklus terbuka per kandang** (dicek di aplikasi + partial unique index). Aksi: `start` (chick-in) dan `cancel` (hanya saat Planned). Transisi Harvesting/Closed/Settled menyusul di Fase 4 & 7.

**Otorisasi per cabang**
- `PUT users/{id}/branches` menetapkan cabang user; permission `branches:access-all` untuk kantor pusat (Administrator otomatis punya).
- Warehouse, Farmer, Coop, Contract, Cycle difilter per cabang di semua query; akses ke cabang lain → **403** `Branches.AccessDenied`.
- Permission baru: `branches:*`, `master-data:*`, `warehouses:*`, `farmers:*`, `contracts:*`, `cycles:*` (read/manage).

**Catatan teknis**
- Enum disimpan & dikirim sebagai **nama** (mis. `"Plasma"`, `"Feed"`), bukan angka.
- Endpoint per resource dikelompokkan dalam satu file (`XxxEndpoints.cs`, `MapGroup`) agar tag & permission konsisten.
- List endpoint mendukung `?search=&page=&pageSize=` (maks 100) + filter spesifik (`branchId`, `type`, `category`, `status`, `farmerId`, `coopId`).
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara): semua skenario di atas, termasuk 409 siklus ganda, 403 lintas cabang, dan event outbox tanpa error.
