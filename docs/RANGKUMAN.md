# Rangkuman Proyek — Aplikasi Peternakan Ayam Broiler Inti-Plasma

> Rangkuman poin penting dari sesi pengembangan 2026-09-30 (Fase 0 s.d. Fase 4), 2026-10-01 (Fase 5–8), 2026-10-02 (Fase 9 — lampiran dokumen; Fase W0 — fondasi WebApp), 2026-10-03 (Fase W1–W8) dan 2026-10-04 (Fase W9–W10).
> Detail lengkap per fase ada di [PLAN.md](PLAN.md) §6–§16 (API) dan [PLAN-WEBAPP.md](PLAN-WEBAPP.md) (WebApp, realisasi W0 di §10, W1 di §12, W2 di §13, W3 di §14, W4 di §15, W5 di §16, W6 di §17, W7 di §18, W8 di §20, W9 di §22, W10 di §24). Fase API 0–9 dan **W0–W10 selesai** (rencana awal WebApp tuntas); berikutnya dipilih dari backlog §9.

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
| 27 | Lampiran dokumen | Entity `Attachment` + `Guid[] Documents` di 21 entity (peternak, kandang, vendor, customer, kontrak, chick-in, panen, recording + revisi, BPB, retur, transfer, mutasi pakan, PO, SO, DO, VI, PV, kas, penerimaan, jurnal manual, settlement) |
| 28 | Lampiran setelah dokumen final | Boleh lewat `PUT …/documents` di semua status kecuali **Cancelled/Voided** |
| 29 | Hapus lampiran terpakai | **Ditolak (409)**; lampiran yatim (Temporary > 24 jam) dibersihkan job |
| 30 | Tipe & ukuran | **JPEG/PNG/WEBP + PDF, maks 10 MB**, maks 20 lampiran per entity; storage lokal dulu (S3/MinIO menyusul) |

## 3. Arsitektur & Konvensi Kode

**Struktur**
- Monolith dengan bounded context per folder + **schema PostgreSQL** sendiri: `identity`, `infrastructure`, `master`, `partnership`, `finance`, `procurement`, `inventory`, `production`, `sales`, `costing`, `documents`.
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

### Fase 9 — Lampiran Dokumen ✅
- **Upload dulu, lalu link**: `POST attachments` (multipart) → id → dikirim sebagai `documents` di create/update, atau belakangan lewat `PUT {resource}/{id}/documents` (juga untuk dokumen approved/posted/paid; cancelled/voided ditolak).
- Tipe dideteksi dari isi file (*magic bytes*), SHA-256, path `yyyy/MM/{id}`; upload ulang dengan `id` sama idempotent (cocok untuk mobile offline).
- Tabel `documents.attachment_links` = indeks balik (satu lampiran bisa punya beberapa pemilik, mis. mutasi pakan). Lampiran Temporary hanya terlihat oleh pengunggah; Linked mengikuti cabang pemilik.
- Pre-check lampiran dipanggil **sebelum** nomor dokumen diambil. Job `AttachmentCleanupJob` menghapus lampiran yatim. Detail respons kini memuat `documents`.
- Permission baru `attachments:upload|read|delete`. ⚠️ `PUT contracts/{id}` body tetap; lampiran kontrak lewat `PUT contracts/{id}/documents`.

### Fase W0 — Fondasi WebApp ✅ (detail: PLAN-WEBAPP §10)
- **Web.App = aplikasi langsung** (referensi Infrastructure, memanggil handler yang sama dengan API). `AddInfrastructure` ⚠️ diganti `AddInfrastructureCore` + `AddJwtAuthentication` (API) + `AddBackgroundJobs`.
- **Background job pindah ke Web.App**: Web.Api `BackgroundJobs:Enabled=false`; cleanup lampiran memakai advisory lock; health check `outbox`.
- **Invalidasi cache lintas proses** via PostgreSQL `LISTEN/NOTIFY` (`ICacheInvalidator`), sehingga perubahan akses langsung berlaku di Web.Api & Web.App.
- `User.IsActive` + `SecurityStamp` (migration `PhaseW0_UserStatus`); ⚠️ login & refresh API menolak user nonaktif (`Users.Inactive`); ganti password mencabut refresh token.
- UI: mainboard + iframe (hash `/#/path`, Back/refresh/deep link), login cookie dengan validasi stamp, ganti password, pemilih cabang, halaman error, tema `#0984E3`, aset 84 → 21 MB, UI English + format id-ID.
- Pondasi untuk fase berikut: `FormTokenFilter` (double-submit), `DbExceptionFilter` (concurrency), `[WorkflowAction]`, partial umum, `DisplayFormatter`.

