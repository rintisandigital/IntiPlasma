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
| `GET approvals/{id}`, `GET farmers|coops|contracts/{id}/approvals` | M6 | Detail + timeline (level, approver, keputusan). Timeline per dokumen dipindah ke endpoint dokumen agar memakai hak baca & lingkup dokumen (realisasi §24). |
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

### Fase M3 — Stok Ayam Harian (offline) ✅ (selesai 2026-10-09, task §17, realisasi §18)
- **Backend**: `WeightRange` + `LiveBirdStockEntry` (migration `PhaseM3_LiveBirdStock`), use case & endpoint §4.4.
- **Web.App**: Master Data → **Weight Ranges**; **Production → Live Bird Stock** (rekap + ekspor).
- **Klien**: input per rentang (offline), list per siklus, "Salin dari kemarin"; rekap Manager.

### Fase M4 — Grafik & Dashboard ✅ (selesai 2026-10-09, task §19, realisasi §20)
- **Backend**: `GetMobileDashboardQuery` (ambang dari `Mobile:Dashboard`), `GET mobile/dashboard`.
- **Klien**: Dashboard PPL & Manager, Grafik produksi (Chart.js), perbandingan siklus.

### Fase M5 — Breed Standard ✅ (selesai 2026-10-11, task §21, realisasi §22)
- **Backend**: `BreedStandard` (strain + baris per umur: BW gram, FCR, deplesi %, pakan kumulatif g/ekor), strain di siklus (migration `PhaseM5_BreedStandard`); `performance` menambah `standard[]` & deviasi; dashboard memakai standar.
- **Web.App**: Production → **Breed Standards** (CRUD + impor CSV), strain di form siklus, garis standar di tab Performance.
- **Klien**: garis standar & deviasi di grafik, indikator di/bawah standar.

### Fase M6 — Modul Approval Berjenjang ✅ (selesai 2026-10-12, task §23, realisasi §24)
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
- **Semua keputusan M-1 s.d. M-39 disepakati** 2026-10-08. Fase **M0 selesai** 2026-10-08 (§12); **M1 selesai** 2026-10-09 (§13 task, §14 realisasi); **M2 selesai** 2026-10-09 (§15 task, §16 realisasi; M-40 s.d. M-45 disepakati); **M3 selesai** 2026-10-09 (§17 task, §18 realisasi; M-46 s.d. M-54 disepakati); **M4 selesai** 2026-10-09 (§19 task, §20 realisasi; M-55 s.d. M-68 disepakati); **M5 selesai** 2026-10-11 (§21 task, §22 realisasi; M-69 s.d. M-80 disepakati, M-71 direvisi dengan snapshot saat Close); **M6 selesai** 2026-10-12 (§23 task, §24 realisasi; M-81 s.d. M-99 disepakati); berikutnya **M7**.
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
- **Migration `PhaseM1_FieldOfficerScope`**: kolom `field_officer_user_id` (nullable, ber-index) di `master.farmers` & `master.coops`, FK ke `identity.users` **ON DELETE SET NULL** (user bisa dihapus permanen, W-22; penugasannya menjadi kosong). Data lama tetap kosong. Sudah di-migrate.
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

---

## 17. Daftar Task M3 — Stok Ayam Harian (offline) (urutan)

Disusun 2026-10-09 dari pemetaan kode. Prasyarat: hasil M2 sudah di-commit. Migration baru **`PhaseM3_LiveBirdStock`** (user yang menjalankan migrate).

### 17.1 Temuan yang memengaruhi desain
- **Permission `production:stock-report` sudah ada** (M0) dan sudah masuk role PPL; Manager cukup `production:read`. Tidak perlu permission baru.
- **Pola master data** (Uom, TaxCode): aggregate di `Domain/MasterData/*` (`Codes.Normalize`, `IsActive`, tanpa hapus), use case per folder `Application/<Nama>/{Create,Get,Update}`, konfigurasi di `Infrastructure/MasterData/MasterDataConfigurations.cs`, layar Web.App `Areas/MasterData` (Index + Form + Export, `MenuCodes.Master*`, `MenuCatalog.Page(…, CreateEditExport, true)`).
- **Pola laporan Web.App** dengan filter cabang & ekspor: `Inventory/StockController` (`support.BranchFilterAsync`, `ExportColumn<T>`, `support.ExportAsync` → Excel/PDF). Menu Production saat ini: Cycles, Daily Recordings, Harvests (`production.*`).
- **Populasi berjalan** = `ProductionCycle.CurrentPopulation` (awal − mati − culling − panen), juga dipakai field-context. Validasi "total ekor ≤ populasi" memakai nilai ini saat simpan; urutan sinkron recording → stok (§3.6) penting karena recording mengurangi populasi.
- **`SyncEngine` (M2) masih khusus recording** (`RecordingDraft`, path `production/daily-recordings`). Harus digeneralisasi per jenis (`SyncKinds`) dengan urutan **tanggal, lalu Recording sebelum LiveBirdStock**. Tabel `sync_queue` sudah punya kolom `kind`, jadi **skema lokal tidak berubah**.
- **Field-context (M2)** sudah disiapkan untuk ditambah rentang bobot & entri stok terakhir tanpa mengganti endpoint.
- Menu mobile: `AppFeatures.LiveBirdStock` (`/fitur/stok-ayam`, `production:read`) sudah ada — tab bawah Manager, di "Lainnya" untuk PPL; kartu "Stok Ayam (M3)" di hub Input masih placeholder.
- **DemoDataSeeder** mensimulasikan siklus harian (`RecordDayAsync`, BW per umur `BodyWeightGram`) → mudah menambah entri stok ayam untuk siklus yang mendekati panen.

### 17.2 Keputusan tambahan (disepakati 2026-10-09)
| # | Topik | Usulan |
|---|---|---|
| M-46 | Lingkup & perubahan Rentang Bobot | Master **global** (semua cabang). Batas `MinWeightKg` (inklusif) / `MaxWeightKg` (eksklusif) dalam kg, 3 desimal. Rentang aktif tidak boleh tumpang tindih. **Tidak bisa dihapus**, hanya dinonaktifkan; batas min/maks hanya bisa diubah selama rentang belum dipakai entri (nama, urutan, status selalu bisa). |
| M-47 | Upsert entri | Kunci unik (siklus, tanggal, rentang). `POST` dengan kombinasi yang sudah ada → **memperbarui** entri itu (id lama dipertahankan & dikembalikan); `id` klien yang sudah dipakai kombinasi lain → `409`. Ganti rentang = hapus + entri baru. |
| M-48 | Lampiran | Entri stok **tanpa foto/lampiran**, hanya catatan (informasi Sales, mengurangi beban sinkron). `IHasDocuments` di §4.4 tidak dipakai. |
| M-49 | Ubah & hapus | Upsert oleh user ber-`production:stock-report` dalam lingkup kandang (tidak terbatas pembuat, agar PPL pengganti bisa mengoreksi). **Hapus**: entri bertanggal hari ini/kemarin (tanggal server), **wajib online**; entri yang belum terkirim cukup dihapus dari antrean. |
| M-50 | Validasi populasi | Total ekor semua rentang pada (siklus, tanggal) ≤ populasi berjalan **saat disimpan** (server); klien memakai populasi field-context dikurangi deplesi recording yang belum terkirim. |
| M-51 | Rekap | Per siklus `Active`/`Harvesting` di cabang: entri dari **tanggal input terakhir ≤ tanggal dipilih** + umur data (hari). Data > 1 hari ditandai basi; siklus tanpa entri tampil "Belum lapor". Per rentang: ekor, kg (ton), jumlah kandang, rata-rata kg; total cabang. |
| M-52 | Field-context | Ditambah `weightRanges` (aktif, urut `SortOrder`) dan per siklus `latestStock` (tanggal entri terakhir + entrinya) untuk daftar hari ini & "Salin dari kemarin". |
| M-53 | Layar mobile | PPL: hub Input → **Stok Ayam** (pilih kandang & tanggal → daftar entri per rentang, tambah/ubah lewat sheet, total & sisa populasi tak terlapor, "Salin dari kemarin"); list per siklus di `/siklus/{id}/stok-ayam` (tautan dari halaman recording siklus). Manager: tab **Stok Ayam** = rekap cabang per tanggal → drill-down kandang (read-only). |
| M-54 | Data demo | Seed 5 rentang (`< 1,4`, `1,4–1,6`, `1,6–1,8`, `1,8–2,0`, `≥ 2,0` kg) dan entri stok 3 hari terakhir untuk siklus berjalan berumur ≥ 25 hari, dibagi dari populasi berjalan sesuai BW simulasi. |

### 17.3 Task
1. **Domain** + unit test:
   - `WeightRange` (`Domain/MasterData/WeightRanges`): `Code`, `Name`, `MinWeightKg?`, `MaxWeightKg?`, `SortOrder`, `IsActive`; `Contains(avgKg)`, `Overlaps(other)`; error (`InvalidBounds`, `Overlap`, `InUse`).
   - `LiveBirdStockEntry` (`Domain/Production/LiveBirdStock`): `Id` (klien), `BranchId`, `CycleId`, `CoopId`, `Date`, `WeightRangeId`, `Birds`, `WeightKg`, `AgeDays`, `Notes`, `CreatedBy/At`, `UpdatedBy/At`; turunan `AverageWeightKg`; `Create`/`Update` dengan validasi M-27 (siklus recordable, tanggal ≥ chick-in, ekor > 0, kg > 0, rata-rata di dalam rentang).
2. **EF + migration `PhaseM3_LiveBirdStock`**: `master.weight_ranges` (unik `code`), `production.live_bird_stock_entries` (unik `cycle_id, date, weight_range_id`, index `branch_id, date`, FK ke siklus, kandang, rentang). **User yang menjalankan migrate.**
3. **Application**:
   - Rentang bobot: `CreateWeightRangeCommand`, `UpdateWeightRangeCommand` (cek tumpang tindih antar rentang aktif & M-46), `GetWeightRangesQuery` (`activeOnly`).
   - `UpsertLiveBirdStockEntryCommand` (M-47, M-50, lingkup lewat `CycleLoader`), `DeleteLiveBirdStockEntryCommand` (M-49).
   - `GetLiveBirdStockEntriesQuery` (`cycleId | branchId`, `date | from–to`, lingkup cabang + PPL), `GetLiveBirdStockSummaryQuery` (M-51).
   - `GetFieldContextQuery` ditambah rentang & `latestStock` (M-52).
4. **Web.Api**: `GET weight-ranges` (`master-data:read`), `POST production/live-bird-stocks` (`production:stock-report`, idempotency), `DELETE production/live-bird-stocks/{id}`, `GET production/live-bird-stocks`, `GET production/live-bird-stocks/summary?date&branchId` (`production:read`).
5. **Test backend**: unit (domain rentang & entri, tumpang tindih, upsert/hapus/validasi populasi, lingkup PPL); integration Web.Api (upsert idempotent + update kombinasi sama + 409, total > populasi → 400, data PPL lain 404, rekap memakai tanggal terakhir ≤ tanggal pilih, Manager `POST` → 403).
6. **Web.App**:
   - **Master Data → Weight Ranges** (`master.weight-ranges`: list, form, ekspor).
   - **Production → Live Bird Stock** (`production.live-bird-stock`): filter tanggal & cabang → rekap rentang (ekor, ton, kandang, rata-rata) + tabel per kandang (umur data, sorot basi, "Belum lapor"), ekspor Excel/PDF.
   - `MenuCatalog` + integration test (`Admin_Should_OpenEveryReleasedMenu`, CRUD rentang, rekap tampil).
   - **DemoDataSeeder** (M-54) + `docs/DEMO-DATA.md`.
7. **MobileApp.Core**:
   - DTO `WeightRange`, `LiveBirdStockEntry`, `LiveBirdStockSummary`, field-context baru; `ProductionApi` (list per siklus, rekap, hapus).
   - `StockDraft` + `StockRules` (M-24/M-27 lokal: rentang sama di tanggal sama → ubah entri, rata-rata di dalam rentang, total ≤ populasi tersedia) + "salin dari kemarin".
   - **Generalisasi `SyncEngine`**: handler per `SyncKinds` (Recording, LiveBirdStock), urutan tanggal lalu Recording → LiveBirdStock; `StockService` (simpan/ubah/hapus/list gabungan server + lokal).
   - `ErrorMessages` untuk kode baru; unit test (aturan, urutan sinkron campuran, upsert lokal).
8. **MobileApp UI**:
   - PPL: kartu **Stok Ayam** di hub Input (status hari ini per kandang), halaman input per kandang & tanggal (daftar rentang, sheet ekor + kg → rata-rata & validasi langsung, total, sisa populasi, salin dari kemarin), list per siklus `/siklus/{id}/stok-ayam`, item stok di **Antrean**.
   - Manager: tab **Stok Ayam** → rekap cabang per tanggal (rentang × ekor/ton/kandang, total) → drill-down kandang.
9. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo):
   - PPL online: input 2 rentang → cocok di WebApp Live Bird Stock; ubah satu rentang → entri yang sama berubah (tidak dobel).
   - Mode pesawat: recording + stok ayam tanggal yang sama → online → recording terkirim dulu, stok divalidasi terhadap populasi baru; total > populasi → Gagal dengan pesan.
   - Salin dari kemarin; hapus entri hari ini (online).
   - Manager: rekap sama dengan WebApp; drill-down kandang; data basi ditandai.
