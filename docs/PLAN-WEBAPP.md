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

### Fase W2 — Ekspor & Master Data ✅ (selesai 2026-10-03, realisasi §13)
- Fondasi ekspor (§5): `ExportColumn<T>`, `PagedExportRunner`, Excel & PDF list, komponen kop/footer PDF, partial `_ExportButtons`.
- Master: UoM, TaxCode (tarif ber-tanggal efektif + rasio DPP), Item (+ konversi satuan), Warehouse, Vendor (toleransi selisih), Customer (NPWP/NITKU, credit limit).
- Partnership: Farmer, Coop, Contract (wizard skema PriceContract/ProfitSharing, harga jaminan per rentang bobot, bonus/potongan; Draft → Active → Inactive) + PDF kontrak.
- Komponen **Attachments** (Dropzone → `AttachmentController` → use case attachment; JPEG/PNG/WEBP/PDF ≤ 10 MB, maks 20) + endpoint lookup Tom-Select.

### Fase W3 — Finance Setup ✅ (selesai 2026-10-03, realisasi §14)
- COA bertingkat (tree, header vs postable, akun kontra, kategori arus kas), cost center, periode fiskal (buka/tutup + checklist), template jurnal, mapping jurnal otomatis, Master Kas/Bank. Ekspor COA.

### Fase W4 — Pengadaan & Gudang ✅ (selesai 2026-10-03, realisasi §15)
- PO (approve/cancel/close, progres penerimaan, **PDF PO**), BPB (**PDF**), transfer stok, retur kandang → induk, mutasi pakan; saldo stok & kartu stok (ekspor Excel/PDF).

### Fase W5 — Produksi ✅ (selesai 2026-10-03, realisasi §16)
- Siklus & chick-in (wizard), detail siklus (tab Recording, Harvest, Coop Stock, Performance, Cost, Attachments), **daily recording** (input admin + revisi + timeline, W-11), panen per truk, grafik performa, tutup siklus (+ PDF ringkasan).

### Fase W6 — Penjualan & AR ✅ (selesai 2026-10-03, realisasi §17)
- SO (approve, approve-over-limit dengan alasan + tampilan exposure), DO (**PDF delivery note**), Sales Invoice (Draft → post, **PDF invoice**), penerimaan customer (**PDF receipt**), uang muka, void, nota kredit (**PDF**), kartu piutang & aging (ekspor).

### Fase W7 — AP, Kas & Bank ✅ (selesai 2026-10-03, realisasi §18)
- Vendor Invoice (3-way match, `post-with-variance`), Payment Voucher vendor & plasma (maker-checker, **PDF PV**), kas masuk/keluar (**PDF**), transfer kas/bank, buku kas/bank, rekonsiliasi bank (impor CSV, tampilan dua kolom, auto/manual match), kartu & aging hutang (ekspor).

### Fase W8 — HPP & Settlement Plasma ✅ (selesai 2026-10-03; rencana §19, realisasi §20)
- HPP siklus (rincian & ekspor), settlement (draft & hitung ulang, rincian komponen, approve checker, **PDF settlement**, lanjut PV plasma).

### Fase W9 — Jurnal, Laporan, Tutup Buku, Pajak & Dashboard ✅ (selesai 2026-10-04; rencana §21, realisasi §22)
- Jurnal manual (maker-checker, **PDF journal voucher**), daftar jurnal otomatis per dokumen, partial `_JournalPreview` di detail dokumen W4–W8.
- Laporan: General Ledger, Trial Balance, Income Statement, Balance Sheet, Cash Flow, Profitability — Excel (builder berstruktur) & PDF.
- Tutup periode & tahun (checklist), rekap pajak PPN/PPh (Excel/PDF + CSV yang sudah ada), monitoring event gagal + retry.
- `GetDashboardSummaryQuery` + dashboard KPI & grafik.

### Fase W10 — Pengerasan ✅ (selesai 2026-10-04; rencana §23, realisasi §24)
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

---

## 13. Realisasi Fase W2 — Ekspor & Master Data (2026-10-03)

Tidak ada perubahan Domain/Application/Web.Api dan **tidak ada migration baru** — seluruh W2 memakai use case yang sudah ada (Fase 1 & 9).

### 13.1 Fondasi ekspor (§5) — `Web.App/Infrastructure/Export`
| Komponen | Realisasi |
|---|---|
| Paket | `ClosedXML` 0.105.1, `QuestPDF` 2026.9.1; `QuestPDF.Settings.License = Community` di `AddWebApp`. |
| `ExportColumn<T>(Header, Value, Format, Width)` | Format `Text/WholeNumber/Number/Money/Percent/Date/DateTime/Boolean`; `Width` relatif (lebar kolom Excel & PDF). |
| `PagedExportRunner` | Mengambil data lewat query list yang sama (100 baris/halaman) dengan filter halaman; menolak bila total > `Export:MaxRows` (default 50.000, error `Export.TooManyRows`). |
| `ExcelExporter` | Judul, filter, info cetak (perusahaan, cabang, dicetak oleh/pada), header tebal + freeze pane + autofilter, nilai bertipe (angka/tanggal asli, bukan teks). |
| `PdfListExporter` + `PdfLayout` | A4 landscape, kop (perusahaan, judul, filter, cabang), header tabel berulang, footer "Printed by … · Page x of y"; angka/tanggal format id-ID. |
| `ExportService` | `HeaderAsync`, `List` (→ `FileContentResult`), `Document(header, compose)` untuk dokumen tunggal; nama file `{name}_{cabang}_{yyyyMMdd-HHmm}.xlsx/pdf`; setiap ekspor di-log. |
| `PageSupport` (scoped) | Helper controller: `CanAsync`, `BranchFilterAsync` (`branch=all` = semua cabang yang boleh diakses; kosong = cabang aktif header), `BranchOptionsAsync`, `AttachmentsAsync`, `ExportAsync`. |
| UI | Partial `_ExportButtons` (Excel/PDF membawa query string halaman → isi file = list terfilter; hanya tampil dengan hak Export), `_ListHeader`, `_FormButtons`. Action `Export` & `Print` ber-`[MenuAccess(…, Export)]`. |

### 13.2 Komponen bersama
- **Lampiran**: `AttachmentsController` (`POST /Attachments/Upload` AJAX + header `X-CSRF-TOKEN`, `GET /Attachments/File/{id}?download=`), partial `_Attachments` + `attachments.js` (drop zone, pratinjau, hapus; id dikirim sebagai hidden `Documents`). Validasi tipe/ukuran tetap di `IAttachmentService` (JPEG/PNG/WEBP/PDF ≤ 10 MB, maks 20).
- **Lookup Tom-Select**: `LookupController` (`/Lookup/Items?category=`, `/Lookup/Farmers?branchId=&type=`; maks 20, hanya yang aktif) + `lookup.js` (`select[data-lookup]`).
- **Baris dinamis**: `collection.js` (`table[data-collection]` + `<template>`; indeks dinomori ulang saat submit) — dipakai konversi satuan, tarif pajak, harga kontrak, harga jaminan, bonus/potongan.
- Partial `_TaxIdentityFields` (NPWP/NITKU/PKP) & `_BankAccountFields`; view model dasar `MasterFormViewModel` (Id, IsActive, Documents, `CanSave`).

### 13.3 Layar
| Area | Halaman |
|---|---|
| `MasterData` | UoM, Tax Codes (tarif ber-tanggal efektif + rasio DPP; field PPN/PPh mengikuti jenis), Items (filter kategori, konversi satuan), Warehouses (filter cabang & jenis; gudang kandang read-only info coop), Vendors (toleransi harga, NPWP, rekening, lampiran), Customers (credit limit, NPWP, lampiran). |
| `Partnership` | Farmers (filter cabang & jenis; jenis/cabang hanya saat create; NIK, NPWP, rekening, lampiran; tautan ke coop), Coops (peternak via Tom-Select; kapasitas, tipe kandang, koordinat; info siklus berjalan; gudang `GK-{kode}` otomatis via outbox), Contracts (list + filter status; **Details** dengan Activate/Deactivate (`[WorkflowAction]` + konfirmasi), Edit hanya Draft, kelola lampiran di status apa pun (`SetDocumentsCommand`), **Print PDF** kontrak (`ContractPdf`: syarat, harga sapronak, harga jaminan, bonus/potongan, catatan, tanda tangan Inti/Plasma)). |

Pola semua master: Index (View) → Export (Export) → Create (Create) → Edit GET (View; **read-only** bila tanpa hak Edit) → Edit POST (Edit).

### 13.4 Penyesuaian dari rencana
- **Hak master & partnership = Create/Edit/Export (tanpa Delete)** — tidak ada use case hapus; penonaktifan lewat flag Active (tabel §11.7 menulis `C E D X`).
- Form kontrak = **satu form bersection** (1. Terms … 5. Attachments), bukan wizard multi-langkah; field bagi hasil hanya tampil untuk skema ProfitSharing.
- ⚠️ **Culture request diubah ke en-US** (sebelumnya id-ID) agar model binding cocok dengan `input type=number/date` HTML5 (titik desimal). Format tampilan id-ID (W-16) tetap lewat `DisplayFormatter`; `app.js` DecimalOnly memakai titik.
- Daftar Items tidak menampilkan konversi (query list memang tidak memuatnya; tampil di form).
- `step` input angka mengikuti skala kolom DB (tarif pajak & bagi hasil `0.0001`), jika tidak nilai tersimpan seperti `60.0000` ditolak validasi browser saat edit.

### 13.5 Perbaikan yang ditemukan saat verifikasi
- Tag helper `asp-menu` belum menargetkan `<ul>` → tombol ekspor tampil untuk user tanpa hak Export (ditangkap integration test; aksi Export sendiri sudah 403).
- `DisplayFormatter.Number(int/long)` ditambahkan (sebelumnya bilangan bulat tampil "1,00").
- Test lama "master.uoms tersembunyi" diganti ke menu yang masih belum rilis (`procurement.purchase-orders`).

### 13.6 Pengujian & verifikasi
- **Test: 282 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 47 (+32): ekspor unit (Excel bertipe, PDF, format id-ID, pengumpul halaman & batas baris, parsing format), 18 halaman W2 terbuka, ekspor xlsx/pdf (content type, nama file, signature), tanpa hak Export → tombol hilang & 403, create UoM, upload + buka lampiran, lookup JSON.
- **End-to-end W2 40/40** (Playwright, Web.App :5098 + Web.Api :5099, DB `intiplasma_verify` baru lalu di-drop): sidebar W2; cabang; UoM (+ duplikat ditolak); kode PPh 23 + tarif, edit ulang; item + konversi SAK=50 KG; gudang pusat; vendor + lampiran PDF (tersimpan, tampil saat edit); customer + credit limit; peternak plasma tanpa NIK ditolak lalu dibuat; coop via Tom-Select (koordinat desimal benar) + gudang `GK-…` dibuat outbox; kontrak bagi hasil (toggle field, harga sapronak via lookup, harga jaminan, bonus FCR) → Draft → edit → Activate (Edit hilang) → **PDF kontrak** (~48 KB) → lampiran pada kontrak aktif; ekspor Items/Vendors/Farmers/Contracts; tanpa error JS.
- **Regresi**: E2E W0 49/49, header 15/15. (E2E W1 memerlukan data gaya lama hasil migrasi; cakupannya dijaga integration test W1.)

---

## 14. Realisasi Fase W3 — Finance Setup (2026-10-03)

Tidak ada perubahan Domain/Application/Web.Api dan **tidak ada migration baru** — layar memakai use case Fase 2/8 yang sudah ada. COA awal & mapping jurnal default sudah di-seed (`FinanceSeeder`).