### Fase W1 — Akses Menu, Akses Cabang & Administrasi ✅ (detail: PLAN-WEBAPP §12)
- **Akses Menu** (profil hak View/Create/Edit/Delete/Export per menu) & **Akses Cabang** (semua/daftar cabang): master ber-CRUD, satu profil dipakai banyak user, satu profil per jenis per user. Profil sistem Full Access & All Branches.
- `BranchAccess` kini dari profil Akses Cabang (berlaku API & WebApp); ⚠️ `branches:access-all` & `PUT users/{id}/branches` dihapus → `branch-access-profiles` + `PUT users/{id}/access`. Migration `PhaseW1_AccessControl` memindahkan data lama.
- Katalog menu di kode (`MenuCatalog`, disinkron saat startup; menu belum rilis tersembunyi), `[MenuAccess]` di setiap action (dijaga test arsitektur), sidebar & pencarian menu hanya `CanView`.
- Administrasi di Web.App: Users (password awal, akses, role API, aktif/nonaktif, reset password, **hapus dengan backup `identity.user_old`**), Menu Access (matriks), Branch Access, Menus, API Roles, Branches.
- ⚠️ Setelah migrate, user non-Administrator perlu diberi Akses Menu dulu sebelum bisa login ke Web.App.

### Fase W2 — Ekspor & Master Data ✅ (detail: PLAN-WEBAPP §13)
- **Fondasi ekspor**: Excel (ClosedXML, nilai bertipe + autofilter) & PDF list (QuestPDF Community, kop + "Page x of y"), mengikuti filter halaman, batas 50.000 baris, nama file `{nama}_{cabang}_{waktu}`; hanya dengan hak Export (termasuk cetak PDF).
- Layar **Master Data** (UoM, Tax Codes + tarif, Items + konversi, Warehouses, Vendors, Customers) dan **Partnership** (Farmers, Coops, Contracts Draft → Active → Inactive + **PDF kontrak**).
- Komponen bersama: lampiran (upload AJAX, pratinjau), lookup Tom-Select (item, peternak), tabel baris dinamis, filter cabang (`branch=all`).
- Master & partnership tanpa Delete (nonaktifkan lewat flag Active); form kontrak satu halaman bersection. Tidak ada migration baru.
- ⚠️ Culture request Web.App kini **en-US** (binding input angka/tanggal HTML5); tampilan tetap format id-ID lewat `DisplayFormatter`.

### Fase W3 — Finance Setup ✅ (detail: PLAN-WEBAPP §14)
- Layar **Chart of Accounts** (tree header/detail, akun kontra, kategori arus kas, ekspor COA), **Cost Centers**, **Fiscal Periods** (buka tahun, checklist penutupan, tutup/buka kembali berurutan; Desember memposting jurnal penutup tahun), **Journal Templates**, **Auto Journal Mappings** (per event & komponen, default + override cabang), **Cash/Bank Accounts** (akun buku sendiri, saldo).
- Lookup akun COA (`/Lookup/Accounts`); hak menu Create/Edit/Export (tanpa Delete). Tidak ada migration baru.
- ⚠️ Perbaikan bug W2: switch Active yang dimatikan sebelumnya tidak menonaktifkan master (default `IsActive = true` di view model).

### Fase W4 — Pengadaan & Gudang ✅ (detail: PLAN-WEBAPP §15)
- **Purchase Orders** (draft → approve → diterima sebagian/penuh → close; cancel dengan alasan; PPN estimasi; **PDF PO**), **Goods Receipts** (dari PO, outstanding terisi otomatis, **PDF BPB**), **Stock Transfers**, **Stock Returns**, **Feed Mutations** (retur + transfer sekaligus), **Stock Balance & Card** (ekspor Excel/PDF); PDF transfer & retur.
- Satuan baris mengikuti item (dasar + konversi), item transfer/retur dari stok gudang asal ("on hand"), tujuan gudang difilter per cabang. Tidak ada migration baru.
- ⚠️ Perbaikan global: aturan `step` jquery-validation (nilai seperti `20.000000` dari DB ditolak tanpa pesan sehingga form edit tidak tersimpan).

