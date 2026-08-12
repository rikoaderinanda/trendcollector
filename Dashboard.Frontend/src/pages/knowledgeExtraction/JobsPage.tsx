import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useKnowledgeExtractionJobs,
  useRetryKnowledgeExtraction,
  useRetryTranscriptUnavailable,
} from '../../hooks/useKnowledgeExtraction';
import { knowledgeExtractionApi } from '../../api/knowledgeExtractionApi';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { KnowledgeExtractionJobDto } from '../../types/knowledgeExtraction';
import { formatDateTime, formatDuration } from '../../utils/formatters';

const columns: Column<KnowledgeExtractionJobDto>[] = [
  {
    key: 'id',
    header: 'Queue ID',
    render: (row) => (
      <Link to={`/knowledge-extraction/video/${row.videoId}`} className="text-blue-600 hover:underline font-medium">
        #{row.id}
      </Link>
    ),
  },
  {
    key: 'videoId',
    header: 'Video ID',
    render: (row) => (
      <Link to={`/knowledge-extraction/video/${row.videoId}`} className="text-blue-600 hover:underline">
        #{row.videoId}
      </Link>
    ),
  },
  {
    key: 'status',
    header: 'Status',
    render: (row) => <StatusBadge status={row.status} />,
  },
  {
    key: 'priority',
    header: 'Priority',
    render: (row) => (
      <span className="font-medium">{row.priority}</span>
    ),
  },
  {
    key: 'retryCount',
    header: 'Retries',
    render: (row) => (
      <span className={row.retryCount > 0 ? 'text-yellow-600 font-medium' : 'text-gray-500'}>
        {row.retryCount}
      </span>
    ),
  },
  {
    key: 'startedAt',
    header: 'Started',
    render: (row) => <span className="text-xs">{row.startedAt ? formatDateTime(row.startedAt) : '-'}</span>,
  },
  {
    key: 'duration',
    header: 'Duration',
    render: (row) => <span className="text-xs">{formatDuration(row.durationMs)}</span>,
  },
  {
    key: 'transcriptScore',
    header: 'Score Transcript',
    render: (row) => (
      <span className={`font-medium ${row.transcriptScore != null ? 'text-green-700' : 'text-gray-400'}`}>
        {row.transcriptScore != null ? row.transcriptScore : '-'}
      </span>
    ),
  },
  {
    key: 'createdAt',
    header: 'Created',
    render: (row) => <span className="text-xs">{formatDateTime(row.createdAt)}</span>,
  },
  {
    key: 'error',
    header: 'Error',
    render: (row) => (
      <span className="text-xs text-red-600 line-clamp-1" title={row.errorMessage}>
        {row.errorMessage ?? '-'}
      </span>
    ),
  },
];

const statusOptions = [
  { value: '', label: 'All statuses' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Running', label: 'Running' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Failed' },
  { value: 'TranscriptUnavailable', label: 'Transcript Unavailable' },
];

