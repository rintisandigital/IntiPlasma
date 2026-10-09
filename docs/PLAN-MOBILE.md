# Plan Implementasi — Mobile App PPL & Manager (Inti-Plasma)

> Lanjutan dari [RANGKUMAN.md](RANGKUMAN.md) §9 (backlog: "mobile app PPL", keputusan bisnis #4) dan [PLAN.md](PLAN.md) keputusan #4 (API *client-agnostic*, recording offline-ready).
> Project: `src/MobileApp/MobileApp.csproj` (.NET MAUI **Blazor Hybrid**, .NET 10, sudah masuk `IntiPlasma.slnx`; saat ini masih template bawaan).
> Referensi tampilan: `docs/html-template-mobile/` (template **Findee**, Bootstrap 5, mobile-first).
> Penomoran fase mobile memakai prefix **M** (M0, M1, …) agar tidak bentrok dengan fase API (0–9) dan WebApp (W0–W10). Kode keputusan memakai format **M-n** (bukan fase).
> Riwayat: disusun 2026-10-08. Revisi 1: tambah Request Pengiriman Pakan & Stok Ayam Harian. Revisi 2: stok harian = informasi Sales, input per rentang bobot, request pakan tanpa approval. **Revisi 3**: approval **berjenjang (multi level)** lewat modul approval terpusat, mutasi pakan menjadi pengajuan, menu WebApp Production → Live Bird Stock, UI memakai template `html-template-mobile`; fase disusun ulang (M0–M10). **Revisi 5**: semua usulan (M-29 s.d. M-33, M-36 s.d. M-39) disepakati. **Revisi 4**: konfirmasi M-9/M-10/M-17/M-19/M-35, **lingkup data PPL** = farmer/kandang yang menjadi tanggung jawabnya (ditegakkan server), Manager = satu cabang; logo `rdi-*`.

---

## 1. Keputusan

### 1.1 Disepakati (2026-10-08) — semua keputusan M-1 s.d. M-39

| # | Topik | Keputusan |
|---|-------|-----------|
| M-1 | Pengguna | Dua peran: **PPL** (petugas lapangan) dan **Manager**. |
| M-2 | Fitur Manager | Login, Dashboard, List/Detail Farmer, List/Detail Farm, List/Detail Contract, Recording Harian (lihat), Grafik perkembangan produksi, **Approval**, lihat Stok Harian & Request Pakan (M-26). |
| M-3 | Fitur PPL | Login, Dashboard, **Add/Edit** + List/Detail Farmer, Farm, Contract, **Recording Harian** (input), Grafik perkembangan produksi, **Request Pengiriman Pakan**, **Input Stok Harian** (tanggal, farm, range bobot, tonase, ekoran). |
| M-4 | Offline | **Recording harian (+ foto) bisa diinput offline**: disimpan di SQLite lokal lalu dikirim lewat antrean sinkron. Data master/list di-*cache* **read-only**. Add/Edit farmer, farm, kontrak, revisi recording, request pakan, mutasi pakan, dan approval **wajib online**. |
| M-5 | Dokumen yang di-approve | (a) **Aktivasi kontrak**, (b) **revisi recording**, (c) **Farmer & Farm (kandang) baru** (+ perubahan NIK/rekening, M-14), (d) **Mutasi pakan**. |
| M-6 | Bahasa UI | **Bahasa Indonesia**. Format data id-ID (`1.234.567,89`, `dd/MM/yyyy`, `Rp`, `Asia/Jakarta`). Pesan error API (English) dipetakan ke teks Indonesia per kode error (§3.8). |
| M-7 | Platform | **Android dulu** (min API 24 sesuai template). iOS menyusul. Target Windows hanya untuk debug cepat di mesin dev. |
| M-8 | Arti "Farm" | **Farm = Kandang (`Coop`)**. Hierarki domain: Farmer → Coop (alamat, lat/long, profil kandang). Label UI: "Kandang". |
| M-9 | Arsitektur klien | Mobile memanggil **Web.Api lewat HTTP + JWT** (bukan in-process seperti Web.App). DTO didefinisikan ulang di klien. |
| M-10 | Peran, hak & lingkup data | Menu & aksi berbasis **permission** (role API `PPL` & `Manager` dari seeder, bisa diubah admin; daftar permission dari `GET users/me`). **Lingkup data**: **PPL** hanya melihat Farmer/Farm yang **masih menjadi tanggung jawabnya**; **Manager** melihat Farmer/Farm **satu cabang** (akses cabang yang sudah ada). Lingkup PPL **ditegakkan di server** (§4.5), bukan sekadar filter tampilan. |
| M-11 | Penugasan PPL | **PPL penanggung jawab** di **kandang** (`Coop.FieldOfficerUserId`) dan di **peternak** (`Farmer.FieldOfficerUserId`). Penugasan diubah admin/Manager di WebApp; begitu dipindah, data langsung hilang dari PPL lama dan muncul di PPL baru. |
| M-12 | Pola approval | **Berjenjang (multi level)**: approver level 1 menyetujui → lanjut ke level 2, dst.; **semua level menyetujui → Approved**; **salah satu level menolak → Rejected** (final untuk pengajuan itu). Dijalankan oleh **modul approval terpusat** (§5). |
| M-13 | Mutasi pakan | Mutasi pakan menjadi **pengajuan**; stok tidak bergerak sampai pengajuan **Approved**, lalu mekanisme mutasi yang sudah ada (retur A → induk → transfer B) dijalankan. |
| M-14 | Perubahan setelah disetujui | Edit Farmer/Kandang yang sudah **Active** langsung berlaku (tercatat di audit), **kecuali** perubahan **NIK & rekening bank peternak** yang diajukan ulang lewat approval. Kontrak hanya bisa diubah saat Draft/Rejected. |
| M-15 | Standar performa | Master **Breed Standard** (BW, FCR, deplesi, konsumsi pakan per umur) dikerjakan (fase M5); grafik menampilkan garis standar & deviasi. |
| M-16 | UI kit | Template **`docs/html-template-mobile`** (Findee: Bootstrap 5 + `styles.css` + ikon icomoon + font Inter), diadaptasi ke komponen Razor (§3.9). Grafik **Chart.js**. Semua aset **lokal** di `wwwroot`. |
| M-17 | Masa berlaku sesi | Refresh token dijadikan konfigurasi `Jwt:RefreshTokenExpirationInDays` (saat ini *hard-coded* 7) = **30 hari** (sliding karena rotasi). Access token tetap 60 menit. |
| M-18 | Identitas aplikasi | Nama **"IntiPlasma"**, `ApplicationId` **`com.intiplasma.mobile`**. |
| M-19 | Distribusi | **Internal saja**: APK dibagikan langsung ke perangkat PPL/Manager (atau MDM perusahaan); tidak dirilis ke Play Store. |
| M-20 | Request Pengiriman Pakan | **Tanpa approval.** PPL mengajukan → langsung masuk daftar outstanding **admin gudang**, yang memenuhinya di WebApp dengan **Transfer Stok** induk → gudang kandang **dari permintaan** (boleh bertahap) → `PartiallyDelivered`/`Delivered`. PPL bisa membatalkan selama belum ada pengiriman; admin bisa menutup sisa dengan alasan. |
| M-21 | Isi Request Pengiriman Pakan | Item kategori **Pakan** saja (OVK tidak), satuan item (mis. SAK), tanggal kirim yang diminta, catatan. Form menampilkan **stok pakan kandang**, rata-rata pemakaian 3 hari terakhir, dan **estimasi sisa hari**. **Wajib online.** Transfer tidak boleh melebihi sisa permintaan. |
| M-22 | Arti "Stok Harian" | **Informasi untuk tim Sales**: estimasi stok ayam hidup siap jual per kandang per tanggal per **rentang bobot** (ekoran & tonase). **Bukan** pergerakan persediaan, **tidak** membuat jurnal, **tanpa** approval. |
| M-23 | Rentang bobot | **Master Rentang Bobot diinput dari WebApp** (Master Data → Weight Ranges). PPL memilih dari daftar. Rentang harga jaminan di kontrak tetap terpisah. |
| M-24 | Frekuensi input Stok Harian | **Satu entri per rentang bobot**: dalam satu tanggal & siklus PPL boleh menginput **beberapa kali**, masing-masing untuk rentang berbeda. Input ulang rentang yang sama di tanggal yang sama **mengubah** entri itu. |
| M-25 | Offline Stok Harian | **Bisa offline**, memakai antrean sinkron yang sama dengan recording (`id` dari klien, idempotent). |
| M-26 | Akses Manager | Manager bisa **melihat Stok Harian** (rekap cabang per rentang + drill-down kandang) dan **melihat Request Pakan** (list/detail + status pemenuhan), read-only. |
| M-27 | Validasi Stok Harian | Hanya siklus yang sudah chick-in & belum tutup; tanggal ≤ hari ini (+1 hari toleransi); `ekor > 0`, `tonase > 0`; bobot rata-rata (tonase ÷ ekor) **harus di dalam rentang** entri itu; **total ekor semua rentang** pada tanggal itu ≤ populasi berjalan. Tonase dalam **kg** (ditampilkan juga dalam ton). |
| M-28 | Stok Harian di WebApp | Menu **Production → Live Bird Stock**: rekap per tanggal × cabang × rentang bobot × kandang + ekspor Excel/PDF. |
| M-29 | Approver per level | Setiap level berisi **daftar user** approver (minimal 1); **salah satu** user di daftar cukup untuk memutuskan level itu. Diatur admin di WebApp (Administration → **Approval Flows**). |
| M-30 | Lingkup alur | Alur didefinisikan **per jenis dokumen**, dengan **alur default (semua cabang)** dan **override per cabang** (alur cabang dipakai bila ada). Jumlah level 1–5. |
| M-31 | Alur belum diatur | Pengajuan **ditolak** dengan pesan "Alur approval untuk dokumen ini belum diatur" (bukan otomatis disetujui). |
| M-32 | Perubahan alur | Saat diajukan, level & approver **disalin (snapshot)** ke pengajuan; perubahan alur hanya berlaku untuk pengajuan baru. Admin bisa **mengganti approver** pengajuan berjalan bila approver tidak aktif (dicatat di audit). |
| M-33 | Aturan approver | Approver **bukan pengaju**, punya **akses cabang** dokumen, dan **satu user tidak boleh memutuskan dua level** dalam pengajuan yang sama. Reject **wajib alasan**. Pengaju bisa **membatalkan** selama Pending. Setelah Rejected, dokumen diperbaiki lalu **diajukan ulang** (pengajuan baru, mulai dari level 1). |
| M-34 | Fitur keuangan | Mobile **tidak** memiliki fitur PO, PV, kas keluar, jurnal manual, settlement, maupun SO over-limit. Dokumen tersebut tetap di **WebApp** dengan maker-checker yang sudah ada dan **tidak** masuk modul approval berjenjang dalam rencana ini. |
| M-35 | Adaptasi template & logo | Primary color **`#0984E3`** (template `#2748DB`); **light mode saja**; font **Inter di-host lokal**; **tanpa jQuery/plugin JS** template (interaksi dibuat di Blazor, CSS template dipakai); logo **`src/MobileApp/wwwroot/images/rdi-*.png`** menggantikan logo Findee (§3.9). |
| M-36 | Mekanisme lingkup PPL | Permission baru **`partnership:assigned-only`** di role PPL: user yang memilikinya (kecuali role sistem Administrator) hanya bisa membaca & mengubah data yang ditugaskan kepadanya. User tanpa permission ini tetap memakai lingkup cabang seperti sekarang (Manager, admin, user WebApp lama tidak terpengaruh). |
| M-37 | Aturan "tanggung jawab PPL" | **Kandang**: `Coop.FieldOfficerUserId = saya`. **Peternak**: `Farmer.FieldOfficerUserId = saya` **atau** punya minimal satu kandang yang ditugaskan ke saya. Peternak/kandang yang **dibuat PPL** otomatis ditugaskan ke pembuatnya. PPL hanya boleh menambah kandang untuk peternak dalam lingkupnya. |
| M-38 | Data turunan dalam lingkup PPL | Lingkup PPL juga berlaku untuk **siklus, recording, grafik, stok ayam harian, request pakan, mutasi pakan** (kandang dalam lingkup). Kandang **tujuan** mutasi pakan boleh di luar lingkup (dipilih dari satu cabang), karena keputusan ada di approval. **Kontrak** tetap memakai **lingkup cabang** (direvisi 2026-10-09: `PartnershipContract` tidak punya `FarmerId`, kontrak adalah skema & harga tingkat cabang, dan kontrak buatan PPL di M7 harus tetap terlihat). |
| M-39 | Siapa mengubah penugasan | **WebApp** (form Farmer & Coop, hak Edit) + layar **Field Officer Assignment** (pindahkan banyak kandang/peternak sekaligus dari PPL A ke B, mis. saat mutasi pegawai). Tidak di mobile. |

---

## 2. Ringkasan Temuan

### 2.1 Kondisi `src/MobileApp` saat ini
| Bagian | Kondisi |
|---|---|
| Jenis | Template **.NET MAUI Blazor Hybrid** (`Components/Pages/Counter`, `Weather`, `Home`; `MainLayout` + `NavMenu` desktop). |
| `MobileApp.csproj` | `TargetFrameworks` android/ios/maccatalyst/windows; `PackageReference` **memakai `Version=`** → bentrok dengan `ManagePackageVersionsCentrally=true` (NU1008); versi harus pindah ke `Directory.Packages.props`. |
| Build | `Directory.Build.props` juga berlaku: `TargetFramework=net10.0`, `AnalysisMode=All`, `TreatWarningsAsErrors`, Sonar. Perlu dicek kombinasi `TargetFramework` (props) + `TargetFrameworks` (csproj) dan analyzer di kode generated MAUI/Razor. |
| Identitas | `ApplicationTitle=MobileApp`, `ApplicationId=com.companyname.mobileapp`, ikon/splash default `#512BD4`. |

### 2.2 Template `docs/html-template-mobile`
| Bagian | Kondisi |
|---|---|
| Isi | 72 halaman HTML (mirror HTTrack dari demo **Findee** — aplikasi cari kerja), ±5 MB. |
| CSS | `bootstrap.min.css`, `styles.css` (variabel `--Primary: #2748DB`, `--Red`, `--Success`, `--Warning`, dark theme via `.dark-theme`), `swiper-bundle`, `nouislider`, `bootstrap-touchspin`. |
| Font & ikon | `fonts.css` memuat **Inter dari Google Fonts** (online); ikon **icomoon** lokal (±118 ikon, mis. `icon-house-fill`). |
| JS | jQuery, Bootstrap, Swiper, noUiSlider, nice-select, touchspin, `main.js` — tidak dipakai (M-35). |
| Komponen berguna | bottom bar `menubar-footer`, header, list kartu, detail bertab, filter, form input/dropdown/radio/switch, stepper, modal, alert, badge, progress, spinner, halaman sukses. |
| Lisensi | Template Themesflat — **pastikan lisensi** mencakup pemakaian di aplikasi internal (lihat §10). |

### 2.3 Kesiapan Web.Api untuk fitur mobile
| Fitur | Endpoint yang sudah ada (`/api/v1`) | Permission | Catatan |
|---|---|---|---|
| Login | `POST users/login` → `{accessToken, refreshToken}`; `POST users/refresh-token` (rotasi) | anonim, rate limit 10/menit | Kredensial salah → **404** `Users.NotFoundByEmail`; lockout → `Users.LockedOut`; nonaktif → `Users.Inactive`. |
| Farmer | `GET/POST farmers`, `GET/PUT farmers/{id}` | `farmers:read` / `farmers:manage` | List: `search, page, pageSize (≤100), branchId, type`. POST idempotent. |
| Farm (Coop) | `GET/POST coops`, `GET/PUT coops/{id}` | `farmers:read` / `farmers:manage` | Filter `farmerId`, `branchId`. Body: `farmerId, code, name, capacity, houseType, address, latitude, longitude, documents, profile`. |
| Contract | `GET/POST contracts`, `GET/PUT contracts/{id}`, `POST contracts/{id}/activate`, `/deactivate` | `contracts:read` / `contracts:manage` | **Aktivasi tanpa approval** (diubah, §5). |
| Siklus | `GET cycles` (filter `farmerId, coopId, status`), `GET cycles/{id}` | `cycles:read` | Recording & stok harian menempel ke siklus terbuka kandang. |
| Recording harian | `GET production/daily-recordings?cycleId&from&to` (tanpa paging), `GET …/{id}`, `POST …` (idempotent, `id` dari klien), `PUT …/{id}` (revisi + `reason`) | `production:read` / `production:record` / `production:revise` | Body: `id, cycleId, date, mortality, culling, averageBodyWeightGram?, notes?, usages[{itemId, uomId, quantity}], documents[]`. Pakan/OVK lewat `usages`. Satu per hari; maks +1 hari dari tanggal server. **Revisi langsung berlaku** (diubah, §5). |
| Grafik | `GET cycles/{id}/performance` | `production:read` | *Time series* per hari: umur, mati, culling, pakan, BW, kumulatif (deplesi %, FCR, ADG, IP, populasi). |
| Lampiran | `POST attachments` (multipart, `id` dari klien → idempotent), `GET attachments/{id}/content` | `attachments:upload` / `read` | JPEG/PNG/WEBP/PDF ≤ 10 MB, maks 20 per entity. |
| Pendukung | `GET items`, `uoms`, `warehouses`, `inventory/stock-balances?warehouseId` | `master-data:read`, `warehouses:read`, `inventory:read` | Pilihan pakan/OVK & stok kandang. |
| Transfer stok | `inventory/stock-transfers` | `inventory:transfer` | Dipakai admin gudang untuk memenuhi Request Pakan. |
| Mutasi pakan | ada (Inventory, `StockReturnUseCases`) | `inventory:return` | **Langsung memindahkan stok** (diubah menjadi pengajuan, M-13). |
| Approval | **tidak ada** modul terpusat; maker-checker per entity keuangan | – | Modul baru (§5). |
| Request Pakan, Stok Ayam | **belum ada** | – | Entity baru (§4.3, §4.4). |

Konvensi API: JSON camelCase, enum sebagai **string**, `PagedList{items, page, pageSize, totalCount}`, error **ProblemDetails** (kode di `title`, pesan di `detail`, validasi di `errors`), create → `200` + Guid, update/aksi → `204`, concurrency → `409`, header `Idempotency-Key` (GUID) pada create, rate limit global 100/menit/user.

### 2.4 Celah yang harus diisi (backend)
| # | Celah | Tindakan (fase) |
|---|---|---|
| 1 | Tidak ada `GET users/me`; JWT tanpa permission | `GET users/me` → profil, cabang, **permission**, kandang yang ditugaskan (M0). |
| 2 | Tidak ada logout & ganti password di API | `POST users/logout`, `POST users/me/change-password` (M0). |
| 3 | Refresh token *hard-coded* 7 hari | `Jwt:RefreshTokenExpirationInDays` (M0). |
| 4 | Tidak ada role PPL/Manager | Seeder role default (M0, §4.2). |
| 5 | Tidak ada penugasan PPL & lingkup data per PPL | `Coop/Farmer.FieldOfficerUserId` + `IFieldScope` ditegakkan di handler (M1, §4.5). |
| 6 | Tidak ada data bundel untuk form offline | `GET mobile/field-context` (M2). |
| 7 | Tidak ada stok ayam & master rentang bobot | `WeightRange` + `LiveBirdStockEntry` (M3). |
| 8 | Tidak ada dashboard di API | `GET mobile/dashboard` (M4). |
| 9 | Tidak ada standar performa | `BreedStandard` (M5). |
| 10 | Tidak ada approval berjenjang & status approval Farmer/Coop/Contract | Modul approval + status (M6). |
| 11 | Revisi recording & mutasi pakan langsung berlaku | Pengajuan lewat modul approval (M8). |
| 12 | Tidak ada permintaan pengiriman pakan | `FeedDeliveryRequest` (M9). |
| 13 | Login salah → 404 | Klien memetakan ke "Email atau password salah". |
| 14 | `Jwt:ExpirationInMinutes` kosong di appsettings dasar | Isi di konfigurasi produksi (`docs/DEPLOY.md`). |

---

## 3. Arsitektur Klien

### 3.1 Gambaran
```
┌──────────────── MobileApp (MAUI Blazor Hybrid, Android) ─────────────────┐
│ Razor Components (Pages, Layout, komponen UI dari template Findee)       │
│        │                                                                 │
│ MobileApp.Core (net10.0, tanpa MAUI → bisa di-unit test)                 │
│   ApiClient (HttpClient + AuthHandler + ProblemDetails → Result)         │
│   SessionService (token, profil, permission)                             │
│   LocalStore (SQLite: cache read-only + antrean sinkron)                 │
│   SyncEngine (kirim antrean, unduh field-context)                        │
└──────────────────────────────┬───────────────────────────────────────────┘
                               │ HTTPS + JWT
                               ▼
                 Web.Api (/api/v1) ──► Application ──► PostgreSQL
                               ▲
           Web.App (background job outbox; layar admin: Approval Flows, Approval Inbox,
                    Weight Ranges, Live Bird Stock, Feed Delivery Requests, Breed Standards)
```
- **`src/MobileApp.Core`** (class library `net10.0`): semua logika non-UI (API client, DTO, sinkronisasi, pemetaan error, format). Komponen Razor hanya memanggil service. `tests/MobileApp.UnitTests` menguji Core tanpa emulator.
- Abstraksi platform di Core (`ISecureStore`, `IConnectivity`, `IClock`, `IPhotoPicker`, `IGeolocation`); implementasi di MobileApp.
- ⚠️ Event outbox dari transaksi mobile diproses **Web.App**; Web.App harus berjalan.

### 3.2 Struktur folder target
```
src/MobileApp.Core/
  Api/            ApiClient, AuthHandler, ApiError, endpoint per modul (Farmers, Coops, Contracts, Cycles,
                  Recordings, LiveBirdStock, FeedRequests, FeedMutations, Approvals, Dashboard, Attachments, Users)
  Contracts/      DTO request/response (record), PagedList<T>, enum string
  Session/        SessionService, TokenStore, CurrentUser (permission → fitur)
  Local/          LocalDb (SQLite), tabel cache & antrean, migrasi skema lokal
  Sync/           SyncEngine, SyncItem, kebijakan retry
  Formatting/     IdFormat (angka, tanggal, Rp, ton), ErrorMessages (kode → teks Indonesia)
src/MobileApp/
  Components/
    Layout/       MainLayout (header + menubar-footer), OfflineBanner, SyncBadge
    Shared/       ListPage, SearchBox, InfiniteList, FilterSheet, StatusBadge, PhotoField, Modal, ConfirmSheet,
                  ReasonSheet, Stepper, Tabs, EmptyState, ApprovalTimeline, ChartCanvas, SuccessPage
    Pages/        Login, Dashboard, Farmers/*, Coops/*, Contracts/*, Recordings/*, LiveBirdStock/*,
                  FeedRequests/*, FeedMutations/*, Performance/*, Approvals/*, SyncQueue, Profile, Settings
  Platforms/      implementasi abstraksi platform
  wwwroot/        css/bootstrap.min.css, css/styles.css (template), css/app.css (override: primary, komponen tambahan),
                  fonts/icomoon*, fonts/inter/* (self-host), images/logo/*, lib/chartjs, js/chart-interop.js
tests/MobileApp.UnitTests/
```

### 3.3 Autentikasi & sesi
- Login → token di **`SecureStorage`** → `GET users/me` → profil + permission disimpan di SQLite (untuk buka aplikasi saat offline).
- `AuthHandler`: sisipkan bearer; bila `401`, **refresh sekali** (dikunci `SemaphoreSlim` karena refresh token berotasi), ulangi request; gagal → hapus sesi, ke Login. Antrean offline **tidak dihapus**; dikirim setelah login ulang oleh **user yang sama** (user lain → antrean ditahan + peringatan).
- **Mode offline**: sesi tersimpan + tanpa koneksi → aplikasi terbuka dengan data cache; fitur wajib online dinonaktifkan ("Butuh koneksi").
- Logout → `POST users/logout` (bila online) → hapus token & cache (antrean belum terkirim: konfirmasi dulu).
- **Alamat server** di Pengaturan: default dari build config (dev `http://10.0.2.2:5000` untuk emulator); dikunci di Release.

### 3.4 Peran → fitur
Menu ditentukan dari permission di `users/me` (M-10). **Lingkup data**: PPL hanya data yang ditugaskan (M-36 s.d. M-38), Manager sesuai profil Akses Cabang:

| Fitur | Permission | PPL | Manager | Offline |
|---|---|:-:|:-:|:-:|
| Dashboard | (login) — isi per peran (§6.2) | ✓ | ✓ | cache |
| Farmer — list/detail | `farmers:read` | ✓ | ✓ | cache |
| Farmer — add/edit/ajukan | `farmers:manage` | ✓ | – | – |
| Farm (Kandang) — list/detail | `farmers:read` | ✓ | ✓ | cache |
| Farm (Kandang) — add/edit/ajukan | `farmers:manage` | ✓ | – | – |
| Contract — list/detail | `contracts:read` | ✓ | ✓ | cache |
| Contract — add/edit/ajukan | `contracts:manage` | ✓ | – | – |
| Recording harian — lihat | `production:read` | ✓ | ✓ | cache |
| Recording harian — input | `production:record` | ✓ | – | **✓** |
| Recording — ajukan revisi | `production:revise` | ✓ | – | – |
| Stok ayam harian — input | `production:stock-report` (baru) | ✓ | – | **✓** |
| Stok ayam harian — lihat/rekap | `production:read` | ✓ | ✓ | cache |
| Request pakan — ajukan/batal | `inventory:request-feed` (baru) | ✓ | – | – |
| Request pakan — lihat | `inventory:read` | ✓ | ✓ | cache |
| Mutasi pakan — ajukan | `inventory:request-feed-mutation` (baru) | ✓ | – | – |
| Grafik produksi | `production:read` | ✓ | ✓ | cache |
| Pengajuan saya (status approval) | (login) | ✓ | ✓ | cache |
| Approval — inbox, setujui/tolak | `approvals:decide` (baru) **dan** terdaftar sebagai approver di level berjalan (§5) | – | ✓ | – |

### 3.5 Penyimpanan lokal (SQLite, `Microsoft.Data.Sqlite`)
| Tabel | Isi | Sumber / umur |
|---|---|---|
| `session` | profil, permission, cabang, waktu sinkron terakhir | `users/me` |
| `cache_entries` | JSON respons list/detail (key = URL) + `fetched_at` | setiap respons sukses; dibaca bila offline ("Data per dd/MM HH:mm") |
| `field_context` | siklus terbuka dalam scope, item pakan/OVK + satuan, stok gudang kandang, **rentang bobot**, populasi berjalan | `GET mobile/field-context` |
| `sync_queue` | `id`, `kind` (`Attachment`/`Recording`/`LiveBirdStock`), payload JSON, path foto, status (`Pending/Sending/Failed/Done`), `attempts`, `last_error`, `created_at` | input offline |
| `local_entries` | recording & stok ayam yang belum terkirim (tampil di list, dashboard & grafik sebagai "Belum terkirim") | dihapus setelah sinkron sukses |

Cache dihapus saat logout atau ganti user. NIK dimasker di list.

### 3.6 Input offline & sinkronisasi (recording & stok ayam harian)
1. **Siapkan** (online): `GET mobile/field-context` → siklus terbuka yang boleh diinput (default kandang yang ditugaskan), umur hari ini, populasi berjalan, tanggal recording & stok ayam terakhir, item pakan/OVK + konversi satuan, saldo stok gudang kandang, rentang bobot aktif. Diunduh saat login, saat buka halaman input (online), dan tombol "Perbarui data".
2. **Input** (offline/online): `id = Guid v7` dibuat klien.
   - Recording: tanggal ≤ hari ini, belum ada recording tanggal itu (server + lokal), mati + culling ≤ populasi, pemakaian ≤ stok cache (peringatan, bukan blokir).
   - Stok ayam: M-24/M-27 dicek lokal (rentang sama di tanggal sama → ubah entri, total ekor ≤ populasi cache, rata-rata di dalam rentang).
   - Foto dikompres (sisi terpanjang 1600 px, JPEG 80%), `attachmentId` v7.
3. **Antre**: `sync_queue` (lampiran dulu, lalu dokumen yang mereferensikannya) + `local_entries`.
4. **Kirim** (`SyncEngine`): saat koneksi kembali, aplikasi ke foreground, atau tombol "Sinkronkan". FIFO per siklus; recording tanggal yang sama dikirim **sebelum** stok ayam (populasi server sudah terkoreksi). `POST attachments` (id sama → idempotent) → `POST` dokumen dengan `Idempotency-Key` = id dokumen.
5. **Hasil**: `2xx` → `Done` & segarkan cache; jaringan/`5xx`/`429` → tetap `Pending` + *backoff* (30 dtk → 5 mnt); `400/404/409` → `Failed` + pesan Indonesia (ubah & kirim ulang, atau hapus); `401` → refresh, gagal → tahan sampai login.
6. Badge antrean di header; halaman **Antrean Sinkron** menampilkan setiap item & errornya.
- Sinkron hanya saat aplikasi terbuka (tanpa WorkManager); sinkron latar menjadi backlog.

### 3.7 Pola halaman
- **List**: pencarian (debounce), filter di bottom sheet, *infinite scroll* (`pageSize=20`), tarik untuk segarkan, state kosong/offline. Cache tampil dulu, lalu diperbarui.
- **Detail**: kartu ringkasan + tab. Aksi di bottom action bar sesuai permission & status. Dokumen yang melalui approval menampilkan **timeline approval** (level, approver, keputusan, waktu, alasan).
- **Form**: satu kolom, section berlipat, validasi klien (DataAnnotations) + error server per field. Tombol simpan dikunci selama request + `Idempotency-Key` per form.
- **Lokasi kandang**: "Ambil lokasi saat ini" (`Geolocation`); buka di aplikasi peta.
- **Foto/lampiran**: kamera atau galeri, pratinjau, upload langsung saat online.

### 3.8 Error & pesan
- `ApiClient` mengembalikan `Result<T>`/`ApiError {Status, Code, Message, FieldErrors}` dari ProblemDetails.
- `ErrorMessages` memetakan kode yang sering muncul ke Bahasa Indonesia (login salah, lockout, nonaktif, tanggal ganda, stok tidak cukup, siklus tertutup, ekor > populasi, rata-rata di luar rentang, alur approval belum diatur, bukan approver level ini, sudah memutuskan level lain, `Concurrency.Conflict` → "Data sudah diubah pengguna lain, muat ulang"). Kode lain → `detail` asli dengan awalan "Server:".
- Test unit menjaga kode yang dipetakan masih ada (reflection ke assembly Domain di test).

### 3.9 Adaptasi template `html-template-mobile` (M-16, M-35)
| Halaman template | Dipakai untuk |
|---|---|
| `sign-in.html` | Login |
| `chance-password.html`, `new-password.html` | Ganti password |
| `index.html` (+ `menubar-footer`, header) | Dashboard + `MainLayout` |
| `job-list.html`, `saved.html` | List Peternak, Kandang, Kontrak, Recording, Stok Ayam, Request Pakan |
| `jobs-filter.html`, `companies-filter.html` | Bottom sheet filter |
| `job-detail.html`, `company-detail.html`, `employer-profile*.html` (tab) | Detail Peternak, Kandang, Kontrak, Siklus |
| `profile-edit.html`, `ui-input`, `ui-dropdown`, `ui-radio`, `ui-switch` | Form add/edit & input harian |
| `ui-stepper.html` | Wizard kontrak; progres level approval |
| `notification.html`, `message-inbox.html` | Inbox Approval, Pengajuan saya |
| `ui-modal.html`, `ui-alert.html` | Konfirmasi setujui, alasan tolak, peringatan |
| `ui-badge`, `ui-progressbar`, `ui-spinner` | Status, progres sinkron, loading |
| `setting-*.html`, `profile.html` | Profil & Pengaturan |
| `setting-successful.html` | Halaman sukses kirim/ajukan |
| `onboarding*`, `otp`, `sign-up`, `setting-*payment*`, `setting-bill`, `message.html`, `new-feed.html`, dll. | Tidak dipakai |

- Aset yang disalin ke `wwwroot`: `bootstrap.min.css`, `styles.css`, font icomoon, gambar logo; **tidak** menyalin JS template, Swiper, noUiSlider, gambar demo, komentar HTTrack.
- **Logo** (M-35, `src/MobileApp/wwwroot/images/`): `rdi-logo-text.png` → Login & header Dashboard; `rdi-small.png` → ikon kecil di header; `rdi-logo-white.png` / `rdi-small-white.png` → di atas latar primary. **Ikon aplikasi & splash MAUI** dari `rdi-small` (1181×1444, tidak persegi) → dibuat versi persegi dengan *padding* transparan (`Resources/AppIcon/appiconfg.png` di atas latar `#0984E3`; `Resources/Splash/splash.png` dari `rdi-small-white`). PNG besar (800–2000 px) diperkecil untuk `wwwroot`.
- `app.css` meng-override `--Primary` → `#0984E3` dan menambah komponen yang tidak ada di template (tabel ringkas rekap, kartu KPI, timeline approval, grafik).
- Ikon yang tidak tersedia di icomoon (mis. ayam, timbangan, truk) → SVG inline kecil di `Components/Shared/Icons`.

---

## 4. Perubahan Backend

### 4.1 Endpoint baru / berubah (semua `/api/v1`)
| Endpoint | Fase | Keterangan |
|---|---|---|
| `GET users/me` | M0 | `{id, email, firstName, lastName, defaultBranchId, allBranches, branches[], permissions[], roles[]}`. |
| `POST users/logout` | M0 | Body `{refreshToken}` → cabut token itu. |
| `POST users/me/change-password` | M0 | `ChangeOwnPasswordCommand` (sudah ada). |
| `GET farmers`, `coops`, `contracts`, `cycles` (berubah) | M1 | Lingkup PPL diterapkan otomatis di server (§4.5); filter opsional `fieldOfficerId` untuk Manager/WebApp. `POST/PUT farmers` & `coops` menerima `fieldOfficerUserId` (untuk user lingkup PPL selalu dirinya). |
| `GET users/field-officers` | M1 | Lookup PPL dalam cabang (filter Manager, form WebApp). |
| `GET mobile/field-context` | M2 | Bundel data form offline; M3 menambah rentang bobot & entri stok terakhir. |
| `GET weight-ranges` | M3 | Master rentang bobot aktif (CRUD hanya di WebApp). |
| `POST production/live-bird-stocks` | M3 | Upsert per (siklus, tanggal, rentang), idempotent, `id` dari klien. |
| `DELETE production/live-bird-stocks/{id}` | M3 | Hapus entri salah rentang (pembuat, tanggal yang sama). |
| `GET production/live-bird-stocks?cycleId&branchId&date&from&to` | M3 | List entri. |
| `GET production/live-bird-stocks/summary?date&branchId` | M3 | Rekap rentang × kandang (Manager mobile & WebApp). |
| `GET mobile/dashboard` | M4 | Ringkasan per peran (§6.2). |
| `GET breed-standards` | M5 | Standar (CRUD di WebApp); `performance` menambah `standard[]`. |
| `GET approvals/inbox`, `GET approvals/inbox/count` | M6 | Pengajuan Pending yang **menunggu keputusan saya** (level berjalan), per jenis. |
| `GET approvals/mine` | M6 | Pengajuan yang saya ajukan + status/level. |
| `GET approvals/{id}`, `GET approvals?documentType&documentId` | M6 | Detail + timeline (level, approver, keputusan). |
| `POST approvals/{id}/approve` `{note?}`, `/reject` `{reason}`, `/cancel` | M6 | Keputusan level berjalan; cancel oleh pengaju. |
| `POST farmers` / `POST coops` (berubah) | M6 | Create langsung **mengajukan** approval (status `PendingApproval`). |
| `POST farmers/{id}/submit`, `POST coops/{id}/submit` | M6 | Ajukan ulang setelah Rejected. Perubahan NIK/rekening lewat `PUT farmers/{id}` otomatis membuat pengajuan `FarmerChange`. |
| `POST contracts/{id}/submit` | M6 | Draft → PendingApproval. ⚠️ `activate` dihapus (aktif hanya lewat approval). |
| `POST production/daily-recordings/{id}/revision-requests` | M8 | Ajukan revisi (body = revisi + `reason`). ⚠️ `PUT …/{id}` dihapus. |
| `POST inventory/feed-mutation-requests`, `GET`, `GET /{id}` | M8 | Ajukan mutasi pakan. ⚠️ create mutasi langsung dihapus dari API (hanya lewat approval). |
| `POST inventory/feed-delivery-requests`, `GET` (filter `cycleId, coopId, branchId, status, fieldOfficerId`), `GET /{id}`, `/{id}/cancel`, `/{id}/close` | M9 | §4.3 (tanpa approval). |
| `POST inventory/stock-transfers` (berubah) | M9 | Field opsional `feedDeliveryRequestId`. |

Semua endpoint memakai `HasPermission`, akses cabang lewat `IBranchAccess`, satu file per use case (boleh dipakai Web.App). Konfigurasi **Approval Flows** hanya di Web.App (in-process); endpoint API-nya tidak dibuat dulu.

### 4.2 Permission & role default
Permission baru (`Domain/Roles/Permissions.cs`):
- `production:stock-report` — input stok ayam harian.
- `inventory:request-feed` — ajukan & batalkan Request Pakan.
- `inventory:request-feed-mutation` — ajukan mutasi pakan.
- `partnership:assigned-only` — membatasi lingkup data ke farmer/kandang yang ditugaskan (M-36). Bukan hak tambahan, melainkan pembatas; diabaikan untuk role sistem Administrator.
- `approvals:decide` — membuka inbox & memutuskan pengajuan. Hak memutuskan pengajuan tertentu **tetap** ditentukan oleh daftar approver di level berjalan (M-29); permission ini hanya pintu masuk fitur.

Tidak ada permission `*:approve` per jenis untuk dokumen M-5 (digantikan alur approval). Konfigurasi alur di WebApp memakai Akses Menu (menu Administration → Approval Flows).

Role default (seeder, dibuat bila belum ada; bisa diubah admin):

| Role | Permission |
|---|---|
| **PPL** | `partnership:assigned-only`, `farmers:read`, `farmers:manage`, `contracts:read`, `contracts:manage`, `cycles:read`, `production:read`, `production:record`, `production:revise`, `production:stock-report`, `inventory:read`, `inventory:request-feed`, `inventory:request-feed-mutation`, `master-data:read`, `warehouses:read`, `attachments:upload`, `attachments:read` |
| **Manager** | `farmers:read`, `contracts:read`, `cycles:read`, `production:read`, `inventory:read`, `master-data:read`, `warehouses:read`, `attachments:read`, `approvals:decide` |

Akses cabang tetap lewat profil Akses Cabang (W1). User mobile dibuat admin di WebApp ("API / Mobile access", W-20).

### 4.3 Request Pengiriman Pakan (M-20, M-21)
**Domain** (`Domain/Inventory/FeedDeliveryRequests`, schema `inventory`):
- `FeedDeliveryRequest` (aggregate, `IHasDocuments`): `Number` (`RPP/{CABANG}/YYYY/BULAN-ROMAWI/NNNN`), `BranchId`, `CycleId`, `CoopWarehouseId`, `RequestedDeliveryDate`, `Notes`, `Status`, `RequestedBy/At`, `CancelledBy/At`, `ClosedBy/At` + `CloseReason`, `Documents`; baris `FeedDeliveryRequestLine`: `ItemId` (kategori Feed), `UomId`, `Quantity`, `DeliveredQuantity` (satuan dasar).
- Status: `Open` → `PartiallyDelivered` → `Delivered`; `Open` → `Cancelled` (pengaju, sebelum ada pengiriman); `Open/PartiallyDelivered` → `Closed` (admin gudang, alasan wajib).
- Aturan: siklus sudah chick-in & belum tutup; item Feed aktif; qty > 0; tanggal kirim ≥ hari ini.
- **Pemenuhan**: `StockTransfer.FeedDeliveryRequestId?`; tujuan = gudang kandang & siklus permintaan, item ada di permintaan, qty ≤ sisa; handler transfer (satu transaksi) menambah `DeliveredQuantity` & mengubah status.

**Application**: `CreateFeedDeliveryRequestCommand`, `CancelFeedDeliveryRequestCommand`, `CloseFeedDeliveryRequestCommand`, `GetFeedDeliveryRequestsQuery`, `GetFeedDeliveryRequestByIdQuery` (+ transfer pemenuhan), `GetOutstandingFeedDeliveryRequestsQuery`; `CreateStockTransferCommand` diperluas.

**Web.App** (`Areas/Inventory`): menu **Feed Delivery Requests** (list + filter, detail sisa per baris, **Create Transfer** dari sisa permintaan, Close sisa, ekspor), kolom "Request" di list & PDF Stock Transfer.

**Mobile**: PPL — ajukan (stok kandang, rata-rata pakai 3 hari, estimasi sisa hari), "Permintaan saya", detail + transfer pemenuhan, batal. Manager — list & detail cabang (read-only), kartu request terbuka/terlambat di dashboard.

### 4.4 Stok Ayam Harian (M-22 s.d. M-28)
**Domain**:
- `WeightRange` (schema `master`, diinput dari WebApp): `Code`, `Name`, `MinWeightKg` (inklusif, null = tanpa batas bawah), `MaxWeightKg` (eksklusif, null = tanpa batas atas), `SortOrder`, `IsActive`. Rentang aktif tidak boleh tumpang tindih. Tidak di-seed (data demo mengisi contoh).
- `LiveBirdStockEntry` (schema `production`, `IHasDocuments`) — **satu entri = satu rentang**: `Id` (klien), `BranchId`, `CycleId`, `CoopId`, `Date`, `WeightRangeId`, `Birds` (ekoran), `WeightKg` (tonase), `AgeDays` (server), `Notes`, `Documents`; turunan `AverageWeightKg`.
- Aturan: unik (siklus, tanggal, rentang) — `id` sama → idempotent; `id` lain untuk kombinasi sama → **mengubah** entri (riwayat lewat audit); validasi M-27; pembuat boleh menghapus entri pada tanggal yang sama.
- Tanpa jurnal, tanpa efek ke persediaan/populasi, tanpa approval.

**Application**: `UpsertLiveBirdStockEntryCommand`, `DeleteLiveBirdStockEntryCommand`, `GetLiveBirdStockEntriesQuery`, `GetLiveBirdStockSummaryQuery` (per siklus aktif memakai entri dari **tanggal input terakhir ≤ tanggal dipilih**, dengan umur data), CRUD `WeightRange`.

**Web.App**: Master Data → **Weight Ranges**; **Production → Live Bird Stock** (M-28: rekap per tanggal & cabang, drill-down kandang, ekspor Excel/PDF).

**Mobile**: PPL — input per rentang (ekoran + tonase → rata-rata & validasi langsung), daftar entri hari itu, total, sisa populasi tak terlapor, "Salin dari kemarin". Manager — rekap cabang per rentang + drill-down kandang (read-only).

### 4.5 Lingkup data PPL (M-10, M-36 s.d. M-39)
**Domain**: `Coop.FieldOfficerUserId` dan **`Farmer.FieldOfficerUserId`** (baru), keduanya opsional, harus user aktif di cabang yang sama. Migration membiarkan kosong (data lama hanya terlihat oleh Manager/admin sampai ditugaskan).

**Application** — `IFieldScope` (berdampingan dengan `IBranchAccess`, implementasi di Infrastructure, di-cache seperti permission):
- `IsRestricted` = user punya `partnership:assigned-only` dan bukan Administrator.
- Query (Dapper) menambah klausa bila restricted: kandang `c.field_officer_user_id = @UserId`; peternak `f.field_officer_user_id = @UserId OR EXISTS (kandang peternak itu yang ditugaskan ke @UserId)`; kontrak **tidak** dibatasi (lingkup cabang, M-38); siklus, recording, performance, stok ayam, request pakan, mutasi pakan lewat kandang. Lingkup cabang tetap berlaku di atasnya.
- Detail & command memanggil `EnsureInScopeAsync` (di luar lingkup → **404**, agar keberadaan data tidak bocor). Create oleh user restricted: `FieldOfficerUserId` diisi dirinya; kandang hanya untuk peternak dalam lingkup.
- `GET mobile/field-context` & `mobile/dashboard` otomatis mengikuti lingkup.
- Inbox approval **tidak** memakai lingkup PPL (approver ditentukan alur, M-29).

**Web.App**: field **PPL** di form & list Farmers/Coops (+ filter per PPL); menu **Partnership → Field Officer Assignment** (pilih PPL asal → centang peternak/kandang → pindahkan ke PPL tujuan; tercatat di audit log). User WebApp tetap memakai Akses Menu & Akses Cabang (lingkup PPL tidak berlaku di WebApp).

**Mobile**: PPL tidak perlu filter "Kandang saya" (server sudah membatasi); Manager punya filter **PPL** di list peternak/kandang/siklus.

---

## 5. Modul Approval Berjenjang (M-12, M-29 s.d. M-33)

Merealisasikan rencana PLAN-WEBAPP §4.8 untuk jenis dokumen M-5. Schema PostgreSQL baru **`approval`**.

### 5.1 Model
- **`ApprovalFlow`** (aggregate, dikonfigurasi di WebApp): `DocumentType`, `BranchId?` (null = default semua cabang), `Name`, `IsActive`, `Levels[]` → `ApprovalFlowLevel { LevelNo (1..5), Name (mis. "Manager Unit", "Kepala Cabang"), ApproverUserIds[] (≥ 1) }`. Unik: satu alur aktif per (`DocumentType`, `BranchId`). Approver harus user aktif yang punya `approvals:decide`.
- **`ApprovalRequest`** (aggregate): `DocumentType`, `DocumentId`, `BranchId`, `DocumentNumber/Title`, `Summary` (teks ringkas untuk inbox), `RequestedBy/At`, `Status` (`Pending` → `Approved` / `Rejected` / `Cancelled`), `CurrentLevel`, **snapshot level** (`LevelNo`, `Name`, `ApproverUserIds[]`), `Decisions[]` (`LevelNo`, `Decision` Approve/Reject, `DecidedBy`, `DecidedAtUtc`, `Note/Reason`). Satu `Pending` per dokumen (partial unique index).
- **`DocumentType`** (enum): `Farmer`, `FarmerChange` (NIK/rekening, M-14), `Coop`, `Contract`, `RecordingRevision`, `FeedMutation`. Modul tidak dipakai untuk dokumen keuangan WebApp (M-34); bila kelak dibutuhkan, cukup menambah nilai enum + handler.

### 5.2 Alur
```
Dokumen disimpan/diajukan ──► IApprovalService.SubmitAsync
                                 ├─ cari alur: cabang dokumen → default; tidak ada → error (M-31)
                                 └─ ApprovalRequest(Pending, level 1, snapshot) + dokumen → PendingApproval
Approver level n ──► Approve ──► level n terakhir? ──tidak──► CurrentLevel = n+1 (menunggu approver berikutnya)
                                        └──ya──► Approved ──► IApprovalHandler<T>.OnApprovedAsync (transaksi sama)
Approver level n ──► Reject (alasan) ──► Rejected ──► IApprovalHandler<T>.OnRejectedAsync
Pengaju ──► Cancel (selama Pending) ──► Cancelled ──► OnCancelledAsync
```
- Validasi keputusan (M-33): status Pending; user ada di `ApproverUserIds` level berjalan; user ≠ pengaju; user belum memutuskan level lain pada pengajuan ini; user punya akses cabang dokumen. Concurrency `xmin` → dua approver level yang sama bersamaan: yang kedua dapat `409`.
- **Efek akhir sinkron** dalam transaksi yang sama dengan keputusan level terakhir (bukan lewat outbox). Bila efek gagal (mis. stok tidak cukup saat menjalankan mutasi pakan/revisi), keputusan **dibatalkan**, pengajuan tetap Pending di level terakhir, approver mendapat pesan error.
- Domain event `ApprovalRequestSubmitted/LevelApproved/Approved/Rejected/Cancelled` disimpan ke outbox untuk notifikasi masa depan (push FCM = backlog).
- Admin dapat **mengganti approver** level yang belum diputuskan pada pengajuan berjalan (M-32), tercatat di audit log.

### 5.3 Efek per jenis dokumen (`IApprovalHandler`)
| Jenis | Status dokumen | Sebelum Approved | Saat Approved | Saat Rejected | Dampak modul lain |
|---|---|---|---|---|---|
| **Farmer** | `PendingApproval` → `Active` / `Rejected` (+ `Inactive` lama) | Tidak bisa dipakai di kandang/kontrak. | Aktif. | Bisa diedit & diajukan ulang. | WebApp Farmers: badge status, timeline, Submit. Create dari WebApp juga lewat approval. |
| **FarmerChange** | Farmer tetap `Active`; usulan NIK/rekening disimpan di `PendingChange` | Data lama tetap dipakai (pembayaran plasma). | Usulan diterapkan. | Usulan dibuang. | Detail farmer menampilkan perubahan tertunda. |
| **Kandang (Coop)** | `PendingApproval` → `Active` / `Rejected` | Tidak bisa direncanakan siklus. | Aktif + **gudang kandang `GK-` dibuat** (event pindah dari create ke approve). | Bisa diedit & diajukan ulang. | Hanya bisa diajukan bila farmer Active atau Pending (approve kandang menunggu farmer Active). |
| **Kontrak** | `Draft` → `PendingApproval` → `Active` / `Rejected` (→ bisa diedit lalu submit lagi) | Tidak bisa dipakai siklus. | Aktif (pengganti `activate`). | Kembali bisa diedit. | WebApp Contracts: Activate → Submit + timeline. |
| **Revisi recording** | Entity `DailyRecordingRevisionRequest` (data usulan + lampiran) | Nilai & stok **tidak berubah**; satu pengajuan Pending per recording. | Logika revisi yang ada dijalankan (riwayat nilai lama, koreksi stok). | Usulan dibuang. | WebApp Daily Recordings: Revise → ajukan + tab "Revision requests". |
| **Mutasi pakan** | Entity `FeedMutationRequest` (kandang asal, tujuan, item, qty, alasan) | Stok **tidak bergerak** (tidak di-*reserve*). | `CreateFeedMutationCommand` yang ada dijalankan (retur A → induk → transfer B, jurnal sama); dokumen hasil ditautkan. | Tidak ada efek. | WebApp Feed Mutations: create → pengajuan + kolom status. |

Data lama dimigrasi sebagai Active (tidak ada yang tiba-tiba Pending). **Stok ayam harian** dan **Request Pakan** tidak memerlukan approval.

### 5.4 Application & Web.App
- **Application** (`Application/Approvals`): `IApprovalService` (Submit, dipanggil handler dokumen), `ApproveRequestCommand`, `RejectRequestCommand`, `CancelRequestCommand`, `ReassignApproverCommand`, `GetMyApprovalInboxQuery` (Pending di mana saya approver level berjalan + akses cabang), `GetMyApprovalInboxCountQuery`, `GetMySubmissionsQuery`, `GetApprovalRequestQuery`, `GetDocumentApprovalsQuery`; CRUD `ApprovalFlow` + `GetApprovalFlowsQuery`; `IApprovalHandler<T>` per jenis.
- **Web.App**:
  - Administration → **Approval Flows** (list per jenis & cabang; form level dengan nama + pemilih user approver; duplikasi alur default ke cabang).
  - Menu **Approval Inbox** (sama dengan mobile: tab per jenis, detail + timeline, Approve/Reject) — aksi lewat `IWorkflowActionService` + `[WorkflowAction]` (PLAN-WEBAPP §4.8).
  - Partial `_ApprovalTimeline` di detail Farmer, Coop, Contract, Daily Recording, Feed Mutation.
  - Di WebApp, hak menyetujui **tidak** lagi dari `CanEdit` untuk jenis M-5: harus terdaftar sebagai approver level berjalan.

---

## 6. Layar Mobile

### 6.1 Navigasi (`menubar-footer` template)
- **PPL**: Beranda · Kandang · **Input** (recording / stok ayam / request pakan / mutasi pakan) · Antrean · Lainnya.
- **Manager**: Beranda · Kandang · **Approval** (badge) · Stok Ayam · Lainnya.
- "Lainnya": Peternak, Kontrak, Grafik, Request Pakan (PPL: milik sendiri; Manager: cabang), Pengajuan Saya, Profil, Pengaturan, Logout.
- Header: judul, indikator **offline**, badge antrean, nama cabang.

### 6.2 Daftar layar
| Layar | PPL | Manager | Isi |
|---|:-:|:-:|---|
| **Login** | ✓ | ✓ | Email, password, tampilkan password, versi app & server. |
| **Dashboard** | ✓ | ✓ | **PPL**: kandang saya (umur, populasi, deplesi %, FCR, BW vs standar), **recording & stok ayam hari ini/kemarin belum diisi** (sorot merah), stok pakan + estimasi sisa hari (peringatan < 3 hari), request pakan berjalan, antrean sinkron, **pengajuan saya** (Pending di level berapa / Rejected + alasan). **Manager**: KPI cabang (siklus aktif, populasi, rata-rata deplesi/FCR/IP), **approval menunggu saya** per jenis, siklus berisiko (di bawah standar, recording terlambat > 1 hari), recording hari ini x/y kandang, stok ayam per rentang (ekor & ton), request pakan terbuka (yang lewat tanggal kirim disorot). |
| **Peternak — list/detail** | ✓ | ✓ | PPL: peternak tanggung jawabnya; Manager: satu cabang + filter PPL. Cari nama/kode/NIK, filter tipe & status; detail identitas (NIK dimasker), kontak (telepon/WhatsApp), rekening (+ perubahan tertunda), kandang, kontrak, lampiran, **timeline approval**. |
| **Peternak — add/edit** | ✓ | – | Kode, nama, tipe, cabang, NIK, NPWP, alamat, telepon, rekening, foto KTP. Simpan → diajukan; perubahan NIK/rekening → pengajuan perubahan. |
| **Kandang — list/detail** | ✓ | ✓ | PPL: kandang tanggung jawabnya; Manager: satu cabang + filter PPL; detail info & profil, lokasi, PPL, siklus berjalan & riwayat, stok gudang kandang, stok ayam terakhir, lampiran, timeline approval. |
| **Kandang — add/edit** | ✓ | – | Peternak, kode, nama, kapasitas, tipe, PPL, alamat, GPS, profil, foto. Simpan → diajukan. |
| **Kontrak — list/detail** | ✓ | ✓ | Status, skema, periode, harga sapronak, harga jaminan per BW, insentif/potongan, timeline approval. |
| **Kontrak — add/edit/ajukan** | ✓ | – | Wizard (`ui-stepper`): Umum → Skema → Harga sapronak → Harga jaminan → Insentif → Lampiran → Ringkasan → **Ajukan**. |
| **Recording — list/detail** | ✓ | ✓ | Per siklus: tanggal, umur, mati, culling, pakan, BW, status (Terkirim / Belum terkirim / Gagal / Revisi menunggu level n). Detail + riwayat revisi. |
| **Recording — input** | ✓ | – | Kandang/siklus, tanggal, mati, culling, BW (kalkulator sampel timbang), pemakaian pakan/OVK + stok, catatan, foto. **Offline.** |
| **Recording — ajukan revisi** | ✓ | – | Nilai baru + alasan wajib + lampiran. Online. |
| **Stok ayam — input** | ✓ | – | Kandang/siklus, tanggal, umur & populasi, rentang bobot, ekoran + tonase → rata-rata & validasi; daftar entri hari itu + total + sisa populasi; "Salin dari kemarin". **Offline.** |
| **Stok ayam — list / rekap** | ✓ | ✓ | List per siklus (entri per rentang + total per tanggal). Manager: rekap cabang per tanggal (rentang × ekor/ton/jumlah kandang) → drill-down kandang. |
| **Request pakan — ajukan/list/detail** | ✓ | ✓ | PPL ajukan (stok, rata-rata pakai, estimasi sisa hari), batal sebelum dikirim. Detail: diminta vs terkirim per baris, transfer pemenuhan, alasan tutup. Manager read-only. |
| **Mutasi pakan — ajukan/list** | ✓ | – | Kandang A → B (via induk): item, qty, alasan; status & level approval. Online. |
| **Grafik produksi** | ✓ | ✓ | Per siklus vs umur: BW, deplesi %, FCR, konsumsi pakan harian & kumulatif, mati harian, IP; garis **standar breed** + deviasi; Manager bandingkan ≤ 3 siklus; data lokal belum terkirim ditampilkan putus-putus. |
| **Approval — inbox** | – | ✓ | Tab per jenis + jumlah; item: jenis, nomor/nama, cabang, pengaju, **level n dari N**, waktu menunggu. |
| **Approval — detail** | – | ✓ | Ringkasan dokumen; nilai lama vs baru (revisi, perubahan farmer); stok asal/tujuan (mutasi); **timeline level** (siapa sudah menyetujui); **Setujui** (catatan opsional) / **Tolak** (alasan wajib). |
| **Pengajuan saya** | ✓ | ✓ | Daftar pengajuan + status & level berjalan; batal selama Pending. |
| **Antrean sinkron** | ✓ | – | Item Pending/Gagal + pesan, kirim ulang, edit, hapus. |
| **Profil & pengaturan** | ✓ | ✓ | Ganti password, cabang, alamat server (Debug), hapus cache, versi. |

---

## 7. Cara Kerja, Pengujian & Verifikasi

### 7.1 Alur per fase
Backend (domain + unit test → command/query + validator → EF config + migration → endpoint + permission → integration test) → layar WebApp bila ada → MobileApp.Core + unit test → layar mobile → verifikasi. **User yang commit & migrate.** Migration baru diberi nama `PhaseM{n}_…`.

### 7.2 Test
- **Domain**: `ApprovalRequest` — level berurutan, semua level → Approved, reject di level mana pun → Rejected, approver bukan anggota level → ditolak, pengaju tidak bisa memutuskan, satu user tidak bisa memutuskan dua level, cancel hanya Pending, snapshot tidak berubah saat alur diubah; `ApprovalFlow` — level 1..5, approver ≥ 1, unik per jenis+cabang. Status Farmer/Coop/Contract, `FeedDeliveryRequest`, `LiveBirdStockEntry`, `WeightRange`.
- **Application**: `IFieldScope` — PPL hanya melihat peternak (ditugaskan langsung atau lewat kandang) & kandang yang ditugaskan, detail di luar lingkup → 404, create otomatis ditugaskan ke pembuat, kandang untuk peternak di luar lingkup ditolak, Administrator tidak dibatasi, pemindahan penugasan langsung berlaku; pemilihan alur (cabang → default → error), efek akhir per jenis dalam transaksi (gagal → keputusan dibatalkan), inbox hanya level berjalan & cabang yang boleh, reassign approver.
- **Web.Api integration** (Testcontainers): `users/me`, logout, refresh, field-context, dashboard, stok ayam (idempotent, upsert per rentang), alur 2 level end-to-end (approve L1 → L2 → Active; reject di L2 → Rejected), request pakan → transfer, Manager read-only untuk stok & request pakan, PPL A tidak bisa membaca data PPL B (list kosong, detail 404), Manager melihat semua data cabangnya.
- **Web.App integration**: menu baru terbuka (`Admin_Should_OpenEveryReleasedMenu`), Approval Flows CRUD, approve dari WebApp hanya untuk approver level berjalan, Contracts/Daily Recordings/Feed Mutations memakai pengajuan.
- **Arsitektur**: `MobileApp.Core` tidak mereferensikan Domain/Application/Infrastructure; endpoint baru punya `HasPermission`.
- **`tests/MobileApp.UnitTests`**: `AuthHandler`, `SyncEngine` (urutan lampiran → recording → stok ayam, retry, klasifikasi error, idempotensi), ProblemDetails & `ErrorMessages`, format id-ID, validasi form, estimasi sisa hari pakan.

### 7.3 Verifikasi end-to-end
- API + Web.App pada database sementara `intiplasma_verify` + data demo; user `ppl.bdg`, `ppl2.bdg`, `manager.bdg` (level 1) dan `kacab.bdg` (level 2) dibuat saat verifikasi; alur 2 level untuk semua jenis.
- **Android Emulator** (API 34): login per peran; `ppl.bdg` & `ppl2.bdg` hanya melihat peternak/kandang masing-masing, `manager.bdg` melihat keduanya; pindahkan kandang dari `ppl.bdg` ke `ppl2.bdg` di WebApp → tampil di `ppl2.bdg` setelah segarkan; input recording & stok ayam online; **mode pesawat** → 2 hari recording + stok ayam (2 rentang, lalu ubah satu) + foto → online → terkirim tanpa dobel (cek DB); konflik → Gagal; PPL ajukan farmer + kandang + kontrak → `manager.bdg` setujui (level 1) → `kacab.bdg` setujui (level 2) → Active & gudang kandang terbentuk; reject di level 2 → Rejected + alasan tampil di PPL → perbaiki → ajukan ulang mulai level 1; `manager.bdg` mencoba memutuskan level 2 → ditolak; mutasi pakan disetujui → stok berpindah, ditolak → stok tetap; request pakan → transfer sebagian & penuh di WebApp → status di mobile PPL & Manager; rekap stok ayam cocok dengan WebApp Production → Live Bird Stock.
- Uji di **satu perangkat fisik** Android kelas menengah sebelum rilis.

### 7.4 Konvensi kode (mobile)
- Ikut `Directory.Build.props` (analyzer `All`, warning = error); pengecualian untuk file generated MAUI/Razor di `.editorconfig` project.
- Komponen Razor tipis; logika di Core. Satu file per halaman; komponen bersama di `Components/Shared`. Markup mengikuti kelas CSS template (§3.9).
- Teks UI Bahasa Indonesia langsung di komponen. Versi paket di `Directory.Packages.props`.

---

## 8. Tahapan Implementasi

### Fase M0 — Fondasi ✅ (selesai 2026-10-08, realisasi §12)
- **Project**: perbaiki `MobileApp.csproj` (versi paket terpusat, `TargetFramework` vs `TargetFrameworks`, nama & `ApplicationId`, ikon/splash dari logo `rdi-small` dengan latar `#0984E3`, buang halaman contoh); `src/MobileApp.Core` + `tests/MobileApp.UnitTests` masuk `IntiPlasma.slnx`.
- **UI dasar**: salin aset template terpilih (§3.9), Inter self-host, `app.css` (primary `#0984E3`), `MainLayout` (header + `menubar-footer` per permission), komponen Modal/Sheet/Badge/Spinner.
- **Backend**: `GET users/me`, `POST users/logout`, `POST users/me/change-password`, `Jwt:RefreshTokenExpirationInDays`, permission baru (§4.2) + seeder role **PPL** & **Manager**.
- **Klien**: `ApiClient` + `AuthHandler` + `SessionService` + `SecureStorage`, SQLite `LocalDb`, `ErrorMessages`, format id-ID, Login, Profil (ganti password), Pengaturan, banner offline.

### Fase M1 — Data Kemitraan (lihat) & Penugasan PPL ✅ (selesai 2026-10-09, realisasi §14)
- **Backend**: `Coop.FieldOfficerUserId` & `Farmer.FieldOfficerUserId` (migration `PhaseM1_FieldOfficerScope`), `IFieldScope` diterapkan di query & command farmer, coop, cycle, recording, performance (§4.5), `GET users/field-officers`.
- **Web.App**: field & filter PPL di Farmers/Coops, menu **Partnership → Field Officer Assignment**.
- **Klien**: list & detail **Peternak**, **Kandang**, **Kontrak** (+ siklus, stok kandang, lampiran), cache read-only, komponen List/Detail/Filter; filter PPL untuk Manager.

### Fase M2 — Recording Harian (offline) ✅ (selesai 2026-10-09, task §15, realisasi §16)
- **Backend**: `GET mobile/field-context`.
- **Klien**: list & detail recording, form input, foto, `sync_queue` + `local_entries` + `SyncEngine`, Antrean Sinkron.

### Fase M3 — Stok Ayam Harian (offline)
- **Backend**: `WeightRange` + `LiveBirdStockEntry` (migration `PhaseM3_LiveBirdStock`), use case & endpoint §4.4.
- **Web.App**: Master Data → **Weight Ranges**; **Production → Live Bird Stock** (rekap + ekspor).
- **Klien**: input per rentang (offline), list per siklus, "Salin dari kemarin"; rekap Manager.

### Fase M4 — Grafik & Dashboard
- **Backend**: `GetMobileDashboardQuery` (ambang dari `Mobile:Dashboard`), `GET mobile/dashboard`.
- **Klien**: Dashboard PPL & Manager, Grafik produksi (Chart.js), perbandingan siklus.

### Fase M5 — Breed Standard
- **Backend**: `BreedStandard` (strain + baris per umur: BW gram, FCR, deplesi %, pakan kumulatif g/ekor), strain di siklus (migration `PhaseM5_BreedStandard`); `performance` menambah `standard[]` & deviasi; dashboard memakai standar.
- **Web.App**: Production → **Breed Standards** (CRUD + impor CSV), strain di form siklus, garis standar di tab Performance.
- **Klien**: garis standar & deviasi di grafik, indikator di/bawah standar.

### Fase M6 — Modul Approval Berjenjang
- **Backend**: schema `approval`, `ApprovalFlow`, `ApprovalRequest`, `IApprovalService`, `IApprovalHandler` untuk **Farmer, FarmerChange, Coop, Contract**; status `PendingApproval/Rejected` + `PendingChange` farmer; gudang kandang dibuat saat approve; `contracts/{id}/activate` → submit (migration `PhaseM6_ApprovalWorkflow`, data lama → Active); endpoint `approvals/*` & `submit`.
- **Web.App**: Administration → **Approval Flows**; menu **Approval Inbox**; `_ApprovalTimeline`; Farmers/Coops/Contracts memakai pengajuan; DemoDataSeeder membuat alur 2 level & memakai submit/approve.
- **Klien**: Inbox Approval + detail + Setujui/Tolak, Pengajuan saya, timeline di detail Peternak/Kandang/Kontrak, badge.

### Fase M7 — Input Peternak, Kandang & Kontrak (PPL)
- **Klien**: form add/edit Peternak (perubahan NIK/rekening → pengajuan), Kandang (GPS, foto, PPL), wizard Kontrak + Ajukan, ajukan ulang setelah Rejected, halaman sukses.

### Fase M8 — Revisi Recording & Mutasi Pakan via Approval
- **Backend**: `DailyRecordingRevisionRequest` & `FeedMutationRequest` + handler approval (efek di transaksi yang sama; stok kurang → keputusan dibatalkan) (migration `PhaseM8_RevisionFeedMutationRequests`); `PUT daily-recordings/{id}` & create mutasi langsung dihapus.
- **Web.App**: Daily Recordings (Revise → ajukan, tab Revision requests), Feed Mutations (create → pengajuan, status), DemoDataSeeder.
- **Klien**: ajukan revisi recording, ajukan mutasi pakan + list; dua jenis baru di inbox Manager.

### Fase M9 — Request Pengiriman Pakan
- **Backend**: `FeedDeliveryRequest` + `StockTransfer.FeedDeliveryRequestId` (migration `PhaseM9_FeedDeliveryRequests`), use case & endpoint §4.3.
- **Web.App**: Inventory → **Feed Delivery Requests** (list, detail, Create Transfer dari permintaan, close sisa, ekspor), kolom request di Stock Transfers.
- **Klien**: PPL ajukan/list/detail/batal; Manager list/detail read-only; kartu di dashboard.

### Fase M10 — Pengerasan & Rilis Android
- Signing (keystore di luar repo), versioning, build Release AAB/APK (trimming + R8, `JsonSerializerContext` source-gen untuk DTO).
- Keamanan: HTTPS wajib di Release, tidak ada log token, cache dihapus saat logout, opsional `FLAG_SECURE`.
- Ketahanan: log error lokal; uji list 1.000 baris & grafik 45 hari.
- Panduan pengguna `docs/user-guide/mobile.md`, update `docs/DEPLOY.md` & RANGKUMAN.

---

## 9. Daftar Task M0 (urutan)
1. Pindahkan versi `Microsoft.Maui.Controls`, `Microsoft.AspNetCore.Components.WebView.Maui`, `Microsoft.Extensions.Logging.Debug`, `Microsoft.Data.Sqlite` ke `Directory.Packages.props`; `dotnet build -f net10.0-android` lulus dengan analyzer ketat.
2. Ganti identitas app; ikon & splash dari `rdi-small`/`rdi-small-white` (versi persegi + padding); hapus `Counter/Weather/dotnet_bot`.
3. Buat `src/MobileApp.Core` & `tests/MobileApp.UnitTests`; referensi MobileApp → Core.
4. Salin aset template (CSS, icomoon, logo) + Inter self-host; `app.css` override primary; buang aset yang tidak dipakai.
5. Backend: permission baru + seeder role PPL/Manager (+ test seeder idempotent).
6. Backend: `GET users/me`, `POST users/logout`, `POST users/me/change-password`, `Jwt:RefreshTokenExpirationInDays` + integration test.
7. Core: `ApiClient`, `ApiError`, `AuthHandler`, `SessionService`, `ISecureStore`, `ErrorMessages`, format id-ID + unit test.
8. Core: `LocalDb` SQLite + migrasi skema lokal (`PRAGMA user_version`).
9. MobileApp: DI di `MauiProgram`, implementasi platform, `MainLayout` + `menubar-footer` per permission, Login (`sign-in.html`), Profil, Pengaturan, banner offline.
10. Verifikasi di emulator: login admin, `ppl.bdg`, `manager.bdg` (menu berbeda), pesan error Indonesia, refresh token (`Jwt:ExpirationInMinutes=1`), mode pesawat → sesi tersimpan tetap terbuka.

---

## 10. Risiko & Catatan Teknis
- **Lingkup PPL & cache offline**: setelah penugasan dipindah, data lama masih ada di cache perangkat PPL lama sampai disegarkan; antrean offline untuk kandang yang sudah dipindah ditolak server (404) → tampil Gagal dengan pesan "Kandang sudah tidak menjadi tanggung jawab Anda". Data lama tanpa PPL tidak terlihat oleh PPL mana pun → lakukan penugasan massal setelah migrate M1.
- **Lisensi template** Findee (Themesflat): pastikan lisensi yang dibeli mencakup aplikasi ini; aset demo (foto, logo Findee) tidak ikut dirilis.
- **Breaking change** di M6/M8/M9 (`contracts/{id}/activate`, `PUT daily-recordings/{id}`, create mutasi langsung, body stock transfer) memengaruhi WebApp, DemoDataSeeder & skrip verifikasi lama → disesuaikan di fase yang sama.
- **Alur approval wajib dikonfigurasi** sebelum PPL bisa mengajukan (M-31): instalasi baru perlu langkah setup di `docs/DEPLOY.md`; user approver yang dinonaktifkan membuat pengajuan macet → layar reassign (M-32) + peringatan di Approval Flows bila ada approver nonaktif.
- Approver level kecil (satu orang) yang sedang cuti memblokir level itu → disarankan ≥ 2 user per level; delegasi/eskalasi otomatis = backlog.
- Efek akhir approval dijalankan sinkron: mutasi/revisi yang gagal karena stok berubah membuat approver terakhir melihat error; pengajuan tetap Pending sampai stok tersedia atau ditolak.
- Stok pakan di form offline bisa basi → hanya peringatan; server penentu.
- Stok ayam bergantung pada populasi server: recording dikirim sebelum stok ayam (§3.6). Rekap menandai data basi (> 1 hari).
- Rate limit 100/menit/user: sinkron antrean besar berurutan; `429` → retry.
- Idempotency server masih in-memory per instance; recording & stok ayam tetap aman karena `id` klien dicek di DB.
- Tanggal input = tanggal lokal perangkat (`Asia/Jakarta`); jam salah ditangkap validasi server.
- BlazorWebView tidak memanggil API dari JavaScript → **CORS tidak diperlukan**.
- CSS template besar (`styles.css`) + Bootstrap: ukur ukuran & waktu render pertama; buang aturan yang tidak dipakai bila perlu (M10).

## 11. Status Keputusan
- **Semua keputusan M-1 s.d. M-39 disepakati** 2026-10-08. Fase **M0 selesai** 2026-10-08 (§12); **M1 selesai** 2026-10-09 (§13 task, §14 realisasi); **M2 selesai** 2026-10-09 (§15 task, §16 realisasi; M-40 s.d. M-45 disepakati); berikutnya **M3**.
- 2026-10-09: M-38 direvisi — kontrak memakai lingkup cabang, bukan lingkup PPL.

---

## 12. Realisasi Fase M0 — Fondasi (2026-10-08)

### 12.1 Backend (Domain / Application / Infrastructure / Web.Api)
- **Permission baru** (`Domain/Roles/Permissions.cs`): `partnership:assigned-only`, `production:stock-report`, `inventory:request-feed`, `inventory:request-feed-mutation`, `approvals:decide`. Role Administrator otomatis ikut (sinkron seeder).
- **Role default** `PPL` (17 permission) & `Manager` (9 permission) dibuat seeder bila belum ada (`DatabaseSeeder.SeedMobileRolesAsync`, nama di `Domain/Roles/MobileRoles.cs`); tidak ditimpa bila admin mengubahnya.
- **`GET users/me`**: `GetCurrentUserQuery` kini juga mengembalikan `roles[]` & `permissions[]` (Web.App ikut mendapat field baru, tanpa perubahan perilaku).
- **`POST users/logout`** (`LogoutUserCommand`, mencabut satu refresh token milik user; token tak dikenal/milik user lain diabaikan) dan **`POST users/me/change-password`** (`ChangeOwnPasswordCommand` yang sudah ada; rate limit auth). Endpoint di `Web.Api/Endpoints/Users/CurrentUserEndpoints.cs`, cukup login.
- **Masa refresh token** dari konfigurasi `Jwt:RefreshTokenExpirationInDays` (default **30**, `RefreshTokenOptions`), menggantikan konstanta 7 hari di handler login & refresh. `appsettings*.json`, `.env.example`, `docs/DEPLOY.md` diperbarui.
- **Tidak ada migration** di M0.

### 12.2 MobileApp & MobileApp.Core
- `src/MobileApp.Core` (net10.0, tanpa MAUI): `ApiClient` (ProblemDetails → `ApiError` berbahasa Indonesia, error jaringan tidak melempar exception), `AuthHandler` (bearer + refresh sekali saat 401, body di-buffer agar bisa dikirim ulang), `TokenRefresher` (satu refresh pada satu waktu karena token berotasi; ditolak server → `SessionExpired`), `TokenStore` (SecureStorage), `SessionService` (login → `users/me` → cache profil di SQLite; buka aplikasi offline dengan sesi tersimpan; ganti user di perangkat menghapus data user sebelumnya; ganti password → login ulang otomatis), `LocalDb` (SQLite + migrasi `PRAGMA user_version`, v1: `session`, `cache_entries`), `ErrorMessages`, `IdFormat` (format id-ID tanpa bergantung locale perangkat), `AppFeatures` (menu dari permission).
- `src/MobileApp`: proyek dirapikan (versi paket terpusat, hanya Android + Windows untuk debug, `ApplicationId` `com.intiplasma.mobile`, nama "IntiPlasma"), ikon & splash dari `rdi-small.png` (latar putih), status bar `#0984E3`, `allowBackup=false`, cleartext hanya ke `10.0.2.2`/`localhost`.
- UI dari template Findee: `bootstrap.min.css` + `styles.css` + icomoon + Inter self-host, override di `wwwroot/css/app.css` (primary `#0984E3`, light mode, tombol ikon). Halaman: Login, Beranda (sapaan, cabang, peran, menu), Lainnya, Profil (ganti password), Pengaturan (versi, alamat server khusus debug, muat ulang profil, hapus cache), placeholder "Segera hadir" per fase, bottom bar per permission, banner offline, dialog konfirmasi keluar.
- Default alamat server: debug `http://10.0.2.2:5000/` (emulator) / `http://localhost:5000/` (Windows); **release masih placeholder** di `Services/MauiPlatformServices.cs` (`ServerDefaults`) — diisi di M10.

### 12.3 Pengujian & verifikasi
- Test: **467 lulus** — 146 domain, 71 application (+3 logout), 14 arsitektur, **46 MobileApp.UnitTests** (baru: API client, AuthHandler termasuk refresh paralel, SessionService, LocalDb, menu per peran, format, sinkronisasi katalog permission & kode error dengan Domain, Core tidak mereferensikan proyek server), 25 integration Web.Api (+6: `users/me`, logout, ganti password, role seeder), 165 integration Web.App.
- Verifikasi end-to-end di **emulator Android 14 (API 34)** terhadap Web.Api pada `intiplasma_verify` + data demo (dibuat & dihapus), `Jwt:ExpirationInMinutes=1`: login salah → "Email atau password salah."; login PPL → bottom bar Beranda · Kandang · Input · Antrean · Lainnya; Manager → Beranda · Kandang · Approval · Stok Ayam · Lainnya; user tanpa permission ditolak; ganti password (401 → refresh → retry 204, lalu login ulang otomatis, refresh token lama tercabut); muat ulang profil setelah access token kedaluwarsa (401 → refresh 200 → 200); restart aplikasi tetap masuk; **mode pesawat** → aplikasi tetap terbuka dengan banner offline; keluar → token dicabut di server (0 refresh token) dan email terisi di halaman login.

### 12.4 Catatan & penyesuaian
- **WebView minimum**: Blazor .NET 10 gagal di WebView bawaan emulator API 26 (`blazor.webview.js: Unexpected token .`). Perangkat Android 7+ butuh **Android System WebView/Chrome yang diperbarui** (Play Store); emulator verifikasi memakai API 34 (WebView 113). Perlu dicantumkan di panduan pengguna (M10).
- `Directory.Build.props` men-set `TargetFramework=net10.0` untuk semua proyek; `MobileApp.csproj` mengosongkannya agar restore multi-target menyertakan runtime Android (`NETSDK1047`).
- `SQLitePCLRaw.bundle_e_sqlite3` dipin ke **2.1.13** (advisory NU1903 pada 2.1.11, dependensi transitif Microsoft.Data.Sqlite 10.0.9).
- Target Windows: `NoWarn CA5392` khusus target itu (file dari paket Windows App SDK).
- Halaman MAUI memakai `SafeAreaEdges="All"`: WebView Android melaporkan `env(safe-area-inset-*)` = 0 sehingga header/bottom bar tertutup status bar & navigation bar.
- CSS template menata **semua `<button>`** sebagai blok biru selebar penuh dan `.input-icon .icon` di kiri; tombol ikon diberi kelas sendiri (`.password-toggle`, `.header-icon-btn`). Hover template membalik warna tombol primer (menempel di layar sentuh) → dinetralkan.
- Tombol profil di header Beranda dihapus (ikon tidak tampil di WebView; Profil tetap dari Lainnya).

---

## 13. Daftar Task M1 — Data Kemitraan (lihat) & Penugasan PPL (urutan)

Disusun 2026-10-09 dari pemetaan kode. Prasyarat: hasil M0 sudah di-commit.

### 13.1 Temuan yang memengaruhi desain
- **Kontrak tanpa `FarmerId`** → kontrak memakai lingkup cabang (M-38 direvisi).
- **Web.App ikut memanggil `AddInfrastructureCore`** (`src/Web.App/Program.cs`). Karena lingkup PPL tidak berlaku di WebApp (§4.5), Web.App mendaftarkan `IFieldScope` versi *unrestricted*.
- **Administrator mendapat semua permission**, termasuk `partnership:assigned-only` (`Permissions.All`), jadi `FieldScope` wajib mengecualikan role sistem Administrator (`r.is_system AND r.name = 'Administrator'`).
- **Belum ada antarmuka permission di Application**: `IUserContext` hanya berisi `UserId`, sedangkan `PermissionProvider` bersifat internal di Infrastructure. `FieldScope` dibuat di Infrastructure dengan memakai `PermissionProvider`.
- **Handler dibuat manual di unit test**: menambah parameter `IFieldScope` di konstruktor akan merusak test berikut, sehingga perlu helper `Unrestricted()`:
  - `PlanCycleCommandHandlerTests`
  - `ProductionHandlersTests`
  - test Costing, Inventory & Sales yang memakai `CycleLoader`
- **Route `users/{userId}` tanpa constraint `:guid`**: route literal `users/field-officers` tetap menang (sama seperti `users/me`).
- Belum ada query user per cabang. Pola SQL yang bisa dipakai ulang ada di `GetUserByIdQueryHandler` (cakupan profil Akses Cabang) dan `GetCurrentUserQueryHandler` (role → permission).
- `LocalDb.cache_entries` sudah ada (v1), tetapi belum punya metode get/set.

### 13.2 Task
1. **Domain**: `Farmer.FieldOfficerUserId` & `Coop.FieldOfficerUserId` (`Guid?`) + metode `AssignFieldOfficer(Guid?)` + unit test.
2. **EF**:
   - FK ke `identity.users` (Restrict) + index `field_officer_user_id` pada `master.farmers` & `master.coops`.
   - Migration **`PhaseM1_FieldOfficerScope`** (data lama dibiarkan kosong). **User yang menjalankan migrate.**
3. **`IFieldScope`** (Application/Abstractions/Authorization):
   - Anggota: `IsRestricted`, `UserId`, potongan SQL/parameter untuk Dapper, serta `EnsureFarmerInScopeAsync`, `EnsureCoopInScopeAsync` & `EnsureCycleInScopeAsync` (di luar lingkup → **404**).
   - Implementasi di Infrastructure (HybridCache, key baru di `PermissionCacheKeys` yang ikut di-*invalidate* oleh `AllForUser`).
   - Web.App memakai versi *unrestricted*.
4. **Terapkan lingkup** (kontrak tidak):
   - **Peternak**: list (langsung **atau** lewat kandang) & detail; update.
   - **Kandang**: list & detail; create (hanya untuk peternak dalam lingkup) & update.
   - **Siklus**: list & detail; `PlanCycle`; `CycleLoader` (start, cancel, create/revise recording, panen, tutup).
   - **Recording & performance**: list, detail & grafik, lewat `ProductionReadSupport.EnsureCycleAccessAsync`.
   - **Lampiran**: `AttachmentService`/`SetDocuments` untuk pemilik Farmer, Coop, Cycle & DailyRecording.
5. **Penugasan pada create/update** farmer & coop:
   - Field `fieldOfficerUserId?`: harus user aktif, punya `partnership:assigned-only`, dan punya akses ke cabang dokumen.
   - User restricted → otomatis diisi dirinya sendiri.
   - Response list/detail menambah `fieldOfficerUserId` & `fieldOfficerName`; filter `fieldOfficerId` di list farmers, coops & cycles.
6. **`GET users/field-officers?branchId`**: user aktif dengan `partnership:assigned-only` lewat role, bukan Administrator, dan profil aksesnya mencakup cabang tersebut. Dipakai untuk lookup WebApp & filter Manager.
7. **Test backend**:
   - Perbaiki konstruktor test lama.
   - Unit test: lingkup peternak langsung vs lewat kandang; detail di luar lingkup → 404; create oleh user restricted otomatis ditugaskan ke pembuatnya; kandang untuk peternak di luar lingkup ditolak; Administrator tidak dibatasi.
   - Integration test Web.Api: PPL A tidak melihat data PPL B (list kosong, detail 404); Manager melihat seluruh cabang; pemindahan penugasan langsung berlaku; `users/field-officers`.
8. **Web.App**:
   - Field PPL (lookup per cabang) di form Farmers/Coops, serta kolom & filter PPL di list (+ ekspor).
   - Menu **Partnership → Field Officer Assignment** (`MenuCatalog`, *released*): pilih PPL asal → centang peternak/kandang → pindahkan ke PPL tujuan. Dijalankan lewat `ReassignFieldOfficerCommand` (bulk, satu transaksi) dan tercatat di audit log (kategori audit baru bila perlu).
   - Integration test: menu terbuka & pemindahan penugasan.
9. **MobileApp.Core**:
   - `PagedList<T>` + DTO farmer, coop, contract, cycle & stock balance.
   - `FarmersApi`, `CoopsApi`, `ContractsApi`, `CyclesApi` & `InventoryApi` (saldo gudang kandang); `UsersApi.GetFieldOfficers`.
   - Get/set `cache_entries` di `LocalDb`: cache tampil dulu lalu diperbarui; saat offline tampil "Data per dd/MM HH:mm".
   - Unit test.
10. **MobileApp UI**:
    - Komponen `Shared`: ListPage/InfiniteList, SearchBox (debounce), FilterSheet, StatusBadge, Tabs, EmptyState.
    - Halaman list & detail **Peternak** (NIK dimasker, telepon/WhatsApp, kandang, lampiran), **Kandang** (profil, lokasi + buka peta, PPL, siklus berjalan & riwayat, stok gudang kandang, lampiran) dan **Kontrak** (skema, periode, harga sapronak, harga jaminan, insentif).
    - Arahkan rute `AppFeatures` (Peternak, Kandang, Kontrak) dari placeholder ke halaman baru.
    - Filter **PPL** untuk Manager.
11. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo):
    - `ppl.bdg` & `ppl2.bdg` hanya melihat datanya masing-masing; `manager.bdg` melihat keduanya.
    - Pindahkan kandang di Field Officer Assignment → muncul di `ppl2.bdg` setelah disegarkan.
    - Detail di luar lingkup → 404.
    - Mode pesawat → list & detail dari cache.