10. **Dokumentasi**: realisasi §18, status §8/§11, RANGKUMAN, DEMO-DATA.

### 17.4 Di luar lingkup M3
- Kartu stok ayam di Dashboard → M4. Notifikasi ke tim Sales → backlog.
- Integrasi rekap stok ayam ke Sales Order (alokasi otomatis) → backlog.
- API CRUD rentang bobot untuk mobile (hanya `GET`).

---

## 18. Realisasi Fase M3 — Stok Ayam Harian (offline) (2026-10-09)

### 18.1 Backend
- **Domain**:
  - `WeightRange` (`Domain/MasterData/WeightRanges`): batas `[MinWeightKg, MaxWeightKg)` 3 desimal, sisi terbuka = null (minimal satu batas), `Contains`, `Overlaps`, `SetBounds`, `Update(name, sortOrder, isActive)`.
  - `LiveBirdStockEntry` (`Domain/Production/LiveBirdStock`): `Create`/`Update` memvalidasi siklus recordable & ≥ chick-in, rentang aktif, ekor & kg > 0, rata-rata di dalam rentang; rentang & siklus entri tidak bisa diganti (`KeyMismatch`). Tanpa lampiran (M-48).
- **Migration `PhaseM3_LiveBirdStock`**: `master.weight_ranges` (unik `code`), `production.live_bird_stock_entries` (unik `cycle_id, date, weight_range_id`; index `branch_id, date`; FK siklus, cabang, kandang, rentang). Sudah di-migrate.
- **Application**:
  - `WeightRanges/WeightRangeUseCases.cs`: create/update dengan cek tumpang tindih antar rentang aktif; batas terkunci bila sudah dipakai entri (`WeightRanges.InUse`); `GetWeightRangesQuery(activeOnly)`.
  - `UpsertLiveBirdStockEntryCommand` (M-47: kombinasi sama → entri lama diubah & id lama dikembalikan; `id` dipakai kombinasi lain → 409; tanggal ≤ server + 1; total ekor semua rentang ≤ populasi berjalan, M-50) dan `DeleteLiveBirdStockEntryCommand` (lingkup PPL, hanya tanggal hari ini/kemarin, M-49).
  - `GetLiveBirdStockEntriesQuery` & `GetLiveBirdStockSummaryQuery` (M-51; siklus `Active`/`Harvesting` yang sudah chick-in pada tanggal itu, entri tanggal lapor terakhir ≤ tanggal pilih, umur data, total per rentang — rentang aktif + rentang nonaktif yang masih terlapor).
  - `GetFieldContextQuery` + `weightRanges` aktif dan per siklus `latestStockDate` + `latestStock` (M-52).
- **Web.Api**: `GET weight-ranges` (`master-data:read`, default aktif saja), `POST production/live-bird-stocks` (`production:stock-report`, idempotency), `DELETE production/live-bird-stocks/{id}`, `GET production/live-bird-stocks`, `GET production/live-bird-stocks/summary` (`production:read`).

### 18.2 Web.App
- **Master Data → Weight Ranges** (`master.weight-ranges`): list, form (kode, nama, dari/di bawah kg, urutan, aktif), ekspor.
- **Production → Live Bird Stock** (`production.live-bird-stock`, hak Export): filter tanggal & cabang, kartu total (ekor, ton, kandang lapor), tabel per rentang, tabel per kandang (kolom per rentang, umur data, baris basi disorot, "Not reported"), ekspor Excel/PDF (satu baris per kandang × rentang).
- **DemoDataSeeder** (M-54): 5 rentang `BB-14 … BB-20UP` dan stok 3 hari terakhir untuk siklus berjalan berumur ≥ 25 hari; `docs/DEMO-DATA.md` diperbarui.

### 18.3 MobileApp.Core & MobileApp
- **Core**:
  - DTO `WeightRange`, `LiveBirdStockEntry`, `LiveBirdStockSummary`; field-context baru; `ApiClient.DeleteAsync`; `ProductionApi` (stok per siklus, rekap, hapus).
  - `StockDraft` + `StockRules` (rata-rata di dalam rentang, total ekor ≤ populasi berjalan dikurangi deplesi recording yang belum terkirim; baris rentang yang sama dianggap diganti).
  - `StockService`: simpan ke antrean (entri antrean dengan rentang sama diganti, id tetap), kirim langsung bila online, kirim ulang, hapus lokal, hapus server (online), gabungan server + antrean per siklus, `Previous` untuk "Salin".
  - **`SyncEngine` digeneralisasi** per `SyncKinds` (`Recording`, `LiveBirdStock`), urutan **tanggal → Recording → LiveBirdStock**; setelah terkirim menyegarkan field-context dan list per jenis; `CountAsync` untuk badge; event `Changed` juga saat proses selesai.
  - `ErrorMessages` untuk kode `LiveBirdStock.*` & `WeightRanges.*`.
- **Layar**:
  - **Stok Ayam** (`/stok-ayam`): PPL — status hari ini per kandang (Belum diisi / n rentang / Belum terkirim / Gagal) + tombol input; Manager — rekap tanggal (total, per rentang, per kandang dengan badge Hari ini / Data n hari lalu / Belum lapor, ketuk untuk rincian rentang).
  - **Input stok** (`/stok-ayam/input?cycleId&date`): pilih kandang & tanggal, umur & populasi tersedia, total & sisa belum terlapor, kartu per rentang (Isi / Ubah / Hapus / Batalkan perubahan) dengan rata-rata & validasi langsung, **Salin dari {tanggal}** bila tanggal itu kosong. Hapus entri server hanya tampil saat online dan untuk hari ini/kemarin.
  - **Riwayat per siklus** (`/siklus/{id}/stok-ayam`, tautan dari halaman recording siklus) dikelompokkan per tanggal.
  - **Antrean** menampilkan entri stok (kirim ulang/ubah/hapus); badge & kartu Beranda menghitung semua jenis.

### 18.4 Pengujian & verifikasi
- **Test: 578 lulus** — 162 domain (+15), 83 application (+2), 14 arsitektur, 109 MobileApp.UnitTests (+6), 39 integration Web.Api (+6 `LiveBirdStockTests`; setup bersama `FieldScenarioTest`), 171 integration Web.App (+4).
- **Demo data** di `intiplasma_verify`: 736 command, 7 entri stok untuk SKL/BDG/2026/IX/0001 (06–08/10).
- **Emulator API 34**:
  - `ppl2.bdg`: "Salin dari 08/10/2026 (2 rentang)" → 2 entri hari ini terkirim; ubah BB-20 → baris yang sama di DB berubah (1.900 ekor / 3.600 kg, tidak dobel); rata-rata di luar rentang ditandai merah & ditolak sebelum simpan.
  - **Mode pesawat**: recording hari ini (mati 5) + ubah BB-20UP (700 ekor) → Antrean berisi 2 item (badge 2), tombol hapus entri server disembunyikan → online → keduanya terkirim otomatis, recording 1 detik lebih dulu dari stok; tetap 2 entri hari ini.
  - Hapus entri BB-20UP hari ini (online) → terhapus di server.
  - `manager.bdg`: tab Stok Ayam — 1.900 ekor · 3,60 ton, kandang lapor 1/2, BB-20 1 kandang, Ahmad 2 "Hari ini" (populasi 2.864 sudah dikurangi recording), Inti Lembang "Belum lapor", rincian rentang saat diketuk.

### 18.5 Catatan
- Perbaikan saat verifikasi: tombol "Mengirim…" di Antrean tidak kembali setelah sinkron (event terakhir terkirim sebelum kunci dilepas → kini juga dikirim setelah proses selesai); pesan sukses di input stok memakai `alert-success` (`alert-info` template berwarna magenta).
- Halaman Live Bird Stock Web.App diuji lewat integration test (render + ekspor), tidak dibuka manual; datanya dari query rekap yang sama dengan mobile.
- `/stok-ayam` untuk PPL ada di menu "Lainnya" (tab bawah PPL sudah penuh), jadi tab Lainnya yang tersorot.
- Validasi "data basi" (> 1 hari) dan "Belum lapor" diuji di integration test; di emulator hanya "Belum lapor" yang terlihat.

---

## 19. Daftar Task M4 — Grafik & Dashboard (urutan)

Disusun 2026-10-09 dari pemetaan kode. Prasyarat: hasil M3 sudah di-commit (`8a4e562`). **Tidak ada migration** di M4 dan skema SQLite lokal tidak berubah.

### 19.1 Temuan yang memengaruhi desain
- **Rumus performa sudah terpusat** di `CyclePerformance.Calculate` (`Domain/Partnership/Cycles/CyclePerformance.cs`): deplesi %, FCR = pakan ÷ (populasi × BW + kg panen), ADG, IP. Namun akumulasi harian (`BuildDays`: BW dibawa dari penimbangan terakhir, panen dihitung per tanggal) masih **private** di `GetCyclePerformanceQueryHandler` (`Application/Production/ProductionQueries.cs`). Dashboard butuh angka kumulatif per siklus, jadi akumulasi ini perlu dipindah ke helper bersama agar tidak ada dua rumus.
- **`GET cycles/{id}/performance` sudah lengkap untuk grafik**: `days[]` per hari (umur, mati, culling, pakan kg, BW, kumulatif: populasi, deplesi %, FCR, ADG, IP) + `harvests[]`; lingkup PPL & cabang sudah ditegakkan. Tidak ada varian multi-siklus; perbandingan ≤ 3 siklus cukup dengan 3 request (masing-masing di-cache).
- **Belum ada rata-rata FCR/IP lintas siklus** di mana pun. Dashboard Admin Office (`Application/Monitoring/GetDashboardSummaryQuery.cs`) hanya untuk Web.App, tanpa lingkup PPL, dan isinya keuangan → tidak dipakai ulang, hanya pola SQL-nya (siklus aktif, populasi).
- **Tanggal server = UTC** (`IDateTimeProvider.UtcNow`; `serverDate` field-context juga UTC). Antara 00:00–07:00 WIB tanggal UTC masih "kemarin" → status "recording hari ini" di dashboard salah bila dihitung dari tanggal server.
- **Data dashboard sudah tersedia di SQL**: populasi & total deplesi di siklus, stok pakan per gudang kandang (`inventory.stock_balances` + item kategori Feed, seperti field-context), pakan per recording (`daily_recording_usages.base_quantity`), tanggal recording/stok ayam terakhir (`MAX(date)`). Pola temp table + `FieldScopeSql` + `IBranchAccess` dari `GetFieldContext`/`GetLiveBirdStockSummary` dipakai ulang.
- **Rekap stok ayam sudah ada** (`GET production/live-bird-stocks/summary`, M3) dan sudah di-cache di klien → tidak perlu diduplikasi di endpoint dashboard.
- **Options**: pola `RefreshTokenOptions` (POCO + `SectionName`, di-bind di `Infrastructure/DependencyInjection.cs`, handler menerima POCO). `appsettings.json` Web.Api belum punya section `Mobile`.
- **Chart.js 4.5.0 (UMD) sudah ada lokal** di Web.App (`wwwroot/assets/plugins/chartjs/chart.min.js`, ±208 KB) + contoh di tab Performance siklus (`Areas/Production/Views/Cycles/Details.cshtml`). MobileApp **belum punya JS interop sama sekali** (`index.html` hanya memuat `blazor.webview.js`).
- **Beranda** (`Components/Pages/Home.razor`) baru berisi sapaan, cabang, kartu antrean, dan grid menu. **Logo gepeng** (§14.5): aturan template `.header-avt > *:first-child { width: 100% }` meregangkan `<img class="header-logo-text">` yang tingginya tetap 36 px.
- Menu `AppFeatures.Performance` (`/fitur/grafik`, `production:read`, di "Lainnya") masih placeholder.
- Bagian dashboard yang bergantung fase lain: **BW vs standar** (M5), **approval & pengajuan saya** (M6), **request pakan** (M9) → belum bisa diisi di M4.

