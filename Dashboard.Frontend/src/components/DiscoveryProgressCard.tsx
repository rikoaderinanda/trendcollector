import type { RunDiscoveryResponse } from '../types/discovery';
import { formatDuration } from '../utils/formatters';

export type DiscoveryProgressStatus = 'idle' | 'running' | 'completed' | 'failed';

interface DiscoveryProgressCardProps {
  status: DiscoveryProgressStatus;
  currentStep: number;
  elapsedSeconds: number;
  jobResult?: RunDiscoveryResponse | null;
  errorMessage?: string | null;
  failureStep?: number;
  onRun: () => void;
  onRetry: () => void;
}

const steps = [
  { label: 'Creating discovery job', hint: 'Initializing job record' },
  { label: 'Building AI prompt', hint: 'Preparing prompt from configuration' },
  { label: 'Calling AI provider', hint: 'Generating trend keywords from AI' },
  { label: 'Saving prompt history', hint: 'Storing audit trail' },
  { label: 'Upserting keywords', hint: 'Parsing & saving keywords to database' },
  { label: 'Finalizing job', hint: 'Marking job as completed' },
];

function formatElapsed(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}m ${s}s`;
}

export default function DiscoveryProgressCard({
  status,
  currentStep,
  elapsedSeconds,
  jobResult,
  errorMessage,
  failureStep,
  onRun,
  onRetry,
}: DiscoveryProgressCardProps) {
  if (status === 'idle') return null;

  const isError = status === 'failed';
  const progress = status === 'completed' ? 100 : Math.min(100, Math.round((currentStep / steps.length) * 100));
  const remainingEstimate = Math.max(1, Math.round((steps.length - currentStep) * 2.5));

  return (
    <div className={`card p-5 border-t-4 ${isError ? 'border-t-red-500' : 'border-t-primary-500'}`}>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          {status === 'running' && (
            <span className="w-2 h-2 bg-primary-500 rounded-full animate-pulse" />
          )}
          <h2 className="font-semibold text-gray-900">
            {status === 'running' && '🔄 Trend Discovery Running...'}
            {status === 'completed' && '✅ Discovery Completed!'}
            {status === 'failed' && '❌ Discovery Failed'}
          </h2>
        </div>
        <span className="text-sm text-gray-500 font-mono">{formatElapsed(elapsedSeconds)}</span>
      </div>

      {/* Progress bar */}
      <div className="mb-5">
        <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all duration-700 ${
              isError ? 'bg-red-500' : status === 'completed' ? 'bg-green-500' : 'bg-primary-600'
            }`}
            style={{ width: `${progress}%` }}
          />
        </div>
        <div className="flex justify-between mt-1 text-xs text-gray-500">
          <span>{progress}%</span>
          {status === 'running' && <span>~{remainingEstimate}s remaining</span>}
        </div>
      </div>

      {/* Steps */}
      <div className="space-y-2.5">
        {steps.map((step, index) => {
          const isCompleted = status === 'completed' || index < currentStep;
          const isActive = status === 'running' && index === currentStep;
          const isFailedStep = status === 'failed' && failureStep === index;

          return (
            <div key={step.label} className="flex items-center gap-3">
              <div
                className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${
                  isFailedStep
                    ? 'bg-red-100 text-red-700'
                    : isCompleted
                      ? 'bg-green-100 text-green-700'
                      : isActive
                        ? 'bg-primary-100 text-primary-700 animate-pulse'
                        : 'bg-gray-100 text-gray-400'
                }`}
              >
                {isFailedStep ? '✗' : isCompleted ? '✓' : index + 1}
              </div>
              <div className="flex-1">
                <p
                  className={`text-sm ${
                    isFailedStep
                      ? 'text-red-700 font-medium'
                      : isCompleted
                        ? 'text-gray-700'
                        : isActive
                          ? 'text-gray-900 font-medium'
                          : 'text-gray-400'
                  }`}
                >
                  {step.label}
                </p>
                <p className="text-xs text-gray-400">{step.hint}</p>
              </div>
              {isActive && (
                <span className="text-xs text-primary-600 font-medium animate-pulse">Processing...</span>
              )}
            </div>
          );
        })}
      </div>

      {/* Error message */}
      {isError && errorMessage && (
        <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-md">
          <p className="text-sm text-red-700">{errorMessage}</p>
        </div>
      )}

      {/* Completed summary */}
      {status === 'completed' && jobResult && (
        <div className="mt-4 grid grid-cols-3 gap-3">
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <p className="text-xs text-green-600">Keywords</p>
            <p className="text-lg font-bold text-green-700">{jobResult.totalKeywords}</p>
          </div>
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <p className="text-xs text-green-600">Duration</p>
            <p className="text-lg font-bold text-green-700">{formatDuration(jobResult.durationMs)}</p>
          </div>
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <p className="text-xs text-green-600">Job #</p>
            <p className="text-lg font-bold text-green-700">{jobResult.jobId}</p>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="mt-4 flex items-center gap-3">
        {status === 'running' && (
          <button className="btn-primary" disabled>
            <span className="w-3 h-3 border-2 border-white/40 border-t-white rounded-full animate-spin" />
            Running...
          </button>
        )}
        {status === 'completed' && (
          <button className="btn-primary" onClick={onRun}>▶ Run Again</button>
        )}
        {status === 'failed' && (
          <>
            <button className="btn-primary" onClick={onRetry}>↻ Retry</button>
            <a href="/discovery/jobs" className="btn-secondary">View Jobs →</a>
          </>
        )}
      </div>
    </div>
  );
}