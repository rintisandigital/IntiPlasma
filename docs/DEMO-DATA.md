# Data Dummy (Demo Data Seeder)

Seeder data dummy untuk demo, pelatihan, dan uji manual. Kodenya ada di `src/Infrastructure/Database/DemoData/`.

Setiap data dibuat lewat **command handler asli**, jadi ikut melewati validasi, aturan domain, akses cabang, penomoran dokumen, maker-checker, dan jurnal otomatis lewat outbox. Hasilnya, data saling konsisten di semua modul. Contohnya, nilai stok gudang sama dengan saldo GL persediaan, GRNI bernilai 0 setelah semua BPB ditagih, dan jurnal selalu seimbang.

Operasi disimulasikan **hari demi hari selama ±95 hari terakhir sampai kemarin**. Semua tanggal dihitung relatif terhadap hari ini, sehingga setiap kali seeder dijalankan, semua tahap siklus selalu tersedia.

## Cara menjalankan

Syarat:
- Database **belum punya cabang**. Kalau sudah ada cabang, seeder dilewati dan ada pesan di log.
- Seed dasar sudah berjalan: COA, satuan, dan user `Seed:Admin`. Di Development, seed dasar otomatis berjalan saat Web.Api start.
- Jangan jalankan di environment **Production** (sengaja ditolak).
- **Web.App sebaiknya mati** selama seeding, karena seeder memproses outbox sendiri secara langsung.

Disarankan memakai database terpisah, misalnya `intiplasma_demo`:

```powershell
# 1. Buat database kosong
& "C:\Program Files\PostgreSQL\15\bin\psql.exe" -U postgres -c "CREATE DATABASE intiplasma_demo;"

# 2. Jalankan Web.Api (Development: migrate + seed dasar + data dummy)
$env:ConnectionStrings__Database = "User ID=postgres;Password=admin;Server=localhost;Port=5432;Database=intiplasma_demo;"
dotnet run --project src/Web.Api -- --Seed:DemoData=true
```

Seeding memakan waktu ±15 detik (±720 command). Di log akan muncul `Demo data: done (… commands)`, lalu API berjalan seperti biasa. Setelah itu Web.App bisa dijalankan dengan connection string yang sama.

Untuk **mengulang dari awal**, hapus database lalu buat lagi (`DROP DATABASE intiplasma_demo WITH (FORCE);`), kemudian ulangi langkah di atas.

## User

| Email | Password | Peran |
|---|---|---|
| `admin@intiplasma.local` | `Admin123!` (dari `Seed:Admin`) | Maker semua dokumen; akses penuh |
| `checker@intiplasma.local` | `Demo123!` | Checker (approve jurnal, kas keluar, PV, settlement, PO, SO); akses penuh + role API Administrator |
| `staff.bdg@intiplasma.local` | `Demo123!` | Akses menu penuh, **hanya Cabang Bandung** (contoh Akses Cabang) |

## Isi data

**Organisasi & keuangan**
- Cabang **BDG** (Bandung) dan **CJR** (Cianjur).
- Tahun fiskal berjalan dibuka. Bulan-bulan kosong sebelum transaksi pertama sudah **ditutup** berurutan.
- Akun COA tambahan untuk kas/bank Cianjur, cost center PRD/ADM/MKT, dan 2 template jurnal.
- Kas/Bank per cabang: Kas Besar, Kas Kecil, dan Bank (BCA Bandung, BRI Cianjur).
- Saldo awal dicatat lewat jurnal manual setoran modal (bank, kas, bangunan, dan peralatan kandang inti), dengan alur maker-checker.
- Transaksi rutin: pengisian kas kecil tiap tanggal 1, listrik tiap tanggal 10, gaji tiap tanggal 25, BBM/transport PPL tiap Senin, penjualan karung bekas (BKM) tiap tanggal 15, dan penyusutan di akhir bulan.
- **Rekonsiliasi bank** BCA Bandung per akhir bulan lalu berstatus *Completed*.

**Master**
- Tax code: PPN-11 (12% DPP nilai lain → efektif 11%), PPN-BBS (dibebaskan), dan PPH23-2.
- Item: DOC CP 707, pakan BR-1/BR-2 (1 SAK = 50 KG), vaksin ND-IB & Gumboro, vitamin, ayam hidup, dan sekam.
- Gudang induk per cabang. Gudang kandang (`GK-…`) dibuat otomatis lewat outbox.
- Vendor: CPI, Japfa, Medion (toleransi harga 2%), dan UD Sekam.
- Customer: RPA, bakul, CV Cianjur, dan satu warung dengan limit 0.

**Kemitraan**
- 7 peternak (2 inti, 5 plasma), 8 kandang.
- 3 kontrak aktif:
  - harga kontrak Bandung,
  - **bagi hasil 40%** Bandung,
  - harga kontrak Cianjur.
- Semua kontrak memuat harga sapronak, harga jaminan per rentang BW, bonus FCR & IP, dan potongan deplesi.

**Siklus produksi** (alur PO → BPB → chick-in → recording harian → panen per truk → SO/DO/invoice → penerimaan → tutup siklus → settlement)

| Kandang | Skema | Populasi | Status akhir | Yang diperlihatkan |
|---|---|---|---|---|
| KDG-BDG-01 | Harga kontrak | 5.000 | **Settled**, PV plasma **Paid** | FCR ±1,46 sehingga mendapat **bonus FCR**; satu recording **direvisi**; **nota kredit** (klaim susut); piutang lunas |
| KDG-BDG-03 | Bagi hasil | 4.500 | **Closed**, settlement **Draft** | Menunggu approve checker; piutang **sebagian**; sisa pakan **dimutasi** ke KDG-BDG-02 |
| KDG-BDG-02 | Harga kontrak | 6.000 | **Harvesting** | 2 dari 4 truk sudah panen; invoice terakhir belum dibuat; satu invoice belum dibayar |
| KDG-BDG-INTI | Inti | 8.000 | **Active** (±20 hari) | Recording berjalan; PV vendor dibayar |
| KDG-BDG-04 | Harga kontrak | 4.000 | **Planned** | Chick-in 3 hari lagi; PO DOC/pakan/OVK sudah diapprove tetapi belum diterima |
| KDG-CJR-01 | Harga kontrak | 5.000 | **Settled**, PV plasma **Approved** (belum dibayar) | Performa buruk (FCR ±1,77, deplesi >6%) sehingga terkena **potongan deplesi** |
| KDG-CJR-INTI | Inti | 7.000 | **Closed** | **Uang muka** customer diterapkan ke invoice; sisa piutang dibayar sebagian |
| KDG-CJR-02 | Harga kontrak | 3.500 | **Active** (±12 hari) | Invoice vendor belum dibayar (aging hutang) |

**Pekerjaan terbuka** (untuk mengisi daftar "Waiting for action" di dashboard):
- jurnal manual Draft,
- kas keluar Cianjur menunggu approve,
- PO pakan Draft,
- settlement KDG-BDG-03 menunggu approve,
- PV plasma KDG-CJR-01 menunggu dibayar.

## Catatan

- Seeder tidak mengunggah lampiran (file).
- Nomor dokumen mengikuti format normal (`PO/BDG/2026/VII/0001`, dan seterusnya), sesuai bulan tanggal dokumen.
- Bila seeding gagal di tengah jalan, pesan error menyebut command dan kode error domainnya. Data yang sudah tersimpan tidak dihapus, jadi hapus dan buat ulang database sebelum mencoba lagi.
