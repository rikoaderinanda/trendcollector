import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { discoveryApi, type KeywordFilters } from "../api/discoveryApi";

export const discoveryKeys = {
  all: ["discovery"] as const,
  keywords: (filters: KeywordFilters) =>
    ["discovery", "keywords", filters] as const,
  jobs: (date: string | undefined, limit: number, offset: number) =>
    ["discovery", "jobs", date, limit, offset] as const,
};

export function useKeywords(filters: KeywordFilters = {}) {
  return useQuery({
    queryKey: discoveryKeys.keywords(filters),
    queryFn: () => discoveryApi.getKeywords(filters),
    refetchInterval: 60_000,
  });
}

export function useDiscoveryJobs(
  date: string | undefined = undefined,
  limit = 20,
  offset = 0,
) {
  return useQuery({
    queryKey: discoveryKeys.jobs(date, limit, offset),
    queryFn: () => discoveryApi.getJobs({ date, limit, offset }),
    refetchInterval: 30_000,
  });
}

export function useRunDiscovery() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => discoveryApi.runDiscovery(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: discoveryKeys.all });
    },
  });
}