12. **Dokumentasi**: realisasi §14, RANGKUMAN, dan catatan "penugasan massal setelah migrate M1" di `docs/DEPLOY.md`.

### 13.3 Di luar lingkup M1
- Form add/edit peternak/kandang/kontrak di mobile → M7.
- `inventory/stock-balances` & modul inventory lain tetap memakai lingkup cabang; lingkup kandang untuk stok/request/mutasi pakan menyusul di M2/M8/M9.

---

## 14. Realisasi Fase M1 — Data Kemitraan (lihat) & Penugasan PPL (2026-10-09)

### 14.1 Backend
- **Domain**: `Farmer.FieldOfficerUserId`, `Coop.FieldOfficerUserId` + `AssignFieldOfficer(Guid?)`; error baru `Users.NotFieldOfficer`.
- **Migration `PhaseM1_FieldOfficerScope`**: kolom `field_officer_user_id` (nullable, ber-index) di `master.farmers` & `master.coops`, FK ke `identity.users` **ON DELETE SET NULL** (user bisa dihapus permanen, W-22; penugasannya menjadi kosong). Data lama tetap kosong. **Belum di-migrate.**
- **`IFieldScope`** (`Application/Abstractions/Authorization`): `GetScopeAsync`, `CanAccessFarmer/Coop/CycleAsync`, `FieldScopeSql` (potongan SQL Dapper), `FieldScopeExtensions` (di luar lingkup → `*.NotFound`). Implementasi `FieldScopeProvider` (Infrastructure): restricted = punya `partnership:assigned-only` **dan bukan** role sistem Administrator; flag di-cache (`PermissionCacheKeys.FieldScopeForUser`, ikut di-*invalidate* bersama akses user). **Web.App** memakai `UnrestrictedFieldScope`.
- **Lingkup diterapkan di**:
  - peternak: list, detail & update (ditugaskan langsung atau lewat kandang);
  - kandang: list, detail, create (hanya untuk peternak dalam lingkup) & update;
  - siklus: list, detail, `PlanCycle`, `CycleLoader` (start, cancel, create/revise recording), panen & tutup siklus;
  - recording & grafik performance: list & detail;
  - lampiran: baca/daftar di `AttachmentService` + `PUT …/documents` untuk Farmer, Coop, Cycle, Harvest, DailyRecording & revisinya.
  - **Kontrak** tetap memakai lingkup cabang (M-38 direvisi).