### 19.2 Keputusan tambahan (disepakati 2026-10-09)
| # | Topik | Usulan |
|---|---|---|
| M-55 | Bentuk `GET mobile/dashboard` | **Satu respons untuk semua peran**, mengikuti lingkup user (PPL = kandangnya, Manager = cabangnya): `date`, `kpi` (siklus aktif, populasi, ekor awal, deplesi % gabungan, FCR gabungan, IP tertimbang, recording hari ini x/y, stok ayam hari ini x/y) dan `cycles[]` per siklus `Active`/`Harvesting` (kandang, peternak, PPL, umur, populasi, deplesi %, FCR, BW terakhir, IP, tanggal recording & stok ayam terakhir, stok pakan kg, rata-rata pakai, estimasi sisa hari, **flag** risiko). Klien yang memilih tampilan per peran. Permission `production:read`. Field untuk M5/M6/M9 ditambahkan di fasenya **tanpa** mengganti endpoint. |
| M-56 | Tanggal acuan | Parameter opsional **`date`** dikirim klien (tanggal lokal `Asia/Jakarta`); default server = UTC+7. Validasi: ≤ tanggal server + 1. |
| M-57 | Cabang | Parameter opsional `branchId` (harus dalam akses); tanpa parameter = semua cabang dalam akses user (Manager = satu cabang, M-10). Mobile mengirim tanpa `branchId`. |
| M-58 | Ambang `Mobile:Dashboard` | `FeedAverageDays` = **3** (rata-rata pakan dari recording 3 hari terakhir yang ada pemakaian pakan; tanpa data → estimasi "–"), `FeedWarningDays` = **3** (sisa < 3 hari → peringatan), `RecordingLateDays` = **1** (recording terakhir lebih dari 1 hari sebelum tanggal acuan → terlambat), `DepletionWarningPercent` = **5** (deplesi kumulatif ≥ 5% → berisiko; sementara sampai standar breed M5). |
| M-59 | Siklus berisiko | Flag: `RecordingLate`, `FeedLow`, `HighDepletion`, `NoStockReport` (stok ayam belum dilaporkan hari ini, hanya untuk siklus umur ≥ 21 hari). Manager melihat daftar siklus ber-flag (urut jumlah flag); PPL melihat flag di kartu kandangnya. M5 menambah `BelowStandard`. |
| M-60 | Rumus gabungan | Deplesi % = Σ(mati + culling) ÷ Σ ekor awal; FCR = Σ pakan ÷ Σ(bobot hidup + kg panen) (siklus tanpa BW dikecualikan); IP = rata-rata IP siklus tertimbang populasi berjalan. Semua memakai helper akumulasi yang sama dengan `performance`. |
| M-61 | Dashboard PPL | Kartu **tugas hari ini** (recording & stok ayam per kandang: Belum diisi / Belum terkirim / Gagal / Terisi — status antrean lokal digabung), kartu **kandang saya** (umur, populasi, deplesi, FCR, BW, IP, flag), **stok pakan** (kg + SAK bila ada konversi, estimasi sisa hari, merah bila < 3 hari), antrean sinkron. Ketuk kandang → grafik siklus. |
| M-62 | Dashboard Manager | **KPI cabang**, recording & stok ayam hari ini x/y, **siklus berisiko**, **stok ayam per rentang** (ekor & ton, dari endpoint rekap M3) + tautan ke tab Stok Ayam. Kartu "Approval menunggu" baru muncul di M6. |
| M-63 | Offline dashboard | Respons dashboard di-cache (`ApiCache`, "Data per …"); status lokal (antrean, recording belum terkirim) selalu dihitung ulang di klien. Tombol "Segarkan". |
| M-64 | Grafik produksi | Layar `/grafik?cycleId=` (pilih siklus dalam lingkup, default siklus aktif pertama): pilih metrik lewat chip — **BW (g)**, **Deplesi %**, **FCR**, **Pakan harian & kumulatif**, **Mati harian**, **IP**; sumbu X = umur (hari); ringkasan angka terakhir + tabel harian ringkas. Recording **belum terkirim** ditampilkan sebagai titik/garis **putus-putus** (dihitung di klien, M-65). Data di-cache untuk offline. |
| M-65 | Rumus di klien | `CyclePerformance.Calculate` + akumulasi harian **diporting** ke `MobileApp.Core` (Core tidak boleh mereferensikan Domain); unit test memakai angka yang sama dengan test Domain agar hasilnya identik. |
| M-66 | Perbandingan siklus | **PPL & Manager** (dalam lingkup masing-masing) bisa membandingkan **≤ 3 siklus** (aktif atau sudah tutup) untuk satu metrik, ditumpuk per umur dengan warna berbeda + legenda. Tanpa endpoint baru (3 × `performance`). |
| M-67 | Chart.js di mobile | Salin **Chart.js 4.5.0** dari Web.App ke `src/MobileApp/wwwroot/lib/chartjs/` + `js/chart-interop.js` (render/update/destroy per canvas; angka sudah diformat id-ID dari .NET); komponen `ChartCanvas` (`IAsyncDisposable`). Tanpa plugin tambahan; animasi dimatikan agar ringan. |
| M-68 | Grafik mingguan | Toggle **Harian / Mingguan** di layar Grafik (juga saat membandingkan siklus). Minggu ke-n = umur `7n-6` s.d. `7n` (minggu 1 = umur 1–7). Metrik **aliran** dijumlah per minggu: pakan (kg & g/ekor), mati + culling (ekor & % dari populasi awal minggu). Metrik **kumulatif/posisi** diambil di hari terakhir minggu: BW (+ pertambahan BW minggu itu), deplesi %, FCR, IP, populasi. Minggu berjalan yang belum genap 7 hari ditandai "berjalan (n hari)"; hari yang tidak ada recording tidak diisi. Dihitung di klien dari `days[]` (tanpa endpoint baru); tabel ringkas per minggu di bawah grafik. |

### 19.3 Task
1. **Application — helper performa**: pindahkan akumulasi harian (`BuildDays`) ke `Application/Production/CyclePerformanceBuilder` (internal static) yang dipakai `GetCyclePerformanceQueryHandler` & dashboard. Perilaku `performance` tidak berubah (test lama tetap lulus).
2. **Backend — `GET mobile/dashboard`**:
   - `MobileDashboardOptions` (`Mobile:Dashboard`, M-58) di-bind di Infrastructure; `appsettings.json` Web.Api + `docs/DEPLOY.md`.
   - `Application/Mobile/GetMobileDashboard.cs` (`GetMobileDashboardQuery(DateOnly? Date, Guid? BranchId)`): temp table siklus aktif dalam lingkup cabang + `FieldScopeSql`; recording per hari (mati, culling, pakan, BW) untuk akumulasi, panen, stok pakan gudang kandang, rata-rata pakai `FeedAverageDays`, tanggal stok ayam terakhir; hitung per siklus dengan helper (task 1), flag (M-59), KPI gabungan (M-60).
   - Endpoint `dashboard` di `MobileEndpoints` (`production:read`).
3. **Test backend**:
   - Unit: helper akumulasi (hasil sama dengan sebelum refactor), KPI gabungan, flag (terlambat, pakan rendah, deplesi tinggi, belum lapor stok), estimasi sisa hari (tanpa data → null).
   - Integration Web.Api (`FieldScenarioTest`): PPL A hanya siklusnya, Manager keduanya; recording hari ini mengubah x/y dan menghapus flag terlambat; stok pakan & estimasi; `date` > server + 1 → 400; `branchId` di luar akses ditolak sesuai pola yang ada.
4. **MobileApp.Core**:
   - DTO `MobileDashboard` (+ `DashboardCycle`, `DashboardKpi`), `CyclePerformance`/`DailyPerformance`; `ProductionApi.GetDashboardAsync(date)` & `GetPerformanceAsync(cycleId)` lewat `ApiCache`.
   - `PerformanceCalculator` (porting M-65) + penggabungan recording antrean ke seri grafik (titik "lokal").
   - `DashboardService`: gabungkan respons server dengan status antrean (tugas hari ini, M-61).
   - `ChartMetric`/`ChartSeries` (metrik → seri, label Indonesia, warna perbandingan) + `WeeklyAggregator` (M-68: agregasi aliran vs posisi, minggu berjalan).
   - Unit test: path API & cache, kalkulator = angka test Domain, penggabungan lokal, status tugas hari ini.
5. **MobileApp — Chart.js**: salin `chart.min.js`, buat `js/chart-interop.js`, tambah script di `index.html`, komponen `Components/Shared/ChartCanvas.razor`.
6. **MobileApp — UI**:
   - **Beranda** (`Home.razor`): perbaiki logo header; tampilan PPL (M-61) & Manager (M-62) menggantikan grid menu (menu tetap ada di "Lainnya"); "Data per …" bila dari cache; tombol segarkan.
   - **Grafik** (`/grafik`, menggantikan placeholder `AppFeatures.Performance`): pilih siklus, chip metrik, toggle **Harian / Mingguan** (M-68), grafik + ringkasan + tabel harian/mingguan, titik lokal putus-putus; **Bandingkan** (tambah ≤ 2 siklus lain).
   - Tautan ke grafik dari kartu kandang di Beranda, tab Siklus detail Kandang, dan halaman recording siklus.
7. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo, `ppl.bdg` / `ppl2.bdg` / `manager.bdg`):
   - PPL: tugas hari ini "Belum diisi" → input recording → "Terisi"; stok pakan & estimasi sisa hari cocok dengan hitungan manual dari DB.
   - Manager: KPI & x/y cocok dengan query DB; siklus berisiko tampil (kondisi terlambat/deplesi tinggi dibuat di DB verifikasi); stok ayam per rentang sama dengan tab Stok Ayam.
   - Grafik: angka hari terakhir = tab Performance di Web.App; mode pesawat → recording baru tampil putus-putus; bandingkan 3 siklus; tampilan mingguan cocok dengan hitungan manual (minggu penuh mode pesawat → recording baru tampil putus-putus; bandingkan 3 siklus. minggu berjalan).
   - Mode pesawat → Beranda dari cache dengan "Data per …"; logo header tidak gepeng.
8. **Dokumentasi**: realisasi §20, status §8/§11, RANGKUMAN.

### 19.4 Di luar lingkup M4
- Garis standar breed, deviasi & flag `BelowStandard` → M5. Kartu approval menunggu & pengajuan saya → M6. Kartu request pakan → M9.
- Dashboard Manager/PPL di Web.App (Web.App tetap memakai dashboard Admin Office) → tidak direncanakan.
- Notifikasi push untuk flag risiko → backlog.
- Ekspor/bagikan grafik sebagai gambar → backlog.

---

## 20. Realisasi Fase M4 — Grafik & Dashboard (2026-10-09)

### 20.1 Backend
- **`CyclePerformanceBuilder`** (`Application/Production`): akumulasi harian dari `GetCyclePerformanceQueryHandler` dipindah ke helper bersama (`BuildDays`, `Current`); `cycles/{id}/performance` tidak berubah.
- **`GET mobile/dashboard?date&branchId`** (`Application/Mobile/GetMobileDashboard.cs`, `production:read`), M-55 s.d. M-60:
  - siklus `Active`/`Harvesting` yang sudah chick-in, lingkup cabang + PPL;
  - per siklus: performa kumulatif (helper yang sama), umur, populasi, tanggal recording & stok ayam terakhir, terisi hari ini/kemarin, stok pakan gudang kandang per item (+ satuan terbesar mis. SAK), rata-rata pakan dari `FeedAverageDays` recording terakhir yang ada pemakaian pakan, estimasi sisa hari, flag;
  - KPI gabungan: deplesi Σ(mati+culling)/Σ awal, FCR Σ pakan/Σ bobot hidup+panen (siklus tanpa BW dikecualikan), IP tertimbang populasi, recording & stok ayam hari ini x/y.
  - Tanggal default = tanggal Asia/Jakarta (UTC+7); `date` > tanggal server + 1 → `400 MobileDashboard.FutureDate`; `branchId` di luar akses → `403`.
  - Logika flag & KPI di `MobileDashboardCalculator` (tanpa database, diuji unit).
- **`MobileDashboardOptions`** (`Mobile:Dashboard`: `FeedAverageDays` 3, `FeedWarningDays` 3, `RecordingLateDays` 1, `DepletionWarningPercent` 5, `StockReportFromAgeDays` 21) di `appsettings.json` Web.Api & `docs/DEPLOY.md`.
- **Tidak ada migration.**

### 20.2 MobileApp.Core
- DTO `MobileDashboard`, `DashboardCycle`, `DashboardKpi`, `PerformanceFigures`, `CyclePerformanceReport`; `ProductionApi.GetDashboardAsync` (tanpa `date` → satu kunci cache lintas hari), `ReadDashboardAsync`, `GetPerformanceAsync`; `PartnershipApi.GetCyclesAsync` (pemilih siklus).
- **`PerformanceCalculator`** (M-65): porting `CyclePerformance.Calculate` + akumulasi harian; `WithLocal` menyisipkan recording antrean (tanggal yang belum ada di server) sebagai titik `IsLocal`.
- **`WeeklyPerformance`** (M-68): minggu ke-n = umur 7n−6…7n (umur 0 masuk minggu 1); pakan & mati+culling dijumlah (pakan g/ekor dan deplesi % terhadap populasi awal minggu), BW (+ pertambahan), deplesi, FCR, IP, populasi dari hari terakhir minggu; minggu terakhir yang belum sampai umur 7n = "berjalan".
- **`PerformanceCharts`**: spesifikasi Chart.js per metrik (BW, Deplesi, FCR, Pakan, Mati, IP), harian/mingguan, satu siklus (pakan: batang harian + garis kumulatif sumbu kanan; mati & culling bertumpuk; mingguan BW + pertambahan) atau ≤ 3 siklus (satu garis per siklus; pakan kumulatif g/ekor, deplesi minggu %), `LocalFrom` untuk garis putus-putus.
- **`DashboardService`**: respons server (atau salinan tersimpan) + antrean → status tugas hari ini (`Done`/`Queued`/`Failed`/`Missing`/`NotExpected`), flag `RecordingLate`/`NoStockReport` disembunyikan bila antrean sudah menjawabnya, `AtRisk` urut jumlah flag.
- `Labels.TaskState/TaskTone/RiskFlag`, `IdFormat.Optional`, `IdFormat.Feed` ("925 kg (18,5 SAK)").

