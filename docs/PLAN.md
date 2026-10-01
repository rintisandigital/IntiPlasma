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

### Fase 2 — Finance Core (dibuat awal karena semua modul posting ke sini) ✅ (selesai 2026-09-30, lihat §8)
- COA (hierarki, group account, tipe: Asset/Liability/Equity/Revenue/Expense), Cost Center, Fiscal Period (open/close).
- JournalEntry: draft → approve → post → reverse; template jurnal; jurnal manual.
- Journal Mapping (Auto Journal Engine) + handler integration event.
- Query: Buku Besar, Ledger Mutation, Trial Balance.

### Fase 3 — Pengadaan & Gudang ✅ (selesai 2026-09-30, lihat §9)
- Purchase Order (DOC/Pakan/OVK) → Goods Receipt (ke gudang induk **atau** langsung ke siklus untuk DOC).
- Stock Transfer gudang induk → kandang/siklus; kartu stok; saldo stok; valuasi (moving average).
- Event → auto journal persediaan.

### Fase 4 — Produksi ✅ (selesai 2026-09-30, lihat §10)
- Chick-in (dari penerimaan DOC), Daily Recording (mati, culling, bobot rata-rata, pakan terpakai, OVK terpakai), Revisi Daily Recording, Mutasi Pakan.
- Kalkulasi otomatis: populasi, deplesi, FCR, ADG, IP (query read model per siklus/hari).
- Tutup siklus (CycleClosing) + ringkasan performa.

### Fase 5 — Penjualan & AR ✅ (selesai 2026-10-01, lihat §11)
- Sales Order/Kontrak customer → Delivery Order (panen, timbangan ekor & kg, per truk) → Sales Invoice.
- Customer Receipt (bank/kas), partial payment, AR ledger & AR aging.

### Fase 6 — AP & Cash/Bank ✅ (selesai 2026-10-01, lihat §12)
- Vendor Invoice (3-way match PO–GR–Invoice), Payment Voucher, multi/partial payment, AP ledger & aging.
- Cash In/Out, Petty Cash, Bank Transfer, Bank Reconciliation, Cash & Bank Ledger.
- Sisa AR dari Fase 5 (keputusan 2026-10-01): *void* penerimaan customer (jurnal pembalik), uang muka penjualan, nota kredit/retur penjualan.

### Fase 7 — HPP & Settlement Plasma ✅ (selesai 2026-10-01, lihat §13)
- CycleCost: akumulasi biaya per siklus (DOC, pakan, OVK, biaya lain, alokasi overhead) → HPP per kg & per ekor; HPP harian (estimasi berjalan).
- PlasmaSettlement: pendapatan plasma = nilai ayam @harga kontrak − sapronak @harga kontrak + bonus/insentif − potongan (hutang, denda) → Hutang Plasma → approval → auto journal → pembayaran lewat Payment Voucher.
- Slip settlement per plasma.

### Fase 8 — Laporan Keuangan & Analitik ✅ (selesai 2026-10-01, lihat §14)
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

---

## 8. Realisasi Fase 2 — Finance Core

Migration: `Phase2_FinanceCore` (schema `finance`). Semua endpoint di bawah `/api/v1/finance/...`.

**Setup**
| Fitur | Endpoint | Aturan utama |
|---|---|---|
| Chart of Accounts | `accounts` | Tipe Asset/Liability/Equity/Revenue/Expense; akun header (pengelompok) vs akun postable; induk harus header & bertipe sama; saldo normal default dari tipe, bisa di-*override* untuk akun kontra (akumulasi penyusutan, retur penjualan). Kode, tipe, induk & sifat postable *immutable*. |
| COA default | (seed) | **67 akun** standar peternakan inti-plasma (kas/bank, piutang, persediaan DOC/pakan/OVK, ayam dalam proses, PPN masukan/keluaran, hutang PPh, hutang plasma, GRNI, HPP, beban kemitraan, dst). Hanya di-seed bila COA masih kosong — silakan disesuaikan tim finance. |
| Cost Center | `cost-centers` | Dimensi analisis opsional per baris jurnal. |
| Periode Fiskal | `fiscal-periods` | `POST years/{tahun}` membuat 12 periode bulanan. **Tutup** berurutan (periode sebelumnya harus tutup, tidak boleh ada jurnal Draft/Approved). **Buka kembali** hanya periode tertutup terakhir. |
| Template Jurnal | `journal-templates` | Susunan akun & sisi (tanpa nominal) untuk jurnal berulang. |

**Jurnal** (`journals`)
- Jurnal manual: `Draft → Approved → Posted`; **maker-checker** — penyetuju tidak boleh pembuatnya (`Journals.SelfApprovalNotAllowed`). Draft bisa diubah/dihapus.
- Wajib seimbang, minimal 2 baris, tiap baris hanya debit **atau** kredit, akun harus postable & aktif, periode harus terbuka.
- **Nomor diberikan saat posting** → nomor jurnal terposting tanpa celah: `JU/{cabang}/{yyyy}/{bulan romawi}/{nnnn}` (manual), `JO/...` (otomatis).
- Jurnal terposting *immutable*; koreksi via `reverse` (jurnal pembalik terposting, asli berstatus `Reversed`, keduanya tetap di buku besar).
- Branch-scoped seperti modul lain.

**Mesin Jurnal Otomatis (Auto Journal Engine)**
- Katalog event akuntansi (`journal-mappings/events`): PurchaseReceipt, VendorInvoice, VendorPayment, StockTransferToCycle, SalesInvoice, CustomerReceipt, PlasmaSettlement, PlasmaPayment — masing-masing dengan komponen nominal (mis. `FeedReceived`, `InputVat`, `IncomeTaxWithheld`).
- `journal-mappings`: tiap komponen → akun debit & kredit. Mapping default (tanpa cabang) + *override* per cabang. Default untuk semua event sudah di-seed.
- `IAutoJournalService.PostAsync(AccountingEntry)` dipanggil modul operasional (Fase 3+) dari domain event handler (outbox). **Idempotent** per (event, dokumen sumber) — dijamin unique index. Akun bisa di-*override* per transaksi (mis. rekening bank yang dipakai). Nominal negatif membalik sisi.
- `journal-mappings/preview`: *dry-run* untuk mengecek mapping tanpa menyimpan.

