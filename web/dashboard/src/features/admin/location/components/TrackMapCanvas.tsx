import { useMemo } from 'react';
import type { LatLngBoundsExpression, LatLngTuple } from 'leaflet';
import { Circle, CircleMarker, MapContainer, Polyline, TileLayer, Tooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { cn } from '@/shared/ui';
import { fmtDistance, fmtTime } from '../../shared/fmt';
import { PING_STATUS_LABEL, type LocationPing, type TrackPlace } from '../types';
import styles from './TrackMap.module.css';

const OSM_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION = '&copy; OpenStreetMap';

export interface TrackMapCanvasProps {
  place: TrackPlace | null;
  pings: readonly LocationPing[];
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

/** Leaflet qatlami — `TrackMap` uni `lazy()` bilan yuklaydi. */
export default function TrackMapCanvas({ place, pings }: TrackMapCanvasProps) {
  const bounds = useMemo(() => boundsOf(place, pings), [place, pings]);
  const line = useMemo<LatLngTuple[]>(() => pings.map((p) => [p.lat, p.lng]), [pings]);

  return (
    <MapContainer
      bounds={bounds}
      boundsOptions={{ padding: [28, 28], maxZoom: 17 }}
      scrollWheelZoom={false}
      style={{ width: '100%', height: '100%' }}
    >
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
          pathOptions={{ className: styles.track ?? '', weight: 2, opacity: 0.7 }}
        />
      )}
      {pings.map((p) => (
        <CircleMarker
          key={p.occurredAt}
          center={[p.lat, p.lng]}
          radius={p.isMocked ? 7 : 5}
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
    </MapContainer>
  );
}
