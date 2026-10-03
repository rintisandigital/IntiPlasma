# 5. Production

## Cycles & Chick-in
Tahapan siklus: **Planned → DOC in coop → Chick-in → Harvesting → Closed** (→ *Settled* untuk plasma setelah settlement).

1. **Add** (rencana): pilih kandang dan (untuk plasma) kontrak aktif. Satu kandang hanya boleh punya satu siklus terbuka.
2. Terima/transfer **DOC** ke gudang kandang (Goods Receipt langsung ke kandang atau Stock Transfer).
3. **Chick-in**: tanggal & jumlah DOC ditebar → populasi awal.
4. Halaman siklus memuat KPI dan tab **Recordings, Harvests, Coop Stock, Performance** (grafik deplesi, FCR, ADG, IP), **Cost**, **Attachments**.
5. **Close**: hanya bila populasi 0, gudang kandang kosong, dan semua panen sudah ditagih lewat invoice terposting. Biaya final dibekukan dan jurnal penyesuaian HPP dibuat. **Print PDF** ringkasan siklus.
6. **Cancel** (dengan alasan) untuk siklus yang batal sebelum berjalan.

## Daily Recordings
- Satu recording per siklus per hari: mati, culling, bobot rata-rata (BW), pemakaian pakan & OVK (mengurangi stok gudang kandang). Tanggal tidak boleh di masa depan.
- Biasanya diinput PPL lewat aplikasi mobile; admin dapat menginput sebagai cadangan.
- **Revise**: wajib alasan; nilai lama tersimpan di timeline riwayat dan stok dikoreksi otomatis.

## Harvests
Panen per truk: tanggal, ekor, kg, BW rata-rata; lampirkan tiket timbangan. Panen dijual lewat Sales Order → Delivery Order → Sales Invoice.