### 20.3 MobileApp
- **Chart.js 4.5.0** (dari Web.App) di `wwwroot/lib/chartjs/`, `wwwroot/js/chart-interop.js` (render/destroy, locale id-ID, animasi mati, segmen lokal putus-putus + titik kosong), komponen `ChartCanvas`.
- **Beranda** (`Home.razor`):
  - PPL — kartu recording & stok ayam hari ini x/y, kartu per kandang (status tugas → tautan ke form/antrean, populasi, deplesi, FCR, BW, IP, stok pakan + estimasi, flag, "Lihat grafik").
  - Manager — KPI cabang (8 kartu), siklus berisiko, stok ayam siap jual per rentang (endpoint rekap M3) + "Rincian".
  - "Perbarui" di header; offline → salinan tersimpan dengan "Offline — data per …"; perubahan antrean memperbarui status tanpa request.
  - Saat online, Beranda PPL juga mengunduh field-context, daftar siklus, dan performa siklus berjalan agar form & grafik bisa dibuka offline setelah login (§3.6).
  - Logo header tidak lagi gepeng (§14.5).
- **Grafik Produksi** (`/grafik?cycleId&from`, menggantikan placeholder; tautan dari Beranda dan halaman recording siklus): pilih siklus (siklus terpilih yang lebih lama dari halaman pertama tetap ditambahkan), chip metrik, Harian/Mingguan, ringkasan, tabel harian/mingguan (baris lokal miring), **Bandingkan** ≤ 3 siklus.
- **`NetworkErrorHandler`** juga membungkus `System.Net.WebException` (koneksi ditolak tepat setelah jaringan putus) — sebelumnya form recording menampilkan "Terjadi kesalahan pada aplikasi".

### 20.4 Pengujian & verifikasi
- **Test: 602 lulus** — 162 domain, 92 application (+9 `MobileDashboardCalculatorTests`: helper akumulasi, flag, estimasi pakan, KPI), 14 arsitektur, 122 MobileApp.UnitTests (+13: kalkulator = angka test Domain, titik lokal, mingguan, spesifikasi grafik, tugas hari ini, path & cache dashboard/performance, format pakan), 41 integration Web.Api (+2 `MobileDashboardTests`: lingkup PPL/Manager, recording hari ini menghapus flag terlambat, stok & estimasi pakan, tanggal masa depan 400, cabang tanpa akses 403), 171 integration Web.App.
- **API** pada `intiplasma_verify` + data demo (`ppl.bdg` = Inti Lembang, `ppl2.bdg` = Ahmad 2, `manager.bdg` keduanya): KPI Manager cocok dengan hitungan manual (deplesi 3,564%, IP 239,79); stok pakan 2.343 & 18.160,5 kg = `inventory.stock_balances`; rata-rata pakan 798,433 & 736,033 kg = rata-rata 3 recording terakhir di DB → estimasi 2,9 (FeedLow) & 24,7 hari; performa dashboard identik dengan `cycles/{id}/performance`.
- **Emulator API 34**:
  - `manager.bdg`: KPI, recording/stok ayam 0/2, 2 siklus berisiko (Pakan menipis, Stok ayam belum lapor), stok ayam per rentang; grafik BW harian, Pakan mingguan (M1–M5), bandingkan 3 siklus (aktif, selesai, aktif).
  - `ppl2.bdg`: mode pesawat → Beranda dari cache + catatan offline; recording mati 10 / BW 1.990 disimpan → status "Belum terkirim" + badge 1; online → terkirim (DB 1 baris) dan Beranda "Terisi", deplesi 4,52%.
  - `ppl.bdg`: mode pesawat → recording mati 20 / BW 800 → grafik menampilkan segmen putus-putus umur 21 + ringkasan lokal (deplesi 3,23%, FCR 1,468, IP 251); setelah online, angka server sama persis (3,225 / 1,468 / 251,21).

### 20.5 Catatan
- Perbaikan saat verifikasi: tombol "Perbarui" terdorong keluar layar oleh aturan header template (override `.header-right`); form recording error saat offline (`WebException`, lihat §20.3); field-context & performa belum tersimpan untuk user yang baru login (kini diunduh Beranda PPL).
- Ringkasan grafik menampilkan umur pada recording terakhir (mis. 34 hari), sedangkan kartu Beranda menampilkan umur hari ini (35 hari).
- "Recording hari ini x/y" hanya menghitung yang sudah di server; entri antrean tampil sebagai "Belum terkirim" pada kartu kandang.
- Standar breed, flag `BelowStandard`, kartu approval & request pakan menyusul di M5/M6/M9 tanpa mengganti endpoint.

---

## 21. Daftar Task M5 — Breed Standard (urutan)

Disusun 2026-10-11 dari pemetaan kode. Prasyarat: hasil M4 sudah di-commit (`0bcbf75`). Ada **migration** `PhaseM5_BreedStandard`; skema SQLite lokal **tidak** berubah (standar ikut respons `performance` yang sudah di-cache lewat `ApiCache`).

### 21.1 Temuan yang memengaruhi desain
- **Belum ada konsep strain/breed sama sekali** di Domain, Application, maupun DB. Strain saat ini hanya tersirat dari **item DOC** saat chick-in (`StartCycleCommand.Lines[].ItemId`, data demo `DOC-CP707` "DOC Broiler CP 707"); `ProductionCycle` tidak menyimpan item DOC-nya.
- **`ProductionCycle`** dibuat lewat `Plan` (`PlanCycleCommand`: kandang, kontrak, tanggal & populasi rencana, catatan) lalu `Start` (chick-in). Belum ada perintah "ubah siklus", jadi field baru perlu metode & command sendiri agar siklus yang **sudah berjalan** (data lama & demo) bisa diberi standar.
- **Pola master global sudah ada**: `WeightRange` (M3). Aggregate-nya di `Domain/MasterData`, use case dalam satu file `Application/WeightRanges/WeightRangeUseCases.cs`, konfigurasi EF di `Infrastructure/MasterData/MasterDataConfigurations.cs`, endpoint `GET weight-ranges` (`master-data:read`), Web.App `Areas/MasterData/Controllers/WeightRangesController.cs`, menu `master.weight-ranges` di `MenuCatalog`, permission `master-data:read/manage`. Breed Standard bisa mengikuti pola yang sama.
- **Pola impor CSV sudah ada**: `BankReconciliationsController.Import` (upload `IFormFile` atau tempel teks, batas ukuran, parsing di `ImportStatementLinesCommand`). ClosedXML sudah dipakai untuk ekspor.
- **`GET cycles/{id}/performance`** (`GetCyclePerformanceQueryHandler`, `ProductionQueries.cs`) memakai `CyclePerformanceBuilder.BuildDays`, yang **membawa maju BW** dari penimbangan terakhir ke hari-hari tanpa penimbangan. Kalau BW bawaan itu dibandingkan dengan standar umur berikutnya, deviasinya palsu (BW standar naik ±60 g/hari). Karena itu deviasi BW dihitung **hanya pada hari ada penimbangan** (`AverageBodyWeightGram` hari itu terisi), dan "deviasi terakhir" memakai penimbangan terakhir.
- **Dashboard** (`Application/Mobile/GetMobileDashboard.cs`): flag `HighDepletion` memakai ambang absolut `DepletionWarningPercent` = 5 % ("sementara sampai standar breed M5", M-58). `DashboardFlags` dan logikanya ada di `MobileDashboardCalculator` (tanpa DB, diuji unit). Klien menerima salinan ambang lewat `DashboardThresholds`.
- **Klien**: `PerformanceCalculator` (porting rumus, M-65) + `WithLocal` (titik antrean), `WeeklyPerformance` (M-68), `PerformanceCharts` (spesifikasi Chart.js per metrik; pakan kumulatif g/ekor = pakan kg × 1000 ÷ **populasi awal**), `PerformanceChart.razor`, Beranda `Home.razor`. Deviasi titik lokal (offline) harus dihitung di klien, jadi kurva standar perlu ikut dalam respons `performance`.
- **Web.App tab Performance** (`Areas/Production/Views/Cycles/Details.cshtml`): 2 grafik Chart.js inline ("Population & depletion", "Body weight vs feed") + tabel harian; form `Plan.cshtml` (`PlanCycleFormViewModel`).

### 21.2 Keputusan tambahan (disepakati 2026-10-11)
| # | Topik | Usulan |
|---|---|---|
| M-69 | Model Breed Standard | Aggregate `BreedStandard` (master **global**, berlaku untuk semua cabang): `Code`, `Name` (mis. "CP 707 — 2024"), `Strain` (teks bebas, mis. "CP 707", "Cobb 500"), `Notes`, `IsActive`, `IsDefault` (hanya satu standar aktif yang boleh default). Baris per umur `BreedStandardRow`: `AgeDays` (0–70, unik), `BodyWeightGram` (wajib), `Fcr`, `DepletionPercent` (kumulatif), `DailyFeedGram` (g/ekor/hari), `CumulativeFeedGram` (g/ekor); semua kolom selain BW opsional. Validasi: BW tidak turun seiring umur, deplesi & pakan kumulatif tidak turun, minimal 1 baris. Standar yang sudah dipakai siklus dinonaktifkan, tidak dihapus. |
| M-70 | Umur tanpa baris | Standar boleh tidak berisi semua hari (mis. hanya mingguan 0, 7, 14, …). Nilai untuk umur di antara dua baris **diinterpolasi linear**; setelah umur terakhir tidak ada standar (null) dan tidak diekstrapolasi. |
| M-71 | Standar di siklus | `ProductionCycle.BreedStandardId` (nullable). Dipilih di **form Plan** (default terisi standar `IsDefault`) dan bisa **diubah** lewat aksi "Ubah standar" di detail siklus selama status **belum Settled/Cancelled** (tidak memengaruhi keuangan; tercatat di audit). Siklus tanpa standar (data lama) **memakai standar default** saat dibaca, sehingga migration tidak perlu mengisi data. Pemetaan otomatis dari item DOC → backlog. **Revisi 2026-10-11 — snapshot**: saat siklus **Close**, standar efektif (standar siklus, atau default bila kosong) **disalin** ke `ProductionCycle.ClosingStandard` (jsonb: id, kode, nama, strain, baris), sama seperti `ClosingPerformance`. Siklus Closed/Settled membaca snapshot, sehingga perubahan baris master, perubahan default, atau nonaktifnya standar **tidak mengubah** siklus yang sudah tutup. "Ubah standar" pada siklus Closed (belum Settled) memperbarui snapshot; setelah Settled terkunci. Siklus Planned/Active/Harvesting tetap membaca master secara langsung (koreksi master langsung terlihat). Siklus yang sudah Closed/Settled sebelum M5 (tanpa snapshot) menampilkan standar default saat ini dengan label "(default)". |
| M-72 | Menu & hak Web.App | **Master Data → Breed Standards** (konsisten dengan Weight Ranges, sama-sama master global) dengan permission `master-data:read`/`master-data:manage`, **tanpa permission baru**. (§8 menyebut Production → Breed Standards; usulan ini memindahkannya ke Master Data.) Mengubah standar di siklus memakai hak edit siklus yang sudah ada. |
| M-73 | Isi & impor | Form header + tabel baris yang bisa diedit (tambah/hapus baris). **Impor CSV** (upload atau tempel) dengan header `age,bw,fcr,depletion,daily_feed,cum_feed` (pemisah `,` atau `;`, desimal titik) mengganti **seluruh** baris standar setelah lolos validasi (error ditampilkan per baris). **Ekspor** Excel/CSV bisa dipakai sebagai template. Bila `cum_feed` kosong tetapi `daily_feed` lengkap sejak umur awal, kumulatif dihitung otomatis. |
| M-74 | Respons `performance` | Tambah `standard`: `{id, code, name, strain, isDefault, rows[]}` (kurva lengkap, untuk garis sampai umur panen); null bila tidak ada standar sama sekali. Setiap `days[]` mendapat `standard` `{bodyWeightGram, fcr, depletionPercent, cumulativeFeedGram}` (hasil interpolasi) dan `deviation` `{bodyWeightPercent, fcr, depletionPoints, cumulativeFeedPercent}`. **Deviasi BW hanya di hari ada penimbangan** (§21.1). `current` mendapat deviasi dari data terakhir. Endpoint baru `GET breed-standards?activeOnly` (`master-data:read`) untuk pemilih di WebApp/klien. |
| M-75 | Rumus deviasi | BW % = (aktual − standar) ÷ standar × 100; FCR = aktual − standar (selisih absolut, 3 desimal); deplesi = aktual − standar (**poin persen**); pakan kumulatif % = (aktual g/ekor − standar) ÷ standar × 100, dengan aktual = pakan kumulatif kg × 1000 ÷ **populasi awal** (sama dengan definisi grafik M4). |
| M-76 | Status "di bawah standar" | Ambang di konfigurasi `Production:Standard` (dipakai API dan ikut dikirim ke klien dalam respons): `BodyWeightBelowPercent` = **5** (BW lebih rendah dari standar −5 %), `FcrAbove` = **0,05**, `DepletionAbovePoints` = **1,0**. Indikator per metrik: **Sesuai standar** (hijau) atau **Di bawah standar** (merah; untuk FCR & deplesi artinya lebih tinggi dari standar). Siklus dianggap **di bawah standar** bila salah satu metrik melewati ambang pada data terakhir. |
| M-77 | Dashboard | Flag baru **`BelowStandard`** (M-59) di `GET mobile/dashboard` + `cycles[].standard` (nama standar, BW standar pada umur penimbangan terakhir, deviasi BW/FCR/deplesi). `HighDepletion` (ambang absolut 5 %) **tetap ada** sebagai jaring pengaman; bila standar ada dan deplesi melewati standar, cukup tampil `BelowStandard` (tidak dobel). KPI Manager menambah **"Di bawah standar x/y"**. |
| M-78 | Grafik mobile | Garis **standar** abu-abu putus-putus untuk **BW**, **Deplesi %**, **FCR**, dan **Pakan kumulatif g/ekor** (Mati harian & IP tanpa standar). Garis membentang sampai umur hari ini + 7 atau umur terakhir standar (mana yang lebih kecil), agar sumbu tidak memadat. Mode mingguan memakai standar umur 7n. Ringkasan: "BW 1.850 g · standar 1.920 g (−3,6 %)" + badge indikator; tabel harian menambah kolom Std & Dev. Mode **Bandingkan** hanya menampilkan garis standar siklus pertama (legenda "Standar <nama>"). Deviasi titik lokal (offline) dihitung di klien dari `standard.rows`. |
| M-79 | Web.App Performance | Tab Performance: garis standar di grafik BW, kolom Std BW / Dev % / Std FCR / Std deplesi di tabel harian, kartu ringkas deviasi terakhir. Header detail siklus menampilkan nama standar (+ "(default)" bila memakai default). |
| M-80 | Data demo | `DemoDataSeeder` membuat satu standar **"CP 707 (contoh)"** (umur 0–42) berisi angka **ilustrasi** yang ditandai sebagai contoh di `Notes`, dijadikan default, dan dipasang ke siklus demo. Angka standar resmi strain diimpor sendiri oleh pengguna dari manual breeder lewat CSV (M-73). |