**Laporan** (`reports`)
- `general-ledger?accountId=&from=&to=[&branchId=&costCenterId=]` — saldo awal, mutasi dengan saldo berjalan (arah saldo normal), saldo akhir.
- `trial-balance?from=&to=[&branchId=&includeZeroBalances=]` — per akun postable: saldo awal, mutasi, saldo akhir (kolom debit/kredit) + `isBalanced`.

**Permission baru**: `finance-setup:read/manage`, `fiscal-periods:close`, `journals:read/create/approve/post`, `finance-reports:read`.

**Catatan & penyesuaian**
- Dimensi cabang disimpan di header jurnal (satu jurnal = satu cabang); transaksi antar-cabang nanti dibuat sebagai dua jurnal dengan akun antar-cabang.
- Belum ada penutupan tahun (laba/rugi → laba ditahan); dikerjakan di Fase 8. Sampai itu, saldo akun pendapatan/beban terakumulasi lintas tahun di neraca saldo.
- Use case Finance ditulis satu file per use case (command + validator + handler) agar ringkas.
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara): seed COA & mapping, jurnal tidak seimbang/akun header ditolak, self-approval ditolak, approve & post oleh checker, reverse, buku besar & neraca saldo seimbang, preview mapping, override mapping per cabang, aturan tutup/buka periode, posting ke periode tertutup ditolak.

---

## 9. Realisasi Fase 3 — Pengadaan & Gudang

Migration: `Phase3_ProcurementInventory` (schema `procurement` & `inventory`).

**Purchase Order** (`/api/v1/purchase-orders`)
- `Draft → Approved → PartiallyReceived → Received`, bisa `Closed` (sisa tidak dikirim) atau `Cancelled` (sebelum ada penerimaan).
- Hanya item sapronak aktif (DOC/Pakan/OVK), satuan = satuan dasar atau konversi item (mis. pakan dipesan per SAK), harga per satuan pesan **exclude PPN**, kode PPN opsional (dipakai di invoice vendor, Fase 6).
- Nomor `PO/{cabang}/{yyyy}/{bulan romawi}/{nnnn}`; tidak bisa diterima sebelum di-approve; tidak boleh over-receipt.

**Penerimaan Barang / BPB** (`/api/v1/inventory/goods-receipts`)
- Terhadap PO; jumlah per baris PO (satuan PO) dikonversi ke satuan dasar; nilai = harga PO × qty.
- Pakan & OVK → **gudang induk**. **DOC boleh langsung ke gudang kandang** → otomatis masuk ke siklus terbuka kandang tsb.
- Jurnal otomatis (outbox): `PurchaseReceipt` (Dr Persediaan DOC/Pakan/OVK, Cr GRNI). DOC langsung ke kandang juga `StockTransferToCycle` (Dr Ayam Dalam Proses, Cr Persediaan DOC).

**Transfer Stok / Kirim Sapronak** (`/api/v1/inventory/stock-transfers`)
- Dari gudang induk ke gudang induk lain (satu cabang, tanpa jurnal) atau ke **gudang kandang** → dibebankan ke **siklus terbuka** kandang (ditentukan otomatis; ditolak bila kandang tidak punya siklus terbuka).
- Keluar dengan harga rata-rata (moving average) saat itu; nilai masuk di tujuan = nilai keluar (tanpa selisih pembulatan).
- Jurnal otomatis ke kandang: `StockTransferToCycle` (Dr Ayam Dalam Proses, Cr Persediaan).

**Stok** (`/api/v1/inventory/stock-balances`, `/api/v1/inventory/stock-card`)
- Saldo per (gudang, item): qty satuan dasar, nilai, **harga rata-rata bergerak**; stok tidak bisa minus.
- Kartu stok: saldo awal, mutasi (termasuk nomor siklus untuk gudang kandang), saldo berjalan & akhir.

**Permission baru**: `purchasing:read/manage/approve`, `inventory:read/receive/transfer`.

**Catatan teknis**
- Konflik konkurensi (dua transaksi mengubah saldo stok yang sama bersamaan) dan pelanggaran unique index kini dikembalikan sebagai **409** (bukan 500) → aman untuk di-*retry* klien.
- Stok di gudang kandang sudah menjadi biaya siklus (WIP); pemakaian harian (Fase 4) hanya mengurangi kuantitas untuk FCR, **retur/mutasi pakan antar kandang** dikerjakan di Fase 4 bersama "Mutasi Pakan".
- Kegagalan jurnal otomatis (mis. periode tutup, mapping belum ada) tercatat di `infrastructure.outbox_messages.error` setelah retry; perlu layar monitoring di WebApp nanti.
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara): moving average (60 SAK @430rb + 40 SAK @440rb = Rp 8.680/kg), transfer 30 SAK = Rp 13.020.000, DOC langsung ke kandang, 5 jurnal otomatis, neraca saldo seimbang (Persediaan Pakan 30,38 jt; Ayam Dalam Proses 50,52 jt; GRNI 80,9 jt), serta penolakan: PO draft, satuan salah, pakan ke kandang, over-receipt, stok kurang, kandang tanpa siklus.

---

## 10. Realisasi Fase 4 — Produksi

Migration: `Phase4_Production` (schema `production`, tabel retur di `inventory`, panen di `partnership`).

**Aturan retur & mutasi pakan (keputusan 2026-09-30)**: retur dan mutasi pakan antar kandang **harus melalui gudang induk**. Tidak ada transfer kandang → kandang langsung; gudang induk selalu tercatat di kartu stok.

**Chick-in** (`POST /api/v1/cycles/{id}/start`) — ⚠️ *breaking change* dari Fase 1
- Body sekarang `{ chickInDate, lines: [{ itemId (DOC), quantity }] }`; DOC diambil dari **stok gudang kandang** (diterima langsung atau ditransfer), populasi awal = total DOC yang ditebar. Ditolak bila DOC kurang.
- Permission: `production:record` (PPL).

