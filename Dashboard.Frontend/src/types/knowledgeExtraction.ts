export interface KnowledgeExtractionJobDto {
  id: number;
  videoId: number;
  status: string;
  priority: number;
  retryCount: number;
  nextRetryAt?: string;
  startedAt?: string;
  finishedAt?: string;
  durationMs?: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}

export interface RunKnowledgeExtractionResponse {
  queueId: number;
  videoId: number;
  status: string;
  retryCount: number;
  errorMessage?: string;
  startedAt: string;
  finishedAt: string;
}

export interface TrendingVideoMetadata {
  id: number;
  platformId: number;
  platformVideoId: string;
  channelId?: number;
  title?: string;
  description?: string;
  url?: string;
  publishedAt?: string;
  duration?: string;
  category?: string;
  language?: string;
  captionAvailable?: boolean;
  definition?: string;
  dimension?: string;
  projection?: string;
  thumbnailDefaultUrl?: string;
  thumbnailMediumUrl?: string;
  thumbnailHighUrl?: string;
  thumbnailStandardUrl?: string;
  thumbnailMaxresUrl?: string;
  processedAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface VideoTranscript {
  id: number;
  videoId: number;
  transcriptText?: string;
  language?: string;
  isGenerated?: boolean;
  fetchedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface VideoKnowledge {
  id: number;
  videoId: number;
  summary?: string;
  keyPoints?: string[];
  entities?: string[];
  topics?: string[];
  sentiment?: string;
  contentType?: string;
  targetAudience?: string;
  contentIdeas?: string[];
  rawJson?: string;
  modelVersion?: string;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgeExtractionQueueEntity {
  id: number;
  videoId: number;
  status: string;
  priority: number;
  retryCount: number;
  nextRetryAt?: string;
  startedAt?: string;
  finishedAt?: string;
  durationMs?: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgeExtractionDetailDto {
  metadata?: TrendingVideoMetadata;
  transcript?: VideoTranscript;
  knowledge?: VideoKnowledge;
  queue?: KnowledgeExtractionQueueEntity;
  executionTimeMs?: number;
}