### Fase W5 — Produksi ✅ (detail: PLAN-WEBAPP §16)
- **Cycles & Chick-in**: plan (coop + kontrak), halaman siklus bertahap (Planned → DOC in coop → Chick-in → Harvesting → Closed) dengan KPI dan tab Recordings, Harvests, Coop Stock, Performance (grafik), Cost, Attachments; chick-in, cancel, close, **PDF ringkasan siklus**.
- **Daily Recordings** (input admin + revisi dengan timeline history, W-11) dan **Harvests** per truk (lampiran tiket timbangan); ekspor Excel/PDF. Tidak ada migration baru.
- Tutup siklus mensyaratkan panen terjual (SO → DO → invoice terposting) — kini lewat layar penjualan W6.

### Fase W6 — Penjualan & AR ✅ (detail: PLAN-WEBAPP §17)
- **Sales Orders** (panel kredit customer; approve dalam limit, di atas limit dengan alasan), **Delivery Orders** (panen per truk, **PDF surat jalan**), **Sales Invoices** (draft → post, **PDF dengan terbilang**), **Credit Notes** (PDF), **Customer Receipts** (alokasi ke invoice, uang muka, apply, void, **PDF receipt**), **Receivable Ledger & Aging** (ekspor).
- Tambahan Application: `GetCustomerCreditQuery` (exposure kredit). Helper **terbilang** Rupiah (W-17). Tidak ada migration baru.
- ⚠️ Credit override di WebApp memakai hak Edit Sales Orders + alasan wajib (API: permission `SalesCreditOverride`).

### Fase W7 — AP, Kas & Bank ✅ (detail: PLAN-WEBAPP §18)
- **Vendor Invoices** (baris BPB belum ditagih, post; di atas toleransi → *post with variance* + alasan), **Payment Vouchers** vendor & plasma (maker-checker: approve oleh user lain, pay dengan tanggal aktual, **PDF PV dengan terbilang**), **Cash In/Out** (BKM langsung post; BKK approve oleh user lain, **PDF voucher**), **Bank Transfers**, **buku kas/bank** (dari Cash/Bank Accounts), **Bank Reconciliations** (impor CSV, dua kolom, auto-match ±3 hari & match manual, complete), **Payable Ledger & Aging** (ekspor). Tidak ada migration baru.
- Komponen bersama: `_DocumentFilter`, `_ReasonModal`, `_AttachmentsCard`, `DocumentLists`, `DocumentPdf`.
- ⚠️ Selisih harga VI di WebApp memakai hak Edit Vendor Invoices + alasan wajib (API: `payables:approve-variance`).
- ⚠️ Perbaikan bug W6: route menu Sales Orders/Delivery Orders/Sales Invoices di sidebar sebelumnya 404; kini ada test yang memastikan semua menu rilis terbuka.

### Fase W8 — HPP & Settlement Plasma ✅ (detail: PLAN-WEBAPP §19–§20)
- **Cycle Cost**: list HPP lintas siklus (`GetCycleCostsQuery`: final dari `closing_cost`, berjalan dari kartu stok kandang) + rincian per item, ekspor list & rincian.
- **Plasma Settlements**: draft dari siklus plasma tertutup (lookup `SettleableCycles` atau tombol di siklus), info saldo hutang plasma + potongan, recalculate, approve checker, cancel (alasan), Pay → PV plasma, **PDF settlement dengan terbilang**, ekspor. Tidak ada migration baru.
- ⚠️ Approve settlement di WebApp memakai hak Edit + maker-checker domain (API: `SettlementsApprove`).