**Daily Recording** (`/api/v1/production/daily-recordings`)
- Per siklus per hari: mati, culling, BW rata-rata (gram, opsional — tidak ditimbang tiap hari), pemakaian pakan & OVK (satuan bebas: KG/SAK/VIAL…), catatan. Umur dihitung otomatis dari tanggal chick-in.
- Pemakaian mengurangi stok **gudang kandang** (kartu stok tipe `Usage`); tidak ada jurnal (biaya sudah di Ayam Dalam Proses saat transfer).
- Aturan: siklus Active/Harvesting, tanggal ≥ chick-in dan tidak di masa depan (toleransi +1 hari untuk zona waktu), satu recording per hari, populasi tidak boleh minus, hanya item Pakan/OVK.
- **Offline-ready**: klien boleh mengirim `id` (Guid v7) sendiri; kirim ulang dengan id yang sama → mengembalikan recording yang sudah ada (tidak dobel). Plus `Idempotency-Key`.

**Revisi Daily Recording** (`PUT /api/v1/production/daily-recordings/{id}`, permission `production:revise`)
- Wajib alasan; nilai sebelumnya disimpan (JSON) di riwayat revisi + siapa & kapan. Detail recording menampilkan riwayatnya.
- Stok dikoreksi: pemakaian lama dikembalikan (`UsageReversal`, dengan nilai aslinya) lalu pemakaian baru dikeluarkan; populasi siklus disesuaikan dengan selisih mati/culling.

**Retur Sapronak** (`/api/v1/inventory/stock-returns`, permission `inventory:return`)
- Gudang kandang → gudang induk (satu cabang), hanya Pakan/OVK, dari siklus terbuka kandang, dengan harga rata-rata gudang kandang.
- Jurnal otomatis baru `StockReturnFromCycle`: Dr Persediaan Pakan/OVK, Cr Ayam Dalam Proses. **Mapping default ditambahkan otomatis** saat startup Development meskipun COA sudah ada (seeder kini melengkapi mapping yang belum ada tanpa mengubah yang sudah ada).

**Mutasi Pakan antar kandang** (`POST /api/v1/inventory/feed-mutations`)
- `{ fromCoopWarehouseId, viaCentralWarehouseId, toCoopWarehouseId, mutationDate, reason, lines }` → dalam **satu transaksi** membuat Retur (kandang A → induk) + Transfer (induk → kandang B). Keduanya bernomor, terjurnal, dan terlihat di kartu stok gudang induk (`ReturnIn` lalu `TransferOut`). Transfer keluar dengan harga rata-rata gudang induk.

**Panen** (`POST /api/v1/cycles/{id}/harvests`)
- Tanggal, jumlah ekor, total bobot (kg), catatan (mis. per truk); umur dicatat otomatis; tidak boleh melebihi populasi; panen pertama → status `Harvesting`. Fase 5 (DO/Invoice) akan merujuk data panen ini.

**Performa** (`GET /api/v1/cycles/{id}/performance`)
- Per hari: populasi, deplesi %, pakan kumulatif, BW (dibawa dari penimbangan terakhir), bobot hidup, **FCR, ADG, IP**; plus daftar panen, angka terkini, dan performa penutupan.
- Rumus (satu sumber: `CyclePerformance.Calculate`): Deplesi = (mati+culling)/populasi awal; FCR = pakan kg / (populasi × BW + kg dipanen); ADG = BW g / umur; IP = (100 − deplesi%) × BW kg / (FCR × umur) × 100. Saat tutup: BW = rata-rata bobot panen, umur = rata-rata umur panen tertimbang ekor.

**Tutup Siklus** (`POST /api/v1/cycles/{id}/close`, permission `production:close`)
- Syarat: status Harvesting, populasi = 0, **gudang kandang kosong** (sisa pakan/OVK sudah diretur). Performa penutupan disimpan permanen (dasar settlement Fase 7). Setelah tutup, recording/panen ditolak.

**Permission baru**: `production:read/record/revise/close`, `inventory:return`.

**Catatan**
- Respons siklus kini menyertakan `totalMortality`, `totalCulling`, `harvestedBirds`, `harvestedWeightKg`, `currentPopulation`, `closedDate`.
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara): chick-in dari stok DOC kandang, recording + retry offline (id sama) + tolak tanggal ganda/masa depan/DOC sebagai pakai, revisi (alasan wajib, riwayat tersimpan, stok terkoreksi), retur kandang→kandang ditolak, mutasi A→induk→B, kartu stok induk, performa harian, panen melebihi populasi ditolak, tutup ditolak selama masih ada sisa pakan lalu berhasil setelah retur, recording setelah tutup ditolak; 8 jurnal otomatis tanpa error; neraca saldo seimbang (Ayam Dalam Proses 14,45 jt = siklus A 8,45 jt [DOC 7,5 jt + pakan 90 kg + 1 vial OVK] + siklus B 6 jt).

---

## 11. Realisasi Fase 5 — Penjualan & AR

Migration: `Phase5_SalesReceivables` (schema `sales`; tabel penerimaan di `finance`).

**Keputusan (2026-10-01)**
- **HPP ditunda**: invoice penjualan hanya menjurnal piutang, penjualan, dan PPN keluaran. Pengakuan HPP (Dr HPP / Cr Ayam Dalam Proses) dikerjakan di Fase 7 dari biaya siklus. Komponen `CostOfGoodsSold` di katalog `SalesInvoice` tetap ada tetapi belum dikirim.
- **Sales Order wajib sebelum DO.**

