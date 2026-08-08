import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useVideos } from '../../hooks/useCollector';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { TrendingVideo } from '../../types/collector';
import { formatDate, formatYouTubeDuration } from '../../utils/formatters';

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
        <div className="flex items-center justify-between mt-3 text-xs text-gray-500">
          <span>{video.processedAt ? formatDate(video.processedAt) : '-'}</span>
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
  });

  const handleDateChange = (value: string) => {
    setDateFilter(value);
    setSearchParams(value ? { date: value } : {});
  };

  const clearDateFilter = () => {
    setDateFilter('');
    setSearchParams({});
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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Collected Videos</h1>
          <p className="text-sm text-gray-500 mt-1">Videos collected by TrendCollector API</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
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