### 21.3 Task
1. **Domain**: `Domain/MasterData/BreedStandards/BreedStandard.cs` (+ `BreedStandardRow`, `BreedStandardErrors`): create/update header, `ReplaceRows` (validasi M-69), `SetDefault`, `ValueAt(age)` dengan interpolasi (M-70). `ProductionCycle.BreedStandardId` + `ClosingStandard` (snapshot, M-71) + `SetBreedStandard(Guid?, snapshot)` (ditolak bila Settled/Cancelled; di status Closed memperbarui snapshot); `Close` menerima snapshot standar efektif. Deviasi & status (M-75, M-76) sebagai fungsi murni di Domain, dipakai performance & dashboard.
2. **Infrastructure**: konfigurasi EF (`master.breed_standards`, `master.breed_standard_rows` unik `(breed_standard_id, age_days)`, index unik parsial untuk satu `is_default` aktif), FK `production_cycles.breed_standard_id`; `IApplicationDbContext`; options `Production:Standard` (`appsettings.json` Web.Api & Web.App, `docs/DEPLOY.md`); migration **`PhaseM5_BreedStandard`**.
3. **Application**:
   - `Application/BreedStandards/BreedStandardUseCases.cs`: command `Create`/`Update`/`ImportRows` (CSV)/`SetDefault` + validator, `GetBreedStandardsQuery(activeOnly)`, `GetBreedStandardByIdQuery` (dengan baris).
   - `PlanCycleCommand` menerima `BreedStandardId?`; `SetCycleBreedStandardCommand` baru.
   - `GetCyclePerformanceQuery`: muat standar efektif (siklus, atau default), isi `standard`, `days[].standard/deviation`, dan deviasi `current` (M-74).
   - `GetMobileDashboard`: standar per siklus, flag `BelowStandard`, KPI "di bawah standar x/y" (M-77). `CycleResponse` (list/detail siklus) menambah `breedStandardId/Name`.
4. **Web.Api**: `GET breed-standards`, `GET breed-standards/{id}` (`master-data:read`); body `POST cycles` menerima `breedStandardId`; `PUT cycles/{id}/breed-standard` (hak edit siklus).
5. **Web.App**: Master Data → **Breed Standards** (list, create/edit + grid baris, impor CSV, ekspor, set default) + entri `MenuCatalog`; dropdown standar di form Plan; aksi "Ubah standar" dan garis/kolom standar di tab Performance (M-79); `DemoDataSeeder` (M-80) + `docs/DEMO-DATA.md`.
6. **Test backend**:
   - Unit Domain: validasi baris (BW turun, umur duplikat, deplesi turun), interpolasi & umur di luar rentang, deviasi & status, `SetBreedStandard` per status, snapshot saat Close.
   - Integration: ubah baris master & ganti default setelah siklus Closed → `performance` siklus itu tetap memakai snapshot; siklus Active ikut berubah.
   - Unit Application: parser CSV (header, `;`, error per baris, kumulatif dari harian), flag `BelowStandard` yang tidak dobel dengan `HighDepletion`, deviasi BW hanya di hari penimbangan.
   - Integration Web.Api: `performance` memakai standar default untuk siklus tanpa standar dan standar terpilih setelah diubah; dashboard `BelowStandard`; `GET breed-standards` hanya yang aktif; PPL tanpa hak tidak bisa mengubah standar siklus.
   - Integration Web.App: CRUD + impor CSV, Plan dengan standar, tab Performance dengan standar.
7. **MobileApp.Core**: DTO `BreedStandard`, `StandardPoint`, `PerformanceDeviation`; `PerformanceCalculator` menghitung standar & deviasi titik lokal (porting interpolasi + rumus M-75, angka test sama dengan Domain); `PerformanceCharts` menambah dataset standar (harian/mingguan, satu siklus & bandingkan, M-78); `DashboardService`/`Labels` untuk `BelowStandard` & indikator. Unit test: interpolasi sama dengan Domain, deviasi titik lokal, dataset standar, label flag.
8. **MobileApp UI**: `PerformanceChart.razor` (garis standar, ringkasan deviasi + badge, kolom Std/Dev di tabel); Beranda PPL (badge di kartu kandang) & Manager (KPI "Di bawah standar", alasan di siklus berisiko); nama standar di detail siklus pada halaman Kandang.
9. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo, `ppl.bdg` / `ppl2.bdg` / `manager.bdg`):
   - Web.App: buat standar lewat impor CSV, set default, ubah standar siklus demo; garis & deviasi di tab Performance cocok dengan hitungan manual.
   - API: deviasi hari terakhir = hitungan manual dari DB + baris standar (termasuk umur hasil interpolasi).
   - Emulator: grafik BW/FCR/Deplesi/Pakan dengan garis standar (harian & mingguan); mode pesawat → recording baru dengan BW jauh di bawah standar tampil putus-putus dengan deviasi & badge merah; setelah online angka server sama; Beranda Manager menampilkan `BelowStandard` (kondisinya dibuat di DB verifikasi).
10. **Dokumentasi**: realisasi §22, status §8/§11, `docs/user-guide` WebApp (Breed Standards), RANGKUMAN.

### 21.4 Di luar lingkup M5
- Pemetaan otomatis item DOC → standar saat chick-in → backlog (M-71).
- Standar per cabang/musim, standar suhu/kelembapan/air minum, standar harga → tidak direncanakan.
- Notifikasi push saat siklus di bawah standar → backlog (sama dengan M4).
- Pengaruh standar ke bonus FCR/IP di settlement plasma → tidak diubah (settlement tetap memakai kontrak).
- Edit standar dari mobile → tidak (mobile hanya membaca).

---

## 22. Realisasi Fase M5 — Breed Standard (2026-10-11)

### 22.1 Backend
- **Domain** (`Domain/MasterData/BreedStandards`): aggregate `BreedStandard` (kode, nama, strain, catatan, aktif, default) + `BreedStandardRow` (kunci `(standar, umur)`; baris yang umurnya tetap diperbarui di tempat). Validasi M-69: umur 0–70 unik, BW wajib > 0, BW/deplesi/pakan kumulatif tidak turun; pakan kumulatif dihitung dari pakan harian bila semua baris berurutan per hari dan tidak ada kumulatif. Standar default tidak bisa dinonaktifkan. `BreedStandardSnapshot.ValueAt` (interpolasi linear per kolom, M-70), `StandardDeviation` + `StandardThresholds` (M-75, M-76).
- **`ProductionCycle`**: `BreedStandardId`, `ClosingStandard` (jsonb), `SetBreedStandard` (ditolak saat Settled/Cancelled; saat Closed memperbarui salinan), `Plan(..., breedStandardId)`, `Close(..., standard)` — `CloseCycleCommand` menyalin standar efektif (standar siklus atau default).
- **Migration `PhaseM5_BreedStandard`**: `master.breed_standards`, `master.breed_standard_rows`, `partnership.production_cycles.breed_standard_id` (FK) & `closing_standard`. Satu default dijaga di aplikasi (`SetDefault` melepas default lain), tidak dengan index unik.
- **Application**: `BreedStandardUseCases` (create — standar pertama otomatis default, update + ganti baris, impor CSV, set default, list, detail), `BreedStandardCsv` (parse/tulis; `;` menerima desimal koma; error per baris), `BreedStandardReader` (memuat standar + default untuk query; standar efektif untuk command), `SetCycleBreedStandardCommand`, `PlanCycleCommand.BreedStandardId`, `StandardComparer` (deviasi harian + `StandardStatus`: BW & FCR dari penimbangan terakhir, deplesi & pakan dari recording terakhir). `CycleResponse` + `breedStandardId/Name/IsDefault`.
- **`cycles/{id}/performance`**: `standard` (`id, code, name, strain, isDefault, isFrozen, thresholds, rows[]`), `standardStatus`, `days[].standard` & `days[].deviation` (BW/FCR hanya di hari penimbangan). Siklus tutup memakai salinan (`isFrozen`).
- **`mobile/dashboard`**: `cycles[].standardName` & `cycles[].standard`, flag **`BelowStandard`**, KPI `cyclesBelowStandard`; `HighDepletion` tidak muncul bila deplesi sudah melewati standar.
- **Web.Api**: `GET breed-standards?activeOnly`, `GET breed-standards/{id}` (`master-data:read`), `PUT cycles/{id}/breed-standard` (`cycles:manage`; PPL tidak punya hak ini).
- **Konfigurasi** `Production:Standard` (`BodyWeightBelowPercent` 5, `FcrAbove` 0,05, `DepletionAbovePoints` 1) di `appsettings.json` Web.Api & Web.App + `docs/DEPLOY.md`.

### 22.2 Web.App
- **Master Data → Breed Standards** (`master.breed-standards`): list (default, jumlah umur, umur maks, jumlah siklus), form header + tabel baris (tambah/hapus), CSV ditempel saat membuat, **Import rows (CSV)** (upload/tempel, mengganti semua baris), **Download rows (CSV)**, **Make default**, ekspor daftar.
- **Cycles**: pilihan standar di form Plan (kosong = default), "Breed standard" di Overview, tab **Performance**: kartu standar + status On/Below standard + **Change standard**, garis standar BW & deplesi di grafik, kolom Std depl. / Std BW / BW dev. / Std FCR (merah bila melewati ambang).
- **DemoDataSeeder**: standar `CP707-CONTOH` "CP 707 (contoh)" — baris mingguan 0–42 diturunkan dari kurva data dummy, ditandai bukan angka resmi — dipilih semua siklus demo; `docs/DEMO-DATA.md` & user guide (03 Master Data, 05 Production) diperbarui.

### 22.3 MobileApp.Core & MobileApp
- **Core**: DTO `PerformanceStandard`, `StandardRow`, `StandardThresholds`, `StandardStatus`; `CyclePerformanceReport.Standard/StandardStatus`, `DashboardCycle.StandardName/Standard`, `DashboardKpi.CyclesBelowStandard`, `Cycle.BreedStandardName`. `StandardCalculator` (porting interpolasi & deviasi; `Latest` & `DayDeviation` juga untuk recording antrean). `PerformanceCharts.Build(..., standard)`: dataset standar (`IsStandard`) untuk BW, deplesi, FCR, pakan (satu siklus: kg kumulatif di sumbu kanan; bandingkan: g/ekor milik siklus pertama), harian diperpanjang 7 hari ke depan, mingguan pada umur 7n. `IdFormat.Deviation`, label flag "Di bawah standar".
- **MobileApp**: `chart-interop.js` menggambar standar sebagai garis abu-abu putus-putus tanpa titik; **Grafik**: keterangan garis standar, kartu standar (BW umur penimbangan, FCR, deplesi, pakan kumulatif vs standar + badge Sesuai/Di bawah standar), kolom Std BW / Dev BW / Std depl. / Std FCR (harian) & Std BW (mingguan); **Beranda** PPL baris "vs standar" (merah bila di bawah), Manager kartu KPI "Di bawah standar x/y" dan alasan di siklus berisiko; nama standar di daftar siklus Kandang.

