import { useParams, Link } from 'react-router-dom';
import {
  ResponsiveContainer,
  RadialBarChart,
  RadialBar,
  PolarAngleAxis,
} from 'recharts';
import { useVideoDetail } from '../../hooks/useCollector';
import { useKnowledgeExtractionDetail } from '../../hooks/useKnowledgeExtraction';
import LoadingSpinner from '../../components/LoadingSpinner';
import StatusBadge from '../../components/StatusBadge';
import {
  formatDateTime,
  formatNumber,
  formatPercentage,
  formatYouTubeDuration,
} from '../../utils/formatters';

function VelocityScore({ value = 0 }: { value?: number }) {
  const clamped = Math.max(0, Math.min(100, value));
  return (
    <div className="h-40 w-40 mx-auto">
      <ResponsiveContainer width="100%" height="100%">
        <RadialBarChart
          innerRadius="60%"
          outerRadius="100%"
          data={[{ name: 'Growth Score', value: clamped, fill: clamped >= 70 ? '#22c55e' : clamped >= 40 ? '#f59e0b' : '#ef4444' }]}
          startAngle={90}
          endAngle={-270}
        >
          <RadialBar dataKey="value" cornerRadius={8} background={{ fill: '#f3f4f6' }} />
          <text x="50%" y="50%" textAnchor="middle" dominantBaseline="middle" className="text-2xl font-bold">
            {Math.round(clamped)}
          </text>
        </RadialBarChart>
      </ResponsiveContainer>
    </div>
  );
}

function MetricItem({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="bg-gray-50 rounded-lg p-4">
      <p className="text-xs text-gray-500 uppercase tracking-wide">{label}</p>
      <p className="mt-1 text-xl font-bold text-gray-900">{value}</p>
      {hint && <p className="text-xs text-gray-400 mt-1">{hint}</p>}
    </div>
  );
}

