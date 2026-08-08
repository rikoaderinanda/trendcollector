# Database Design — Trend Collector MVP

## 1. Prinsip Desain

| Prinsip | Penerapan |
|---|---|
| Multi-platform | `platform_id`, `platform_video_id`, `platform_channel_id` — bukan hardcode `youtube_*` |
| Tidak ada data dibuang | Setiap response API asli disimpan utuh di kolom `raw_json` (JSONB) |
| Snapshot metrik | Metrik engagement dihitung & disimpan saat koleksi (snapshot, bukan query live) |
| Relasi kanonik | `trending_videos.channel_id` → `channels.id` (FK) |
| Idempotent | Semua tabel punya unique constraint → duplicate-safe |

## 2. Tabel

### 2.1 `platforms` — Register platform

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | SERIAL | PK | |
| `code` | TEXT | UNIQUE NOT NULL | `youtube`, `tiktok`, ... |
| `name` | TEXT | NOT NULL | Nama display |

Seed: `('youtube', 'YouTube')`

### 2.2 `channels` — Informasi channel

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `platform_id` | INT | FK → platforms.id, NOT NULL | |
| `platform_channel_id` | TEXT | NOT NULL | ID asli di platform (`UC...`) |
| `name` | TEXT | | |
| `country` | TEXT | | 2-huruf (ISO 3166) |
| `subscriber_count` | BIGINT | | |
| `video_count` | INT | | |
| `total_views` | BIGINT | | |
| `published_at` | TIMESTAMPTZ | | Tanggal channel dibuat |
| `custom_url` | TEXT | | `@username` |
| `raw_json` | JSONB | | Response `channels.list` penuh |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |

Unique: `(platform_id, platform_channel_id)`

### 2.3 `trending_videos` — Metadata video lengkap

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `platform_id` | INT | FK → platforms.id, NOT NULL | |
| `platform_video_id` | TEXT | NOT NULL | ID video asli (`dQw4w9WgXcQ`) |
| `channel_id` | BIGINT | FK → channels.id, NULL | |
| `title` | TEXT | | |
| `description` | TEXT | | |
| `url` | TEXT | | URL kanonik platform |
| `published_at` | TIMESTAMPTZ | | |
| `duration` | TEXT | | ISO 8601 (`PT12M34S`) |
| `category` | TEXT | | Nama kategori |
| `tags` | TEXT[] | | Array tag |
| `language` | TEXT | | `id`, `en`, ... |
| `caption_available` | BOOLEAN | | |
| `definition` | TEXT | | `hd`, `sd`, ... |
| `dimension` | TEXT | | `2d` / `3d` |
| `projection` | TEXT | | `rectangular`, `360`, ... |
| `thumbnail_default_url` | TEXT | | |
| `thumbnail_medium_url` | TEXT | | |
| `thumbnail_high_url` | TEXT | | |
| `thumbnail_standard_url` | TEXT | | |
| `thumbnail_maxres_url` | TEXT | | |
| `processed_at` | TIMESTAMPTZ | | Kapan video pertama dikoleksi |
| `raw_json` | JSONB | | Response `videos.list` penuh |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |

Unique: `(platform_id, platform_video_id)`
Index: `platform_id`, `channel_id`, `published_at DESC`, GIN `tags`, `language`

