import { useState } from 'react';
import { useKnowledgeExtractionJobs } from '../../hooks/useKnowledgeExtraction';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { KnowledgeExtractionJobDto } from '../../types/knowledgeExtraction';
import { formatDateTime, formatDuration } from '../../utils/formatters';

const columns: Column<KnowledgeExtractionJobDto>[] = [
  { key: 'id', header: 'Queue ID', render: (row) => `#${row.id}` },
  { key: 'videoId', header: 'Video ID', render: (row) => `#${row.videoId}` },
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

  if (jobsQuery.isLoading) {
    return <LoadingSpinner text="Loading knowledge extraction jobs..." />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Knowledge Extraction Jobs</h1>
          <p className="text-sm text-gray-500 mt-1">Queue of AI knowledge extraction from collected videos</p>
        </div>
        <div className="flex items-center gap-2">
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

      <DataTable
        columns={columns}
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