### 14.1 Layar (`Areas/Finance`)
| Halaman | Realisasi |
|---|---|
| **Chart of Accounts** | Tree (indentasi per level, header bertanda folder, badge **Contra** bila saldo normal ≠ default tipe), filter tipe/status/cari, tombol "+" pada header → akun anak (tipe & induk terisi). Create: kode, nama, tipe, induk (hanya header bertipe sama — difilter JS), header vs detail, saldo normal (kosong = default tipe; isi hanya untuk akun kontra), kategori arus kas. Edit: hanya nama, kategori arus kas, aktif (kode/tipe/induk/jenis/saldo normal read-only). Ekspor COA Excel/PDF (nama berindentasi + level). |
| **Cost Centers** | List, create, edit (nama, aktif), ekspor. |
| **Fiscal Periods** | Per tahun (pilih tahun), **Open Year** (12 periode, hak Create, konfirmasi), status + waktu tutup. Halaman **checklist** (blocking vs warning, deskripsi English per kode cek, Refresh), **Close Period** (dinonaktifkan bila ada blocking; peringatan jurnal penutup tahun untuk Desember) & **Reopen** — keduanya `[WorkflowAction]` + konfirmasi. Di list, Close… hanya untuk periode terbuka pertama dan Reopen hanya untuk periode tertutup terakhir. Ekspor. |
| **Journal Templates** | List (baris Dr/Cr), form dengan baris dinamis (akun via lookup, sisi, cost center, deskripsi), ekspor per baris. |
| **Auto Journal Mappings** | Satu kartu per event katalog `AccountingEvents` (nama & komponen dalam English), default debit/kredit per komponen, status "Not configured", daftar override cabang. Form: baris tetap per komponen (debit/kredit via lookup, cost center); komponen kosong = tidak dipetakan, setengah terisi = error; **Add branch override** memulai dari akun default; ganti event memuat ulang komponen. Ekspor per komponen. |
| **Cash/Bank Accounts** | Filter cabang/tipe/cari, saldo buku, create (kode, nama, tipe, cabang, akun COA aset postable via lookup, bank & no. rekening), edit (nama, bank, no. rekening, aktif; cabang/akun/saldo read-only), ekspor. |

- Lookup baru `/Lookup/Accounts?q=&type=` (akun postable & aktif).
- Menu: keenam halaman dirilis dengan hak **Create/Edit/Export** (tanpa Delete — tidak ada use case hapus; Fiscal Periods: Create = buka tahun, Edit = tutup/buka kembali).

### 14.2 Perbaikan lintas fase
- ⚠️ **Bug W2**: `MasterFormViewModel.IsActive` ber-default `true`, sehingga switch Active yang dimatikan (tidak terkirim) tidak pernah menonaktifkan master. Default dihapus (Edit GET selalu mengisi nilainya); dijaga integration test.
- Form Edit akun: field read-only yang `[Required]` (Type) kini dikirim sebagai hidden — sebelumnya simpan gagal tanpa pesan (error properti tidak tampil di summary `ModelOnly`).
- **Lookup Tom-Select**: dropdown menutup & melepas fokus setelah memilih (sebelumnya preload saat fokus membukanya lagi dan menutupi tombol Save); wrapper tidak lagi menggambar kotak/panah ganda; placeholder disembunyikan bila sudah ada nilai (`theme.css`).

### 14.3 Pengujian & verifikasi
- **Test: 300 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 65 (+18): 12 halaman finance terbuka, sidebar finance (menu belum rilis tersembunyi), COA seed + ekspor, cost center dibuat lalu dinonaktifkan, **master dinonaktifkan saat switch mati (regresi bug W2)**, buka tahun fiskal + checklist, lookup akun hanya akun postable.
- **End-to-end W3 48/48** (Playwright, DB `intiplasma_verify` baru lalu di-drop): sidebar; tree COA; filter induk per tipe; akun header & anak via "+", akun kontra, kode duplikat ditolak, edit (nama/arus kas/nonaktif) + filter status; cost center create & nonaktif; buka tahun 2026, checklist English, tutup Januari → tombol di list (Close… Februari, Reopen Januari) → buka kembali; template (hanya debit ditolak, 3 baris, label akun & cost center saat edit, hapus baris); mapping (semua event ter-seed, edit default, override cabang dari default, komponen setengah terisi ditolak, komponen kosong tidak dipetakan, override ganda ditolak, ganti event memuat komponen); kas/bank (akun buku sendiri, saldo Rp 0,00, akun buku yang sama ditolak, edit + nonaktif); 7 ekspor; tanpa error JS.
- **Regresi**: E2E W2 40/40, W0 49/49, header 15/15.

---

## 15. Realisasi Fase W4 — Pengadaan & Gudang (2026-10-03)

Tidak ada perubahan Domain/Application/Web.Api dan **tidak ada migration baru** — layar memakai use case Fase 3.

### 15.1 Layar
| Halaman | Realisasi |
|---|---|
| **Purchase Orders** (`Areas/Procurement`) | List (filter cabang/status/cari), form (cabang, vendor via lookup, tanggal, baris dinamis: item sapronak via lookup → **satuan otomatis** (dasar + konversi), qty, harga, kode PPN; jumlah & subtotal dihitung di browser), detail (progres penerimaan per baris, subtotal + **PPN estimasi** dari kode PPN pada tanggal order, total), aksi **Approve / Close / Cancel (modal alasan)** via `[WorkflowAction]`, **Receive Goods** (ke form BPB), kelola lampiran (selain Cancelled), **PDF PO**. Edit hanya Draft. |
| **Goods Receipts** | List (cabang, rentang tanggal, cari), form: pilih PO (lookup PO Approved/Partially received, atau dari tombol di PO) → baris outstanding terisi otomatis (kosong/0 = tidak diterima, `max` = outstanding), gudang penerima (gudang cabang PO; gudang kandang membebani siklus), no. surat jalan vendor, lampiran. Detail + **PDF BPB**. |
| **Stock Transfers** | Dari gudang pusat → gudang lain di cabang yang sama (tujuan difilter di browser, bukan gudang asal); item via lookup **stok gudang asal** (label "on hand"), satuan otomatis. Ke kandang dibebankan ke siklus berjalan. Detail + PDF. |
| **Stock Returns** | Gudang kandang → gudang pusat cabang yang sama, alasan wajib. Detail + PDF. |
| **Feed Mutations** | Halaman = form (kandang → via gudang pusat → kandang lain), alasan wajib; hasilnya retur + transfer (tampil di Stock Returns & Stock Transfers). |
| **Stock Balance & Card** | Saldo per gudang/item (cabang, gudang, kategori, cari, tampilkan saldo nol), tautan ke **kartu stok** (gudang + item + periode: saldo awal, mutasi dengan saldo berjalan, saldo akhir); ekspor saldo & kartu stok Excel/PDF. |

- Hak menu: Purchase Orders **Create/Edit/Export** (Approve, Close, Cancel = Edit — tidak ada CanApprove, approval terpusat menyusul); dokumen gudang **Create/Export** (dokumen ter-posting tidak bisa diubah; lampiran hanya saat dibuat); Stock = View/Export.
- PDF dokumen gudang memakai satu komponen `InventoryDocumentPdf` (BPB, transfer, retur). Terbilang tidak dipakai (W-17 hanya untuk invoice/receipt/PV/settlement).

### 15.2 Komponen baru
- Lookup: `/Lookup/Vendors`, `/Lookup/PurchasableItems` (DOC/pakan/OVK aktif), `/Lookup/ItemUnits?itemId=` (satuan dasar + konversi + kode pajak default), `/Lookup/StockItems?warehouseId=` (item bersaldo + qty on hand), `/Lookup/ReceivablePurchaseOrders`.
- `lookup.js`: `data-lookup-depends="warehouseId:#FromWarehouseId"` (nilai field lain ikut di query; ganti sumber → pilihan dikosongkan).
- `item-units.js`: `select.line-item` → isi `select.line-uom` (dan `select.line-tax` default) di baris yang sama.
- `ItemOptions` & `InventoryOptions` (scoped): label item & satuan untuk form yang dirender ulang, daftar gudang per cabang.
- ⚠️ **`validation-setup.js`** (dimuat `_ValidationScriptsPartial`): aturan `step` jquery-validation diganti — sebelumnya nilai dengan desimal lebih banyak dari step (mis. `20.000000` dari kolom numeric(18,6)) ditolak **tanpa pesan**, sehingga form edit PO tidak bisa disimpan. Berlaku untuk semua form.

### 15.3 Pengujian & verifikasi
- **Test: 313 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 78 (+13): 11 halaman W4 terbuka; alur PO (dibuat lewat halaman master) → approve → BPB sebagian → saldo stok → lookup stok → transfer → kartu stok → close PO → PDF PO & BPB → ekspor kartu stok; cancel PO (tanpa alasan ditolak).
- **End-to-end W4 51/51** (Playwright; siklus kandang direncanakan lewat Web.Api karena layar produksi baru W5): setup master + buka tahun fiskal; PO (satuan mengikuti item, subtotal berjalan, edit draft, approve, Edit hilang, PDF); BPB dari tombol PO (outstanding terisi, gudang cabang PO, PDF), BPB kedua via lookup PO (over-receipt diblok), close PO; transfer ke kandang (tujuan difilter, lookup "on hand", dibebankan ke siklus, PDF), transfer melebihi stok ditolak; retur (tujuan gudang pusat, alasan wajib); mutasi pakan (target kandang lain); saldo & kartu stok + ekspor; cancel PO via modal; 5 ekspor list; **jurnal otomatis BPB/transfer/retur terposting tanpa dead letter**; tanpa error JS.
- **Regresi**: E2E W3 48/48, W2 40/40, W0 49/49, header 15/15.

### 15.4 Catatan
- ⚠️ BPB/transfer/retur tetap bisa diposting walau **tahun fiskal belum dibuka**; jurnal otomatisnya lalu gagal (dead letter `FiscalPeriods.NotFoundForDate`) dan memblokir tutup periode. Buka tahun fiskal (W3) sebelum transaksi gudang. Layar dead letter/retry menyusul di W9.

---

## 16. Realisasi Fase W5 — Produksi (2026-10-03)

Tidak ada perubahan Domain/Application/Web.Api dan **tidak ada migration baru** — layar memakai use case Fase 4 & 7.

### 16.1 Layar (`Areas/Production`)
| Halaman | Realisasi |
|---|---|
| **Cycles & Chick-in** | List (cabang/status/cari) + ekspor. **Plan**: coop via lookup (hanya coop aktif tanpa siklus terbuka), kontrak via lookup yang bergantung pada coop (kontrak aktif cabang coop; kosong untuk coop inti), tanggal & populasi rencana. **Halaman siklus** = alur bertahap (pengganti wizard): indikator langkah *Planned → DOC in coop → Chick-in → Harvesting → Closed* dengan aksi per langkah (Transfer DOC, Chick-in, Daily Recording, Harvest, Close, Cancel + alasan), kartu KPI (populasi, deplesi, umur, BW, pakan, FCR, IP, panen) dan tab **Overview** (+ syarat kontrak yang dibekukan), **Recordings**, **Harvests**, **Coop Stock** (+ tautan kartu stok), **Performance** (2 grafik Chart.js + tabel harian kumulatif), **Cost** (sapronak terpakai, biaya berjalan/final, per kg/ekor, HPP diakui, penyesuaian, pendapatan plasma), **Attachments**. **Chick-in**: DOC yang ada di gudang kandang terisi otomatis. **Summary PDF** (interim / *Cycle Closing Summary*). |
| **Daily Recordings** | Pilih siklus (lookup siklus Active/Harvesting) → daftar recording + ekspor; form input (tanggal default = hari setelah recording terakhir, mati, afkir, BW gram, catatan, pemakaian pakan/OVK dari stok gudang kandang dengan satuan otomatis, lampiran); detail + **revisi** (alasan wajib, nilai lama di **timeline History**). |
| **Harvests** | Pilih siklus → form per truk (tanggal, ekor, kg, rata-rata dihitung langsung, truk/catatan, lampiran tiket timbangan) + daftar panen dengan kelola lampiran per panen; ekspor. |

- Hak menu: Cycles Create/Edit/Export (Create = plan; Edit = chick-in, cancel, close, lampiran), Recordings Create/Edit/Export (Edit = revisi), Harvests Create/Edit/Export (Edit = lampiran panen).
- Lookup baru: `/Lookup/PlannableCoops`, `/Lookup/CoopContracts?coopId=`, `/Lookup/RecordableCycles`. `InventoryOptions.CoopWarehouseIdAsync`.
- Form BPB: keterangan diperjelas — hanya DOC yang boleh diterima langsung ke gudang kandang (aturan domain sejak Fase 3).

