import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useKeywords, useDiscoveryJobs } from '../hooks/useDiscovery';
import { useCollectionJobs, useVideos } from '../hooks/useCollector';
import { useKnowledgeExtractionJobs } from '../hooks/useKnowledgeExtraction';
import { useViralAnalysisRuns } from '../hooks/useViralAnalysis';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';
import { formatDateTime, formatDuration } from '../utils/formatters';

function StepCard({
  title,
  subtitle,
  icon,
  color,
  children,
}: {
  title: string;
  subtitle: string;
  icon: string;
  color: string;
  children?: React.ReactNode;
}) {
  return (
    <div className={`card p-5 border-t-4 ${color}`}>
      <div className="flex items-center gap-3 mb-2">
        <span className="text-2xl">{icon}</span>
        <div>
          <h2 className="font-semibold text-gray-900">{title}</h2>
          <p className="text-xs text-gray-500">{subtitle}</p>
        </div>
      </div>
      {children}
    </div>
  );
}

function todayString() {
  const now = new Date();
  const tzOffset = now.getTimezoneOffset() * 60000;
  return new Date(now.getTime() - tzOffset).toISOString().slice(0, 10);
}

function sameDay(dateString: string | undefined, selectedDate: string) {
  if (!dateString) return false;
  return dateString.slice(0, 10) === selectedDate;
}

