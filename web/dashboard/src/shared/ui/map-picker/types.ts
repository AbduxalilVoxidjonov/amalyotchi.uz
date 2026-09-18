export interface MapPoint {
  lat: number;
  lng: number;
}

export interface MapPickerProps {
  /** Tanlangan nuqta; null — hali tanlanmagan. */
  value: MapPoint | null;
  onChange: (point: MapPoint) => void;
  /** Geofence radiusi (m) — xaritada doira bo'lib ko'rinadi. */
  radiusM?: number | undefined;
  /** a11y yorlig'i va sarlavha ("Korxona joylashuvi"). */
  label: string;
  /** Xarita balandligi (px), default 280. */
  height?: number | undefined;
  disabled?: boolean | undefined;
  /** Validatsiya xatosi (maydon ostida chiqadi). */
  error?: string | undefined;
}

/** Toshkent markazi — nuqta tanlanmagan bo'lsa xarita shu yerdan ochiladi. */
export const TASHKENT: MapPoint = { lat: 41.3111, lng: 69.2797 };
export const START_ZOOM = 12;
export const PICKED_ZOOM = 16;

export function clampLat(n: number) {
  return Math.min(90, Math.max(-90, n));
}

export function clampLng(n: number) {
  return Math.min(180, Math.max(-180, n));
}
