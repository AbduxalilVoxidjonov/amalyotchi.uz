import { Component, lazy, Suspense, type ReactNode } from 'react';
import { MapPlaceholder } from '@/shared/ui';
import type { LocationPing, TrackPlace } from '../types';
import styles from './TrackMap.module.css';

/** Leaflet alohida chunk'da (MapPicker kabi) — Bugun sahifasi bundle'iga tushmaydi. */
const TrackMapCanvas = lazy(() => import('./TrackMapCanvas'));

const MAP_HEIGHT = 420;

class MapBoundary extends Component<{ children: ReactNode; height: number }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (this.state.failed) {
      return (
        <MapPlaceholder
          height={this.props.height}
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
  checkInAt?: string | null | undefined;
  checkOutAt?: string | null | undefined;
  /** px; sukut 420, modalda kichikroq. */
  height?: number | undefined;
  /** Yo'nalish strelkalari soni chegarasi. */
  maxArrows?: number | undefined;
  /** Belgilar xarita ustida emas, ostida (kichik xaritada xaritani to'sib qo'ymasin). */
  legendBelow?: boolean | undefined;
}

/**
 * Kunlik iz xaritasi: korxona nuqtasi + radius, ping'lar holat rangida, vaqt bo'yicha chiziq,
 * yo'nalish strelkalari, boshlanish (A) va tugash (B) belgilari.
 */
export function TrackMap({
  place,
  pings,
  checkInAt,
  checkOutAt,
  height = MAP_HEIGHT,
  maxArrows,
  legendBelow = false,
}: TrackMapProps) {
  if (!place && pings.length === 0) {
    return (
      <MapPlaceholder
        height={height}
        title="Xaritada ko'rsatiladigan nuqta yo'q"
        note="Korxona joylashuvi kiritilmagan va bu kunda ping kelmagan."
      />
    );
  }
  const legend = (
    <ul className={legendBelow ? styles.legendBelow : styles.legend} aria-label="Xarita belgilari">
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
      {pings.length > 0 && (
        <>
          <li>
            <span className={styles.dot} data-kind="start" aria-hidden /> A — boshlanish
          </li>
          <li>
            <span className={styles.dot} data-kind="end" aria-hidden /> B — tugash
          </li>
          <li>
            <span className={styles.arrowLegend} aria-hidden>
              ➤
            </span>{' '}
            Yo&apos;nalish
          </li>
        </>
      )}
    </ul>
  );
  const map = (
    <div className={styles.wrap} style={{ height }}>
      <MapBoundary height={height}>
        <Suspense fallback={<div className={styles.loading}>Xarita yuklanmoqda…</div>}>
          <TrackMapCanvas
            place={place}
            pings={pings}
            checkInAt={checkInAt}
            checkOutAt={checkOutAt}
            maxArrows={maxArrows}
          />
        </Suspense>
      </MapBoundary>
      {!legendBelow && legend}
    </div>
  );
  if (!legendBelow) return map;
  return (
    <div className={styles.stack}>
      {map}
      {legend}
    </div>
  );
}
