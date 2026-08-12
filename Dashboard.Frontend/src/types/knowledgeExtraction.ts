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
  transcriptScore?: number | null;
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

export interface RetryTranscriptUnavailableResponse {
  resetCount: number;
}

/** Mirrors AIContentFactory.Api.Models.Entities.TrendingVideoMetadata. */
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
  tags?: string[];
  language?: string;
  captionAvailable?: boolean;
}

/** Mirrors AIContentFactory.Api.Models.Entities.VideoTranscript. */
export interface VideoTranscript {
  id: number;
  videoId: number;
  transcript: string;
  language?: string;
  source?: string;
  transcriptScore?: number | null;
  createdAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Entities.VideoKnowledge. */
export interface VideoKnowledge {
  id: number;
  videoId: number;
  summary?: string;
  mainTopic?: string;
  keywords?: string[];
  targetAudience?: string;
  tone?: string;
  hook?: string;
  contentStructure?: string[];
  callToAction?: string;
  importantPoints?: string[];
  learningNotes?: string[];
  interestingFacts?: string[];
  psychologicalTriggers?: string[];
  storyPattern?: string;
  contentType?: string;
  difficultyLevel?: string;
  language?: string;
  emotion?: string;
  curiosityScore?: number;
  educationalValue?: number;
  entertainmentValue?: number;
  engagementTechniques?: string[];
  retentionStrategy?: string;
  suggestedImprovements?: string[];
  createdAt: string;
  updatedAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Entities.KnowledgeExtractionQueue. */
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

/** Mirrors AIContentFactory.Api.Models.Dtos.KnowledgeExtractionDetailDto. */
export interface KnowledgeExtractionDetailDto {
  metadata?: TrendingVideoMetadata;
  transcript?: VideoTranscript;
  knowledge?: VideoKnowledge;
  queue?: KnowledgeExtractionQueueEntity;
  executionTimeMs?: number;
}