### 16.2 Pengujian & verifikasi
- **Test: 318 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 83 (+5): halaman produksi terbuka; alur plan → coop hilang dari lookup → DOC (BPB langsung ke gudang kandang) + pakan (gudang pusat → transfer) → chick-in → recording + revisi (history, total mati terkoreksi) → panen (Harvesting) → close ditolak saat populasi tersisa → grafik performa & biaya berjalan → PDF ringkasan → ekspor; plan kedua → cancel dengan alasan.
- **End-to-end W5 42/42**: setup + buka tahun fiskal; coop plasma tanpa kontrak ditolak, coop inti tanpa tawaran kontrak; plan (langkah 1, coop tak bisa direncanakan lagi); PO, BPB DOC ke kandang, pakan ke kandang ditolak → gudang pusat → transfer; langkah 2; chick-in terisi otomatis → Active 1.000 ekor; 3 recording harian (tanggal default berurutan, stok pakan berkurang), tanggal ganda ditolak; revisi (tanpa alasan ditolak, history, total & stok terkoreksi); 2 truk panen (rata-rata langsung, melebihi populasi ditolak); close ditolak (sisa pakan); **penjualan lewat Web.Api** (SO → 2 DO per panen → invoice diposting, layar W6 belum ada) + retur sisa pakan → **close berhasil**, biaya final, aksi hilang, 2 grafik ter-render, PDF ringkasan penutupan; cancel siklus terencana; 3 ekspor; outbox tanpa dead letter; tanpa error JS.
- **Regresi**: E2E W4 51/51, W3 48/48, W2 40/40, W0 49/49, header 15/15.

---

## 17. Realisasi Fase W6 — Penjualan & AR (2026-10-03)

Tidak ada migration baru. Satu tambahan Application: **`GetCustomerCreditQuery`** (limit, exposure, sisa kredit customer — memakai perhitungan yang sama dengan approval SO) untuk tampilan exposure.

### 17.1 Layar (`Areas/Sales`)
| Halaman | Realisasi |
|---|---|
| **Sales Orders** | List (cabang/status/tanggal/cari) + ekspor; form (cabang, customer & item ayam hidup via lookup, ekor, estimasi kg, harga/kg, kode PPN, total estimasi langsung); detail dengan **panel kredit customer** (limit, exposure order/invoice lain, tersedia, order ini). Approve dalam limit; bila ditolak (`CreditLimitExceeded`) pesan exposure tampil dan **Approve over limit** dengan alasan wajib (tercatat di order, badge "Over limit" di list). Close (sebagian terkirim), Cancel (modal alasan), New Delivery, lampiran. |
| **Delivery Orders** | Pilih SO (lookup SO Approved/Partially delivered) → panen yang belum dikirim di cabang SO (centang + baris SO), kendaraan, sopir; detail + **PDF delivery note (surat jalan)**, Create Invoice, Cancel (sebelum ditagih). |
| **Sales Invoices** | Pilih customer → DO terkirim yang belum ditagih (dicentang) → draft; detail (baris dengan PPN, subtotal/PPN/total/dibayar/kredit/outstanding), **Post** (nomor, piutang & HPP estimasi dijurnal), Cancel draft, **PDF invoice dengan terbilang**, Credit Note, Receive Payment. |
| **Credit Notes** | Dari invoice terposting: pengurangan per baris (PPN ikut dikoreksi), langsung terposting; list + **PDF dengan terbilang**. |
| **Customer Receipts** | Pilih customer → invoice terbuka (aging per hari ini) dengan alokasi per invoice (tombol "Full"), akun kas/bank, referensi, **uang muka**, total langsung; detail (alokasi, uang muka diterapkan/sisa), **Apply advance** ke invoice terbuka, **Void** (modal tanggal + alasan; disembunyikan bila uang muka sudah diterapkan — aturan domain), **PDF receipt dengan terbilang**. |
| **Receivable Ledger & Aging** | Tab **Aging** (per tanggal, cabang, customer opsional; bucket current/1–30/31–60/61–90/>90 per customer & invoice) dan **Customer ledger** (saldo awal, invoice/receipt/void/uang muka/nota kredit dengan saldo berjalan, saldo akhir); ekspor Excel/PDF keduanya. |

- Hak menu: SO/DO/Invoice/Receipt **Create/Edit/Export** (Edit = approve, approve over limit, close, cancel, post, apply advance, void), Credit Notes **Create/Export**, Receivables **Export**.
- ⚠️ **Credit override**: di API memakai permission khusus `SalesCreditOverride`; di WebApp (tidak ada CanApprove) cukup hak **Edit** Sales Orders + alasan wajib. Bila perlu dibatasi lebih ketat, tunggu approval terpusat.
- **Terbilang** (`Infrastructure/Formatting/Terbilang.cs`): Rupiah dalam bahasa Indonesia sampai triliun + sen (W-17), dipakai invoice, nota kredit, receipt.
- Lookup baru: `/Lookup/Customers`, `/Lookup/DeliverableSalesOrders`; item ayam hidup memakai `/Lookup/Items?category=LiveBird`. Partial `_SalesFilter`, `_ReasonModal`.

### 17.2 Pengujian & verifikasi
- **Test: 346 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 111 (+28): 16 kasus terbilang; 11 halaman penjualan terbuka; alur panen → SO ditolak limit → approve over limit → DO → invoice draft → post (total 4 jt) → nota kredit (outstanding 3,9 jt) → receipt 3 jt + uang muka 0,5 jt (Partially paid) → apply advance (outstanding 0,4 jt) → ledger & aging → 4 PDF + ekspor aging → void ditolak untuk receipt dengan uang muka terpakai → receipt kedua (Paid) → void (outstanding kembali 0,4 jt).
- **End-to-end W6 42/42**: setup + siklus dipanen (100 ekor / 200 kg); SO (total estimasi langsung), panel kredit, approve ditolak → approve over limit (alasan, badge); DO (tanpa centang ditolak, panen tampil, SO Delivered, PDF); invoice (DO terpilih otomatis, draft → post bernomor, PDF); nota kredit (outstanding 3,9 jt, PDF); receipt (alokasi otomatis, total langsung, uang muka, PDF), apply advance, void tersembunyi, receipt kedua "Full" → Paid → void → terbuka lagi; aging & ledger (Credit Note, Receipt Void, saldo akhir 400.000) + ekspor; cancel SO draft; **siklus ditutup setelah panennya terjual lewat UI**; 5 ekspor list; jurnal otomatis tanpa dead letter; tanpa error JS.
- **Regresi**: E2E W5 42/42, W4 51/51, W3 48/48, W2 40/40, W0 49/49, header 15/15.

---

## 18. Realisasi Fase W7 — AP, Kas & Bank (2026-10-03)

Tidak ada perubahan Domain/Application/Web.Api dan **tidak ada migration baru**; layar memakai use case Fase 6–7.

### 18.1 Layar (`Areas/Finance`)
| Halaman | Realisasi |
|---|---|
| **Vendor Invoices** | List (cabang/status/tanggal/cari no. internal atau no. faktur vendor) + ekspor, badge "Variance". Form: pilih vendor (lookup) + cabang → baris BPB yang belum ditagih (dicentang, qty & harga PO terisi, subtotal langsung, harga ≠ PO disorot), no. faktur vendor & faktur pajak, PPh opsional (kode pajak IncomeTax), lampiran → **draft**. Detail: ringkasan (subtotal, nilai barang, selisih harga, PPN, PPh, total, dibayar, outstanding), baris dengan % deviasi vs toleransi vendor. **Post**; bila ditolak (`PriceVarianceAboveTolerance`) muncul form **Post with variance** dengan alasan wajib. Cancel draft (alasan), lampiran, tombol **Pay** ke PV. |
| **Payment Vouchers** | List + ekspor; tombol **Pay Vendor** / **Pay Plasma**. Form: pilih penerima (vendor atau peternak plasma via lookup) → dokumen terbuka (invoice terposting dari aging hari ini, atau settlement Approved/PartiallyPaid) dengan alokasi per dokumen + "Full", akun kas/bank, referensi, total langsung → draft bernomor. Detail: **Approve** (checker, domain menolak pembuat), **Pay** (modal tanggal aktual), Cancel (alasan), lampiran, **PDF PV dengan terbilang**. |
| **Cash In/Out** | List (filter arah kas masuk/keluar) + ekspor; tombol **Cash In** / **Cash Out**. Form: akun kas/bank, tanggal, keterangan, referensi, baris akun COA (lookup) + cost center + keterangan + jumlah (tabel dinamis, total langsung), lampiran → draft. Kas masuk langsung **Post** (BKM); kas keluar **Approve** oleh user lain lalu **Post** (BKK). Cancel (alasan), **PDF voucher kas dengan terbilang**. |
| **Bank Transfers** | List + ekspor, form (dari/ke kas/bank, tanggal, jumlah, referensi, catatan; akun sama ditolak) → langsung terposting. |
| **Cash/Bank Book** | Tombol buku di Cash/Bank Accounts (W3) → saldo awal, mutasi (jurnal, sumber, keterangan, masuk/keluar, saldo berjalan), saldo akhir; ekspor Excel/PDF; tombol Transfer. |
| **Bank Reconciliations** | List (cabang, rekening) + ekspor; mulai (rekening bank, tanggal & saldo rekening koran). Halaman kerja: kartu saldo rekening koran (bisa diubah), saldo buku, belum clear, **selisih**, baris belum cocok; impor CSV (upload file atau tempel), tambah baris manual; **dua kolom**: rekening koran (cocokkan manual lewat dropdown mutasi buku — jumlah sama ✓ dan tanggal terdekat di atas, unmatch, hapus baris) dan mutasi buku yang belum clear; **Auto-match** (±3 hari); **Complete** aktif bila semua cocok & selisih 0; ekspor baris rekening koran Excel/PDF. |
| **Payable Ledger & Aging** | Tab Aging (per tanggal, cabang, vendor opsional; bucket per vendor & invoice) dan Vendor ledger (saldo awal, invoice/pembayaran, saldo berjalan, saldo akhir); ekspor Excel/PDF. |

- Hak menu: VI/PV/Cash **Create/Edit/Export** (Edit = post, post with variance, approve, pay, cancel, lampiran; draft tidak bisa diubah — batal lalu buat ulang, jadi tanpa Delete), Bank Transfers **Create/Export**, Bank Reconciliations **Create/Edit/Export**, Payables **Export**. Buku kas/bank mengikuti hak View/Export Cash/Bank Accounts.
- ⚠️ **Selisih harga VI**: di API memakai permission `payables:approve-variance`; di WebApp cukup hak **Edit** Vendor Invoices + alasan wajib (sama dengan credit override W6), sampai approval terpusat tersedia.
- PV plasma sudah bisa dipakai, tetapi settlement baru punya layar di W8 (sementara dibuat lewat Web.Api).

### 18.2 Komponen & perbaikan
- Komponen bersama baru: partial `Partials/_DocumentFilter` (menggantikan `_SalesFilter`, + select tambahan `FilterSelect`), `Partials/_ReasonModal` (dipindah dari area Sales), `Partials/_AttachmentsCard`; helper `DocumentLists` (filter tanggal & deskripsi ekspor) dan `DocumentPdf` (blok cetak dokumen: field, tabel, total, terbilang, tanda tangan — dipakai `SalesDocumentPdf` & `FinanceDocumentPdf`); `FinanceOptions` (kas/bank, kode PPh).
- ⚠️ **Perbaikan bug W6**: route katalog menu Sales Orders/Delivery Orders/Sales Invoices menunjuk `/Sales/Orders`, `/Sales/Deliveries`, `/Sales/Invoices` (404 dari sidebar) → diperbaiki ke `/Sales/SalesOrders`, `/Sales/DeliveryOrders`, `/Sales/SalesInvoices` (route diperbarui otomatis oleh sinkronisasi katalog saat startup). Test baru memastikan **setiap menu rilis** membuka halaman 200.

