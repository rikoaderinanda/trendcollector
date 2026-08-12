import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useViralAnalysisRuns,
  useRunViralAnalysis,
} from '../../hooks/useViralAnalysis';
import DataTable, { type Column } from '../../components/DataTable';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { ViralAnalysisRun } from '../../types/viralAnalysis';
import type { RunViralAnalysisRequest } from '../../types/viralAnalysis';
import { formatDateTime, formatDuration } from '../../utils/formatters';

const columns: Column<ViralAnalysisRun>[] = [
  {
    key: 'id',
    header: 'Run ID',
    render: (row) => (
      <Link
        to={`/viral-analysis/${row.id}`}
        className="text-blue-600 hover:underline font-medium"
      >
        #{row.id}
      </Link>
    ),
  },
  {
    key: 'status',
    header: 'Status',
    render: (row) => <StatusBadge status={row.status} />,
  },
  {
    key: 'candidates',
    header: 'Candidates',
    render: (row) => (
      <span className="font-medium">
        {row.eligibleCandidates}/{row.totalCandidates}
      </span>
    ),
  },
  {
    key: 'opportunities',
    header: 'Ops',
    render: (row) => <span className="font-medium">{row.opportunitiesGenerated}</span>,
  },
  {
    key: 'confidence',
    header: 'Confidence',
    render: (row) => (
      <span className={row.confidenceScore != null ? 'font-medium' : 'text-gray-400'}>
        {row.confidenceScore != null ? `${row.confidenceScore.toFixed(0)}%` : '-'}
      </span>
    ),
  },
  {
    key: 'startedAt',
    header: 'Started',
    render: (row) => <span className="text-xs">{formatDateTime(row.startedAt)}</span>,
  },
  {
    key: 'duration',
    header: 'Duration',
    render: (row) => (
      <span className="text-xs">
        {row.finishedAt
          ? formatDuration(
              new Date(row.finishedAt).getTime() - new Date(row.startedAt).getTime(),
            )
          : '-'}
      </span>
    ),
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

export default function ViralAnalysisRunsPage() {
  const [showRunDialog, setShowRunDialog] = useState(false);
  const [form, setForm] = useState<RunViralAnalysisRequest>({});
  const runsQuery = useViralAnalysisRuns(20, 0);
  const runMutation = useRunViralAnalysis();

  const isLoading = runsQuery.isLoading;
  const runs = runsQuery.data ?? [];

  const handleRun = async (e: React.FormEvent) => {
    e.preventDefault();
    const request: RunViralAnalysisRequest = {
      niche: form.niche || undefined,
      trendKeyword: form.trendKeyword || undefined,
      minimumCandidateScore: form.minimumCandidateScore ?? 0,
      maximumVideos: form.maximumVideos ?? 50,
    };
    await runMutation.mutateAsync(request);
    setShowRunDialog(false);
    setForm({});
  };

  if (isLoading) {
    return <LoadingSpinner text="Loading viral analysis runs..." />;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Viral Analysis</h1>
          <p className="text-sm text-gray-500 mt-1">
            Agent 3 — Analyzes trending videos and generates ranked content opportunities
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowRunDialog(true)}
          className="btn-primary px-4 py-2"
        >
          + Run Analysis
        </button>
      </div>

      {/* Run Analysis Dialog */}
      {showRunDialog && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Run New Analysis</h2>
            <form onSubmit={handleRun} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Niche (optional)
                </label>
                <input
                  type="text"
                  className="input-field w-full"
                  placeholder="e.g. AI Tools"
                  value={form.niche ?? ''}
                  onChange={(e) => setForm({ ...form, niche: e.target.value })}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Trend Keyword (optional)
                </label>
                <input
                  type="text"
                  className="input-field w-full"
                  placeholder="e.g. AI automation"
                  value={form.trendKeyword ?? ''}
                  onChange={(e) => setForm({ ...form, trendKeyword: e.target.value })}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Max Videos
                </label>
                <input
                  type="number"
                  className="input-field w-full"
                  min={1}
                  max={100}
                  value={form.maximumVideos ?? 50}
                  onChange={(e) =>
                    setForm({ ...form, maximumVideos: Number(e.target.value) || 50 })
                  }
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Min Candidate Score
                </label>
                <input
                  type="number"
                  className="input-field w-full"
                  min={0}
                  max={100}
                  value={form.minimumCandidateScore ?? 0}
                  onChange={(e) =>
                    setForm({ ...form, minimumCandidateScore: Number(e.target.value) || 0 })
                  }
                />
              </div>

              {runMutation.isError && (
                <p className="text-sm text-red-600">
                  Failed to run analysis. Please check the backend is running and API key is configured.
                </p>
              )}

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  className="px-4 py-2 border border-gray-300 rounded-md text-sm text-gray-700 hover:bg-gray-50"
                  onClick={() => {
                    setShowRunDialog(false);
                    setForm({});
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-primary px-4 py-2"
                  disabled={runMutation.isPending}
                >
                  {runMutation.isPending ? 'Running...' : 'Run'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <DataTable
        columns={columns}
        data={runs}
        keyExtractor={(row) => row.id}
        emptyMessage="No viral analysis runs yet. Click 'Run Analysis' to start one."
      />
    </div>
  );
}