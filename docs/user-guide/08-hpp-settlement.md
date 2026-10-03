# 8. Costing & Settlement

## Cycle Cost
- Daftar HPP semua siklus: **final** untuk siklus tertutup, **berjalan** untuk siklus aktif (dari kartu stok gudang kandang: DOC + pakan/OVK terpakai pada harga moving average).
- Rincian per item dan HPP/kg. Ekspor daftar dan rincian.
- HPP penjualan diakui per invoice dengan **HPP estimasi**, lalu dikoreksi saat siklus ditutup.

## Plasma Settlements (maker-checker)
1. **Add** dari siklus plasma yang sudah *Closed* (atau tombol di halaman siklus).
2. Perhitungan sesuai kontrak:
   - **PriceContract**: harga jaminan per panen (sesuai BW rata-rata truk) − sapronak @harga kontrak ± bonus/potongan.
   - **ProfitSharing**: % × (penjualan bersih − biaya siklus); rugi ditanggung inti.
3. PPh kontrak dan **potongan hutang plasma** (saldo hutang ditampilkan sebagai info; tombol **Use it** mengisi nilainya).
4. **Recalculate** selama *Draft*. **Approve** oleh user lain → jurnal beban kemitraan & hutang plasma, siklus menjadi *Settled*. Hasil negatif menjadi **piutang plasma** yang bisa dipotong pada settlement berikutnya.
5. **Pay** membuat Payment Voucher plasma. **Print PDF** settlement dengan terbilang. **Cancel** (alasan) hanya untuk draft.

Settlement yang sudah disetujui tidak bisa dibatalkan; koreksi lewat jurnal manual.