### 18.3 Pengujian & verifikasi
- **Test: 363 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 128 (+17): 14 halaman W7 terbuka; semua route menu rilis terbuka; alur BPB → VI draft dibatalkan (alasan wajib) → VI di atas toleransi (post ditolak → post with variance) → PV (pembuat ditolak approve, checker approve) → paid, invoice Paid → ledger & aging → PDF PV + ekspor; kas masuk (BKM), kas keluar (maker-checker, BKK), baris tanpa akun ditolak, transfer (akun sama ditolak), buku kas/bank + ekspor, rekonsiliasi (impor CSV, complete ditolak saat ada baris belum cocok, tambah/hapus baris, complete) + PDF. `WebAppFactory.CreateUserAsync` kini menerima profil Akses Cabang.
- **End-to-end W7 62/62** (Playwright, outbox berjalan di Web.App): semua menu sidebar terbuka (termasuk route Sales yang diperbaiki); setup (bank + kas kecil, peternak plasma, user checker lewat layar Users); PO 150 KG → BPB; VI (baris BPB terisi, subtotal & sorotan harga, post ditolak → variance + alasan, badge); PV dari tombol Pay (alokasi terisi), approve oleh pembuat ditolak, checker approve, pay via modal, invoice Paid, PDF; VI kedua sisa 50 KG tanpa variance; PV draft dibatalkan; form PV plasma; kas masuk BKM, kas keluar 2 baris (maker-checker) BKK + PDF, baris tanpa akun ditolak, cancel, filter arah; transfer; **jurnal otomatis VI/PV/BKM/BKK/transfer terposting**, saldo bank −800.000 di buku kas/bank + Excel; rekonsiliasi: impor CSV 5 baris (`;`, header), auto-match 3, match manual baris bertanggal −10 hari, hapus biaya admin, selisih −6.500 → koreksi saldo → 0 → Complete + PDF; aging & ledger vendor (outstanding 400.000) + ekspor; 5 ekspor list; tanpa dead letter; tanpa error JS.
- **Regresi**: E2E W6 42/42, W5 42/42, W4 51/51, W3 48/48 (ekspektasi "vendor-invoices belum rilis" disesuaikan), W2 40/40, W0 49/49, header 15/15.

### 18.4 Catatan
- Rekonsiliasi hanya menawarkan rekening bertipe **Bank**. Biaya admin bank yang ada di rekening koran tetapi belum dibukukan perlu dicatat lewat Cash Out dulu agar bisa dicocokkan.
- Draft VI/PV/kas tidak bisa diedit (tidak ada use case update) — batalkan lalu buat ulang.

---

## 19. Rencana Detail Fase W8 — HPP & Settlement Plasma

> Disusun 2026-10-03 dari kode Fase 7 (`Application/Costing`, `Domain/Costing/PlasmaSettlements`, `Web.Api/Endpoints/Costing`) dan pola layar W5–W7. Keputusan §19.8 disepakati 2026-10-03; realisasi di §20.

### 19.1 Ruang lingkup & hasil akhir
- Area baru **`Areas/Costing`** dengan dua menu yang sudah ada di katalog (saat ini belum rilis): `costing.cycle-costs` (**Cycle Cost**, hak Export) dan `costing.settlements` (**Plasma Settlements**, hak Create/Edit/Export).
- Alur lengkap lewat UI: siklus plasma ditutup (W5) → **settlement draft** → hitung ulang → **approve oleh checker** (jurnal otomatis, siklus `Settled`) → **Pay** → PV plasma (W7) → settlement Paid. Rugi menjadi piutang plasma dan bisa dipotong di settlement siklus berikutnya.
- **PDF settlement** (slip perhitungan hasil) dengan terbilang (W-17), ekspor list settlement serta list & rincian HPP.

### 19.2 Backend yang dipakai (Fase 7) & celahnya
| Kebutuhan | Sudah ada | Celah untuk WebApp |
|---|---|---|
| HPP per siklus | `GetCycleCostQuery` (input terpakai per item, DOC/pakan/OVK, total, HPP/kg & /ekor, HPP diakui di invoice, penyesuaian tutup siklus, pendapatan plasma) — sudah dipakai tab **Cost** detail siklus (W5) | Belum ada **query list lintas siklus** untuk halaman Cycle Cost & ekspornya |
| List settlement | `GetPlasmaSettlementsQuery(branch, farmer, cycle, status)` → `IReadOnlyList`, tanpa paging | Tanpa cari & filter tanggal |
| Detail settlement | `GetPlasmaSettlementByIdQuery` (+ baris perhitungan) | Kandang, tanggal chick-in/tutup, kinerja penutupan (FCR, IP, deplesi, BW), nama pembuat/penyetuju belum ikut — dibutuhkan slip & PDF |
| Buat / hitung ulang / approve / batal | `Create…`, `Recalculate…` (hanya Draft), `Approve…` (pembuat ditolak, periode fiskal dicek), `Cancel…` (hanya Draft, alasan) | — |
| Lampiran | `ApplyDocumentsAsync` saat create/recalculate; `SetDocumentsCommand` (`AttachmentOwnerTypes.PlasmaSettlement`) | — |
| Pembayaran | PV plasma (W7): `/Finance/PaymentVouchers/Create?payeeType=Farmer&payeeId=&documentId=` | — |
| Potongan hutang | Validasi domain: 0 ≤ potongan ≤ pendapatan − PPh | **Tidak ada saldo piutang plasma per peternak** (RANGKUMAN §8) — admin tidak tahu berapa yang perlu dipotong |
| Siklus yang bisa di-settle | — | Lookup siklus plasma `Closed` tanpa settlement aktif |

### 19.3 Application (tambahan; satu file per use case, boleh dipakai Web.Api)
1. **`GetCycleCostsQuery`** (paged, Dapper) — filter cabang, status siklus, periode chick-in, cari (siklus/kandang/peternak). Kolom: siklus, kandang, peternak, skema (Inti/PriceContract/ProfitSharing), status, populasi awal, panen (ekor, kg), biaya DOC/pakan/OVK/total (final dari `closing_cost` jsonb untuk siklus tertutup; berjalan = Σ −nilai `stock_ledger_entries` tipe ChickIn/Usage/UsageReversal), HPP/kg final, HPP diakui (Σ `cost_amount` baris invoice terposting), penyesuaian, pendapatan plasma (settlement disetujui), penanda *Final/Running*. Logika harus identik dengan `CycleCosting` → diuji silang terhadap `GetCycleCostQuery`.
2. **`GetPlasmaSettlementsQuery`** diperluas: `Search` (no. settlement/siklus/peternak), `From`/`To` tanggal settlement (opsional; `null` = perilaku sekarang). Endpoint API & `PaymentVouchersController` menyesuaikan konstruktor. Ekspor memakai overload `PageSupport.ExportAsync` untuk list tanpa paging.
3. **`GetPlasmaSettlementByIdQuery`** diperluas: kandang, tanggal chick-in & tutup, ringkasan `ClosingPerformance` (populasi, ekor & kg panen, BW rata-rata, FCR, deplesi %, IP, umur), nama pembuat & penyetuju.
4. **`GetSettleableCyclesQuery`** (lookup) — siklus plasma `Closed`, cabang dalam akses, belum punya settlement non-Cancelled.
5. **`GetFarmerPlasmaDebtQuery`** — saldo piutang plasma peternak **dari data settlement**: Σ `deficit` − Σ `debt_deduction` settlement yang disetujui. Hanya informasi di form, bukan sub-ledger penuh.
- Unit test Application: lookup tidak menawarkan siklus inti/belum tutup/sudah di-settle; saldo hutang plasma; list HPP = `GetCycleCostQuery` untuk siklus berjalan & tertutup.