### Fase W9 — Jurnal, Laporan, Tutup Buku, Pajak & Dashboard ✅ (detail: PLAN-WEBAPP §21–§22)
- **Journals**: jurnal manual (template, total/selisih langsung, draft → approve checker → post → reverse, hapus draft, **PDF journal voucher**) + jurnal otomatis (tautan ke dokumen sumber); kartu **Journal** di detail dokumen W4–W8 (`GetDocumentJournalsQuery`).
- **Reports**: GL, Trial Balance, Laba Rugi, Neraca, Arus Kas, Profitabilitas, Rekap Pajak (PPN/PPh + CSV) — satu model `ReportDocument` untuk HTML, **Excel berstruktur** (lembar per tabel) dan PDF.
- **Failed Events** (retry) + tautan dari checklist tutup periode; **Dashboard** KPI, daftar menunggu tindakan, grafik 6 bulan (`GetDashboardSummaryQuery`). Semua menu katalog kini dirilis. Tidak ada migration baru.
- ⚠️ Perbaikan: definisi dead letter (event yang masih di-retry tidak lagi dihitung gagal).

### Fase W10 — Pengerasan ✅ (detail: PLAN-WEBAPP §23–§24)
- **Lockout login** 5 kali / 15 menit di Web.App **dan** Web.Api (⚠️ `POST users/login` → `Users.LockedOut`; endpoint baru `POST users/{id}/unlock`), badge Locked + **Unlock** di Users; rate limit `POST /Auth/Login` 10/menit/IP.
- **Audit Log** (`infrastructure.audit_logs`, menu Administration → Audit Log): perubahan akses (decorator `IAuditedCommand`, web & API), ekspor/cetak (`ExportAuditFilter`), login; password disamarkan.
- **CSP ketat** dengan nonce (handler inline dipindah ke `app.js`), header keamanan tambahan, cookie Secure (`Security:SecureCookies`), forwarded headers; **Data Protection** key ring di PostgreSQL (sesi bertahan restart/replika).
- Health `/health/live`, `/health/ready`, `/health` (+ cek storage & listener); Dockerfile non-root + HEALTHCHECK, `docker-compose.yml` siap produksi + `.env.example`, `Database:SeedOnStartup` untuk instalasi pertama, `docs/DEPLOY.md`.
- UI: font Nunito di-host sendiri (template memanggil Google Fonts), `style.min.css`, aset ber-fingerprint, **sidebar tablet diperbaiki** (sebelumnya tidak bisa dibuka < 992 px), kontras AA & nama aksesibel (axe 0 critical/serious kecuali tombol primer), peringatan sesi habis. Panduan pengguna `docs/user-guide/`.
- Migration **`PhaseW10_Hardening`**.

### Audit Integrasi & Data Dummy (2026-10-04)
- **Data dummy** (`src/Infrastructure/Database/DemoData/`, panduan [DEMO-DATA.md](DEMO-DATA.md)): `dotnet run --project src/Web.Api -- --Seed:DemoData=true` pada DB tanpa cabang (bukan Production). Seluruh data dibuat lewat command handler asli dengan impersonasi user (admin = maker, `checker@intiplasma.local` = checker), dan outbox diproses inline. Simulasi berjalan harian selama 95 hari: 2 cabang, 8 siklus di semua status (Planned → Settled), ±720 command. Tidak ada migration.
- Hasil verifikasi pada `intiplasma_verify`:
  - outbox 0 gagal; Dr = Cr;
  - nilai stok = GL persediaan; GRNI = 0;
  - 55 menu + 28 halaman detail Web.App terbuka berisi data;
  - user `staff.bdg` hanya melihat data BDG.
- Hasil audit: semua modul API & WebApp lengkap dan terhubung (lihat celah di §8).

### Fase M0 — Fondasi Mobile App ✅ (2026-10-08; detail: PLAN-MOBILE §12)
- Rencana mobile PPL & Manager: [PLAN-MOBILE.md](PLAN-MOBILE.md) (M0–M10, keputusan M-1 s.d. M-39 disepakati).
- API: permission baru (`partnership:assigned-only`, `production:stock-report`, `inventory:request-feed`, `inventory:request-feed-mutation`, `approvals:decide`), role default **PPL** & **Manager** di seeder, `GET users/me` (+ roles & permissions), `POST users/logout`, `POST users/me/change-password`, refresh token **30 hari** (`Jwt:RefreshTokenExpirationInDays`). Tidak ada migration.
- Proyek baru `src/MobileApp.Core` (API client + refresh token, sesi, SQLite, format id-ID, menu per permission) & `tests/MobileApp.UnitTests`; `src/MobileApp` (MAUI Blazor Hybrid, Android) dengan template Findee, logo `rdi-*`, primary `#0984E3`: Login, Beranda, Lainnya, Profil/ganti password, Pengaturan, offline dengan sesi tersimpan.
- ⚠️ Butuh Android System WebView yang diperbarui (WebView bawaan API 26 tidak bisa menjalankan Blazor .NET 10).