### 22.4 Pengujian & verifikasi
- **Test: 628 lulus** — 170 domain (+8 `BreedStandardTests`), 100 application (+6 handler/CSV/perbandingan, +2 dashboard), 14 arsitektur, 129 MobileApp.UnitTests (+7 `StandardTests`: interpolasi & deviasi = angka test Domain, recording antrean di bawah standar, garis standar harian/mingguan/pakan kg), 43 integration Web.Api (+2: default → standar siklus, PPL 403, standar nonaktif 400, dashboard `BelowStandard`, list aktif; siklus tutup tetap memakai salinan setelah master diubah), 172 integration Web.App (+1: buat dari CSV, impor `;`/desimal koma, CSV salah ditolak, ekspor, edit, default tidak bisa dinonaktifkan, Plan dengan standar, Change standard, tab Performance).
- **API** pada `intiplasma_verify` + data demo: standar `CP707-CONTOH` default dengan 7 baris, dipilih 8 siklus; Inti Lembang umur 20: standar BW 699,72 g / FCR 1,74 / deplesi 3,114% / pakan 1.175,9 g = interpolasi manual baris 14–21; aktual (BW 692, deplesi 2,975%, pakan 1.136,2 g/ekor) = query DB → deviasi BW −1,103%, tidak di bawah standar.
- **Emulator API 34**:
  - `ppl.bdg`: Beranda "vs standar: BW -1,1% · FCR -0,048 · deplesi -0,14 poin"; Grafik BW harian dengan garis standar sampai umur 27, Pakan mingguan dengan standar kg kumulatif.
  - Mode pesawat → recording umur 21 (mati 40, BW 650 g) → titik lokal putus-putus, kartu standar "Di bawah standar" (BW -14,1% merah, FCR +0,082 merah, deplesi +0,28 poin); online → terkirim, server sama persis (−14,069% / +0,082 / +0,275) dan Beranda menampilkan flag "Di bawah standar".
  - `manager.bdg`: KPI "Di bawah standar 1/2", Inti Lembang di siklus berisiko dengan baris "vs standar".

### 22.5 Catatan
- Siklus yang sudah Closed/Settled sebelum M5 tidak punya salinan standar → menampilkan standar default saat ini (label default).
- Garis standar mingguan memakai nilai umur 7n, sedangkan minggu berjalan memakai hari terakhir yang tercatat (mis. umur 20 di minggu 3).
- Angka standar demo diturunkan dari kurva data dummy agar siklus demo berada di sekitar standar; FCR-nya turun seiring umur (tidak realistis) — impor standar resmi breeder sebelum dipakai.

---

## 23. Daftar Task M6 — Modul Approval Berjenjang (urutan)

Disusun 2026-10-12 dari pemetaan kode. Prasyarat: hasil M5 sudah di-commit (`094197e`). Ada **migration** `PhaseM6_ApprovalWorkflow`; skema SQLite lokal **tidak** berubah (inbox, pengajuan & timeline lewat `ApiCache`). Jenis dokumen M6: **Farmer, FarmerChange, Coop, Contract** (RecordingRevision & FeedMutation di M8).

### 23.1 Temuan yang memengaruhi desain
- **Belum ada modul approval sama sekali** (Domain/Application/Web.App/mobile). Maker-checker yang ada (PO, PV, jurnal, kas, settlement, SO) ada di aggregate masing-masing (`Approve(approverId, utcNow)` + `SelfApprovalNotAllowed`) dan **tidak** diubah (M-34).
- **Farmer & Coop hanya punya `IsActive`** (tanpa status; Farmer tanpa domain event). `IsActive` bisa diubah bebas lewat `Update(..., isActive)` (switch di form Edit Web.App). Banyak tempat sudah bergantung pada `IsActive`: `Coop.Create` menolak peternak nonaktif (`Coop.cs:79`), `ProductionCycle.Plan` mengecek kandang & peternak aktif (`ProductionCycle.cs:276,281`), lookup Web.App (`LookupController` :77, :259), field-context & dashboard mobile.
- **`PartnershipContract`**: `ContractStatus { Draft, Active, Inactive }` (disimpan sebagai string), `UpdateTerms` hanya Draft, `Activate()` (validasi `PriceContractIncomplete`) memunculkan `ContractActivatedDomainEvent` yang **tidak punya handler**. Kontrak tidak punya `FarmerId` → cabang dokumen = `BranchId` kontrak.
- **Gudang kandang `GK-`** dibuat `CoopCreatedDomainEventHandler` (outbox, idempotent). Untuk M6 pemicunya pindah ke approve.
- **Satu `SaveChangesAsync` = satu transaksi**: `IApplicationDbContext` tidak membuka transaksi eksplisit; keputusan level terakhir + efek ke dokumen cukup diubah pada entity ter-*track* lalu disimpan sekali → efek sinkron dalam transaksi yang sama (§5.2) tanpa API transaksi baru. Concurrency `xmin` (`Version`) sudah otomatis untuk setiap `AggregateRoot` → keputusan bersamaan pada level yang sama → `409`.
- **Permission `approvals:decide` sudah ada** (`Permissions.cs:272`) dan sudah di role Manager (seeder M0); mobile sudah punya placeholder **Approval** (`/fitur/approval`, bottom bar Manager) dan **Pengajuan Saya** (`/fitur/pengajuan`) di `AppFeatures`.
- **User Web.App memakai Akses Menu, bukan permission**; permission hanya datang dari role API. Kriteria "approver yang sah" harus jelas untuk kedua klien (M-86).
- **Web.App**: Farmers & Coops **tidak punya halaman Details** (hanya Index + Form; simpan → kembali ke Index); Contracts punya Details (kartu bertumpuk) dengan Activate/Deactivate via `[WorkflowAction]` + `IWorkflowActionService` (`DirectWorkflowActionService`). Partial `_DocumentActions` (belum dipakai) & `_ReasonModal` siap dipakai; `_StatusBadge` sudah memetakan Pending/Approved/Rejected. Belum ada lookup "semua user" (baru `FieldOfficers`); pola baris dinamis `collection.js` (Breed Standards) bisa dipakai untuk editor level. Belum ada badge/notifikasi di header (`_Header.cshtml`); dashboard punya kartu "Waiting for action".
- **Pemilihan approver**: template query terbaik `FieldOfficerRules` (EF) & `GetFieldOfficers` (Dapper) — user aktif + permission via role + profil Akses Cabang.
- **Audit**: `IAuditTrail.Record` + `IAuditedCommand` (contoh `FieldOfficerAssignments.cs:171`) untuk perubahan alur & reassign approver.
- **Dampak ke kode lama** (breaking change, §10): `contracts/{id}/activate` dipakai `ContractsController`, `DemoDataSeeder.CreateContractAsync`, `TestData.cs`, test domain kontrak; create farmer/coop lalu langsung dipakai di integration test Web.Api (`FieldOfficerScopeTests`, `FieldScenarioTest`, `AttachmentsTests`) dan Web.App (`ProductionTests`, `SalesTests`, `FieldOfficerTests`, `CostingTests`, `ProductionTests.BreedStandards`) → helper test "buat + setujui".

### 23.2 Keputusan tambahan (disepakati 2026-10-12)
| # | Topik | Usulan |
|---|---|---|
| M-81 | Status Farmer & Coop | Tambah `RegistrationStatus { Draft, PendingApproval, Rejected, Approved }` **terpisah** dari `IsActive`. `IsActive` hanya bisa `true` bila `Approved` (switch Active/Inactive di form hanya tampil saat Approved). Dengan begitu semua filter `IsActive` yang ada (lookup, Plan siklus, field-context, dashboard) otomatis mengecualikan data yang belum disetujui tanpa diubah satu per satu. Status tampilan gabungan: **Menunggu approval / Ditolak / Draft / Aktif / Nonaktif**. Migration: data lama → `Approved` (IsActive tetap). |
| M-82 | Create = simpan + ajukan | `POST farmers/coops` (API & Web.App) menyimpan **dan** mengajukan dalam satu `SaveChanges`. Bila alur belum diatur (M-31) seluruh create **gagal** (tidak ada data tersimpan) dengan `Approvals.FlowNotConfigured` → pesan **"Alur approval belum dikonfigurasi untuk {jenis dokumen} di cabang {cabang}. Hubungi administrator."** (mobile & Web.App; juga untuk `submit`). Berlaku juga untuk admin Web.App. |
| M-83 | Edit selama proses | **PendingApproval terkunci** (tidak bisa diedit); pengaju **membatalkan** dulu → status **Draft** → edit → `submit`. **Rejected** & **Draft** bisa diedit lalu `submit` (pengajuan baru, mulai level 1). **Approved** mengikuti M-14. Lampiran tetap bisa diubah di semua status (sama dengan kontrak sekarang). |
| M-84 | FarmerChange (NIK & rekening) | `PUT farmers/{id}` pada peternak **Approved**: field lain langsung disimpan; bila **NIK atau rekening bank** berubah, nilai baru disimpan di `Farmer.PendingChange` (jsonb: nik, bankAccount) + pengajuan `FarmerChange`, nilai lama tetap dipakai. Hanya **satu** perubahan tertunda; edit NIK/rekening lagi selama Pending → ditolak (`Farmers.ChangePending`, batalkan dulu). Approved → diterapkan; Rejected/Cancelled → `PendingChange` dibuang. Pada status Draft/Rejected NIK/rekening diedit langsung (belum pernah disetujui). |
| M-85 | Kontrak | `ContractStatus` ditambah `PendingApproval` & `Rejected`. `POST contracts/{id}/submit` dari Draft/Rejected (validasi `PriceContractIncomplete` dijalankan saat submit **dan** saat approve). `UpdateTerms` boleh pada Draft & Rejected. Cancel → Draft. Approved → `Activate()` (event yang ada tetap). `activate` **dihapus** dari API & Web.App; **Deactivate tetap langsung** (hak `contracts:manage` / Edit), tidak lewat approval. Create kontrak tetap Draft (tidak otomatis diajukan, karena kontrak disusun bertahap). |
| M-86 | Siapa bisa jadi approver | Approver = **user aktif yang punya permission `approvals:decide`** (lewat role API; Administrator otomatis). Pemilih di Approval Flows hanya menampilkan user ini. Saat memutuskan dicek ulang: aktif, punya `approvals:decide`, ada di daftar level berjalan, bukan pengaju, belum memutuskan level lain, punya akses cabang dokumen (M-33). Di Web.App, menu **Approval Inbox** perlu hak View (Akses Menu) **dan** user harus memenuhi syarat di atas; user Web.App yang ingin menjadi approver diberi role API berisi `approvals:decide` (layar Users sudah bisa memberi role). |
| M-87 | Kandang & peternak | Kandang hanya bisa **diajukan** bila peternaknya Approved atau PendingApproval (Draft/Rejected ditolak). **Approve kandang** ditolak (`Coops.FarmerNotApproved`) selama peternak belum Approved → approver menunggu atau menolak. Gudang `GK-` dibuat oleh event baru **`CoopApprovedDomainEvent`** (handler lama dipindah; `CoopCreatedDomainEvent` tidak lagi membuat gudang). |
| M-88 | Penyimpanan pengajuan | Schema `approval`: `approval_flows` + `approval_flow_levels` (`approver_user_ids uuid[]`); `approval_requests` (dokumen, cabang, kode/judul, `summary`, `payload` jsonb untuk tampilan "lama → baru" FarmerChange, pengaju, status, level berjalan, jumlah level) + `approval_request_levels` (snapshot nama & approver) + `approval_decisions`. Partial unique index: satu `Pending` per (`document_type`, `document_id`); satu alur aktif per (`document_type`, `branch_id`) dengan `NULLS NOT DISTINCT` (PostgreSQL 15+). Tidak memakai penomoran dokumen (nomor = kode farmer/kandang/kontrak). |
| M-89 | Respons dokumen | `FarmerResponse`, `CoopResponse`, `ContractResponse` menambah `registrationStatus` (kontrak: `status` yang diperluas), `approval` `{requestId, status, currentLevel, levelCount, currentLevelName, lastRejectReason}` (pengajuan terakhir) dan, untuk farmer, `pendingChange`. List menerima filter `status`. Timeline lengkap lewat `GET approvals?documentType&documentId`. |
| M-90 | Endpoint & hak | `approvals/inbox` (paging, filter `documentType`), `inbox/count` (per jenis), `approvals/{id}`, `/approve`, `/reject`: **`approvals:decide`**. `approvals/mine` & `/cancel`: semua user login (cancel hanya pengaju; detail juga boleh untuk pengaju). `GET approvals?documentType&documentId`: hak baca dokumen terkait (`farmers:read`/`contracts:read`) + lingkup PPL/cabang. `submit`: `farmers:manage` / `contracts:manage`. Inbox **tidak** memakai lingkup PPL, tetapi tetap lingkup cabang. |
| M-91 | Web.App | Menu baru **Administration → Approval Flows** (`admin.approval-flows`, Crud), **Approval Inbox** (`approvals.inbox`, View) dan **Approval Requests** (semua pengajuan, filter status/jenis/cabang, reassign approver; `approvals.requests`, View/Edit) dalam grup baru **Approvals**. Farmers & Coops mendapat **halaman Details** (data + `_ApprovalTimeline` + Submit/Cancel), simpan → Details. Contracts: Activate → **Submit** + timeline. Badge jumlah inbox di **header** (view component) dan baris di kartu "Waiting for action" dashboard. Aksi Approve/Reject lewat `[WorkflowAction]` + `IWorkflowActionService` (Reject memakai `_ReasonModal`). |
| M-92 | Mobile | **Approval** (Manager, bottom bar + badge `inbox/count`): tab per jenis + jumlah, item (jenis, kode/nama, cabang, pengaju, "Level n dari N", lama menunggu); detail (ringkasan dokumen, "lama → baru" untuk FarmerChange, timeline, **Setujui** dengan catatan opsional / **Tolak** dengan alasan wajib lewat `ConfirmSheet`). **Pengajuan Saya** (semua peran): filter status, batal selama Pending. Timeline + badge status di detail Peternak/Kandang/Kontrak; list menampilkan badge Menunggu/Ditolak. Inbox & pengajuan **di-cache untuk dilihat offline**, aksi wajib online (M-4). |
| M-93 | Dashboard | `GET mobile/dashboard` menambah `approvals`: `waitingForMe[]` (jenis, jumlah; hanya bila punya `approvals:decide`) dan `mySubmissions` (pending + rejected 7 hari terakhir dengan alasan). Beranda Manager: kartu "Approval menunggu saya" per jenis; Beranda PPL: kartu "Pengajuan saya". |
| M-94 | Data demo | `DemoDataSeeder` membuat user demo **`kacab`** (Kepala Cabang, role berisi `approvals:decide`) dan **alur default 2 level** untuk keempat jenis (L1 "Manager Unit" = Siti/checker, L2 "Kepala Cabang" = kacab). Peternak, kandang & kontrak demo diajukan admin lalu disetujui L1 & L2 (`SendAsAsync`); satu peternak demo dibiarkan **Pending di level 2** dan satu kandang **Rejected** sebagai contoh. |
| M-95 | Notifikasi | Hanya badge/hitungan (header Web.App, bottom bar & Beranda mobile). Push FCM, email & eskalasi otomatis → backlog (delegasi masuk M6, M-98). |
| M-96 | Nama & jumlah level | Nama level **teks bebas** (mis. "Verifikasi", "Kepala Produksi", "Kepala Cabang", "Pusat"); semua level **berperilaku sama** (setujui / tolak dengan alasan), tidak ada tipe "Verifier" khusus. Jumlah level **dinamis 1–5 per alur** (cabang A 2 level, cabang B 4 level, dst.); di form level & approver bisa **ditambah/dihapus**. Perubahan berlaku untuk pengajuan baru (M-32). Approver boleh dari cabang lain (mis. user Pusat) asalkan Akses Cabangnya mencakup cabang dokumen. |
| M-97 | Setup alur saat Create Branch | Form **Create Branch** (Web.App) mendapat bagian opsional **"Alur approval"**: editor level dinamis (diisi awal dari alur default bila ada) + pemilih approver, opsi **"Pakai untuk semua jenis dokumen"** (Farmer, FarmerChange, Coop, Contract sekaligus) atau **"Pakai alur default"**. Disimpan sebagai alur cabang biasa (bisa diubah per jenis di Approval Flows). Approver yang Akses Cabangnya belum mencakup cabang baru → peringatan + opsi menambahkan cabang ke profil Akses Cabangnya. Edit Branch menampilkan ringkasan alur + tautan ke Approval Flows; list Branches memberi peringatan untuk cabang tanpa alur (cabang & default kosong). |
| M-98 | Delegasi (cuti) | Entity `ApprovalDelegation`: `DelegatorUserId`, `DelegateUserId`, `FromDate`–`ToDate` (tanggal Asia/Jakarta, inklusif, maks 90 hari), `Reason`, `RevokedAt`. Diatur **oleh approver sendiri** (Web.App menu user "My Delegations", mobile Lainnya → **Delegasi Approval**) atau oleh admin untuk user lain (Web.App Approvals → **Delegations**, tercatat di audit). Selama periode aktif, pengajuan yang menunggu delegator **juga muncul di inbox penerima** (delegator tetap bisa memutuskan sendiri). Keputusan mencatat `DecidedBy` = penerima dan `OnBehalfOf` = delegator; timeline "Budi a.n. Tommy". Aturan: penerima user aktif dengan `approvals:decide`, punya akses cabang dokumen (dicek saat memutuskan), **bukan pengaju**; aturan "satu user satu level" berlaku untuk penerima **dan** delegator; delegasi **tidak berantai** (delegasi milik penerima tidak ikut diteruskan); periode tidak boleh tumpang tindih untuk delegator yang sama; bisa dicabut kapan saja; berakhir otomatis. API: `GET/POST approvals/delegations`, `POST approvals/delegations/{id}/revoke` (`approvals:decide`). |
| M-99 | Approver resign & macet | (a) **Ganti Approver massal** — Web.App Approvals → **Replace Approver**: pilih user lama → pengganti (aktif, `approvals:decide`) → pratinjau alur & pengajuan Pending terdampak → ganti sekaligus di semua alur (pengajuan baru) **dan** level yang belum diputuskan pada pengajuan Pending (snapshot), opsi cabang tertentu saja; tercatat di audit. (b) **Peringatan saat Deactivate user**: daftar alur & pengajuan Pending tempat user itu approver + tautan ke Replace Approver. (c) **Penanda "Macet"**: level yang **semua approvernya nonaktif / tanpa `approvals:decide`** (delegasi aktif dihitung sebagai approver) ditandai di Approval Flows, Approval Requests (filter "Macet") dan baris "Stuck approvals" di kartu dashboard Web.App. Keputusan yang sudah dibuat user nonaktif tetap sah di timeline. |

