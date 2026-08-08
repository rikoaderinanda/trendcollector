export function formatDateTime(iso?: string): string {
  if (!iso) return '-';
  const date = new Date(iso);
  return date.toLocaleString('id-ID', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatDate(iso?: string): string {
  if (!iso) return '-';
  return new Date(iso).toLocaleDateString('id-ID', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

export function formatNumber(value?: number | null): string {
  if (value === undefined || value === null) return '-';
  return new Intl.NumberFormat('en-US').format(value);
}

export function formatCompactNumber(value?: number | null): string {
  if (value === undefined || value === null) return '-';
  return new Intl.NumberFormat('en-US', {
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(value);
}

export function formatDuration(durationMs?: number | null): string {
  if (durationMs === undefined || durationMs === null) return '-';
  const seconds = Math.floor(durationMs / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

export function formatPercentage(value?: number | null): string {
  if (value === undefined || value === null) return '-';
  return `${value.toFixed(2)}%`;
}

export function formatYouTubeDuration(iso?: string): string {
  if (!iso) return '-';
  const match = iso.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
  if (!match) return iso;
  const hours = match[1] ? parseInt(match[1]) : 0;
  const minutes = match[2] ? parseInt(match[2]) : 0;
  const seconds = match[3] ? parseInt(match[3]) : 0;
  if (hours > 0) {
    return `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }
  return `${minutes}:${String(seconds).padStart(2, '0')}`;
}