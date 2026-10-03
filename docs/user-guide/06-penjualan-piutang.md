# 6. Sales & AR

Alur: **Sales Order → Delivery Order → Sales Invoice → Customer Receipt**.

## Sales Orders
- Customer, ekor (kuantitas mengikat), estimasi kg, harga/kg **tanpa PPN**. Panel kanan menampilkan **exposure kredit** customer.
- **Approve**: dalam credit limit. Bila melebihi: **Approve over limit** dengan alasan wajib (hak *Edit*). Limit 0 = penjualan tunai/tanpa kredit.

## Delivery Orders
- Satu baris = satu panen (truk), dikirim utuh; harga diambil dari SO. Satu panen hanya di satu DO aktif.
- **Print PDF** = surat jalan. Batal DO mengembalikan sisa SO.

## Sales Invoices
- *Draft* dari satu atau lebih DO → **Post** (nomor `INV/…`, jurnal piutang/penjualan/PPN, HPP estimasi) → *PartiallyPaid/Paid*.
- Invoice terposting final; hanya draft yang bisa dibatalkan. **Print PDF** memuat terbilang.

## Credit Notes
Potongan/retur atas invoice terposting (mengurangi piutang & PPN keluaran). **Print PDF**.

## Customer Receipts
- Pilih kas/bank penerimaan, alokasikan ke invoice (boleh parsial, tidak melebihi sisa). Kelebihan menjadi **uang muka**.
- **Apply Advance**: menerapkan uang muka ke invoice berikutnya.
- **Void** (dengan tanggal & alasan) membalik jurnalnya. **Print PDF** kuitansi.

## Receivable Ledger & Aging
Kartu piutang per customer dan umur piutang (current, 1–30, 31–60, …). Ekspor Excel/PDF.
