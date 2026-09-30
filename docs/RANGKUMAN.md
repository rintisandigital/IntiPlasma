# Rangkuman Proyek — Aplikasi Peternakan Ayam Broiler Inti-Plasma

> Rangkuman poin penting dari sesi pengembangan 2026-09-30 (Fase 0 s.d. Fase 4) dan 2026-10-01 (Fase 5).
> Detail lengkap per fase ada di [PLAN.md](PLAN.md) §6–§11. Dokumen ini adalah titik awal untuk melanjutkan Fase 6.

---

## 1. Tujuan & Konteks

- Aplikasi manajemen peternakan ayam broiler pola **kemitraan Inti-Plasma**, mengikuti flowchart `design-alur-aplikasi.jpg`:
  Struktur peternakan → Pengadaan sapronak → Gudang & transfer → Produksi → Penjualan → HPP & settlement plasma → Finance & accounting.
- Basis: template **Clean Architecture .NET 10** (SharedKernel, Domain, Application, Infrastructure, Web.Api) + PostgreSQL.
- Pattern: **DDD** (aggregate, value object, domain event) + **CQRS** (command lewat EF Core, query lewat Dapper + SQL).

## 2. Keputusan Bisnis (disepakati)

| # | Topik | Keputusan |
|---|-------|-----------|
| 1 | Skema kemitraan | **Diatur per kontrak**: `PriceContract` (harga kontrak sapronak + harga jaminan ayam per rentang bobot) atau `ProfitSharing` (% bagi hasil). Bonus/potongan dikonfigurasi per kontrak. |
| 2 | Valuasi persediaan | **Moving Average** |
| 3 | Struktur usaha | **Satu perusahaan, banyak cabang** (COA tunggal, cabang sebagai dimensi) |
| 4 | Tampilan | **API saja dulu**; menyusul mobile app untuk PPL dan MVC WebApp untuk Admin Office |
| 5 | Pajak | **Perlu PPN & PPh**; semua tarif sebagai data (tidak di-hardcode), menunggu konfirmasi konsultan pajak |
| 6 | Sample Todos | Dihapus |
| 7 | Retur & mutasi pakan antar kandang | **Harus melalui gudang induk** (tidak ada transfer kandang → kandang langsung) |
| 8 | Format nomor dokumen | `PREFIX/CABANG/YYYY/BULAN-ROMAWI/NNNN`, mis. `PO/BDG/2026/IX/0001` |
| 9 | Pengakuan HPP penjualan | **Ditunda ke Fase 7** (dari biaya siklus); invoice hanya menjurnal piutang, penjualan, dan PPN keluaran |
| 10 | Sales Order | **Wajib sebelum DO** |

## 3. Arsitektur & Konvensi Kode

**Struktur**
- Monolith dengan bounded context per folder + **schema PostgreSQL** sendiri: `identity`, `infrastructure`, `master`, `partnership`, `finance`, `procurement`, `inventory`, `production`.
- Write side: handler → `IApplicationDbContext` (DbSet = repository) → method domain → `SaveChangesAsync`.
- Read side: handler → `IDbConnectionFactory` + Dapper; satu `NpgsqlDataSource` dipakai EF & Dapper.

**Building block (SharedKernel)**
- `Entity`, `AggregateRoot` (domain event + kolom audit `created_at_utc/by`, `modified_at_utc/by`), `DomainEvent` (punya `Id` untuk idempotensi), `Money` (IDR, 2 desimal), `PagedList`, `ISoftDeletable`.
- ID = **Guid v7** (`Guid.CreateVersion7()`); klien mobile boleh membuat ID sendiri.
- Concurrency token `xmin` di semua aggregate. Konflik → **409**.
- Enum disimpan & dikirim sebagai **nama** (string).

