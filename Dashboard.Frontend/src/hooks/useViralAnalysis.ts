import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { viralAnalysisApi } from "../api/viralAnalysisApi";
import type { RunViralAnalysisRequest } from "../types/viralAnalysis";

export const viralAnalysisKeys = {
  all: ["viral-analysis"] as const,
  runs: ["viral-analysis", "runs"] as const,
  detail: (analysisRunId: number) =>
    ["viral-analysis", "detail", analysisRunId] as const,
  patterns: (analysisRunId: number) =>
    ["viral-analysis", "patterns", analysisRunId] as const,
  opportunities: (analysisRunId: number) =>
    ["viral-analysis", "opportunities", analysisRunId] as const,
  recommendation: (analysisRunId: number) =>
    ["viral-analysis", "recommendation", analysisRunId] as const,
};

export function useViralAnalysisRuns(limit = 20, offset = 0) {
  return useQuery({
    queryKey: [...viralAnalysisKeys.runs, limit, offset],
    queryFn: () => viralAnalysisApi.getRuns(limit, offset),
    refetchInterval: 30_000,
  });
}

export function useViralAnalysisDetail(analysisRunId: number) {
  return useQuery({
    queryKey: viralAnalysisKeys.detail(analysisRunId),
    queryFn: () => viralAnalysisApi.getRun(analysisRunId),
    refetchInterval: 30_000,
  });
}

export function useViralAnalysisPatterns(analysisRunId: number) {
  return useQuery({
    queryKey: viralAnalysisKeys.patterns(analysisRunId),
    queryFn: () => viralAnalysisApi.getPatterns(analysisRunId),
    refetchInterval: 60_000,
  });
}

export function useViralAnalysisOpportunities(analysisRunId: number) {
  return useQuery({
    queryKey: viralAnalysisKeys.opportunities(analysisRunId),
    queryFn: () => viralAnalysisApi.getOpportunities(analysisRunId),
    refetchInterval: 60_000,
  });
}

export function useViralAnalysisRecommendation(analysisRunId: number) {
  return useQuery({
    queryKey: viralAnalysisKeys.recommendation(analysisRunId),
    queryFn: () => viralAnalysisApi.getRecommendation(analysisRunId),
    refetchInterval: 60_000,
  });
}

export function useRunViralAnalysis() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RunViralAnalysisRequest) =>
      viralAnalysisApi.runAnalysis(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viralAnalysisKeys.all });
    },
  });
}