### Fase M1 — Data Kemitraan & Penugasan PPL ✅ (2026-10-09; detail: PLAN-MOBILE §14)
- Migration **`PhaseM1_FieldOfficerScope`**: `field_officer_user_id` di `master.farmers` & `master.coops` (FK user, ON DELETE SET NULL).
- `IFieldScope`: user dengan `partnership:assigned-only` (bukan Administrator) hanya melihat peternak/kandang yang ditugaskan (+ siklus, recording, grafik, lampiran); di luar lingkup → 404. Kontrak tetap lingkup cabang (M-38 direvisi). Web.App tidak memakai lingkup PPL.
- API: `GET users/field-officers`, filter `fieldOfficerId` (farmers/coops/cycles), `fieldOfficerUserId` di create/update farmer & coop, `warehouseId` di response coop.
- Web.App: field & filter PPL di Farmers/Farms, menu **Partnership → Field Officer Assignment** (audit log).
- Mobile: Peternak, Kandang, Kontrak (list + detail), cache read-only offline, filter PPL untuk Manager.

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
- **Kolom array (`uuid[]`) + Dapper**: row/response harus kelas/record dengan setter atau `init`; record posisional gagal di-*bind*. Di `TestDbContext` (InMemory) `Guid[]` jalan tanpa converter, tetapi `AttachmentLink` perlu `HasKey` eksplisit.
- Penomoran dokumen (`IDocumentNumberGenerator`) langsung *commit* di luar transaksi EF → semua validasi (termasuk lampiran) harus selesai **sebelum** nomor diambil.
- Hindari `sed` bernomor baris saat menyunting file yang baru diubah — pernah menimpa baris `HasKey` di `TestDbContext`. Pakai Edit dengan konteks unik.
- Dokumen yang jurnalnya diposting lewat outbox (VI, PV, kas, void, nota kredit) mengecek **periode fiskal terbuka** saat posting, agar tidak jatuh ke dead letter.
- **jQuery 4** (bawaan template) menghapus `$.parseJSON/$.trim/$.isFunction/$.isArray` yang masih dipakai jquery-validation-unobtrusive → shim `wwwroot/js/jquery-compat.js` wajib dimuat tepat setelah jQuery.
- Di .NET 10 kelas `Program` top-level bersifat public: project yang mereferensikan Web.Api **dan** Web.App harus memakai `Web.Api.Program` / `Web.App.Program` secara eksplisit.
- Browser tidak mengirim fragment (`#…`) ke server: deep link mainboard dibawa ke `ReturnUrl` oleh JavaScript halaman login.
- FluentValidation `ErrorCode` bukan nama property → error validasi command tampil di validation summary; validasi per field memakai DataAnnotations view model.
- Model binding MVC memakai culture request: dengan id-ID, nilai `1234.5` dari `input type=number` salah dibaca → culture request Web.App en-US, format tampilan lewat `DisplayFormatter`.
- `step` input angka harus mengikuti skala kolom DB: Dapper mengembalikan `60.0000` (numeric 7,4) dan validasi browser `step="0.01"` menolaknya saat edit.
- Tag helper `asp-menu` hanya bekerja pada elemen yang terdaftar di `HtmlTargetElement` (a, button, form, li, ul, div) — elemen lain diam-diam tetap dirender.
- Checkbox yang tidak dicentang tidak terkirim: properti `bool?` view model untuk switch (mis. `IsActive`) **jangan diberi default `true`**, dan field `[Required]` yang read-only di form Edit harus dikirim sebagai hidden (error properti tidak tampil di summary `ModelOnly`, simpan gagal tanpa pesan).
- Tom-Select dengan `preload: 'focus'` membuka lagi dropdown setelah memilih → `closeAfterSelect` + `blur()`; template tidak punya CSS Tom-Select untuk wrapper `form-select` & placeholder single-select (diatur di `theme.css`).
- Aturan `step` bawaan jquery-validation menghitung jumlah desimal, bukan kelipatan → nilai DB `20.000000` gagal `step="0.001"` tanpa pesan; diganti di `wwwroot/js/validation-setup.js`.
- Folder lampiran relatif terhadap `AppContext.BaseDirectory` (folder `bin`) → di dev Web.App menunjuk `../../../../Web.Api/bin/Debug/net10.0/uploads`; di container keduanya `/app/uploads`.
- Route di `MenuCatalog` harus sama persis dengan nama controller area (mis. `/Sales/SalesOrders`, bukan `/Sales/Orders`) — sidebar tidak divalidasi saat build; test `Admin_Should_OpenEveryReleasedMenu` menjaganya.
- Skrip E2E harus memakai **tanggal lokal** (`toLocaleDateString('sv-SE')`), bukan `toISOString()` (UTC): antara 00.00–07.00 WIB tanggal UTC masih kemarin sehingga dokumen "hari ini" tampak bertanggal salah.
- Outbox menyimpan `error` pada setiap percobaan yang gagal; baru menjadi dead letter setelah `MaxAttempts` (processed_on_utc diisi). Query "gagal" wajib memakai keduanya.
- **CSP (W10)**: jangan menulis `onclick=`/`onchange=`/`href="javascript:…"` di view — pakai `data-autosubmit`, `data-copy-label`, `data-fill-target` atau script per halaman; blok `<script>` inline otomatis diberi nonce oleh tag helper. Sumber eksternal (CDN, Google Fonts) diblokir.
- Nonce CSP memakai hex: base64 berisi `+` yang di-encode Razor menjadi `&#x2B;` di atribut.
- `style.min.css` dihasilkan dari `style.css` (lightningcss) — ubah tema di `css/theme.css`; bila `style.css` diubah, regenerasi file min.
- Perubahan view/aset Web.App butuh **build ulang** (tanpa runtime compilation; manifest static assets dibuat saat build).
- Parameter Dapper bernilai null tanpa tipe (`@X IS NULL`) → `42P08 could not determine data type`: pakai `CAST(@X AS timestamptz/uuid/text)`.
- Integration test Web.App: wrong-password memakai user baru (bukan admin) karena lockout; factory men-set `Security:SecureCookies=false` dan batas rate limit tinggi.
- Migration bundle (`efbundle`) butuh env `ConnectionStrings__Database` agar host Web.Api bisa dibangun.

