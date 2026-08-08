# Testing Guide — Trend Collector MVP

Panduan untuk menguji end-to-end aplikasi Trend Collector: dari menjalankan API, menguji 4 endpoint, hingga memverifikasi data di database PostgreSQL (Neon).

---

## 1. Prasyarat

| Item | Keterangan |
|---|---|
| .NET SDK | 10.0+ |
| Database | PostgreSQL (Neon atau lokal) — tabel otomatis dibuat saat app pertama dijalankan |
| YouTube Data API v3 key | Dari [Google Cloud Console](https://console.cloud.google.com/) |
| Tool HTTP | Swagger UI (bawaan) / curl / PowerShell |
| Tool DB (opsional) | psql atau Neon Console |

### Konfigurasi rahasia

Buat file `TrendCollector.Api/appsettings.Local.json` (file ini **tidak di-commit** — sudah di-gitignore):

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=YOUR_NEON_HOST;Port=5432;Database=neondb;Username=USER;Password=PASSWORD;SSL Mode=Require;Channel Binding=Require;Timeout=15"
  },
  "YouTube": {
    "ApiKey": "YOUR_YOUTUBE_API_KEY"
  }
}
```

> Alternatif: environment variables `ConnectionStrings__Postgres` dan `YouTube__ApiKey`.

---

## 2. Menjalankan Aplikasi

```bash
dotnet build
cd TrendCollector.Api
dotnet run
```

Harapan pada log startup:
```
info: TrendCollector.Api.Data.DbInitializer[0]
      Database schema applied successfully.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5075
```

- 5 tabel dibuat otomatis di database via `DbInitializer` (idempotent — aman dijalankan ulang)
- Buka Swagger UI: **http://localhost:5075/swagger**

---

## 3. Verifikasi Swagger

1. Buka `http://localhost:5075/swagger`
2. Pastikan 4 endpoint muncul:
   - `POST /api/trend/collect`
   - `GET /api/trend/jobs`
   - `GET /api/trend/videos`
   - `GET /api/trend/videos/{id}`
3. Pada `POST /api/trend/collect`, pastikan contoh request tampil:
   ```json
   { "keyword": "AI", "language": "id", "country": "ID", "maxResults": 20 }
   ```
   dan contoh response `CollectSummary` tersedia.

---

## 4. Uji Endpoint

### 4a. Collect — `POST /api/trend/collect`

Menggunakan PowerShell:

```powershell
$body = @{ keyword = "AI"; language = "id"; country = "ID"; maxResults = 20 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5075/api/trend/collect" `
  -Method Post -ContentType "application/json" -Body $body
```

Harapan (status 200):
```json
{
  "jobId": 1,
  "keyword": "AI",
  "country": "ID",
  "language": "id",
  "totalCollected": 20,
  "totalSaved": 18,
  "totalSkipped": 2,
  "startedAt": "2026-08-05T00:00:00Z",
  "finishedAt": "2026-08-05T00:00:01Z",
  "durationMs": 120340
}
```

### 4b. List Jobs — `GET /api/trend/jobs`

```powershell
Invoke-RestMethod -Uri "http://localhost:5075/api/trend/jobs"
```

Harapan: job terbaru di urutan pertama, `status = "completed"`, counter terisi.

### 4c. List Videos — `GET /api/trend/videos`

```powershell
Invoke-RestMethod -Uri "http://localhost:5075/api/trend/videos?language=id&limit=5"
```

Harapan: array video dengan `title`, `description`, `platformVideoId`, `thumbnail*Url`, `tags`, `duration`, `captionAvailable`, `rawJson`.

### 4d. Video Detail — `GET /api/trend/videos/{id}`

```powershell
Invoke-RestMethod -Uri "http://localhost:5075/api/trend/videos/1"
```

Harapan (status 200):
```json
{
  "video": { "id": 1, "title": "...", "rawJson": "...", "...": "..." },
  "statistics": {
    "views": 100000,
    "likes": 5000,
    "comments": 300,
    "favorites": 0,
    "engagementRate": 5.3,
    "likeRatio": 5.0,
    "commentRatio": 0.3,
    "viewPerDay": 25000,
    "videoAgeDays": 4,
    "capturedAt": "2026-08-05T00:00:01Z"
  }
}
```

### 4e. Uji Duplikat — Requirement #12

Ulangi **4a** dengan keyword yang sama:

Harapan: `totalSkipped` = jumlah video yang sudah tersimpan pada koleksi sebelumnya, `totalSaved` ≈ 0.

```
{
  "totalCollected": 20,
  "totalSaved": 0,
  "totalSkipped": 20
}
```

Karena `UNIQUE(platform_id, platform_video_id)`, video yang sudah ada di-skip — duplikat tidak pernah disimpan dua kali.

---

## 5. Uji Edge Case

| Kasus | Harapan |
|---|---|
| `POST collect` body kosong / tanpa `keyword` | `400 Bad Request` (validasi `[Required]`) |
| `maxResults: 999` | `400 Bad Request` (validasi `[Range(1,50)]`) |
| `GET /api/trend/videos/999999` | `404 Not Found` |
| `GET /api/trend/videos/abc` | `404` (route constraint `:long`) |
| API key YouTube salah | Job tercatat dengan `status: "failed"` + kolom `error` terisi di `GET /api/trend/jobs` |

---

## 6. Verifikasi Data di Neon (psql)

Konek via psql:

```bash
psql "postgresql://USER:PASSWORD@HOST/neondb?sslmode=require"
```

### 6a. Semua tabel ada

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public' ORDER BY table_name;
```

