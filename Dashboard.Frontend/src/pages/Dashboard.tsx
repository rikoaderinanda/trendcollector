import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useKeywords, useDiscoveryJobs } from '../hooks/useDiscovery';
import { useVideos, useCollectionJobs } from '../hooks/useCollector';
import { useKnowledgeExtractionJobs } from '../hooks/useKnowledgeExtraction';
import { useViralAnalysisRuns, useViralAnalysisRecommendation } from '../hooks/useViralAnalysis';
import { useRunViralAnalysis } from '../hooks/useViralAnalysis';
import StatCard from '../components/StatCard';
import LoadingSpinner from '../components/LoadingSpinner';
import StatusBadge from '../components/StatusBadge';
import DiscoveryProgressCard, { type DiscoveryProgressStatus } from '../components/DiscoveryProgressCard';
import type { RunDiscoveryResponse } from '../types/discovery';
import type { RunViralAnalysisRequest } from '../types/viralAnalysis';
import { formatDateTime } from '../utils/formatters';
import { discoveryApi } from '../api/discoveryApi';

function todayString() {
  const now = new Date();
  const tzOffset = now.getTimezoneOffset() * 60000;
  return new Date(now.getTime() - tzOffset).toISOString().slice(0, 10);
}

function sameDay(dateString: string | undefined, selectedDate: string) {
  if (!dateString) return false;
  return dateString.slice(0, 10) === selectedDate;
}

