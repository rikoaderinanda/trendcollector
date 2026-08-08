# 🎯 AI Content Factory — Trend Collector Agent

Agent **Trend Collector** dari platform **AI Content Factory**. Agent ini mengumpulkan data selengkap mungkin dari **YouTube** untuk menjadi knowledge base utama bagi agent-agent AI di masa depan (Transcript Collector, Viral Analyzer, Thumbnail Analyzer, Audience Analyzer, Script Generator, Storyboard Generator, Prompt Generator, dan Learning Agent).

> ⚠️ **Catatan**: Project ini **bukan** YouTube downloader. Fokus utamanya adalah kualitas dan kelengkapan data yang dikumpulkan, bukan kecepatan eksekusi.

---

## 📦 Status Proyek

| Phase | Deskripsi | Status |
|---|---|---|
| 1–8 | Core: struktur, database, entities, repositories, services, controllers, SQL, Swagger | ✅ Selesai |
| 9 | Discovery Mode (search.list) + Tracking Mode (videos.list) | ✅ Selesai |
| 10 | Quota tracking & auto-switch ke Tracking Mode saat quota habis | ✅ Selesai |
| 11 | Integrasi Knowledge Extraction (auto-enqueue) | ✅ Selesai |
| 12 | Dashboard Frontend (React + Vite + TanStack Query) | ✅ Selesai |
| 13 | Workflow monitoring (end-to-end Discovery → Collection → Extraction) | ✅ Selesai |
| 14 | **Reliability improvements**: error granular, retry policy, concurrency guard, health check, data retention | ✅ Selesai |
| 15 | Testing Guide | ⏳ Belum dikerjakan |

---

## ⚙️ Teknologi

- **Backend**: ASP.NET Core (.NET 10) + C#
- **Data Access**: Dapper
- **Database**: PostgreSQL (Neon, cloud)
- **API**: REST API + Swagger / OpenAPI
- **Frontend**: React 18 + Vite + TypeScript + Tailwind CSS + TanStack Query
- **AI Provider**: OpenAI-compatible (DeepSeek) untuk Trend Discovery & Knowledge Extraction

---

## 🏗️ Arsitektur

Project berisi **3 agent** yang dijalankan in-process dalam satu API (`AIContentFactory.Api`):

```
┌────────────────────────────────────────────────────────────────┐
│                     AIContentFactory.Api                        │
│                                                                │
│  Agent 0: Discovery ──► trend_keywords                         │
│                                │ (active)                      │
│                                ▼                               │
│  Agent 1: Collector ──► collection_jobs (running/completed)    │
│                                │                               │
│                                ▼                               │
│               trending_videos + video_statistics               │
│                                │ (auto-enqueue)                │
│                                ▼                               │
│  Agent 2: Extraction ──► knowledge_extraction_queue            │
│               video_transcripts / video_knowledge / raw        │
│                                                                │
│  Background services:                                          │
│   ├─ TrendCollectionBackgroundService   (discovery polling)    │
│   ├─ TrendTrackingBackgroundService     (statistics refresh)   │
│   ├─ KnowledgeExtractionBackgroundService (queue worker)       │
│   └─ DataRetentionBackgroundService     (cleanup)              │
└────────────────────────────────────────────────────────────────┘
```

### Mode Koleksi

| Mode | Deskripsi | Trigger |
|---|---|---|
| **Discovery** | `search.list` untuk cari video baru per keyword, lalu `videos.list` + `channels.list` untuk detail lengkap, simpan ke DB | Manual `POST /api/trend/collect` atau polling background service |
| **Tracking** | Hanya `videos.list` untuk refresh statistik & menghitung velocity (views/h, growth score) video yang sudah dikoleksi — **tanpa** `search.list` (hemat quota) | Auto-switch saat quota harian habis, atau background service periodik |

**Quota-driven**: Jika `search.list` sudah mencapai `MaxSearchCallsPerDay` (default 10/hari), semua call `CollectAsync` otomatis beralih ke Tracking Mode.