- **Penugasan**: `fieldOfficerUserId` di create/update farmer & coop (API: body POST dan `UpdateRequest`). User restricted otomatis menugaskan dirinya dan tidak bisa memindahkan. Validasi `FieldOfficerRules`:
  - user aktif;
  - punya `partnership:assigned-only` lewat role;
  - bukan Administrator;
  - profil Akses Cabang mencakup cabang data.
  - ⚠️ Untuk user tidak terbatas, `PUT` mengganti penugasan (null = kosong).
- **Endpoint**:
  - `GET users/field-officers?branchId` (`farmers:read`).
  - Filter `fieldOfficerId` di `GET farmers` (langsung atau lewat kandang), `coops` dan `cycles`.
  - Response farmer & coop ditambah `fieldOfficerUserId` dan `fieldOfficerName`; response coop ditambah `warehouseId` (gudang kandang).
- **Penugasan massal**: `GetFieldOfficerAssignmentsQuery` & `ReassignFieldOfficerCommand` (hanya memindahkan data yang masih milik PPL asal; dicatat di audit log kategori Access, aksi `ReassignFieldOfficer`).

### 14.2 Web.App
- **Farmers & Farms**: field **Field officer (PPL)** (Tom-Select `/Lookup/FieldOfficers`, bergantung cabang/peternak), kolom & filter **PPL** di list, ikut ekspor.
- **Menu baru Partnership → Field Officer Assignment** (`partnership.field-officers`, hak Edit): pilih cabang & PPL asal (atau *Unassigned*), centang peternak/kandang, lalu pindahkan ke PPL lain atau kosongkan.
- **`app.js`**: perilaku umum `data-check-all` dan `select[data-submit-on-change]` (CSP tidak mengizinkan handler inline).

