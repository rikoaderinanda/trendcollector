import axios from "axios";

// Setelah penggabungan 3 API project menjadi 1 (AIContentFactory.Api),
// semua endpoint tersedia pada base URL yang sama.
export const DISCOVERY_API_URL =
  import.meta.env.VITE_DISCOVERY_API_URL ?? "http://localhost:5000";
export const COLLECTOR_API_URL =
  import.meta.env.VITE_COLLECTOR_API_URL ?? "http://localhost:5000";
export const KNOWLEDGE_EXTRACTION_API_URL =
  import.meta.env.VITE_KNOWLEDGE_EXTRACTION_API_URL ?? "http://localhost:5000";

export const discoveryAxios = axios.create({
  baseURL: DISCOVERY_API_URL,
  headers: { "Content-Type": "application/json" },
});

export const collectorAxios = axios.create({
  baseURL: COLLECTOR_API_URL,
  headers: { "Content-Type": "application/json" },
});

export const knowledgeExtractionAxios = axios.create({
  baseURL: KNOWLEDGE_EXTRACTION_API_URL,
  headers: { "Content-Type": "application/json" },
});
