export interface CollectionJob {
  id: number;
  startedAt: string;
  finishedAt?: string;
  durationMs?: number;
  keyword: string;
  mode: "Discovery" | "Tracking";
  country?: string;
  language?: string;
  status: string;
  totalCollected: number;
  totalSaved: number;
  totalSkipped: number;
  error?: string;
}

export interface TrendingVideo {
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
  definition?: string;
  dimension?: string;
  projection?: string;
  thumbnailDefaultUrl?: string;
  thumbnailMediumUrl?: string;
  thumbnailHighUrl?: string;
  thumbnailStandardUrl?: string;
  thumbnailMaxresUrl?: string;
  processedAt?: string;
  rawJson?: string;
  createdAt: string;
  updatedAt: string;
}

export interface VideoStatistics {
  id: number;
  videoId: number;
  views?: number;
  likes?: number;
  comments?: number;
  favorites?: number;
  engagementRate?: number;
  likeRatio?: number;
  commentRatio?: number;
  viewPerDay?: number;
  videoAgeDays?: number;
  capturedAt: string;
  // Tracking Mode velocity metrics
  viewsPerHour?: number;
  likeVelocity?: number;
  commentVelocity?: number;
  growthScore?: number;
  previousSnapshotId?: number;
}

export interface VideoDetailDto {
  video: TrendingVideo;
  statistics?: VideoStatistics;
}

export interface CollectSummary {
  jobId: number;
  keyword: string;
  country?: string;
  language?: string;
  mode: "Discovery" | "Tracking";
  totalCollected: number;
  totalSaved: number;
  totalSkipped: number;
  totalTracked: number;
  searchCallsRemaining: number;
  startedAt: string;
  finishedAt?: string;
  durationMs?: number;
}

export interface Channel {
  id: number;
  platformId: number;
  platformChannelId: string;
  name?: string;
  country?: string;
  subscriberCount?: number;
  videoCount?: number;
  totalViews?: number;
  publishedAt?: string;
  customUrl?: string;
  rawJson?: string;
  createdAt: string;
  updatedAt: string;
}
