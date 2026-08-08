import { useQuery } from "@tanstack/react-query";
import { collectorApi, type VideoFilters } from "../api/collectorApi";

export const collectorKeys = {
  all: ["collector"] as const,
  videos: (filters: VideoFilters) => ["collector", "videos", filters] as const,
  videoDetail: (id: number) => ["collector", "video-detail", id] as const,
  jobs: (date: string | undefined, limit: number, offset: number) =>
    ["collector", "jobs", date, limit, offset] as const,
};

export function useVideos(filters: VideoFilters = {}) {
  return useQuery({
    queryKey: collectorKeys.videos(filters),
    queryFn: () => collectorApi.getVideos(filters),
    refetchInterval: 60_000,
  });
}

export function useVideoDetail(id: number) {
  return useQuery({
    queryKey: collectorKeys.videoDetail(id),
    queryFn: () => collectorApi.getVideoDetail(id),
    refetchInterval: 60_000,
  });
}

export function useCollectionJobs(
  date: string | undefined = undefined,
  limit = 20,
  offset = 0,
) {
  return useQuery({
    queryKey: collectorKeys.jobs(date, limit, offset),
    queryFn: () => collectorApi.getJobs({ date, limit, offset }),
    refetchInterval: 30_000,
  });
}
