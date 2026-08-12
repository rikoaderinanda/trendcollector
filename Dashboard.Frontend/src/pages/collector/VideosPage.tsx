import { useEffect, useState, type ChangeEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useVideos } from '../../hooks/useCollector';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { TrendingVideo } from '../../types/collector';
import type { SortMetric, VideoFilters } from '../../api/collectorApi';
import { formatDate, formatYouTubeDuration } from '../../utils/formatters';

function formatCompact(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toString();
}

const SORT_OPTIONS: { value: SortMetric; label: string }[] = [
  { value: 'views', label: 'Views' },
  { value: 'likes', label: 'Likes' },
  { value: 'comments', label: 'Comments' },
  { value: 'favorites', label: 'Favorites' },
  { value: 'engagement_rate', label: 'Engagement Rate' },
  { value: 'view_per_day', label: 'View / Day' },
  { value: 'video_age_days', label: 'Video Age' },
  { value: 'captured_at', label: 'Captured At' },
  { value: 'growth_score', label: 'Growth Score' },
  { value: 'views_per_hour', label: 'Views / Hour' },
  { value: 'like_velocity', label: 'Like Velocity' },
  { value: 'comment_velocity', label: 'Comment Velocity' },
  { value: 'published_at', label: 'Published At' },
];

interface DraftFilters {
  minViews: string;
  maxViews: string;
  minLikes: string;
  maxLikes: string;
  minComments: string;
  maxComments: string;
  minFavorites: string;
  maxFavorites: string;
  minEngagementRate: string;
  maxEngagementRate: string;
  minViewPerDay: string;
  maxViewPerDay: string;
  minVideoAgeDays: string;
  maxVideoAgeDays: string;
  minViewsPerHour: string;
  maxViewsPerHour: string;
  minLikeVelocity: string;
  maxLikeVelocity: string;
  minCommentVelocity: string;
  maxCommentVelocity: string;
  minGrowthScore: string;
  maxGrowthScore: string;
  capturedAfter: string;
  capturedBefore: string;
}

const EMPTY_DRAFT: DraftFilters = {
  minViews: '',
  maxViews: '',
  minLikes: '',
  maxLikes: '',
  minComments: '',
  maxComments: '',
  minFavorites: '',
  maxFavorites: '',
  minEngagementRate: '',
  maxEngagementRate: '',
  minViewPerDay: '',
  maxViewPerDay: '',
  minVideoAgeDays: '',
  maxVideoAgeDays: '',
  minViewsPerHour: '',
  maxViewsPerHour: '',
  minLikeVelocity: '',
  maxLikeVelocity: '',
  minCommentVelocity: '',
  maxCommentVelocity: '',
  minGrowthScore: '',
  maxGrowthScore: '',
  capturedAfter: '',
  capturedBefore: '',
};

interface RangeFilterDef {
  minKey: keyof DraftFilters;
  maxKey: keyof DraftFilters;
  label: string;
}

const RANGE_FILTERS: RangeFilterDef[] = [
  { minKey: 'minViews', maxKey: 'maxViews', label: 'Views' },
  { minKey: 'minLikes', maxKey: 'maxLikes', label: 'Likes' },
  { minKey: 'minComments', maxKey: 'maxComments', label: 'Comments' },
  { minKey: 'minFavorites', maxKey: 'maxFavorites', label: 'Favorites' },
  { minKey: 'minEngagementRate', maxKey: 'maxEngagementRate', label: 'Engagement Rate (%)' },
  { minKey: 'minViewPerDay', maxKey: 'maxViewPerDay', label: 'View / Day' },
  { minKey: 'minVideoAgeDays', maxKey: 'maxVideoAgeDays', label: 'Video Age (days)' },
  { minKey: 'minViewsPerHour', maxKey: 'maxViewsPerHour', label: 'Views / Hour' },
  { minKey: 'minLikeVelocity', maxKey: 'maxLikeVelocity', label: 'Like Velocity' },
  { minKey: 'minCommentVelocity', maxKey: 'maxCommentVelocity', label: 'Comment Velocity' },
  { minKey: 'minGrowthScore', maxKey: 'maxGrowthScore', label: 'Growth Score' },
];