### 14.3 MobileApp.Core & MobileApp
- **Core**:
  - DTO kemitraan: `PagedList<T>`, `Farmer`, `Coop`, `Contract`, `Cycle`, `StockBalance`, `Attachment`, `FieldOfficer`.
  - `PartnershipApi`.
  - **`ApiCache`**: GET yang sukses disimpan di `cache_entries`; saat offline dipakai salinan terakhir + umur datanya; 403/404 menghapus salinan; hasil pencarian tidak di-cache.
  - `PagedLoader<T>`.
  - `Labels` (label Indonesia, masker NIK, nomor telepon/WhatsApp) dan `ExternalLinks`.
  - `ApiClient.GetBytesAsync` dan `CurrentUser.IsLimitedToAssignedData`.
  - Pesan error `*.NotFound` = "… tidak ditemukan atau bukan tanggung jawab Anda".
- **Layar**:
  - **Peternak**: list + detail (data, NIK dimasker, rekening, telepon/WhatsApp, kandang, lampiran).
  - **Kandang**: list + detail (info & buka peta, siklus, stok gudang kandang, lampiran).
  - **Kontrak**: list + filter status; detail berisi umum, harga sapronak & jaminan, insentif, lampiran.
- **Komponen**: `SearchBox`, `LoaderList`, `CacheNote`, `StatusBadge`, `Tabs`, `ErrorState`, `Spinner`, `CoopCard`, `AttachmentList` (pratinjau foto), `FieldOfficerFilter` (Manager), dan `IExternalLauncher` (MAUI `Launcher`).
- **`NetworkErrorHandler`**: di Android, `AndroidMessageHandler` melempar `Java.IO.IOException` saat offline. Handler ini mengubahnya menjadi `HttpRequestException` agar Core melaporkan offline dan memakai cache; sebelumnya halaman menampilkan error.