**Infrastruktur penting**
- **Outbox**: domain event disimpan di `infrastructure.outbox_messages` dalam transaksi yang sama, diproses `OutboxProcessor` (retry s.d. `Outbox:MaxAttempts`, lalu dead letter dengan kolom `error`).
- **Audit interceptor** mengisi kolom audit otomatis.
- **Penomoran dokumen** (`IDocumentNumberGenerator`): reset per bulan per (prefix, cabang); nomor tidak terpakai bila transaksi gagal. Pola di handler: validasi dulu dengan nomor kosong → baru ambil nomor.
- **Idempotency**: header `Idempotency-Key` pada endpoint create (HybridCache in-memory; perlu Redis sebelum multi-instance).
- **Otorisasi**: role → permission (`{module}:{action}`); role sistem `Administrator` otomatis mendapat semua permission. **Branch-scoped**: user hanya melihat cabang yang ditugaskan, kecuali punya `branches:access-all`. Akses cabang lain → 403.
- Semua endpoint di `/api/v1`. Endpoint per resource dikelompokkan dalam satu file `XxxEndpoints.cs` (`MapGroup`).
- List endpoint: `?search=&page=&pageSize=` (maks 100) + filter spesifik.

**Konvensi file**
- Fase 1: satu file per class (Command, Validator, Handler terpisah).
- Fase 2 dst.: **satu file per use case** (command + validator + handler). *Bisa diseragamkan bila diminta.*
- Build: `TreatWarningsAsErrors` + Sonar + analyzer `AnalysisMode=All` — gaya kode ketat (mis. `var` vs tipe eksplisit, record privat init-only ditolak Sonar → pakai `internal sealed class` bersetter untuk row Dapper).

## 4. Ringkasan Per Fase

### Fase 0 — Fondasi ✅
- Solution di-rename `IntiPlasma.slnx`; sample Todos dihapus; migration awal dibuat ulang.
- Identity: User & Role (+ permission), **registrasi user hanya oleh admin** (`users:manage`); admin awal dari `Seed:Admin` (dev: `admin@intiplasma.local` / `Admin123!`).
- Outbox, audit, numbering, idempotency, API versioning.
- Testcontainers dinaikkan ke 4.15.0 (celah keamanan SSH.NET).

### Fase 1 — Master Data & Kemitraan ✅
- Master: Branch, UoM (seed 9 satuan standar), TaxCode (PPN/PPh, tarif ber-tanggal efektif + rasio DPP, **tidak di-seed**), Item (+ konversi satuan, mis. 1 SAK = 50 KG), Warehouse (induk / kandang), Vendor, Customer (NPWP 15/16 digit, NITKU 22 digit), Farmer (Inti/Plasma; Plasma wajib NIK), Coop.
- **Gudang kandang dibuat otomatis** saat kandang dibuat (`GK-{kode}`, via outbox).
- Kontrak kemitraan (Draft → Active → Inactive; hanya draft yang bisa diubah).
- Siklus produksi: plasma wajib kontrak aktif & satu cabang; inti tanpa kontrak; **satu siklus terbuka per kandang** (juga unique index); *snapshot* kontrak disimpan ke siklus.

### Fase 2 — Finance Core ✅
- COA bertingkat (header vs postable, akun kontra) — **seed 67 akun** standar peternakan bila COA kosong.
- Cost center, periode fiskal bulanan (tutup berurutan, tidak boleh ada draft; buka kembali hanya periode tutup terakhir), template jurnal.
- Jurnal manual: Draft → Approved → Posted → Reversed; **maker-checker** (penyetuju ≠ pembuat); nomor diberi saat posting (`JU/...`, otomatis `JO/...`).
- **Mesin jurnal otomatis**: katalog event akuntansi + mapping akun per komponen (default + override per cabang), `IAutoJournalService` idempotent per dokumen sumber, endpoint preview.
- Laporan: Buku Besar & Neraca Saldo.

### Fase 3 — Pengadaan & Gudang ✅
- PO (Draft → Approved → PartiallyReceived/Received → Closed/Cancelled), hanya sapronak, harga exclude PPN.
- BPB/penerimaan: pakan & OVK → gudang induk; **DOC boleh langsung ke gudang kandang** (masuk siklus). Tidak boleh over-receipt.
- Transfer stok induk → induk/kandang; ke kandang dibebankan ke siklus terbuka.
- Saldo stok **moving average**, tidak bisa minus; kartu stok.
- Jurnal otomatis: `PurchaseReceipt` (Dr Persediaan / Cr GRNI), `StockTransferToCycle` (Dr Ayam Dalam Proses / Cr Persediaan).

