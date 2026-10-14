import { Component, lazy, Suspense, type ReactNode } from 'react';
import { MapPlaceholder } from '@/shared/ui';
import type { LocationPing, TrackPlace } from '../types';
import styles from './TrackMap.module.css';

/** Leaflet alohida chunk'da (MapPicker kabi) — Bugun sahifasi bundle'iga tushmaydi. */
const TrackMapCanvas = lazy(() => import('./TrackMapCanvas'));

const MAP_HEIGHT = 420;

class MapBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (this.state.failed) {
      return (
        <MapPlaceholder
          height={MAP_HEIGHT}
          title="Xaritani yuklab bo'lmadi"
          note="Xulosa va vaqt chizig'i quyida ko'rsatilgan."
        />
      );
    }
    return this.props.children;
  }
}

export interface TrackMapProps {
  place: TrackPlace | null;
  pings: readonly LocationPing[];
}

/** Kunlik iz xaritasi: korxona nuqtasi + radius, ping'lar holat rangida, vaqt bo'yicha chiziq. */
export function TrackMap({ place, pings }: TrackMapProps) {
  if (!place && pings.length === 0) {
    return (
      <MapPlaceholder
        height={MAP_HEIGHT}
        title="Xaritada ko'rsatiladigan nuqta yo'q"
        note="Korxona joylashuvi kiritilmagan va bu kunda ping kelmagan."
      />
    );
  }
  return (
    <div className={styles.wrap} style={{ height: MAP_HEIGHT }}>
      <MapBoundary>
        <Suspense fallback={<div className={styles.loading}>Xarita yuklanmoqda…</div>}>
          <TrackMapCanvas place={place} pings={pings} />
        </Suspense>
      </MapBoundary>
      <ul className={styles.legend} aria-label="Xarita belgilari">
        <li>
          <span className={styles.dot} data-kind="inside" aria-hidden /> Ichkarida
        </li>
        <li>
          <span className={styles.dot} data-kind="outside" aria-hidden /> Tashqarida
        </li>
        <li>
          <span className={styles.dot} data-kind="inaccurate" aria-hidden /> Aniqlik past
        </li>
        <li>
          <span className={styles.dot} data-kind="mocked" aria-hidden /> Soxta joylashuv
        </li>
      </ul>
    </div>
  );
}