### 14.4 Pengujian & verifikasi
- **Test: 507 lulus**:

  | Proyek test | Jumlah |
  |---|---|
  | Domain | 147 (+1) |
  | Application | 81 (+10 `FieldOfficerScopeTests`) |
  | Arsitektur | 14 |
  | MobileApp.UnitTests | 68 (+22: `PartnershipApi`/cache, `PagedLoader`, `Labels`, `ExternalLinks`; `CatalogSyncTests` kini juga membaca error factory) |
  | Integration Web.Api | 30 (+5 lingkup PPL) |
  | Integration Web.App | 167 (+2 penugasan & form) |

- **API** pada `intiplasma_verify` + data demo (user `ppl.bdg`, `ppl2.bdg`, `manager.bdg`; 5 kandang BDG dibagi):
  - PPL A melihat 3 kandang, PPL B 2, Manager 5;
  - detail kandang, siklus, recording & grafik milik PPL lain → 404;
  - peternak yang kandangnya dipegang dua PPL terlihat oleh keduanya;
  - `users/field-officers` hanya mengembalikan 2 PPL.
- **Emulator API 34**:
  - PPL melihat kandang/peternak miliknya; tab Kandang di detail peternak hanya berisi kandangnya; siklus & stok gudang kandang tampil;
  - kandang yang dipindah lewat API langsung muncul tanpa login ulang;
  - **mode pesawat** → list & detail tampil dari cache dengan catatan "Offline — data per …";
  - Manager melihat 5 kandang + filter PPL; kontrak & harga tampil.

