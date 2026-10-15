import { useEffect, useMemo } from 'react';
import { divIcon, type LatLngBoundsExpression, type LatLngTuple } from 'leaflet';
import {
  Circle,
  CircleMarker,
  MapContainer,
  Marker,
  Polyline,
  TileLayer,
  Tooltip,
  useMap,
} from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { cn } from '@/shared/ui';
import { fmtDistance, fmtTime } from '../../shared/fmt';
import { routeArrows, sortByTime } from '../route';
import { PING_STATUS_LABEL, type LocationPing, type TrackPlace } from '../types';
import styles from './TrackMap.module.css';

const OSM_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION = '&copy; OpenStreetMap';

export interface TrackMapCanvasProps {
  place: TrackPlace | null;
  pings: readonly LocationPing[];
  checkInAt?: string | null | undefined;
  checkOutAt?: string | null | undefined;
  /** Yo'nalish strelkalari soni chegarasi (kichik xaritada kamroq). */
  maxArrows?: number | undefined;
}

/** 1° kenglik ≈ 111 km — radius doirasi bounds'ga sig'ishi uchun. */
const M_PER_DEG = 111_000;

function boundsOf(
  place: TrackPlace | null,
  pings: readonly LocationPing[],
): LatLngBoundsExpression {
  const pts: LatLngTuple[] = pings.map((p) => [p.lat, p.lng]);
  if (place) {
    const d = place.radiusM / M_PER_DEG;
    pts.push([place.lat - d, place.lng - d], [place.lat + d, place.lng + d]);
  }
  const lats = pts.map((p) => p[0]);
  const lngs = pts.map((p) => p[1]);
  return [
    [Math.min(...lats), Math.min(...lngs)],
    [Math.max(...lats), Math.max(...lngs)],
  ];
}

/** Strelka — yuqoriga qaragan chevron; `bearing` bo'yicha buriladi. */
function arrowIcon(bearing: number) {
  return divIcon({
    className: styles.arrowIcon ?? '',
    html: `<svg viewBox="0 0 16 16" width="16" height="16" style="transform:rotate(${Math.round(bearing)}deg)" aria-hidden="true"><path d="M8 2 L14 13 L8 10 L2 13 Z"/></svg>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
  });
}

function endpointIcon(kind: 'start' | 'end') {
  return divIcon({
    className: cn(styles.endpoint, kind === 'start' ? styles.endpointStart : styles.endpointEnd),
    html: kind === 'start' ? 'A' : 'B',
    iconSize: [22, 22],
    iconAnchor: [11, 11],
  });
}

/**
 * Konteyner o'lchami o'zgarsa (modal ochilish animatsiyasi, oyna burilishi) Leaflet plitkalarini
 * qayta hisoblaydi — aks holda xarita kulrang bo'laklar bilan qoladi.
 */
function AutoResize() {
  const map = useMap();
  useEffect(() => {
    const el = map.getContainer();
    if (typeof ResizeObserver === 'undefined') return undefined;
    const ro = new ResizeObserver(() => map.invalidateSize());
    ro.observe(el);
    return () => ro.disconnect();
  }, [map]);
  return null;
}

/** Leaflet qatlami — `TrackMap` uni `lazy()` bilan yuklaydi. */
export default function TrackMapCanvas({
  place,
  pings,
  checkInAt = null,
  checkOutAt = null,
  maxArrows = 14,
}: TrackMapCanvasProps) {
  const ordered = useMemo(() => sortByTime(pings), [pings]);
  const bounds = useMemo(() => boundsOf(place, ordered), [place, ordered]);
  const line = useMemo<LatLngTuple[]>(() => ordered.map((p) => [p.lat, p.lng]), [ordered]);
  const arrows = useMemo(() => routeArrows(ordered, { maxArrows }), [ordered, maxArrows]);
  const first = ordered[0];
  const last = ordered.length > 1 ? ordered[ordered.length - 1] : undefined;

  return (
    <MapContainer
      bounds={bounds}
      boundsOptions={{ padding: [28, 28], maxZoom: 17 }}
      scrollWheelZoom={false}
      dragging
      touchZoom
      style={{ width: '100%', height: '100%' }}
    >
      <AutoResize />
      <TileLayer url={OSM_URL} attribution={OSM_ATTRIBUTION} maxZoom={19} />
      {place && (
        <>
          <Circle
            center={[place.lat, place.lng]}
            radius={place.radiusM}
            pathOptions={{ className: styles.fence ?? '', weight: 1, fillOpacity: 0.12 }}
          />
          <CircleMarker
            center={[place.lat, place.lng]}
            radius={7}
            pathOptions={{ className: styles.placeDot ?? '', weight: 2, fillOpacity: 1 }}
          >
            <Tooltip>
              {place.name} · radius {fmtDistance(place.radiusM)}
            </Tooltip>
          </CircleMarker>
        </>
      )}
      {line.length > 1 && (
        <Polyline
          positions={line}
          pathOptions={{ className: styles.track ?? '', weight: 3, opacity: 0.8 }}
        />
      )}
      {ordered.map((p) => (
        <CircleMarker
          key={p.occurredAt}
          center={[p.lat, p.lng]}
          radius={p.isMocked ? 6 : 4}
          pathOptions={{
            className: cn(styles[p.status], p.isMocked && styles.mocked),
            weight: p.isMocked ? 2 : 1,
            fillOpacity: 0.95,
          }}
        >
          <Tooltip>
            {fmtTime(p.occurredAt)} · {PING_STATUS_LABEL[p.status]} · {fmtDistance(p.distanceM)}
            {p.status === 'inaccurate' ? ` · aniqlik ${fmtDistance(p.accuracyM)}` : ''}
            {p.isMocked ? ' · Soxta joylashuv' : ''}
          </Tooltip>
        </CircleMarker>
      ))}
      {arrows.map((a, i) => (
        <Marker
          key={`arrow-${i}`}
          position={[a.lat, a.lng]}
          icon={arrowIcon(a.bearing)}
          interactive={false}
          keyboard={false}
        />
      ))}
      {first && (
        <Marker
          position={[first.lat, first.lng]}
          icon={endpointIcon('start')}
          title="Boshlanish"
          zIndexOffset={1000}
        >
          <Tooltip>
            Boshlanish · {fmtTime(first.occurredAt)}
            {checkInAt ? ` (check-in ${fmtTime(checkInAt)})` : ''}
          </Tooltip>
        </Marker>
      )}
      {last && (
        <Marker
          position={[last.lat, last.lng]}
          icon={endpointIcon('end')}
          title={checkOutAt ? 'Tugash (check-out)' : 'Oxirgi nuqta'}
          zIndexOffset={1000}
        >
          <Tooltip>
            {checkOutAt
              ? `Tugash · check-out ${fmtTime(checkOutAt)}`
              : `Oxirgi nuqta · ${fmtTime(last.occurredAt)}`}
          </Tooltip>
        </Marker>
      )}
    </MapContainer>
  );
}
