/** Backend `MapPointKind`: rad → bad, kech kelgan → late, aks holda ok. */
export type MapPointKind = 'ok' | 'late' | 'bad';

/** Har talaba uchun shu kundagi oxirgi check-in urinishi (qabul qilingan yoki rad etilgan). */
export interface MapPoint {
  studentId: string;
  name: string;
  company: string;
  /** Belgilanish masofasi (m). */
  distanceM: number;
  /** Korxona geofence radiusi (m). */
  radiusM: number;
  /** Radius tashqarisi — tizim rad etgan ("3,4 km — rad etildi"). */
  rejected: boolean;
  /** "09:02" (Toshkent) — urinish server tomonidan qabul qilingan vaqt. */
  time: string;
  kind: MapPointKind;
  lat: number;
  lng: number;
}

/** GET /api/tutor/map?date=YYYY-MM-DD */
export interface MapResponse {
  /** DateOnly */
  date: string;
  points: MapPoint[];
}