### 23.3 Task
1. **Domain** (`Domain/Approvals`):
   - `ApprovalFlow` (+ `ApprovalFlowLevel`, errors): level 1–5 berurutan, nama wajib, approver ≥ 1 & unik per level, aktif/nonaktif.
   - `ApprovalRequest` (+ `ApprovalRequestLevel`, `ApprovalDecision`, `ApprovalDocumentType`, `ApprovalStatus`, errors, domain event `Submitted/LevelApproved/Approved/Rejected/Cancelled`): `Submit(flow snapshot)`, `Approve(userId, note)`, `Reject(userId, reason)`, `Cancel(userId)`, `ReassignApprover(level, from, to)`; aturan M-33; keputusan `OnBehalfOf` (M-98).
   - `ApprovalDelegation` (M-98): periode, cabut, tidak tumpang tindih, bukan diri sendiri.
   - `Farmer`/`Coop`: `RegistrationStatus` (M-81), `MarkSubmitted/Approve/Reject/ReturnToDraft`, `Farmer.PendingChange` + `ProposeChange/ApplyPendingChange/DiscardPendingChange` (M-84); `CoopApprovedDomainEvent` (M-87). `PartnershipContract`: status baru, `Submit/Approve/Reject/ReturnToDraft` (M-85).
2. **Infrastructure**: schema `approval` (M-88), konfigurasi EF + kolom `registration_status`, `pending_change`; `IApplicationDbContext`; handler gudang kandang dipindah ke `CoopApprovedDomainEvent`; migration **`PhaseM6_ApprovalWorkflow`** (data lama Approved); `TestDbContext`.
3. **Application** (`Application/Approvals`):
   - `IApprovalService.SubmitAsync` (cari alur cabang → default → `FlowNotConfigured`, snapshot), `IApprovalHandler` per jenis (Farmer, FarmerChange, Coop, Contract) + registry.
   - Command `ApproveRequest`, `RejectRequest`, `CancelRequest`, `ReassignApprover` (audit); query `GetMyApprovalInbox`, `GetMyApprovalInboxCount`, `GetMySubmissions`, `GetApprovalRequest`, `GetDocumentApprovals`, `GetApprovalRequests` (Web.App); CRUD `ApprovalFlow` + `GetApprovalFlows` + `GetApproverCandidates` (M-86); `CreateDelegation`/`RevokeDelegation`/`GetDelegations` (inbox & keputusan memperhitungkan delegasi aktif, M-98); `ReplaceApproverCommand` + `GetApproverUsage` (pratinjau & peringatan Deactivate) + deteksi level macet (M-99).
   - Ubah `CreateFarmer`/`CreateCoop` (M-82), `UpdateFarmer` (M-83, M-84), `UpdateCoop`, `UpdateContract` (Rejected); command baru `SubmitFarmer/Coop/Contract`; `Coop.Create` menerima peternak Approved/Pending (M-87); `ActivateContractCommand` dihapus dari jalur publik (aktivasi oleh handler approval). Response M-89; dashboard M-93.
   - Kode error baru: `Approvals.FlowNotConfigured`, `NotApprover`, `RequesterCannotDecide`, `AlreadyDecidedOtherLevel`, `NotPending`, `ReasonRequired`, `Farmers.ChangePending`, `Coops.FarmerNotApproved`, `…NotEditable`.
4. **Web.Api**: `ApprovalEndpoints` (M-90), `POST farmers|coops|contracts/{id}/submit`, hapus `contracts/{id}/activate`, filter `status` di list.
5. **Web.App**: Approval Flows (list per jenis & cabang, form level dinamis + pemilih approver Tom-Select `/Lookup/Approvers`, duplikasi default → cabang, peringatan approver nonaktif/tanpa hak), Approval Inbox (tab per jenis, detail + timeline, Approve/Reject), Approval Requests (+ reassign), bagian "Alur approval" di Create Branch + ringkasan di Edit Branch + peringatan di list Branches (M-97), Delegations + My Delegations (M-98), Replace Approver + peringatan Deactivate user + penanda Macet (M-99), `_ApprovalTimeline`, Details Farmer/Coop, Submit/Cancel di Farmer/Coop/Contract, badge header & dashboard (M-91), `MenuCatalog`; `DemoDataSeeder` (M-94) + `docs/DEMO-DATA.md`.
6. **Test backend**:
   - Unit Domain: alur level 1–5 & approver ≥ 1; pengajuan berurutan, semua level → Approved, reject di level mana pun → Rejected, bukan approver / pengaju / dua level → ditolak, cancel hanya Pending, reassign hanya level belum diputuskan; status Farmer/Coop/Contract; `PendingChange`; `IsActive` hanya saat Approved.
   - Unit Application: pemilihan alur (cabang → default → error), create gagal total tanpa alur, efek per jenis (gudang via event, NIK/rekening diterapkan, kontrak Active), approve kandang ditolak bila peternak belum Approved, inbox hanya level berjalan & cabang; penyesuaian test lama (`FieldOfficerScopeTests` dll).
   - Integration Web.Api: alur 2 level end-to-end (PPL ajukan farmer + kandang + kontrak → Manager L1 → kacab L2 → Active & gudang `GK-`), reject di L2 → Rejected → edit → submit ulang mulai L1, Manager mencoba memutuskan L2 → ditolak, dua approval bersamaan → 409, delegasi (penerima memutuskan a.n. delegator; setelah periode habis/dicabut → ditolak; pengaju tidak bisa menjadi penerima yang memutuskan), Replace Approver mengubah alur & pengajuan Pending, FarmerChange (nilai lama sampai disetujui), `activate` → 404, PPL tanpa `approvals:decide` → 403 di inbox, `mine` & cancel.
   - Integration Web.App: menu baru terbuka (`Admin_Should_OpenEveryReleasedMenu`), Approval Flows CRUD, approve dari Inbox hanya oleh approver level berjalan, Contracts Submit, Farmers/Coops Details; test lama memakai helper "buat & setujui".
7. **MobileApp.Core**: DTO `ApprovalRequest`, `ApprovalLevel`, `ApprovalDecision`, `ApprovalInboxCount`; `ApprovalsApi` (inbox, count, mine, detail, approve/reject/cancel); `Labels` (jenis, status, "Level n dari N", status registrasi) & `ErrorMessages`; `AppFeatures` (Approval & Pengajuan Saya tersedia); dashboard M-93. Unit test: label & tone, pemetaan error baru, `AppFeatures`, `ApprovalsApi` (cache, aksi online).
8. **MobileApp UI**: halaman Approval (inbox bertab + detail + Setujui/Tolak), Pengajuan Saya (+ batal), komponen `ApprovalTimeline`, badge di bottom bar, status & timeline di detail Peternak/Kandang/Kontrak, badge di list, kartu Beranda (M-92, M-93), Lainnya → **Delegasi Approval** (list, buat, cabut; M-98).
9. **Verifikasi** (emulator API 34, `intiplasma_verify` + data demo, `ppl.bdg` / `ppl2.bdg` / `manager.bdg` (L1) / `kacab.bdg` (L2)):
   - Web.App: buat cabang baru dengan alur approval (M-97); atur alur 2 level cabang BDG; inbox & badge; reassign approver; Details farmer/coop dengan timeline; nonaktifkan approver tunggal → peringatan + penanda Macet → Replace Approver → pengajuan kembali muncul di inbox pengganti.
   - Delegasi: `kacab.bdg` mendelegasikan ke user lain (mobile) → pengajuan level 2 muncul di inbox penerima → setujui → timeline "a.n. kacab"; cabut → hilang dari inbox penerima.
   - API (skrip): `ppl.bdg` mengajukan farmer + kandang + kontrak (form mobile baru di M7) dan perubahan NIK.
   - Emulator: `manager.bdg` badge & inbox → setujui L1; `kacab.bdg` setujui L2 → peternak/kandang Aktif, gudang `GK-` terbentuk (cek DB); reject di L2 → `ppl.bdg` melihat Ditolak + alasan di detail & Pengajuan Saya → ajukan ulang mulai L1; `manager.bdg` mencoba L2 → pesan Indonesia; batal pengajuan dari Pengajuan Saya; FarmerChange "lama → baru"; mode pesawat → inbox tetap terbaca, aksi menampilkan pesan offline.
10. **Dokumentasi**: realisasi §24, status §8/§11, `docs/DEPLOY.md` (langkah wajib mengatur alur approval sebelum input, M-31), user guide (02 Administration: Approval Flows; 03 Kemitraan: Submit/Details; bab baru Approval), RANGKUMAN.