export default function VideoDetailPage() {
  const { id } = useParams<{ id: string }>();
  const videoId = Number(id);

  const { data, isLoading, isError } = useVideoDetail(videoId);
  const knowledgeQuery = useKnowledgeExtractionDetail(videoId);
  const knowledgeData = knowledgeQuery.data;
  const knowledge = knowledgeData?.knowledge;
  const queue = knowledgeData?.queue;

  if (isLoading) {
    return <LoadingSpinner text="Loading video detail..." />;
  }

  if (isError || !data) {
    return (
      <div className="card p-8 text-center">
        <p className="text-gray-500">Video not found or failed to load.</p>
        <Link to="/collector/videos" className="btn-secondary mt-4">← Back to Videos</Link>
      </div>
    );
  }

  const { video, statistics } = data;

  const chartData = [
    { name: 'Engagement Rate', value: statistics?.engagementRate ?? 0, fill: '#6366f1' },
    { name: 'Like Ratio', value: statistics?.likeRatio ?? 0, fill: '#22c55e' },
    { name: 'Comment Ratio', value: statistics?.commentRatio ?? 0, fill: '#f59e0b' },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <Link to="/collector/videos" className="text-sm text-primary-600 hover:text-primary-700 hover:underline">
          ← Back to Videos
        </Link>
        <span className="text-xs text-gray-500">Video ID: {video.id}</span>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="card overflow-hidden">
          <div className="relative aspect-video bg-gray-200">
            {video.thumbnailMaxresUrl ?? video.thumbnailHighUrl ?? video.thumbnailMediumUrl ? (
              <img
                src={video.thumbnailMaxresUrl ?? video.thumbnailHighUrl ?? video.thumbnailMediumUrl}
                alt={video.title ?? 'Video thumbnail'}
                className="w-full h-full object-cover"
              />
            ) : (
              <div className="w-full h-full flex items-center justify-center text-5xl text-gray-400">🎬</div>
            )}
            {video.duration && (
              <span className="absolute bottom-3 right-3 bg-black/80 text-white text-sm font-medium px-2 py-1 rounded">
                {formatYouTubeDuration(video.duration)}
              </span>
            )}
          </div>
          <div className="p-5">
            <h1 className="text-xl font-bold text-gray-900 leading-snug">{video.title ?? 'Untitled video'}</h1>
            <div className="flex flex-wrap gap-x-4 gap-y-1 mt-3 text-sm text-gray-500">
              {video.category && <span>📂 {video.category}</span>}
              {video.language && <span>🌐 {video.language.toUpperCase()}</span>}
              {video.definition && <span>🎥 {video.definition}</span>}
              {video.dimension && <span>📐 {video.dimension}</span>}
              {video.projection && <span>🔭 {video.projection}</span>}
              {video.captionAvailable !== undefined && (
                <span>{video.captionAvailable ? '♿ Captions' : '🚫 No Captions'}</span>
              )}
            </div>
            {video.publishedAt && (
              <p className="mt-2 text-sm text-gray-500">
                📅 Published: {formatDateTime(video.publishedAt)}
              </p>
            )}
            {video.processedAt && (
              <p className="mt-1 text-sm text-gray-500">
                📥 Collected: {formatDateTime(video.processedAt)}
              </p>
            )}
            {video.url && (
              <a
                href={video.url}
                target="_blank"
                rel="noreferrer"
                className="inline-block mt-4 text-sm text-primary-600 hover:text-primary-700 hover:underline"
              >
                Open on YouTube ↗
              </a>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div className="card p-5">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Latest Statistics</h2>
            <div className="grid grid-cols-2 gap-4">
              <MetricItem label="Views" value={formatNumber(statistics?.views)} />
              <MetricItem label="Likes" value={formatNumber(statistics?.likes)} />
              <MetricItem label="Comments" value={formatNumber(statistics?.comments)} />
              <MetricItem label="Favorites" value={formatNumber(statistics?.favorites)} />
              <MetricItem
                label="Engagement Rate"
                value={formatPercentage(statistics?.engagementRate)}
              />
              <MetricItem label="View / Day" value={formatNumber(statistics?.viewPerDay)} />
              <MetricItem label="Video Age" value={statistics?.videoAgeDays ? `${statistics.videoAgeDays} days` : '-'} />
              <MetricItem
                label="Captured At"
                value={statistics?.capturedAt ? formatDateTime(statistics.capturedAt).split(',')[0] : '-'}
              />
            </div>
          </div>

          <div className="card p-5">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              Growth & Velocity <span className="text-xs font-normal text-gray-400">(Tracking Mode)</span>
            </h2>

            {statistics?.viewsPerHour !== undefined ||
            statistics?.likeVelocity !== undefined ||
            statistics?.commentVelocity !== undefined ||
            statistics?.growthScore !== undefined ? (
              <>
                <VelocityScore value={statistics.growthScore} />

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-4">
                  <MetricItem
                    label="Views / Hour"
                    value={statistics.viewsPerHour !== undefined && statistics.viewsPerHour !== null ? `${formatNumber(Math.round(statistics.viewsPerHour))}/h` : '-'}
                    hint="Views gained per hour since last snapshot"
                  />
                  <MetricItem
                    label="Like Velocity"
                    value={statistics.likeVelocity !== undefined && statistics.likeVelocity !== null ? `${formatNumber(Math.round(statistics.likeVelocity))}/h` : '-'}
                    hint="Likes gained per hour"
                  />
                  <MetricItem
                    label="Comment Velocity"
                    value={statistics.commentVelocity !== undefined && statistics.commentVelocity !== null ? `${formatNumber(Math.round(statistics.commentVelocity))}/h` : '-'}
                    hint="Comments gained per hour"
                  />
                </div>
              </>
            ) : (
              <p className="text-sm text-gray-500">
                No velocity data yet. Tracking Mode (every 4–6h) refreshes statistics and computes
                velocity metrics automatically.
              </p>
            )}
          </div>

          <div className="card p-5">
            <h2 className="text-lg font-semibold text-gray-900 mb-2">Engagement Breakdown</h2>
            <p className="text-xs text-gray-500 mb-4">Ratios as percentage of views</p>
            <div className="h-56">
              <ResponsiveContainer width="100%" height="100%">
                <RadialBarChart
                  innerRadius="30%"
                  outerRadius="100%"
                  data={chartData}
                  startAngle={180}
                  endAngle={0}
                >
                  <PolarAngleAxis type="number" domain={[0, 100]} tick={false} />
                  <RadialBar dataKey="value" cornerRadius={8} background={{ fill: '#f3f4f6' }} />
                </RadialBarChart>
              </ResponsiveContainer>
            </div>
            <div className="flex justify-center gap-6 mt-2">
              {chartData.map((item) => (
                <div key={item.name} className="flex items-center gap-2 text-sm">
                  <span className="w-3 h-3 rounded-full" style={{ backgroundColor: item.fill }} />
                  <span className="text-gray-600">{item.name}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {video.description && (
        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-3">Description</h2>
          <p className="text-sm text-gray-700 whitespace-pre-wrap line-clamp-6">{video.description}</p>
        </div>
      )}

      {video.tags && video.tags.length > 0 && (
        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-3">Tags</h2>
          <div className="flex flex-wrap gap-2">
            {video.tags.slice(0, 30).map((tag, index) => (
              <span key={index} className="px-2.5 py-1 bg-gray-100 text-xs text-gray-600 rounded-full">
                #{tag.replace(/\s+/g, '')}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Knowledge Extraction section */}
      <div className="card p-5">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900">🧠 Knowledge Extraction</h2>
          <Link
            to={`/knowledge-extraction/video/${video.id}`}
            className="text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            View full detail →
          </Link>
        </div>

        {knowledgeQuery.isLoading ? (
          <LoadingSpinner text="Loading knowledge..." />
        ) : (
          <>
            {/* Queue status */}
            <div className="flex items-center gap-4 mb-4">
              <span className="text-xs text-gray-500 uppercase tracking-wide">Queue Status</span>
              {queue ? (
                <>
                  <StatusBadge status={queue.status} />
                  <span className="text-xs text-gray-500">retry {queue.retryCount}</span>
                </>
              ) : (
                <span className="text-xs text-gray-400">Not queued / no extraction attempt yet</span>
              )}
            </div>

            {knowledge ? (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="md:col-span-2">
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Summary</p>
                  <p className="text-sm text-gray-800 mt-0.5 line-clamp-3">{knowledge.summary ?? '-'}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Main Topic</p>
                  <p className="text-sm text-gray-800 mt-0.5">{knowledge.mainTopic ?? '-'}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Content Type</p>
                  <p className="text-sm text-gray-800 mt-0.5">{knowledge.contentType ?? '-'}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Hook</p>
                  <p className="text-sm text-gray-800 mt-0.5 line-clamp-2">{knowledge.hook ?? '-'}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Emotion / Tone</p>
                  <p className="text-sm text-gray-800 mt-0.5">
                    {[knowledge.emotion, knowledge.tone].filter(Boolean).join(' · ') || '-'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Call To Action</p>
                  <p className="text-sm text-gray-800 mt-0.5">{knowledge.callToAction ?? '-'}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 uppercase tracking-wide">Target Audience</p>
                  <p className="text-sm text-gray-800 mt-0.5">{knowledge.targetAudience ?? '-'}</p>
                </div>

                {knowledge.keywords && knowledge.keywords.length > 0 && (
                  <div className="md:col-span-2">
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Keywords</p>
                    <div className="flex flex-wrap gap-1.5 mt-1">
                      {knowledge.keywords.slice(0, 15).map((kw) => (
                        <span key={kw} className="bg-gray-100 text-gray-700 text-xs px-2 py-1 rounded">
                          {kw}
                        </span>
                      ))}
                    </div>
                  </div>
                )}

                {knowledge.psychologicalTriggers && knowledge.psychologicalTriggers.length > 0 && (
                  <div className="md:col-span-1">
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Psychological Triggers</p>
                    <div className="flex flex-wrap gap-1.5 mt-1">
                      {knowledge.psychologicalTriggers.slice(0, 8).map((item) => (
                        <span key={item} className="bg-purple-50 text-purple-700 text-xs px-2 py-1 rounded">
                          {item}
                        </span>
                      ))}
                    </div>
                  </div>
                )}

                {knowledge.contentStructure && knowledge.contentStructure.length > 0 && (
                  <div className="md:col-span-1">
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Content Structure</p>
                    <div className="flex flex-wrap gap-1.5 mt-1">
                      {knowledge.contentStructure.slice(0, 8).map((item) => (
                        <span key={item} className="bg-blue-50 text-blue-700 text-xs px-2 py-1 rounded">
                          {item}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div className="bg-gray-50 rounded-lg p-4 text-sm text-gray-500">
                {queue ? (
                  <>
                    Knowledge extraction has not produced structured data yet
                    {queue.status === 'TranscriptUnavailable' && ' (transcript unavailable)'}.
                  </>
                ) : (
                  'This video has not been processed by Agent 2 (Knowledge Extraction) yet.'
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}