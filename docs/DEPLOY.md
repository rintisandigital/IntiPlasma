# Panduan Deploy — Inti-Plasma (Web.Api + Web.App)

> Fase W10 (PLAN-WEBAPP §23.7). Dua container aplikasi, satu database PostgreSQL, satu volume lampiran (W-12).

## 1. Komponen

| Service | Image / Dockerfile | Port (default) | Peran |
|---|---|---|---|
| `postgres` | `postgres:17` | 5432 | Database tunggal (semua schema) |
| `web-api` | `src/Web.Api/Dockerfile` | 5000 → 8080 | API mobile & integrasi (JWT). **Tidak** menjalankan background job |
| `web-app` | `src/Web.App/Dockerfile` | 5002 → 8080 | Admin office (MVC). **Menjalankan** background job (outbox, cleanup lampiran) |
| volume `uploads` | — | — | `/app/uploads` di kedua container (lampiran) |
| `seq` | `datalust/seq` | 8081 | Opsional, log terpusat (development) |

⚠️ Bila `web-app` mati, event outbox dari transaksi API/mobile (jurnal otomatis, gudang kandang) **tertunda** sampai `web-app` hidup lagi — tidak hilang.

Image berbasis `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian): ICU (format `id-ID`) dan tzdata (`Asia/Jakarta`) tersedia, aplikasi berjalan sebagai user non-root, font PDF disematkan oleh QuestPDF dan font UI (Nunito) di-host sendiri — **tidak butuh akses internet**.

## 2. Konfigurasi (`.env`)

Salin `.env.example` → `.env` di samping `docker-compose.yml` lalu isi. `.env` tidak masuk git maupun image.

| Variabel | Keterangan |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Database; connection string kedua aplikasi dibentuk dari sini |
| `WEB_API_PORT`, `WEB_APP_PORT`, `POSTGRES_PORT` | Port di host |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpirationInMinutes` | Token mobile (Web.Api). Secret acak ≥ 32 karakter |
| `App__CompanyName`, `App__TimeZone` | Kop dokumen & zona waktu tampilan |
| `Security__Lockout__MaxFailedAttempts`, `Security__Lockout__LockoutMinutes` | Lockout login (default 5 kali / 15 menit), berlaku Web.App & Web.Api |
| `Security__LoginPermitPerMinute` | Batas percobaan login Web.App per IP per menit (default 10) |
| `Security__TrustForwardedHeaders` | `true` bila di belakang reverse proxy (lihat §5) |
| `Security__SecureCookies` | `true` (default): cookie hanya lewat HTTPS. `false` hanya bila terpaksa melayani HTTP biasa |
| `Security__CspReportOnly` | `false` (default). `true` hanya untuk diagnosa: CSP dilaporkan, tidak diblokir |

Variabel `Bagian__Kunci` menimpa `appsettings.json` (mis. `Outbox__MaxAttempts`, `FileStorage__OrphanRetentionHours`).

## 3. Instalasi pertama

1. **Build image**: `docker compose build` (atau `podman build --format docker -f src/Web.App/Dockerfile -t webapp .` dan hal yang sama untuk Web.Api).
2. **Database**: `docker compose up -d postgres`.
3. **Migration** (manual, dari mesin admin — aplikasi tidak menjalankan migration di Production):
   ```bash
   # sekali buat bundle (di mesin dengan .NET SDK + dotnet-ef)
   dotnet ef migrations bundle --project src/Infrastructure --startup-project src/Web.Api -o efbundle.exe
   # jalankan terhadap database tujuan (variabel ConnectionStrings__Database wajib ada)
   ConnectionStrings__Database="Host=<host>;Port=5432;Database=intiplasma;Username=postgres;Password=<pw>" ./efbundle.exe
   ```
   Alternatif: `dotnet ef database update --project src/Infrastructure --startup-project src/Web.Api --connection "<connection string>"`.
4. **Data awal** (profil sistem Full Access & All Branches, satuan, COA standar, admin pertama): jalankan Web.Api **sekali** dengan
   `Database__SeedOnStartup=true`, `Seed__Admin__Email=…`, `Seed__Admin__Password=…` (tambahkan sementara ke `.env`), lalu hapus lagi ketiganya. Seeding idempotent.
