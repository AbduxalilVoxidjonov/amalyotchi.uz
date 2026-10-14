/** Server hisoblagan ping holati (LIVE-JOYLASHUV.md): accuracyM > 100 → inaccurate. */
export type PingStatus = 'inside' | 'outside' | 'inaccurate';

export type PingSource = 'foreground' | 'background';

export interface LocationPing {
  /** UTC ISO */
  occurredAt: string;
  lat: number;
  lng: number;
  accuracyM: number;
  distanceM: number;
  status: PingStatus;
  isMocked: boolean;
  source: PingSource;
}

export interface TrackPlace {
  name: string;
  lat: number;
  lng: number;
  radiusM: number;
}

export interface LocationPingsSummary {
  total: number;
  inside: number;
  outside: number;
  inaccurate: number;
  outsideMinutes: number;
  longestGapMinutes: number;
  /** UTC ISO; ping bo'lmasa null. */
  lastPingAt: string | null;
}

/** GET /api/admin/students/{studentId}/location-pings?date=YYYY-MM-DD */
export interface LocationPingsResponse {
  /** DateOnly */
  date: string;
  place: TrackPlace | null;
  checkInAt: string | null;
  checkOutAt: string | null;
  consentGiven: boolean;
  /** Eskidan yangiga. */
  pings: LocationPing[];
  summary: LocationPingsSummary;
}

export const PING_STATUS_LABEL: Record<PingStatus, string> = {
  inside: 'Ichkarida',
  outside: 'Tashqarida',
  inaccurate: 'Aniqlik past',
};