### Fase 4 — Produksi ✅
- **Chick-in** mengambil DOC dari stok gudang kandang; populasi awal = DOC ditebar. ⚠️ Body endpoint `start` berubah (breaking change).
- **Daily recording** (mati, culling, BW, pakan/OVK) mengurangi stok gudang kandang; offline-ready (ID dari klien, idempotent); satu per hari; tidak boleh tanggal masa depan (+1 hari toleransi).
- **Revisi recording**: wajib alasan, riwayat nilai lama tersimpan, stok dikoreksi (reversal + issue ulang).
- **Retur** kandang → induk (jurnal baru `StockReturnFromCycle`: Dr Persediaan / Cr Ayam Dalam Proses) dan **mutasi pakan** A → induk → B dalam satu transaksi.
- **Panen** per truk; **performa** harian (deplesi, FCR, ADG, IP); **tutup siklus** hanya bila populasi 0 dan gudang kandang kosong — performa penutupan disimpan permanen.
- Seeder kini **melengkapi mapping jurnal default yang belum ada** meski COA sudah terisi.

### Fase 5 — Penjualan & AR ✅
- **Sales Order** (ekor = kuantitas mengikat, estimasi kg, harga/kg exclude PPN). **Credit limit dicek saat approve**: exposure customer lintas cabang = outstanding invoice + draft invoice + DO belum ditagih + sisa SO terbuka. Melebihi limit hanya lewat `approve-over-limit` (permission `sales:credit-override` + alasan). Limit 0 = tanpa kredit.
- **Delivery Order**: 1 baris = 1 data panen (truk), dikirim utuh. Satu panen hanya di satu DO aktif (partial unique index). Harga di-*snapshot* dari SO. Batal DO mengembalikan sisa SO.
- **Sales Invoice**: Draft (tanpa nomor) dari ≥1 DO → `post` (nomor `INV/...`, jurnal) → PartiallyPaid/Paid. PPN per baris dari TaxCode & tarif pada tanggal invoice (Exempt = 0). Invoice terposting final; hanya draft yang bisa dibatalkan.
- **Penerimaan customer** (`finance/customer-receipts`): ke akun kas/bank pilihan, dialokasikan ke invoice, parsial boleh, tidak boleh melebihi outstanding.
- **Kartu piutang & aging** (`finance/receivables/ledger`, `/aging`).
- **Tutup siklus** kini mensyaratkan semua panen ada di DO yang ditagih invoice **terposting**.
- Perbaikan: replay `Idempotency-Key` kini camelCase, sama dengan respons pertama (bug Fase 0).

## 5. Alur Akuntansi yang Sudah Berjalan

| Transaksi | Jurnal otomatis |
|---|---|
| Penerimaan sapronak (BPB) | Dr Persediaan DOC/Pakan/OVK — Cr Hutang Belum Ditagih (GRNI) |
| DOC langsung ke kandang | + Dr Ayam Dalam Proses — Cr Persediaan DOC |
| Transfer ke kandang | Dr Ayam Dalam Proses — Cr Persediaan |
| Retur dari kandang | Dr Persediaan — Cr Ayam Dalam Proses |
| Pemakaian harian | *(tanpa jurnal; biaya sudah di Ayam Dalam Proses, hanya kuantitas untuk FCR)* |
| Posting invoice penjualan | Dr Piutang Usaha — Cr Penjualan Ayam Hidup (DPP) & Cr PPN Keluaran |
| Penerimaan customer | Dr Kas/Bank (akun dipilih per transaksi) — Cr Piutang Usaha |

Sudah ada di katalog tetapi belum dipakai (untuk fase berikut): `VendorInvoice`, `VendorPayment`, `PlasmaSettlement`, `PlasmaPayment`, dan komponen `SalesInvoice.CostOfGoodsSold` (HPP, Fase 7).

## 6. Pelajaran & Jebakan Teknis