**Sales Order** (`/api/v1/sales/orders`)
- `Draft → Approved → PartiallyDelivered → Delivered`, bisa `Closed` (sisa tidak dikirim) atau `Cancelled` (sebelum ada pengiriman). Nomor `SO/{cabang}/{yyyy}/{bulan romawi}/{nnnn}`.
- Baris: item **ayam hidup** aktif, jumlah **ekor** (kuantitas yang mengikat), estimasi kg, harga per kg (exclude PPN), kode PPN opsional.
- **Credit limit dicek saat approve**. Exposure customer mencakup seluruh cabang: outstanding invoice terposting, draft invoice, DO yang belum ditagih, dan sisa estimasi SO lain yang masih terbuka (tanpa PPN). Exposure + nilai SO ini tidak boleh melebihi limit. Limit 0 berarti tidak ada kredit.
- Jika melebihi limit, SO hanya bisa di-approve lewat `POST {id}/approve-over-limit` dengan alasan dan permission `sales:credit-override`. Alasan disimpan di `creditOverrideReason`.

**Delivery Order / Surat Jalan** (`/api/v1/sales/delivery-orders`)
- Setiap baris adalah **satu data panen** (`cycle_harvests`, biasanya satu truk) dan dikirim utuh: ekor & kg hasil timbang panen yang dijual. Harga/kg & kode PPN di-*snapshot* dari baris SO.
- Satu panen hanya boleh ada di **satu DO aktif** (dicek aplikasi + partial unique index `ix_delivery_order_lines_harvest_id_active`). Ekor tidak boleh melebihi sisa SO. Panen harus dari cabang SO. Tanggal DO ≥ tanggal SO & tanggal panen.
- Status `Delivered → Invoiced`. `cancel` hanya untuk DO yang belum ditagih: ekor kembali ke sisa SO dan panen bisa di-DO ulang.
- `GET /api/v1/sales/undelivered-harvests?branchId=&cycleId=` menampilkan panen yang belum masuk DO.

**Sales Invoice** (`/api/v1/sales/invoices`)
- Dibuat sebagai **Draft** dari satu atau lebih DO (customer & cabang sama). Draft **belum bernomor**. Jatuh tempo = tanggal invoice + termin customer.
- PPN per baris mengikuti kode PPN & tarif yang berlaku pada tanggal invoice: `Taxable` → DPP = nilai × rasio DPP, PPN = DPP × tarif. `Exempt`/`NotCollected`/tanpa kode → tanpa PPN. Invoice ditolak jika kode Taxable tidak punya tarif pada tanggal tersebut.
- `post` memberi nomor `INV/...` (posting tanpa celah nomor) dan memicu jurnal `SalesInvoice`: Dr Piutang Usaha / Cr Penjualan Ayam Hidup (DPP) dan Dr Piutang / Cr PPN Keluaran. Invoice terposting final (koreksi lewat nota kredit, belum ada).
- `cancel` hanya untuk draft; DO-nya bisa ditagih lagi.

**Penerimaan Pembayaran Customer** (`/api/v1/finance/customer-receipts`)
- Per cabang & customer, diterima ke **akun kas/bank** yang dipilih (akun aset postable & aktif), dialokasikan ke satu atau lebih invoice terposting. Pembayaran parsial boleh, tetapi tidak boleh melebihi outstanding. Tanggal ≥ tanggal invoice. Nomor `RCV/...`.
- Invoice menjadi `PartiallyPaid` / `Paid`. Jurnal `CustomerReceipt`: Dr akun kas/bank penerimaan (override per transaksi) / Cr Piutang Usaha.

**Laporan piutang** (`/api/v1/finance/receivables`)
- `ledger?customerId=&from=&to=[&branchId=]` (kartu piutang): saldo awal, invoice (debit) & penerimaan (kredit) dengan saldo berjalan, saldo akhir.
- `aging?asOf=[&branchId=&customerId=]`: outstanding per invoice pada tanggal tersebut (hanya memperhitungkan penerimaan s.d. tanggal itu), dikelompokkan *belum jatuh tempo* / 1–30 / 31–60 / 61–90 / >90 hari lewat jatuh tempo, per customer & total.

**Tutup siklus** — syarat baru: setiap panen siklus harus ada di DO yang ditagih oleh invoice **terposting** (`Cycles.UnsoldHarvest`). Draft invoice belum dihitung terjual.

**Permission baru**: `sales:read/manage/approve/credit-override/deliver/invoice`, `receivables:read/manage`.

**Perbaikan lain**
- `IdempotencyFilter` kini menyimpan respons dengan opsi JSON API (camelCase, enum sebagai nama). Sebelumnya respons *replay* memakai PascalCase sehingga berbeda dari respons pertama (bug Fase 0).

**Catatan**
- `TaxRate.TaxBaseRatio` disimpan dengan presisi (10,8), sehingga 11/12 tersimpan sebagai 0,91666667. Contoh hasil verifikasi: DPP 40.400.000 → PPN 4.444.000,02 (seharusnya 4.444.000). **Keputusan 2026-10-01**: PPN 12% DPP nilai lain diinput sebagai **tarif efektif 11% dengan rasio DPP 1**.
- Penerimaan belum bisa dibatalkan/di-*void*, dan belum ada uang muka penjualan maupun nota kredit/retur penjualan. **Diputuskan masuk Fase 6.**
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara), **43 skenario lulus**: penolakan item non-ayam, credit limit (ditolak lalu override dengan alasan), batal SO, DO + replay idempotency, panen ganda (409), DO ke SO batal / sebelum tanggal panen, panen belum terkirim, batal DO mengembalikan sisa SO, draft invoice (tanpa nomor, DO terkunci, batal draft melepas DO), PPN dengan rasio DPP, jatuh tempo, posting bernomor & final, penerimaan parsial (tolak lebih bayar & akun non-aset), kartu piutang, aging (1–30 hari & per tanggal lampau), 2 jurnal otomatis tanpa error, neraca saldo seimbang (Piutang 14.844.000,02; Penjualan 40.400.000; PPN Keluaran 4.444.000,02; Bank 30.000.000), tutup siklus ditolak selama panen belum terjual/invoice masih draft lalu berhasil setelah posting.

---

## 12. Realisasi Fase 6 — AP, Kas & Bank, sisa AR

Migration: `Phase6_PayablesCashBank` (tabel baru di schema `finance` dan `sales`; kolom baru di `master.vendors`, `inventory.goods_receipt_lines`, `sales.sales_invoices`/`_lines`, `finance.customer_receipts`).

