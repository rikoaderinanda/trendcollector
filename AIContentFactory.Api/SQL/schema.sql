-- =====================================================================
-- AIContentFactory - Merged Database Schema (idempotent)
-- Applied automatically by DbInitializer on application startup.
-- Combines: Agent 0 (Trend Discovery), Agent 1 (Trend Collector),
--           Agent 2 (Knowledge Extraction)
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
    mode             TEXT NOT NULL DEFAULT 'Discovery',
    country          TEXT,
    language         TEXT,
    status           TEXT NOT NULL,
    total_collected  INT NOT NULL DEFAULT 0,
    total_saved      INT NOT NULL DEFAULT 0,
    total_skipped    INT NOT NULL DEFAULT 0,
    error            TEXT
);

-- Migration safety: adds the mode column when the table already existed
-- without it. IMPORTANT: this ALTER must run BEFORE any index on `mode`,
-- otherwise the batch aborts on old databases and the column never gets added.
ALTER TABLE collection_jobs
    ADD COLUMN IF NOT EXISTS mode TEXT NOT NULL DEFAULT 'Discovery';

CREATE INDEX IF NOT EXISTS idx_collection_jobs_started_at ON collection_jobs (started_at DESC);
CREATE INDEX IF NOT EXISTS idx_collection_jobs_keyword    ON collection_jobs (keyword);
CREATE INDEX IF NOT EXISTS idx_collection_jobs_status     ON collection_jobs (status);
CREATE INDEX IF NOT EXISTS idx_collection_jobs_mode       ON collection_jobs (mode);

-- ---------------------------------------------------------------------
-- 6. daily_api_usage
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS daily_api_usage (
    id          BIGSERIAL PRIMARY KEY,
    usage_date  DATE NOT NULL,
    endpoint    TEXT NOT NULL,
    call_count  INT NOT NULL DEFAULT 0,
    CONSTRAINT uq_daily_api_usage UNIQUE (usage_date, endpoint)
);

CREATE INDEX IF NOT EXISTS idx_daily_api_usage_usage_date ON daily_api_usage (usage_date);

-- ---------------------------------------------------------------------
-- 7. video_statistics velocity columns (Tracking Mode)
--    Added idempotently so existing installs are upgraded in place.
-- ---------------------------------------------------------------------
ALTER TABLE video_statistics
    ADD COLUMN IF NOT EXISTS views_per_hour       NUMERIC(14,4),
    ADD COLUMN IF NOT EXISTS like_velocity        NUMERIC(14,4),
    ADD COLUMN IF NOT EXISTS comment_velocity     NUMERIC(14,4),
    ADD COLUMN IF NOT EXISTS growth_score         NUMERIC(12,4),
    ADD COLUMN IF NOT EXISTS previous_snapshot_id BIGINT REFERENCES video_statistics (id);

CREATE INDEX IF NOT EXISTS idx_video_statistics_previous_snapshot_id ON video_statistics (previous_snapshot_id);
CREATE INDEX IF NOT EXISTS idx_video_statistics_growth_score ON video_statistics (growth_score DESC);

-- ---------------------------------------------------------------------
-- 8. trend_keywords (Agent 0: Trend Discovery)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trend_keywords (
    id                BIGSERIAL PRIMARY KEY,
    keyword           TEXT NOT NULL,
    niche             TEXT,
    country           TEXT NOT NULL DEFAULT 'Global',
    language          TEXT NOT NULL DEFAULT 'en',
    priority          INT NOT NULL DEFAULT 50,
    discovery_reason  TEXT,
    source            TEXT NOT NULL DEFAULT 'AI',
    status            TEXT NOT NULL DEFAULT 'active',
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_trend_keywords_keyword_country_language UNIQUE (keyword, country, language),
    CONSTRAINT chk_trend_keywords_priority CHECK (priority >= 1 AND priority <= 100)
);

CREATE INDEX IF NOT EXISTS idx_trend_keywords_niche      ON trend_keywords (niche);
CREATE INDEX IF NOT EXISTS idx_trend_keywords_country    ON trend_keywords (country);
CREATE INDEX IF NOT EXISTS idx_trend_keywords_language   ON trend_keywords (language);
CREATE INDEX IF NOT EXISTS idx_trend_keywords_priority   ON trend_keywords (priority DESC);
CREATE INDEX IF NOT EXISTS idx_trend_keywords_status     ON trend_keywords (status);
CREATE INDEX IF NOT EXISTS idx_trend_keywords_source     ON trend_keywords (source);

