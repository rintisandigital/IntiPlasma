# 9. Journals, Reports & Closing

## Setup (Finance)
| Menu | Isi |
|---|---|
| **Chart of Accounts** | Akun bertingkat (header vs detail; hanya akun detail yang menerima posting), akun kontra, kategori arus kas. |
| **Cost Centers** | Dimensi biaya opsional pada baris jurnal. |
| **Fiscal Periods** | Buka tahun fiskal, checklist & tutup periode bulanan, buka kembali (hanya periode tutup terakhir). |
| **Journal Templates** | Pola jurnal manual yang sering dipakai. |
| **Auto Journal Mappings** | Akun per event & komponen jurnal otomatis (default + override per cabang). |

## Journals
- **Manual** (hak *Create*): cabang, tanggal, keterangan, baris akun (lookup) + cost center + debit/kredit; total & selisih tampil langsung (**Balanced**). Bisa dimulai dari template.
- *Draft* → **Approve** (oleh user lain) → **Post** (nomor `JU/…`, periode harus terbuka) → bila perlu **Reverse** (tanggal + alasan; membuat jurnal pembalik). Draft bisa diedit/dihapus. **Print PDF** journal voucher.
- **Otomatis** (dari BPB, invoice, PV, dll.) tampil read-only dengan tautan **Open source document**.

## Reports
**General Ledger, Trial Balance, Income Statement, Balance Sheet, Cash Flow** (metode langsung), **Profitability** (per siklus/kandang/peternak/cabang), **Tax Recap** (PPN keluaran/retur/masukan, PPh dipotong + CSV). Isi parameter → **Show**; ekspor Excel (berstruktur, satu lembar per tabel) atau PDF dengan hak *Export*.

## Tutup periode & tahun
1. **Fiscal Periods → Checklist**: pemblokir (jurnal draft/approved, event jurnal otomatis tertunda/gagal) harus nol; peringatan (dokumen draft, DO belum ditagih, bank belum direkonsiliasi) sebaiknya ditindaklanjuti.
2. **Close** periode berurutan. Menutup **Desember** memposting jurnal penutup ke Laba Ditahan per cabang; membuka kembali Desember membaliknya.
3. Event gagal → **Administration → Failed Events** (lihat bagian 2).

## Dashboard
Kartu KPI (siklus berjalan, panen & penjualan bulan ini, kas/bank, piutang & hutang jatuh tempo, nilai stok, event gagal), daftar **Waiting for action** dan grafik 6 bulan, mengikuti cabang aktif.
