# 3. Master Data & Partnership

Master tidak dihapus; nonaktifkan lewat switch **Active** agar riwayat tetap utuh.

## Master Data
| Menu | Catatan |
|---|---|
| **Units of Measure** | Satuan (KG, SAK, EKOR, …). Satuan standar sudah tersedia. |
| **Tax Codes** | PPN/PPh dengan tarif bertanggal efektif & rasio DPP. PPN 12% DPP nilai lain diinput sebagai **tarif 11% rasio 1**. Tarif final menunggu konfirmasi konsultan pajak. |
| **Items** | Sapronak (DOC, pakan, OVK) dan ayam hidup; satuan dasar + konversi (mis. 1 SAK = 50 KG). |
| **Warehouses** | Gudang induk per cabang. Gudang kandang (`GK-…`) dibuat otomatis saat kandang dibuat. |
| **Vendors** | NPWP/NITKU, rekening, **toleransi selisih harga** invoice (%) — default 0. |
| **Customers** | NPWP/NITKU, **credit limit** (0 = tanpa kredit). |
| **Weight Ranges** | Rentang bobot rata-rata untuk laporan stok ayam harian PPL; rentang aktif tidak boleh tumpang tindih. |
| **Breed Standards** | Standar performa strain per umur: BW (g, wajib), FCR, deplesi % kumulatif, pakan per hari & kumulatif (g/ekor). Baris diisi di tabel atau **Import rows (CSV)** — header `age,bw,fcr,depletion,daily_feed,cum_feed`, pemisah `,` (desimal titik) atau `;` (desimal titik/koma); impor **mengganti semua baris**. **Download rows (CSV)** untuk template. Umur di antara dua baris diinterpolasi. Satu standar aktif menjadi **default** (**Make default**) untuk siklus yang tidak memilih standar. Standar yang sudah dipakai tidak dihapus, cukup dinonaktifkan; standar default tidak bisa dinonaktifkan. |

## Partnership
| Menu | Catatan |
|---|---|
| **Farmers** | Peternak **Inti** atau **Plasma** (plasma wajib NIK), rekening untuk pembayaran. Peternak baru langsung **diajukan approval**; perubahan **NIK atau rekening** peternak yang sudah disetujui juga diajukan (nilai lama tetap dipakai sampai disetujui). |
| **Coops** | Kandang milik peternak, kapasitas, cabang. Kandang baru langsung diajukan approval (peternaknya harus sudah disetujui atau sedang diajukan); gudang kandang `GK-…` dibuat setelah kandang disetujui. |
| **Contracts** | Kontrak kemitraan plasma: skema **PriceContract** (harga sapronak kontrak + harga jaminan ayam per rentang bobot) atau **ProfitSharing** (% bagi hasil), bonus/potongan, PPh. |

### Status & approval peternak/kandang
*Pending Approval* (terkunci, tidak bisa diedit) → disetujui semua level → *Active* (bisa dipakai siklus; bisa dinonaktifkan), atau → *Rejected* (alasan tampil di kartu **Approval**; perbaiki, **Save**, lalu **Submit for approval** — pengajuan baru mulai level 1). Pengaju bisa **Cancel request** selama menunggu → *Draft*. Halaman peternak/kandang (ikon mata di daftar) menampilkan kartu **Approval** berisi riwayat level & keputusan.

### Alur kontrak
*Draft* (bisa diubah) → **Submit for approval** → *Pending Approval* → semua level setuju → *Active* (dipakai siklus) → **Deactivate** → *Inactive*. Bila ditolak → *Rejected* (bisa diedit lalu diajukan lagi). Hanya kontrak *Draft*/*Rejected* yang bisa diedit. **Print PDF** mencetak kontrak.

Siklus plasma wajib memakai kontrak aktif pada cabang yang sama; data kontrak disalin ke siklus saat direncanakan.
