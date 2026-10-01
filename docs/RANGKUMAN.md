# Rangkuman Proyek — Aplikasi Peternakan Ayam Broiler Inti-Plasma

> Rangkuman poin penting dari sesi pengembangan 2026-09-30 (Fase 0 s.d. Fase 4) dan 2026-10-01 (Fase 5–8).
> Detail lengkap per fase ada di [PLAN.md](PLAN.md) §6–§14. Seluruh fase rencana awal (0–8) selesai; lihat §9 untuk backlog berikutnya.

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
| 11 | PPN "DPP nilai lain" (12% × 11/12) | Diinput sebagai **tarif efektif 11% dengan rasio DPP 1** (menghindari selisih sen akibat presisi rasio) |
| 12 | Void penerimaan customer, uang muka penjualan, nota kredit/retur penjualan | **Masuk Fase 6** (selesai) |
| 13 | Selisih harga invoice vendor vs PO | **Toleransi % per vendor** (default 0); selisih ke akun Selisih (5-1301); di atas toleransi perlu approval (`payables:approve-variance` + alasan) |
| 14 | Payment Voucher | **Maker-checker**: Draft → Approved (user lain) → Paid |
| 15 | Kas & bank | **Master Kas/Bank** (Kas / Bank / Kas Kecil per cabang) → tepat satu akun COA |
| 16 | Rekonsiliasi bank | Rekening koran diinput / diimpor **CSV**, dicocokkan (auto ±3 hari atau manual) dengan mutasi buku |
| 17 | Pengakuan HPP penjualan | **Per invoice dengan HPP estimasi** (biaya terpakai / bobot hidup), dikoreksi saat tutup siklus (menggantikan #9) |
| 18 | Pembayaran plasma | Payment Voucher dengan penerima **vendor atau peternak plasma** |
| 19 | Settlement harga kontrak negatif | Menjadi **piutang plasma**, bisa dipotong dari settlement berikutnya |
| 20 | Bagi hasil | % × (penjualan bersih − biaya siklus); **rugi ditanggung inti** |
| 21 | Harga jaminan ayam | **Per panen (truk)** sesuai BW rata-rata panen |
| 22 | Approval settlement | **Maker-checker** |
| 23 | Tutup tahun buku | **Jurnal penutup** saat Desember ditutup (P&L → Laba Ditahan per cabang); buka kembali Desember membalik jurnal itu |
| 24 | Arus kas | **Metode langsung**, kategori arus kas per akun COA (Operating/Investing/Financing) |
| 25 | Profitabilitas | **Tanpa alokasi overhead** (margin kontribusi); overhead terlihat per cabang |
| 26 | Laporan pajak | **Rekap PPN & PPh + ekspor CSV**; XML Coretax menyusul |

## 3. Arsitektur & Konvensi Kode

**Struktur**
- Monolith dengan bounded context per folder + **schema PostgreSQL** sendiri: `identity`, `infrastructure`, `master`, `partnership`, `finance`, `procurement`, `inventory`, `production`, `sales`.
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

### Fase 6 — AP, Kas & Bank, sisa AR ✅
- **Master Kas/Bank** (`finance/cash-bank-accounts`) + **buku kas/bank** (`{id}/ledger`).
- **Kas masuk/keluar** (`finance/cash-transactions`): keluar wajib maker-checker; nomor `BKM`/`BKK` saat posting; jurnal dari baris dokumen sendiri (`IAutoJournalService.PostLinesAsync`).
- **Transfer** antar kas/bank satu cabang (`finance/bank-transfers`), termasuk pengisian kas kecil.
- **Vendor Invoice** (`finance/vendor-invoices`): 3-way match ke baris BPB (qty & nilai tertagih disimpan di BPB), PPN masukan dari PO, PPh dipotong opsional, selisih harga + toleransi vendor; Draft → `post`/`post-with-variance` (nomor `VI`).
- **Payment Voucher** (`finance/payment-vouchers`): Draft → approve (checker) → pay (tanggal aktual); **kartu & aging hutang** (`finance/payables`).
- **Rekonsiliasi bank** (`finance/bank-reconciliations`): impor CSV, auto/manual match, selesai bila semua cocok & saldo seimbang; mutasi buku hanya bisa clear sekali.
- **Sisa AR**: uang muka penjualan (mengurangi exposure credit limit) + penerapan ke invoice; **void** penerimaan (jurnal dibalik, `IAutoJournalService.ReverseAsync`); **nota kredit** (`sales/credit-notes`).
- ⚠️ Body `POST finance/customer-receipts` berubah: `cashBankAccountId` menggantikan `branchId` + `cashAccountId`; tambah `advanceAmount`.
- Seeder mapping kini juga menambah **komponen** baru ke mapping default yang sudah ada.

### Fase 7 — HPP & Settlement Plasma ✅
- **HPP siklus** (`cycles/{id}/cost`) dari kartu stok gudang kandang: DOC ditebar + pakan/OVK terpakai pada harga moving average. HPP/kg berjalan = biaya terpakai / (kg dipanen + populasi × BW terakhir).
- **HPP per invoice**: saat posting, baris invoice mendapat `costAmount` (HPP/kg berjalan × kg) dan dijurnal Dr HPP / Cr Ayam Dalam Proses. **Tutup siklus** membekukan `ClosingCost` dan menjurnal penyesuaian (`CycleCostAdjustment`), sehingga Ayam Dalam Proses siklus = 0.
- **Settlement plasma** (`costing/settlements`, schema `costing`): `ISettlementPolicy` per skema (harga kontrak per panen − sapronak @harga kontrak ± insentif; bagi hasil % laba). Ada PPh kontrak dan potongan hutang. Rugi menjadi piutang plasma. Draft (hitung ulang) → approve oleh checker → jurnal + siklus `Settled`.
- **PV plasma** (`finance/payment-vouchers/plasma`): PV kini generik (`payeeType`, antarmuka `IPayable`) dengan jurnal `PlasmaPayment`. ⚠️ Respons PV berubah (payee*, alokasi `documentType/documentId/...`).

### Fase 8 — Laporan Keuangan, Tutup Buku & Pajak ✅
- **Laporan** (`finance/reports`): `income-statement`, `balance-sheet` (dengan laba tahun berjalan & tahun lalu yang belum ditutup), `cash-flow` (metode langsung, konsisten dengan saldo kas buku besar), `profitability` (per siklus/kandang/peternak/cabang).
- **Kategori arus kas** per akun COA (`cashFlowCategory`); migration mengklasifikasikan akun yang ada.
- **Checklist tutup periode** (`fiscal-periods/{id}/checklist`): pemblokir (jurnal draft, event jurnal otomatis tertunda/gagal) & peringatan (dokumen draft, DO belum ditagih, bank belum direkonsiliasi).
- **Tutup tahun buku**: menutup Desember memposting jurnal penutup per cabang ke Laba Ditahan (akun kredit mapping `YearEndClosing.NetIncome`); buka kembali Desember membaliknya.
- **Monitoring event gagal** (`system/failed-events`, retry).
- **Pajak** (`finance/tax`): rekap PPN keluaran/retur/masukan & PPh dipotong per masa + ekspor CSV.

## 5. Alur Akuntansi yang Sudah Berjalan

| Transaksi | Jurnal otomatis |
|---|---|
| Penerimaan sapronak (BPB) | Dr Persediaan DOC/Pakan/OVK — Cr Hutang Belum Ditagih (GRNI) |
| DOC langsung ke kandang | + Dr Ayam Dalam Proses — Cr Persediaan DOC |
| Transfer ke kandang | Dr Ayam Dalam Proses — Cr Persediaan |
| Retur dari kandang | Dr Persediaan — Cr Ayam Dalam Proses |
| Pemakaian harian | *(tanpa jurnal; biaya sudah di Ayam Dalam Proses, hanya kuantitas untuk FCR)* |
| Posting invoice penjualan | Dr Piutang Usaha — Cr Penjualan Ayam Hidup (DPP) & Cr PPN Keluaran |
| Penerimaan customer | Dr Kas/Bank (dari kas/bank penerimaan) — Cr Piutang Usaha (bagian dialokasikan) & Cr Uang Muka Penjualan (uang muka) |
| Penerapan uang muka | Dr Uang Muka Penjualan — Cr Piutang Usaha |
| Void penerimaan | Jurnal pembalik (`CustomerReceipt.Reversal`) pada tanggal void |
| Nota kredit penjualan | Dr Potongan & Retur Penjualan + Dr PPN Keluaran — Cr Piutang Usaha |
| Vendor invoice | Dr GRNI (nilai BPB) + Dr/Cr Selisih Harga + Dr PPN Masukan — Cr Hutang Usaha; Dr Hutang Usaha — Cr Hutang PPh |
| Payment voucher | Dr Hutang Usaha — Cr Kas/Bank (dari PV) |
| Kas masuk / keluar | Dr Kas/Bank — Cr akun baris / Dr akun baris — Cr Kas/Bank (tanpa mapping) |
| Transfer kas/bank | Dr kas/bank tujuan — Cr kas/bank asal (tanpa mapping) |

| HPP penjualan (per invoice) | Dr HPP Ayam Hidup — Cr Ayam Dalam Proses (HPP/kg estimasi × kg) |
| Tutup siklus | Penyesuaian HPP: Dr/Cr HPP — Cr/Dr Ayam Dalam Proses (biaya final − HPP diakui) |
| Settlement plasma (disetujui) | Dr Beban Kemitraan — Cr Hutang Plasma; Dr Hutang Plasma — Cr Hutang PPh / Cr Piutang Plasma (potongan); rugi: Dr Piutang Plasma — Cr Beban Kemitraan |
| Pembayaran plasma (PV) | Dr Hutang Plasma — Cr Kas/Bank |
| Tutup tahun buku (tutup Desember) | Dr pendapatan / Cr beban (nolkan) — selisih ke Laba Ditahan; dibalik bila Desember dibuka kembali |

Semua event di katalog kini sudah dipakai.

## 6. Pelajaran & Jebakan Teknis

- **Entity anak dengan Guid Id buatan domain** (mis. `CycleHarvest`) wajib `ValueGeneratedNever()`; jika tidak, EF menjalankan UPDATE, bukan INSERT.
- **Dapper + Npgsql 10**: kolom `date` terbaca sebagai `DateOnly`; record posisional harus cocok tipe persis. Array PostgreSQL tidak bisa dipetakan ke konstruktor record → pakai dua result set.
- **Value object untuk EF complex type** butuh setter `private init` agar binding konstruktor jalan; provider **InMemory tidak mendukung query complex type** → di `TestDbContext` dipetakan sebagai owned type.
- Pesan error berisi angka diformat `InvariantCulture` (server ber-locale Indonesia).
- Setelah `dotnet ef migrations add`, **build ulang** sebelum menjalankan API dengan `--no-build`.
- Tanggal server saat pengembangan = 2026-09-30; data uji harus bertanggal ≤ hari ini.
- **Owned type di TestDbContext (InMemory)**: satu instance `Money` tidak boleh dipakai bersama oleh dua entity (mis. harga disalin dari baris SO ke baris DO). Simpan salinan (`money with { }`); di PostgreSQL (complex type) hal ini tidak bermasalah.
- Respons error API membawa kode error di field `title` (ProblemDetails), bukan `code`.
- **Rate limiter global**: 100 request/menit per user (`RateLimiting:Global`). Skrip verifikasi end-to-end menaikkannya lewat env `RateLimiting__Global__PermitLimit=10000`.
- Hindari `sed` bernomor baris saat menyunting file yang baru diubah — pernah menimpa baris `HasKey` di `TestDbContext`. Pakai Edit dengan konteks unik.
- Dokumen yang jurnalnya diposting lewat outbox (VI, PV, kas, void, nota kredit) mengecek **periode fiskal terbuka** saat posting, agar tidak jatuh ke dead letter.

## 7. Cara Kerja & Verifikasi

- Setiap fase: domain + unit test invariant → command/query + validator → EF config + migration → endpoint + permission → **verifikasi end-to-end** ke PostgreSQL lokal pada database sementara `intiplasma_verify` (dibuat & dihapus otomatis; database `intiplasma` milik user tidak disentuh).
- User yang melakukan **commit & migrate** setelah tiap fase.
- Status test saat ini: **156 test lulus** (104 domain, 34 application, 8 arsitektur, 10 integration).
- Integration test (Testcontainers) **sudah bisa dijalankan** di mesin dev (container runtime tersedia) — `dotnet test IntiPlasma.slnx`.
- Verifikasi end-to-end (Fase 5: 43 skenario, Fase 6: 70 skenario, Fase 7: 32 skenario, Fase 8: 35 skenario) memakai script Node (fetch + psql) terhadap API di port 5099 dengan `ConnectionStrings__Database` diarahkan ke `intiplasma_verify`. Skenario maker-checker memakai user kedua (role `Checker`) yang dibuat lewat API.
- Migration yang ada: `Initial`, `Phase1_MasterData_Partnership`, `Phase2_FinanceCore`, `Phase3_ProcurementInventory`, `Phase4_Production`, `Phase5_SalesReceivables`, `Phase6_PayablesCashBank`, `Phase7_CostingSettlement`, `Phase8_ReportingClosing`.

## 8. Catatan Terbuka / Hutang Teknis

- Tarif & fasilitas pajak (PPN dibebaskan untuk DOC/pakan/ayam hidup, PPh) perlu dikonfirmasi konsultan pajak lalu diinput sebagai TaxCode.
- Kegagalan jurnal otomatis kini terlihat lewat API `system/failed-events` (+ retry) dan memblokir tutup periode → tinggal layar di WebApp.
- `IdempotencyFilter` masih in-memory → tambah Redis sebelum scale-out.
- XML Coretax (e-Faktur & e-Bupot) belum ada; baru rekap + CSV.
- Satu admin tunggal tidak bisa menyelesaikan jurnal manual (maker-checker) → perlu user kedua.
- Presisi rasio DPP: `TaxBaseRatio` (10,8) menyimpan 11/12 sebagai 0,91666667 sehingga PPN bisa meleset beberapa sen. **Diputuskan**: PPN 12% DPP nilai lain diinput sebagai tarif 11% dengan rasio 1. Rasio ≠ 1 tetap didukung, tetapi hindari pecahan berulang.
- Vendor invoice hanya untuk barang ber-PO; hutang non-PO (jasa) & nota debit vendor belum ada (sementara lewat kas keluar).
- Transfer antar cabang belum didukung (perlu akun antar-cabang).
- PPh vendor selalu ke akun mapping `IncomeTaxWithheld` (default Hutang PPh 23); PPh 22/4(2) perlu override.
- Exposure credit limit memuat semua penerimaan customer ke memori (uang muka belum diterapkan) — perlu query SQL bila datanya besar.
- Biaya siklus baru sapronak; biaya lain/overhead (listrik, tenaga kerja, penyusutan kandang inti) belum dialokasikan ke siklus.
- Belum ada sub-ledger piutang plasma per peternak: potongan hutang di settlement hanya dibatasi pendapatan − PPh, belum dicek terhadap saldo piutangnya.
- Siklus lama yang ditutup sebelum Fase 7 tidak punya `ClosingCost`; invoice lama punya `costAmount` 0 (tidak ada penyesuaian HPP untuk siklus tersebut).

## 9. Langkah Berikutnya — Backlog setelah Fase 8

Seluruh fase rencana awal (0–8) selesai di sisi API. Kandidat berikutnya (urutan bisa disepakati):
- **Klien**: MVC WebApp Admin Office (keputusan #4) dan mobile app PPL (recording offline sudah siap di API).
- **Pajak**: ekspor XML Coretax (e-Faktur & e-Bupot) setelah format dikonfirmasi konsultan pajak; input tarif final PPN/PPh.
- **Akuntansi lanjutan**: hutang non-PO (jasa) & nota debit vendor; transaksi antar cabang (akun antar-cabang); alokasi overhead ke siklus; sub-ledger piutang plasma per peternak; aset tetap & penyusutan.
- **Operasional/infra**: Redis untuk idempotency & cache (multi-instance), integration test per modul (Testcontainers sudah jalan), observabilitas (Seq/OTel).
