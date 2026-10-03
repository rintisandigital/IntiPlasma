# Panduan Pengguna — Inti-Plasma Admin Office

Panduan singkat aplikasi web **Admin Office** untuk usaha ayam broiler pola kemitraan inti-plasma. Tampilan aplikasi berbahasa Inggris; nama menu dan tombol di panduan ini ditulis **tebal** persis seperti di layar.

| No | Bagian | Isi |
|---|---|---|
| 1 | [Login & navigasi](01-login-navigasi.md) | Masuk, mainboard, pencarian menu, cabang aktif, sesi, ganti password, lockout |
| 2 | [Administration](02-administration.md) | Users, Menu Access, Branch Access, Menus, API Roles, Branches, Failed Events, Audit Log |
| 3 | [Master Data & Partnership](03-master-kemitraan.md) | Satuan, kode pajak, item, gudang, vendor, customer, peternak, kandang, kontrak |
| 4 | [Procurement & Inventory](04-pengadaan-gudang.md) | PO, penerimaan barang (BPB), transfer, retur, mutasi pakan, stok |
| 5 | [Production](05-produksi.md) | Siklus, chick-in, recording harian, panen, tutup siklus |
| 6 | [Sales & AR](06-penjualan-piutang.md) | SO, DO, invoice, nota kredit, penerimaan customer, piutang |
| 7 | [AP, Kas & Bank](07-hutang-kas-bank.md) | Vendor invoice, payment voucher, kas masuk/keluar, transfer, rekonsiliasi |
| 8 | [Costing & Settlement](08-hpp-settlement.md) | HPP siklus, settlement plasma, pembayaran plasma |
| 9 | [Journals, Reports & Closing](09-jurnal-laporan-tutup-buku.md) | Jurnal manual/otomatis, laporan keuangan, pajak, tutup periode |

## Konsep umum

- Dokumen baru dibuat dengan tombol **Add …** atau **New …** di kanan atas daftar.
- **Hak akses per menu**: setiap user memakai satu profil **Menu Access** (hak *View, Create, Edit, Delete, Export* per menu) dan satu profil **Branch Access** (cabang yang boleh dilihat). Menu tanpa hak *View* tidak muncul di sidebar; tombol tanpa hak tidak ditampilkan.
- **Edit** juga mencakup aksi alur kerja (approve, post, cancel, void, pay, close). **Export** mencakup Excel/PDF daftar & laporan serta cetak PDF dokumen.
- **Maker-checker**: dokumen tertentu (jurnal manual, payment voucher, kas keluar, settlement) harus disetujui **user lain** dari pembuatnya.
- **Status dokumen** umum: *Draft* (masih bisa diubah/dihapus) → *Approved/Posted* (final, bernomor) → *Paid/Closed*. Dokumen final tidak bisa diubah; koreksi lewat pembatalan, void, nota kredit atau jurnal.
- **Nomor dokumen**: `PREFIX/CABANG/TAHUN/BULAN-ROMAWI/URUT`, mis. `PO/BDG/2026/X/0001`, diberikan saat dokumen di-approve/post.
- **Format**: angka `1.234.567,89`, tanggal `dd/MM/yyyy`, mata uang Rupiah, zona waktu WIB.
- **Lampiran**: foto/PDF (JPEG, PNG, WEBP, PDF; maks 10 MB per file, 20 file per dokumen) di kartu **Attachments**; bisa ditambah setelah dokumen final, kecuali dokumen *Cancelled/Voided*.
- **Ekspor**: ikon Excel/PDF di kanan atas daftar mengikuti filter yang sedang aktif (maks 50.000 baris). Setiap ekspor tercatat di **Audit Log**.