const STAT_KEYS: (keyof VideoFilters)[] = [
  'minViews', 'maxViews', 'minLikes', 'maxLikes', 'minComments', 'maxComments',
  'minFavorites', 'maxFavorites', 'minEngagementRate', 'maxEngagementRate',
  'minViewPerDay', 'maxViewPerDay', 'minVideoAgeDays', 'maxVideoAgeDays',
  'minViewsPerHour', 'maxViewsPerHour', 'minLikeVelocity', 'maxLikeVelocity',
  'minCommentVelocity', 'maxCommentVelocity', 'minGrowthScore', 'maxGrowthScore',
  'capturedAfter', 'capturedBefore',
];

function parseNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

function buildStatFilters(draft: DraftFilters): VideoFilters {
  const filters: VideoFilters = {};
  const set = (key: keyof VideoFilters, value: string) => {
    const n = parseNumber(value);
    if (n !== undefined) {
      (filters as Record<string, unknown>)[key] = n;
    }
  };

  set('minViews', draft.minViews);
  set('maxViews', draft.maxViews);
  set('minLikes', draft.minLikes);
  set('maxLikes', draft.maxLikes);
  set('minComments', draft.minComments);
  set('maxComments', draft.maxComments);
  set('minFavorites', draft.minFavorites);
  set('maxFavorites', draft.maxFavorites);
  set('minEngagementRate', draft.minEngagementRate);
  set('maxEngagementRate', draft.maxEngagementRate);
  set('minViewPerDay', draft.minViewPerDay);
  set('maxViewPerDay', draft.maxViewPerDay);
  set('minVideoAgeDays', draft.minVideoAgeDays);
  set('maxVideoAgeDays', draft.maxVideoAgeDays);
  set('minViewsPerHour', draft.minViewsPerHour);
  set('maxViewsPerHour', draft.maxViewsPerHour);
  set('minLikeVelocity', draft.minLikeVelocity);
  set('maxLikeVelocity', draft.maxLikeVelocity);
  set('minCommentVelocity', draft.minCommentVelocity);
  set('maxCommentVelocity', draft.maxCommentVelocity);
  set('minGrowthScore', draft.minGrowthScore);
  set('maxGrowthScore', draft.maxGrowthScore);

  if (draft.capturedAfter.trim()) filters.capturedAfter = draft.capturedAfter.trim();
  if (draft.capturedBefore.trim()) filters.capturedBefore = draft.capturedBefore.trim();

  return filters;
}

function statFilterCount(filters: VideoFilters): number {
  return STAT_KEYS.filter((k) => filters[k] !== undefined).length;
}

function VideoCard({ video }: { video: TrendingVideo }) {
  return (
    <Link
      to={`/collector/videos/${video.id}`}
      className="card overflow-hidden hover:shadow-md transition-shadow group"
    >
      <div className="relative aspect-video bg-gray-200">
        {video.thumbnailHighUrl ? (
          <img
            src={video.thumbnailHighUrl}
            alt={video.title ?? 'Video thumbnail'}
            className="w-full h-full object-cover group-hover:opacity-90 transition-opacity"
            loading="lazy"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-4xl text-gray-400">🎬</div>
        )}
        {video.duration && (
          <span className="absolute bottom-2 right-2 bg-black/80 text-white text-xs font-medium px-1.5 py-0.5 rounded">
            {formatYouTubeDuration(video.duration)}
          </span>
        )}
        {video.language && (
          <span className="absolute top-2 left-2 bg-primary-600 text-white text-xs font-medium px-2 py-0.5 rounded">
            {video.language.toUpperCase()}
          </span>
        )}
      </div>
      <div className="p-4">
        <h3 className="text-sm font-semibold text-gray-900 line-clamp-2">{video.title ?? 'Untitled video'}</h3>
        {video.category && <p className="text-xs text-gray-500 mt-1">{video.category}</p>}

        {/* Core statistics snapshot (from GET /api/trend/videos) */}
        {(video.views != null || video.likes != null || video.comments != null || video.favorites != null) && (
          <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-gray-600">
            {video.views != null && <span title="Views">👁 {formatCompact(video.views)}</span>}
            {video.likes != null && <span title="Likes">👍 {formatCompact(video.likes)}</span>}
            {video.comments != null && <span title="Comments">💬 {formatCompact(video.comments)}</span>}
            {video.favorites != null && <span title="Favorites">⭐ {formatCompact(video.favorites)}</span>}
            {video.engagementRate != null && (
              <span className="text-emerald-600" title="Engagement rate">▲ {video.engagementRate.toFixed(1)}%</span>
            )}
          </div>
        )}

        {/* Velocity / growth metrics (tracking mode) */}
        {(video.viewPerDay != null || video.videoAgeDays != null || video.growthScore != null) && (
          <div className="flex flex-wrap items-center gap-3 mt-1 text-xs text-gray-500">
            {video.viewPerDay != null && <span title="Views per day">📈 {formatCompact(video.viewPerDay)}/day</span>}
            {video.videoAgeDays != null && <span title="Video age">⏳ {Math.round(video.videoAgeDays)}d</span>}
            {video.growthScore != null && <span title="Growth score">🚀 {video.growthScore.toFixed(1)}</span>}
          </div>
        )}

        <div className="flex items-center justify-between mt-3 text-xs text-gray-500">
          <span>
            {video.views != null && video.statisticsCapturedAt
              ? `Updated ${formatDate(video.statisticsCapturedAt)}`
              : video.processedAt
                ? formatDate(video.processedAt)
                : '-'}
          </span>
          <span className="text-primary-600">Details →</span>
        </div>
      </div>
    </Link>
  );
}