**Keputusan (2026-10-01)**
| Topik | Keputusan |
|---|---|
| Selisih harga invoice vendor vs PO | **Toleransi per vendor** (`priceTolerancePercent`, default 0 = harus sama). Selisih dijurnal ke akun selisih (5-1301). Di atas toleransi hanya bisa diposting lewat `post-with-variance` (permission `payables:approve-variance` + alasan). |
| Approval Payment Voucher | **Maker-checker**: Draft → Approved (oleh user lain) → Paid |
| Kas & bank | **Master Kas/Bank** (`CashBankAccount`: Kas / Bank / Kas Kecil, per cabang) yang menunjuk tepat satu akun COA |
| Rekonsiliasi bank | Baris rekening koran diinput atau diimpor **CSV**, lalu dicocokkan dengan mutasi buku |
| PPN 12% DPP nilai lain | Diinput sebagai **tarif efektif 11% dengan rasio DPP 1** |

**Master Kas/Bank** (`/api/v1/finance/cash-bank-accounts`)
- Kode, nama, tipe (`Cash`/`Bank`/`PettyCash`), cabang, akun COA (aset, postable, aktif; **satu akun COA hanya untuk satu kas/bank**), bank & nomor rekening (wajib untuk tipe Bank). Daftar menampilkan saldo buku.
- `GET {id}/ledger?from=&to=` → **buku kas/bank**: saldo awal, mutasi masuk/keluar dengan saldo berjalan, saldo akhir.

**Kas Masuk / Keluar** (`/api/v1/finance/cash-transactions`)
- Transaksi lain-lain (bunga, listrik, biaya kas kecil, setoran modal) dengan ≥1 baris akun lawan (+ cost center). Baris tidak boleh memakai akun kas/bank itu sendiri.
- Kas **masuk** bisa diposting langsung dari Draft. Kas **keluar** wajib `approve` oleh user lain (`cash-bank:approve`), baru `post`. Nomor `BKM/...` / `BKK/...` diberikan saat posting. Draft/approved bisa dibatalkan.
- Jurnal otomatis dari barisnya sendiri (tanpa mapping): masuk = Dr kas/bank / Cr akun baris; keluar = Dr akun baris / Cr kas/bank.

**Transfer / Pemindahbukuan & Kas Kecil** (`/api/v1/finance/bank-transfers`)
- Antar dua kas/bank **dalam satu cabang**, langsung terposting (`TRF/...`): Dr tujuan / Cr asal. Pengisian kas kecil = transfer bank → kas kecil; pengeluaran kas kecil = kas keluar dari akun kas kecil.

**Vendor Invoice / Tagihan Vendor** (`/api/v1/finance/vendor-invoices`)
- **3-way match**: tiap baris menagih baris BPB (PO–BPB–Invoice) vendor & cabang yang sama, qty (satuan PO) ≤ qty diterima yang belum ditagih. Qty & nilai tertagih disimpan di baris BPB (`quantity_invoiced`, `value_invoiced`). Tagihan terakhir menghapus sisa nilai BPB sampai ke sen.
- `GET uninvoiced-receipts?vendorId=` → BPB yang belum ditagih.
- Draft (tanpa nomor) → `post` (nomor `VI/...`). Nomor invoice vendor unik per vendor (kecuali yang dibatalkan). PPN masukan dari kode PPN baris PO. **PPh** dipotong opsional (kode PPh di header, dasar = DPP). Total hutang = DPP + PPN − PPh.
- Jurnal `VendorInvoice`: `GoodsValue` Dr GRNI / Cr Hutang Usaha (nilai BPB), `PriceVariance` Dr Selisih / Cr Hutang Usaha (negatif → sisi dibalik), `InputVat` Dr PPN Masukan, `IncomeTaxWithheld` Dr Hutang Usaha / Cr Hutang PPh 23.
- Batal draft → qty BPB bisa ditagih lagi.

**Payment Voucher** (`/api/v1/finance/payment-vouchers`)
- Membayar ≥1 invoice terposting satu vendor dari satu kas/bank; parsial boleh, tidak melebihi outstanding. Nomor `PV/...` saat dibuat. `approve` (checker, bukan pembuat; `payables:approve`) → `pay` dengan tanggal bayar aktual (`payables:pay`) → invoice PartiallyPaid/Paid. Jurnal `VendorPayment`: Dr Hutang Usaha / Cr akun kas/bank PV.

**Laporan hutang** (`/api/v1/finance/payables`): `ledger?vendorId=&from=&to=` (kartu hutang) dan `aging?asOf=` (umur hutang per vendor, bucket sama dengan AR).

**Rekonsiliasi Bank** (`/api/v1/finance/bank-reconciliations`)
- `POST` {cashBankAccountId (tipe Bank), statementDate, statementBalance}. Hanya satu yang berjalan per rekening; tanggal harus setelah rekonsiliasi selesai terakhir.
- Baris rekening koran: `POST {id}/statement-lines` (JSON) atau `.../import` {csv}: `tanggal,keterangan,debet,kredit` (sudut pandang bank: debet = keluar, kredit = masuk), pemisah `,` atau `;`, tanggal `yyyy-MM-dd` / `dd/MM/yyyy`, header opsional, field ber-kutip didukung.
- Pencocokan: `auto-match` (nominal sama, selisih tanggal ≤ 3 hari, terdekat dulu) atau manual `statement-lines/{n}/match` {journalEntryId, journalLineNumber}. Satu mutasi buku hanya bisa clear sekali (unique index lintas rekonsiliasi).
- Detail menampilkan saldo buku, **mutasi buku belum clear** (setoran/cek dalam perjalanan), dan selisih. `complete` hanya bila semua baris rekening koran cocok dan saldo rekening koran = saldo buku − belum clear. Biaya/bunga bank yang belum dibukukan dicatat dulu lewat kas masuk/keluar.

