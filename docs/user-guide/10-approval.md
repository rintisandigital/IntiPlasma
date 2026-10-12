# 10. Approval

Modul approval berjenjang untuk **peternak baru**, **perubahan NIK/rekening peternak**, **kandang baru** dan **aktivasi kontrak** (dokumen keuangan tetap memakai maker-checker biasa). Alurnya diatur admin di **Administration → Approval Flows** ([bagian 2](02-administration.md#approval-flows)).

## Cara kerja
- Level diputuskan **berurutan**: approver level 2 baru melihat pengajuan setelah level 1 menyetujui. Semua level setuju → dokumen aktif; satu level menolak → pengajuan *Rejected*.
- Di setiap level, **salah satu** approver yang terdaftar cukup untuk memutuskan. Pengaju tidak bisa memutuskan pengajuannya sendiri, dan satu orang hanya boleh memutuskan satu level per pengajuan.
- Menolak **wajib alasan**; menyetujui boleh dengan catatan.

## Menu Approvals
| Menu | Isi |
|---|---|
| **Approval Inbox** | Pengajuan yang menunggu keputusan Anda (sebagai approver level berjalan atau penerima delegasi), tab per jenis + jumlah. Klik ikon mata → ringkasan, perubahan lama → baru (perubahan NIK/rekening), riwayat level, lalu **Approve** (catatan opsional) atau **Reject** (alasan). Ikon checklist di header menampilkan jumlahnya. |
| **Approval Requests** | Semua pengajuan di cabang Anda, filter status/jenis/cabang dan **Stuck only** (tidak ada approver aktif di level berjalan). Dengan hak *Edit*: ganti approver satu level pada satu pengajuan (mis. approver cuti). |
| **Delegations** | (admin) Delegasi atas nama approver lain dan pencabutannya. |

Menu user (avatar kanan atas): **My Submissions** — pengajuan yang Anda buat, statusnya, dan pembatalan; **My Approval Delegations** — delegasi Anda.

## Delegasi (cuti)
Approver mengatur **My Approval Delegations** → pilih penerima (approver lain), tanggal **From**–**To** (maks. 90 hari), alasan. Selama periode itu pengajuan yang menunggu Anda juga masuk ke inbox penerima; keputusan tercatat "*penerima* a.n. *Anda*". Delegasi berakhir sendiri atau bisa **Revoke**. Delegasi tidak diteruskan berantai.

## Approver resign / pengajuan macet
Gunakan **Approval Flows → Replace approver** sebelum menonaktifkan user. Pengajuan yang semua approver levelnya nonaktif ditandai **Stuck** di Approval Requests, Approval Flows dan dashboard (*Stuck approvals*).