export default function VideosPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const dateParam = searchParams.get('date') ?? '';
  const [dateFilter, setDateFilter] = useState(dateParam);
  const [language, setLanguage] = useState('');
  const [limit, setLimit] = useState(20);
  const [offset, setOffset] = useState(0);

  // Sorting state
  const [sortBy, setSortBy] = useState<SortMetric | undefined>(undefined);
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');

  // Statistics filter state: draft (input fields) vs applied (sent to API)
  const [showFilters, setShowFilters] = useState(false);
  const [draftFilters, setDraftFilters] = useState<DraftFilters>(EMPTY_DRAFT);
  const [appliedStatFilters, setAppliedStatFilters] = useState<VideoFilters>({});

  // Keep the local date filter state in sync with the URL query param
  // (e.g. when navigating from the Workflow page while already on this page).
  useEffect(() => {
    setDateFilter(dateParam);
  }, [dateParam]);

  // Pass the date filter to the backend so pagination happens server-side.
  // Reset to the first page whenever the date filter changes.
  useEffect(() => {
    setOffset(0);
  }, [dateFilter]);

  const videosQuery = useVideos({
    language,
    date: dateFilter || undefined,
    limit: Math.min(limit, 100),
    offset,
    sortBy,
    sortDirection,
    ...appliedStatFilters,
  });

  const handleDateChange = (value: string) => {
    setDateFilter(value);
    setSearchParams(value ? { date: value } : {});
  };

  const clearDateFilter = () => {
    setDateFilter('');
    setSearchParams({});
  };

  const updateDraft =
    (key: keyof DraftFilters) =>
    (e: ChangeEvent<HTMLInputElement>) => {
      setDraftFilters((d) => ({ ...d, [key]: e.target.value }));
    };

  const applyFilters = () => {
    setAppliedStatFilters(buildStatFilters(draftFilters));
    setOffset(0);
  };

  const clearFilters = () => {
    setDraftFilters(EMPTY_DRAFT);
    setAppliedStatFilters({});
    setOffset(0);
  };

  const handleSortByChange = (value: string) => {
    setSortBy((value === '' ? undefined : value) as SortMetric | undefined);
    setOffset(0);
  };

  const toggleSortDirection = () => {
    setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    setOffset(0);
  };

  const videos = videosQuery.data ?? [];
  const filteredVideos = videos;
  const paginatedVideos = filteredVideos;
  // Server-side pagination: the Next button is enabled while the server
  // returns a full page (meaning there may be more).
  const hasNextPage = paginatedVideos.length >= limit;

  if (videosQuery.isLoading) {
    return <LoadingSpinner text="Loading videos..." />;
  }

  const activeCount = statFilterCount(appliedStatFilters);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Collected Videos</h1>
          <p className="text-sm text-gray-500 mt-1">Videos collected by TrendCollector API</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <select
            className="input-field w-40"
            value={sortBy ?? ''}
            onChange={(e) => handleSortByChange(e.target.value)}
            title="Sort videos by statistic"
          >
            <option value="">Sort: Default</option>
            {SORT_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
          <button
            className="btn-secondary px-3"
            onClick={toggleSortDirection}
            title="Toggle sort direction"
          >
            {sortDirection === 'asc' ? '↑ Asc' : '↓ Desc'}
          </button>
          <button
            className={`btn-secondary ${showFilters || activeCount > 0 ? 'bg-indigo-50 text-indigo-700' : ''}`}
            onClick={() => setShowFilters((s) => !s)}
          >
            ⚙ Filters{activeCount > 0 ? ` (${activeCount})` : ''}
          </button>
          <input
            type="date"
            className="input-field w-40"
            value={dateFilter}
            onChange={(e) => handleDateChange(e.target.value)}
          />
          <input
            className="input-field w-32"
            placeholder="Language..."
            value={language}
            onChange={(e) => { setLanguage(e.target.value); setOffset(0); }}
          />
          <select
            className="input-field w-32"
            value={limit}
            onChange={(e) => { setLimit(Number(e.target.value)); setOffset(0); }}
          >
            {[10, 20, 50, 100].map((n) => (
              <option key={n} value={n}>{n} rows</option>
            ))}
          </select>
        </div>
      </div>

      {showFilters && (
        <div className="card p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-900">Statistics Filters (latest snapshot)</h2>
            <button className="text-xs text-indigo-600 font-medium hover:underline" onClick={clearFilters}>
              Clear all filters
            </button>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
            {RANGE_FILTERS.map(({ minKey, maxKey, label }) => (
              <div key={minKey} className="flex items-center gap-2">
                <span className="text-xs text-gray-500 w-36 shrink-0">{label}</span>
                <input
                  type="number"
                  className="input-field min-w-0 flex-1"
                  placeholder="Min"
                  value={draftFilters[minKey]}
                  onChange={updateDraft(minKey)}
                />
                <span className="text-xs text-gray-400">to</span>
                <input
                  type="number"
                  className="input-field min-w-0 flex-1"
                  placeholder="Max"
                  value={draftFilters[maxKey]}
                  onChange={updateDraft(maxKey)}
                />
              </div>
            ))}
            <div className="flex items-center gap-2">
              <span className="text-xs text-gray-500 w-36 shrink-0">Captured At</span>
              <input
                type="datetime-local"
                className="input-field min-w-0 flex-1"
                value={draftFilters.capturedAfter}
                onChange={updateDraft('capturedAfter')}
              />
              <span className="text-xs text-gray-400">to</span>
              <input
                type="datetime-local"
                className="input-field min-w-0 flex-1"
                value={draftFilters.capturedBefore}
                onChange={updateDraft('capturedBefore')}
              />
            </div>
          </div>
          <div className="flex items-center gap-3 pt-2 border-t border-gray-100">
            <button className="btn-primary text-sm" onClick={applyFilters}>
              Apply Filters
            </button>
            {activeCount > 0 && (
              <span className="text-xs text-emerald-600 font-medium">
                ✓ {activeCount} active filter(s)
              </span>
            )}
          </div>
        </div>
      )}

      {dateFilter && (
        <div className="flex items-center justify-between bg-indigo-50 border border-indigo-200 rounded-lg px-4 py-2 text-sm">
          <span className="text-indigo-700">
            🔎 Showing {filteredVideos.length} video(s) collected on <strong>{dateFilter}</strong>
          </span>
          <button className="text-indigo-600 font-medium hover:underline" onClick={clearDateFilter}>
            Clear date filter (show all)
          </button>
        </div>
      )}

      {paginatedVideos.length === 0 ? (
        <div className="card p-8 text-center text-gray-500 text-sm">
          {dateFilter
            ? `No videos collected on ${dateFilter}.`
            : activeCount > 0
              ? 'No videos match the current statistics filters.'
              : 'No videos collected yet. Run a collection job to gather videos.'}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {paginatedVideos.map((video) => (
            <VideoCard key={video.id} video={video} />
          ))}
        </div>
      )}

      <div className="flex items-center justify-center gap-4">
        <button className="btn-secondary" onClick={() => setOffset(Math.max(0, offset - limit))} disabled={offset === 0}>
          ← Previous
        </button>
        <span className="text-sm text-gray-500">
          {paginatedVideos.length === 0
            ? '0 results'
            : `${offset + 1}-${offset + paginatedVideos.length}`}
        </span>
        <button className="btn-secondary" onClick={() => setOffset(offset + limit)} disabled={!hasNextPage}>
          Next →
        </button>
      </div>
    </div>
  );
}