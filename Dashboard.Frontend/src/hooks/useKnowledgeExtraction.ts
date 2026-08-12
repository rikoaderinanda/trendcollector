import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  knowledgeExtractionApi,
  type KnowledgeExtractionJobsFilters,
} from "../api/knowledgeExtractionApi";

export const knowledgeExtractionKeys = {
  all: ["knowledge-extraction"] as const,
  jobs: (filters: KnowledgeExtractionJobsFilters) =>
    ["knowledge-extraction", "jobs", filters] as const,
  videoDetail: (videoId: number) =>
    ["knowledge-extraction", "video-detail", videoId] as const,
};

export function useKnowledgeExtractionJobs(
  filters: KnowledgeExtractionJobsFilters = {},
) {
  return useQuery({
    queryKey: knowledgeExtractionKeys.jobs(filters),
    queryFn: () => knowledgeExtractionApi.getJobs(filters),
    refetchInterval: 30_000,
  });
}

export function useKnowledgeExtractionDetail(videoId: number) {
  return useQuery({
    queryKey: knowledgeExtractionKeys.videoDetail(videoId),
    queryFn: () => knowledgeExtractionApi.getVideoDetail(videoId),
    refetchInterval: 60_000,
  });
}

export function useRetryKnowledgeExtraction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (queueId: number) => knowledgeExtractionApi.retryJob(queueId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: knowledgeExtractionKeys.all });
    },
  });
}

export function useRetryTranscriptUnavailable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => knowledgeExtractionApi.retryTranscriptUnavailable(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: knowledgeExtractionKeys.all });
    },
  });
}
