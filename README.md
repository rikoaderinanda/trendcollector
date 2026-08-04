# 🎯 AI Content Factory — MVP Trend Collector Agent

Agent pertama dari platform **AI Content Factory**. Agent ini bertugas mengumpulkan data selengkap mungkin dari **YouTube** untuk menjadi knowledge base utama bagi agent-agent AI di masa depan (Transcript Collector, Viral Analyzer, Thumbnail Analyzer, Audience Analyzer, Script Generator, Storyboard Generator, Prompt Generator, dan Learning Agent).

> ⚠️ **Catatan**: Project ini **bukan** YouTube downloader. Fokus utamanya adalah kualitas dan kelengkapan data yang dikumpulkan, bukan kecepatan eksekusi.

---

## 🗺️ Status Proyek

| Phase | Deskripsi | Status |
|---|---|---|
| 1 | Project Structure | ✅ Selesai |
| 1.5 | Integrasi Database Neon | ✅ Selesai |
| 2 | Database Design | ✅ Selesai |
| 3 | Entities | ✅ Selesai |
| 4 | Repositories | ✅ Selesai |
| 5 | Services | ✅ Selesai |
| 6 | Controller | ✅ Selesai |
| 7 | SQL Script | ⏳ Belum dikerjakan |
| 8 | Swagger Examples | ⏳ Belum dikerjakan |
| 9 | Testing Guide | ⏳ Belum dikerjakan |

---

## ⚙️ Teknologi

- **Backend**: ASP.NET Core (.NET 10) + C#
- **Data Access**: Dapper
- **Database**: PostgreSQL (Neon, cloud)
- **API**: REST API
- **Dokumentasi**: Swagger / OpenAPI

---

## 🚀 Quick Start

### Prasyarat
- .NET 10 SDK
- Akun Neon PostgreSQL (atau PostgreSQL lokal)
- YouTube Data API v3 key ([Google Cloud Console](https://console.cloud.google.com/))

### Setup

1. **Restore dependencies**
   ```bash
   dotnet restore
   ```

2. **Konfigurasi rahasia** — buat file `TrendCollector.Api/appsettings.Local.json` (file ini **tidak ikut di-commit** karena di-gitignore):
   ```json
   {
     "ConnectionStrings": {
       "Postgres": "Host=host.neon.tech;Port=5432;Database=neondb;Username=user;Password=password;SSL Mode=Require"
     },
     "YouTube": {
       "ApiKey": "YOUR_YOUTUBE_API_KEY"
     }
   }
   ```
   > Alternatif: gunakan environment variable `ConnectionStrings__Postgres` dan `YouTube__ApiKey`.

3. **Jalankan API**
   ```bash
   cd TrendCollector.Api
   dotnet run
   ```

4. **Buka Swagger**: http://localhost:5075/swagger

---

## 🧩 Arsitektur (Rencana)

```
Client
   │
   ▼
POST /api/trend/collect
   │
   ▼
TrendCollectorService
   │
   ▼
YouTube API
   │
   ▼
Map Data
   │
   ▼
Save PostgreSQL
   │
   ▼
Return Summary
```

---

## 📚 Struktur Proyek

```
Agent Trend collector/
├── TrendCollector.slnx
└── TrendCollector.Api/
    ├── Program.cs                       # Entry point + wiring DI
    ├── appsettings.json                 # Konfigurasi placeholder (tanpa secret)
    ├── appsettings.Development.json     # Konfigurasi development
    ├── appsettings.Local.json           # KONFIGURASI RAHASIA (gitignored)
    ├── Configuration/                   # Options pattern (DatabaseOptions)
    ├── Controllers/                     # REST API endpoints (Phase 6)
    ├── Data/                            # DbConnectionFactory, DbInitializer
    ├── Models/                          # Entities & DTOs (Phase 3)
    ├── Properties/launchSettings.json   # Launch profile
    ├── Repositories/                    # Data access layer (Phase 4)
    ├── Services/                        # Business logic layer (Phase 5)
    └── SQL/schema.sql                   # DDL database (Phase 7)
```

---

## 🔌 API (Rencana — belum diimplementasikan)

| Method | Endpoint | Deskripsi |
|---|---|---|
| POST | `/api/trend/collect` | Menjalankan koleksi tren berdasarkan keyword |
| GET | `/api/trend/jobs` | Mendapatkan daftar job koleksi |
| GET | `/api/trend/videos` | Mendapatkan daftar video yang tersimpan |
| GET | `/api/trend/videos/{id}` | Mendapatkan detail video |

**Contoh request `POST /api/trend/collect`:**
```json
{
  "keyword": "AI",
  "language": "id",
  "country": "ID",
  "maxResults": 20
}
```

---

## 💾 Desain Database (Rencana)

Skema dirancang untuk kompatibilitas multi-platform (YouTube, TikTok, Instagram, Facebook, Reddit, X) tanpa perubahan skema besar — menggunakan `platform_id`, `platform_video_id`, dan `platform_channel_id`.

| Tabel | Fungsi |
|---|---|
| `platforms` | Lookup platform (youtube, tiktok, instagram, dll) |
| `channels` | Informasi channel lengkap |
| `trending_videos` | Metadata video lengkap + thumbnail + `raw_json` |
| `video_statistics` | Statistik + metrik engagement (engagement_rate, like_ratio, dll) |
| `collection_jobs` | Riwayat eksekusi koleksi |

`raw_json` (JSONB) menyimpan **seluruh response API asli** — data apa pun yang belum dipetakan hari ini tetap tersedia untuk agent AI di masa depan.

---

## 🔄 Git Workflow

- Branch utama: `main`
- Remote: `origin` → `https://github.com/rikoaderinanda/trendcollector.git`
- `appsettings.Local.json` (berisi rahasia) **tidak pernah** di-commit
- Commit mengikuti pola: `feat:`, `fix:`, `docs:` (conventional commits)

---

## ⚠️ Keamanan

- Jangan pernah commit API key atau password ke repository
- Gunakan `appsettings.Local.json` (lokal, gitignored) atau environment variable
- Disarankan **rotate** YouTube API key yang pernah terekspos di chat publik
- Untuk production, gunakan secret manager (misal: GitHub Secrets, Azure Key Vault)

---

## 📄 Lisensi

Hak milik / private. Hubungi pemilik repository untuk informasi lebih lanjut.