### 19.4 Web.App — layar (`Areas/Costing`)
| Halaman | Rencana |
|---|---|
| **Cycle Cost** (`/Costing/CycleCosts`) | List `GetCycleCostsQuery` (filter cabang/status/periode/cari, badge Final/Running) + ekspor Excel/PDF. **Detail** per siklus (`GetCycleCostQuery`): kartu ringkasan (DOC, pakan, OVK, total, HPP/kg & /ekor, diakui di invoice, penyesuaian, pendapatan plasma, total biaya termasuk plasma), tabel input terpakai per item, tautan ke siklus (W5), kartu stok gudang kandang (W4) & settlement; ekspor rincian Excel/PDF. |
| **Plasma Settlements** (`/Costing/Settlements`) | List (cabang/status/tanggal/cari) + ekspor: no., tanggal, siklus, peternak, skema, pendapatan kotor, PPh, potongan, dibayarkan, rugi, dibayar, outstanding, status. |
| **New Settlement** | Pilih siklus via lookup `SettleableCycles` (atau dari tombol di detail siklus), tanggal (default hari ini, ≥ tanggal tutup), potongan hutang (+ info saldo piutang plasma bila #2 disetujui), catatan, lampiran → **draft** bernomor `STL/…`. Error domain tampil apa adanya (`NoLiveBirdPrice`, `NoInputPrice`, `InvalidDebtDeduction`, `BeforeClosing`). |
| **Detail / slip** | Header (siklus, kandang, peternak, kontrak & skema, tanggal tutup), **kinerja penutupan** (FCR, IP, deplesi, BW), **rincian komponen** dikelompokkan per tipe baris (Live bird value per truk, Sapronak charge per item, Profit share, Bonus, Penalty) dengan qty × harga = jumlah, lalu pendapatan kotor → PPh (kode & tarif) → potongan hutang → **dibayarkan**, atau **rugi → piutang plasma**; dibayar & outstanding. Aksi `[WorkflowAction]`: **Recalculate** (Draft; tanggal/potongan/catatan, menghitung ulang dari data siklus terkini), **Approve** (Draft; checker), **Cancel** (Draft; modal alasan), **Pay** → PV plasma (Approved/PartiallyPaid), lampiran (selain Cancelled), **Print PDF**. |
| **PDF settlement** | `CostingDocumentPdf` memakai `DocumentPdf` (W7): kop, data siklus & kinerja, tabel komponen, ringkasan, **terbilang** jumlah dibayarkan (atau rugi), tanda tangan (Prepared by / Approved by / Plasma). |
| **Integrasi layar lain** | Detail siklus (W5): tombol **Create Settlement** (siklus plasma Closed tanpa settlement, hak Create settlement) atau tautan ke settlement; tab Cost → "Open cycle cost". Detail PV plasma: tautan balik ke settlement. |

- Hak menu: Cycle Cost **Export**; Plasma Settlements **Create/Edit/Export** (Create = buat draft; Edit = recalculate, approve, cancel, lampiran; tanpa Delete — batal lalu buat ulang).
- Lookup baru: `/Lookup/SettleableCycles` (saldo hutang plasma dirender server di form — tanpa lookup terpisah).
- `MenuCatalog`: kedua menu costing dirilis.

### 19.5 Data uji & verifikasi
- **PriceContract**: kontrak dengan harga jaminan per rentang BW + harga sapronak (DOC, pakan, OVK) + bonus FCR + PPh 23 → siklus plasma → panen 2 truk BW berbeda → jual (W6) → tutup → settlement (2 baris ayam, sapronak per item, bonus, PPh) → approve oleh checker → jurnal `PlasmaSettlement` terposting → PV plasma sebagian lalu lunas (PartiallyPaid → Paid).
- **Rugi**: harga sapronak kontrak tinggi → gross negatif → `Deficit` (net 0, langsung Paid saat approve) → siklus berikutnya peternak yang sama: settlement dengan **potongan hutang** (melebihi batas ditolak).
- **ProfitSharing**: % × (penjualan − nota kredit − biaya final); laba negatif → bagian 0.
- Penolakan: siklus inti / belum tutup tidak muncul di lookup; tanggal < tanggal tutup; kontrak tanpa harga untuk BW/sapronak; approve oleh pembuat; approve di periode tertutup/tahun fiskal belum dibuka; recalculate/cancel tidak tersedia setelah approve.
- **Integration test Web.App** (±15): halaman terbuka, menu rilis 200, draft → recalculate → approve (checker) → PV, cancel dengan alasan, PDF `%PDF`, ekspor Cycle Cost & settlement, tombol aksi hilang tanpa hak Edit/Export.
- **E2E W8** (Playwright, DB `intiplasma_verify` baru lalu di-drop, outbox di Web.App) + regresi E2E W0–W7.

### 19.6 Urutan task
1. Application: perluas `GetPlasmaSettlementsQuery` & `GetPlasmaSettlementByIdQuery`; tambah `GetCycleCostsQuery`, `GetSettleableCyclesQuery` (+ `GetFarmerPlasmaDebtQuery`); sesuaikan endpoint API & `PaymentVouchersController`; unit test.
2. Web.App: `Areas/Costing` (model, `CycleCostsController`, `SettlementsController`), lookup, view list/detail/form/recalculate (memakai `_DocumentFilter`, `_ReasonModal`, `_AttachmentsCard`).
3. PDF settlement + ekspor Cycle Cost (list & rincian) + ekspor settlement.
4. Integrasi tombol di detail siklus & detail PV; rilis menu.
5. Integration test, E2E W8 + regresi, realisasi §20, update RANGKUMAN.
- **Tidak ada migration** yang diperkirakan (semua kolom sudah ada sejak `Phase7_CostingSettlement`).

### 19.7 Risiko & catatan teknis
- Siklus yang ditutup sebelum Fase 7 tidak punya `ClosingCost` → list menampilkan biaya kartu stok bertanda "Running" walau siklus tertutup (RANGKUMAN §8).
- Recalculate membaca ulang data siklus (panen, nota kredit, input terpakai): hasil bisa berubah bila ada nota kredit baru setelah draft dibuat — tampilkan waktu hitung terakhir di detail.
- Approve sudah menolak periode tertutup/tidak ada sebelum jurnal masuk outbox → pesan tampil di atas halaman, bukan dead letter.
- Settlement yang sudah Approved tidak bisa dibatalkan (tidak ada use case reversal) — keterbatasan; koreksi lewat jurnal manual (W9).

### 19.8 Keputusan (disepakati 2026-10-03)
| # | Topik | Keputusan |
|---|-------|--------|
| 1 | Halaman Cycle Cost | **Query list baru `GetCycleCostsQuery`** (lintas siklus + ekspor) dan detail per siklus, bukan sekadar tautan ke tab Cost detail siklus. |
| 2 | Info saldo piutang plasma di form | **Ya**, `GetFarmerPlasmaDebtQuery` berbasis data settlement (rugi − potongan), sebagai informasi; sub-ledger penuh tetap di backlog. |
| 3 | Pratinjau sebelum draft | **Tidak** — draft sudah berfungsi sebagai pratinjau (bisa dihitung ulang/dibatalkan; nomor hanya dipakai bila validasi lolos). |
| 4 | Approve | Hak **Edit** + maker-checker domain (seperti PV/kas W7), sampai approval terpusat tersedia. |
| 5 | List settlement | Tetap **tanpa paging** (±1 per siklus plasma), filter cari/tanggal di SQL; pindah ke `PagedList` bila volume membesar. |

---

## 20. Realisasi Fase W8 — HPP & Settlement Plasma (2026-10-03)

**Tidak ada migration baru** dan tidak ada perubahan Domain; semua kolom sudah ada sejak `Phase7_CostingSettlement`.

### 20.1 Application & Web.Api
| Use case | Realisasi |
|---|---|
| `GetCycleCostsQuery` (baru, paged, Dapper) | HPP lintas siklus: biaya final dari `closing_cost` (jsonb) untuk siklus tertutup, selain itu biaya berjalan dari kartu stok gudang kandang (ChickIn/Usage/UsageReversal) — logika sama dengan `CycleCosting` (HPP/kg berjalan = biaya / (kg panen + sisa ekor × BW recording terakhir)); HPP diakui di invoice, penyesuaian, pendapatan plasma, penanda Final. Filter cabang, status, periode chick-in, cari siklus/kandang/peternak. |
| `GetSettleableCyclesQuery` (baru) | Siklus plasma `Closed` tanpa settlement non-Cancelled, dalam akses cabang (lookup). |
| `GetFarmerPlasmaDebtQuery` (baru) | Saldo hutang plasma dari settlement disetujui: Σ rugi − Σ potongan (minimal 0). |
| `GetPlasmaSettlementsQuery` (diperluas) | Parameter opsional `Search`, `From`, `To` (pemanggil lama tidak berubah). |
| `GetPlasmaSettlementByIdQuery` / `PlasmaSettlementResponse` (diperluas) | Kandang, tanggal chick-in & tutup, status siklus, nama pembuat & penyetuju, waktu hitung terakhir (`CalculatedAtUtc`), `Performance` (kinerja penutupan, detail saja). |
| Web.Api | `GET costing/cycle-costs`, `GET costing/farmers/{farmerId}/plasma-debt`; `GET costing/settlements` menerima `search`, `from`, `to`. |

### 20.2 Layar (`Areas/Costing`)
| Halaman | Realisasi |
|---|---|
| **Cycle Cost** | List (cabang/status/periode chick-in/cari; badge Final/Running; DOC, pakan, OVK, total, HPP/kg, diakui, pendapatan plasma) + ekspor Excel/PDF. Detail: tabel sapronak terpakai per item (kode item → kartu stok gudang kandang mulai tanggal chick-in, biaya per unit), kartu ringkasan (berjalan/final, estimasi bobot hidup, penyesuaian, pendapatan plasma, total termasuk plasma), tombol ke siklus, ke settlement atau **Create Settlement**; ekspor rincian Excel/PDF (ringkasan di header ekspor). |
| **Plasma Settlements** | List (cabang/status/tanggal/cari; pendapatan kotor, dibayarkan, rugi, outstanding) + ekspor. **New Settlement**: pilih siklus lewat lookup (halaman dimuat ulang) atau dari tombol di siklus/HPP; tanggal, potongan hutang dengan info **saldo hutang plasma** + tautan "Use it", catatan, lampiran → draft. **Detail/slip**: data siklus & kontrak, kartu kinerja penutupan, ringkasan (pendapatan kotor → PPh → potongan → dibayarkan / rugi → hutang plasma, dibayar, outstanding), rincian dikelompokkan per tipe dengan subtotal; **Recalculate** (form yang sama), **Approve** (checker), **Cancel** (modal alasan), **Pay** → form PV plasma terisi, lampiran, **PDF**. |
| **PDF settlement** | `CostingDocumentPdf`: kop, data siklus & kontrak, kinerja penutupan, rincian per tipe, total (PPh & potongan, atau rugi), terbilang jumlah dibayarkan/rugi, catatan rugi, tanda tangan Prepared/Approved/Plasma. |
| Integrasi | Detail siklus (W5): tombol **Create Settlement** (siklus plasma Closed tanpa settlement) / tautan settlement, tab Cost → "Open cycle cost". Detail PV: nomor settlement menaut ke slip. |

- Hak menu: Cycle Cost **View/Export**; Plasma Settlements **Create/Edit/Export** (Edit = recalculate, approve, cancel, lampiran). Kedua menu dirilis di `MenuCatalog` (`MenuCodes.CostingCycleCosts`, `MenuCodes.CostingSettlements`).
- Penyesuaian dari rencana: lookup `/Lookup/FarmerPlasmaDebt` tidak dibuat — saldo hutang dirender server saat form dimuat (pemilihan siklus memuat ulang halaman).

### 20.3 Pengujian & verifikasi
- **Test: 370 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 135 (+7): 5 halaman costing terbuka; alur siklus plasma (kontrak harga) → HPP berjalan (Rp 3.500/kg) = rincian → tutup → HPP final + ekspor → lookup & form (saldo hutang) → draft dibatalkan (alasan wajib) → draft baru (3,2 jt) → recalculate (potongan berlebih ditolak, potongan 200 rb) → approve pembuat ditolak, checker approve → siklus Settled & hilang dari lookup → PV plasma sebagian → PartiallyPaid, PV menaut ke slip → PDF & ekspor; user hanya View: tombol aksi/PDF tidak tampil, Print/Approve/Cycle Cost ditolak (403).
- Query baru berbasis Dapper diuji lewat integration test (PostgreSQL Testcontainers), bukan unit test InMemory.
- **End-to-end W8 44/44** (Playwright, Web.App :5098 dengan outbox + Web.Api :5099, DB `intiplasma_verify` baru lalu di-drop): sidebar & semua menu 200; setup lewat UI (kode PPh 21 2%, peternak plasma dengan 2 kandang, kontrak harga: DOC 8.000, pakan 9.000, harga ayam 0,5–1,5 kg 18.000 & 1,5–2,5 kg 20.000, bonus FCR ≤ 2 Rp 100/kg; user checker); 2 siklus (DOC langsung ke kandang, pakan via gudang pusat + transfer, recording, panen); HPP berjalan Rp 1,9 jt / 11.176,47 per kg → jual & tutup → Final + ekspor; tombol Create Settlement di siklus, tanggal sebelum tutup ditolak, draft dibatalkan; **K2 rugi** −340.000 (pembuat tidak bisa approve, checker approve → Paid tanpa pembayaran, siklus Settled); **K1** menampilkan hutang 340.000, pendapatan 1.127.000, PPh 21 22.540, potongan berlebih ditolak, "Use it" → net 764.460, PDF draft, checker approve; jurnal `PlasmaSettlement` kedua settlement terposting oleh outbox; Pay → PV terisi → checker approve → paid → settlement Paid, tautan balik; list & filter status, HPP menampilkan pendapatan plasma; ekspor; tanpa dead letter & error JS. PDF settlement untung & rugi diperiksa visual.
- **Regresi**: E2E W7 62/62, W6 42/42, W5 42/42, W4 51/51, W3 48/48, W2 40/40, W0 49/49, header 15/15.

### 20.4 Catatan
- Skema **ProfitSharing** tidak diuji end-to-end di W8 (perhitungannya dijaga unit test domain Fase 7); layar menampilkannya dengan tipe baris *Profit share*.
- Saldo hutang plasma hanya dari settlement (rugi − potongan); potongan untuk hutang lain atau pelunasan tunai belum tercatat → sub-ledger piutang plasma tetap di backlog.
- Settlement yang sudah Approved tidak bisa dibatalkan/direverse (belum ada use case); koreksi lewat jurnal manual (W9).

---

## 21. Rencana Fase W9 — Jurnal, Laporan, Tutup Buku, Pajak & Dashboard

> Disusun 2026-10-03 saat W9 dimulai, dari use case Fase 2 & 8 (`Application/Finance/Journals|Reports|Tax|FiscalPeriods`, `Application/Monitoring`). Keputusan §21.5 memakai usulan default (pola W6–W8) dan bisa diubah.

### 21.1 Backend yang dipakai & tambahan
| Kebutuhan | Sudah ada | Tambahan W9 |
|---|---|---|
| Jurnal manual | `Create/Update/DeleteJournalCommand` (draft), `Approve` (pembuat ditolak), `Post` (periode terbuka, nomor), `Reverse` (tanggal + alasan), `GetJournalsQuery` (paged, sumber manual/otomatis), `GetJournalByIdQuery` | Nama pembuat/penyetuju/pemosting di respons (untuk PDF & detail) |
| Jurnal per dokumen | — | **`GetDocumentJournalsQuery(SourceIds)`**: jurnal + baris per `source_id` (partial `_JournalPreview`) |
| Laporan | GL, TB, Laba Rugi, Neraca, Arus Kas, Profitabilitas | — |
| Pajak | Rekap PPN (keluaran, retur, masukan) & PPh dipotong per masa; CSV ada di Web.Api | Penulis CSV di Web.App (kolom sama dengan API) |
| Tutup periode & tahun | Sudah di layar Fiscal Periods (W3: checklist, close/reopen, jurnal penutup Desember) | Tautan item checklist "event gagal" → layar Failed Events |
| Event gagal | `GetFailedEventsQuery`, `RetryFailedEventsCommand` | — |
| Dashboard | — | **`GetDashboardSummaryQuery(BranchId)`**: siklus aktif & populasi, panen bulan ini, piutang & hutang (total/jatuh tempo), nilai stok, saldo kas/bank, dokumen menunggu persetujuan, event gagal, tren 6 bulan (penjualan, panen kg) |

### 21.2 Layar
| Menu | Layar |
|---|---|
| **Journals** (`finance.journals`, C/E/D/X) | List (cabang/status/sumber/tanggal/cari) + ekspor; form jurnal manual (baris dinamis: akun via lookup, cost center, keterangan, debit/kredit, total & selisih langsung; isi dari **template**), edit/hapus draft, **Approve** (checker), **Post**, **Reverse** (modal tanggal + alasan), lampiran, tautan ke dokumen sumber (jurnal otomatis), **PDF journal voucher**. Edit = approve/post/reverse; Delete = hapus draft. |
| `_JournalPreview` | Kartu "Journal" di detail BPB, transfer, retur (W4), siklus (W5, penyesuaian HPP), invoice/nota kredit/penerimaan (W6), VI/PV/kas/transfer bank (W7), settlement (W8); tampil bila punya hak View Journals. |
| **Reports** (`reports.*`, View/Export) | General Ledger (akun via lookup, periode, cabang, cost center), Trial Balance, Income Statement, Balance Sheet, Cash Flow, Profitability (grouping), **Tax Recap** (PPN & PPh per masa + CSV). Ekspor Excel **berstruktur** (judul, bagian, subtotal, total; lembar per tabel) & PDF dari satu model `ReportDocument`. |
| **Failed Events** (`admin.failed-events`, Edit) | Daftar event gagal (tipe, waktu, percobaan, error), **Retry** satu/semua. |
| **Dashboard** | Kartu KPI + 2 grafik (Chart.js) sesuai cabang aktif; tautan ke layar terkait sesuai hak. |

### 21.3 Pengujian
- Integration test Web.App: halaman terbuka, jurnal manual draft → edit → approve pembuat ditolak → checker approve → post → reverse, PDF JV; jurnal otomatis tampil di detail dokumen; tiap laporan + ekspor xlsx/pdf (+ CSV pajak); retry event gagal; dashboard.
- E2E W9 (Playwright) + regresi W0–W8.

### 21.4 Urutan task
1. Application: `GetDocumentJournalsQuery`, `GetDashboardSummaryQuery`, nama user di `JournalResponse`.
2. Web.App: `ReportDocument` + renderer Excel/PDF + CSV; Journals; `_JournalPreview` di detail W4–W8; Reports & Tax; Failed Events; Dashboard; tautan checklist.
3. Test, E2E, realisasi §22, RANGKUMAN.

### 21.5 Keputusan (usulan default)
| # | Topik | Keputusan |
|---|-------|-----------|
| 1 | Approve/post/reverse jurnal manual | Hak **Edit** + maker-checker domain (seperti PV/settlement). |
| 2 | Visibilitas `_JournalPreview` | Hanya user dengan hak **View Journals**. |
| 3 | Tutup periode/tahun | Tidak ada layar baru — memakai Fiscal Periods (W3). |
| 4 | Excel laporan | Satu workbook, **satu lembar per tabel** (pajak: keluaran, retur, masukan, PPh). |
| 5 | Dashboard | Satu query ringkas (Dapper), menghormati akses cabang; grafik 6 bulan terakhir. |

---

## 22. Realisasi Fase W9 — Jurnal, Laporan, Tutup Buku, Pajak & Dashboard (2026-10-04)

**Tidak ada migration baru** dan tidak ada perubahan Domain. Semua menu katalog kini dirilis (tidak ada lagi menu "belum rilis").

### 22.1 Application & perbaikan
| Use case | Realisasi |
|---|---|
| `GetDocumentJournalsQuery` (baru) | Jurnal + baris per `source_id` (bisa beberapa id, mis. penerimaan + penerapan uang muka), dalam akses cabang. |
| `GetDashboardSummaryQuery` (baru, `Application/Monitoring`) | Per tanggal & cabang (null = semua cabang yang boleh): siklus berjalan & populasi, panen bulan ini, penjualan bulan ini (DPP), piutang & hutang (jatuh tempo), nilai stok, saldo kas/bank, dokumen menunggu (jurnal approve/post, PV approve/bayar, kas keluar, settlement, siklus plasma tutup belum di-settle), event gagal, tren 6 bulan (penjualan, kg panen). |
| `JournalResponse` (diperluas) | Nama pembuat, penyetuju, pemosting. |
| ⚠️ **Perbaikan Fase 8 — definisi event gagal** | `GetFailedEventsQuery`, `RetryFailedEventsCommand`, checklist `FailedAutoJournals` dan dashboard sebelumnya menghitung `error IS NOT NULL` → event yang **masih dalam retry otomatis** ikut dianggap gagal. Kini dead letter = `processed_on_utc IS NOT NULL AND error IS NOT NULL` (sama dengan health check outbox); event yang masih dicoba ulang tetap terhitung sebagai *PendingAutoJournals*. |

### 22.2 Layar
| Menu | Realisasi |
|---|---|
| **Journals** (`finance.journals`) | List (cabang/status/sumber/tanggal/cari) + ekspor; form jurnal manual (cabang, tanggal, keterangan, baris dinamis akun via lookup + cost center + keterangan + debit/kredit, **total & selisih langsung** "Balanced"), **mulai dari template** (baris & sisi terisi), lampiran; detail (baris, pembuat/penyetuju/pemosting, status, tautan jurnal pembalik/asal); **Edit/Delete** draft, **Approve** (checker; pembuat ditolak domain), **Post** (nomor `JU/…`, periode harus terbuka), **Reverse** (modal tanggal + alasan → membuka jurnal pembalik), **PDF journal voucher**. Jurnal otomatis: label sumber + **Open source document** (BPB, transfer/BPB kandang — ditentukan lewat aksi `Source`, retur, VI, PV, invoice, penerimaan, settlement, siklus, kas; tutup tahun → Fiscal Periods). Hak: Create; Edit = edit draft/approve/post/reverse/lampiran; Delete = hapus draft; Export = list & PDF. |
| **Kartu Journal** di dokumen | View component `JournalPreview` (tampil hanya bila punya hak View Journals) di detail BPB/transfer/retur (W4), tab Cost siklus tertutup (W5, penyesuaian HPP), invoice (+ nota kreditnya) & penerimaan (+ penerapan uang muka) (W6), VI/PV/kas (W7), settlement (W8); nomor jurnal menaut ke detail jurnal. |
| **Reports** (`/Reports/*`, `ReportsController`) | Satu halaman generik: form parameter (tanggal, akun/peternak via lookup, cabang, cost center, grouping, masa pajak) → tabel berstruktur; **General Ledger** (saldo awal, mutasi + saldo berjalan, nomor jurnal tertaut), **Trial Balance** (± saldo nol, catatan seimbang), **Income Statement**, **Balance Sheet** (bagian per akun induk, laba tahun berjalan/tahun lalu belum ditutup, catatan seimbang), **Cash Flow** (metode langsung per kategori), **Profitability** (cycle/coop/farmer/branch; per cabang + laba buku besar & overhead), **Tax Recap** (PPN: ringkasan kurang/lebih bayar, keluaran, retur, masukan; PPh dipotong per kode & per dokumen) + **CSV** (kolom sama dengan Web.Api). Hak View/Export per laporan. |
| Ekspor laporan | Model bersama `ReportDocument` (tabel, kolom berformat, baris *Detail/Section/Subtotal/Total* + indentasi, catatan) dirender ke **HTML, Excel (ClosedXML; satu lembar per tabel, judul & filter, angka asli, subtotal bergaris, total garis ganda) dan PDF** (`ReportExporter`); `ExportService.Report/Csv`. |
| **Failed Events** (`admin.failed-events`) | Daftar dead letter (tipe, waktu, percobaan, error), petunjuk penyebab umum, **Retry** satu / **Retry All** (hak Edit). Checklist tutup periode menautkan "Failed auto journals" ke layar ini dan "Manual journals still draft/approved" ke Journals. |
| **Dashboard** | 8 kartu KPI (tertaut sesuai hak) + daftar "Waiting for action" (7 jenis, tersembunyi tanpa hak) + grafik 6 bulan (penjualan batang, panen garis; Chart.js), mengikuti cabang aktif di header. |
| Tutup periode & tahun | Tidak ada layar baru — Fiscal Periods (W3) sudah mencakup checklist, tutup/buka kembali dan jurnal penutup Desember. |

### 22.3 Pengujian & verifikasi
- **Test: 388 lulus** — Domain 143, Application 60, Arsitektur 14, Integration Web.Api 18, Integration Web.App 153 (+18): 14 halaman W9 terbuka; jurnal manual (tidak seimbang ditolak → draft → edit → approve pembuat ditolak → checker approve → post bernomor → GL menampilkan & menautkan jurnal → PDF JV & ekspor → reverse tanpa alasan ditolak → reversal Posted & asal Reversed → hapus draft); laporan TB/IS/BS/CF/Profitability + ekspor xlsx/pdf (Excel dibuka ulang: judul, baris Total tebal), PDF GL/BS, pajak (4 lembar Excel, PDF PPh, 4 CSV dengan header Web.Api); event gagal (tampil, dashboard menandai, retry); dashboard & kartu jurnal mengikuti hak (user tanpa Journals tidak melihat kartu/daftar jurnal). Ekspektasi lama "finance.journals belum rilis" (W1/W3) diganti: semua menu katalog dirilis.
- **End-to-end W9 44/44** (Playwright, outbox di Web.App dengan interval 2 dtk, DB `intiplasma_verify` baru lalu di-drop): sidebar & semua menu 200; dashboard (kartu, daftar, grafik); template jurnal → jurnal dari template (baris terisi), selisih langsung, tidak seimbang ditolak domain, draft → edit → maker ditolak → checker approve → post `JU/…` → nama pembuat/penyetuju → PDF JV → reverse via modal → hapus draft → filter list; **dead letter nyata**: BPB bertanggal tahun lalu (tahun fiskal belum dibuka) → jurnal otomatis gagal 5× → Failed Events menampilkan penyebab, dashboard menandai → buka tahun fiskal → Retry All → jurnal BPB terposting, kartu Journal di BPB, detail jurnal otomatis tanpa tombol workflow, "Open source document" kembali ke BPB; GL (lookup akun, saldo penutup 0 setelah reversal, ekspor membawa parameter, tautan ke jurnal); 7 laporan tampil + Excel + PDF; periode lintas tahun di URL; 3 CSV pajak; tanpa dead letter & error JS. PDF journal voucher & neraca serta tampilan dashboard/jurnal/laba rugi diperiksa visual.
- **Regresi** (satu DB baru, urutan header → W0 → W2 … W9): header 15/15, W0 49/49, W2 40/40, W3 48/48, W4 51/51, W5 42/42, W6 42/42, W7 61/62, W8 44/44, W9 44/44. Satu-satunya FAIL W7 ("Variance badge" di list Vendor Invoices) adalah artefak urutan skrip: W0 memilih "Branch Alpha" sebagai cabang aktif admin sehingga list default tersaring ke cabang itu; dengan `branch=all` badge tampil (diperiksa manual). Ekspektasi W2/W3 "journals belum rilis" diganti "journals dirilis".

### 22.4 Catatan
- ⚠️ Skrip E2E lama memakai tanggal **UTC** (`toISOString`) — antara pukul 00.00–07.00 WIB tanggalnya berbeda dari aplikasi (Asia/Jakarta) sehingga skenario bertanggal "hari ini" gagal. Semua skrip kini memakai tanggal lokal.
- Skrip W9 membuka tahun fiskal sebelumnya; karena penutupan periode berurutan, jalankan W3 sebelum W9 pada DB verifikasi yang sama.
- Kartu Journal pada dokumen yang jurnalnya diproses outbox bisa kosong beberapa detik setelah posting (ditampilkan "No journal yet").
- Laporan memakai respons query yang sudah ada; batas jumlah baris laporan belum diterapkan (GL satu akun, TB per akun — ukuran wajar).

---

## 23. Rencana Fase W10 — Pengerasan

> Disusun 2026-10-04 dari inventaris kode setelah W9. Keputusan §23.10 disepakati user 2026-10-04.

### 23.1 Ruang lingkup & hasil akhir
Tidak ada fitur bisnis baru. Hasil akhir: Web.App siap dijalankan sebagai container produksi (bersama Web.Api, satu DB, satu volume), tahan terhadap brute force login & XSS dasar, sesi tetap valid setelah restart/antar replika, bisa dipakai di tablet, punya jejak audit ekspor & perubahan akses, dan punya panduan pengguna.

### 23.2 Temuan (kondisi saat ini)
| Area | Kondisi | Dampak |
|---|---|---|
| Security headers | `SecurityHeaders.cs`: `X-Frame-Options`, `nosniff`, `Referrer-Policy`, CSP **hanya** `frame-ancestors 'self'`. HSTS hanya di non-Development. Cookie `SecurePolicy = SameAsRequest`. | CSP belum membatasi script/style/koneksi. |
| Inline script | **21 view** berisi blok `<script>` inline (Login, Dashboard, Report, form dinamis: Journals, PO, SO, VI, PV, kas, kontrak, …), **16** handler `onchange=`/`onclick=`, **7** `href="javascript:void(0)"` (header, sidebar, branch switcher, user menu, menu search), **58** atribut `style=`. | CSP `script-src 'self'` langsung mematahkan halaman tersebut → perlu nonce + pemindahan handler. |
| Lockout login | Tidak ada (`User` tanpa hitungan gagal; `SignInUserCommand` & `LoginUserCommand` tidak membatasi). Web.App tanpa rate limiter (Web.Api punya global 100/menit/user). | Brute force password tidak tertahan. |
| Data Protection | Tidak dikonfigurasi → key ring per container, hilang saat restart. | Cookie sesi, antiforgery, dan `ip.branch` invalid setelah restart / antar replika (hutang W0). |
| Health check | `/health` (Npgsql + `outbox` bila job aktif), detail JSON anonim. | Belum ada pemisahan liveness/readiness; detail internal terbuka; storage & listener `LISTEN/NOTIFY` tidak dicek. |
| Docker | `src/Web.App/Dockerfile` (template VS, `EXPOSE 8081` tidak dipakai). `docker-compose.yml`: service `web-app` tanpa connection string, `depends_on`, healthcheck; override memakai `ASPNETCORE_ENVIRONMENT=Development`. | Compose belum siap produksi. Font QuestPDF, ICU (`id-ID`), dan tzdata (`Asia/Jakarta`) di image Linux belum diverifikasi. |
| Aset | `wwwroot` 21 MB. `assets/css/style.css` **1,4 MB tidak diminify**; link `assets/...` relatif tanpa `~/` → tidak ter-fingerprint (`asp-append-version` hanya sebagian). `MapStaticAssets` sudah aktif. | Muatan awal besar, cache tidak optimal setelah deploy. |
| Responsif | Belum pernah diuji di lebar tablet (768–1024 px): sidebar two-column, tabel lebar, form baris dinamis, matriks Menu Access. | — |
| Aksesibilitas | Belum diaudit. Catatan awal: kontras `#0984E3` di atas putih ±3,9:1 (di bawah AA 4,5:1 untuk teks kecil); tombol ikon tanpa `aria-label`; iframe perlu `title`. | — |
| Audit | Ekspor hanya dicatat ke log Serilog (`ExportService.LogExport`). Perubahan akses (user, profil Akses Menu/Cabang, role API, menu, cabang) hanya meninggalkan `modified_at/by`. Login tidak dicatat. | Tidak ada jejak "siapa mengubah apa" yang bisa dibuka admin. |
| Lain-lain | Peringatan "sesi akan habis" di parent (§3.10) & notifikasi header belum ada. Login DEBUG mengisi kredensial admin otomatis (hanya build Debug — aman di image Release). | — |

### 23.3 Keamanan HTTP & CSP
- **CSP berbasis nonce**: middleware membuat nonce per request, tag helper menambahkan `nonce` ke blok `<script>` inline. Kebijakan:
  `default-src 'self'; script-src 'self' 'nonce-…'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; frame-src 'self' blob:; frame-ancestors 'self'; form-action 'self'; base-uri 'self'; object-src 'none'`.
  `style-src 'unsafe-inline'` dipertahankan (atribut `style=`, SweetAlert2/Tom-Select/ApexCharts menyuntik style). Pratinjau lampiran PDF dicek terhadap `frame-src`.
- **Pemindahan handler inline**: `onchange="this.form.submit()"` → `data-autosubmit` (varian "hanya bila ada nilai"), salin label opsi → `data-copy-label="#id"`, tombol "Use it" settlement → `data-fill-target`; ditangani sekali di `app.js`. `href="javascript:void(0)"` → `href="#"` + `role="button"`.
- **Report-Only** dulu (pelanggaran dikirim ke `/csp-report` → Serilog), lalu enforce. Integration test memastikan header & nonce; E2E menangkap `securitypolicyviolation` = 0 di semua halaman menu.
- Header tambahan: `Permissions-Policy` (kamera/mikrofon/geolokasi mati), `Cross-Origin-Opener-Policy: same-origin`; HSTS tetap non-Development. Cookie `ip.auth`/`ip.branch`/antiforgery: `SecurePolicy = Always` di luar Development.
- **Forwarded headers** (`X-Forwarded-For/Proto`) untuk deploy di belakang reverse proxy — dibutuhkan juga oleh rate limiter & IP di audit.

### 23.4 Lockout login & rate limit
- **Domain `User`**: `AccessFailedCount`, `LockoutEndUtc`; `RegisterFailedSignIn(now, maxAttempts, lockoutDuration)`, `RegisterSuccessfulSignIn()`, `IsLockedOut(now)`, `Unlock()`. Reset password oleh admin & `Activate` ikut membuka kunci.
- `SignInUserCommand` (Web.App) **dan** `LoginUserCommand` (Web.Api) memakai aturan yang sama: terkunci → error baru `Users.LockedOut` ("Account is locked. Try again in N minutes."); password salah menaikkan hitungan; email tak dikenal tetap `Users.InvalidCredentials`.
- Opsi `Security:Lockout` (`MaxFailedAttempts` 5, `LockoutMinutes` 15), dipakai kedua host.
- Layar **Users**: badge "Locked until …" dan aksi **Unlock** (hak Edit).
- **Rate limiter Web.App**: `POST /Auth/Login` dibatasi per IP (10/menit) → 429 dengan halaman ramah.
- Migration **`PhaseW10_Hardening`** (kolom user + tabel §23.5 & §23.6).

### 23.5 Data Protection bersama
- `AddDataProtection().SetApplicationName("IntiPlasma.WebApp").PersistKeysToDbContext<…>()` dengan tabel `infrastructure.data_protection_keys` (paket `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) → semua replika & restart memakai key ring yang sama; sesuai W-12 (satu DB, tanpa volume tambahan).
- Key tidak terenkripsi at-rest secara default — dicatat di panduan deploy (opsi `ProtectKeysWithCertificate`).
- Verifikasi: login → restart Web.App → sesi & form (antiforgery) tetap valid.

### 23.6 Log audit (ekspor, perubahan akses, login)
- Tabel `infrastructure.audit_logs`: `id` (Guid v7), `occurred_at_utc`, `user_id`, `user_email`, `category` (`Access`/`Export`/`SignIn`), `action`, `entity_type`, `entity_id`, `summary` (English), `details` (`jsonb`: nilai lama/baru atau filter ekspor), `ip_address`, `source` (`WebApp`/`WebApi`).
- Abstraksi `IAuditLog` (Application), ditulis **dalam transaksi yang sama** oleh handler perubahan akses: create/update/delete/activate/deactivate/reset password/unlock user, set akses user, CRUD Akses Menu (ringkasan diff matriks) & Akses Cabang, role API & permission, ubah menu, status cabang. Karena handler dipakai bersama, perubahan lewat Web.Api ikut tercatat.
- Ekspor (Excel/PDF/CSV/cetak dokumen) dicatat `ExportService` (menu, judul, format, jumlah baris, filter).
- Login: sukses, gagal, terkunci (Web.App & Web.Api).
- Layar **Administration → Audit Log** (`admin.audit-logs`, View/Export): filter tanggal, kategori, user, cari; detail JSON; ekspor Excel/PDF. Tanpa edit/hapus.

### 23.7 Health check & operasional
- `/health/live` (tanpa dependensi) dan `/health/ready` (PostgreSQL, storage lampiran bisa ditulis, listener `cache_invalidation` tersambung, `outbox` di Web.App). Detail JSON hanya untuk jaringan internal; publik cukup status singkat. Berlaku untuk kedua host.
- **Docker**: Dockerfile Web.App & Web.Api dirapikan (port 8080, `USER $APP_UID`, `HEALTHCHECK`, tzdata/ICU diverifikasi, font QuestPDF tersedia); `docker-compose.yml` lengkap: `postgres` (healthcheck) → `web-api` & `web-app` (`depends_on: service_healthy`, connection string & secret dari `.env`, `BackgroundJobs__Enabled` true/false, volume `uploads` bersama, `restart: unless-stopped`); `.env.example`; override Development tetap untuk Visual Studio.
- **Migration produksi**: tetap manual oleh user, didokumentasikan dengan *EF migration bundle*. Web.App tidak menjalankan migration.
- **Peringatan sesi**: mainboard menampilkan modal "Your session will expire in 5 minutes" + *Stay signed in* (ping ringan yang memperpanjang cookie sliding).
- `docs/DEPLOY.md`: variabel lingkungan, volume, reverse proxy/TLS, backup DB & uploads, urutan upgrade (backup → migrate → deploy Web.Api & Web.App).

### 23.8 Aset, responsif & aksesibilitas
- **Aset**: semua link `assets/...` diubah ke `~/assets/...` agar ter-fingerprint `MapStaticAssets` (cache immutable + brotli). `style.css` diminify (`style.min.css` hasil generate, sumber tetap disimpan). Tanpa bundler runtime. Ukuran transfer diukur sebelum/sesudah.
- **Responsif (tablet 768–1024 px)**: sidebar collapse & tertutup otomatis setelah klik menu; tabel list `.table-responsive`; filter bar & tombol aksi membungkus; form baris dinamis bisa digulir horizontal; matriks Menu Access dengan header lengket. Ponsel bukan target (ada mobile app PPL), cukup tidak rusak.
- **Aksesibilitas (WCAG 2.1 AA dasar)**: audit **axe-core** lewat Playwright di halaman utama tiap modul, target 0 pelanggaran *critical/serious*; perbaikan umum: `lang="en"`, `title` iframe, `aria-label` tombol ikon, label terhubung ke input (termasuk Tom-Select & baris dinamis), fokus terlihat, pesan validasi `aria-live`, kontras teks/link primer.

### 23.9 Panduan pengguna
- `docs/user-guide/` — satu Markdown per modul (Login & navigasi, Administration, Master Data & Partnership, Procurement & Inventory, Production, Sales & AR, AP/Kas/Bank, Costing & Settlement, Journals/Reports/Closing): alur langkah demi langkah, arti status, maker-checker, hak menu yang dibutuhkan, screenshot dari E2E.

### 23.10 Keputusan (disepakati 2026-10-04)
| # | Topik | Usulan |
|---|-------|--------|
| 1 | CSP | Nonce untuk `<script>` inline; handler `on*`/`javascript:` dipindah ke `app.js`; `style-src 'unsafe-inline'` diterima. Report-Only dulu, enforce setelah E2E bersih. |
| 2 | Lockout | 5 kali gagal → terkunci 15 menit; berlaku Web.App **dan** Web.Api (⚠️ perilaku login mobile berubah); admin bisa Unlock; pesan "Account is locked" (risiko enumerasi email diterima — aplikasi internal). |
| 3 | Rate limit login Web.App | 10 percobaan/menit per IP. |
| 4 | Data Protection | Key ring di **PostgreSQL**, bukan volume file. |
| 5 | Log audit | Tabel `infrastructure.audit_logs` + layar Audit Log (View/Export); cakupan: perubahan akses, ekspor/cetak, login. Tanpa retensi otomatis dulu. |
| 6 | Bundling | Tanpa paket bundler; minify `style.css` + fingerprint semua aset. |
| 7 | Target perangkat | Desktop + **tablet**; ponsel tidak ditargetkan. |
| 8 | Aksesibilitas & warna | Audit axe-core, 0 critical/serious; warna **teks/link** primer digelapkan (mis. `#0770C2`) bila gagal AA, tombol tetap `#0984E3`. |
| 9 | Panduan pengguna | Markdown di `docs/user-guide/`, **Bahasa Indonesia** (nama tombol/menu tetap English seperti UI), tanpa halaman Help di aplikasi. |
| 10 | Migration produksi | Tetap manual oleh user; didokumentasikan di `docs/DEPLOY.md`. |
| 11 | Di luar W10 | Notifikasi header, approval terpusat (§4.8), Redis/scale-out, S3/MinIO, observabilitas lanjutan, hutang teknis domain (RANGKUMAN §8). |

### 23.11 Urutan task
1. **Domain/Application**: lockout di `User` (+ unit test), `SignIn`/`Login` memakai lockout, `UnlockUserCommand`; `IAuditLog` + penulisan di handler akses & login; `GetAuditLogsQuery`.
2. **Infrastructure**: EF config `audit_logs` & `data_protection_keys`, opsi `Security:Lockout`, health check storage & listener; migration **`PhaseW10_Hardening`**.
3. **Web.App keamanan**: Data Protection, CSP nonce + tag helper, pemindahan handler inline (21 view, 16 handler, 7 link), header tambahan, forwarded headers, cookie Secure, rate limiter login, peringatan sesi.
4. **Web.App layar**: Users (Locked + Unlock), **Audit Log**, ekspor tercatat ke audit.
5. **Aset & UI**: path `~/assets`, minify `style.css`, responsif tablet, perbaikan aksesibilitas.
6. **Deploy**: Dockerfile, `docker-compose.yml` + `.env.example`, health `/live` & `/ready`, uji `docker compose up` (login, PDF dengan font, zona waktu, upload ke volume bersama, outbox di Web.App).
7. **Pengujian**: unit (lockout), application (audit, unlock), integration Web.App (CSP & nonce, lockout + 429, audit log & layar, unlock, health, Data Protection di DB), integration Web.Api (login terkunci).
8. **E2E**: W10 (CSP tanpa pelanggaran di semua menu, lockout & unlock, audit log, viewport tablet + screenshot, axe-core, restart Web.App tanpa logout) + **regresi penuh W0–W9**.
9. `docs/user-guide/`, `docs/DEPLOY.md`, realisasi §24, RANGKUMAN.

### 23.12 Risiko & catatan teknis
- CSP paling rawan regresi: plugin template (`script.js`, feather, ApexCharts, Chart.js, SweetAlert2) bisa memakai `eval`/inline handler — Report-Only + tangkap pelanggaran di E2E sebelum enforce.
- Lockout di Web.Api mengubah perilaku login mobile (dicatat ⚠️ di RANGKUMAN); hitungan gagal di DB sehingga konsisten antar proses.
- Audit dalam transaksi yang sama menambah satu insert per command akses (volume kecil); audit ekspor ditulis setelah file berhasil dibuat.
- Minify `style.css` diverifikasi visual (screenshot sebelum/sesudah).
- Image Linux: pastikan `Asia/Jakarta`, culture `id-ID` (ICU, bukan invariant mode) dan font PDF tersedia — diuji di `docker compose`, bukan hanya Windows.

---

## 24. Realisasi Fase W10 — Pengerasan (2026-10-04)

Migration baru **`PhaseW10_Hardening`**: kolom `identity.users.access_failed_count` & `lockout_end_utc`, tabel `infrastructure.audit_logs` dan `infrastructure.data_protection_keys`.

### 24.1 Domain, Application, Infrastructure
| Area | Realisasi |
|---|---|
| Lockout | `User.RegisterFailedSignIn/RegisterSuccessfulSignIn/IsLockedOut/Unlock`; `ChangePassword` (reset & ganti sendiri) dan `Activate` membuka kunci. `CredentialVerifier` (Application) dipakai **`SignInUserCommand` (Web.App) dan `LoginUserCommand` (Web.Api)**: email tak dikenal & password salah tetap pesan yang sama (API tetap `Users.NotFoundByEmail`), terkunci → `Users.LockedOut` (sisa menit). Opsi `Security:Lockout` (5 kali / 15 menit). `UnlockUserCommand` + ⚠️ endpoint baru `POST users/{id}/unlock`. `GetUsers`/`GetUserById` memuat `lockoutEndUtc` (& `accessFailedCount`). |
| Audit trail | Entity `AuditLog` (Domain/Auditing, kategori Access/Export/SignIn), `IAuditTrail` + `AuditTrail` (Infrastructure: user dari claim cookie/JWT, IP, `source` = Web.App/Web.Api, detail JSON dengan properti `*password*` disamarkan `***`). **`AuditDecorator`** (decorator command terdalam) mencatat command bertanda `IAuditedCommand` yang sukses — 22 command akses (user, profil Akses Menu/Cabang, menu, API role, cabang, unlock), baik dari Web.App maupun Web.Api. Penyimpanan audit memakai `SaveChanges` tersendiri setelah command sukses (bukan transaksi yang sama — penyesuaian dari §23.6; command gagal tidak dicatat). Login sukses/gagal/terkunci/ditolak dicatat `CredentialVerifier`. `RecordExportCommand`, `GetAuditLogsQuery`, `GetAuditLogByIdQuery`. |
| Data Protection | `ApplicationDbContext` = `IDataProtectionKeyContext`; `AddSharedDataProtection("IntiPlasma.WebApp")`. Paket `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` + pin `System.Security.Cryptography.Xml` 10.0.12 (advisory NU1903 pada 10.0.9). |
| Health | `MapAppHealthChecks` (kedua host): `/health/live` (tanpa cek), `/health/ready`, `/health` (detail JSON hanya untuk IP loopback/privat). Cek baru `storage` (folder lampiran bisa ditulis) & `cache-invalidation` (listener `LISTEN` tersambung, Degraded bila putus). |
| Web.Api | `Database:SeedOnStartup` untuk instalasi produksi pertama (seed idempotent di luar Development). |

### 24.2 Web.App
| Area | Realisasi |
|---|---|
| CSP | Nonce hex per request + `ScriptNonceTagHelper` (semua `<script>`); kebijakan `default-src 'self'; script-src 'self' 'nonce-…'; style-src 'self' 'unsafe-inline'; img/font data:/blob:; frame-src 'self' blob:; frame-ancestors/form-action/base-uri 'self'; object-src 'none'; report-uri /csp-report` **hanya untuk respons HTML** (file PDF/Excel tetap `frame-ancestors 'self'` agar pratinjau PDF tidak terganggu). `Security:CspReportOnly` untuk diagnosa; laporan pelanggaran dicatat log `Web.App.Csp`. 16 handler `onchange/onclick` → `data-autosubmit`, `data-copy-label`, `data-fill-target` (`app.js`); 7 `javascript:void(0)` → `href="#"` + `role="button"` (dicegah navigasi di `app.js`/`mainboard.js`). Header `Permissions-Policy`, `Cross-Origin-Opener-Policy`. |
| Font | ⚠️ Template meng-`@import` Google Fonts (Nunito, Poppins) — diblokir CSP & butuh internet. **Nunito di-host sendiri** (`assets/fonts/nunito`, woff2 variabel latin + latin-ext, SIL OFL), Poppins tidak dipakai → dihapus. |
| Cookie & proxy | `Security:SecureCookies` (default true; Development & test false) untuk cookie sesi + antiforgery; `Security:TrustForwardedHeaders` → `UseForwardedHeaders` (X-Forwarded-For/Proto). |
| Rate limit | Policy `login` pada `POST /Auth/Login`: `Security:LoginPermitPerMinute` (10) per IP, fixed window → 429 + halaman "Too many attempts". |
| Users | Badge **Locked** (list & detail, *Locked until …*), jumlah *Failed sign-ins*, aksi **Unlock** (Edit), tombol **Audit Log** per user. |
| Audit Log | Menu baru `admin.audit-logs` (View/Export): filter cari/kategori/tanggal (WIB)/user, detail JSON terformat, ekspor Excel/PDF. Ekspor & cetak dokumen dicatat lewat `ExportAuditFilter` (result filter global; `ExportService` menandai ekspor, filter menulis setelah file terkirim — semua pemanggil lama tidak berubah). |
| Sesi | Setiap halaman membawa `data-session-expires` (memperhitungkan sliding renewal); mainboard menampilkan modal 5 menit sebelum habis, **Stay signed in** → `POST /Main/KeepAlive`; saat habis → login dengan `ReturnUrl` halaman saat ini. |
| Aset | Semua `assets/...` → `~/assets/...` + `asp-append-version` (fingerprint `MapStaticAssets`). `style.min.css` hasil lightningcss (1,43 → 1,18 MB; gzip 153 → 139 KB) — dirender identik dengan `style.css` (perbandingan screenshot piksel 4 halaman). Regenerasi: `npx lightningcss-cli --minify --error-recovery style.css -o style.min.css`. |
| Tablet | ⚠️ Template menyembunyikan sidebar two-column di < 992 px sehingga menu **tidak bisa dibuka** di tablet; kini sidebar slide-in lewat tombol ☰ (CSS `theme.css`), tombol tidak mengubah hash, sidebar menutup setelah memilih menu. Tidak ada scroll horizontal halaman di 768 & 1024 px. |
| Aksesibilitas | axe-core (WCAG 2.1 A/AA) pada 18 halaman + login: 364 node critical/serious → 0 (kecuali tombol primer, lihat catatan). Warna teks AA di `theme.css`: primer teks/link `#0770C2`, muted `#646B72`, danger `#C82333`, badge success `#157347`; link sidebar & judul menu; `aria-label` modul sidebar, tombol menu/akun, filter cabang/cari; nama aksesibel otomatis (`app.js`) untuk input baris tabel ("Quantity — line 2"), select filter, dan input Tom-Select. |
| Deploy | Dockerfile kedua host: port 8080, non-root, folder `/app/uploads` milik user app, `HEALTHCHECK` via bash `/dev/tcp` ke `/health/live` (image tanpa curl). `docker-compose.yml`: postgres dengan healthcheck → web-api/web-app (`depends_on: service_healthy`, `env_file: .env` opsional, connection string dari `POSTGRES_*`, named volume `uploads`, `restart: unless-stopped`). `.env.example`, `.env` di-ignore git & image. `docs/DEPLOY.md`, `docs/user-guide/` (9 modul + README, Bahasa Indonesia, screenshot). |

### 24.3 Pengujian & verifikasi
- **Test: 412 lulus** — Domain 146 (+3), Application 68 (+8: lockout sign-in/login, audit decorator, unlock), Arsitektur 14, Integration Web.Api 19 (+1 lockout & unlock API), Integration Web.App 165 (+12 `HardeningTests`: CSP & nonce per request, file tanpa CSP halaman, tanpa handler inline, lockout → Locked → Unlock → audit, rate limit 429, audit akses dengan password disamarkan + layar & ekspor, audit ekspor, 403 tanpa hak, health, key Data Protection di DB, keep-alive).
- **E2E W10 53/53** (Playwright, Web.App :5098 + Web.Api :5099, DB `intiplasma_verify` di-migrate dengan *migration bundle*): 55 menu + 12 form tanpa pelanggaran CSP & 200, perilaku `data-*`, lockout 5× → pesan terkunci → badge → Unlock → login, audit (urutan event, aktor, masking, list/detail/ekspor), modal sesi & redirect saat habis, tablet portrait/landscape, axe, CSS minify identik, health. **Restart 7/7**: form yang dibuka sebelum restart tetap bisa disimpan (antiforgery) & sesi tetap, key ring tidak bertambah, rate limit 3/menit → 429.
- **Container 20/20** (Podman + docker-compose v2, image dari Dockerfile, DB di-migrate bundle, `Database__SeedOnStartup`): non-root, tzdata/WIB, format id-ID, PDF (font tersemat) & Excel di Linux, upload Web.App terlihat di container Web.Api (volume bersama), audit dari kedua sumber, key DP di DB, HEALTHCHECK *healthy*.
- **Regresi** (satu DB baru, urutan header → W0 → W2 … W9): header 15/15, W0 49/49 (ekspektasi warna avatar diperbarui ke `#0770C2`), W2 40/40, W3 48/48, W4 51/51, W5 42/42, W6 42/42, W7 61/62 (artefak urutan skrip yang sama seperti W9: badge Variance tersaring cabang aktif dari W0), W8 44/44, W9 44/44.

### 24.4 Catatan
- ⚠️ **Login API**: 5 kali salah password mengunci akun 15 menit (juga untuk mobile); respons `400` dengan `Users.LockedOut`. Hitungan bersama Web.App & Web.Api.
- ⚠️ Tombol primer (`btn-primary`, putih di atas `#0984E3`, rasio 3,87) **tetap** sesuai keputusan §23.10 #8 — satu-satunya temuan axe yang dikecualikan.
- Avatar inisial & badge primer kini `#0770C2` (skrip E2E W0 diperbarui).
- Migration bundle butuh variabel `ConnectionStrings__Database` saat dijalankan (host Web.Api dibangun untuk membuat DbContext); argumen `--connection` saja tidak cukup.
- Podman rootless: `docker compose build` (buildkit) gagal → build image dengan `podman build --format docker`, lalu `compose up --no-build`. Docker Desktop tidak terdampak.
- Audit tanpa retensi otomatis; tabel bisa tumbuh — rencanakan arsip/purge bila perlu.