### 2.4 `video_statistics` — Statistik + metrik turunan

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `video_id` | BIGINT | FK → trending_videos.id, NOT NULL | |
| `views` | BIGINT | | |
| `likes` | BIGINT | | |
| `comments` | BIGINT | | |
| `favorites` | BIGINT | | |
| `engagement_rate` | NUMERIC(12,4) | | (likes+comments)/views*100 |
| `like_ratio` | NUMERIC(12,4) | | likes/views*100 |
| `comment_ratio` | NUMERIC(12,4) | | comments/views*100 |
| `view_per_day` | NUMERIC(14,4) | | views/video_age_days |
| `video_age_days` | INT | | max(1, days since published) |
| `captured_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | Waktu snapshot |

Unique: `(video_id, captured_at)` — riwayat snapshot
Index: `video_id`, `captured_at`

Formula (guard `views = 0` → metrik 0; likes/comments NULL → dianggap 0 untuk hitung tapi disimpan NULL di kolom asal):
```
video_age_days  = max(1, (captured_at - published_at).days)
engagement_rate = (likes + comments) / views * 100
like_ratio      = likes / views * 100
comment_ratio   = comments / views * 100
view_per_day    = views / video_age_days
```

### 2.5 `collection_jobs` — Riwayat eksekusi koleksi

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `started_at` | TIMESTAMPTZ | NOT NULL | |
| `finished_at` | TIMESTAMPTZ | | NULL saat running |
| `duration_ms` | BIGINT | | |
| `keyword` | TEXT | NOT NULL | |
| `mode` | TEXT | NOT NULL DEFAULT `'Discovery'` | `Discovery` / `Tracking` |
| `country` | TEXT | | |
| `language` | TEXT | | |
| `status` | TEXT | NOT NULL | `running` / `completed` / `failed` |
| `total_collected` | INT | NOT NULL DEFAULT 0 | |
| `total_saved` | INT | NOT NULL DEFAULT 0 | |
| `total_skipped` | INT | NOT NULL DEFAULT 0 | Duplikat / gagal validasi |
| `error` | TEXT | | |

Index: `started_at DESC`, `keyword`, `status`, `mode`

## 3. Relasi (ER)

```
platforms 1---N channels ---N trending_videos 1---N video_statistics
    +----------+--------+
collection_jobs (berdiri sendiri - log eksekusi)
```

## 4. Mapping Ke Requirement

| Requirement | Kolom |
|---|---|
| Search | `collection_jobs.keyword` + `trending_videos.*` |
| Metadata lengkap | `trending_videos.title/description/category/tags/language/definition/dimension/projection` |
| Statistik | `video_statistics.views/likes/comments/favorites` |
| Channel | `channels.*` |
| Thumbnails | `trending_videos.thumbnail_*_url` (5 ukuran) |
| Tags | `trending_videos.tags (TEXT[])` |
| Duration | `trending_videos.duration (ISO 8601)` |
| Caption | `trending_videos.caption_available` |
| Raw JSON | `raw_json` (channels & trending_videos) |
| Duplicate ignore | `UNIQUE(platform_id, platform_video_id)` + `total_skipped` |
| Platform-agnostic | `platform_id`, `platform_video_id`, `platform_channel_id` |

## 5. Database Bersama Multi-Agent

Semua agent berbagi **satu database** (`neondb`) melalui connection string yang identik di `appsettings.Local.json` masing-masing:

| Agent | Project | Tabel yang dibuat (prefix unik — tidak conflict) |
|---|---|---|
| Agent 0 — Trend Discovery | `TrendDiscovery.Api` | `trend_keywords`, `trend_discovery_jobs`, `trend_discovery_prompt_history` |
| Agent 1 — Trend Collector | `TrendCollector.Api` | `platforms`, `channels`, `trending_videos`, `video_statistics`, `collection_jobs` |
| Agent 2 — Knowledge Extraction | `KnowledgeExtraction.Api` | `knowledge_extraction_queue`, `video_transcripts`, `video_knowledge`, `video_knowledge_raw` |

> **Urutan startup**: Agent 1 harus lebih dulu (membuat `trending_videos`) karena tabel Agent 2 memiliki FK → `trending_videos(id)`.

## 6. Tabel Agent 2 — Knowledge Extraction

### 6.1 `knowledge_extraction_queue` — Antrian ekstraksi

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `video_id` | BIGINT | FK → trending_videos.id, UNIQUE NOT NULL | Satu queue per video |
| `status` | TEXT | NOT NULL DEFAULT 'Pending' | `Pending`/`Running`/`Completed`/`Failed`/`TranscriptUnavailable` |
| `priority` | INT | NOT NULL DEFAULT 0 | Lebih tinggi diproses dulu |
| `retry_count` | INT | NOT NULL DEFAULT 0 | Maks 3 (config) |
| `next_retry_at` | TIMESTAMPTZ | | Exponential backoff: 30s → 60s → 120s |
| `started_at` | TIMESTAMPTZ | | |
| `finished_at` | TIMESTAMPTZ | | |
| `duration_ms` | BIGINT | | |
| `error_message` | TEXT | | |
| `created_at` / `updated_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |

