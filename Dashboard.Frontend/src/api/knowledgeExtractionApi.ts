import { knowledgeExtractionAxios } from './axios';
import type {
  KnowledgeExtractionDetailDto,
  KnowledgeExtractionJobDto,
  RetryTranscriptUnavailableResponse,
  RunKnowledgeExtractionResponse,
} from "../types/knowledgeExtraction";

export interface KnowledgeExtractionJobsFilters {
  status?: string;
  date?: string;
  limit?: number;
  offset?: number;
}

export const knowledgeExtractionApi = {
  async getJobs(
    filters: KnowledgeExtractionJobsFilters = {},
  ): Promise<KnowledgeExtractionJobDto[]> {
    const { data } = await knowledgeExtractionAxios.get<
      KnowledgeExtractionJobDto[]
    >("/knowledge-extraction/jobs", { params: filters });
    return data;
  },

  async getVideoDetail(videoId: number): Promise<KnowledgeExtractionDetailDto> {
    const { data } =
      await knowledgeExtractionAxios.get<KnowledgeExtractionDetailDto>(
        `/knowledge-extraction/video/${videoId}`,
      );
    return data;
  },

  async enqueueVideo(
    videoId: number,
    priority = 0,
  ): Promise<KnowledgeExtractionJobDto> {
    const { data } =
      await knowledgeExtractionAxios.post<KnowledgeExtractionJobDto>(
        `/knowledge-extraction/queue/${videoId}`,
        null,
        { params: { priority } },
      );
    return data;
  },

  async runExtraction(
    videoId: number,
  ): Promise<RunKnowledgeExtractionResponse> {
    const { data } =
      await knowledgeExtractionAxios.post<RunKnowledgeExtractionResponse>(
        `/knowledge-extraction/run/${videoId}`,
      );
    return data;
  },

  async retryJob(queueId: number): Promise<RunKnowledgeExtractionResponse> {
    const { data } =
      await knowledgeExtractionAxios.post<RunKnowledgeExtractionResponse>(
        `/knowledge-extraction/retry/${queueId}`,
      );
    return data;
  },

  async retryTranscriptUnavailable(): Promise<RetryTranscriptUnavailableResponse> {
    const { data } =
      await knowledgeExtractionAxios.post<RetryTranscriptUnavailableResponse>(
        "/knowledge-extraction/retry-transcript-unavailable",
      );
    return data;
  },

  async reconstructTranscript(
    videoId: number,
  ): Promise<RetryTranscriptUnavailableResponse> {
    const { data } =
      await knowledgeExtractionAxios.post<RetryTranscriptUnavailableResponse>(
        `/knowledge-extraction/retranscript/${videoId}`,
      );
    return data;
  },
};
