import { discoveryAxios } from "./axios";
import type {
  RunDiscoveryResponse,
  TrendDiscoveryJob,
  TrendKeyword,
} from "../types/discovery";

export interface KeywordFilters {
  country?: string;
  language?: string;
  niche?: string;
  status?: string;
  limit?: number;
  offset?: number;
}

export interface JobsFilters {
  date?: string;
  limit?: number;
  offset?: number;
}

export const discoveryApi = {
  async getKeywords(filters: KeywordFilters = {}): Promise<TrendKeyword[]> {
    const { data } = await discoveryAxios.get<TrendKeyword[]>(
      "/api/trend-discovery/keywords",
      {
        params: filters,
      },
    );
    return data;
  },

  async getJobs(filters: JobsFilters = {}): Promise<TrendDiscoveryJob[]> {
    const { data } = await discoveryAxios.get<TrendDiscoveryJob[]>(
      "/api/trend-discovery/jobs",
      {
        params: filters,
      },
    );
    return data;
  },

  async runDiscovery(): Promise<RunDiscoveryResponse> {
    const { data } = await discoveryAxios.post<RunDiscoveryResponse>(
      "/api/trend-discovery/run",
    );
    return data;
  },
};
