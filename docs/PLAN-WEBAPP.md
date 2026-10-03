# Plan Implementasi — MVC WebApp Admin Office (Inti-Plasma)

> Lanjutan dari [PLAN.md](PLAN.md) (Fase 0–9, API) dan [RANGKUMAN.md](RANGKUMAN.md) §9 (backlog: "MVC WebApp Admin Office", keputusan #4).
> Project: `src/Web.App/Web.App.csproj` (ASP.NET Core MVC, .NET 10, sudah masuk `IntiPlasma.slnx`).
> Referensi tampilan: `docs/html-template/` (template **Dreams POS**, Bootstrap 5, layout *two-column*).
> Penomoran fase WebApp memakai prefix **W** (W0, W1, …) agar tidak bentrok dengan fase API.

---

## 1. Keputusan (disepakati 2026-10-02)

| # | Topik | Keputusan |
|---|-------|-----------|
| W-1 | Arsitektur | WebApp adalah **aplikasi langsung (in-process)**: mereferensikan `Infrastructure.csproj` (transitif: Application, Domain, SharedKernel) dan memanggil `ICommandHandler`/`IQueryHandler` yang sama dengan Web.Api. **Tidak** lewat HTTP ke Web.Api. |
| W-2 | Otorisasi WebApp | **RBAC dinamis** per menu: `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `CanExport`, tersimpan di DB dan diatur admin tanpa deploy ulang. Hak didefinisikan dalam **profil Akses Menu** yang dipilih per user (W-15, §4). |
| W-3 | Ekspor | **Excel** dengan **ClosedXML**, **PDF** dengan **QuestPDF** (list, laporan, dan cetak dokumen). |
| W-4 | Aksi persetujuan | **Tidak** ada hak `CanApprove`. Persetujuan nanti memakai **sistem approval terpusat berjenjang** (fase terpisah, §4.8). Sampai itu tersedia, aksi alur kerja ikut `CanEdit` + aturan maker-checker yang sudah ada di domain. |
| W-5 | Cetak PDF dokumen | Butuh **`CanExport`** (sama dengan ekspor list & laporan). |
| W-6 | Lisensi QuestPDF | **Community (gratis)**. |
| W-7 | Akses cabang | **Dinamis** lewat **profil Akses Cabang** yang dipilih per user (W-15, §4.5); berlaku untuk WebApp **dan** Web.Api. |
| W-8 | Kerangka tampilan | `Views/Main/Index.cshtml` adalah **mainboard** (header + sidebar two-column). Halaman menu tampil di dalam **iframe** `content-frame` sesuai menu yang diklik (§3.10). |
| W-9 | Background job | Tetap **hosted service**, dijalankan di **Web.App** (outbox & cleanup lampiran). Web.Api **tidak** menjalankan job (`BackgroundJobs:Enabled = false`) — §3.11. |
| W-10 | Bahasa UI | **English** (label, menu, pesan, validasi, header Excel/PDF). Dokumen plan tetap berbahasa Indonesia. |
| W-11 | Input recording harian di WebApp | **Ada** (cadangan mobile PPL). |
| W-12 | Deploy | **Container terpisah** (Web.Api, Web.App), **satu database**, **satu storage** lampiran. |
| W-13 | Tema | Light, two-column sesuai template, theme switcher template dimatikan, **primary color `#0984E3`** (menggantikan `#FE9F43`). |
| W-14 | Permission `branches:access-all` | **Dihapus** setelah data dimigrasi ke profil Akses Cabang "All Branches". |
| W-15 | Model akses | **Per user**: setiap user memilih satu **Akses Menu** dan satu **Akses Cabang**. Keduanya master yang punya layar CRUD sendiri. Sidebar mainboard hanya merender menu yang `CanView` pada Akses Menu user. |
| W-16 | Format data | Teks UI English, format data **Indonesia**: `1.234.567,89`, `dd/MM/yyyy`, mata uang `Rp`, zona `Asia/Jakarta` (diatur lewat `App:FormatCulture`, default `id-ID`). |
| W-17 | Dokumen cetak resmi | Mengikuti UI (**English**), dengan **terbilang berbahasa Indonesia** untuk invoice, receipt, PV, dan settlement. |
| W-18 | Relasi user ↔ profil | Setiap user memakai **satu** Akses Menu & **satu** Akses Cabang; **satu profil Akses Menu bisa dipakai banyak user**, begitu juga **satu profil Akses Cabang** (relasi *many-to-one*: FK `users.menu_access_profile_id` & `users.branch_access_profile_id`). Mengubah profil langsung berlaku untuk semua user pemakainya. Variasi dibuat sebagai profil baru (fitur "Duplicate"). |
| W-19 | Override akses per user | **Tidak ada** pengecualian per user di luar profil. |
| W-20 | Role API di form user | Bagian opsional **"API / Mobile access"** di form create/edit user. |

---

## 2. Ringkasan Temuan

### 2.1 Kondisi `src/Web.App` saat ini
| Bagian | Kondisi |
|---|---|
| `Program.cs` | Default MVC: `AddControllersWithViews`, route default `Auth/Index`, belum ada autentikasi, `UseExceptionHandler("/Home/Error")` (controller `Home` tidak ada). |
| Controller | `AuthController` (Login GET/POST tanpa logika) dan `MainController` (Index, Dashboard, ExampleList, ExampleForm — halaman contoh). |
| View | `Views/Main/Index.cshtml` = **mainboard** (`Layout = null`): header, sidebar two-column (kolom kiri ikon modul sebagai tab, kolom kanan daftar menu), dan `<iframe name="content-frame">` yang tingginya disesuaikan script saat resize. Link menu memakai `target="content-frame"`; menu masih statis (contoh) dan sebagian menunjuk file `.html` template. `_Layout.cshtml` = layout **halaman di dalam iframe** (CSS/JS + helper toast/antiforgery, tanpa header/sidebar) — sudah sesuai pola. `Login.cshtml` `Layout = null`. Masih ada branding "Dreams POS". |
| Tema | `#FE9F43` ditulis langsung **784 kali** di `assets/css/style.css` (bukan lewat satu variabel). Mainboard memakai `data-color="magenta"`; `theme-script.js` membaca warna dari `localStorage`. |
| `wwwroot` | ±84 MB: seluruh `assets/` template (35+ plugin, banyak gambar demo) + `lib/` bawaan MVC → jQuery/Bootstrap ganda. |
| Referensi project | Belum ada (belum mereferensikan Infrastructure). |
| Build | `Directory.Build.props` juga berlaku di sini: `AnalysisMode=All`, `TreatWarningsAsErrors`, Sonar. `Web.App.csproj` mengulang `TargetFramework/Nullable/ImplicitUsings` (redundan). |

### 2.2 Dampak memakai Infrastructure langsung
| Temuan | Dampak & tindakan |
|---|---|
| `AddInfrastructure()` mendaftarkan **JWT Bearer sebagai skema default** | Web.App butuh **cookie** → `AddInfrastructure` dipecah (§3.2). |
| `AddInfrastructure()` mendaftarkan hosted service `OutboxProcessor` & `AttachmentCleanupJob` | Dibuat opsional (`BackgroundJobs:Enabled`): **aktif di Web.App, nonaktif di Web.Api** (W-9, §3.11). |
| Cache permission/cabang memakai **HybridCache in-memory per proses** (10 menit), diinvalidasi di proses yang menjalankan command | Dengan 2 proses (dan replika), perubahan akses di Web.App tidak terlihat oleh Web.Api sampai cache kedaluwarsa → **invalidasi lintas proses** via PostgreSQL `LISTEN/NOTIFY` (§3.12). |
| Permission `{module}:{action}` hanya dicek **di endpoint** (`.HasPermission(...)`), bukan di handler | Di Web.App, **RBAC menu menjadi satu-satunya gerbang otorisasi** → setiap action controller wajib diberi atribut akses (dijaga test arsitektur, §6.2). |
| Branch-scope ditegakkan di **handler** lewat `IBranchAccess` (saat ini: permission `branches:access-all` atau `user_branches`) | Otomatis berlaku di Web.App, asal principal cookie membawa claim `NameIdentifier` = user id. `BranchAccess` diubah membaca profil Akses Cabang (§4.5) — satu titik perubahan, berlaku juga untuk API. |
| Maker-checker, penomoran dokumen, outbox, audit interceptor, jurnal otomatis | Otomatis berlaku (bagian dari domain, `ApplicationDbContext` & handler). |
| `Error.Description` di Domain/Application sudah **berbahasa Inggris** | Sesuai W-10: pesan ditampilkan langsung, tanpa katalog terjemahan. |
| `IdempotencyFilter` ada di Web.Api (endpoint filter) | Web.App membuat proteksi double-submit sendiri (token form, §3.7). |
| `LocalFileStorage` lampiran | Web.Api & Web.App (termasuk job cleanup) memakai **volume yang sama** (`FileStorage:RootPath`, W-12). |
| Migration & seeding (`ApplyMigrations`, `DatabaseSeeder`) dipanggil Web.Api (dev) | Web.App **tidak** menjalankan migration. Sinkronisasi katalog menu (idempotent) dijalankan Web.App saat startup (§4.3). |
| `LoginUserCommand` mengembalikan JWT + refresh token | Web.App butuh use case baru **tanpa token**: `SignInUserCommand` → profil user. |
| Entity `User` belum punya status aktif/nonaktif | Ditambah `IsActive` + `Deactivate/Activate`. Login API & WebApp sama-sama menolak user nonaktif. |
| Test arsitektur memeriksa Domain/Application/Infrastructure/Presentation (Web.Api) | Ditambah aturan untuk Web.App (§6.2). |

### 2.3 Use case Application yang belum ada (dibutuhkan WebApp)
| Kebutuhan | Use case |
|---|---|
| Login cookie | `SignInUserCommand` (cek password + `IsActive`) |
| Profil user login, menu & cabang efektif | `GetCurrentUserQuery`, `GetMyMenuAccessQuery`, `GetEffectiveBranchesQuery` |
| Manajemen user | `GetUsersQuery`, `GetUserByIdQuery` (diperluas), `CreateUserCommand` (data user + Akses Menu + Akses Cabang + cabang default + role API), `UpdateUserCommand`, `SetUserAccessCommand`, `DeactivateUserCommand`/`ActivateUserCommand`, `ResetUserPasswordCommand`, `ChangeOwnPasswordCommand` |
| Akses Menu (CRUD) | `GetMenuAccessProfilesQuery`, `GetMenuAccessProfileByIdQuery`, `CreateMenuAccessProfileCommand`, `UpdateMenuAccessProfileCommand` (nama + matriks), `DeleteMenuAccessProfileCommand` |
| Akses Cabang (CRUD) | `GetBranchAccessProfilesQuery`, `GetBranchAccessProfileByIdQuery`, `CreateBranchAccessProfileCommand`, `UpdateBranchAccessProfileCommand`, `DeleteBranchAccessProfileCommand` |
| Katalog menu | `SyncMenuCatalogCommand`, `GetMenusQuery`, `UpdateMenuCommand` |
| Dashboard | `GetDashboardSummaryQuery` (siklus aktif, populasi, piutang/hutang jatuh tempo, stok, event gagal) |
| Ekspor list tanpa batas `pageSize` 100 | Tidak perlu use case baru: exporter memanggil query list yang sama **halaman per halaman** (§5.1) |

Use case ditempatkan di Application seperti modul lain (satu file per use case) dan **boleh dipakai Web.Api** juga.

---

## 3. Arsitektur

### 3.1 Gambaran
```
Browser ──cookie──► Web.App (MVC + background job) ─┐
Mobile  ──JWT─────► Web.Api ────────────────────────┴──► Application ──► Infrastructure ──► PostgreSQL
                                                                                              ▲
                    (kedua proses) ◄──── invalidasi cache lintas proses (LISTEN/NOTIFY) ──────┘
```
- Controller **tipis**: bangun command/query → panggil handler → map `Result` ke view/redirect. **Tidak** memakai `ApplicationDbContext`/`IDbConnectionFactory` langsung (dijaga test arsitektur).
- Satu database, satu set aturan bisnis. Perubahan use case otomatis berlaku untuk API & WebApp.

### 3.2 Pemecahan `Infrastructure.DependencyInjection`
```csharp
// Web.Api
services.AddInfrastructureCore(configuration);      // services, database, storage, IUserContext, IPasswordHasher,
                                                    // PermissionProvider, IBranchAccess, IMenuAccessProvider,
                                                    // cache invalidation (publisher + listener), health checks
services.AddJwtAuthentication(configuration);       // JWT bearer + ITokenProvider + permission policy

// Web.App
services.AddInfrastructureCore(configuration);      // + cookie auth & policy menu didaftarkan di Web.App
services.AddBackgroundJobs(configuration);          // OutboxProcessor, AttachmentCleanupJob — hanya bila BackgroundJobs:Enabled
```
- Kedua host memanggil `AddApplication()` (handler + decorator); Web.App juga butuh handler domain event untuk outbox.
- `AddInfrastructure()` lama dihapus (atau menjadi alias `Core + Jwt`) agar job tidak tanpa sengaja ikut jalan di Web.Api.

### 3.3 Autentikasi (cookie)
- `SignInUserCommand` → principal dengan claim `NameIdentifier` (user id), `Name`, `Email`, + `security_stamp`. Cookie `HttpOnly`, `Secure`, `SameSite=Lax`, sliding 8 jam.
- `OnValidatePrincipal`: tolak sesi bila user dinonaktifkan atau password direset (cek `security_stamp`, cache singkat).
- User **tanpa Akses Menu** tidak bisa login ke WebApp (pesan "No menu access assigned"), tetapi tetap bisa memakai API bila punya role.
- Fitur: login (`returnUrl`), logout, ganti password sendiri, lockout setelah N kali gagal (W10).

### 3.4 RBAC dinamis (detail §4)
- Atribut `[MenuAccess("sales.orders", MenuRight.Create)]` pada action → policy `menu:sales.orders:Create` (custom `IAuthorizationPolicyProvider` + handler membaca `IMenuAccessProvider`).
- Tag helper `<a asp-menu="sales.orders" asp-right="Edit">` untuk menyembunyikan tombol. Sidebar mainboard hanya merender menu `CanView`.

### 3.5 Konteks cabang
- Pemilih cabang di header berisi **cabang efektif** dari Akses Cabang user. Cabang aktif disimpan di cookie terpisah (awalnya = cabang default user) → filter default `branchId` pada list & nilai default form. Profil "semua cabang" mendapat opsi "All branches".
- Bila Akses Cabang user berubah saat sesi berjalan, cabang aktif yang tidak lagi diizinkan otomatis diganti ke cabang default. Branch-scope sesungguhnya tetap ditegakkan handler.

### 3.6 Pola halaman
| Jenis | Pola |
|---|---|
| List | Server-rendered tabel + filter (search, status, cabang, tanggal) via query string → query handler (`PagedList`). Tombol **Excel** & **PDF** (bila `CanExport`) mengirim filter yang sama. |
| Detail | Header dokumen + tab (Lines, Journal, Attachments, History). Tombol aksi sesuai **status + hak akses** dari partial `_DocumentActions` (konfirmasi SweetAlert2). Tombol **Print PDF** (QuestPDF) bila `CanExport`. |
| Form | POST + PRG; validasi server dari `ValidationDecorator` (FluentValidation) dipetakan ke `ModelState` per field; validasi klien ringan (unobtrusive). Baris dinamis (PO, SO, VI, jurnal, kas) dengan JS kecil per halaman. Dropdown pencarian **Tom-Select** ke endpoint lookup Web.App; tanggal **flatpickr**; angka/uang **IMask**. |
| Aksi state | POST `/sales/orders/{id}/approve` → command → TempData toast → redirect ke detail. |
| Laporan | Form parameter → tabel; ekspor Excel/PDF. |

### 3.7 Penanganan hasil & error
- Extension `Result` → UI: `ErrorType.Validation` → `ModelState`; `NotFound` → 404; `Conflict`/`Problem` → pesan di atas form memakai `Error.Description` (English).
- `DbUpdateConcurrencyException` (`xmin`) → "This record was changed by another user. Please reload." (filter global). Nilai versi disimpan di hidden field form edit.
- **Proteksi double-submit**: token form (`Guid v7`) dibuat saat GET form create; POST kedua dengan token sama dalam 24 jam di-redirect ke hasil pertama (HybridCache, meniru `IdempotencyFilter`). Untuk use case yang menerima ID dari klien, token yang sama dipakai sebagai ID dokumen.
- Halaman error 403/404/500 & sesi habis; exception handler + status code pages; Serilog seperti Web.Api.

### 3.8 Struktur folder target
```
src/Web.App/
├─ Program.cs
├─ Infrastructure/
│  ├─ Auth/            # cookie setup, CurrentUser, BranchContext
│  ├─ Authorization/   # MenuAccessAttribute, MenuPolicyProvider, MenuAuthorizationHandler, MenuCatalog
│  ├─ Results/         # Result → IActionResult/ModelState
│  ├─ Forms/           # FormToken (double-submit)
│  ├─ Workflow/        # WorkflowActionAttribute, IWorkflowActionService (§4.8)
│  └─ Export/          # IExcelExporter (ClosedXML), IPdfExporter (QuestPDF), ExportColumn<T>, PagedExportRunner
├─ Documents/          # template QuestPDF per dokumen (PO, Invoice, PV, Settlement, …) + komponen header/footer
├─ Areas/
│  ├─ Admin/        (users, menu access, branch access, menus, branches, API roles)
│  ├─ MasterData/   (uom, tax-code, item, warehouse, vendor, customer)
│  ├─ Partnership/  (farmer, coop, contract, cycle)
│  ├─ Procurement/  (purchase order)
│  ├─ Inventory/    (goods receipt, transfer, return, feed mutation, stock)
│  ├─ Production/   (chick-in, recording, harvest, performance, close)
│  ├─ Sales/        (SO, DO, invoice, credit note)
│  ├─ Finance/      (COA, cost center, period, journal, mapping, cash-bank, AR, AP, recon, report, tax)
│  ├─ Costing/      (cycle cost, settlement)
│  └─ System/       (failed events)
├─ Controllers/        # Auth, Main (mainboard + dashboard), Lookup, Attachment, Error
├─ ViewComponents/     # Sidebar (mainboard), BranchSwitcher, UserMenu, Breadcrumb (halaman iframe)
├─ TagHelpers/         # menu-right, status-badge, money, date
├─ wwwroot/
│  ├─ css/theme.css    # sisa override tema (§3.13)
│  └─ js/
│     ├─ mainboard.js  # navigasi iframe, sinkron hash/judul/menu aktif, postMessage (§3.10)
│     └─ frame.js      # dipakai _Layout: lapor navigasi ke parent, redirect bila dibuka di luar mainboard
└─ Views/Shared/
   ├─ _Layout.cshtml        # layout halaman di dalam iframe (tanpa header/sidebar)
   ├─ _LayoutAuth.cshtml    # login & error tingkat atas (tanpa mainboard)
   ├─ Mainboard/            # partial header & sidebar untuk Views/Main/Index.cshtml
   └─ Partials/ (_PageHeader, _FilterBar, _Pagination, _ExportButtons, _Attachments, _StatusBadge, _DocumentActions, _JournalPreview)
```

### 3.9 Pemetaan halaman template → layar aplikasi
| Template (`docs/html-template/`) | Dipakai untuk |
|---|---|
| `signin.html`, `forgot-password.html`, `error-404.html`, `error-500.html`, `lock-screen.html` | Login, ganti password, halaman error, sesi habis |
| `layout-two-column.html` / `index.html` (shell) | Mainboard `Views/Main/Index.cshtml` (header, sidebar dua kolom); isi `page-wrapper` diganti iframe |
| `blank-page.html` | Kerangka halaman di dalam iframe (`_Layout`) |
| `admin-dashboard.html`, `sales-dashboard.html` | Dashboard |
| `product-list.html`, `customers.html`, `suppliers.html`, `warehouse.html`, `units.html`, `tax-rates.html` | List master data |
| `add-product.html`, `edit-product.html`, `form-*.html`, `form-wizard.html` | Form master & transaksi (wizard untuk kontrak, chick-in, dan create user) |
| `users.html`, `roles-permissions.html`, `permissions.html` | Users, **Menu Access** (matriks checkbox View/Create/Edit/Delete/Export), Branch Access, API Roles |
| `purchase-list.html`, `purchase-order-report.html`, `purchase-returns.html` | PO, BPB, retur |
| `stock-transfer.html`, `manage-stocks.html`, `stock-history.html`, `low-stocks.html` | Transfer, saldo stok, kartu stok |
| `orders.html`, `edit-sales.html`, `invoice.html`, `invoice-details.html`, `sales-returns.html` | SO, DO, invoice, nota kredit |
| `account-list.html`, `account-statement.html`, `money-transfer.html`, `expense-list.html`, `income.html` | Kas/bank, buku kas, transfer, kas keluar/masuk |
| `balance-sheet.html`, `trial-balance.html`, `profit-and-loss.html`, `cash-flow.html`, `annual-report.html` | Laporan keuangan |
| `customer-due-report.html`, `supplier-due-report.html` | Aging piutang / hutang |
| `tax-reports.html` | Rekap PPN/PPh |
| `file-manager.html`, `form-fileupload.html` (Dropzone) | Komponen lampiran |
| `activities.html`, `ui-timeline.html` | Riwayat revisi recording / audit dokumen |
| `chart-apex.html` / `chart-js.html` | Grafik performa siklus & dashboard |

Halaman yang **tidak dipakai** (POS, chat, call, blog, e-commerce, HRM, dll.) beserta aset & plugin-nya dibuang dari `wwwroot` di W0.

### 3.10 Mainboard & iframe
**Struktur**
- `MainController.Index` (cukup login, tanpa `[MenuAccess]`) merender mainboard: header (logo, BranchSwitcher, notifikasi, UserMenu) + sidebar two-column + iframe `content-frame`. Halaman awal iframe = `Main/Dashboard`.
- Sidebar dibangun dari `menus` aktif + Akses Menu user (**hanya `CanView`**): **level 1** (grup modul: Dashboard, Master Data, Partnership, Procurement, Inventory, Production, Sales, Finance, Costing, Reports, Administration) = ikon tab di kolom kiri; **level 2/3** = `menu-title`/`submenu` di kolom kanan. Grup tanpa satu pun menu `CanView` disembunyikan. Semua link `target="content-frame"`.
- Semua halaman menu (list, detail, form, laporan) memakai `_Layout` dan **hanya** tampil di dalam iframe. Login, logout, dan halaman error sesi memakai `_LayoutAuth` di jendela utama.

**Navigasi & URL** (`mainboard.js` + `frame.js`)
- Klik menu → iframe dimuat, URL jendela utama diperbarui ke `/Main#/sales/orders?status=Draft` (`history.pushState`) → bisa di-bookmark, di-refresh, dan tombol Back/Forward berfungsi.
- Saat mainboard dibuka dengan hash, iframe langsung memuat path tersebut (hanya path internal yang diawali `/`, untuk mencegah open redirect).
- Setiap halaman iframe mengirim `postMessage({ type: 'navigated', url, title, menuCode })` ke parent (origin dicek) → parent menyinkronkan hash, `document.title`, dan menu aktif. Ini juga berlaku untuk navigasi di dalam iframe (submit form, redirect PRG, link detail).
- Halaman iframe yang dibuka **langsung** di tab sendiri (`window.top === window.self`) dialihkan ke `/Main#<path>`. Pengecualian: unduhan & pratinjau PDF (`target="_blank"`).

**Sesi & keamanan**
- Sesi habis saat request di dalam iframe → server mengembalikan halaman kecil yang menjalankan `window.top.location = '/Auth/Login?returnUrl=/Main%23<path>'`. `Login.cshtml` juga memaksa keluar dari frame.
- Header keamanan: `X-Frame-Options: SAMEORIGIN` + CSP `frame-ancestors 'self'`.
- Hak akses tetap dicek per halaman (`[MenuAccess]`); sidebar hanya kenyamanan.

**Interaksi parent ↔ iframe**
- Ganti cabang di header → cookie cabang aktif diperbarui → iframe di-reload.
- Toast, SweetAlert, modal, dan dropdown berjalan **di dalam iframe**. Pesan global (mis. sesi akan habis) ditampilkan parent.
- Tinggi iframe mengikuti jendela (script resize yang sudah ada); scroll di dalam iframe. Indikator loading di parent saat iframe berpindah halaman.
- Logout di UserMenu memakai jendela utama (`target="_top"`).

**Performa**
- `_Layout` hanya memuat aset inti (Bootstrap, jQuery, ikon, style template, toast, SweetAlert); plugin lain (Tom-Select, flatpickr, IMask, Dropzone, Chart) lewat `@section Scripts` per halaman. Aset statis di-cache (`MapStaticAssets` + fingerprint).

### 3.11 Background job (di Web.App)
- `AddBackgroundJobs` mendaftarkan `OutboxProcessor` (domain event → handler, termasuk jurnal otomatis & pembuatan gudang kandang) dan `AttachmentCleanupJob` sebagai hosted service **di Web.App**. Job baru nanti (mis. pengingat jatuh tempo, eskalasi approval) juga di sini.
- Konfigurasi `BackgroundJobs:Enabled`: `true` di Web.App, `false` di Web.Api (perilaku Web.Api berubah — job tidak lagi jalan di sana).
- Event handler berjalan **tanpa HttpContext** (sama seperti sekarang di dalam Web.Api): `IUserContext.IsAuthenticated = false`, audit memakai perilaku yang sudah ada.
- Replika Web.App lebih dari satu aman: outbox `FOR UPDATE SKIP LOCKED`; cleanup lampiran memakai advisory lock agar hanya satu replika yang jalan.
- Health check Web.App menyertakan status job (waktu proses outbox terakhir, jumlah pesan tertunda/gagal).
- ⚠️ Bila Web.App berhenti, event outbox dari transaksi Web.Api/mobile (mis. jurnal otomatis BPB, gudang kandang) **tertunda** sampai Web.App jalan kembali — tidak hilang, karena tersimpan di `outbox_messages`. Dev: jalankan Web.Api + Web.App bersamaan (multiple startup projects / `docker-compose`), atau set `BackgroundJobs__Enabled=true` di Web.Api bila hanya menjalankan API.

### 3.12 Invalidasi cache lintas proses
- Command yang mengubah akses (user, Akses Menu, Akses Cabang, menu, role API, status cabang) tetap menghapus cache lokal, lalu mengirim `NOTIFY cache_invalidation, '<tag|key>'` (dalam transaksi yang sama → hanya terkirim bila commit).
- Setiap proses (Web.Api, Web.App, termasuk replika) menjalankan listener (`LISTEN cache_invalidation`, koneksi khusus dari `NpgsqlDataSource`) yang menghapus key/tag di HybridCache lokal. Bila koneksi listener putus → reconnect + kosongkan tag akses (fail-safe).
- Hasil: perubahan akses berlaku **tanpa login ulang** di semua aplikasi dalam hitungan detik. Redis tetap opsi bila nanti scale-out besar.

### 3.13 Tema (primary color)
- Ganti satu kali di `assets/css/style.css`: `#FE9F43` → `#0984E3`, bentuk RGB `254, 159, 67` → `9, 132, 227`, serta shade turunannya (hover/active/light — diinventarisasi dulu dengan `grep`, lalu dipetakan ke shade `#0984E3` yang sepadan).
- Mainboard memakai `data-color` tetap (bukan `magenta`), `theme-script.js` & theme customizer dihapus (tidak membaca `localStorage`).
- Sisa override kecil di `wwwroot/css/theme.css` (dimuat setelah `style.css`). Cek logo/ikon SVG yang berwarna oranye.

---

## 4. RBAC Dinamis & Akses Cabang (per user)

### 4.1 Konsep
```
User ──1──► Akses Menu  (Menu Access Profile)  ──► baris per menu: CanView/Create/Edit/Delete/Export
     ──1──► Akses Cabang (Branch Access Profile) ──► All branches | daftar cabang
     ──n──► Role (permission API, untuk Web.Api/mobile)
     ──1──► Cabang default (harus termasuk Akses Cabang)
```
- **Akses Menu** dan **Akses Cabang** adalah master tersendiri dengan layar CRUD. Satu profil bisa dipakai banyak user (mis. "Finance Staff", "Area Jawa Barat").
- Saat **create/edit user**, admin memilih satu Akses Menu, satu Akses Cabang, cabang default, dan (opsional) role API.
- **Role** tetap ada, hanya untuk permission Web.Api/mobile (`{module}:{action}`). WebApp tidak memakai role untuk otorisasi.

### 4.2 Model data (schema `identity`)
| Tabel | Kolom utama | Keterangan |
|---|---|---|
| `menus` | `id`, `code` (unik, mis. `sales.orders`), `parent_id`, `name`, `icon`, `route`, `sort_order`, `is_active`, `supports_create`, `supports_edit`, `supports_delete`, `supports_export` | Menu bertingkat. Flag `supports_*` menentukan checkbox yang tampil di matriks (mis. laporan hanya View + Export). |
| `menu_access_profiles` | `id`, `name` (unik), `description`, `is_system`, `xmin` | Aggregate **Akses Menu** |
| `menu_access_profile_items` | `profile_id`, `menu_id`, `can_view`, `can_create`, `can_edit`, `can_delete`, `can_export` | Child (PK `profile_id + menu_id`) |
| `branch_access_profiles` | `id`, `name` (unik), `description`, `all_branches`, `is_system`, `xmin` | Aggregate **Akses Cabang** |
| `branch_access_profile_branches` | `profile_id`, `branch_id` | Child |
| `users` (+kolom) | `is_active`, `menu_access_profile_id` (nullable), `branch_access_profile_id` (nullable), `default_branch_id` (nullable), `security_stamp` | |
| `user_branches` | — | **Dihapus** setelah migrasi data (§4.6) |

### 4.3 Katalog menu
- Kode menu terikat ke controller, sehingga **daftar menu didefinisikan di kode** (`MenuCatalog` di Web.App: code, parent, nama default, ikon, route, `supports_*`). Saat startup Web.App menjalankan `SyncMenuCatalogCommand` (idempotent): menambah menu baru, menonaktifkan menu yang hilang dari kode, **tidak menimpa** nama/ikon/urutan/aktif yang sudah diubah admin.
- Yang dinamis (diatur admin tanpa deploy): profil Akses Menu & Akses Cabang, akses per user, nama tampilan, ikon, urutan, aktif/nonaktif menu.

### 4.4 Akses Menu — aturan & arti hak
**Invariant domain** (`MenuAccessProfile.SetItems(...)`):
- Hak apa pun selain `CanView` ⇒ `CanView` wajib `true`.
- Hak yang tidak didukung menu (`supports_* = false`) ditolak.
- Nama profil unik.
- Profil sistem **"Full Access"**: semua hak pada semua menu aktif (dihitung, tidak disimpan per baris), tidak bisa diubah/dihapus. Admin seed memakai profil ini.
- Profil yang **masih dipakai user tidak bisa dihapus** (409, pesan menyebut jumlah user).
- Perubahan profil → invalidasi cache semua user pemakainya (tag) + `NOTIFY` (§3.12).

| Hak | Cakupan di WebApp |
|---|---|
| `CanView` | Menu tampil di sidebar; buka list & detail; lihat/unduh lampiran |
| `CanCreate` | Form & aksi create (termasuk unggah lampiran pada dokumen baru) |
| `CanEdit` | Edit draft, ubah lampiran, aksi alur kerja (approve, post, cancel, void, pay, close, …) **sementara** sampai approval terpusat tersedia (W-4, §4.8) |
| `CanDelete` | Hapus/nonaktifkan master, hapus draft, hapus lampiran |
| `CanExport` | Ekspor list & laporan ke Excel / PDF **dan cetak PDF dokumen** |

**Penegakan**: setiap action controller wajib `[MenuAccess(code, right)]` atau `[AllowAnonymous]`/`[AuthenticatedOnly]` eksplisit (dijaga test arsitektur). Lookup Tom-Select & unduh lampiran ikut menu pemiliknya (`CanView`).

### 4.5 Akses Cabang — aturan
- `all_branches = true` ⇒ semua cabang **aktif**, termasuk cabang yang dibuat kemudian; daftar cabang diabaikan.
- Selain itu minimal satu cabang; cabang nonaktif tidak termasuk cabang efektif.
- Profil sistem **"All Branches"** tidak bisa diubah/dihapus. Profil yang masih dipakai user tidak bisa dihapus.
- **Cabang efektif user** = cabang dari profil Akses Cabang-nya. User tanpa Akses Cabang ⇒ tidak melihat data cabang mana pun (API & WebApp).
- `default_branch_id` wajib termasuk cabang efektif; bila profil berubah dan default tidak lagi valid → dikosongkan, jatuh ke cabang efektif pertama.
- `BranchAccess` (Infrastructure) membaca profil ini → **berlaku sama** untuk Web.Api & Web.App.

### 4.6 Migrasi data & perubahan API
- Profil Akses Cabang "All Branches" dibuat; user yang punya role dengan permission `branches:access-all` → profil ini. Permission `branches:access-all` dihapus dari katalog & dari `role_permissions` (W-14).
- User lain dengan `user_branches`: satu profil per **kombinasi cabang yang sama** (nama otomatis, mis. "Branches: BDG, JKT"), lalu `user_branches` dihapus.
- Profil Akses Menu "Full Access" dibuat dan diberikan ke user yang memegang role `Administrator`. User lain belum punya Akses Menu (diatur admin setelah rilis).
- Web.Api: `PUT users/{id}/branches` ⚠️ **diganti** `PUT users/{id}/access` (`menuAccessProfileId`, `branchAccessProfileId`, `defaultBranchId`); tambah endpoint CRUD `menu-access-profiles` & `branch-access-profiles`, `GET users` (list), aktif/nonaktif & reset password. Perubahan kontrak dicatat di RANGKUMAN saat fase dikerjakan.

### 4.7 Layar (W1)
| Layar | Isi |
|---|---|
| **Menu Access** | List (nama, jumlah user, jumlah menu `CanView`) + create/edit: nama, deskripsi, **matriks** menu bertingkat × View/Create/Edit/Delete/Export (centang per baris, per kolom, per grup; checkbox tidak tampil untuk hak yang tidak didukung menu) + delete. Aksi "Duplicate" untuk membuat profil serupa. |
| **Branch Access** | List (nama, all branches / jumlah cabang, jumlah user) + create/edit: nama, deskripsi, "All branches" atau pilih cabang + delete. |
| **Users** | List (filter status, Menu Access, Branch Access) + **create**: email, nama, password awal, **pilih Menu Access**, **pilih Branch Access**, cabang default (difilter dari Branch Access terpilih), role API (opsional). Edit, activate/deactivate, reset password. Detail menampilkan menu & cabang efektif. |
| **Menus** | Nama tampilan, ikon, urutan, aktif/nonaktif (struktur & kode dari katalog). |
| **API Roles** | Role + permission API (khusus Web.Api/mobile). |
| **Branches** | Master cabang (aktif/nonaktif memengaruhi cabang efektif). |

### 4.8 Persiapan approval terpusat (fase terpisah, di luar W0–W10)
Approval berjenjang akan menjadi modul sendiri (aturan per jenis dokumen/cabang/nilai, level 1..n, inbox persetujuan, delegasi, riwayat; eskalasi dijalankan background job Web.App). Agar tidak perlu bongkar ulang, sejak W0:
- Semua aksi persetujuan di controller diberi penanda `[WorkflowAction("approve")]` dan dipanggil lewat satu layanan Web.App (`IWorkflowActionService`) — saat ini meneruskan langsung ke command `Approve…`/`Post…` yang ada; nanti dialihkan ke modul approval.
- Tombol aksi di detail dokumen dirender dari satu partial (`_DocumentActions`), sehingga penggantian ke status "Waiting for approval level n" cukup di satu tempat.
- Maker-checker di domain tetap berlaku sebagai pengaman minimum.

---

## 5. Ekspor Excel & PDF

### 5.1 Fondasi
- Paket di `Directory.Packages.props`: `ClosedXML`, `QuestPDF`.
- `ExportColumn<T>` (header, selector, format: text/number/money/date/percent, lebar) — satu definisi kolom dipakai **Excel & PDF** list.
- `PagedExportRunner`: menjalankan query list yang sama dengan filter layar, **halaman per halaman** (100 baris) sampai habis, dengan batas `Export:MaxRows` (default 50.000; lebih dari itu diminta mempersempit filter).
- Nama file: `{menu}_{branch}_{yyyyMMdd-HHmm}.xlsx|pdf`. Setiap ekspor dicatat ke log (user, menu, filter, jumlah baris).

### 5.2 Excel (ClosedXML)
- Header tebal + freeze pane + autofilter, kolom uang sebagai **angka** (bukan teks) agar bisa dijumlah, format angka & tanggal sesuai W-16. Baris judul: nama laporan, cabang, periode/filter, printed by & at.
- Laporan berstruktur (Balance Sheet, Income Statement, Cash Flow, General Ledger, Aging, Cycle Cost, Settlement) memakai builder khusus (indentasi akun, subtotal).

### 5.3 PDF (QuestPDF)
- Komponen bersama: kop (logo, nama perusahaan, cabang), judul & nomor dokumen, footer "Page x of y", printed by/at. Font disematkan (agar sama di Windows & container Linux).
- **List & laporan**: tabel landscape A4 dari `ExportColumn<T>`.
- **Cetak dokumen**: PO, Goods Receipt, Transfer/Return, SO, DO (delivery note), Sales Invoice, Credit Note, Customer Receipt, Vendor Invoice, Payment Voucher, Cash In/Out, Journal Voucher, Plasma Settlement, Cycle Closing Summary. Bahasa & "terbilang" mengikuti W-17.
- Lisensi: `QuestPDF.Settings.License = LicenseType.Community` diset saat startup (W-6). Syarat Community (omzet perusahaan < USD 1 juta/tahun) ditinjau ulang bila skala usaha bertambah.
- Akses: semua PDF butuh `CanExport` pada menu terkait (W-5).

---

## 6. Cara Kerja, Pengujian & Verifikasi

### 6.1 Alur per fase
Use case Application/Domain baru dikerjakan & diuji dulu (unit test seperti fase API) → layar Web.App → verifikasi. **User yang commit & migrate.** Migration baru diberi nama `PhaseW{n}_…` (mis. `PhaseW1_AccessControl`).

### 6.2 Test
- **Domain**: invariant `MenuAccessProfile`, `BranchAccessProfile`, `User.SetAccess` (default branch), `User.Deactivate`.
- **Application**: CRUD profil (termasuk tolak hapus profil terpakai), create user dengan akses, `SyncMenuCatalogCommand` (tidak menimpa ubahan admin), invalidasi cache saat akses berubah.
- **Infrastructure** (integration, Testcontainers): `BranchAccess` dari profil (all branches, cabang nonaktif, tanpa profil), listener `LISTEN/NOTIFY`, migrasi data `user_branches` & `branches:access-all`.
- **Arsitektur** (tambahan): Domain/Application/Infrastructure tidak bergantung pada Web.App; controller Web.App tidak memakai `ApplicationDbContext`/`IDbConnectionFactory`; setiap action controller punya `[MenuAccess]` atau atribut anonim/autentikasi eksplisit; setiap kode di `[MenuAccess]` ada di `MenuCatalog`; Web.Api tidak mendaftarkan hosted service job secara default.
- **`tests/Web.App.IntegrationTests`**: `WebApplicationFactory` + Testcontainers PostgreSQL — login/logout, user tanpa Akses Menu ditolak, sidebar hanya `CanView`, 403 untuk hak yang tidak dimiliki, ekspor Excel/PDF menghasilkan file valid (dibuka ulang dengan ClosedXML / cek header `%PDF`).

### 6.3 Verifikasi end-to-end
- Database sementara `intiplasma_verify` (dibuat, di-migrate, dihapus otomatis; DB `intiplasma` tidak disentuh). Web.App (dengan background job) dijalankan dengan `ConnectionStrings__Database` ke DB tersebut; Web.Api ikut bila skenario menguji invalidasi cache lintas proses atau event outbox dari transaksi API.
- Skenario per fase dengan **Playwright** (Node, interaksi lewat `frameLocator('iframe[name=content-frame]')`): admin (Full Access), user `Checker` (maker-checker), user dengan Akses Menu/Cabang terbatas (menu tersembunyi, tombol hilang, 403, data cabang lain tidak terlihat), ubah profil saat user login (berlaku tanpa login ulang), ekspor Excel/PDF; data dicek via psql.
- Tanggal data uji ≤ hari ini.

### 6.4 Konvensi kode
- Controller `sealed`, satu file per controller; view per action di `Areas/{Area}/Views/{Controller}/`.
- View model terpisah dari command/response Application bila bentuk form berbeda.
- Teks UI **English**; format angka/tanggal/zona waktu mengikuti W-16.

---

## 7. Tahapan Implementasi

### Fase W0 — Fondasi ✅ (selesai 2026-10-02, realisasi §10)
- Rapikan `Web.App.csproj` (hapus properti redundan) + `ProjectReference` ke `Infrastructure.csproj`; build bersih dengan analyzer.
- Pecah `AddInfrastructure` (§3.2); background job pindah ke Web.App (`BackgroundJobs:Enabled`, advisory lock cleanup, health check job); Web.Api berhenti menjalankan job; seluruh test tetap lulus.
- **Invalidasi cache lintas proses** (`LISTEN/NOTIFY`, §3.12) untuk cache permission & cabang yang sudah ada.
- `appsettings`: `ConnectionStrings:Database`, `FileStorage:RootPath` (volume sama), `Serilog`, `Export`.
- Pangkas `wwwroot` (hapus duplikasi `lib/`/`assets`, plugin & gambar demo), ganti branding, **primary color `#0984E3`** (§3.13), matikan theme customizer.
- Mainboard (`Views/Main/Index.cshtml`): header & sidebar menjadi partial/view component, `mainboard.js`; `_Layout` (halaman iframe) dirampingkan + `frame.js`; tambah `_LayoutAuth`. `ExampleList`/`ExampleForm` dipakai sebagai acuan partial lalu dihapus.
- Domain/Application: `User.IsActive` + `security_stamp`, `SignInUserCommand`, `GetCurrentUserQuery`, `ChangeOwnPasswordCommand`; login API ikut menolak user nonaktif. Migration `PhaseW0_UserStatus`.
- Cookie auth, login/logout/change password, UserMenu (BranchSwitcher sementara memakai akses cabang lama).
- Mapping `Result` → UI, filter concurrency, token double-submit, `[WorkflowAction]`/`IWorkflowActionService`, halaman error.
- Partial umum: page header, filter bar, pagination, status badge, money/date, `_DocumentActions`.
- **Selesai bila**: login admin → dashboard kosong → ganti cabang → change password → logout; user nonaktif tidak bisa login (WebApp & API); jurnal otomatis dari transaksi API tetap terbentuk lewat job di Web.App.

### Fase W1 — Akses Menu, Akses Cabang & Administrasi ✅ (selesai 2026-10-03; rencana §11, realisasi §12)
- Domain: `Menu`, `MenuAccessProfile`, `BranchAccessProfile`, `User.SetAccess` (§4.2–4.5). Migration `PhaseW1_AccessControl` (termasuk migrasi data §4.6 dan penghapusan `user_branches` & `branches:access-all`).
- Application: use case §2.3 (CRUD profil, user, katalog menu); `IMenuAccessProvider` + cache; `BranchAccess` versi profil.
- Web.Api: `PUT users/{id}/access` (menggantikan `PUT users/{id}/branches`), CRUD profil, `GET users`, aktif/nonaktif, reset password.
- Web.App: `MenuCatalog` (seluruh menu W1–W9 didefinisikan sekarang), policy provider + handler, `[MenuAccess]`, tag helper, **Sidebar hanya `CanView`**, BranchSwitcher dari Akses Cabang.
- Layar §4.7: Menu Access, Branch Access, Users (create dengan pilihan Menu Access & Branch Access), Menus, API Roles, Branches.
- Test arsitektur penegakan `[MenuAccess]`; integration test `BranchAccess` & migrasi data.

### Fase W2 — Ekspor & Master Data
- Fondasi ekspor (§5): `ExportColumn<T>`, `PagedExportRunner`, Excel & PDF list, komponen kop/footer PDF, partial `_ExportButtons`.
- Master: UoM, TaxCode (tarif ber-tanggal efektif + rasio DPP), Item (+ konversi satuan), Warehouse, Vendor (toleransi selisih), Customer (NPWP/NITKU, credit limit).
- Partnership: Farmer, Coop, Contract (wizard skema PriceContract/ProfitSharing, harga jaminan per rentang bobot, bonus/potongan; Draft → Active → Inactive) + PDF kontrak.
- Komponen **Attachments** (Dropzone → `AttachmentController` → use case attachment; JPEG/PNG/WEBP/PDF ≤ 10 MB, maks 20) + endpoint lookup Tom-Select.

### Fase W3 — Finance Setup
- COA bertingkat (tree, header vs postable, akun kontra, kategori arus kas), cost center, periode fiskal (buka/tutup + checklist), template jurnal, mapping jurnal otomatis, Master Kas/Bank. Ekspor COA.

### Fase W4 — Pengadaan & Gudang
- PO (approve/cancel/close, progres penerimaan, **PDF PO**), BPB (**PDF**), transfer stok, retur kandang → induk, mutasi pakan; saldo stok & kartu stok (ekspor Excel/PDF).

### Fase W5 — Produksi
- Siklus & chick-in (wizard), detail siklus (tab Recording, Harvest, Coop Stock, Performance, Cost, Attachments), **daily recording** (input admin + revisi + timeline, W-11), panen per truk, grafik performa, tutup siklus (+ PDF ringkasan).

### Fase W6 — Penjualan & AR
- SO (approve, approve-over-limit dengan alasan + tampilan exposure), DO (**PDF delivery note**), Sales Invoice (Draft → post, **PDF invoice**), penerimaan customer (**PDF receipt**), uang muka, void, nota kredit (**PDF**), kartu piutang & aging (ekspor).

### Fase W7 — AP, Kas & Bank
- Vendor Invoice (3-way match, `post-with-variance`), Payment Voucher vendor & plasma (maker-checker, **PDF PV**), kas masuk/keluar (**PDF**), transfer kas/bank, buku kas/bank, rekonsiliasi bank (impor CSV, tampilan dua kolom, auto/manual match), kartu & aging hutang (ekspor).

### Fase W8 — HPP & Settlement Plasma
- HPP siklus (rincian & ekspor), settlement (draft & hitung ulang, rincian komponen, approve checker, **PDF settlement**, lanjut PV plasma).

### Fase W9 — Jurnal, Laporan, Tutup Buku, Pajak & Dashboard
- Jurnal manual (maker-checker, **PDF journal voucher**), daftar jurnal otomatis per dokumen, partial `_JournalPreview` di detail dokumen W4–W8.
- Laporan: General Ledger, Trial Balance, Income Statement, Balance Sheet, Cash Flow, Profitability — Excel (builder berstruktur) & PDF.
- Tutup periode & tahun (checklist), rekap pajak PPN/PPh (Excel/PDF + CSV yang sudah ada), monitoring event gagal + retry.
- `GetDashboardSummaryQuery` + dashboard KPI & grafik.

### Fase W10 — Pengerasan
- Responsif (tablet), aksesibilitas, bundling/minify aset, security headers (CSP), lockout login, health check, Dockerfile Web.App + `docker-compose` (2 container, satu volume storage), panduan pengguna singkat per modul, log audit ekspor & perubahan akses.

```
W0 ─► W1 ─► W2 ─► W3 ─┬─► W4 ─► W5 ─► W6 ─► W7 ─► W8 ─► W9 ─► W10
                      └─ (W3 wajib sebelum W6/W7: kas/bank & periode fiskal)
```

---

## 8. Daftar Task W0 ✅ (semua selesai, lihat §10)

1. `Web.App.csproj`: hapus properti redundan, referensi `Infrastructure.csproj`.
2. Pecah `AddInfrastructure` → `AddInfrastructureCore` / `AddJwtAuthentication` / `AddBackgroundJobs`; Web.Api memakai Core + Jwt (job nonaktif).
3. Background job di Web.App: `BackgroundJobs:Enabled`, advisory lock untuk cleanup, health check job, Web.App masuk `docker-compose`; jalankan seluruh test.
4. Invalidasi cache lintas proses (`NOTIFY` di command akses yang ada + listener di semua host) + integration test.
5. `Program.cs` Web.App: Serilog, `AddApplication`, `AddInfrastructureCore`, cookie auth, exception handler, status code pages, security headers frame.
6. Pangkas `wwwroot`, branding, primary color `#0984E3`, matikan theme customizer; mainboard + iframe (§3.10: `mainboard.js`, `frame.js`, hash routing, postMessage, redirect di luar frame, keluar dari frame saat sesi habis); `_Layout` ramping + `_LayoutAuth`; hapus halaman contoh.
7. Domain `User.IsActive` + `security_stamp` (+ unit test) dan migration `PhaseW0_UserStatus`.
8. Application: `SignInUserCommand`, `GetCurrentUserQuery`, `ChangeOwnPasswordCommand`; `LoginUserCommand` menolak user nonaktif.
9. Login/logout/change password, `OnValidatePrincipal`, BranchSwitcher, UserMenu.
10. `Result` → UI, filter concurrency, token double-submit, `[WorkflowAction]`/`IWorkflowActionService`, partial umum (termasuk `_DocumentActions`), halaman error 403/404/500.
11. `tests/Web.App.IntegrationTests` (login, logout, user nonaktif) + verifikasi end-to-end (Web.App dengan background job).

---

## 9. Status Keputusan

Seluruh keputusan W-1 s.d. W-20 sudah disepakati (lihat §1). Keputusan baru yang muncul saat implementasi ditambahkan ke tabel §1 dengan nomor lanjutan (W-21, …).

---

## 10. Realisasi Fase W0 — Fondasi (2026-10-02)

### 10.1 Backend (Domain / Application / Infrastructure / Web.Api)
| Area | Realisasi |
|---|---|
| `User` | `IsActive` + `SecurityStamp` (32 char), `ChangePassword`, `Deactivate` (idempotent, memperbarui stamp), `Activate`. Error baru: `Users.InvalidCredentials`, `Users.Inactive`, `Users.InvalidCurrentPassword`. Migration **`PhaseW0_UserStatus`** (user lama tetap aktif, masing-masing mendapat stamp acak). |
| Use case baru | `SignInUserCommand` (tanpa token; email salah & password salah memberi error yang sama), `GetUserSessionQuery` (aktif + stamp, untuk validasi cookie), `GetCurrentUserQuery` (profil + cabang efektif aktif, urut kode), `ChangeOwnPasswordCommand` (verifikasi password lama, stamp baru, **refresh token API dicabut**). |
| Login/refresh API | ⚠️ `POST users/login` & `users/refresh-token` kini menolak user nonaktif (400 `Users.Inactive`). |
| `AddInfrastructure` | ⚠️ Dihapus, diganti `AddInfrastructureCore` (DB, storage, user context, akses cabang, invalidasi cache, `ITokenProvider`) + `AddJwtAuthentication` (JWT + policy permission, khusus Web.Api) + `AddBackgroundJobs` (outbox + cleanup + health check `outbox`, hanya bila `BackgroundJobs:Enabled`). |
| Background job | Web.Api: `BackgroundJobs:Enabled=false` (appsettings). Web.App: `true`. Cleanup lampiran memakai `pg_try_advisory_xact_lock` (aman untuk banyak replika). Integration test Web.Api menyalakan job lewat setting. |
| Invalidasi cache | Abstraksi `ICacheInvalidator` (Application) → `PostgresCacheInvalidator`: hapus lokal + `pg_notify('cache_invalidation', 'key:…'/'tag:…')`. `CacheInvalidationListener` (hosted service di semua host) `LISTEN` + hapus lokal; saat (re)connect membuang tag `permissions`. Handler `AssignUserRoles`, `AssignUserBranches`, `UpdateRole` kini memakai `ICacheInvalidator`. |

### 10.2 Web.App
| Area | Realisasi |
|---|---|
| Proyek | `Web.App.csproj` ramping + referensi Infrastructure; `.editorconfig` lokal menonaktifkan CA1054/CA1055/CA1056 (URL MVC berupa path string) dengan alasan tertulis. `Program.cs`: Serilog, `AddApplication` + `AddInfrastructureCore` + `AddBackgroundJobs` + `AddWebApp`, exception handler, status code pages `/Error/{code}`, security headers, request localization, `/health` (termasuk `outbox`). |
| Aset | `wwwroot` 84 MB → 21 MB: plugin tersisa apexchart, chartjs, dropzone, flatpickr, fontawesome, imask, jquery, jquery.toast, simplebar, sweetalert2, tabler-icons, tom-select; `lib/` tinggal jquery-validation(+unobtrusive). Aset yang dibuang dapat disalin lagi dari `docs/html-template/assets`. Logo baru `img/logo*.svg`. |
| Tema | `#FE9F43` → `#0984E3` di `style.css` (termasuk `rgba(254,159,67,…)`, shade hover `rgb(7,104,178)`/`rgb(6,96,165)`, soft `#FFF6EE` → `#E7F2FC`); `data-color="primary"` tetap; `theme-script.js` & customizer dihapus; override di `css/theme.css`. |
| Mainboard | `Main/Index` = header (logo, BranchSwitcher, UserMenu) + `SidebarViewComponent` (dari `IMainboardMenuProvider`; W0 menu statis Dashboard & My Account) + iframe `content-frame`. `mainboard.js`: hash mirror (`replaceState`), title & menu aktif dari `postMessage`, guard path internal, loader, tinggi iframe, switch cabang (fetch + reload iframe). `frame.js`: redirect ke `/#path` bila dibuka langsung, lapor navigasi ke parent. |
| Layout | `_Layout` (halaman iframe: aset inti, toast via `data-*` bukan inline script, `app.js`), `_LayoutAuth` (login & error), `js/jquery-compat.js` (shim `$.parseJSON/$.trim/$.isFunction/$.isArray` yang dihapus jQuery 4 tetapi dipakai jquery-validation-unobtrusive). |
| Auth | Cookie `ip.auth` (HttpOnly, SameSite=Lax, sliding 8 jam, Remember me), `AppCookieEvents.ValidatePrincipal` (aktif + stamp, cache 5 menit bertag `permissions`), AJAX → 401/403. Login di iframe → keluar ke jendela utama dengan `ReturnUrl=/#<path>`; deep link `/Main#/x` dipertahankan melewati login. Ganti password menerbitkan ulang cookie sesi saat ini; sesi lain keluar. |
| Cabang | `IBranchContext` (cookie `ip.branch`; seleksi basi jatuh ke cabang pertama; "All branches" hanya untuk akses semua cabang). `POST Main/SwitchBranch` → 204/403. |
| Infrastruktur UI | `AppController` (toast `success`/`error`), `ResultExtensions` (error use case → validation summary), `DbExceptionFilter` (concurrency & unique violation → pesan + redirect balik / 409 AJAX), `FormTokenFilter` + `<form-token />` (double-submit 24 jam), `[WorkflowAction]` + `IWorkflowActionService` (langsung, siap dialihkan ke approval terpusat), `DisplayFormatter` (`Fmt`: angka/uang/tanggal id-ID, Asia/Jakarta). Partial: `_PageHeader`, `_FilterBar`, `_Pagination`, `_StatusBadge`, `_DocumentActions`. |
| Halaman | Login, Dashboard (placeholder), Change Password, Error 403/404/500. |
| Deploy | `src/Web.App/Dockerfile`, service `web-app` di `docker-compose` (port 5002, volume `./.containers/uploads` bersama Web.Api). Dev: `FileStorage:RootPath` Web.App menunjuk folder `uploads` milik Web.Api. |

### 10.3 Pengujian & verifikasi
- **Test: 207 lulus** — Domain 124 (+4), Application 48 (+7), Arsitektur 11 (+3: lapisan dalam tidak bergantung Web.App, Web.App tidak bergantung Web.Api, controller tanpa akses data langsung), Integration Web.Api 13, **Web.App.IntegrationTests 11 (baru)**: login/logout, password salah, user nonaktif, sesi berakhir saat dinonaktifkan, AJAX 401, header frame, halaman 404, `FormTokenFilter` (3 skenario).
- **End-to-end 49/49** (Playwright + fetch + psql, Web.App :5098 + Web.Api :5099, DB `intiplasma_verify` dibuat & dihapus): outbox diproses Web.App saat job Web.Api mati, hash/title/menu aktif mengikuti iframe, Back browser, refresh dengan hash, redirect halaman langsung, guard hash eksternal, 404 di iframe, branch switcher (admin vs staf, 403 di luar akses), ganti password (salah, mismatch klien, sukses, sesi lain keluar, refresh token API dicabut), **invalidasi cache lintas proses** (role diubah lewat Web.Api → sesi user nonaktif di Web.App langsung berakhir), user nonaktif ditolak Web.App & Web.Api, sesi habis di iframe → login jendela utama → kembali ke halaman semula, logout.

### 10.4 Catatan & penyesuaian
- ⚠️ Skrip verifikasi API lama (Fase 5–9) yang hanya menjalankan Web.Api perlu `BackgroundJobs__Enabled=true`, karena jurnal otomatis/outbox tidak lagi diproses Web.Api secara default.
- Error validasi FluentValidation dari command tidak membawa nama property (`ErrorCode` validator), sehingga tampil di validation summary; validasi per field memakai DataAnnotations view model. Kontrak error API tidak diubah.
- Route default `Main/Index` = `/`, jadi URL mainboard berbentuk `/#/path` (bukan `/Main#/path`); keduanya berfungsi.
- `Program` hasil top-level statements di .NET 10 bersifat public → test arsitektur memakai `typeof(Web.Api.Program)` / `typeof(Web.App.Program)` secara eksplisit.
- Belum ada: menu dari DB & `[MenuAccess]` (W1), notifikasi header, lockout login (W10), Data Protection key ring bersama untuk banyak replika Web.App (W10).

---

## 11. Rencana Detail Fase W1 — Akses Menu, Akses Cabang & Administrasi

> Disusun 2026-10-03 setelah W0 di-commit (`c47a18e`, `29631bc`). Melengkapi §4 (desain) dengan detail teknis, urutan kerja, dan keputusan yang masih perlu disepakati (§11.10).

### 11.1 Ruang lingkup & hasil akhir
- Admin dapat mengelola **Akses Menu** (profil + matriks hak), **Akses Cabang** (profil + daftar cabang), **User** (buat, ubah, akses, aktif/nonaktif, reset password), **Menu** (label/ikon/urutan/aktif), **Role API**, dan **Cabang** — semuanya di Web.App.
- Sidebar mainboard hanya menampilkan menu `CanView` dari Akses Menu user; setiap action controller dijaga `[MenuAccess]`.
- `BranchAccess` (dipakai semua use case, API & Web.App) membaca **profil Akses Cabang**; `user_branches` dan permission `branches:access-all` dihapus.
- **Selesai bila**: user staf dengan profil terbatas hanya melihat menu & cabangnya, tombol tanpa hak tersembunyi, URL langsung ke halaman tanpa hak → 403, dan perubahan profil berlaku tanpa login ulang di Web.App **dan** Web.Api.

### 11.2 Model domain (schema `identity`)
| Aggregate / entity | Isi | Invariant & perilaku |
|---|---|---|
| `Menu` (aggregate, disinkron dari katalog) | `Code` (unik, ≤ 100), `ParentCode`, `Name`, `DefaultName`, `Icon`, `Route`, `SortOrder`, `IsActive`, `IsAvailable` (layar sudah dirilis), `SupportsCreate/Edit/Delete/Export`, `IsCustomized` | `Sync(catalogEntry)` hanya menimpa struktur (parent, route, supports, available, default name); nama/ikon/urutan/aktif tidak ditimpa bila `IsCustomized`. `Customize(name, icon, sortOrder, isActive)` menandai `IsCustomized`. |
| `MenuAccessProfile` (aggregate) | `Name` (unik), `Description`, `IsSystem`, `Items` (`MenuId`, `CanView/Create/Edit/Delete/Export`) | `SetItems(items, menus)`: hak selain View ⇒ View wajib; hak yang tidak didukung menu ditolak; baris tanpa hak apa pun dibuang. Profil sistem **Full Access** tidak bisa diubah/dihapus (haknya dihitung, tanpa baris). `Duplicate(newName)`. Event `MenuAccessProfileChangedDomainEvent`. |
| `BranchAccessProfile` (aggregate) | `Name` (unik), `Description`, `AllBranches`, `IsSystem`, `Branches` (`BranchId`) | `AllBranches` ⇒ daftar dikosongkan; selain itu minimal 1 cabang. Profil sistem **All Branches** tidak bisa diubah/dihapus. Event `BranchAccessProfileChangedDomainEvent`. |
| `User` (+) | `MenuAccessProfileId?`, `BranchAccessProfileId?`, `DefaultBranchId?`, `UpdateProfile(first, last)`, `SetAccess(menuProfileId, branchProfileId, defaultBranchId)`, `ResetPassword(hash)` (= stamp baru) | `_branches`/`SetBranches` & `UserBranch` **dihapus**. Validasi "default branch termasuk profil cabang" dilakukan handler (butuh data profil). Password awal/reset **tidak** wajib diganti saat login (W-21). |
| `DeletedUser` (tabel `identity.user_old`, W-22) | Salinan user saat dihapus: `id` (Guid v7 baris backup), `user_id`, `email`, `first_name`, `last_name`, `password_hash`, `is_active`, `menu_access_profile_id`, `branch_access_profile_id`, `default_branch_id`, `roles` (jsonb: id + nama role), audit asli (`created_at_utc/by`, `modified_at_utc/by`), `deleted_at_utc`, `deleted_by`, `reason` (opsional) | Dibuat oleh `User.Delete…` → handler menulis backup **dalam transaksi yang sama** sebelum `DELETE`; tidak ada FK ke `users` (user sudah hilang). Tanpa fitur restore di UI (pemulihan manual lewat SQL bila perlu). |
| Error baru | `Menus.NotFound`, `MenuAccessProfiles.NotFound/NameNotUnique/SystemReadOnly/InUse(count)/RightNotSupported/ViewRequired`, `BranchAccessProfiles.NotFound/NameNotUnique/SystemReadOnly/InUse(count)/BranchRequired`, `Users.DefaultBranchNotAccessible`, `Users.CannotDeactivateSelf`, `Users.CannotDeleteSelf`, `Users.LastAdministrator` | |

### 11.3 Migration `PhaseW1_AccessControl` (urutan di dalam satu migration)
1. Tabel baru: `menus`, `menu_access_profiles`, `menu_access_profile_items`, `branch_access_profiles`, `branch_access_profile_branches`, `user_old` (backup user terhapus, index `user_id` & `email`); kolom `users.menu_access_profile_id`, `branch_access_profile_id`, `default_branch_id` (FK; `RESTRICT` untuk profil, `SET NULL` untuk cabang default).
2. Data: profil sistem **Full Access** & **All Branches** (id tetap, ditulis di kode) dibuat.
3. Data: user dengan role yang memiliki `branches:access-all` → `branch_access_profile_id = All Branches`.
4. Data: user lain yang punya `user_branches` → satu profil per **kombinasi cabang yang sama** (nama "Branches: BDG, JKT"), user diarahkan ke profilnya; `default_branch_id` = cabang pertama (urut kode).
5. Data: user dengan role `Administrator` → `menu_access_profile_id = Full Access`.
6. Hapus baris `role_permissions` dengan `branches:access-all`; `DROP TABLE identity.user_branches`.
7. `Down`: membuat ulang `user_branches` dari profil (best effort) — dicatat sebagai *lossy*.
- Seeder: memastikan kedua profil sistem ada dan admin seed memakai keduanya (idempoten). `Permissions.BranchesAccessAll` dihapus dari katalog; `Administrator.SyncSystemPermissions` otomatis membuangnya.

### 11.4 Application (use case baru/berubah)
| Kelompok | Use case | Catatan |
|---|---|---|
| Users | `GetUsersQuery` (search, status, menu profile, branch profile; paging), `GetUserByIdQuery` (diperluas: status, profil, cabang default, role, **cabang efektif & menu efektif**), `CreateUserCommand` (data + password awal + akses + role API, satu transaksi), `UpdateUserCommand` (nama), `SetUserAccessCommand`, `AssignUserRolesCommand` (tetap), `DeactivateUserCommand` / `ActivateUserCommand` (tidak bisa menonaktifkan diri sendiri), `ResetUserPasswordCommand` (stamp baru + cabut refresh token; tanpa kewajiban ganti password), `DeleteUserCommand(userId, reason?)` (W-22: tolak hapus diri sendiri & admin Full Access aktif terakhir → tulis `user_old` → hapus user; `user_roles` & `refresh_tokens` ikut terhapus lewat cascade; email bisa dipakai lagi) | Semua perubahan akses & hapus → `ICacheInvalidator` (key user: permission, menu, branch, session) → sesi user terhapus langsung berakhir. |
| Akses Menu | `GetMenuAccessProfilesQuery` (+ jumlah user, jumlah menu View), `GetMenuAccessProfileByIdQuery` (matriks lengkap seluruh menu katalog), `Create…`, `Update…` (nama + matriks), `Duplicate…`, `Delete…` (tolak bila dipakai) | Update/Delete → `RemoveByTagAsync(permissions)`. |
| Akses Cabang | `GetBranchAccessProfilesQuery`, `GetBranchAccessProfileByIdQuery`, `Create…`, `Update…`, `Delete…` | idem. |
| Menu | `SyncMenuCatalogCommand(entries)`, `GetMenusQuery` (tree), `UpdateMenuCommand` (customize) | Sync dipanggil Web.App saat startup. |
| Akses saat ini | `GetMyMenuAccessQuery` (dipakai provider menu & policy), `GetCurrentUserQuery` (tetap; cabang dari profil + `DefaultBranchId`) | |
| Dihapus | `AssignUserBranchesCommand` (+ validator) | ⚠️ diganti `SetUserAccessCommand`. |

### 11.5 Infrastructure
- `BranchAccess`: scope = profil Akses Cabang user (`AllBranches` atau daftar cabang **aktif**); tanpa profil ⇒ scope kosong. Tetap di-cache 10 menit bertag `permissions`.
- `MenuAccessProvider` (`IMenuAccessProvider`, abstraksi di Application): hak efektif per user (`FullAccess` atau peta `code → hak`), cache `menu-access:user:{id}` bertag `permissions`.
- `PermissionCacheKeys`: tambah `MenuAccessForUser`; semua invalidasi lewat `ICacheInvalidator` (lintas proses, W0).
- EF configuration + DbSet `IApplicationDbContext`: `Menus`, `MenuAccessProfiles`, `BranchAccessProfiles`.

### 11.6 Web.Api
| Endpoint | Status |
|---|---|
| `GET users`, `PUT users/{id}`, `PUT users/{id}/access`, `POST users/{id}/deactivate`, `POST users/{id}/activate`, `POST users/{id}/reset-password`, `DELETE users/{id}` | baru (`users:manage` / `users:read`) |
| `PUT users/{id}/branches` | ⚠️ **dihapus** (diganti `PUT users/{id}/access`) |
| `GET/POST/PUT/DELETE branch-access-profiles` | baru (`users:manage`) — akses cabang juga berlaku untuk API/mobile |
| Akses Menu & Menu | **tidak** dibuat endpoint API (khusus Web.App) — W-24 |
| `GET permissions` | `branches:access-all` hilang dari daftar |

### 11.7 Web.App
**Perbaikan W0** ✅ (selesai 2026-10-03, sebelum W1 dimulai):
- `BranchSwitcher/Default.cshtml` memakai tampilan `select-store-dropdown` (commit `29631bc`) **dengan** hook yang dibutuhkan `mainboard.js` (`.branch-switcher`, `data-switch-url`, `.active-branch-name`); item "All Branches" kini berlabel benar.
- Kotak search dipindah ke partial `Mainboard/_MenuSearch.cshtml` dan dijadikan **pencarian menu** (W-23): mencari judul menu & grup dari link sidebar (otomatis hanya menu `CanView` setelah W1), Enter/klik membuka di iframe, `Ctrl+K` fokus, Escape/klik luar menutup & mengosongkan. Konten demo template dihapus.
- Verifikasi: header 15/15 (Playwright), regresi E2E W0 49/49.

**Otorisasi**
- `MenuRight` (`View/Create/Edit/Delete/Export`), `[MenuAccess(code, right)]` (controller atau action; action menimpa controller), `[AuthenticatedOnly]` (penanda halaman umum: dashboard, ganti password).
- `MenuPolicyProvider` (`menu:{code}:{right}`) + `MenuAuthorizationHandler` (`IMenuAccessProvider`); nama policy lain jatuh ke provider default.
- Tag helper `asp-menu` / `asp-right` pada `<a>`, `<button>`, `<form>` (elemen tidak dirender bila tidak berhak); `IMenuRights` untuk kondisi di view.
- 403 untuk halaman tanpa hak → `/Error/403` di dalam iframe.

**Katalog menu (draf, `Web.App/Infrastructure/Authorization/MenuCatalog.cs`)** — hak: C=Create, E=Edit, D=Delete, X=Export (View selalu ada). `Rilis` = fase layar tersedia; menu belum rilis **tidak tampil di sidebar** tetapi tampil abu-abu di matriks (W-25).

| Grup (ikon) | Kode | Menu | Hak | Rilis |
|---|---|---|---|---|
| Dashboard (`ti-smart-home`) | `dashboard` | Dashboard | – | W0 |
| Master Data (`ti-database`) | `master.uoms`, `master.tax-codes`, `master.items`, `master.warehouses`, `master.vendors`, `master.customers` | UoM, Tax Codes, Items, Warehouses, Vendors, Customers | C E D X | W2 |
| Partnership (`ti-users-group`) | `partnership.farmers`, `partnership.coops`, `partnership.contracts` | Farmers, Coops, Contracts | C E D X | W2 |
| Procurement (`ti-shopping-cart`) | `procurement.purchase-orders` | Purchase Orders | C E D X | W4 |
| Inventory (`ti-building-warehouse`) | `inventory.goods-receipts`, `inventory.stock-transfers`, `inventory.stock-returns`, `inventory.feed-mutations` | Goods Receipts, Transfers, Returns, Feed Mutations | C X | W4 |
| | `inventory.stock` | Stock Balance & Card | X | W4 |
| Production (`ti-egg`) | `production.cycles`, `production.recordings`, `production.harvests` | Cycles & Chick-in, Daily Recordings, Harvests | C E X | W5 |
| Sales (`ti-receipt`) | `sales.orders`, `sales.invoices` | Sales Orders, Sales Invoices | C E D X | W6 |
| | `sales.deliveries`, `sales.credit-notes`, `sales.receipts` | Delivery Orders, Credit Notes, Customer Receipts | C E X | W6 |
| | `sales.receivables` | Receivable Ledger & Aging | X | W6 |
| Finance (`ti-building-bank`) | `finance.accounts`, `finance.cost-centers`, `finance.journal-templates`, `finance.cash-bank-accounts` | COA, Cost Centers, Journal Templates, Cash/Bank Accounts | C E D X | W3 |
| | `finance.fiscal-periods`, `finance.journal-mappings` | Fiscal Periods, Auto Journal Mappings | E X | W3 |
| | `finance.vendor-invoices`, `finance.payment-vouchers`, `finance.cash-transactions` | Vendor Invoices, Payment Vouchers, Cash In/Out | C E D X | W7 |
| | `finance.bank-transfers`, `finance.bank-reconciliations` | Bank Transfers, Reconciliations | C E X | W7 |
| | `finance.payables` | Payable Ledger & Aging | X | W7 |
| | `finance.journals` | Journals | C E D X | W9 |
| Costing (`ti-calculator`) | `costing.cycle-costs` | Cycle Cost | X | W8 |
| | `costing.settlements` | Plasma Settlements | C E X | W8 |
| Reports (`ti-report-analytics`) | `reports.general-ledger`, `reports.trial-balance`, `reports.income-statement`, `reports.balance-sheet`, `reports.cash-flow`, `reports.profitability`, `reports.tax` | GL, Trial Balance, Income Statement, Balance Sheet, Cash Flow, Profitability, Tax Recap | X | W9 |
| Administration (`ti-settings`) | `admin.users` | Users | C E D X | W1 |
| | `admin.menu-access`, `admin.branch-access` | Menu Access, Branch Access | C E D X | W1 |
| | `admin.menus` | Menus | E | W1 |
| | `admin.api-roles`, `admin.branches` | API Roles, Branches | C E X | W1 |
| | `admin.failed-events` | Failed Events | E | W9 |

Halaman "Change Password" tetap di menu user (`[AuthenticatedOnly]`), bukan bagian katalog.

**Layar**
| Layar | Detail |
|---|---|
| Users | List (search, status, Menu Access, Branch Access, paging); Create (nama, email, password awal + konfirmasi, Menu Access, Branch Access, cabang default yang difilter dari profil terpilih, role API opsional); Detail (status, profil, menu efektif ringkas, cabang efektif, role); aksi Edit, Change Access, Activate/Deactivate (konfirmasi), Reset Password (modal, password baru diketik admin), **Delete** (`CanDelete`; konfirmasi + alasan opsional; backup ke `user_old`). |
| Menu Access | List (nama, #user, #menu View, sistem); Create/Edit: matriks tree per grup — checkbox per sel, centang semua per baris/kolom/grup, sel tidak didukung disembunyikan, menu belum rilis abu-abu; Duplicate; Delete (ditolak bila dipakai). |
| Branch Access | List (nama, all/#cabang, #user); Create/Edit (toggle All branches, multi-select cabang aktif dengan pencarian); Delete. |
| Menus | Tree; edit nama tampilan, ikon (class tabler + pratinjau), urutan, aktif. |
| API Roles | List, Create/Edit (nama, deskripsi, permission dikelompokkan per modul). |
| Branches | List, Create, Edit (nama, alamat, telepon, aktif). |

**Lain-lain**
- `DatabaseMainboardMenuProvider` menggantikan menu statis (grup tanpa item `CanView` & tersedia disembunyikan).
- `Program.cs`: `SyncMenuCatalogCommand` dijalankan saat startup (gagal → log error, aplikasi tetap jalan dengan katalog terakhir).
- `BranchContext`: memakai `DefaultBranchId` sebagai pilihan awal.

### 11.8 Pengujian & verifikasi
- **Domain**: invariant `MenuAccessProfile` (View wajib, hak tidak didukung, sistem read-only, duplicate), `BranchAccessProfile` (all vs daftar, minimal 1), `Menu.Sync` vs `Customize`, `User.SetAccess/ResetPassword`.
- **Application**: CRUD profil (nama unik, tolak hapus profil terpakai), create user dengan akses + cabang default tidak valid, nonaktifkan/hapus diri sendiri ditolak, hapus admin Full Access terakhir ditolak, hapus user menulis backup lengkap (termasuk role) sebelum menghapus, sync katalog idempoten, invalidasi cache dipanggil.
- **Integration Web.Api** (Testcontainers): migrasi data (`user_branches` & `branches:access-all` → profil), `BranchAccess` (all, daftar, cabang nonaktif, tanpa profil), endpoint `users/{id}/access`, `PUT users/{id}/branches` → 404.
- **Arsitektur**: setiap action controller Web.App punya `[MenuAccess]`/`[AllowAnonymous]`/`[AuthenticatedOnly]`; setiap kode `[MenuAccess]` ada di `MenuCatalog`; kode katalog unik.
- **Integration Web.App**: sidebar hanya `CanView`; 403 tanpa hak; tombol tersembunyi; Full Access.
- **End-to-end (Playwright)**: admin membuat profil "Finance Staff" & "Area Alpha" → membuat user staf → staf login: sidebar & cabang terbatas, URL admin → 403; admin menambah hak saat staf login → menu muncul setelah reload tanpa login ulang; API (token staf) hanya melihat cabang profilnya; ubah profil cabang di Web.App → API langsung mengikuti; hapus profil terpakai ditolak; reset password → sesi staf berakhir; hapus user staf → baris `user_old` ada (psql), sesi & token API staf langsung tidak berlaku, email bisa didaftarkan ulang; pencarian menu hanya menemukan menu `CanView`.

### 11.9 Urutan task
1. ~~Perbaikan BranchSwitcher & pencarian menu (W-23)~~ ✅ selesai 2026-10-03.
2. Domain: `Menu`, `MenuAccessProfile`, `BranchAccessProfile`, perubahan `User` (+ unit test).
3. EF configuration + migration `PhaseW1_AccessControl` (DDL + migrasi data) + seeder profil sistem.
4. `BranchAccess` & `MenuAccessProvider` + cache keys; hapus `branches:access-all` & `AssignUserBranchesCommand`.
5. Use case §11.4 (+ unit test).
6. Endpoint Web.Api §11.6 (+ integration test).
7. Web.App: `MenuCatalog`, sync saat startup, policy/handler/atribut/tag helper, `DatabaseMainboardMenuProvider`, test arsitektur.
8. Layar Administration (Users, Menu Access, Branch Access, Menus, API Roles, Branches).
9. Integration test Web.App + verifikasi end-to-end; update RANGKUMAN & §12 (realisasi W1).

### 11.10 Keputusan (disepakati 2026-10-03)
| # | Topik | Keputusan |
|---|-------|-----------|
| W-21 | Password awal / reset oleh admin | Admin mengetik password awal/baru; user **tidak wajib** menggantinya saat login. |
| W-22 | Hapus user | User **bisa dihapus**; sebelum dihapus datanya **disalin ke tabel backup `identity.user_old`** dalam transaksi yang sama (detail §11.2/§11.4). Hapus diri sendiri & admin Full Access aktif terakhir ditolak. |
| W-23 | Kotak search di header | Dijadikan **pencarian menu** (sudah dikerjakan, §11.7). |
| W-24 | Endpoint API untuk Akses Menu & Menu | **Tidak dibuat**. |
| W-25 | Menu yang belum rilis | **Tidak tampil di sidebar** (tampil abu-abu di matriks Akses Menu). |
| W-26 | Kebijakan password | **Minimal 8 karakter**. |

---

## 12. Realisasi Fase W1 — Akses Menu, Akses Cabang & Administrasi (2026-10-03)

### 12.1 Domain & data
| Area | Realisasi |
|---|---|
| `Domain.Access` | `MenuRights` (flags View/Create/Edit/Delete/Export), `Menu` (`Sync` vs `Customize`, `RemoveFromCatalog`, `IsUsable`, `SupportedRights`), `MenuAccessProfile` (+ `MenuAccessItem`, `MenuAccessGrant`; View wajib, hak tak didukung ditolak, Full Access read-only, `Duplicate`), `BranchAccessProfile` (+ `BranchAccessProfileBranch`; all vs daftar, minimal 1 cabang, All Branches read-only, `Covers`). Id profil sistem tetap: Full Access `0199a3c0-…-0001`, All Branches `0199a3c0-…-0002`. |
| `User` | `MenuAccessProfileId`, `BranchAccessProfileId`, `DefaultBranchId`, `UpdateProfile`, `SetAccess`; `UserBranch`/`SetBranches` dihapus. `DeletedUser` (tabel `identity.user_old`, W-22) menyalin data user + `role_ids uuid[]` + `role_names text[]` + audit asli + `deleted_at/by` + alasan. Error baru: `Users.NoMenuAccess/DefaultBranchNotAccessible/CannotDeactivateSelf/CannotDeleteSelf/LastAdministrator`. |
| Permission | ⚠️ `branches:access-all` dihapus dari katalog (W-14). |
| Migration **`PhaseW1_AccessControl`** | Tabel `menus`, `menu_access_profiles(+_items)`, `branch_access_profiles(+_branches)`, `user_old`; kolom akses di `users` (FK profil `RESTRICT`, cabang default `SET NULL`). Migrasi data: profil sistem; pemegang `branches:access-all` → All Branches; `user_branches` → satu profil per kombinasi cabang ("Branches: BDG, JKT", default = cabang pertama); role Administrator → Full Access; hapus permission lama; drop `user_branches`. `Down` best effort (lossy untuk All Branches). Diuji: data gaya lama → migrate → hasil sesuai; rollback → `user_branches` kembali; migrate ulang OK. |
| Seeder | Membuat profil sistem bila belum ada dan memberi admin seed Full Access + All Branches. |

### 12.2 Application, Infrastructure, Web.Api
- **Users** (`Application/Users/Manage`): `GetUsersQuery` (search, status, profil), `CreateUserCommand` (password awal, akses, role API — satu transaksi), `UpdateUserCommand`, `SetUserAccessCommand`, `Deactivate/ActivateUserCommand` (cabut refresh token), `ResetUserPasswordCommand` (tanpa wajib ganti, W-21), `DeleteUserCommand` (backup `user_old` → hapus; tolak diri sendiri & admin Full Access aktif terakhir). `UserAccessRules`: validasi profil + cabang default, guard admin terakhir, invalidasi semua cache per user. `GetUserByIdQuery` diperluas (status, profil, cabang default, role id/nama, cabang efektif). `SignInUserCommand` menolak user tanpa Akses Menu. `GetCurrentUserQuery` + `DefaultBranchId`. `AssignUserBranchesCommand` ⚠️ dihapus.
- **Akses Menu / Akses Cabang / Menu** (`Application/Access`): list (+ jumlah user/menu/cabang), detail (matriks seluruh menu katalog), create/update/duplicate/delete (tolak bila dipakai, 409 `InUse`); update cabang mengosongkan cabang default user yang tak lagi tercakup. `SyncMenuCatalogCommand` (idempoten, mempertahankan kustomisasi), `GetMenusQuery`, `UpdateMenuCommand`. Perubahan profil/menu → `RemoveByTagAsync(permissions)` lintas proses.
- **Infrastructure**: `BranchAccess` membaca profil Akses Cabang (cabang aktif; tanpa profil = tidak ada cabang); `MenuAccessProvider` (`IMenuAccessProvider`) menghitung hak efektif per user (hanya menu usable; Full Access = semua hak yang didukung), cache `menu-access:user:{id}` bertag `permissions`.
- **Web.Api**: `GET users`, `PUT users/{id}`, `PUT users/{id}/access`, `POST users/{id}/deactivate|activate|reset-password`, `DELETE users/{id}?reason=`, CRUD `branch-access-profiles` (`users:read/manage`). ⚠️ `PUT users/{id}/branches` dihapus. Tidak ada endpoint Akses Menu/Menu (W-24).

### 12.3 Web.App
- **Otorisasi**: `MenuCatalog` (11 grup, 51 halaman; W1 merilis 6 halaman Administration, sisanya `IsAvailable=false`), `MenuCatalogSyncService` (hosted, retry 30 detik), `[MenuAccess(code, right)]` (turunan `AuthorizeAttribute`, policy `menu:{code}:{right}` lewat `MenuPolicyProvider` + `MenuAuthorizationHandler`), `[AuthenticatedOnly]`, `IMenuRights` (sekali per request), tag helper `asp-menu`/`asp-right` (a, button, form, li, div).
- **Sidebar**: `DatabaseMainboardMenuProvider` — Dashboard bawaan + grup katalog yang punya halaman `CanView`. Penyesuaian dari rencana §11.7: **Dashboard tidak masuk katalog** (semua user yang login melihatnya) dan grup sidebar "My Account" dihapus (Change Password tetap di menu user).
- **Layar `Areas/Admin`**: Users (list + filter, detail + cabang efektif, create dengan `<form-token />`, edit, change access + role API, activate/deactivate, reset password via modal, delete via modal + alasan), Menu Access (list, detail read-only, form matriks dengan centang baris/kolom/grup, View otomatis, menu "Coming soon" abu-abu, duplicate, delete), Branch Access (list, detail, form all/daftar + filter), Menus (daftar bertingkat, edit nama/ikon/urutan/aktif), API Roles (permission per modul; role sistem read-only), Branches (list, create, edit + aktif). Cabang default difilter lewat AJAX `Users/ProfileBranches`.
- `BranchContext` memakai `DefaultBranchId` sebagai pilihan awal.

### 12.4 Pengujian & verifikasi
- **Test: 250 lulus** — Domain 143 (+19), Application 60 (+12), Arsitektur 14 (+3: setiap action punya `[MenuAccess]`/`[AuthenticatedOnly]`/`[AllowAnonymous]`, setiap kode ada di katalog, katalog unik & halaman punya grup), Integration Web.Api 18 (+5: cakupan cabang dari profil termasuk perubahan tanpa login ulang, tanpa profil = 403, endpoint lama hilang, hapus user + backup + token dicabut, list user), Integration Web.App 15 (+4: tanpa Akses Menu ditolak, sidebar admin & menu belum rilis tersembunyi, user terbatas hanya melihat/membuka menu yang diberikan, create → delete user via UI dengan backup).
- **End-to-end W1 36/36** (Playwright, Web.App + Web.Api, DB `intiplasma_verify` dengan data gaya lama yang dimigrasi): profil hasil migrasi tampil; buat Branch Access, API Role, Menu Access (perilaku matriks), user staf (cabang default mengikuti profil, role API); staf hanya melihat Dashboard + Branches, cabang hanya Bandung, pencarian menu tak menemukan menu lain, `/Admin/Users` → Access denied, tanpa tombol Add; hak baru muncul tanpa login ulang; **Web.Api mengikuti perubahan Akses Cabang yang dibuat di Web.App** (403 → 200, lintas proses); hapus profil terpakai ditolak; rename menu terlihat user lain; reset password mengakhiri sesi staf (login dengan password baru tanpa paksaan ganti); hapus staf → baris `user_old` (alasan + nama role), sesi & refresh token berakhir, email bisa dipakai lagi; user tanpa Akses Menu tidak bisa login; tanpa error JS.
- **Regresi**: E2E W0 49/49 dan header 15/15 (skrip disesuaikan: akses staf lewat `users/{id}/access`, Change Password dibuka dari menu user, pencarian menu memakai "Users").

### 12.5 Catatan
- ⚠️ Setelah migrate, user selain Administrator **belum punya Akses Menu** sehingga belum bisa login ke Web.App sampai admin memberinya profil (API tetap jalan sesuai role).
- ⚠️ Skrip/klien yang memakai `PUT users/{id}/branches` harus beralih ke `branch-access-profiles` + `PUT users/{id}/access`.
- Ekspor (CanExport) baru dipakai mulai W2; hak Export sudah bisa diatur di matriks.
