-- =====================================================================
-- Trend Collector - Database Schema (idempotent)
-- Applied automatically by DbInitializer on application startup.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. platforms
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS platforms (
    id          SERIAL PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL
);

-- Seed the YouTube platform (always a no-op after the first run).
INSERT INTO platforms (code, name)
VALUES ('youtube', 'YouTube')
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------
-- 2. channels
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS channels (
    id                   BIGSERIAL PRIMARY KEY,
    platform_id          INT NOT NULL REFERENCES platforms (id),
    platform_channel_id  TEXT NOT NULL,
    name                 TEXT,
    country              TEXT,
    subscriber_count     BIGINT,
    video_count          INT,
    total_views          BIGINT,
    published_at         TIMESTAMPTZ,
    custom_url           TEXT,
    raw_json             JSONB,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_channels_platform_channel UNIQUE (platform_id, platform_channel_id)
);

CREATE INDEX IF NOT EXISTS idx_channels_platform_id ON channels (platform_id);

-- ---------------------------------------------------------------------
-- 3. trending_videos
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trending_videos (
    id                      BIGSERIAL PRIMARY KEY,
    platform_id             INT NOT NULL REFERENCES platforms (id),
    platform_video_id       TEXT NOT NULL,
    channel_id              BIGINT REFERENCES channels (id),
    title                   TEXT,
    description             TEXT,
    url                     TEXT,
    published_at            TIMESTAMPTZ,
    duration                TEXT,
    category                TEXT,
    tags                    TEXT[],
    language                TEXT,
    caption_available       BOOLEAN,
    definition              TEXT,
    dimension               TEXT,
    projection              TEXT,
    thumbnail_default_url   TEXT,
    thumbnail_medium_url    TEXT,
    thumbnail_high_url      TEXT,
    thumbnail_standard_url  TEXT,
    thumbnail_maxres_url    TEXT,
    processed_at            TIMESTAMPTZ,
    raw_json                JSONB,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_trending_videos_platform_video UNIQUE (platform_id, platform_video_id)
);

CREATE INDEX IF NOT EXISTS idx_trending_videos_platform_id  ON trending_videos (platform_id);
CREATE INDEX IF NOT EXISTS idx_trending_videos_channel_id   ON trending_videos (channel_id);
CREATE INDEX IF NOT EXISTS idx_trending_videos_published_at ON trending_videos (published_at DESC);
CREATE INDEX IF NOT EXISTS idx_trending_videos_language     ON trending_videos (language);
CREATE INDEX IF NOT EXISTS idx_trending_videos_tags         ON trending_videos USING GIN (tags);

-- ---------------------------------------------------------------------
-- 4. video_statistics
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS video_statistics (
    id              BIGSERIAL PRIMARY KEY,
    video_id        BIGINT NOT NULL REFERENCES trending_videos (id),
    views           BIGINT,
    likes           BIGINT,
    comments        BIGINT,
    favorites       BIGINT,
    engagement_rate NUMERIC(12,4),
    like_ratio      NUMERIC(12,4),
    comment_ratio   NUMERIC(12,4),
    view_per_day    NUMERIC(14,4),
    video_age_days  INT,
    captured_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_video_statistics_video_captured UNIQUE (video_id, captured_at)
);

CREATE INDEX IF NOT EXISTS idx_video_statistics_video_id ON video_statistics (video_id);

-- ---------------------------------------------------------------------
-- 5. collection_jobs
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS collection_jobs (
    id               BIGSERIAL PRIMARY KEY,
    started_at       TIMESTAMPTZ NOT NULL,
    finished_at      TIMESTAMPTZ,
    duration_ms      BIGINT,
    keyword          TEXT NOT NULL,
    country          TEXT,
    language         TEXT,
    status           TEXT NOT NULL,
    total_collected  INT NOT NULL DEFAULT 0,
    total_saved      INT NOT NULL DEFAULT 0,
    total_skipped    INT NOT NULL DEFAULT 0,
    error            TEXT
);

CREATE INDEX IF NOT EXISTS idx_collection_jobs_started_at ON collection_jobs (started_at DESC);
CREATE INDEX IF NOT EXISTS idx_collection_jobs_keyword    ON collection_jobs (keyword);
CREATE INDEX IF NOT EXISTS idx_collection_jobs_status     ON collection_jobs (status);