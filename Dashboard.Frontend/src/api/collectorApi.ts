import { collectorAxios } from "./axios";
import type {
  CollectionJob,
  CollectSummary,
  TrendingVideo,
  VideoDetailDto,
} from "../types/collector";

export interface VideoFilters {
  language?: string;
  date?: string;
  limit?: number;
  offset?: number;

  // Sorting
  sortBy?: SortMetric;
  sortDirection?: "asc" | "desc";

  // Statistics filter ranges (latest snapshot)
  minViews?: number;
  maxViews?: number;
  minLikes?: number;
  maxLikes?: number;
  minComments?: number;
  maxComments?: number;
  minFavorites?: number;
  maxFavorites?: number;
  minEngagementRate?: number;
  maxEngagementRate?: number;
  minViewPerDay?: number;
  maxViewPerDay?: number;
  minVideoAgeDays?: number;
  maxVideoAgeDays?: number;
  capturedAfter?: string;
  capturedBefore?: string;

  // Tracking Mode velocity metrics
  minViewsPerHour?: number;
  maxViewsPerHour?: number;
  minLikeVelocity?: number;
  maxLikeVelocity?: number;
  minCommentVelocity?: number;
  maxCommentVelocity?: number;
  minGrowthScore?: number;
  maxGrowthScore?: number;
}

export type SortMetric =
  | "published_at"
  | "views"
  | "likes"
  | "comments"
  | "favorites"
  | "engagement_rate"
  | "view_per_day"
  | "video_age_days"
  | "captured_at"
  | "views_per_hour"
  | "like_velocity"
  | "comment_velocity"
  | "growth_score";

export interface JobsFilters {
  date?: string;
  limit?: number;
  offset?: number;
}

export const collectorApi = {
  async getVideos(filters: VideoFilters = {}): Promise<TrendingVideo[]> {
    const { data } = await collectorAxios.get<TrendingVideo[]>(
      "/api/trend/videos",
      {
        params: filters,
      },
    );
    return data;
  },

  async getVideoDetail(id: number): Promise<VideoDetailDto> {
    const { data } = await collectorAxios.get<VideoDetailDto>(
      `/api/trend/videos/${id}`,
    );
    return data;
  },

  async getJobs(filters: JobsFilters = {}): Promise<CollectionJob[]> {
    const { data } = await collectorAxios.get<CollectionJob[]>(
      "/api/trend/jobs",
      {
        params: filters,
      },
    );
    return data;
  },

  async collect(request: {
    keyword: string;
    language?: string;
    country?: string;
    maxResults?: number;
  }): Promise<CollectSummary> {
    const { data } = await collectorAxios.post<CollectSummary>(
      "/api/trend/collect",
      request,
    );
    return data;
  },
};