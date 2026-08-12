import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  useKnowledgeExtractionDetail,
  useRetryKnowledgeExtraction,
} from '../../hooks/useKnowledgeExtraction';
import { knowledgeExtractionApi } from '../../api/knowledgeExtractionApi';
import LoadingSpinner from '../../components/LoadingSpinner';
import StatusBadge from '../../components/StatusBadge';
import { formatDateTime, formatDuration } from '../../utils/formatters';

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card p-5">
      <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-3">{title}</h2>
      {children}
    </div>
  );
}

function Field({ label, value }: { label: string; value?: string | number | null }) {
  if (value === undefined || value === null || value === '') {
    return null;
  }
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs text-gray-400">{label}</span>
      <span className="text-sm text-gray-800">{value}</span>
    </div>
  );
}

function TagList({ tags }: { tags?: string[] }) {
  if (!tags || tags.length === 0) return <span className="text-sm text-gray-400">-</span>;
  return (
    <div className="flex flex-wrap gap-1.5">
      {tags.map((tag) => (
        <span key={tag} className="bg-gray-100 text-gray-700 text-xs px-2 py-1 rounded">
          {tag}
        </span>
      ))}
    </div>
  );
}

export default function DetailPage() {
  const { id } = useParams<{ id: string }>();
  const videoId = Number(id);

  const detailQuery = useKnowledgeExtractionDetail(videoId);
  const retryMutation = useRetryKnowledgeExtraction();
  const [reconstructing, setReconstructing] = useState(false);
  const [reconstructMessage, setReconstructMessage] = useState<string | null>(null);
  const [reconstructError, setReconstructError] = useState<string | null>(null);

  const handleReconstruct = async () => {
    if (!confirm('Reconstruct this transcript using the latest dedup normalization? This will update the stored transcript.')) {
      return;
    }
    setReconstructing(true);
    setReconstructMessage(null);
    setReconstructError(null);
    try {
      await knowledgeExtractionApi.reconstructTranscript(videoId);
      setReconstructMessage(`Transcript reconstructed successfully.`);
      detailQuery.refetch();
    } catch (err) {
      setReconstructError(err instanceof Error ? err.message : 'Reconstruction failed');
    } finally {
      setReconstructing(false);
    }
  };

  const data = detailQuery.data;
  const metadata = data?.metadata;
  const transcript = data?.transcript;
  const knowledge = data?.knowledge;
  const queue = data?.queue;

  const handleRetry = () => {
    if (!queue) return;
    if (window.confirm(`Retry knowledge extraction job #${queue.id}?`)) {
      retryMutation.mutate(queue.id);
    }
  };

  if (detailQuery.isLoading) {
    return <LoadingSpinner text="Loading video detail..." />;
  }

  if (detailQuery.isError || !metadata) {
    return (
      <div className="space-y-4">
        <Link to="/knowledge-extraction/jobs" className="text-blue-600 text-sm hover:underline">← Back to jobs</Link>
        <div className="card p-8 text-center text-red-600 text-sm">
          Video not found or failed to load detail.
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <Link to="/knowledge-extraction/jobs" className="text-blue-600 text-sm hover:underline">
            ← Back to jobs
          </Link>
          <h1 className="text-2xl font-bold text-gray-900 mt-1">
            {metadata.title ?? `Video #${metadata.id}`}
          </h1>
          <p className="text-sm text-gray-500">
            Video ID {metadata.platformVideoId} · YouTube ID #{metadata.id}
          </p>
        </div>
        {queue && (queue.status === 'TranscriptUnavailable' || queue.status === 'Failed') && (
          <button
            className="btn-primary text-sm"
            onClick={handleRetry}
            disabled={retryMutation.isPending}
          >
            {retryMutation.isPending ? 'Retrying...' : 'Retry Job'}
          </button>
        )}
      </div>

      {retryMutation.isSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-800 text-sm px-4 py-2 rounded">
          Retry triggered: {retryMutation.data.status}
        </div>
      )}
      {retryMutation.isError && (
        <div className="bg-red-50 border border-red-200 text-red-800 text-sm px-4 py-2 rounded">
          Retry failed: {(retryMutation.error as Error)?.message ?? 'Unknown error'}
        </div>
      )}

      <Section title="Queue Status">
        <div className="flex flex-wrap items-center gap-6">
          {queue ? (
            <>
              <div className="flex flex-col gap-0.5">
                <span className="text-xs text-gray-400">Status</span>
                <StatusBadge status={queue.status} />
              </div>
              <Field label="Priority" value={queue.priority} />
              <Field label="Retries" value={queue.retryCount} />
              <Field label="Duration" value={queue.durationMs ? formatDuration(queue.durationMs) : undefined} />
              <Field label="Started" value={queue.startedAt ? formatDateTime(queue.startedAt) : undefined} />
              <Field label="Finished" value={queue.finishedAt ? formatDateTime(queue.finishedAt) : undefined} />
            </>
          ) : (
            <span className="text-sm text-gray-400">Not queued for knowledge extraction yet.</span>
          )}
        </div>
        {queue?.errorMessage && (
          <p className="text-xs text-red-600 mt-2">{queue.errorMessage}</p>
        )}
      </Section>

      <Section title="Video Metadata">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="md:col-span-1">
            {metadata.url ? (
              <a href={metadata.url} target="_blank" rel="noreferrer">
                <img
                  src={`https://i.ytimg.com/vi/${metadata.platformVideoId}/hqdefault.jpg`}
                  alt={metadata.title ?? 'Video thumbnail'}
                  className="rounded-lg w-full"
                />
              </a>
            ) : (
              <div className="aspect-video bg-gray-100 rounded-lg flex items-center justify-center text-gray-400 text-sm">
                No thumbnail
              </div>
            )}
          </div>
          <div className="md:col-span-2 space-y-3">
            <Field label="Title" value={metadata.title} />
            <Field label="Language" value={metadata.language} />
            <Field label="Category" value={metadata.category} />
            <Field label="Duration" value={metadata.duration} />
            <Field label="Published" value={metadata.publishedAt ? formatDateTime(metadata.publishedAt) : undefined} />
            <Field label="Caption Available" value={metadata.captionAvailable ? 'Yes' : 'No'} />
            {metadata.tags && metadata.tags.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Tags</span>
                <TagList tags={metadata.tags} />
              </div>
            )}
          </div>
        </div>
        {metadata.description && (
          <p className="text-sm text-gray-600 mt-3 line-clamp-4">{metadata.description}</p>
        )}
      </Section>

      <Section title="Transcript">
        {transcript ? (
          <div>
            <div className="flex items-center gap-3 mb-3">
              <StatusBadge status="completed" />
              <span className="text-xs text-gray-400">
                Language: {transcript.language ?? '-'} · Source: {transcript.source ?? '-'} · {transcript.transcript.length.toLocaleString()} chars
              </span>

              {/* AI transcript quality score */}
              {transcript.transcriptScore != null && (
                <span
                  className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                    transcript.transcriptScore >= 70
                      ? 'bg-emerald-100 text-emerald-700'
                      : transcript.transcriptScore >= 40
                        ? 'bg-amber-100 text-amber-700'
                        : 'bg-red-100 text-red-700'
                  }`}
                  title="AI-assessed transcript quality score (0-100)"
                >
                  🎯 {transcript.transcriptScore}/100
                </span>
              )}

              <button
                className="btn-secondary text-xs ml-auto"
                onClick={handleReconstruct}
                disabled={reconstructing}
              >
                {reconstructing ? (
                  <>
                    <span className="w-3 h-3 border-2 border-gray-400 border-t-gray-600 rounded-full animate-spin" />
                    Reconstructing...
                  </>
                ) : (
                  '🔄 Reconstruct + Polish'
                )}
              </button>
            </div>
            {reconstructMessage && (
              <div className="bg-green-50 border border-green-200 text-green-800 text-sm px-4 py-2 rounded mb-3">
                {reconstructMessage}
              </div>
            )}
            {reconstructError && (
              <div className="bg-red-50 border border-red-200 text-red-800 text-sm px-4 py-2 rounded mb-3">
                {reconstructError}
              </div>
            )}
            <p className="text-sm text-gray-800 leading-relaxed whitespace-pre-wrap">{transcript.transcript}</p>
          </div>
        ) : (
          <p className="text-sm text-gray-400">
            No transcript has been loaded for this video yet.
          </p>
        )}
      </Section>

      <Section title="Knowledge">
        {knowledge ? (
          <div className="space-y-4">
            <Field label="Summary" value={knowledge.summary} />
            <Field label="Main Topic" value={knowledge.mainTopic} />
            <Field label="Content Type" value={knowledge.contentType} />
            <Field label="Target Audience" value={knowledge.targetAudience} />
            <Field label="Tone" value={knowledge.tone} />
            <Field label="Hook" value={knowledge.hook} />
            <Field label="Emotion" value={knowledge.emotion} />
            <Field label="Story Pattern" value={knowledge.storyPattern} />
            <Field label="Difficulty Level" value={knowledge.difficultyLevel} />
            <Field label="Retention Strategy" value={knowledge.retentionStrategy} />
            <Field label="Call To Action" value={knowledge.callToAction} />

            <div className="flex flex-wrap gap-6">
              <Field label="Curiosity Score" value={knowledge.curiosityScore} />
              <Field label="Educational Value" value={knowledge.educationalValue} />
              <Field label="Entertainment Value" value={knowledge.entertainmentValue} />
            </div>

            {knowledge.keywords && knowledge.keywords.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Keywords</span>
                <TagList tags={knowledge.keywords} />
              </div>
            )}
            {knowledge.contentStructure && knowledge.contentStructure.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Content Structure</span>
                <TagList tags={knowledge.contentStructure} />
              </div>
            )}
            {knowledge.importantPoints && knowledge.importantPoints.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Important Points</span>
                <TagList tags={knowledge.importantPoints} />
              </div>
            )}
            {knowledge.learningNotes && knowledge.learningNotes.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Learning Notes</span>
                <TagList tags={knowledge.learningNotes} />
              </div>
            )}
            {knowledge.interestingFacts && knowledge.interestingFacts.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Interesting Facts</span>
                <TagList tags={knowledge.interestingFacts} />
              </div>
            )}
            {knowledge.psychologicalTriggers && knowledge.psychologicalTriggers.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Psychological Triggers</span>
                <TagList tags={knowledge.psychologicalTriggers} />
              </div>
            )}
            {knowledge.engagementTechniques && knowledge.engagementTechniques.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Engagement Techniques</span>
                <TagList tags={knowledge.engagementTechniques} />
              </div>
            )}
            {knowledge.suggestedImprovements && knowledge.suggestedImprovements.length > 0 && (
              <div>
                <span className="text-xs text-gray-400">Suggested Improvements</span>
                <TagList tags={knowledge.suggestedImprovements} />
              </div>
            )}
          </div>
        ) : (
          <p className="text-sm text-gray-400">
            No structured knowledge has been extracted for this video yet.
          </p>
        )}
      </Section>
    </div>
  );
}