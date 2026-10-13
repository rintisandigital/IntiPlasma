# Plan — Backlog Teknis (Hutang Teknis RANGKUMAN §8)

> Lanjutan dari [RANGKUMAN.md](RANGKUMAN.md) §8 "Catatan Terbuka / Hutang Teknis" setelah temuan audit 2026-10-04 selesai ([PLAN.md](PLAN.md) §17).
> Disusun 2026-10-13 dari pemetaan kode. **Status: menunggu persetujuan.** Belum ada kode yang diubah untuk backlog ini.
> Kode keputusan memakai format **B-n**. Ukuran: **S** ≤ ½ hari, **M** 1–2 hari, **L** ≥ 3 hari (termasuk test & verifikasi end-to-end).

---

## 1. Ringkasan

| # | Butir | Ukuran | Butuh keputusan bisnis? | Usulan gelombang |
|---|---|---|---|---|
| 1 | Idempotency & cache in-memory → Redis | S–M | Infra (Redis di produksi?) | B1 |
| 2 | Ekspor XML Coretax (e-Faktur & e-Bupot) | L | **Ya** (konsultan pajak) | B4 |
| 3 | Admin tunggal tidak bisa menyelesaikan jurnal manual (maker-checker) | S–M | **Ya** (kontrol internal) | B2 |
| 4 | Presisi rasio DPP `TaxBaseRatio` | S–M | Terkait Coretax | B4 |
| 5 | Hutang non-PO (jasa) & nota debit vendor | L | **Ya** | B3 |
| 6 | Transaksi antar cabang | L | **Ya** (akuntansi) | B3 |
| 7 | Akun PPh vendor per pasal (21/22/23/4(2)) | S–M | Ya (akun per pasal) | B2 |
| 8 | Exposure credit limit dimuat ke memori | S–M | Tidak | B1 |
| 9 | Alokasi overhead ke biaya siklus | L | **Ya** | B3 |
| 10 | Sub-ledger piutang plasma per peternak | M–L | **Ya** | B3 |
| 11 | Pembatalan/reversal settlement plasma Approved | M | Ya | B2 |
| 12 | Lampiran: storage S3/MinIO, batas & tipe ke konfigurasi | M | Infra (provider) | B1 |
| 13 | Outbox hanya diproses Web.App | S (a) / M (b) | Infra | B1 |
| 14 | Retensi audit log & kontras tombol primer | S | Ya (masa retensi, warna) | B2 |

Gelombang yang diusulkan:
- **B1 — Infra & performa.** Tanpa keputusan akuntansi; bisa langsung jalan setelah B-1…B-4 disepakati.
- **B2 — Kontrol & koreksi.** Keputusan kecil.
- **B3 — Akuntansi lanjutan.** Butuh keputusan bisnis dan desain COA.
- **B4 — Pajak / Coretax.** Butuh format XSD resmi dan konfirmasi konsultan pajak.

---

## 2. Temuan per butir

### 2.1 Idempotency & cache → Redis (#1)

**Kondisi sekarang**
- `Web.Api/Infrastructure/IdempotencyFilter.cs` memakai `HybridCache` L1 saja. `Infrastructure/DependencyInjection.cs` memanggil `AddHybridCache()` tanpa `IDistributedCache`.
- Tidak ada kunci antara lookup dan simpan, sehingga dua request duplikat yang benar-benar bersamaan bisa sama-sama dieksekusi.
- Pemakai `HybridCache` lain: `PermissionProvider`, `MenuAccessProvider`, `FieldScopeProvider`, `BranchAccess`, `AppCookieEvents`, dan `FormTokenFilter` (Web.App). Semuanya otomatis mendapat L2 begitu distributed cache dipasang.
- Invalidasi antar-instance sudah ada lewat PostgreSQL LISTEN/NOTIFY (`PostgresCacheInvalidator`).
- `docker-compose.yml` belum punya Redis.

