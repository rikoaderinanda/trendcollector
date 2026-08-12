import { Link, useParams } from 'react-router-dom';
import { useViralAnalysisRecommendation } from '../../hooks/useViralAnalysis';
import LoadingSpinner from '../../components/LoadingSpinner';

export default function ViralAnalysisRecommendationPage() {
  const { id } = useParams<{ id: string }>();
  const runId = Number(id);
  const recQuery = useViralAnalysisRecommendation(runId);

  if (recQuery.isLoading) {
    return <LoadingSpinner text="Loading recommendation..." />;
  }

  if (recQuery.isError || !recQuery.data) {
    return (
      <div className="card p-8 text-center text-gray-500">
        <p>No TOP 1 recommendation available for run #{runId}.</p>
        <Link to={`/viral-analysis/${runId}`} className="text-blue-600 hover:underline text-sm mt-2 inline-block">
          ← Back to analysis detail
        </Link>
      </div>
    );
  }

  const rec = recQuery.data;
  const opp = rec.opportunity;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to={`/viral-analysis/${runId}`} className="text-blue-600 hover:underline text-sm">
            ← Analysis Detail
          </Link>
          <h1 className="text-2xl font-bold text-gray-900">TOP 1 Content Blueprint</h1>
        </div>
        <span className="bg-amber-100 text-amber-800 px-3 py-1 rounded-full text-sm font-semibold">
          {rec.confidenceScore.toFixed(0)}% confidence
        </span>
      </div>

      <div className="card p-6 border-t-4 border-amber-500">
        <div className="flex items-center justify-between mb-4">
          <div>
            <span className="text-xs text-gray-500 uppercase tracking-wide">Recommended Topic</span>
            <h2 className="text-xl font-bold text-gray-900">{opp.topic}</h2>
          </div>
          <span className="text-3xl font-bold text-gray-800">{opp.opportunityScore.toFixed(0)}</span>
        </div>

        <p className="text-sm text-gray-600 mb-4">{opp.angle}</p>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Hook</strong></p>
            <p className="text-gray-800">{opp.hook}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Target Audience</strong></p>
            <p className="text-gray-800">{opp.targetAudience ?? '-'}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Format</strong></p>
            <p className="text-gray-800">{opp.format}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Content Structure</strong></p>
            <p className="text-gray-800">{(opp.structure ?? []).join(' → ') || '-'}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Emotional Trigger</strong></p>
            <p className="text-gray-800">{opp.emotion ?? '-'}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Psychological Trigger</strong></p>
            <p className="text-gray-800">{opp.psychologicalTrigger ?? '-'}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Recommended CTA</strong></p>
            <p className="text-gray-800">{opp.callToAction ?? '-'}</p>
          </div>
          <div className="bg-gray-50 rounded p-3">
            <p className="text-xs text-gray-500 mb-1"><strong>Risk Level</strong></p>
            <p className={`capitalize font-medium ${opp.riskLevel === 'high' ? 'text-red-600' : opp.riskLevel === 'medium' ? 'text-yellow-600' : 'text-green-600'}`}>
              {opp.riskLevel}
            </p>
          </div>
        </div>

        {opp.whyNow && (
          <div className="mt-4">
            <p className="text-sm font-semibold text-gray-800 mb-1">Why This Opportunity</p>
            <p className="text-sm text-gray-600">{opp.whyNow}</p>
          </div>
        )}

        {rec.differentiationStrategy && (
          <div className="mt-3">
            <p className="text-sm font-semibold text-gray-800 mb-1">Differentiation Strategy</p>
            <p className="text-sm text-gray-600">{rec.differentiationStrategy}</p>
          </div>
        )}

        {rec.evidence.length > 0 && (
          <div className="mt-3">
            <p className="text-sm font-semibold text-gray-800 mb-1">Evidence</p>
            <ul className="text-sm text-gray-600 list-disc list-inside space-y-1">
              {rec.evidence.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          </div>
        )}

        {rec.risks.length > 0 && (
          <div className="mt-3">
            <p className="text-sm font-semibold text-gray-800 mb-1">Risks</p>
            <ul className="text-sm text-gray-600 list-disc list-inside space-y-1">
              {rec.risks.map((r, i) => <li key={i}>{r}</li>)}
            </ul>
          </div>
        )}

        {opp.evidence && (
          <div className="mt-3">
            <p className="text-sm font-semibold text-gray-800 mb-1">Source Video Evidence</p>
            <p className="text-sm text-gray-600 whitespace-pre-line">{opp.evidence}</p>
          </div>
        )}

        <p className="text-xs text-gray-400 mt-4">
          Strategic blueprint only — final script is generated by the next content-generation agent.
        </p>
      </div>
    </div>
  );
}