- **Entity anak dengan Guid Id buatan domain** (mis. `CycleHarvest`) wajib `ValueGeneratedNever()`; jika tidak, EF menjalankan UPDATE, bukan INSERT.
- **Dapper + Npgsql 10**: kolom `date` terbaca sebagai `DateOnly`; record posisional harus cocok tipe persis. Array PostgreSQL tidak bisa dipetakan ke konstruktor record → pakai dua result set.
- **Value object untuk EF complex type** butuh setter `private init` agar binding konstruktor jalan; provider **InMemory tidak mendukung query complex type** → di `TestDbContext` dipetakan sebagai owned type.
- Pesan error berisi angka diformat `InvariantCulture` (server ber-locale Indonesia).
- Setelah `dotnet ef migrations add`, **build ulang** sebelum menjalankan API dengan `--no-build`.
- Tanggal server saat pengembangan = 2026-09-30; data uji harus bertanggal ≤ hari ini.
- **Owned type di TestDbContext (InMemory)**: satu instance `Money` tidak boleh dipakai bersama oleh dua entity (mis. harga disalin dari baris SO ke baris DO). Simpan salinan (`money with { }`); di PostgreSQL (complex type) hal ini tidak bermasalah.
- Respons error API membawa kode error di field `title` (ProblemDetails), bukan `code`.

## 7. Cara Kerja & Verifikasi

- Setiap fase: domain + unit test invariant → command/query + validator → EF config + migration → endpoint + permission → **verifikasi end-to-end** ke PostgreSQL lokal pada database sementara `intiplasma_verify` (dibuat & dihapus otomatis; database `intiplasma` milik user tidak disentuh).
- User yang melakukan **commit & migrate** setelah tiap fase.
- Status test saat ini: **134 test lulus** (85 domain, 31 application, 8 arsitektur, 10 integration).
- Integration test (Testcontainers) **sudah bisa dijalankan** di mesin dev (container runtime tersedia) — `dotnet test IntiPlasma.slnx`.
- Verifikasi end-to-end Fase 5 memakai script Node (fetch + psql) terhadap API di port 5099 dengan `ConnectionStrings__Database` diarahkan ke `intiplasma_verify`.
- Migration yang ada: `Initial`, `Phase1_MasterData_Partnership`, `Phase2_FinanceCore`, `Phase3_ProcurementInventory`, `Phase4_Production`, `Phase5_SalesReceivables`.

## 8. Catatan Terbuka / Hutang Teknis

- Tarif & fasilitas pajak (PPN dibebaskan untuk DOC/pakan/ayam hidup, PPh) perlu dikonfirmasi konsultan pajak lalu diinput sebagai TaxCode.
- Kegagalan jurnal otomatis hanya terlihat di `infrastructure.outbox_messages.error` → perlu layar monitoring di WebApp.
- `IdempotencyFilter` masih in-memory → tambah Redis sebelum scale-out.
- Belum ada penutupan tahun buku (laba/rugi → laba ditahan) → Fase 8.
- Satu admin tunggal tidak bisa menyelesaikan jurnal manual (maker-checker) → perlu user kedua.
- **Presisi rasio DPP**: `TaxBaseRatio` (10,8) menyimpan 11/12 sebagai 0,91666667 → PPN meleset beberapa sen (DPP 40,4 jt → PPN 4.444.000,02, seharusnya 4.444.000). Perlu diputuskan: simpan sebagai pecahan, atau pakai tarif efektif 11% dengan rasio 1 (konfirmasi konsultan pajak).
- Penerimaan customer belum bisa di-*void*; belum ada uang muka penjualan, nota kredit/retur penjualan.

## 9. Langkah Berikutnya — Fase 6: AP & Cash/Bank

Rencana (lihat PLAN.md §4 & §5):
- Vendor Invoice dengan **3-way match** PO–BPB–Invoice, PPN Masukan & PPh dipotong → jurnal `VendorInvoice` (GRNI / Hutang Usaha / PPN Masukan / Hutang PPh).
- Payment Voucher (multi/parsial) → jurnal `VendorPayment`; AP ledger & aging (pola sama dengan AR Fase 5).
- Cash In/Out, Petty Cash, Bank Transfer, Bank Reconciliation, Cash & Bank Ledger.
- Pertimbangkan: *void* penerimaan customer (dengan jurnal pembalik) dan uang muka penjualan.
- Setelah itu: Fase 7 (HPP & Settlement Plasma — termasuk pengakuan HPP penjualan), Fase 8 (Laporan Keuangan & pajak).