**Usulan:** `AddStackExchangeRedisCache` bersyarat (`ConnectionStrings:Redis` terisi), plus service Redis di compose, health check dan baris di DEPLOY.md. Tambahkan penanda *in-progress* (SET NX, TTL pendek) agar duplikat bersamaan mendapat 409.

**B-1.** Redis tersedia di hosting produksi? Alternatif tanpa infra baru: tabel `infrastructure.idempotency_keys` di PostgreSQL (unik `key`), yang juga menyelesaikan masalah duplikat bersamaan. *Rekomendasi: tabel PostgreSQL untuk idempotency (sudah transaksional, tanpa komponen baru), Redis hanya bila memang multi-instance.*

### 2.2 Coretax XML (#2) dan presisi DPP (#4)

**Kondisi sekarang**
- Rekap ada di `Application/Finance/Tax/TaxReports.cs`: PPN keluaran, retur dan masukan; PPh dipotong dari VI dan settlement.
- Ekspor yang sudah ada:
  - CSV di API: `finance/tax/vat/export` dan `withholding/export`.
  - Excel/PDF/CSV di Web.App.
- Sudah tersedia:
  - `TaxIdentity` (NPWP 15/16 digit, NITKU, PKP) untuk customer, vendor dan peternak.
  - NIK peternak.
  - `TaxInvoiceNumber` di VI.
- Belum ada:
  - Nomor faktur pajak keluaran (NSFP) di invoice penjualan.
  - Kode transaksi.
  - Jenis ID pembeli.
  - Kode barang/satuan Coretax.
  - Nomor/status bupot di VI dan settlement.
- `TaxBaseRatio` bertipe `numeric(10,8)`: 11/12 tersimpan 0,91666667. Keputusan sementara adalah tarif 11% dengan rasio 1.
- Ada perhitungan PPN duplikat di `PurchaseOrdersController.cs:346` yang seharusnya memakai `TaxCode.Calculate`.

**Usulan:**
- Builder XML per jenis (e-Faktur keluaran, retur, e-Bupot Unifikasi, Bupot 21) yang memakai query rekap ditambah detail per baris.
- Migration untuk NSFP & status faktur di invoice, kode Coretax di item/tax code, dan nomor bupot.
- Tombol unduh XML di Web.App dan endpoint API.
- Untuk presisi, simpan rasio DPP sebagai pembilang/penyebut (11/12), karena Coretax menampilkan DPP nilai lain = 11/12 × harga.

**B-2.** Ditunda sampai XSD/versi Coretax dan daftar dokumen ber-e-Faktur dikonfirmasi konsultan. DOC/pakan/ayam hidup kemungkinan dibebaskan.
- Konfirmasi juga: plasma memakai PPh 21 (Bupot 21) atau PPh 23 (Unifikasi)?
- Konfirmasi juga: siapa yang meminta NSFP?

*Rekomendasi: jangan dikerjakan sebelum ada contoh XML valid dari Coretax.* Perbaikan kecil `PurchaseOrdersController` (memakai `TaxCode.Calculate`) bisa langsung masuk B1.

### 2.3 Maker-checker untuk admin tunggal (#3)

**Kondisi sekarang:** larangan menyetujui dokumen sendiri ditanam di domain, di 5 tempat:
- `JournalEntry.Approve`
- `CashTransaction`
- `PaymentVoucher`
- `PlasmaSettlement`
- `ApprovalRequest` (`RequesterCannotDecide`)

Tidak ada konfigurasi.

**Usulan:** permission baru `approvals:self-approve` (default tidak diberikan ke siapa pun, termasuk Administrator).
- Bila dimiliki, handler mengoper `allowSelf: true` ke domain.
- Self-approval dicatat di audit log, dan dokumen ditandai "self-approved" di detail/PDF.

**B-3.** Self-approval boleh sama sekali? Bila boleh:
- Untuk jurnal manual saja, atau juga PV, kas dan settlement?
- Perlu batas nominal?

*Rekomendasi: jurnal manual dan kas keluar saja, lewat permission eksplisit; PV dan settlement tetap wajib dua orang.*

