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
}

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