5. **Jalankan**: `docker compose up -d`. Cek `http://<host>:5002/health/ready` dan `http://<host>:5000/health/ready` → `Healthy`.
6. Login Web.App dengan admin awal, **ganti password**, buat Akses Menu/Cabang & user lain (Administration).

## 4. Upgrade versi

1. **Backup** database (`pg_dump -Fc`) dan volume `uploads` (§6).
2. `git pull` → build image baru.
3. Terapkan migration baru (bundle baru, langkah §3.3).
4. `docker compose up -d` (Web.Api & Web.App diganti bersamaan; keduanya kompatibel dengan skema yang sama).
5. Cek `/health/ready`, login, buka satu dokumen.

Catatan W10: migration `PhaseW10_Hardening` menambah kolom lockout user, tabel `infrastructure.audit_logs` dan `infrastructure.data_protection_keys`.

## 5. Reverse proxy & TLS

Disarankan reverse proxy (Nginx, Caddy, Traefik, IIS ARR) di depan `web-app` (dan `web-api`) yang menangani HTTPS.

- Set `Security__TrustForwardedHeaders=true` agar aplikasi melihat skema HTTPS dan IP klien asli (dipakai rate limit login, log audit, HSTS).
- ⚠️ Dengan opsi itu aplikasi mempercayai header `X-Forwarded-*` dari mana pun: **jangan** buka port 5002/5000 langsung ke jaringan luar — hanya proxy yang boleh mengakses container.
- Proxy harus meneruskan `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`.
- Header keamanan (CSP, X-Frame-Options, HSTS, dll.) dikirim aplikasi; proxy tidak perlu menambahkannya (jangan menimpa `Content-Security-Policy`).

## 6. Data, backup & kunci

| Data | Lokasi | Backup |
|---|---|---|
| Database | volume/bind `./.containers/db` (service `postgres`) | `pg_dump -Fc intiplasma > intiplasma-YYYYMMDD.dump` (harian) |
| Lampiran | volume `uploads` (`/app/uploads`) | salin isi volume (`docker run --rm -v <proyek>_uploads:/data -v $PWD:/backup alpine tar czf /backup/uploads.tgz -C /data .`) |
| Kunci Data Protection (cookie sesi & token form) | tabel `infrastructure.data_protection_keys` | ikut backup database |

- Kunci Data Protection disimpan di PostgreSQL sehingga sesi tetap valid setelah restart dan di semua replika Web.App. Kunci tidak dienkripsi at-rest; bila diwajibkan, tambahkan `ProtectKeysWithCertificate` (perlu perubahan kode kecil).
- Restore: `pg_restore -c -d intiplasma file.dump` + kembalikan isi volume `uploads` → start ulang kedua aplikasi.

## 7. Health check & monitoring

| Endpoint | Isi | Untuk |
|---|---|---|
| `/health/live` | Proses menjawab (tanpa cek dependensi) | `HEALTHCHECK` container / restart otomatis |
| `/health/ready` | PostgreSQL, storage lampiran bisa ditulis, listener `cache_invalidation`, outbox (Web.App) | Load balancer / monitoring |
| `/health` | Sama dengan ready + rincian JSON (hanya dari jaringan lokal/privat; dari luar hanya status) | Diagnosa admin |

- `Degraded` pada `outbox` = ada event tertunda > 5 menit atau dead letter → buka **Administration → Failed Events**.
- `Degraded` pada `cache-invalidation` = koneksi `LISTEN` putus (reconnect otomatis); perubahan akses dari proses lain baru berlaku saat cache kedaluwarsa.
- Log: console (JSON Serilog) → `docker compose logs -f web-app`. Pelanggaran CSP dicatat dengan kategori `Web.App.Csp`.
- Jejak audit (perubahan akses, ekspor, login): **Administration → Audit Log**.

## 8. Keamanan (ringkas)

- Lockout login 5 kali gagal → 15 menit (Web.App & API); admin bisa **Unlock** di Users. Rate limit login Web.App per IP.
- CSP ketat (script hanya dari aplikasi + nonce), cookie `HttpOnly` + `Secure` + `SameSite=Lax`, halaman hanya boleh di-frame oleh aplikasi sendiri.
- Ganti `Jwt__Secret` dan password database dari contoh; batasi akses port PostgreSQL ke jaringan internal.
- Lisensi QuestPDF Community (omzet < USD 1 juta/tahun) — tinjau ulang bila usaha berkembang.