### 2.4 Hutang non-PO & nota debit vendor (#5)

**Kondisi sekarang**
- Baris `VendorInvoice` **wajib** terkait BPB dan PO (three-way match).
- Komponen jurnal VI: GoodsValue, PriceVariance, InputVat, IncomeTaxWithheld. Tidak ada komponen beban.
- Pembayaran lewat `IPayable` (VI dan settlement), dengan alokasi PV per jenis.
- Solusi sementara: kas keluar langsung.

**Usulan:** aggregate baru **`VendorBill`** (tagihan non-PO) yang mengimplementasikan `IPayable`.
- Baris: akun beban, cost center, deskripsi, jumlah, PPN dan PPh.
- Dibayar lewat PV seperti VI.
- Aggregate baru **`VendorDebitNote`** mengurangi outstanding VI atau tagihan: koreksi harga, atau retur barang yang mengeluarkan stok dari gudang pusat.

**B-4.**
- Tagihan non-PO perlu approval (maker-checker), atau langsung post?
- Akun beban mana yang boleh dipakai: semua akun Expense, atau daftar tertentu?
- Nota debit hanya koreksi harga, atau juga retur barang (mengurangi stok)?

### 2.5 Transaksi antar cabang (#6)

**Kondisi sekarang**
- Diblokir di domain:
  - `StockTransfer` (`CrossBranch`; sumber juga harus gudang pusat).
  - `StockReturn`.
  - `BankTransfer` (`DifferentBranches`).
- Setiap `JournalEntry` hanya punya satu `BranchId`.

**Usulan:**
- Event baru `InterBranchStockTransfer` dan `InterBranchBankTransfer` yang membuat **dua jurnal**, satu per cabang, lewat akun RK (rekening koran) antar cabang: Dr RK cabang tujuan / Cr persediaan, dan sebaliknya.
- Guard domain dicabut untuk kombinasi yang didukung.
- Laporan konsolidasi memeriksa saldo RK = 0.

**B-5.**
- COA akun RK antar cabang: satu akun, atau satu per pasangan cabang?
- Harga transfer: biaya rata-rata, atau harga transfer?
- Cabang penerima wajib konfirmasi terima (dokumen dua langkah)?

### 2.6 Akun PPh vendor per pasal (#7)

**Kondisi sekarang**
- VI dan settlement selalu memakai komponen `IncomeTaxWithheld`, dengan default Hutang PPh 23.
- Tax code sudah punya pasal 21/22/23/4(2).
- Satu VI hanya punya satu kode PPh.

**Usulan:** kolom `PayableAccountId` (akun hutang PPh) di `TaxCode` jenis PPh. Bila terisi, AutoJournal memakai akun ini sebagai kredit menggantikan mapping. Ini mekanisme yang sama dengan `AccountingAmount.CreditAccountId` yang sudah ada.

**B-6.** Akun hutang PPh 21/22/4(2) (kode COA)? Satu VI cukup satu kode PPh? *Rekomendasi: satu kode per VI dulu.*

### 2.7 Exposure credit limit di memori (#8)

**Kondisi sekarang**
- `SalesSupport.CreditExposureAsync` memuat ke memori semua invoice terbuka, DO terkirim beserta baris, SO terbuka beserta baris, dan **semua** penerimaan Posted customer, lalu menjumlahkannya di memori.
- Pemanggil: `GetCustomerCredit` (kini juga di API) dan approve SO.
- Catatan §8 "uang muka belum diterapkan" sudah usang: uang muka sudah bisa diterapkan.

**Usulan:** satu query SQL (Dapper) dengan SUM per komponen: invoice outstanding, draft invoice, DO belum ditagih, sisa SO, dikurangi uang muka belum diterapkan. Rumus `DeliveryOrder.Amount` dan `SalesOrder.OutstandingEstimatedAmount` direplikasi di SQL, dijaga oleh test yang membandingkan hasilnya dengan versi domain. Tambahkan index `customer_id, status`. Tidak ada keputusan bisnis.