- **MobileApp**: `Directory.Build.props` men-set `TargetFramework` → proyek MAUI multi-target wajib `<TargetFramework></TargetFramework>`, jika tidak restore tidak memuat runtime Android (`NETSDK1047`).
- **MobileApp**: WebView Android melaporkan `env(safe-area-inset-*)` = 0 → pakai `SafeAreaEdges="All"` di `ContentPage`; CSS template Findee menata semua `<button>` (biru, lebar penuh) dan `.input-icon .icon` di kiri — tombol ikon perlu kelas sendiri.
- **MobileApp**: crash saat start `No view found for id … jumpToStart` = ID resource basi dari build inkremental → hapus `src/MobileApp/obj` & `bin`, build ulang.
- **MobileApp**: di Android `AndroidMessageHandler` melempar `Java.IO.IOException` (bukan `HttpRequestException`) saat offline → dibungkus `NetworkErrorHandler`.
- **MobileApp**: build Android (javac) bisa kehabisan memori saat emulator + API + Podman berjalan bersamaan → `-p:JavaMaximumHeapSize=512m`, matikan Podman bila tidak dipakai.

## 7. Cara Kerja & Verifikasi

- Setiap fase: domain + unit test invariant → command/query + validator → EF config + migration → endpoint + permission → **verifikasi end-to-end** ke PostgreSQL lokal pada database sementara `intiplasma_verify` (dibuat & dihapus otomatis; database `intiplasma` milik user tidak disentuh).
- User yang melakukan **commit & migrate** setelah tiap fase.
- Status test saat ini: **507 test lulus** (147 domain, 81 application, 14 arsitektur, 68 MobileApp.UnitTests, 30 integration Web.Api, 167 integration Web.App).
- Verifikasi mobile (M0): emulator Android **API 34** (`intiplasma_api34`; image API 26 terlalu tua untuk WebView) + Web.Api `:5000` pada `intiplasma_verify`, diarahkan dengan `adb` (tap/teks/screencap). Integration test butuh mesin Podman berjalan (`podman machine start`).
- Integration test (Testcontainers) **sudah bisa dijalankan** di mesin dev (container runtime tersedia) — `dotnet test IntiPlasma.slnx`.
- Verifikasi end-to-end (Fase 5: 43 skenario, Fase 6: 70 skenario, Fase 7: 32 skenario, Fase 8: 35 skenario, Fase 9: 79 skenario) memakai script Node (fetch + psql) terhadap API di port 5099 dengan `ConnectionStrings__Database` diarahkan ke `intiplasma_verify`. Skenario maker-checker memakai user kedua (role `Checker`) yang dibuat lewat API.
- ⚠️ Sejak W0 Web.Api tidak memproses outbox: verifikasi yang hanya menjalankan API perlu `BackgroundJobs__Enabled=true`.
- Verifikasi WebApp (W0: 49 skenario, W1: 36 skenario, W2: 40 skenario, W3: 48 skenario, W4: 51 skenario, W5: 42 skenario, W6: 42 skenario, W7: 62 skenario, W8: 44 skenario, W9: 44 skenario, W10: 53 + 7 restart/rate-limit + 20 container + regresi W0–W9) memakai **Playwright** (dipasang di scratchpad, bukan di repo) terhadap Web.App :5098 + Web.Api :5099 pada `intiplasma_verify`; interaksi halaman lewat frame `content-frame`.
- Migration yang ada: `Initial`, `Phase1_MasterData_Partnership`, `Phase2_FinanceCore`, `Phase3_ProcurementInventory`, `Phase4_Production`, `Phase5_SalesReceivables`, `Phase6_PayablesCashBank`, `Phase7_CostingSettlement`, `Phase8_ReportingClosing`, `Phase9_Attachments`, `PhaseW0_UserStatus`, `PhaseW1_AccessControl`, `PhaseW10_Hardening`.

