# 7. AP, Kas & Bank

## Cash/Bank Accounts
Master Kas / Bank / Kas Kecil per cabang, masing-masing terhubung ke satu akun COA. Buka akun untuk melihat **buku kas/bank** (mutasi & saldo).

## Vendor Invoices
1. Pilih vendor & cabang → baris BPB yang belum ditagih muncul (3-way match PO–BPB–invoice). Isi harga tagihan, PPN dari PO, PPh dipotong (opsional).
2. **Post** (nomor `VI/…`). Bila selisih harga melebihi toleransi vendor: **Post with variance** dengan alasan wajib.

## Payment Vouchers (maker-checker)
- **Vendor**: alokasikan ke vendor invoice. **Plasma**: dari settlement plasma yang sudah disetujui.
- *Draft* → **Approve** (oleh **user lain**) → **Pay** (tanggal bayar aktual, jurnal Hutang / Kas-Bank) → *Paid*. **Print PDF** dengan terbilang.

## Cash In/Out
- **Cash In** (BKM): langsung terposting.
- **Cash Out** (BKK): *Draft* → **Approve** oleh user lain → terposting.
- Baris berisi akun lawan (bukan mapping otomatis). **Print PDF** voucher.

## Bank Transfers
Pemindahan antar kas/bank dalam satu cabang (termasuk pengisian kas kecil). Transfer antar cabang belum didukung.

## Bank Reconciliations
1. **Add**: rekening & periode, impor rekening koran (CSV).
2. Tampilan dua kolom: mutasi bank vs mutasi buku. **Auto match** (nominal sama, tanggal ±3 hari) atau cocokkan manual.
3. **Complete** bila semua cocok dan saldo seimbang. Mutasi buku hanya bisa direkonsiliasi sekali.

## Payable Ledger & Aging
Kartu hutang per vendor/peternak dan umur hutang. Ekspor Excel/PDF.