### 14.5 Catatan
- Build Android inkremental bisa menghasilkan ID resource basi sehingga aplikasi crash saat start (`No view found for id … jumpToStart`). Solusi: hapus `src/MobileApp/obj` & `bin`, lalu build ulang.
- `CoopCount` di list peternak menghitung semua kandang peternak (termasuk milik PPL lain); detail hanya menampilkan kandang dalam lingkup.
- Logo header Beranda tampak gepeng di emulator (sudah ada sejak M0) → dirapikan di M4 (dashboard).
- Setelah migrate M1 semua data belum ditugaskan: PPL tidak melihat apa pun sampai admin memakai **Field Officer Assignment** (*Unassigned* → PPL).

---

## 15. Daftar Task M2 — Recording Harian (offline) (urutan)

Disusun 2026-10-09 dari pemetaan kode. Prasyarat: hasil M1 sudah di-commit (`44959f4`). **Tidak ada migration** di M2 (hanya skema SQLite lokal v2).

### 15.1 Temuan yang memengaruhi desain
- **`POST production/daily-recordings` sudah siap offline**:
  - `id` dari klien → kirim ulang `id` yang sama mengembalikan id itu (idempotent di DB); `id` sama untuk siklus/tanggal lain → `409 DailyRecordings.IdBelongsToOtherRecording`.
  - Tanggal yang sudah ada → `409 DailyRecordings.AlreadyRecorded`; maks +1 hari dari tanggal server (`FutureDate`).
  - Siklus harus `Active`/`Harvesting` (`Cycles.NotRecordable`) dan ≥ chick-in (`Cycles.BeforeChickIn`); mati + culling ≤ populasi (`Cycles.PopulationExceeded`).
  - Lingkup PPL sudah ditegakkan lewat `CycleLoader` (M1) → kandang yang sudah dipindah = `404 Cycles.NotFound`.