### 6.2 `video_transcripts` — Transcript video

| Kolom | Tipe | Constraints | Keterangan |
|---|---|---|---|
| `id` | BIGSERIAL | PK | |
| `video_id` | BIGINT | FK → trending_videos.id, UNIQUE NOT NULL | |
| `transcript` | TEXT | NOT NULL | Teks lengkap subtitle |
| `language` | TEXT | | `en`, `id`, ... |
| `source` | TEXT | | `youtube_captions` |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT now() | |

### 6.3 `video_knowledge` — Knowledge terstruktur (asset reusable AI)

| Kolom | Tipe | Keterangan |
|---|---|---|
| `id` | BIGSERIAL | PK |
| `video_id` | BIGINT | FK → trending_videos.id, UNIQUE (one-to-one) |
| `summary`, `main_topic` | TEXT | Ringkasan & topik utama |
| `keywords` | TEXT[] | SEO keywords |
| `target_audience`, `tone`, `hook` | TEXT | |
| `content_structure` | TEXT[] | Alur section video |
| `call_to_action`, `important_points` | TEXT / TEXT[] | |
| `learning_notes`, `interesting_facts` | TEXT[] | |
| `psychological_triggers` | TEXT[] | `Curiosity`, `FOMO`, ... |
| `story_pattern`, `content_type`, `difficulty_level`, `language` | TEXT | |
| `emotion` | TEXT | Emosi dominan |
| `curiosity_score`, `educational_value`, `entertainment_value` | INT | Skor 1-100 |
| `engagement_techniques` | TEXT[] | |
| `retention_strategy`, `suggested_improvements` | TEXT / TEXT[] | |
| `created_at` / `updated_at` | TIMESTAMPTZ | |

### 6.4 `video_knowledge_raw` — Audit trail AI (tidak pernah dibuang)

| Kolom | Tipe | Keterangan |
|---|---|---|
| `id` | BIGSERIAL | PK |
| `video_id` | BIGINT | FK → trending_videos.id |
| `provider`, `model` | TEXT | `OpenAICompatible`, `deepseek-chat` |
| `prompt` | TEXT | Prompt lengkap yang dikirim |
| `response` | TEXT | Raw JSON dari AI |
| `execution_time_ms`, `tokens_input`, `tokens_output` | BIGINT / INT | Telemetry |
| `created_at` | TIMESTAMPTZ | |

## 7. Relasi ER — v2 (dengan Agent 2)

```
platforms 1---N channels ---N trending_videos 1---N video_statistics
                                     │
                                     │ (video_id FK)
                                     ├── 1─1 video_transcripts
                                     ├── 1─1 video_knowledge
                                     └── 1─N video_knowledge_raw
                                     │
                                     └── 1─1 knowledge_extraction_queue
collection_jobs (berdiri sendiri - log eksekusi)
trend_keywords / trend_discovery_jobs / trend_discovery_prompt_history (Agent 0 - terpisah)
```

## 8. Catatan Untuk Future AI Agents

- **Transcript Collector**: `trending_videos.id` + `caption_available` → tahu video mana yang punya caption
- **Viral Analyzer**: `video_statistics` (snapshot historis + metrik) → deteksi growth
- **Thumbnail Analyzer**: `thumbnail_*_url` kolom langsung + `raw_json`
- **Audience Analyzer**: `language`, `country` (dari job) + `channel.country`
- **Script/Storyboard/Prompt Generator**: `description`, `tags`, `raw_json`
- **Agent 3+ (Content Generator)**: konsumsi `video_knowledge` — JANGAN baca raw metadata langsung
- **Agent 3+ (Virality Predictor)**: konsumsi `video_knowledge` (curiosity_score, psychological_triggers) + `video_statistics`
- **Agent 3+ (Script Writer)**: konsumsi `video_knowledge` (contentStructure, hook, storyPattern, learningNotes)
- **Debug/audit**: `video_knowledge_raw` menyimpan prompt + response AI mentah untuk tracing