-- ---------------------------------------------------------------------
-- 9. trend_discovery_jobs (Agent 0)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trend_discovery_jobs (
    id              BIGSERIAL PRIMARY KEY,
    started_at      TIMESTAMPTZ NOT NULL,
    finished_at     TIMESTAMPTZ,
    duration_ms     BIGINT,
    status          TEXT NOT NULL,
    total_keywords  INT NOT NULL DEFAULT 0,
    error_message   TEXT,
    source          TEXT NOT NULL DEFAULT 'AI'
);

CREATE INDEX IF NOT EXISTS idx_trend_discovery_jobs_started_at ON trend_discovery_jobs (started_at DESC);
CREATE INDEX IF NOT EXISTS idx_trend_discovery_jobs_status     ON trend_discovery_jobs (status);

-- ---------------------------------------------------------------------
-- 10. trend_discovery_prompt_history (Agent 0)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trend_discovery_prompt_history (
    id                BIGSERIAL PRIMARY KEY,
    job_id            BIGINT REFERENCES trend_discovery_jobs (id),
    prompt            TEXT NOT NULL,
    ai_response       TEXT,
    provider          TEXT NOT NULL,
    model             TEXT,
    tokens_input      INT,
    tokens_output     INT,
    execution_time_ms BIGINT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_trend_discovery_prompt_history_job_id     ON trend_discovery_prompt_history (job_id);
CREATE INDEX IF NOT EXISTS idx_trend_discovery_prompt_history_created_at ON trend_discovery_prompt_history (created_at DESC);

-- ---------------------------------------------------------------------
-- 11. knowledge_extraction_queue (Agent 2: Knowledge Extraction)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS knowledge_extraction_queue (
    id              BIGSERIAL PRIMARY KEY,
    video_id        BIGINT NOT NULL REFERENCES trending_videos (id),
    status          TEXT NOT NULL DEFAULT 'Pending',
    priority        INT NOT NULL DEFAULT 0,
    retry_count     INT NOT NULL DEFAULT 0,
    started_at      TIMESTAMPTZ,
    finished_at     TIMESTAMPTZ,
    duration_ms     BIGINT,
    error_message   TEXT,
    next_retry_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_knowledge_extraction_queue_video UNIQUE (video_id)
);

CREATE INDEX IF NOT EXISTS idx_knowledge_extraction_status  ON knowledge_extraction_queue (status);
CREATE INDEX IF NOT EXISTS idx_knowledge_extraction_priority ON knowledge_extraction_queue (priority DESC);
CREATE INDEX IF NOT EXISTS idx_knowledge_extraction_video_id ON knowledge_extraction_queue (video_id);
CREATE INDEX IF NOT EXISTS idx_knowledge_extraction_created  ON knowledge_extraction_queue (created_at);

-- Migration safety: adds next_retry_at when the table already existed
-- without it. Must run BEFORE the index on next_retry_at (same reason as
-- the collection_jobs.mode migration above).
ALTER TABLE knowledge_extraction_queue
    ADD COLUMN IF NOT EXISTS next_retry_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS idx_knowledge_extraction_next_retry ON knowledge_extraction_queue (next_retry_at);

-- ---------------------------------------------------------------------
-- 12. video_transcripts (Agent 2)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS video_transcripts (
    id          BIGSERIAL PRIMARY KEY,
    video_id    BIGINT NOT NULL REFERENCES trending_videos (id),
    transcript  TEXT NOT NULL,
    language    TEXT,
    source      TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_video_transcripts_video UNIQUE (video_id)
);

ALTER TABLE video_transcripts
    ADD COLUMN IF NOT EXISTS transcript_score INT;

CREATE INDEX IF NOT EXISTS idx_video_transcripts_video_id ON video_transcripts (video_id);

-- ---------------------------------------------------------------------
-- 13. video_knowledge (Agent 2)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS video_knowledge (
    id                       BIGSERIAL PRIMARY KEY,
    video_id                 BIGINT NOT NULL REFERENCES trending_videos (id),
    summary                  TEXT,
    main_topic               TEXT,
    keywords                 TEXT[],
    target_audience          TEXT,
    tone                     TEXT,
    hook                     TEXT,
    content_structure        TEXT[],
    call_to_action           TEXT,
    important_points         TEXT[],
    learning_notes           TEXT[],
    interesting_facts        TEXT[],
    psychological_triggers   TEXT[],
    story_pattern            TEXT,
    content_type             TEXT,
    difficulty_level         TEXT,
    language                 TEXT,
    emotion                  TEXT,
    curiosity_score          INT,
    educational_value        INT,
    entertainment_value      INT,
    engagement_techniques    TEXT[],
    retention_strategy       TEXT,
    suggested_improvements   TEXT[],
    created_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_video_knowledge_video UNIQUE (video_id)
);

CREATE INDEX IF NOT EXISTS idx_video_knowledge_video_id    ON video_knowledge (video_id);
CREATE INDEX IF NOT EXISTS idx_video_knowledge_main_topic  ON video_knowledge (main_topic);
CREATE INDEX IF NOT EXISTS idx_video_knowledge_language    ON video_knowledge (language);
CREATE INDEX IF NOT EXISTS idx_video_knowledge_keywords    ON video_knowledge USING GIN (keywords);

-- ---------------------------------------------------------------------
-- 14. video_knowledge_raw (Agent 2)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS video_knowledge_raw (
    id                 BIGSERIAL PRIMARY KEY,
    video_id           BIGINT NOT NULL REFERENCES trending_videos (id),
    provider           TEXT,
    model              TEXT,
    prompt             TEXT,
    response           TEXT,
    execution_time_ms  BIGINT,
    tokens_input       INT,
    tokens_output      INT,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_video_knowledge_raw_video_id ON video_knowledge_raw (video_id);

-- ---------------------------------------------------------------------
-- 15. viral_analysis_runs (Agent 3: Viral Analyzer)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS viral_analysis_runs (
    id                        BIGSERIAL PRIMARY KEY,
    started_at                TIMESTAMPTZ NOT NULL,
    finished_at               TIMESTAMPTZ,
    status                    TEXT NOT NULL DEFAULT 'Running',
    niche                     TEXT,
    trend_keyword             TEXT,
    date_from                 DATE,
    date_to                   DATE,
    total_candidates          INT NOT NULL DEFAULT 0,
    eligible_candidates       INT NOT NULL DEFAULT 0,
    opportunities_generated   INT NOT NULL DEFAULT 0,
    recommended_opportunity_id BIGINT,
    trend_summary             TEXT,
    market_observation        TEXT,
    confidence_score          NUMERIC(5,2),
    analysis_version          TEXT,
    error_message             TEXT,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_viral_analysis_runs_recommendation UNIQUE (recommended_opportunity_id)
);

CREATE INDEX IF NOT EXISTS idx_viral_analysis_runs_status     ON viral_analysis_runs (status);
CREATE INDEX IF NOT EXISTS idx_viral_analysis_runs_started_at ON viral_analysis_runs (started_at DESC);
CREATE INDEX IF NOT EXISTS idx_viral_analysis_runs_niche      ON viral_analysis_runs (niche);
CREATE INDEX IF NOT EXISTS idx_viral_analysis_runs_keyword    ON viral_analysis_runs (trend_keyword);

-- ---------------------------------------------------------------------
-- 16. viral_analysis_winning_patterns (Agent 3)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS viral_analysis_winning_patterns (
    id                      BIGSERIAL PRIMARY KEY,
    analysis_run_id         BIGINT NOT NULL REFERENCES viral_analysis_runs (id) ON DELETE CASCADE,
    pattern_type            TEXT NOT NULL,
    pattern_name            TEXT NOT NULL,
    description             TEXT NOT NULL,
    frequency               INT NOT NULL DEFAULT 0,
    supporting_video_count  INT NOT NULL DEFAULT 0,
    average_momentum_score  NUMERIC(6,2),
    evidence                TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_viral_patterns_run_id    ON viral_analysis_winning_patterns (analysis_run_id);
CREATE INDEX IF NOT EXISTS idx_viral_patterns_type      ON viral_analysis_winning_patterns (pattern_type);
CREATE INDEX IF NOT EXISTS idx_viral_patterns_name      ON viral_analysis_winning_patterns (pattern_name);

-- ---------------------------------------------------------------------
-- 17. viral_analysis_content_opportunities (Agent 3)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS viral_analysis_content_opportunities (
    id                       BIGSERIAL PRIMARY KEY,
    analysis_run_id          BIGINT NOT NULL REFERENCES viral_analysis_runs (id) ON DELETE CASCADE,
    rank                     INT NOT NULL DEFAULT 0,
    topic                    TEXT NOT NULL,
    angle                    TEXT NOT NULL,
    target_audience          TEXT,
    hook                     TEXT NOT NULL,
    format                   TEXT NOT NULL,
    structure                TEXT[],
    emotion                  TEXT,
    psychological_trigger    TEXT,
    why_now                  TEXT NOT NULL,
    content_gap              TEXT,
    differentiation_strategy TEXT,
    call_to_action           TEXT,
    opportunity_score        NUMERIC(6,2) NOT NULL DEFAULT 0,
    confidence_score         NUMERIC(6,2) NOT NULL DEFAULT 0,
    risk_level               TEXT NOT NULL DEFAULT 'Medium',
    supporting_video_ids     BIGINT[],
    evidence                 TEXT,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_viral_opportunities_run_rank UNIQUE (analysis_run_id, rank)
);

-- Migration safety: adds call_to_action when the table already existed
-- without it (same pattern as collection_jobs.mode).
ALTER TABLE viral_analysis_content_opportunities
    ADD COLUMN IF NOT EXISTS call_to_action TEXT;

CREATE INDEX IF NOT EXISTS idx_viral_opportunities_run_id ON viral_analysis_content_opportunities (analysis_run_id);
CREATE INDEX IF NOT EXISTS idx_viral_opportunities_score  ON viral_analysis_content_opportunities (opportunity_score DESC);

-- ---------------------------------------------------------------------
-- 18. viral_analysis_prompt_history (Agent 3)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS viral_analysis_prompt_history (
    id                BIGSERIAL PRIMARY KEY,
    analysis_run_id   BIGINT NOT NULL REFERENCES viral_analysis_runs (id) ON DELETE CASCADE,
    prompt            TEXT NOT NULL,
    ai_response       TEXT NOT NULL,
    provider          TEXT NOT NULL,
    model             TEXT,
    temperature       NUMERIC(4,2),
    tokens_input      INT,
    tokens_output     INT,
    execution_time_ms BIGINT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_viral_prompt_history_run_id     ON viral_analysis_prompt_history (analysis_run_id);
CREATE INDEX IF NOT EXISTS idx_viral_prompt_history_created_at ON viral_analysis_prompt_history (created_at DESC);

-- ---------------------------------------------------------------------
-- 19. viral_analysis_candidate_snapshots (Agent 3)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS viral_analysis_candidate_snapshots (
    id                      BIGSERIAL PRIMARY KEY,
    analysis_run_id         BIGINT NOT NULL REFERENCES viral_analysis_runs (id) ON DELETE CASCADE,
    video_id                BIGINT NOT NULL REFERENCES trending_videos (id),
    is_eligible             BOOLEAN NOT NULL DEFAULT FALSE,
    skip_reason             TEXT,
    performance_summary_json JSONB,
    pattern_summary_json    JSONB,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_viral_candidates_run_id  ON viral_analysis_candidate_snapshots (analysis_run_id);
CREATE INDEX IF NOT EXISTS idx_viral_candidates_video_id ON viral_analysis_candidate_snapshots (video_id);
CREATE INDEX IF NOT EXISTS idx_viral_candidates_eligible ON viral_analysis_candidate_snapshots (analysis_run_id, is_eligible);

-- ---------------------------------------------------------------------
-- 20. data_processing_failures (Shared Data Quality / Recovery Framework)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS data_processing_failures (
    id                  BIGSERIAL PRIMARY KEY,
    agent_name          TEXT NOT NULL,
    entity_type         TEXT NOT NULL,
    entity_id           BIGINT NOT NULL,
    operation           TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'Retryable',
    failure_type        TEXT NOT NULL DEFAULT 'Transient',
    failure_reason      TEXT,
    exception_type      TEXT,
    retry_count         INT NOT NULL DEFAULT 0,
    max_retry_attempts  INT NOT NULL DEFAULT 5,
    first_attempt_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_attempt_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    next_retry_at       TIMESTAMPTZ,
    resolved_at         TIMESTAMPTZ,
    resolution_type     TEXT,
    raw_reference       TEXT,
    metadata_json       JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_data_failures_agent       ON data_processing_failures (agent_name);
CREATE INDEX IF NOT EXISTS idx_data_failures_entity      ON data_processing_failures (entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_data_failures_status      ON data_processing_failures (status);
CREATE INDEX IF NOT EXISTS idx_data_failures_next_retry  ON data_processing_failures (next_retry_at);
CREATE INDEX IF NOT EXISTS idx_data_failures_type        ON data_processing_failures (failure_type);
