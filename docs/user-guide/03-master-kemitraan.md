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

## Partnership
| Menu | Catatan |
|---|---|
| **Farmers** | Peternak **Inti** atau **Plasma** (plasma wajib NIK), rekening untuk pembayaran. |
| **Coops** | Kandang milik peternak, kapasitas, cabang. |
| **Contracts** | Kontrak kemitraan plasma: skema **PriceContract** (harga sapronak kontrak + harga jaminan ayam per rentang bobot) atau **ProfitSharing** (% bagi hasil), bonus/potongan, PPh. |

### Alur kontrak
*Draft* (bisa diubah) → **Activate** → *Active* (dipakai siklus) → **Deactivate** → *Inactive*. Hanya kontrak *Draft* yang bisa diedit. **Print PDF** mencetak kontrak.

Siklus plasma wajib memakai kontrak aktif pada cabang yang sama; data kontrak disalin ke siklus saat direncanakan.