Harapan: `channels`, `collection_jobs`, `platforms`, `trending_videos`, `video_statistics`.

### 6b. Seed platform

```sql
SELECT * FROM platforms;
```

Harapan: `1 | youtube | YouTube`.

### 6c. Data video + raw JSON (requirement #9 & #11)

```sql
SELECT id, platform_video_id, title, duration, tags, caption_available,
       thumbnail_high_url, jsonb_typeof(raw_json) AS raw_type
FROM trending_videos
LIMIT 5;
```

Harapan: tags berupa array, `raw_type = 'object'` (JSON lengkap tersimpan).

### 6d. Metrik engagement

```sql
SELECT tv.id, tv.title, vs.views, vs.likes, vs.comments,
       vs.engagement_rate, vs.like_ratio, vs.comment_ratio,
       vs.view_per_day, vs.video_age_days
FROM video_statistics vs
JOIN trending_videos tv ON tv.id = vs.video_id
WHERE vs.views > 0
LIMIT 5;
```

Harapan: metrik terisi (tidak NULL) untuk video dengan views > 0.

### 6e. Riwayat snapshot

```sql
SELECT video_id, captured_at, views FROM video_statistics
WHERE video_id = 1 ORDER BY captured_at;
```

Ulangi flow 4a beberapa saat kemudian → baris baru muncul dengan `views` terbaru (snapshot historis untuk Viral Analyzer).

---

## 7. Troubleshooting

| Masalah | Solusi |
|---|---|
| `Connection refused` / timeout saat startup | Cek connection string di `appsettings.Local.json`; pastikan SSL Mode + Channel Binding benar |
| `Database schema applied successfully` tidak muncul | Cek file `SQL/schema.sql` tercopy di output (`bin/Debug/net10.0/SQL/`) |
| YouTube API error `quotaExceeded` | Kuota harian habis — tunggu reset (00:00 PST) atau gunakan key lain |
| Swagger blank / 404 | Pastikan `ASPNETCORE_ENVIRONMENT=Development` (launchSettings sudah mengaturnya) |
| `400` pada POST collect | Body tidak valid: keyword wajib, maxResults 1–50 |
| Video `totalSaved: 0` semua | Keyword pernah dikoleksi sebelumnya → normal (duplicate skip). Gunakan keyword berbeda untuk data baru |

---

## 8. Ringkasan Skenario Sukses

1. `dotnet run` → log schema applied ✅
2. Swagger UI menampilkan 4 endpoint + contoh ✅
3. `POST collect ("AI", id, ID, 20)` → summary dengan totalSaved > 0 ✅
4. `GET jobs` → job completed ✅
5. `GET videos` → data lengkap (metadata, thumbnail, tags) ✅
6. `GET videos/{id}` → statistik + metrik terisi ✅
7. Ulangi POST → totalSkipped bertambah (duplicate dictur) ✅
8. psql → raw_json JSON object + 5 tabel ✅

Dengan seluruh langkah di atas lolos, MVP Trend Collector dinyatakan **layak sebagai knowledge base** bagi agent AI masa depan (Transcript Collector, Viral Analyzer, dll).