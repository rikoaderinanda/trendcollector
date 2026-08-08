import { useState } from 'react';
import { useCollectionJobs } from '../../hooks/useCollector';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { CollectionJob } from '../../types/collector';
import { formatDateTime, formatDuration } from '../../utils/formatters';

function ModeBadge({ mode }: { mode: CollectionJob['mode'] }) {
  return mode === 'Tracking' ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-100 text-indigo-700">
      🔄 Tracking
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700">
      🔍 Discovery
    </span>
  );
}

const columns: Column<CollectionJob>[] = [
  { key: 'id', header: 'Job ID', render: (row) => `#${row.id}` },
  {
    key: 'keyword',
    header: 'Keyword',
    render: (row) => (
      <div className="flex flex-col gap-1">
        <ModeBadge mode={row.mode} />
        {row.keyword !== '[tracking-mode]' && (
          <span className="font-medium">{row.keyword}</span>
        )}
      </div>
    ),
  },
  { key: 'country', header: 'Country', render: (row) => row.country ?? '-' },
  { key: 'language', header: 'Lang', render: (row) => (row.language ? row.language.toUpperCase() : '-') },
  {
    key: 'startedAt',
    header: 'Started',
    render: (row) => <span className="text-xs">{formatDateTime(row.startedAt)}</span>,
  },
  {
    key: 'duration',
    header: 'Duration',
    render: (row) => <span className="text-xs">{formatDuration(row.durationMs)}</span>,
  },
  {
    key: 'collected',
    header: 'Collected',
    render: (row) => (
      <div className="flex items-center gap-2">
        <span className="text-green-600 font-medium">{row.totalSaved}</span>
        <span className="text-gray-400">/</span>
        <span>{row.totalCollected}</span>
        {row.totalSkipped > 0 && (
          <span className="text-xs text-yellow-600">({row.totalSkipped} skipped)</span>
        )}
      </div>
    ),
  },
  { key: 'status', header: 'Status', render: (row) => <StatusBadge status={row.status} /> },
  {
    key: 'error',
    header: 'Error',
    render: (row) => (
      <span className="text-xs text-red-600 line-clamp-1" title={row.error}>
        {row.error ?? '-'}
      </span>
    ),
  },
];

export default function JobsPage() {
  const [limit, setLimit] = useState(20);
  const [offset, setOffset] = useState(0);

  const jobsQuery = useCollectionJobs(undefined, limit, offset);
  const jobs = jobsQuery.data ?? [];

  if (jobsQuery.isLoading) {
    return <LoadingSpinner text="Loading collection jobs..." />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Collection Jobs</h1>
          <p className="text-sm text-gray-500 mt-1">History of trend collection executions from TrendCollector API</p>
        </div>
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

      <DataTable
        columns={columns}
        data={jobs}
        keyExtractor={(row) => row.id}
        emptyMessage="No collection jobs yet. The background service will collect keywords automatically."
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