### 23.4 Di luar lingkup M6
- Revisi recording & mutasi pakan lewat approval → **M8**. Form add/edit peternak/kandang/kontrak di mobile → **M7**.
- Endpoint API untuk konfigurasi Approval Flows → tidak (hanya Web.App, §4.1).
- Push/email, eskalasi otomatis, SLA → backlog (M-95).
- Alur bersyarat per nilai, approval paralel dalam satu level (semua harus setuju) → tidak direncanakan (M-29: satu approver cukup per level).
- Dokumen keuangan (PO, PV, kas, jurnal, settlement, SO over-limit) tetap maker-checker lama (M-34).

---

## 24. Realisasi Fase M6 — Modul Approval Berjenjang (2026-10-12)

### 24.1 Backend
- **Domain** (`Domain/Approvals`): `ApprovalFlow` (+ `ApprovalFlowLevel`; 1–5 level, nama wajib, ≥ 1 approver, level diperbarui di tempat, `ReplaceApprover`), `ApprovalRequest` (+ `ApprovalRequestLevel` salinan level, `ApprovalDecision`; `Approve/Reject/Cancel/ReassignApprover/ReplaceApprover`, aturan M-33: berurutan, pengaju tidak memutuskan, satu user satu level, alasan wajib saat tolak), `ApprovalActor` (keputusan **a.n.** delegator, M-98), `ApprovalDelegation` (periode tanggal Jakarta, maks 90 hari, cabut), event `Submitted/LevelApproved/Approved/Rejected/Cancelled` (outbox, tanpa handler — siap untuk notifikasi). `ApprovalErrors.FlowNotConfigured` berbahasa Indonesia: *"Alur approval belum dikonfigurasi untuk {jenis} di cabang {cabang}. Hubungi administrator."* (M-82).
- **Farmer/Coop**: `RegistrationStatus` (Draft/PendingApproval/Rejected/Approved) terpisah dari `IsActive`, `IsActive` hanya bisa true saat Approved (M-81); `Submit/Approve/Reject/ReturnToDraft`; PendingApproval terkunci (M-83). `Farmer.PendingChange` (jsonb) + `Update` mengembalikan apakah NIK/rekening diusulkan (M-84). `Coop.Create/Submit` menerima peternak Approved/Pending, `Coop.Approve` menunggu peternak Approved dan memunculkan **`CoopApprovedDomainEvent`** (gudang `GK-` dibuat di sini; handler juga menerima `CoopCreatedDomainEvent` lama hanya bila kandang Approved) (M-87).
- **Kontrak**: status `PendingApproval` & `Rejected`, `Submit/Approve/Reject/ReturnToDraft` (cek `PriceContractIncomplete` saat submit & approve), `UpdateTerms` untuk Draft/Rejected; `Activate()` dihapus (M-85).
- **Migration `PhaseM6_ApprovalWorkflow`**: schema `approval` (`approval_flows`, `approval_flow_levels` `uuid[]`, `approval_requests` + partial unique 1 Pending per dokumen, `approval_request_levels`, `approval_decisions`, `approval_delegations`), unique alur aktif per (jenis, cabang) `NULLS NOT DISTINCT`; `master.farmers.registration_status` + `pending_change`, `master.coops.registration_status` — **data lama = `Approved`**.
- **Application** (`Application/Approvals`): `IApprovalService.SubmitAsync` (alur cabang → default → `FlowNotConfigured`; satu save dengan dokumen), `IApprovalHandler` per jenis (efek di unit of work yang sama; gagal → tidak ada yang disimpan), `ApprovalDecider` (aktor sebagai approver atau delegasi aktif hari ini, cek user aktif + `approvals:decide` + akses cabang), command Approve/Reject/Cancel/Reassign, CRUD alur + `SetUpBranchApprovalFlows` (M-97, opsi tambahkan cabang ke profil Akses Cabang approver), `ReplaceApprover` + `GetApproverUsage` (M-99), delegasi (Create/Revoke/Get), query Dapper inbox/count/mine/detail/daftar (penanda **Stuck**: tidak ada approver aktif ber-hak, delegasi aktif dihitung), `SubmitFarmer/Coop`, timeline per dokumen dengan cek lingkup. Create farmer/coop = simpan + ajukan; update farmer mengajukan `FarmerChange`.
- **Respons** (M-89): `FarmerResponse`/`CoopResponse` + `registrationStatus`, farmer + `pendingChange`; ketiganya + kolom datar `approvalRequestId/DocumentType/Status/Level/LevelCount/LevelName/RejectReason` (pengajuan terakhir). **Dashboard** (M-93) + `approvals { waitingForMe, waitingByDocumentType, myPending, myRejected[] }`.
- **Web.Api** (M-90): `approvals/inbox`, `/inbox/count`, `/{id}/approve`, `/reject`, `delegations` (GET/POST), `/delegations/{id}/revoke`, `delegates` — `approvals:decide`; `approvals/mine`, `/{id}`, `/{id}/cancel` — user login; `POST farmers|coops|contracts/{id}/submit`, `GET farmers|coops|contracts/{id}/approvals`; `contracts/{id}/activate` dihapus.
- **DemoDataSeeder** (M-94): user `kacab@intiplasma.local` (Tommy, role Manager), alur default 2 level (Siti → Tommy) untuk 4 jenis; semua peternak/kandang/kontrak demo diajukan admin & disetujui 2 level; contoh PTN-BDG-004 menunggu level 2 dan KDG-BDG-05 ditolak di level 1.

### 24.2 Web.App
- Grup menu baru **Approvals**: **Approval Inbox** (`approvals.inbox`, tab per jenis + jumlah, detail + timeline, Approve dengan catatan / Reject lewat `_ReasonModal`, via `[WorkflowAction]`), **Approval Requests** (`approvals.requests`: filter status/jenis/cabang/**Stuck only**, reassign approver satu level — hak Edit), **Delegations** (`approvals.delegations`, admin). Halaman tanpa menu (AuthenticatedOnly) **My Submissions** (+ batal) dan **My Approval Delegations**, ditautkan dari menu avatar.
- **Administration → Approval Flows** (`admin.approval-flows`): daftar per jenis & cabang (peringatan bila jenis tanpa alur default, approver ✗ & level **Stuck**), form level dinamis (`collection.js` + Tom-Select multi `/Lookup/Approvers`), salin ke cabang, **Replace approver** (pratinjau alur & pengajuan Pending, opsi satu cabang).
- **Branches**: Create Branch bagian **Approval flow** (default / alur cabang sendiri, level diisi awal dari alur default, jenis dokumen, tambah akses cabang approver), Edit Branch ringkasan alur per jenis, badge **No approval flow** di daftar (M-97). **Users** Details: kartu peringatan approver + tautan Replace approver dan konfirmasi Deactivate yang menyebutnya (M-99b).
- **Farmers/Coops**: halaman Edit berfungsi sebagai detail (`Details` = alias), kartu **Approval** (status, Submit for approval, timeline, Cancel request), switch Active hanya saat Approved, form terkunci saat Pending, simpan → kembali ke halaman itu; daftar menampilkan status approval. Lookup peternak untuk kandang baru ikut peternak Pending. **Contracts**: Activate → **Submit for approval**, Edit untuk Draft/Rejected, kartu Approval.
- Header: ikon inbox + badge (`vc:approval-badge`); dashboard "Waiting for action" + *Approvals waiting for me* & *Stuck approvals*.
- **Perbaikan**: halaman area yang dibuka langsung (tab baru/bookmark) dialihkan ke `/Admin/Main` (404) karena `Url.Action` mewarisi area → `_Layout` memakai `area = ""`.

### 24.3 MobileApp.Core & MobileApp
- **Core**: `ApprovalContracts` (request, detail, level, keputusan, delegasi, dashboard), `ApprovalTracked` (field approval di Farmer/Coop/Contract), `ApprovalsApi` (inbox/mine/detail/timeline di-cache untuk offline; approve/reject/cancel/delegasi online), `Labels` (status rekaman, jenis & status pengajuan, level, field, delegasi, ringkasan perubahan), `ErrorMessages` (kode approval; `FlowNotConfigured` menampilkan pesan server apa adanya), `AppFeatures` (Approval `/approval`, Pengajuan Saya `/pengajuan`, **Delegasi Approval** `/delegasi` untuk `approvals:decide`).
- **UI**: Inbox **Approval** (tab per jenis + jumlah, level n/N, lama menunggu), **Detail Pengajuan** (perubahan lama → baru, timeline, Setujui dengan catatan / Tolak dengan alasan wajib via `ConfirmSheet`, label "atas nama" saat delegasi, pesan offline), **Pengajuan Saya** (filter status, alasan tolak, batal dari detail), **Delegasi Approval** (form + daftar + cabut), komponen `ApprovalTimeline`/`ApprovalBanner`/`ApprovalTab`, tab **Approval** + banner di detail Peternak/Kandang/Kontrak, badge status di daftar, badge jumlah di bottom bar, kartu Beranda Manager "Approval menunggu saya" & PPL "Pengajuan saya".

### 24.4 Pengujian & verifikasi
- **Test: 700 lulus** — 194 domain (+24: `ApprovalTests`, `RegistrationTests`), 114 application (+14 `ApprovalHandlersTests`: tanpa alur tidak tersimpan, alur cabang > default, 2 level, tolak → ajukan ulang dari level 1, batal → Draft, kandang menunggu peternak, FarmerChange, kontrak, delegasi, approver nonaktif, replace approver, validasi alur, setup cabang), 14 arsitektur, 140 MobileApp.UnitTests (+11), 50 integration Web.Api (+7 `ApprovalWorkflowTests`: 2 level + dashboard, reject → resubmit + gudang `GK-`, kontrak submit & `activate` 404, FarmerChange, cancel, keputusan bersamaan (satu 204, satu 409/403), delegasi), 188 integration Web.App (+16 `ApprovalTests`). Test lama memakai alur default yang di-seed factory + helper `ApproveAsync`.
- **Data demo** di `intiplasma_verify`: 788 command tanpa error; isi approval sesuai M-94 (19 pengajuan Approved, 1 Pending L2, 1 Rejected; gudang `GK-` hanya untuk 8 kandang yang disetujui).
- **Web.App (Playwright, 44/44)**: alur BDG 2 level (manager.bdg → kacab.bdg) untuk 4 jenis lewat form level dinamis + Tom-Select, alur aktif ganda ditolak; cabang SMD dibuat dengan alur sendiri (level terisi dari default, 4 alur); badge header Tommy 1 → approve PTN-BDG-004 dari inbox (SweetAlert) → Approved/aktif; timeline di detail peternak; kandang ditolak menampilkan alasan + Submit; daftar Rejected; Replace preview & peringatan user; dashboard; **approver resign**: manager.bdg dinonaktifkan → pengajuan ulang kandang **Stuck** → Replace approver ke Siti (4 alur, 1 pengajuan) → tidak Stuck, tercatat di audit; halaman area dibuka langsung masuk mainboard; tanpa error JS.
- **API (12/12)**: `ppl.bdg` membuat peternak (Level 1 dari 2 · Manager Unit), kandang untuk peternak Pending, peternak kedua, kontrak + submit, `activate` 404, usulan NIK/rekening (nilai lama tetap); kacab tidak bisa memutuskan level 1 (403 `Approvals.NotApprover`); PPL tanpa inbox (403); inbox Manager = 5.
- **Emulator API 34**: `manager.bdg` Beranda kartu "Approval menunggu saya 5" + badge 5 → inbox bertab → detail perubahan NIK/rekening (lama → baru) → Setujui dengan catatan → badge 4; `kacab.bdg` inbox level 2/2 → Tolak tanpa alasan ditolak aplikasi → tolak kandang dengan alasan; **mode pesawat**: inbox & detail ter-cache terbaca, Setujui → "Tidak dapat terhubung ke server…"; online → peternak PTN-BDG-101 Approved/aktif; Delegasi Approval ke Tommy (daftar tanpa diri sendiri, status Berjalan) → Tommy memutuskan kontrak **a.n. Kepala Bandung** (API) → kontrak Active, perubahan NIK diterapkan; `ppl.bdg` Beranda "Pengajuan saya" dengan alasan tolak, Pengajuan Saya (5, alasan), batalkan PTN-BDG-102 → Draft; detail kandang ditolak: banner + tab Approval; ajukan ulang → mulai level 1.

### 24.5 Catatan
- Timeline per dokumen memakai `GET farmers|coops|contracts/{id}/approvals` (bukan `approvals?documentType&documentId`) agar memakai hak baca & lingkup PPL dokumen.
- Field approval di respons dokumen berupa kolom datar (`approval…`), bukan objek, karena dibaca Dapper langsung.
- Badge inbox Web.App dihitung saat halaman dimuat (tanpa push, M-95); badge mobile diperbarui setiap pindah halaman.
- Form tambah/ubah peternak, kandang & kontrak di mobile serta ajukan ulang dari mobile → **M7**.
- Pengguna Web.App yang menjadi approver perlu role API berisi `approvals:decide` (Administrator otomatis); hak menu Approval Inbox saja tidak cukup (M-86).
- Instalasi baru wajib membuat alur default sebelum input peternak/kandang/kontrak (`docs/DEPLOY.md` langkah 7).
