export interface TrendKeyword {
  id: number;
  keyword: string;
  niche?: string;
  country: string;
  language: string;
  priority: number;
  discoveryReason?: string;
  source: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface TrendDiscoveryJob {
  id: number;
  startedAt: string;
  finishedAt?: string;
  durationMs?: number;
  status: string;
  totalKeywords: number;
  errorMessage?: string;
  source: string;
}

export interface RunDiscoveryResponse {
  jobId: number;
  status: string;
  totalKeywords: number;
  startedAt: string;
  finishedAt?: string;
  durationMs?: number;
  message?: string;
}