**Sisa AR dari Fase 5**
- **Uang muka penjualan**: penerimaan customer boleh berisi `advanceAmount` (tanpa/selain alokasi). Jurnal: `Received` Dr kas/bank / Cr Piutang, `Advance` Dr kas/bank / Cr Uang Muka Penjualan (2-1501). `POST customer-receipts/{id}/apply-advance` menerapkan uang muka ke invoice. Jurnal `CustomerAdvanceApplied`: Dr Uang Muka / Cr Piutang. **Uang muka yang belum diterapkan mengurangi exposure credit limit**, sehingga customer limit 0 bisa membeli setelah bayar di muka.
- **Void penerimaan** (`POST customer-receipts/{id}/void` {date, reason}, permission `receivables:void`): alokasi dikembalikan ke invoice, jurnal dibalik pada tanggal void (`CustomerReceipt.Reversal`). Ditolak bila uang mukanya sudah diterapkan.
- **Nota kredit / retur penjualan** (`/api/v1/sales/credit-notes`, nomor `CN/...`): mengurangi baris invoice terposting (DPP) dengan PPN proporsional tarif baris; tidak melebihi sisa baris maupun outstanding invoice. Jurnal `SalesCreditNote`: Dr Potongan & Retur Penjualan (4-1901) & Dr PPN Keluaran / Cr Piutang. Invoice kini punya `creditedAmount`; outstanding = total − dibayar − dikredit.
- Kartu piutang kini memuat: Invoice, Receipt (bagian yang dialokasikan), ReceiptVoid, AdvanceApplied, CreditNote. Aging memperhitungkan void, penerapan uang muka, dan nota kredit per tanggal.

**⚠️ Perubahan API (breaking) dari Fase 5**
- `POST /finance/customer-receipts`: body kini `{ cashBankAccountId, customerId, receiptDate, reference, notes, allocations, advanceAmount }`. `branchId` diambil dari kas/bank, sedangkan `cashAccountId` (akun COA) diganti `cashBankAccountId`. Penerimaan lama tetap valid (status `Posted`, `cashBankAccountId` null).

**Perbaikan & perubahan teknis**
- Perhitungan pajak dipindah ke `TaxCode.Calculate` (dipakai invoice penjualan & vendor). Error `SalesInvoices.NoTaxRate` → `TaxCodes.NoRate`.
- `IAutoJournalService`: `PostLinesAsync` (jurnal dari baris dokumen sendiri) dan `ReverseAsync` (membalik jurnal otomatis sumber tertentu, idempotent).
- Seeder mapping kini juga **menambah komponen baru ke mapping default yang sudah ada** (mis. `VendorInvoice.PriceVariance`, `CustomerReceipt.Advance`) tanpa mengubah komponen lain. Diverifikasi dengan menghapus komponen dari DB lalu restart.
- Posting VI/PV/kas/void/nota kredit mengecek periode fiskal terbuka lebih dulu, sehingga tidak jatuh ke dead letter outbox.

**Permission baru**: `cash-bank:read/manage/approve/reconcile`, `payables:read/manage/approve-variance/approve/pay`, `receivables:void`.

**Catatan**
- Vendor invoice hanya untuk barang ber-PO (3-way match). Biaya jasa/non-PO dibayar lewat kas keluar. Hutang non-PO & nota debit vendor belum ada.
- Transfer antar cabang belum didukung (perlu akun antar-cabang).
- PPh atas invoice vendor selalu dijurnal ke akun mapping `IncomeTaxWithheld` (default Hutang PPh 23). PPh 22/4(2) perlu override mapping per cabang atau per akun ke depan.
- Rate limiter global 100 request/menit per user berlaku (untuk skrip verifikasi dinaikkan lewat env `RateLimiting__Global__PermitLimit`).
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara), **70 skenario lulus**, termasuk user checker kedua, CSV rekening koran (pemisah `;`, tanggal campuran, field ber-kutip), rekonsiliasi dua periode dengan cek dalam perjalanan (pencocokan manual karena lewat 3 hari), dan neraca saldo seimbang (GRNI 7,5 jt; Selisih Harga 200 rb; PPN Masukan 2.860.000; Hutang PPh 23 348.000; Hutang Usaha 4.546.000; Uang Muka Penjualan 1.266.000; Retur Penjualan 1 jt; PPN Keluaran 4.334.000; Piutang 0).

---

## 13. Realisasi Fase 7 — HPP & Settlement Plasma

Migration: `Phase7_CostingSettlement` (schema baru `costing`; kolom `cost_amount` di `sales.sales_invoice_lines`, `closing_cost` (jsonb) di `partnership.production_cycles`, `payee_type`/`farmer_id` di `finance.payment_vouchers` dengan `vendor_id` menjadi nullable, tabel `finance.payment_voucher_settlement_allocations`).

**Keputusan (2026-10-01)**
| Topik | Keputusan |
|---|---|
| Pengakuan HPP penjualan | **Per invoice dengan HPP estimasi**, dikoreksi saat tutup siklus |
| Pembayaran ke plasma | **Payment Voucher diperluas**: penerima vendor **atau** peternak plasma |
| Hasil settlement negatif (harga kontrak) | Menjadi **piutang plasma** (1-1302), bisa dipotong dari settlement berikutnya |
| Bagi hasil | Laba siklus = penjualan bersih (DPP − nota kredit) − biaya siklus; plasma dapat %; **rugi ditanggung inti** (bagian plasma 0) |
| Harga jaminan ayam | **Per panen (truk)** sesuai bobot rata-rata panen itu |
| Approval settlement | **Maker-checker** |

**HPP siklus** (`GET /api/v1/cycles/{id}/cost`)
- Biaya siklus = sapronak **terpakai** di kandang dengan harga moving average, diambil dari kartu stok gudang kandang per siklus: DOC ditebar (chick-in), pakan & OVK terpakai (recording, termasuk koreksi revisi). Sisa stok di kandang belum dihitung, karena akan diretur atau dipakai.
- **HPP/kg berjalan** = biaya terpakai / (kg dipanen + populasi × BW recording terakhir; jika belum pernah ditimbang, pakai rata-rata bobot panen).
- Respons: rincian per item, DOC/pakan/OVK, total, bobot hidup, HPP/kg & /ekor, HPP yang sudah diakui di invoice, penyesuaian (setelah tutup), pendapatan plasma & total biaya + kemitraan (setelah settlement).