export default function JobsPage() {
  const [limit, setLimit] = useState(20);
  const [offset, setOffset] = useState(0);
  const [status, setStatus] = useState('');

  const jobsQuery = useKnowledgeExtractionJobs({
    status: status || undefined,
    limit,
    offset,
  });
  const jobs = jobsQuery.data ?? [];

  const retryJobMutation = useRetryKnowledgeExtraction();
  const retryAllMutation = useRetryTranscriptUnavailable();

  const [reconstructingIds, setReconstructingIds] = useState<Set<number>>(new Set());
  const [reconstructMsg, setReconstructMsg] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [batchReconstructing, setBatchReconstructing] = useState(false);
  const [batchProgress, setBatchProgress] = useState<string | null>(null);

  const retryInFlightId = retryJobMutation.isPending ? retryJobMutation.variables : undefined;

  const canRetry = (row: KnowledgeExtractionJobDto) =>
    row.status === 'TranscriptUnavailable' || row.status === 'Failed';

  const canReconstruct = (row: KnowledgeExtractionJobDto) =>
    row.status === 'Completed';

  const handleReconstruct = async (videoId: number) => {
    if (!window.confirm(`Reconstruct transcript for video #${videoId} using AI dedup + polish?`)) return;
    setReconstructingIds((prev) => new Set(prev).add(videoId));
    setReconstructMsg(null);
    try {
      await knowledgeExtractionApi.reconstructTranscript(videoId);
      setReconstructMsg(`Video #${videoId} transcript reconstructed successfully.`);
      jobsQuery.refetch();
    } catch (err) {
      setReconstructMsg(`Reconstruct failed for video #${videoId}: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setReconstructingIds((prev) => {
        const next = new Set(prev);
        next.delete(videoId);
        return next;
      });
    }
  };

  // Selection helpers (only Completed rows are selectable).
  const completedIds = jobs.filter((j) => j.status === 'Completed').map((j) => j.videoId);
  const allSelected = completedIds.length > 0 && completedIds.every((id) => selectedIds.has(id));
  const selectedCount = [...selectedIds].filter((id) => completedIds.includes(id)).length;

  const toggleSelect = (videoId: number) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(videoId)) {
        next.delete(videoId);
      } else {
        next.add(videoId);
      }
      return next;
    });
  };

  const toggleSelectAll = () => {
    setSelectedIds(allSelected ? new Set() : new Set(completedIds));
  };

  const handleReconstructSelected = async () => {
    const targets = [...selectedIds].filter((id) => completedIds.includes(id));
    if (targets.length === 0) return;
    if (!window.confirm(`Reconstruct ${targets.length} selected transcript(s) using AI dedup + polish?`)) return;

    setBatchReconstructing(true);
    setBatchProgress(null);
    setReconstructMsg(null);
    let success = 0;
    let failed = 0;

    for (let i = 0; i < targets.length; i++) {
      const videoId = targets[i];
      setReconstructingIds((prev) => new Set(prev).add(videoId));
      setBatchProgress(`Reconstructing ${i + 1}/${targets.length}: Video #${videoId}...`);
      try {
        await knowledgeExtractionApi.reconstructTranscript(videoId);
        success++;
      } catch {
        failed++;
      } finally {
        setReconstructingIds((prev) => {
          const next = new Set(prev);
          next.delete(videoId);
          return next;
        });
      }
    }

    setBatchReconstructing(false);
    setBatchProgress(null);
    setSelectedIds(new Set());
    setReconstructMsg(
      failed === 0
        ? `${success} transcript(s) reconstructed successfully.`
        : `${success} succeeded, ${failed} failed.`,
    );
    jobsQuery.refetch();
  };

  const handleRetryJob = (queueId: number) => {
    if (window.confirm(`Retry knowledge extraction job #${queueId}?`)) {
      retryJobMutation.mutate(queueId);
    }
  };

  const handleRetryAll = () => {
    const transcriptJobs = jobs.filter((j) => j.status === 'TranscriptUnavailable').length;
    const confirmMessage =
      transcriptJobs > 0
        ? `Reset ${transcriptJobs} TranscriptUnavailable job(s) in the current view back to Pending so the worker can retry them?`
        : 'No TranscriptUnavailable jobs in the current view. Reset ALL TranscriptUnavailable jobs in the queue anyway?';
    if (window.confirm(confirmMessage)) {
      retryAllMutation.mutate();
    }
  };

  if (jobsQuery.isLoading) {
    return <LoadingSpinner text="Loading knowledge extraction jobs..." />;
  }

  const selectionColumn: Column<KnowledgeExtractionJobDto> = {
    key: 'select',
    header: (
      <input
        type="checkbox"
        checked={allSelected}
        onChange={toggleSelectAll}
        disabled={completedIds.length === 0 || batchReconstructing}
        title="Select all Completed"
      />
    ),
    render: (row) =>
      row.status === 'Completed' ? (
        <input
          type="checkbox"
          checked={selectedIds.has(row.videoId)}
          onChange={() => toggleSelect(row.videoId)}
          disabled={batchReconstructing}
        />
      ) : null,
  };

  const actionsColumn: Column<KnowledgeExtractionJobDto> = {
    key: 'actions',
    header: 'Actions',
    render: (row) => (
      <div className="flex items-center gap-2">
        {canRetry(row) && (
          <button
            className="btn-secondary text-xs px-2 py-1"
            disabled={retryInFlightId === row.id}
            onClick={() => handleRetryJob(row.id)}
          >
            {retryInFlightId === row.id ? 'Retrying...' : 'Retry'}
          </button>
        )}
        {canReconstruct(row) && (
          <button
            className="btn-primary text-xs px-2 py-1"
            disabled={reconstructingIds.has(row.videoId)}
            onClick={() => handleReconstruct(row.videoId)}
          >
            {reconstructingIds.has(row.videoId) ? 'Reconstructing...' : '🔄 Reconstruct'}
          </button>
        )}
      </div>
    ),
  };

  const allColumns = [selectionColumn, ...columns, actionsColumn];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Knowledge Extraction Jobs</h1>
          <p className="text-sm text-gray-500 mt-1">Queue of AI knowledge extraction from collected videos</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            className="btn-primary text-sm"
            onClick={handleReconstructSelected}
            disabled={selectedCount === 0 || batchReconstructing}
          >
            {batchReconstructing
              ? 'Reconstructing...'
              : `🔄 Reconstruct Selected (${selectedCount})`}
          </button>
          <button
            className="btn-primary text-sm"
            onClick={handleRetryAll}
            disabled={retryAllMutation.isPending}
          >
            {retryAllMutation.isPending
              ? 'Resetting...'
              : 'Retry All Transcript Unavailable'}
          </button>
          <select
            className="input-field w-44"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setOffset(0); }}
          >
            {statusOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
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

      {retryAllMutation.isSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-800 text-sm px-4 py-2 rounded">
          Reset {retryAllMutation.data?.resetCount} TranscriptUnavailable job(s) back to Pending. The worker will reprocess them.
        </div>
      )}

      {retryAllMutation.isError && (
        <div className="bg-red-50 border border-red-200 text-red-800 text-sm px-4 py-2 rounded">
          Failed to reset jobs: {(retryAllMutation.error as Error)?.message ?? 'Unknown error'}
        </div>
      )}

      {batchProgress && (
        <div className="bg-blue-50 border border-blue-200 text-blue-800 text-sm px-4 py-2 rounded">
          {batchProgress}
        </div>
      )}

      {reconstructMsg && (
        <div className="bg-green-50 border border-green-200 text-green-800 text-sm px-4 py-2 rounded">
          {reconstructMsg}
        </div>
      )}

      <DataTable
        columns={allColumns}
        data={jobs}
        keyExtractor={(row) => row.id}
        emptyMessage="No knowledge extraction jobs yet."
      />

      <div className="flex items-center justify-center gap-4">
        <button className="btn-secondary" onClick={() => setOffset(Math.max(0, offset - limit))} disabled={offset === 0}>
          ← Previous
        </button>
        <span className="text-sm text-gray-500">
          {offset + 1}-{offset + jobs.length} ({jobs.length})
        </span>
        <button className="btn-secondary" onClick={() => setOffset(offset + limit)} disabled={jobs.length < limit}>
          Next →
        </button>
      </div>
    </div>
  );
}