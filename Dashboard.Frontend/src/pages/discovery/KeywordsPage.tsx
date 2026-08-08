import { useState } from 'react';
import { useKeywords, useRunDiscovery } from '../../hooks/useDiscovery';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { TrendKeyword } from '../../types/discovery';
import { formatDateTime } from '../../utils/formatters';

const columns: Column<TrendKeyword>[] = [
  {
    key: 'keyword',
    header: 'Keyword',
    render: (row) => (
      <div>
        <p className="font-medium text-gray-900">{row.keyword}</p>
        {row.niche && <p className="text-xs text-gray-500">{row.niche}</p>}
      </div>
    ),
  },
  { key: 'country', header: 'Country', render: (row) => row.country },
  { key: 'language', header: 'Lang', render: (row) => row.language.toUpperCase() },
  {
    key: 'priority',
    header: 'Priority',
    render: (row) => (
      <div className="flex items-center gap-2">
        <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full ${
              row.priority >= 70 ? 'bg-red-500' : row.priority >= 40 ? 'bg-yellow-500' : 'bg-green-500'
            }`}
            style={{ width: `${row.priority}%` }}
          />
        </div>
        <span className="text-xs font-medium">{row.priority}</span>
      </div>
    ),
  },
  { key: 'source', header: 'Source', render: (row) => row.source },
  { key: 'status', header: 'Status', render: (row) => <StatusBadge status={row.status} /> },
  {
    key: 'updatedAt',
    header: 'Updated',
    render: (row) => <span className="text-xs text-gray-500">{formatDateTime(row.updatedAt)}</span>,
  },
];

const statusOptions = ['', 'active', 'collected', 'paused', 'failed', 'archived'];

export default function KeywordsPage() {
  const [country, setCountry] = useState('');
  const [language, setLanguage] = useState('');
  const [status, setStatus] = useState('');
  const [limit, setLimit] = useState(20);
  const [offset, setOffset] = useState(0);

  const keywordsQuery = useKeywords({ country, language, status, limit, offset });
  const runDiscovery = useRunDiscovery();
  const [runMessage, setRunMessage] = useState<string | null>(null);

  const keywords = keywordsQuery.data ?? [];
  const isLoading = keywordsQuery.isLoading;

  const handleRunDiscovery = async () => {
    setRunMessage(null);
    try {
      const result = await runDiscovery.mutateAsync();
      setRunMessage(`Discovery started: Job #${result.jobId} (${result.status})`);
    } catch {
      setRunMessage('Failed to run discovery');
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Trend Keywords</h1>
          <p className="text-sm text-gray-500 mt-1">Discovered keywords from TrendDiscovery API</p>
        </div>
        <button className="btn-primary" onClick={handleRunDiscovery} disabled={runDiscovery.isPending}>
          {runDiscovery.isPending ? 'Running...' : '▶ Run Discovery'}
        </button>
      </div>

      {runMessage && (
        <div className="p-3 rounded-md bg-blue-50 text-blue-800 text-sm">{runMessage}</div>
      )}

      <div className="flex flex-wrap gap-3 items-center card p-4">
        <input
          className="input-field w-40"
          placeholder="Country..."
          value={country}
          onChange={(e) => { setCountry(e.target.value); setOffset(0); }}
        />
        <input
          className="input-field w-32"
          placeholder="Language..."
          value={language}
          onChange={(e) => { setLanguage(e.target.value); setOffset(0); }}
        />
        <select
          className="input-field w-36"
          value={status}
          onChange={(e) => { setStatus(e.target.value); setOffset(0); }}
        >
          {statusOptions.map((s) => (
            <option key={s} value={s}>{s === '' ? 'All Status' : s}</option>
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

      {isLoading ? (
        <LoadingSpinner text="Loading keywords..." />
      ) : (
        <DataTable
          columns={columns}
          data={keywords}
          keyExtractor={(row) => row.id}
          emptyMessage="No keywords found. Run a discovery job to generate keywords."
        />
      )}

      <div className="flex items-center justify-center gap-4">
        <button className="btn-secondary" onClick={() => setOffset(Math.max(0, offset - limit))} disabled={offset === 0}>
          ← Previous
        </button>
        <span className="text-sm text-gray-500">
          {offset + 1}-{offset + keywords.length} ({keywords.length})
        </span>
        <button className="btn-secondary" onClick={() => setOffset(offset + limit)} disabled={keywords.length < limit}>
          Next →
        </button>
      </div>
    </div>
  );
}