---

## 🚀 Quick Start

### Prasyarat
- .NET 10 SDK
- Node.js 18+ (untuk Dashboard Frontend)
- Akun Neon PostgreSQL (atau PostgreSQL lokal)
- YouTube Data API v3 key ([Google Cloud Console](https://console.cloud.google.com/))
- (Opsional) DeepSeek API key untuk AI Discovery / Knowledge Extraction

### Setup

1. **Restore dependencies**
   ```bash
   dotnet restore
   ```

2. **Konfigurasi rahasia** — buat file `AIContentFactory.Api/appsettings.Local.json` (file ini **tidak ikut di-commit** karena di-gitignore):
   ```json
   {
     "ConnectionStrings": {
       "Postgres": "Host=host.neon.tech;Port=5432;Database=neondb;Username=user;Password=password;SSL Mode=Require"
     },
     "YouTube": {
       "ApiKey": "YOUR_YOUTUBE_API_KEY"
     },
     "TrendDiscovery": {
       "ApiKey": "YOUR_DEEPSEEK_API_KEY"
     },
     "KnowledgeExtraction": {
       "ApiKey": "YOUR_DEEPSEEK_API_KEY"
     }
   }
   ```
   > Alternatif: gunakan environment variable `ConnectionStrings__Postgres`, `YouTube__ApiKey`, dll.

3. **Jalankan API**
   ```bash
   cd AIContentFactory.Api
   dotnet run
   ```
   - Database schema otomatis di-apply saat startup (idempotent)
   - Background services otomatis mulai setelah API berjalan

4. **Buka Swagger**: http://localhost:5000/swagger

5. **Jalankan Dashboard Frontend** (opsional)
   ```bash
   cd Dashboard.Frontend
   npm install
   npm run dev
   ```
   Buka http://localhost:5173

---

## 📚 Struktur Proyek

```
Agent Trend collector/
├── AIContentFactory.Api/              # Backend API (3 agent in-process)
│   ├── Program.cs                     # Entry point + DI wiring
│   ├── appsettings.json               # Konfigurasi placeholder (tanpa secret)
│   ├── appsettings.Local.json         # KONFIGURASI RAHASIA (gitignored)
│   ├── AI/                            # AI provider (Discovery & Extraction)
│   ├── Configuration/                 # Options pattern
│   ├── Controllers/                   # REST API endpoints
│   │   ├── TrendController.cs         # Collect / jobs / videos
│   │   ├── TrendDiscoveryController.cs
│   │   ├── KnowledgeExtractionController.cs
│   │   └── HealthController.cs        # /api/health
│   ├── Exceptions/                    # Custom exception types (quota, key, transient)
│   ├── Data/                          # DbConnectionFactory, DbInitializer
│   ├── Models/                        # Entities & DTOs
│   ├── Repositories/                  # Data access layer (Dapper)
│   ├── Services/                      # Business logic layer
│   │   ├── TrendCollectorService.cs           # Discovery mode
│   │   ├── TrendCollectionBackgroundService.cs
│   │   ├── TrendTrackingBackgroundService.cs
│   │   ├── CollectionCoordinator.cs           # Concurrency guard
│   │   ├── DataRetentionBackgroundService.cs  # Cleanup job
│   │   ├── YouTubeApiService.cs               # Retry + error mapping
│   │   └── ...                                # Quota, Statistics, Queue, dsb.
│   ├── SQL/schema.sql                 # DDL database (idempotent)
│   ├── Transcript/                    # YouTube transcript provider
│   └── Workers/                       # Knowledge extraction worker
├── Dashboard.Frontend/                # React dashboard
│   └── src/
│       ├── api/                       # Axios clients per agent
│       ├── hooks/                     # TanStack Query hooks
│       ├── pages/                     # Dashboard, Workflow, Collector, Discovery, Extraction
│       └── types/                     # TypeScript types
└── docs/database-design.md            # Dokumentasi desain database
```

---

## 🔌 API

| Method | Endpoint | Deskripsi |
|---|---|---|
| POST | `/api/trend/collect` | Menjalankan koleksi tren (Discovery / auto-switch Tracking) |
| GET | `/api/trend/jobs` | Riwayat collection jobs (filter `date`, pagination) |
| GET | `/api/trend/videos` | Daftar video tersimpan (filter `language`, `date`, pagination) |
| GET | `/api/trend/videos/{id}` | Detail video + statistik terbaru + velocity metrics |
| GET | `/api/trend-discovery/keywords` | Daftar keyword hasil discovery |
| GET | `/api/trend-discovery/jobs` | Riwayat discovery jobs |
| POST | `/api/trend-discovery/run` | Menjalankan discovery AI |
| GET | `/api/knowledge-extraction/jobs` | Antrian knowledge extraction |
| GET | `/api/knowledge-extraction/videos/{id}` | Detail lengkap extraction (metadata + transcript + knowledge + queue) |
| GET | `/api/health` | Health check (liveness + DB connectivity) |

**Contoh request `POST /api/trend/collect`:**
```json
{
  "keyword": "AI",
  "language": "id",
  "country": "ID",
  "maxResults": 20
}
```

**Contoh response:**
```json
{
  "jobId": 42,
  "keyword": "AI",
  "mode": "Discovery",
  "totalCollected": 20,
  "totalSaved": 18,
  "totalSkipped": 2,
  "searchCallsRemaining": 7,
  "startedAt": "2026-08-08T08:00:00Z",
  "finishedAt": "2026-08-08T08:00:02Z",
  "durationMs": 2345
}
```

---

## 💾 Desain Database

Skema dirancang untuk kompatibilitas multi-platform (YouTube, TikTok, Instagram, Facebook, Reddit, X) tanpa perubahan skema besar — menggunakan `platform_id`, `platform_video_id`, dan `platform_channel_id`.

| Tabel | Fungsi |
|---|---|
| `platforms` | Lookup platform (youtube, tiktok, instagram, dll) |
| `channels` | Informasi channel lengkap |
| `trending_videos` | Metadata video lengkap + thumbnail + `raw_json` |
| `video_statistics` | Statistik + metrik engagement + velocity (tracking mode) |
| `collection_jobs` | Riwayat eksekusi koleksi (dengan kolom `mode`) |
| `trend_keywords` / `trend_discovery_jobs` / `trend_discovery_prompt_history` | Agent 0 (Discovery) |
| `knowledge_extraction_queue` / `video_transcripts` / `video_knowledge` / `video_knowledge_raw` | Agent 2 (Extraction) |
| `daily_api_usage` | Pelacakan quota YouTube harian |

`raw_json` (JSONB) menyimpan **seluruh response API asli** — data apa pun yang belum dipetakan hari ini tetap tersedia untuk agent AI di masa depan.

📖 Lihat `docs/database-design.md` untuk detail lengkap.

---

## 🛡️ Fitur Reliability (Baru)

| Fitur | Deskripsi |
|---|---|
| **Error granular** | Exception ter-tipe: `YouTubeQuotaExceededException`, `YouTubeApiKeyInvalidException`, `YouTubeTransientException` |
| **Retry policy** | 3x retry exponential backoff (1s → 2s → 4s) hanya untuk error transien |
| **Concurrency guard** | `CollectionCoordinator` memastikan discovery/tracking/manual collect tidak berjalan bersamaan |
| **Health check** | `GET /api/health` — cek koneksi database |
| **Data retention** | Cleanup otomatis `collection_jobs` & snapshot statistik lama (>30 hari) |
| **Quota-driven tracking** | Auto-switch ke Tracking Mode saat search quota habis |
| **Server-side filtering** | Filter date & language dipindah ke backend (pagination akurat) |

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