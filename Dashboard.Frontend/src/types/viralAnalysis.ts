/** Mirrors AIContentFactory.Api.Models.Entities.ViralAnalysisRun. */
export interface ViralAnalysisRun {
  id: number;
  startedAt: string;
  finishedAt?: string;
  status: string;
  niche?: string;
  trendKeyword?: string;
  dateFrom?: string;
  dateTo?: string;
  totalCandidates: number;
  eligibleCandidates: number;
  opportunitiesGenerated: number;
  recommendedOpportunityId?: number;
  trendSummary?: string;
  marketObservation?: string;
  confidenceScore?: number;
  analysisVersion?: string;
  errorMessage?: string;
  createdAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Entities.WinningPattern. */
export interface WinningPattern {
  id: number;
  analysisRunId: number;
  patternType: string;
  patternName: string;
  description: string;
  frequency: number;
  supportingVideoCount: number;
  averageMomentumScore: number;
  evidence: string;
  createdAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Entities.ContentOpportunity. */
export interface ContentOpportunity {
  id: number;
  analysisRunId: number;
  rank: number;
  topic: string;
  angle: string;
  targetAudience?: string;
  hook: string;
  format: string;
  structure?: string[];
  emotion?: string;
  psychologicalTrigger?: string;
  whyNow: string;
  contentGap?: string;
  differentiationStrategy?: string;
  callToAction?: string;
  opportunityScore: number;
  confidenceScore: number;
  riskLevel: string;
  supportingVideoIds?: number[];
  evidence: string;
  createdAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.RunViralAnalysisRequest. */
export interface RunViralAnalysisRequest {
  niche?: string;
  trendKeyword?: string;
  dateFrom?: string;
  dateTo?: string;
  minimumCandidateScore?: number;
  maximumVideos?: number;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.RunViralAnalysisResponse. */
export interface RunViralAnalysisResponse {
  analysisRunId: number;
  status: string;
  totalCandidates: number;
  eligibleCandidates: number;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.ViralAnalysisResultDto. */
export interface ViralAnalysisResult {
  id: number;
  analysisRunId: number;
  analyzedAt: string;
  trendSummary?: string;
  marketObservation?: string;
  winningPatterns: WinningPattern[];
  contentOpportunities: ContentOpportunity[];
  recommendedOpportunity?: ContentOpportunity;
  confidenceScore?: number;
  analysisVersion?: string;
  createdAt: string;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.WinningPatternDto. */
export interface WinningPatternDto {
  id: number;
  patternType: string;
  patternName: string;
  description: string;
  frequency: number;
  supportingVideoCount: number;
  averageMomentumScore: number;
  evidence: string;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.ContentOpportunityDto. */
export interface ContentOpportunityDto {
  id: number;
  rank: number;
  topic: string;
  angle?: string;
  targetAudience?: string;
  hook: string;
  format: string;
  structure?: string[];
  emotion?: string;
  psychologicalTrigger?: string;
  whyNow: string;
  contentGap?: string;
  differentiationStrategy?: string;
  callToAction?: string;
  opportunityScore: number;
  confidenceScore: number;
  riskLevel: string;
  supportingVideoIds?: number[];
  evidence: string;
}

/** Mirrors AIContentFactory.Api.Models.Dtos.ViralAnalysisRecommendationDto. */
export interface ViralAnalysisRecommendation {
  opportunity: ContentOpportunityDto;
  confidenceScore: number;
  whyThisOpportunity: string;
  evidence: string[];
  risks: string[];
  differentiationStrategy: string;
}