- **Stok gudang kandang di server memblokir** (`StockBalance.Issue` → `Stock.Insufficient`), sedangkan di klien hanya peringatan (§3.6). Recording offline dengan stok cache basi akan berakhir **Gagal** saat sinkron → pesan Indonesia + bisa diedit.
- **`POST attachments` idempotent** lewat `id` form (`AttachmentService.SaveAsync` mengembalikan lampiran yang ada). Lampiran wajib terunggah **sebelum** recording yang mereferensikannya.
- **`GET items` tidak mengembalikan konversi satuan** (hanya detail) → konversi wajib dibundel di `field-context`; tanpa itu klien butuh satu request per item.
- **`GET inventory/stock-balances` hanya memakai lingkup cabang** dan mengembalikan **nilai & HPP rata-rata** → field-context hanya mengirim kuantitas; endpoint stock-balances perlu lingkup PPL (ditunda dari M1, §13.3).
- `ApiClient` (Core) **belum** mendukung header `Idempotency-Key` maupun upload multipart; belum ada `IClock` & `IPhotoPicker`; manifest Android belum punya izin `CAMERA`.
- `LocalDb` baru v1 (`session`, `cache_entries`); `ClearUserDataAsync` menghapus semuanya → perlu aturan untuk antrean (antrean user lain ditahan, §3.3).
- `ErrorMessages` belum memetakan kode recording/stok/siklus di atas; `CatalogSyncTests` otomatis memastikan kode yang dipetakan ada di Domain.
- `CoopDetail` sudah punya tab **Siklus** → titik masuk list recording per siklus.

### 15.2 Keputusan tambahan (disepakati 2026-10-09)
| # | Topik | Usulan |
|---|---|---|
| M-40 | Isi `field-context` | Siklus `Active`/`Harvesting` dalam lingkup (kandang, peternak, gudang kandang, chick-in, populasi awal & berjalan, umur, **tanggal recording 14 hari terakhir**), item **Feed/OVK aktif** + konversi satuan, **kuantitas** stok gudang kandang (tanpa nilai/HPP), `serverDate`. Permission `production:read` (M3 menambah rentang bobot tanpa mengganti endpoint). |
| M-41 | Lingkup `inventory/stock-balances` | User lingkup PPL hanya melihat **gudang kandang dalam lingkupnya** (gudang induk/lain tidak). User lain tetap memakai lingkup cabang. |
| M-42 | Urutan & kegagalan sinkron | FIFO per siklus berdasarkan tanggal; item **Gagal tidak memblokir** item lain (server tidak mewajibkan tanggal berurutan). Lampiran gagal → recording-nya ikut ditahan. |
| M-43 | Edit/hapus sebelum terkirim | Item `Pending`/`Gagal` bisa **diedit** (id tetap) atau **dihapus** dari Antrean; item `Sending` terkunci. Setelah `Done`, perubahan hanya lewat revisi (M8). |
| M-44 | Foto | Maks **5 foto** per recording (batas server 20), dari kamera atau galeri, dikompres (sisi terpanjang 1600 px, JPEG 80%) dengan `Microsoft.Maui.Graphics` (tanpa paket baru). File disimpan di `AppDataDirectory/pending/` dan dihapus setelah terkirim. |
| M-45 | Recording untuk Manager | Manager: list & detail recording **read-only** dari tab Siklus kandang (M-2). Input hanya untuk `production:record`. |

