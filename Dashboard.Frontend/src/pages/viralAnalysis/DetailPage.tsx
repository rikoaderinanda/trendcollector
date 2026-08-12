import { Link, useParams } from 'react-router-dom';
import {
  useViralAnalysisDetail,
  useViralAnalysisPatterns,
  useViralAnalysisOpportunities,
} from '../../hooks/useViralAnalysis';
import StatusBadge from '../../components/StatusBadge';
import LoadingSpinner from '../../components/LoadingSpinner';
import type { ContentOpportunityDto } from '../../types/viralAnalysis';
import { formatDateTime } from '../../utils/formatters';

function riskBadge(risk: string) {
  const styles: Record<string, string> = {
    low: 'bg-green-100 text-green-800',
    medium: 'bg-yellow-100 text-yellow-800',
    high: 'bg-red-100 text-red-800',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium capitalize ${styles[risk.toLowerCase()] ?? 'bg-gray-100 text-gray-800'}`}>
      {risk}
    </span>
  );
}

function OpportunityCard({ opp }: { opp: ContentOpportunityDto }) {
  const isTop = opp.rank === 1;
  return (
    <div className={`border rounded-lg p-4 ${isTop ? 'border-amber-400 bg-amber-50' : 'border-gray-200 bg-white'}`}>
      <div className="flex items-start justify-between mb-2">
        <div className="flex items-center gap-2">
          <span className={`inline-flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold ${isTop ? 'bg-amber-500 text-white' : 'bg-gray-200 text-gray-700'}`}>
            {opp.rank}
          </span>
          <h3 className="font-semibold text-gray-900">{opp.topic}</h3>
        </div>
        <span className={`text-lg font-bold ${opp.opportunityScore >= 80 ? 'text-green-600' : 'text-gray-600'}`}>
          {opp.opportunityScore.toFixed(0)}
        </span>
      </div>
      <p className="text-xs text-gray-500 mb-3">{opp.angle}</p>
      <div className="flex flex-wrap gap-2 text-xs mb-3">
        <span className="px-2 py-0.5 bg-gray-100 rounded">{opp.format}</span>
        {opp.emotion && <span className="px-2 py-0.5 bg-purple-50 text-purple-700 rounded">😊 {opp.emotion}</span>}
        {opp.psychologicalTrigger && <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded">🧠 {opp.psychologicalTrigger}</span>}
        {riskBadge(opp.riskLevel)}
      </div>
      <p className="text-xs text-gray-600 mb-1"><strong>Hook:</strong> {opp.hook}</p>
      {opp.structure && opp.structure.length > 0 && (
        <p className="text-xs text-gray-600 mb-1"><strong>Structure:</strong> {opp.structure.join(' → ')}</p>
      )}
      {opp.callToAction && <p className="text-xs text-gray-600 mb-1"><strong>CTA:</strong> {opp.callToAction}</p>}
      {opp.targetAudience && <p className="text-xs text-gray-600 mb-1"><strong>Audience:</strong> {opp.targetAudience}</p>}
      {opp.whyNow && <p className="text-xs text-gray-600 mb-1"><strong>Why now:</strong> {opp.whyNow}</p>}
      {opp.evidence && (
        <p className="text-xs text-gray-500 mt-2 border-t border-gray-100 pt-2 whitespace-pre-line">
          <strong className="text-gray-700">Evidence:</strong> {opp.evidence}
        </p>
      )}
    </div>
  );
}

export default function ViralAnalysisDetailPage() {
  const { id } = useParams<{ id: string }>();
  const runId = Number(id);
  const detailQuery = useViralAnalysisDetail(runId);
  const patternsQuery = useViralAnalysisPatterns(runId);
  const opportunitiesQuery = useViralAnalysisOpportunities(runId);

  if (detailQuery.isLoading) {
    return <LoadingSpinner text="Loading analysis detail..." />;
  }

  if (detailQuery.isError || !detailQuery.data) {
    return (
      <div className="card p-8 text-center text-gray-500">
        <p>Analysis run #{runId} not found or failed to load.</p>
        <Link to="/viral-analysis/runs" className="text-blue-600 hover:underline text-sm mt-2 inline-block">
          ← Back to runs
        </Link>
      </div>
    );
  }

  const detail = detailQuery.data;
  const patterns = patternsQuery.data ?? [];
  const opportunities = opportunitiesQuery.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to="/viral-analysis/runs" className="text-blue-600 hover:underline text-sm">← Runs</Link>
          <h1 className="text-2xl font-bold text-gray-900">Run #{runId}</h1>
          <StatusBadge status="completed" />
        </div>
        {detail.recommendedOpportunity && (
          <Link to={`/viral-analysis/${runId}/recommendation`} className="btn-primary px-4 py-2">
            View TOP 1 Blueprint
          </Link>
        )}
      </div>

      <div className="card p-5">
        <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
          <div>
            <p className="text-gray-500">Analyzed At</p>
            <p className="font-medium">{formatDateTime(detail.analyzedAt)}</p>
          </div>
          <div>
            <p className="text-gray-500">Confidence</p>
            <p className="font-medium">{detail.confidenceScore != null ? `${detail.confidenceScore.toFixed(0)}%` : '-'}</p>
          </div>
          <div>
            <p className="text-gray-500">Version</p>
            <p className="font-medium">{detail.analysisVersion ?? '-'}</p>
          </div>
        </div>
        {detail.trendSummary && (
          <p className="text-sm text-gray-600 mt-4"><strong className="text-gray-800">Trend Summary:</strong> {detail.trendSummary}</p>
        )}
        {detail.marketObservation && (
          <p className="text-sm text-gray-600 mt-2"><strong className="text-gray-800">Market Observation:</strong> {detail.marketObservation}</p>
        )}
      </div>

      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3">Winning Patterns</h2>
        {patterns.length === 0 ? (
          <p className="text-sm text-gray-500">No winning patterns detected.</p>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            {patterns.map((pattern) => (
              <div key={pattern.id} className="card p-4">
                <div className="flex items-center justify-between mb-2">
                  <span className="px-2 py-0.5 bg-indigo-50 text-indigo-700 rounded text-xs font-medium">{pattern.patternType}</span>
                  <span className="text-xs text-gray-500">{pattern.supportingVideoCount}/{pattern.frequency} videos</span>
                </div>
                <h3 className="font-semibold text-gray-900">{pattern.patternName}</h3>
                <p className="text-xs text-gray-500 mt-1">{pattern.description}</p>
                <p className="text-xs text-gray-600 mt-2"><strong>Avg momentum:</strong> {pattern.averageMomentumScore.toFixed(1)}</p>
              </div>
            ))}
          </div>
        )}
      </div>

      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3">Content Opportunities</h2>
        {opportunities.length === 0 ? (
          <p className="text-sm text-gray-500">No opportunities generated (AI might be unavailable).</p>
        ) : (
          <div className="space-y-4">
            {opportunities.map((opp) => <OpportunityCard key={opp.id} opp={opp} />)}
          </div>
        )}
      </div>
    </div>
  );
}