## 8. Catatan Terbuka / Hutang Teknis

- Tarif & fasilitas pajak (PPN dibebaskan untuk DOC/pakan/ayam hidup, PPh) perlu dikonfirmasi konsultan pajak lalu diinput sebagai TaxCode.
- Kegagalan jurnal otomatis terlihat di layar **Failed Events** (W9, + retry) dan API `system/failed-events`; dead letter = `processed_on_utc` terisi **dan** `error` terisi (event yang masih di-retry otomatis dihitung *pending*).
- `IdempotencyFilter` masih in-memory → tambah Redis sebelum scale-out.
- XML Coretax (e-Faktur & e-Bupot) belum ada; baru rekap + CSV.
- Satu admin tunggal tidak bisa menyelesaikan jurnal manual (maker-checker) → perlu user kedua.
- Presisi rasio DPP: `TaxBaseRatio` (10,8) menyimpan 11/12 sebagai 0,91666667 sehingga PPN bisa meleset beberapa sen. **Diputuskan**: PPN 12% DPP nilai lain diinput sebagai tarif 11% dengan rasio 1. Rasio ≠ 1 tetap didukung, tetapi hindari pecahan berulang.
- Vendor invoice hanya untuk barang ber-PO; hutang non-PO (jasa) & nota debit vendor belum ada (sementara lewat kas keluar).
- Transfer antar cabang belum didukung (perlu akun antar-cabang).
- PPh vendor selalu ke akun mapping `IncomeTaxWithheld` (default Hutang PPh 23); PPh 22/4(2) perlu override.
- Exposure credit limit memuat semua penerimaan customer ke memori (uang muka belum diterapkan) — perlu query SQL bila datanya besar.
- Biaya siklus baru sapronak; biaya lain/overhead (listrik, tenaga kerja, penyusutan kandang inti) belum dialokasikan ke siklus.
- Belum ada sub-ledger piutang plasma per peternak: potongan hutang di settlement hanya dibatasi pendapatan − PPh. Sejak W8 form settlement menampilkan saldo hutang plasma **dari data settlement** (rugi − potongan, `GetFarmerPlasmaDebtQuery`) sebagai informasi; hutang/pelunasan dari sumber lain belum tercatat.
- Settlement plasma yang sudah Approved belum bisa dibatalkan/direverse (koreksi lewat jurnal manual).
- Lampiran: storage lokal hanya untuk satu instance (perlu S3/MinIO sebelum scale-out); batas 10 MB & tipe file masih konstanta; belum ada thumbnail/kompresi; `PUT …/documents` pada PV *paid* & PV plasma belum diuji end-to-end.
- Bila Web.App mati, event outbox (jurnal otomatis, gudang kandang) tertunda sampai Web.App hidup lagi. (Key ring Data Protection sudah di PostgreSQL sejak W10.)
- Audit log tanpa retensi/purge otomatis. Tombol primer `#0984E3` belum memenuhi kontras AA (keputusan W10).
- Siklus lama yang ditutup sebelum Fase 7 tidak punya `ClosingCost`; invoice lama punya `costAmount` 0 (tidak ada penyesuaian HPP untuk siklus tersebut).
- **Temuan audit 2026-10-04** (belum diperbaiki):
  - **Tutup siklus** juga tidak mengecek periode fiskal; jurnal `CycleCostAdjustment` bisa jatuh ke dead letter. Ini menambah butir BPB/transfer/retur di bawah.
  - Tutup tahun mengabaikan **override mapping cabang** untuk `YearEndClosing.NetIncome` (`YearEndClosing.cs` hanya memfilter `BranchId == null`).
  - Endpoint sinkronisasi `?modifiedSince=` untuk mobile (PLAN §decision #4) belum dibuat.
  - Belum ada endpoint API untuk: kredit customer, siklus siap-settle, jurnal per dokumen, dashboard, ganti password sendiri, audit log, detail/cancel nota kredit, dan detail transfer bank.
  - Hak **Export** bisa diberikan untuk menu Users, Menu Access, Branch Access, API Roles, Branches & Feed Mutations, tetapi menu-menu itu tidak punya fitur ekspor.
  - Transfer bank & nota kredit tidak punya halaman detail, sehingga jurnal otomatisnya tidak bertautan ke dokumen sumber (`JournalSources.Route`).
  - Kode mati `GetUserByEmailQuery`.
  - Value object `Quantity` (PLAN §2.4) tidak pernah dibuat.
- BPB, transfer & retur bisa diposting walau tahun fiskal belum dibuka; jurnal otomatisnya menjadi dead letter (`FiscalPeriods.NotFoundForDate`) dan memblokir tutup periode → buka tahun fiskal dulu. Pertimbangkan validasi periode saat posting dokumen gudang (seperti VI/PV).

## 9. Langkah Berikutnya — Backlog setelah Fase 8

Seluruh fase rencana awal (0–8) selesai di sisi API. Kandidat berikutnya (urutan bisa disepakati):
- **Klien**: MVC WebApp Admin Office (keputusan #4) — rencana di [PLAN-WEBAPP.md](PLAN-WEBAPP.md) (Fase W0–W10; aplikasi langsung via Infrastructure, mainboard + iframe, Akses Menu & Akses Cabang per user, background job di Web.App, ekspor ClosedXML/QuestPDF, UI English) — dan mobile app PPL (recording offline sudah siap di API).
- **Mobile app PPL & Manager** — rencana di [PLAN-MOBILE.md](PLAN-MOBILE.md) (Fase M0–M10; MAUI Blazor Hybrid via Web.Api + JWT, Android dulu, UI Bahasa Indonesia dari template `docs/html-template-mobile`, recording & stok ayam harian per rentang bobot (offline, informasi Sales; WebApp Production → Live Bird Stock), request pengiriman pakan tanpa approval, breed standard, **modul approval berjenjang** untuk farmer/kandang baru, perubahan NIK/rekening, aktivasi kontrak, revisi recording & mutasi pakan). Semua keputusan M-1 s.d. M-39 disepakati 2026-10-08; berikutnya Fase M0.
- **Pajak**: ekspor XML Coretax (e-Faktur & e-Bupot) setelah format dikonfirmasi konsultan pajak; input tarif final PPN/PPh.
- **Akuntansi lanjutan**: hutang non-PO (jasa) & nota debit vendor; transaksi antar cabang (akun antar-cabang); alokasi overhead ke siklus; sub-ledger piutang plasma per peternak; aset tetap & penyusutan.
- **Operasional/infra**: Redis untuk idempotency & cache (multi-instance), integration test per modul (Testcontainers sudah jalan), observabilitas (Seq/OTel).