**HPP per invoice (estimasi)**
- Saat invoice penjualan diposting, setiap baris mendapat `costAmount` = HPP/kg berjalan siklus × kg. Jurnal `SalesInvoice` kini memuat komponen `CostOfGoodsSold`: Dr HPP Ayam Hidup (5-1101) / Cr Ayam Dalam Proses (1-1501).

**Tutup siklus → biaya final & penyesuaian**
- Saat tutup, `ClosingCost` dibekukan (DOC/pakan/OVK, total, HPP/kg & /ekor, HPP diakui, penyesuaian). Karena sisa sapronak wajib diretur, biaya final = saldo Ayam Dalam Proses siklus.
- Event baru `CycleCostAdjustment` (komponen `CostOfGoodsSold`): penyesuaian = biaya final − HPP diakui; negatif membalik sisi. Ayam Dalam Proses siklus menjadi **nol**.

**Settlement plasma** (`/api/v1/costing/settlements`)
- `POST` {cycleId, settlementDate, debtDeduction, notes} → draft bernomor `STL/...` untuk siklus plasma berstatus **Closed** (satu per siklus, kecuali yang dibatalkan). `PUT {id}` menghitung ulang draft. `approve` (checker, `settlements:approve`) → jurnal + siklus **Settled**. `cancel` untuk draft.
- Kebijakan per skema kontrak (`ISettlementPolicy`, dari *snapshot* kontrak siklus):
  - **Harga kontrak**: per panen kg × harga jaminan rentang bobot rata-rata panen [min, max) − sapronak terpakai × harga kontrak (setiap item wajib punya harga kontrak) ± insentif/denda.
  - **Bagi hasil**: % × laba siklus (0 bila rugi) ± insentif/denda.
  - Insentif/denda kontrak berlaku bila metrik penutupan (FCR, IP, deplesi %, BW rata-rata; `None` = selalu) berada dalam [RangeFrom, RangeTo]; basis per kg / per ekor / lump sum.
- PPh dari kode PPh kontrak atas pendapatan (hanya bila positif). **Potongan hutang** ≤ pendapatan − PPh. Hasil negatif → `deficit`, tidak ada PPh/pembayaran, status langsung `Paid` setelah disetujui.
- Jurnal `PlasmaSettlement`: `PlasmaIncome` Dr Beban Kemitraan (5-1201) / Cr Hutang Plasma (2-1201); `IncomeTaxWithheld` Dr Hutang Plasma / Cr Hutang PPh; `Deduction` Dr Hutang Plasma / Cr Piutang Plasma; `PlasmaDeficit` (baru) Dr Piutang Plasma / Cr Beban Kemitraan.
- Slip settlement = `GET {id}` (baris perhitungan: tipe, uraian, qty, harga, nilai; total, PPh, potongan, netto, terbayar).

**Pembayaran plasma** (`POST /api/v1/finance/payment-vouchers/plasma` {cashBankAccountId, farmerId, paymentDate, reference, notes, allocations: [{plasmaSettlementId, amount}]})
- Payment Voucher kini punya `payeeType` (`Vendor`/`Farmer`). Approve/pay/cancel tetap di endpoint yang sama. Dokumen yang dibayar memakai antarmuka `IPayable` (VendorInvoice, PlasmaSettlement).
- Jurnal `PlasmaPayment`: Dr Hutang Plasma / Cr kas/bank PV.
- ⚠️ Respons PV berubah: tambah `payeeType`, `payeeId`, `payeeCode`, `payeeName`, `farmerId`; `vendorCode`/`vendorName` diganti `payeeCode`/`payeeName`; alokasi kini `{documentType, documentId, number, reference, documentDate, amount}`. Filter daftar PV: `farmerId`. Kode error PV generik: `PaymentVouchers.OverPayment` & `PaymentVouchers.DocumentNotPayable` (sebelumnya `VendorInvoices.*`).

**Permission baru**: `costing:read`, `settlements:manage`, `settlements:approve`.

**Catatan**
- Biaya siklus belum memuat biaya lain-lain/overhead (listrik, tenaga kerja, penyusutan kandang inti). Kandidat Fase 8 (alokasi overhead via cost center).
- HPP estimasi memakai BW recording terakhir. Bila BW lama tidak diperbarui, estimasi bisa meleset, tetapi selalu dikoreksi penuh saat tutup siklus.
- Potongan hutang plasma tidak dicek terhadap saldo piutang plasma per peternak (belum ada sub-ledger piutang plasma). Batasnya hanya pendapatan − PPh.
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara), **32 skenario lulus**: dua siklus plasma (A untung dengan bonus FCR, PPh 2%, potongan hutang 3,3 jt dari rugi siklus B; B rugi → piutang plasma), HPP estimasi dua invoice (16.918,46/kg dan 17.358,70/kg), penyesuaian HPP 418.231,10 saat tutup, maker-checker settlement, PV plasma, dan neraca saldo seimbang dengan Ayam Dalam Proses, Piutang Plasma, dan Hutang Plasma = 0.

---

## 14. Realisasi Fase 8 — Laporan Keuangan, Tutup Buku & Pajak

Migration: `Phase8_ReportingClosing` (kolom `cash_flow_category` di `finance.accounts`, plus data update: akun `1-2%` → Investing, `2-2%` dan Equity → Financing).

**Keputusan (2026-10-01)**
| Topik | Keputusan |
|---|---|
| Tutup tahun buku | **Jurnal penutup** saat periode Desember ditutup; buka kembali Desember membalik jurnal itu |
| Arus kas | **Metode langsung**: mutasi kas/bank dikelompokkan menurut kategori arus kas akun lawannya |
| Overhead di profitabilitas | **Tanpa alokasi**: margin kontribusi per siklus/kandang/peternak; overhead terlihat per cabang |
| Laporan pajak | **Rekap + ekspor CSV**; XML Coretax menyusul setelah format dikonfirmasi konsultan pajak |

