import { useState } from 'react';
import { useDiscoveryJobs } from '../../hooks/useDiscovery';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { TrendDiscoveryJob } from '../../types/discovery';
import { formatDateTime, formatDuration } from '../../utils/formatters';

const columns: Column<TrendDiscoveryJob>[] = [
  { key: 'id', header: 'Job ID', render: (row) => `#${row.id}` },
  {
    key: 'startedAt',
    header: 'Started',
    render: (row) => <span className="text-xs">{formatDateTime(row.startedAt)}</span>,
  },
  {
    key: 'finishedAt',
    header: 'Finished',
    render: (row) => <span className="text-xs">{row.finishedAt ? formatDateTime(row.finishedAt) : '-'}</span>,
  },
  {
    key: 'duration',
    header: 'Duration',
    render: (row) => <span className="text-xs">{formatDuration(row.durationMs)}</span>,
  },
  { key: 'source', header: 'Source', render: (row) => row.source },
  { key: 'totalKeywords', header: 'Keywords', render: (row) => row.totalKeywords },
  { key: 'status', header: 'Status', render: (row) => <StatusBadge status={row.status} /> },
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

export default function JobsPage() {
  const [limit, setLimit] = useState(20);
  const [offset, setOffset] = useState(0);

  const jobsQuery = useDiscoveryJobs(undefined, limit, offset);
  const jobs = jobsQuery.data ?? [];

  if (jobsQuery.isLoading) {
    return <LoadingSpinner text="Loading discovery jobs..." />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Discovery Jobs</h1>
          <p className="text-sm text-gray-500 mt-1">History of trend discovery executions</p>
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
        emptyMessage="No discovery jobs yet. Click 'Run Discovery' to start."
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