### 2.8 Alokasi overhead siklus (#9)

**Kondisi sekarang**
- `CycleCostSummary` hanya berisi DOC, pakan dan OVK.
- `ClosingCost` dibekukan saat tutup siklus, dan penyesuaian HPP dijurnal saat tutup.
- `JournalLine` sudah punya `CostCenterId`, tetapi belum ada penanda siklus.

**Usulan:**
- Tambah `OverheadCost` ke ringkasan biaya.
- Sumber biaya: jurnal (kas keluar atau tagihan non-PO dari #5) yang diberi cost center "Overhead Produksi" per cabang.
- Saat tutup periode atau tutup siklus, biaya dialokasikan ke siklus inti yang aktif dengan kunci **ekor-hari**.
- Hasil alokasi masuk penyesuaian HPP.

**B-7.**
- Biaya apa saja yang dialokasikan: listrik, tenaga kerja, penyusutan kandang inti?
- Kunci alokasi apa?
- Hanya siklus **inti**? Plasma dibayar per kontrak, sehingga overhead inti tidak masuk HPP plasma.
- Bergantung pada #5 (tagihan non-PO) dan aset tetap/penyusutan, yang belum ada.

### 2.9 Sub-ledger piutang plasma (#10)

**Kondisi sekarang**
- `GetFarmerPlasmaDebtQuery` hanya menghitung rugi − potongan dari settlement, sebagai informasi saja.
- Potongan hutang dibatasi pendapatan − PPh, bukan saldo hutang.

**Usulan:**
- Tabel `costing.farmer_receivable_entries`, yang diisi dari:
  - defisit settlement (+),
  - potongan settlement (−),
  - dokumen baru **pinjaman/kasbon peternak** (+, lewat kas keluar),
  - pelunasan lewat kas masuk (−).
- Potongan settlement divalidasi ≤ saldo.
- Laporan kartu hutang peternak direkonsiliasi ke akun GL piutang plasma.

**B-8.**
- Jenis hutang peternak selain defisit: kasbon tunai, sapronak di luar kontrak, denda?
- Ada bunga?
- Potongan settlement wajib ≤ saldo?

### 2.10 Reversal settlement plasma (#11)

**Kondisi sekarang**
- Status settlement: Draft → Approved → PartiallyPaid/Paid. `Cancel` hanya untuk Draft.
- Approve menandai siklus `Settled` dan membuat jurnal.
- PV Paid tidak bisa di-void.

**Usulan:**
- Transisi baru `Void(date, reason)` hanya bila **belum ada pembayaran** (PaidAmount = 0). Efeknya:
  - jurnal dibalik (`ReverseAsync`),
  - siklus kembali `Closed`,
  - hutang plasma ikut terkoreksi otomatis (query mengecualikan status Voided),
  - periode fiskal dicek.
- Tambahan UI dan API.
- Settlement yang sudah dibayar dikoreksi dengan jurnal manual atau settlement koreksi (di luar lingkup).

**B-9.**
- Void hanya untuk yang belum dibayar?
- Tanggal reversal: tanggal void atau tanggal asli? *Rekomendasi: belum dibayar saja, tanggal void (konsisten dengan void nota kredit/penerimaan).*
- Perlu approval berjenjang?

### 2.11 Lampiran: storage & konfigurasi (#12)

**Kondisi sekarang**
- `IFileStorage` sudah abstrak. Implementasinya `LocalFileStorage`, dengan volume `uploads` yang dipakai bersama di compose.
- Batas 10 MB dan whitelist (jpg/png/webp/pdf dengan sniffing magic byte) masih konstanta di `AttachmentFileTypes`.
- Health check mengasumsikan path lokal.

**Usulan:**
- `S3FileStorage` (AWSSDK.S3, kompatibel MinIO), dipilih lewat `FileStorage:Provider`.
- Health check per provider.
- Service MinIO di compose.
- `MaxSizeBytes` dan daftar tipe yang diaktifkan dipindah ke `FileStorageOptions`. Tipe baru tetap perlu aturan signature di kode.
- Thumbnail dan kompresi di luar lingkup.

**B-10.** Provider: MinIO self-hosted atau S3 cloud? Batas ukuran? Perlu tipe xlsx/docx?

### 2.12 Outbox hanya di Web.App (#13)

**Kondisi sekarang**
- `OutboxProcessor` dan `AttachmentCleanupJob` hanya aktif bila `BackgroundJobs:Enabled`. Di compose, Web.Api `false` dan Web.App `true`.
- Processor sudah memakai `FOR UPDATE SKIP LOCKED`, sehingga aman dijalankan di beberapa host.

**Usulan:**
- **(a)** aktifkan juga di Web.Api, cukup lewat konfigurasi, setelah memastikan `AttachmentCleanupJob` aman konkuren (penghapusan bersyarat status), atau
- **(b)** host **Worker** terpisah (`src/Worker`).

**B-11.** *Rekomendasi: (a) sekarang. Pertimbangkan (b) bila beban job besar.*

### 2.13 Retensi audit log & kontras tombol (#14)

**Kondisi sekarang**
- `infrastructure.audit_logs` tanpa purge.
- Tombol primer `#0984E3` di `theme.css` belum AA. `--ip-primary-text: #0770C2` sudah AA untuk teks.

**Usulan:**
- `AuditLogRetentionJob`: hapus bertahap berdasarkan `occurred_at_utc` dengan opsi `AuditLog:RetentionDays`, plus index.
- Override `.btn-primary` ke `#0770C2`.

**B-12.**
- Masa retensi? Catatan: dokumen keuangan lazim 10 tahun, sedangkan log login/ekspor bisa lebih pendek. Usulan: retensi per kategori.
- Perlu arsip sebelum hapus?
- Pemilik brand setuju tombol lebih gelap?

---

## 3. Daftar Task (setelah keputusan)

### Gelombang B1 — Infra & performa
1. Exposure credit limit via SQL + test kesetaraan dengan versi domain (#8).
2. Idempotency: tabel PostgreSQL (atau Redis sesuai B-1) + penanda *in-progress* + test duplikat bersamaan (#1).
3. Outbox di Web.Api (B-11 a): konfigurasi compose & appsettings, cek konkurensi cleanup job, DEPLOY.md (#13).
4. Lampiran: batas & tipe ke `FileStorageOptions`, lalu `S3FileStorage` + MinIO compose + health check (#12, sesuai B-10).
5. `PurchaseOrdersController` memakai `TaxCode.Calculate` (bagian kecil #4).

### Gelombang B2 — Kontrol & koreksi
6. Akun hutang PPh per tax code (#7).
7. Void settlement plasma yang belum dibayar (#11).
8. Self-approval berizin (#3, sesuai B-3).
9. Retensi audit log + warna tombol primer (#14).

### Gelombang B3 — Akuntansi lanjutan (masing-masing bisa menjadi fase sendiri)
10. Tagihan non-PO + nota debit vendor (#5).
11. Sub-ledger piutang plasma + kasbon peternak (#10).
12. Transaksi antar cabang (#6).
13. Alokasi overhead (#9; setelah #5 dan aset tetap).

### Gelombang B4 — Pajak
14. Coretax XML + NSFP + rasio DPP pecahan (#2, #4).

Setiap task mengikuti alur fase yang biasa:
1. Domain + unit test.
2. Command/query.
3. EF + migration.
4. Endpoint + Web.App.
5. Verifikasi end-to-end di `intiplasma_verify`.
6. Realisasi dicatat di dokumen ini.

## 4. Di luar lingkup
- Aset tetap & penyusutan (prasyarat #9). Diusulkan sebagai fase tersendiri.
- Thumbnail/kompresi lampiran.
- Konsolidasi multi-entitas (beda NPWP).
- Void PV yang sudah dibayar dan refund ke peternak.