**Laporan keuangan** (`/api/v1/finance/reports`, permission `finance-reports:read`)
- `income-statement?from=&to=[&branchId=]` — Laba Rugi per akun induk teratas COA (mis. 4 Pendapatan Usaha, 5 HPP, 6 Beban Operasional, 7/8 lain-lain). Ditampilkan per akun, total pendapatan/beban, laba bersih. Jurnal penutup tidak dihitung, sehingga tahun yang sudah ditutup tetap menampilkan hasilnya.
- `balance-sheet?asOf=[&branchId=]` — Neraca: aset, liabilitas, ekuitas per akun induk, ditambah **laba tahun berjalan** dan **laba tahun lalu yang belum ditutup** (keduanya 0 setelah tutup buku). `isBalanced`.
- `cash-flow?from=&to=[&branchId=]` — Arus kas metode langsung. Setiap jurnal terposting yang menyentuh akun kas/bank: baris akun lawannya masuk ke kategori arus kas akun tersebut (kredit = kas masuk, debit = kas keluar). Transfer antar kas/bank saling hapus. Kas awal + kenaikan = saldo kas buku besar (`isConsistent`).
- `profitability?from=&to=&groupBy=Cycle|Coop|Farmer|Branch[&branchId=&farmerId=]` — penjualan (DPP) − nota kredit − HPP (estimasi di invoice + penyesuaian tutup siklus) − beban kemitraan (settlement; rugi plasma bernilai negatif) = margin, margin/kg. Per cabang ditambah laba bersih buku besar dan selisihnya (`overheadAndOther`).

**Kategori arus kas akun** — `Account.CashFlowCategory` (`Operating`/`Investing`/`Financing`). Default: Equity → Financing, lainnya Operating. Seed COA: aset tetap Investing, liabilitas jangka panjang Financing. Bisa diubah lewat `POST/PUT /finance/accounts` (`cashFlowCategory`).

**Tutup periode & tahun buku**
- `GET /finance/fiscal-periods/{id}/checklist` — `canClose` + daftar cek.
  - **Pemblokir**: jurnal manual draft/approved di periode, event jurnal otomatis belum diproses, event gagal (*dead letter*).
  - **Peringatan**: invoice penjualan draft, DO belum ditagih, invoice vendor draft, PV belum dibayar, kas masuk/keluar belum diposting, settlement draft, rekening bank belum direkonsiliasi sampai akhir periode.
- `close` kini menolak bila ada event jurnal otomatis tertunda atau gagal (`FiscalPeriods.AutoJournalsNotPosted`), selain jurnal belum diposting.
- **Menutup Desember = tutup tahun buku**: dalam transaksi yang sama dibuat satu **jurnal penutup per cabang** (`YearEndClosing`, nomor `JO/...` bertanggal 31 Des). Semua akun pendapatan & beban tahun itu dinolkan, selisihnya (laba/rugi) ke **Laba Ditahan**, yaitu akun kredit mapping `YearEndClosing.NetIncome` (default 3-2101; diatur di Journal Mappings).
- **Buka kembali Desember** membalik jurnal penutup (`YearEndClosing.Reversal`). Menutup ulang membuat jurnal penutup baru yang memuat transaksi susulan.
- Posting invoice penjualan kini juga mengecek periode fiskal terbuka (sebelumnya hanya dicek di outbox).

**Monitoring event gagal** (`/api/v1/system/failed-events`, permission `system:outbox`)
- `GET` daftar event dead letter (tipe, waktu, percobaan, error). `POST retry {eventId?}` mengembalikan event gagal (satu/semua) ke antrean setelah penyebabnya diperbaiki (mis. mapping jurnal ditambahkan, periode dibuka).

**Laporan pajak** (`/api/v1/finance/tax`, permission `tax-reports:read`)
- `vat?year=&month=[&branchId=]` — rekap PPN per masa: PPN keluaran (invoice terposting: nomor, tanggal, customer, NPWP/NITKU/PKP, kode pajak, DPP, DPP nilai lain, PPN), retur PPN keluaran (nota kredit), PPN masukan (invoice vendor ber-PPN: nomor internal, nomor invoice vendor, nomor faktur pajak, NPWP vendor, DPP, PPN); `netVat` = keluaran − retur − masukan (+ kurang bayar / − lebih bayar).
- `withholding?year=&month=` — PPh dipotong per masa dari invoice vendor & settlement plasma (penerima, NPWP/NIK, kode & pasal, DPP, tarif, PPh) + total per kode.
- Ekspor CSV: `vat/export?year=&month=&section=output|output-returns|input` dan `withholding/export?year=&month=` (UTF-8 BOM, pemisah koma, angka titik desimal, tanggal ISO).

**Permission baru**: `tax-reports:read`, `system:outbox`.

**Catatan**
- XML Coretax (e-Faktur & e-Bupot) belum dibuat. Perlu skema & aturan pengisian dari konsultan pajak.
- Laporan per cabang memakai cabang di header jurnal. Transaksi antar cabang belum ada.
- CSV memakai pemisah koma. Excel ber-locale Indonesia mungkin perlu impor manual (Data → From Text).
- Diverifikasi end-to-end ke PostgreSQL lokal (database sementara), **35 skenario lulus**, dengan satu tahun buku penuh 2025:
  - Laba rugi 31.550.000; neraca seimbang (aset 636,1 jt); arus kas operasi +35.275.000 / investasi −200 jt / pendanaan +600 jt, konsisten dengan buku besar.
  - Profitabilitas per siklus/peternak/cabang; rekap PPN Juni (masukan 825 rb) & Juli (keluaran 4,4 jt); PPh 23 150 rb; ekspor CSV.
  - Checklist dengan event gagal & jurnal draft sebagai pemblokir; tutup Jan–Des dengan jurnal penutup (Laba Ditahan 31.550.000, akun P&L nol); buka kembali Desember (jurnal dibalik), posting susulan, tutup ulang (Laba Ditahan 31.550.001).