### 15.3 Task
1. **Backend — `GET mobile/field-context`**:
   - `Application/Mobile/GetFieldContextQuery` (Dapper; lingkup cabang + `FieldScopeSql` untuk siklus/kandang); respons sesuai M-40.
   - Endpoint `Web.Api/Endpoints/Mobile/MobileEndpoints.cs` (`Tags.Mobile`, `HasPermission(production:read)`).
2. **Backend — lingkup `stock-balances`** (M-41): `GetStockBalancesQueryHandler` menerapkan `IFieldScope` (hanya gudang kandang dalam lingkup) bila user restricted.
3. **Test backend** — integration Web.Api:
   - field-context PPL A hanya berisi siklus PPL A, tanpa kolom nilai;
   - recording dikirim dua kali dengan `id` sama → satu baris; `id` sama untuk tanggal lain → 409;
   - lampiran diunggah ulang dengan `id` sama → satu lampiran;
   - recording untuk siklus PPL lain → 404;
   - stock-balances PPL hanya berisi gudang kandangnya.
4. **MobileApp.Core — infrastruktur**:
   - `ApiClient`: header `Idempotency-Key` pada POST, `PostMultipartAsync` (file + `id`).
   - Abstraksi `IClock` (tanggal lokal `Asia/Jakarta`), `IPhotoPicker`, `IImageCompressor`, `IFileStore` (folder pending).
   - `LocalDb` **v2**: tabel `field_context` (json + `fetched_at`), `sync_queue` (`id`, `user_id`, `kind`, `cycle_id`, `date`, payload, path file, status, `attempts`, `next_attempt_at`, `last_error_code/message`, `created_at`) dan `local_entries` (tampilan "Belum terkirim").
   - `ClearUserDataAsync` tidak menghapus antrean user lain (ditahan + peringatan, §3.3); logout dengan antrean belum terkirim → konfirmasi.
5. **MobileApp.Core — recording**:
   - DTO `FieldContext`, `DailyRecording` (+ usage, revisi), `CreateDailyRecordingRequest`.
   - `ProductionApi`: field-context (disimpan ke `field_context`), list/detail recording (via `ApiCache`), create, upload lampiran.
   - `RecordingDraft` + validator:
     - tanggal ≤ hari ini & ≥ chick-in, belum ada di server maupun lokal;
     - mati + culling ≤ populasi berjalan dikurangi entri lokal;
     - pemakaian > stok = **peringatan**, bukan blokir.
     - Stok & populasi di form sudah dikurangi entri lokal yang belum terkirim.
   - `SyncQueue` (enqueue, edit, hapus, daftar, jumlah per status).
   - `SyncEngine`:
     - satu proses pada satu waktu; lampiran dulu, lalu recording;
     - klasifikasi: `2xx` → Done; jaringan/`5xx`/`429` → Pending + backoff 30 dtk → 5 mnt; `400/404/409` → Gagal; `401` → tahan sampai login;
     - setelah Done, segarkan field-context & cache recording siklus itu;
     - event jumlah antrean untuk badge.
   - `ErrorMessages`:
     - `DailyRecordings.AlreadyRecorded/FutureDate/IdBelongsToOtherRecording/UsageItemNotAllowed`;
     - `Cycles.NotRecordable/BeforeChickIn/PopulationExceeded`;
     - `Stock.Insufficient`, `Items.ConversionToBaseUom`, `Attachments.*`;
     - `Cycles.NotFound` saat sinkron → "Kandang sudah tidak menjadi tanggung jawab Anda" (§10).
   - Unit test: migrasi v2, antrean & SyncEngine (urutan, retry/backoff, klasifikasi, header idempotensi, antrean user lain ditahan), validator draft, penggabungan data lokal ke list.
6. **MobileApp — platform**:
   - Implementasi `IPhotoPicker` (MAUI `MediaPicker`), kompresi, `IClock`; izin `CAMERA` di manifest.
   - Pemicu sinkron: koneksi kembali (`IConnectivity.Changed`) & aplikasi kembali ke depan (`Window.Resumed`).
7. **MobileApp — UI**:
   - **Input** (`/input`, ganti placeholder `DailyInput`): kartu Recording Harian; Stok Ayam (M3) & Request Pakan (M9) masih "Segera hadir".
   - **Form Recording** (`/input/recording?cycleId=`):
     - pilih kandang/siklus (dari field-context), tanggal (default hari ini; tanggal yang sudah terisi ditandai), umur & populasi;
     - mati, culling, BW + **kalkulator sampel timbang** (total berat ÷ jumlah ekor);
     - baris pemakaian (item, satuan, qty, stok tersedia + peringatan), catatan, foto;
     - Simpan → antrean → langsung dikirim bila online → halaman sukses ("Terkirim" / "Tersimpan, akan dikirim saat online");
     - tombol "Perbarui data" (field-context).
   - **List recording per siklus** (`/siklus/{id}/recording`, dari tab Siklus di detail Kandang): data server + lokal dengan status Terkirim / Belum terkirim / Gagal; tombol Input untuk PPL.
   - **Detail recording**: nilai, pemakaian, riwayat revisi, lampiran (server) atau nilai + status + Edit/Hapus (lokal).
   - **Antrean Sinkron** (`/antrean`, ganti placeholder `SyncQueue`): item Pending/Gagal + pesan, Kirim ulang, Edit, Hapus, "Sinkronkan sekarang"; info antrean milik user lain.
   - Badge antrean di header `MainLayout`; ringkasan "n belum terkirim" di Beranda.
8. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo, `ppl.bdg` / `ppl2.bdg` / `manager.bdg`):
   - Input recording online + 1 foto → muncul di WebApp Daily Recordings, stok gudang kandang berkurang.
   - **Mode pesawat** → 2 hari recording + foto → online → terkirim otomatis **tanpa dobel** (cek DB: recording & lampiran).
   - Konflik: tanggal yang sama diinput dulu di WebApp → item Gagal "sudah ada recording"; stok tidak cukup → Gagal; edit → kirim ulang sukses.
   - Kandang dipindah ke `ppl2.bdg` saat antrean belum terkirim → Gagal "Kandang sudah tidak menjadi tanggung jawab Anda".
   - `manager.bdg`: list & detail recording read-only, tanpa tombol Input.
9. **Dokumentasi**: realisasi §16, status §8/§11, RANGKUMAN.

### 15.4 Di luar lingkup M2
- Revisi recording dari mobile → M8 (lewat approval). Stok ayam harian → M3. Grafik → M4.
- Sinkron di latar belakang (WorkManager) → backlog (§3.6).
- Pembersihan lampiran yatim di server (lampiran sudah terunggah tetapi recording-nya dihapus dari antrean) → backlog.

---

## 16. Realisasi Fase M2 — Recording Harian (offline) (2026-10-09)

### 16.1 Backend
- **`GET mobile/field-context`** (`Application/Mobile/GetFieldContext.cs`, `Web.Api/Endpoints/Mobile/MobileEndpoints.cs`, tag `Mobile`, permission `production:read`), sesuai M-40:
  - siklus `Active`/`Harvesting` dalam lingkup cabang + PPL: kandang, peternak, gudang kandang, chick-in, populasi awal & berjalan, tanggal recording terakhir, tanggal recording 14 hari terakhir;
  - item Feed/OVK aktif + satuan (satuan dasar faktor 1 lebih dulu, lalu konversi);
  - **kuantitas** stok Feed/OVK di gudang kandang siklus tersebut (tanpa nilai/HPP);
  - `serverDate` & `generatedAtUtc`.
- **Lingkup PPL di stok (M-41)**: `GET inventory/stock-balances` hanya gudang kandang dalam lingkup; `GET inventory/stock-card` untuk gudang di luar lingkup → 404 `Warehouses.NotFound`.
- Endpoint recording, lampiran & idempotensi tidak berubah (sudah siap offline sejak Fase 4/9). **Tidak ada migration.**

### 16.2 MobileApp.Core
- `ApiClient`: `PostAsync<T>(…, idempotencyKey)` (header `Idempotency-Key`) dan `PostFileAsync<T>` (multipart `file` + `id`).
- Abstraksi `IClock` (`JakartaClock`, UTC+7) dan `IPhotoPicker`.
- **`LocalDb` v2**: tabel `sync_queue` (per user; status `Pending`/`Sending`/`Failed`, percobaan, waktu coba berikutnya, kode & pesan error). Baris dihapus setelah server menerima, sehingga tabel ini juga menjadi daftar "Belum terkirim" — tabel `local_entries` & `field_context` dari §3.5 **tidak dibuat** (field-context disimpan di `cache_entries`). `ClearUserDataAsync` tidak menghapus antrean: entri user lain ditahan sampai pemiliknya masuk lagi.
- **`ProductionApi`** (field-context, list/detail recording lewat `ApiCache`), `PartnershipApi.GetCycleAsync`.
- **`RecordingDraft` + `RecordingRules`**: tanggal ≤ hari ini & ≥ chick-in, belum ada di server (14 hari) maupun di antrean; mati + culling ≤ populasi berjalan dikurangi entri antrean; pemakaian: gudang kandang wajib ada, item sekali, satuan valid, qty > 0; stok (dikurangi antrean) hanya **peringatan**; maks 5 foto; kalkulator sampel timbang.
- **`SyncEngine`** (M-42): satu proses pada satu waktu, urut tanggal; foto diunggah dulu (status per foto disimpan, tidak diunggah ulang), lalu recording dengan `Idempotency-Key` = id. Klasifikasi: 2xx → hapus dari antrean + hapus file foto; jaringan/5xx/429 → Pending + backoff 30 dtk → 5 mnt (proses berhenti); 401 → ditahan; 4xx lain → Gagal (entri lain tetap jalan). `Cycles.NotFound` → "Kandang sudah tidak menjadi tanggung jawab Anda…". Setelah ada yang terkirim, field-context & list recording siklus disegarkan. Pemicu: koneksi kembali, timer 30 dtk, aplikasi aktif lagi (`Window.Resumed`), sesudah login, tombol "Sinkronkan".
- **`RecordingService`** (M-43): simpan/ubah (id tetap) → antrean → langsung dikirim bila online; kirim ulang; hapus (beserta foto); list siklus = server (cache) + lokal, terbaru dulu.
- `ErrorMessages`: kode recording, siklus, stok, item & lampiran.

### 16.3 MobileApp
- **Input** (`/input`): kartu Recording Harian (Stok Ayam M3 & Request Pakan M9 masih "Segera hadir") + status recording hari ini per siklus (Belum diisi / Belum terkirim / Gagal / Terkirim).
- **Form Recording** (`/input/recording?cycleId=` | `?id=` untuk ubah entri antrean): data per tanggal field-context + "Perbarui data", pilih kandang (otomatis bila hanya satu), tanggal (maks hari ini), umur & populasi tersedia, mati/culling, BW + kalkulator, baris pemakaian (satuan default = satuan terbesar, stok tersedia), catatan, foto kamera/galeri (`MediaPicker` MAUI 10: maks 1600 px, JPEG 80, diputar tegak), peringatan stok → tombol "Simpan tetap"; halaman hasil Terkirim / Tersimpan di perangkat / Ditolak server (+ "Ubah data").
- **Recording per siklus** (`/siklus/{id}/recording`, dari tab Siklus detail Kandang) + **Detail** (`/recording/{id}`: entri lokal dengan Kirim ulang/Ubah/Hapus, atau data server dengan pemakaian, revisi, lampiran). Manager read-only (M-45).
- **Antrean Sinkron** (`/antrean`): status, pesan error, jumlah percobaan, Kirim ulang/Ubah/Hapus, "Sinkronkan", info entri milik user lain. Badge jumlah antrean di bottom bar + kartu "n data belum terkirim" di Beranda.
- Manifest: izin `CAMERA`, `READ_EXTERNAL_STORAGE` (≤ API 32), query intent `IMAGE_CAPTURE`.
- Foto diunggah dengan nama `recording-YYYYMMDD-n.jpg`.

### 16.4 Pengujian & verifikasi
- **Test: 545 lulus** — 147 domain, 81 application, 14 arsitektur, **103 MobileApp.UnitTests** (+35: aturan draft, `SyncEngine`, `RecordingService`, antrean `LocalDb`), **33 integration Web.Api** (+3 `MobileRecordingTests`: isi field-context & tanpa nilai, replay antrean tanpa dobel + 409 + 404 lingkup, lingkup stock-balances/stock-card), 167 integration Web.App.
- **API** pada `intiplasma_verify` + data demo: field-context `ppl.bdg` hanya siklus Inti Lembang, `ppl2.bdg` hanya Ahmad 2, `manager.bdg` keduanya; stock-balances PPL hanya gudang kandangnya.
- **Emulator API 34**:
  - `ppl.bdg` online: recording 3 mati, 1 culling, BW 650, 3 SAK pakan + 1 foto → "Recording terkirim"; DB: 150 kg pakan, 1 lampiran (≈26 KB).
  - `ppl2.bdg` **mode pesawat** (tanggal 07–08/10 dikosongkan di DB verifikasi): 07/10 + foto dan 08/10 tersimpan di perangkat (badge 1 → 2); 07/10 kedua ditolak lokal ("sudah ada di antrean"); 09/10 ikut diantre sementara admin lebih dulu menginput 09/10 lewat API.
  - Mode pesawat dimatikan → sinkron otomatis: 07 & 08/10 terkirim (DB: satu baris per tanggal, foto tertaut), 09/10 **Gagal** "Recording untuk tanggal ini sudah ada…"; dihapus dari antrean → badge hilang.
  - List recording siklus & detail (tab Lampiran) tampil; `manager.bdg` melihat list tanpa tombol input.

### 16.5 Catatan
- Perbaikan saat verifikasi: banner offline menutupi tombol paling bawah (ruang bawah ditambah saat offline); tombol outline "Hapus" berubah biru saat ditekan (warna dikunci).
- Tanggal di `input type=date` ditampilkan WebView sesuai locale perangkat (emulator en-US: `10/09/2026`); nilai yang dikirim tetap ISO.
- Pemindahan kandang saat antrean belum terkirim (→ Gagal dengan pesan lingkup) diuji di unit test & integration test, tidak diulang di emulator.
- Lampiran yang sudah terunggah lalu entrinya dihapus dari antrean menjadi lampiran yatim di server (backlog, §15.4).