export default function Dashboard() {
  const [selectedDate, setSelectedDate] = useState(todayString());

  const keywordsQuery = useKeywords({ limit: 100 });
  const discoveryJobsQuery = useDiscoveryJobs(selectedDate, 10);
  const videosQuery = useVideos({ limit: 100 });
  const collectionJobsQuery = useCollectionJobs(selectedDate, 10);
  const knowledgeExtractionJobsQuery = useKnowledgeExtractionJobs({ date: selectedDate, limit: 10 });
  const viralAnalysisRunsQuery = useViralAnalysisRuns(10, 0);
  const runViralAnalysisMutation = useRunViralAnalysis();

  // Latest completed viral analysis for TOP 1 summary (hook must be called
  // unconditionally per Rules of Hooks — placed before any early return).
  const latestCompletedRun = (viralAnalysisRunsQuery.data ?? []).find((r) => r.status === 'completed');
  const latestRunId = latestCompletedRun?.id;
  const recommendationQuery = useViralAnalysisRecommendation(latestRunId ?? 0);
  const topRecommendation = latestRunId ? recommendationQuery.data : undefined;
  const totalOpportunities = (viralAnalysisRunsQuery.data ?? []).reduce((sum, r) => sum + r.opportunitiesGenerated, 0);
  const avgConfidence = (viralAnalysisRunsQuery.data ?? [])
    .filter((r) => r.confidenceScore != null)
    .reduce((sum, r, _, arr) => sum + (r.confidenceScore ?? 0) / (arr.length || 1), 0);

  // Viral analysis state
  const [runningAnalysis, setRunningAnalysis] = useState(false);

  const handleRunAnalysis = async () => {
    setRunningAnalysis(true);
    try {
      const request: RunViralAnalysisRequest = {};
      await runViralAnalysisMutation.mutateAsync(request);
    } catch {
      // unused
    } finally {
      setRunningAnalysis(false);
      viralAnalysisRunsQuery.refetch();
    }
  };

  // Discovery progress state
  const [progressStatus, setProgressStatus] = useState<DiscoveryProgressStatus>('idle');
  const [currentStep, setCurrentStep] = useState(0);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [jobResult, setJobResult] = useState<RunDiscoveryResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [failureStep, setFailureStep] = useState<number | undefined>(undefined);
  const [triggeredJobId, setTriggeredJobId] = useState<number | null>(null);
  const triggerRef = useRef(false);

  // Step timer - advances simulated steps every 2s during running
  useEffect(() => {
    if (progressStatus !== 'running') return;
    const timer = setInterval(() => {
      setCurrentStep((prev) => {
        // Cap at step 4 (before "Finalizing job") while still running
        if (prev < 4) return prev + 1;
        return prev;
      });
    }, 2000);
    return () => clearInterval(timer);
  }, [progressStatus]);

  // Elapsed timer
  useEffect(() => {
    if (progressStatus !== 'running') return;
    const timer = setInterval(() => {
      setElapsedSeconds((prev) => prev + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [progressStatus]);

  // Poll job status every 2s while running
  useEffect(() => {
    if (progressStatus !== 'running' || !triggeredJobId) return;
    const poll = setInterval(async () => {
      try {
        const jobs = await discoveryApi.getJobs({ limit: 5 });
        const matched = jobs.find((j) => j.id === triggeredJobId);
        if (!matched) return;

        if (matched.status === 'completed') {
          setProgressStatus('completed');
          setCurrentStep(6);
          setJobResult({
            jobId: matched.id,
            status: matched.status,
            totalKeywords: matched.totalKeywords,
            startedAt: matched.startedAt,
            finishedAt: matched.finishedAt,
            durationMs: matched.durationMs,
            message: 'Completed',
          });
        } else if (matched.status === 'failed') {
          setProgressStatus('failed');
          setFailureStep(2);
          setErrorMessage(matched.errorMessage ?? 'Discovery job failed');
        }
      } catch {
        // Keep polling on transient errors
      }
    }, 2000);
    return () => clearInterval(poll);
  }, [progressStatus, triggeredJobId]);

  // Refresh queries when job finishes
  useEffect(() => {
    if (progressStatus === 'completed' || progressStatus === 'failed') {
      keywordsQuery.refetch();
      discoveryJobsQuery.refetch();
      triggerRef.current = false;
    }
  }, [progressStatus]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleRunDiscovery = useCallback(async () => {
    setProgressStatus('running');
    setCurrentStep(0);
    setElapsedSeconds(0);
    setJobResult(null);
    setErrorMessage(null);
    setFailureStep(undefined);
    setTriggeredJobId(null);
    triggerRef.current = true;

    try {
      const result = await discoveryApi.runDiscovery();
      setTriggeredJobId(result.jobId);
    } catch (err) {
      setProgressStatus('failed');
      setFailureStep(0);
      setErrorMessage(err instanceof Error ? err.message : 'Failed to start discovery');
    }
  }, []);

  const handleRetry = useCallback(() => {
    setProgressStatus('idle');
    setCurrentStep(0);
    setElapsedSeconds(0);
    setJobResult(null);
    setErrorMessage(null);
    setFailureStep(undefined);
    setTriggeredJobId(null);
    // Small delay so the card disappears then re-appears
    setTimeout(() => {
      handleRunDiscovery();
    }, 150);
  }, [handleRunDiscovery]);

  const isLoading =
    keywordsQuery.isLoading ||
    discoveryJobsQuery.isLoading ||
    videosQuery.isLoading ||
    collectionJobsQuery.isLoading ||
    knowledgeExtractionJobsQuery.isLoading ||
    viralAnalysisRunsQuery.isLoading;

  if (isLoading) {
    return <LoadingSpinner text="Loading dashboard..." />;
  }

  const allKeywords = keywordsQuery.data ?? [];
  const discoveryJobs = discoveryJobsQuery.data ?? [];
  const allVideos = videosQuery.data ?? [];
  const collectionJobs = collectionJobsQuery.data ?? [];
  const knowledgeExtractionJobs = knowledgeExtractionJobsQuery.data ?? [];
  const viralRuns = viralAnalysisRunsQuery.data ?? [];

  // Client-side date filter for keywords & videos (backend has no date param for these)
  const keywords = allKeywords.filter((k) => sameDay(k.createdAt, selectedDate));
  const videos = allVideos.filter((v) => sameDay(v.createdAt, selectedDate));

  const totalKeywords = keywords.length;
  const activeKeywords = keywords.filter((k) => k.status === 'active').length;
  const totalVideos = videos.length;
  const totalDiscoveryJobs = discoveryJobs.length;
  const totalCollectionJobs = collectionJobs.length;
  const completedKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'completed').length;
  const runningKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'running').length;
  const failedKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'failed').length;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Dashboard Overview</h1>
          <p className="text-sm text-gray-500 mt-1">Monitoring workflow Trend Discovery, Collector & Knowledge Extraction</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <label htmlFor="dashboard-date" className="text-sm font-medium text-gray-700">
              Date:
            </label>
            <input
              id="dashboard-date"
              type="date"
              className="input-field w-auto"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
            />
          </div>
          <button
            className="btn-primary"
            onClick={handleRunDiscovery}
            disabled={progressStatus === 'running'}
          >
            {progressStatus === 'running' ? (
              <>
                <span className="w-3 h-3 border-2 border-white/40 border-t-white rounded-full animate-spin" />
                Running...
              </>
            ) : (
              <>
                <span>▶</span> Run Discovery
              </>
            )}
          </button>
        </div>
      </div>

      <DiscoveryProgressCard
        status={progressStatus}
        currentStep={currentStep}
        elapsedSeconds={elapsedSeconds}
        jobResult={jobResult}
        errorMessage={errorMessage}
        failureStep={failureStep}
        onRun={handleRunDiscovery}
        onRetry={handleRetry}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <StatCard
          title="Total Keywords"
          value={totalKeywords}
          subtitle={`${activeKeywords} active`}
          icon="🔍"
          accentColor="bg-indigo-50 text-indigo-600"
        />
        <StatCard
          title="Discovery Jobs"
          value={totalDiscoveryJobs}
          subtitle={`Last: ${discoveryJobs[0] ? formatDateTime(discoveryJobs[0].startedAt) : '-'}`}
          icon="⚡"
          accentColor="bg-blue-50 text-blue-600"
        />
        <StatCard
          title="Videos Collected"
          value={totalVideos}
          subtitle={`Last: ${videos[0] ? formatDateTime(videos[0].processedAt) : '-'}`}
          icon="🎬"
          accentColor="bg-purple-50 text-purple-600"
        />
        <StatCard
          title="Collection Jobs"
          value={totalCollectionJobs}
          subtitle={`Last: ${collectionJobs[0] ? formatDateTime(collectionJobs[0].startedAt) : '-'}`}
          icon="📥"
          accentColor="bg-green-50 text-green-600"
        />
        <StatCard
          title="Knowledge Extraction"
          value={knowledgeExtractionJobs.length}
          subtitle={`${completedKnowledge} done · ${runningKnowledge} running · ${failedKnowledge} failed`}
          icon="🧠"
          accentColor="bg-orange-50 text-orange-600"
        />
        <StatCard
          title="Viral Analysis Runs"
          value={viralRuns.length}
          subtitle={`${viralRuns.filter((r) => r.status === 'completed').length} completed`}
          icon="🚀"
          accentColor="bg-rose-50 text-rose-600"
        />
        <StatCard
          title="Opportunities"
          value={totalOpportunities}
          subtitle={`${topRecommendation ? `TOP1: ${topRecommendation.opportunity.topic}` : 'No recommendation yet'}`}
          icon="💡"
          accentColor="bg-cyan-50 text-cyan-600"
        />
        <StatCard
          title="Avg Confidence"
          value={avgConfidence.toFixed(0)}
          subtitle={latestCompletedRun ? `Latest run #${latestCompletedRun.id}` : 'No completed runs'}
          icon="🎯"
          accentColor="bg-amber-50 text-amber-600"
        />
      </div>

      {/* Viral Analysis Summary section */}
      <div className="card p-5">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900">🚀 Viral Analysis Summary</h2>
          <div className="flex items-center gap-3">
            <Link to="/viral-analysis/runs" className="text-sm text-primary-600 hover:text-primary-700 hover:underline">
              View all
            </Link>
            <button
              className="btn-primary"
              onClick={handleRunAnalysis}
              disabled={runningAnalysis}
            >
              {runningAnalysis ? (
                <>
                  <span className="w-3 h-3 border-2 border-white/40 border-t-white rounded-full animate-spin" />
                  Analyzing...
                </>
              ) : (
                <>
                  <span>▶</span> Run Analysis
                </>
              )}
            </button>
          </div>
        </div>

        {latestCompletedRun && topRecommendation ? (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 mb-4">
            <div className="flex items-start justify-between gap-4">
              <div className="flex-1">
                <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600">
                  TOP 1 Recommended Opportunity
                </p>
                <h3 className="mt-1 text-lg font-bold text-gray-900">
                  {topRecommendation.opportunity.topic}
                </h3>
                <p className="mt-1 text-sm text-gray-600">
                  <span className="font-medium">Angle:</span> {topRecommendation.opportunity.angle}
                </p>
                <p className="mt-1 text-sm text-gray-600">
                  <span className="font-medium">Hook:</span> {topRecommendation.opportunity.hook}
                </p>
                <p className="mt-1 text-sm text-gray-600">
                  <span className="font-medium">Audience:</span> {topRecommendation.opportunity.targetAudience ?? '-'}
                </p>
              </div>
              <div className="text-right shrink-0">
                <div className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1">
                  <span className="text-sm font-bold text-emerald-700">
                    {topRecommendation.opportunity.opportunityScore.toFixed(0)}
                  </span>
                </div>
                <p className="text-xs text-gray-500 mt-1">Opportunity Score</p>
                <Link
                  to={`/viral-analysis/${latestCompletedRun.id}/recommendation`}
                  className="text-sm text-primary-600 hover:text-primary-700 hover:underline mt-2 block"
                >
                  View full recommendation →
                </Link>
              </div>
            </div>
          </div>
        ) : (
          <p className="text-sm text-gray-500 mb-4">
            No completed viral analysis runs yet. Click "Run Analysis" to generate content opportunities.
          </p>
        )}

        <div className="space-y-3">
          {viralRuns.length === 0 ? (
            <p className="text-sm text-gray-500">No viral analysis runs.</p>
          ) : (
            viralRuns.slice(0, 5).map((run) => (
              <div key={run.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div>
                  <p className="text-sm font-medium text-gray-900">
                    Run #{run.id}{' '}
                    {run.niche && <span className="text-gray-500">({run.niche})</span>}
                  </p>
                  <p className="text-xs text-gray-500 mt-0.5">
                    {run.trendKeyword || 'Daily analysis'} · {run.eligibleCandidates}/{run.totalCandidates} candidates · {formatDateTime(run.startedAt)}
                  </p>
                </div>
                <div className="text-right">
                  <p className="text-xs text-gray-600 font-medium">
                    {run.opportunitiesGenerated} opportunities
                  </p>
                  <StatusBadge status={run.status} />
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <div className="card p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-gray-900">Latest Discovery Jobs</h2>
            <Link to="/discovery/jobs" className="text-sm text-primary-600 hover:text-primary-700 hover:underline">
              View all
            </Link>
          </div>
          <div className="space-y-3">
            {discoveryJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No discovery jobs on this date.</p>
            ) : (
              discoveryJobs.slice(0, 5).map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">
                      Job #{job.id} <span className="text-gray-500">({job.source})</span>
                    </p>
                    <p className="text-xs text-gray-500 mt-0.5">{formatDateTime(job.startedAt)}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-gray-600 font-medium">{job.totalKeywords} keywords</span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-gray-900">Latest Collection Jobs</h2>
            <Link to="/collector/jobs" className="text-sm text-primary-600 hover:text-primary-700 hover:underline">
              View all
            </Link>
          </div>
          <div className="space-y-3">
            {collectionJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No collection jobs on this date.</p>
            ) : (
              collectionJobs.slice(0, 5).map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">"{job.keyword}"</p>
                    <p className="text-xs text-gray-500 mt-0.5">{formatDateTime(job.startedAt)}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-gray-600">
                      {job.totalSaved}/{job.totalCollected} saved
                    </span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-gray-900">Latest Knowledge Extraction</h2>
            <Link to="/knowledge-extraction/jobs" className="text-sm text-primary-600 hover:text-primary-700 hover:underline">
              View all
            </Link>
          </div>
          <div className="space-y-3">
            {knowledgeExtractionJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No knowledge extraction jobs on this date.</p>
            ) : (
              knowledgeExtractionJobs.slice(0, 5).map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">
                      Queue #{job.id} <span className="text-gray-500">(Video #{job.videoId})</span>
                    </p>
                    <p className="text-xs text-gray-500 mt-0.5">{formatDateTime(job.createdAt)}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-gray-600">
                      retry {job.retryCount}
                    </span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}