export default function WorkflowPage() {
  const [selectedDate, setSelectedDate] = useState(todayString());

  const keywordsQuery = useKeywords({ limit: 100 });
  const discoveryJobsQuery = useDiscoveryJobs(selectedDate, 5);
  const collectionJobsQuery = useCollectionJobs(selectedDate, 5);
  const videosQuery = useVideos({ limit: 100, date: selectedDate });
  const knowledgeExtractionJobsQuery = useKnowledgeExtractionJobs({
    date: selectedDate,
    limit: 5,
  });
  const viralAnalysisRunsQuery = useViralAnalysisRuns(5, 0);

  const isLoading =
    keywordsQuery.isLoading ||
    discoveryJobsQuery.isLoading ||
    collectionJobsQuery.isLoading ||
    videosQuery.isLoading ||
    knowledgeExtractionJobsQuery.isLoading ||
    viralAnalysisRunsQuery.isLoading;

  if (isLoading) {
    return <LoadingSpinner text="Loading workflow..." />;
  }

  const allKeywords = keywordsQuery.data ?? [];
  const discoveryJobs = discoveryJobsQuery.data ?? [];
  const collectionJobs = collectionJobsQuery.data ?? [];
  const allVideos = videosQuery.data ?? [];
  const knowledgeExtractionJobs = knowledgeExtractionJobsQuery.data ?? [];
  const viralAnalysisRuns = viralAnalysisRunsQuery.data ?? [];

  // Videos are filtered server-side by date. Keywords still need client-side
  // filtering until the backend exposes a date param for them.
  const keywords = allKeywords.filter((k) => sameDay(k.createdAt, selectedDate));
  const videos = allVideos;

  const activeKeywords = keywords.filter((k) => k.status === 'active');
  const collectedKeywords = keywords.filter((k) => k.status === 'collected');
  const failedKeywords = keywords.filter((k) => k.status === 'failed');
  const pausedKeywords = keywords.filter((k) => k.status === 'paused');

  const runningDiscovery = discoveryJobs.filter((j) => j.status === 'running').length;
  const failedDiscovery = discoveryJobs.filter((j) => j.status === 'failed').length;
  const runningCollection = collectionJobs.filter((j) => j.status === 'running').length;
  const failedCollection = collectionJobs.filter((j) => j.status === 'failed').length;

  const totalKnowledgeJobs = knowledgeExtractionJobs.length;
  const runningKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'running').length;
  const completedKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'completed').length;
  const failedKnowledge = knowledgeExtractionJobs.filter((j) => j.status === 'failed').length;

  const totalViralRuns = viralAnalysisRuns.length;
  const runningViralAnalysis = viralAnalysisRuns.filter((r) => r.status === 'running').length;
  const completedViralAnalysis = viralAnalysisRuns.filter((r) => r.status === 'completed').length;
  const failedViralAnalysis = viralAnalysisRuns.filter((r) => r.status === 'failed').length;

  const totalSavedVideos = videos.length;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Workflow Monitoring</h1>
          <p className="text-sm text-gray-500 mt-1">
            End-to-end flow: Discovery → Keywords → Collection → Videos → Knowledge Extraction → Viral Analysis
          </p>
        </div>
        <div className="flex items-center gap-2">
          <label htmlFor="workflow-date" className="text-sm font-medium text-gray-700">
            Date:
          </label>
          <input
            id="workflow-date"
            type="date"
            className="input-field w-auto"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
          />
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-6 gap-4">
        <StepCard
          title="1. Discovery"
          subtitle="AI generates keywords"
          icon="🤖"
          color="border-t-blue-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Total jobs</span>
              <span className="font-medium">{discoveryJobs.length}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Running now</span>
              <StatusBadge status={runningDiscovery > 0 ? 'running' : 'completed'} />
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Failed</span>
              <span className="font-medium text-red-600">{failedDiscovery}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Latest</span>
              <span className="text-xs text-gray-600">
                {discoveryJobs[0] ? formatDateTime(discoveryJobs[0].startedAt) : '-'}
              </span>
            </div>
          </div>
        </StepCard>

        <StepCard
          title="2. Keywords"
          subtitle="Discovered search targets"
          icon="🔍"
          color="border-t-indigo-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Active</span>
              <span className="font-medium text-green-600">{activeKeywords.length}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Collected</span>
              <span className="font-medium text-purple-600">{collectedKeywords.length}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Failed</span>
              <span className="font-medium text-red-600">{failedKeywords.length}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Paused</span>
              <span className="font-medium text-yellow-600">{pausedKeywords.length}</span>
            </div>
          </div>
        </StepCard>

        <StepCard
          title="3. Collection"
          subtitle="YouTube video collection"
          icon="📥"
          color="border-t-purple-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Total jobs</span>
              <span className="font-medium">{collectionJobs.length}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Running now</span>
              <StatusBadge status={runningCollection > 0 ? 'running' : 'completed'} />
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Failed</span>
              <span className="font-medium text-red-600">{failedCollection}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Videos saved</span>
              <span className="font-medium">{totalSavedVideos}</span>
            </div>
          </div>
        </StepCard>

        <StepCard
          title="4. Videos"
          subtitle="Collected from YouTube"
          icon="🎬"
          color="border-t-green-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Total videos</span>
              <span className="font-medium">{videos.length}</span>
            </div>
            <Link
              to={`/collector/videos?date=${selectedDate}`}
              className="inline-block mt-2 text-xs text-primary-600 hover:text-primary-700 hover:underline"
            >
              View all videos →
            </Link>
          </div>
        </StepCard>

        <StepCard
          title="5. Knowledge Extraction"
          subtitle="AI extracts structured knowledge"
          icon="🧠"
          color="border-t-orange-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Total jobs</span>
              <span className="font-medium">{totalKnowledgeJobs}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Running now</span>
              <StatusBadge status={runningKnowledge > 0 ? 'running' : 'completed'} />
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Completed</span>
              <span className="font-medium text-green-600">{completedKnowledge}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Failed</span>
              <span className="font-medium text-red-600">{failedKnowledge}</span>
            </div>
          </div>
        </StepCard>

        <StepCard
          title="6. Viral Analysis"
          subtitle="Opportunity detection & ranking"
          icon="🎯"
          color="border-t-rose-500"
        >
          <div className="mt-2 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Total runs</span>
              <span className="font-medium">{totalViralRuns}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Running now</span>
              <StatusBadge status={runningViralAnalysis > 0 ? 'running' : 'completed'} />
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Completed</span>
              <span className="font-medium text-green-600">{completedViralAnalysis}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Failed</span>
              <span className="font-medium text-red-600">{failedViralAnalysis}</span>
            </div>
            <Link
              to="/viral-analysis/runs"
              className="inline-block mt-1 text-xs text-primary-600 hover:text-primary-700 hover:underline"
            >
              View all runs →
            </Link>
          </div>
        </StepCard>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Recent Discovery Jobs</h2>
          <div className="space-y-3">
            {discoveryJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No discovery jobs on this date.</p>
            ) : (
              discoveryJobs.map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">Job #{job.id}</p>
                    <p className="text-xs text-gray-500">{formatDateTime(job.startedAt)} · {formatDuration(job.durationMs)}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-600">{job.totalKeywords} kw</span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Recent Collection Jobs</h2>
          <div className="space-y-3">
            {collectionJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No collection jobs on this date.</p>
            ) : (
              collectionJobs.map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">"{job.keyword}"</p>
                    <p className="text-xs text-gray-500">{formatDateTime(job.startedAt)} · {formatDuration(job.durationMs)}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-600">{job.totalSaved}/{job.totalCollected}</span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Knowledge Extraction Jobs</h2>
          <div className="space-y-3">
            {knowledgeExtractionJobs.length === 0 ? (
              <p className="text-sm text-gray-500">No knowledge extraction jobs on this date.</p>
            ) : (
              knowledgeExtractionJobs.map((job) => (
                <div key={job.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">Queue #{job.id}</p>
                    <p className="text-xs text-gray-500">
                      Video #{job.videoId} · {formatDateTime(job.createdAt)} · {formatDuration(job.durationMs)}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-600">#{job.videoId}</span>
                    <StatusBadge status={job.status} />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card p-5">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Recent Viral Analysis Runs</h2>
          <div className="space-y-3">
            {viralAnalysisRuns.length === 0 ? (
              <p className="text-sm text-gray-500">No viral analysis runs yet.</p>
            ) : (
              viralAnalysisRuns.map((run) => (
                <Link
                  to={`/viral-analysis/${run.id}`}
                  key={run.id}
                  className="flex items-center justify-between p-3 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <div>
                    <p className="text-sm font-medium text-gray-900">Run #{run.id}</p>
                    <p className="text-xs text-gray-500">
                      {run.eligibleCandidates}/{run.totalCandidates} candidates · {formatDateTime(run.startedAt)}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-600">{run.opportunitiesGenerated} ops</span>
                    <StatusBadge status={run.status} />
                  </div>
                </Link>
              ))
            )}
          </div>
        </div>
      </div>

      <div className="card p-5">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Top Keywords by Priority</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
          {keywords
            .slice()
            .sort((a, b) => b.priority - a.priority)
            .slice(0, 12)
            .map((keyword) => (
              <div key={keyword.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div>
                  <p className="text-sm font-medium text-gray-900">{keyword.keyword}</p>
                  <p className="text-xs text-gray-500">
                    {keyword.country} · {keyword.language.toUpperCase()} · {keyword.niche ?? 'General'}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-gray-700">{keyword.priority}</span>
                  <StatusBadge status={keyword.status} />
                </div>
              </div>
            ))}
        </div>
      </div>
    </div>
  );
}