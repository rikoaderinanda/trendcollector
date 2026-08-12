import { viralAnalysisAxios } from "./axios";
import type {
  ContentOpportunityDto,
  RunViralAnalysisRequest,
  RunViralAnalysisResponse,
  ViralAnalysisRecommendation,
  ViralAnalysisResult,
  ViralAnalysisRun,
  WinningPatternDto,
} from "../types/viralAnalysis";

export const viralAnalysisApi = {
  async getRuns(
    limit = 20,
    offset = 0,
  ): Promise<ViralAnalysisRun[]> {
    const { data } = await viralAnalysisAxios.get<ViralAnalysisRun[]>(
      "/api/viral-analysis",
      { params: { limit, offset } },
    );
    return data;
  },

  async runAnalysis(
    request: RunViralAnalysisRequest,
  ): Promise<RunViralAnalysisResponse> {
    const { data } = await viralAnalysisAxios.post<RunViralAnalysisResponse>(
      "/api/viral-analysis/run",
      request,
    );
    return data;
  },

  async getRun(analysisRunId: number): Promise<ViralAnalysisResult> {
    const { data } = await viralAnalysisAxios.get<ViralAnalysisResult>(
      `/api/viral-analysis/${analysisRunId}`,
    );
    return data;
  },

  async getPatterns(analysisRunId: number): Promise<WinningPatternDto[]> {
    const { data } = await viralAnalysisAxios.get<WinningPatternDto[]>(
      `/api/viral-analysis/${analysisRunId}/patterns`,
    );
    return data;
  },

  async getOpportunities(
    analysisRunId: number,
  ): Promise<ContentOpportunityDto[]> {
    const { data } = await viralAnalysisAxios.get<ContentOpportunityDto[]>(
      `/api/viral-analysis/${analysisRunId}/opportunities`,
    );
    return data;
  },

  async getRecommendation(
    analysisRunId: number,
  ): Promise<ViralAnalysisRecommendation> {
    const { data } = await viralAnalysisAxios.get<ViralAnalysisRecommendation>(
      `/api/viral-analysis/${analysisRunId}/recommendation`,
    );
    return data;
  },
};