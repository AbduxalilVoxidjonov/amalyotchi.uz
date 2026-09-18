import {
  Component,
  lazy,
  Suspense,
  useEffect,
  useId,
  useState,
  type CSSProperties,
  type ReactNode,
} from 'react';
import { cn, Input } from '@amaliyotchi/shared/ui';
import styles from './MapPicker.module.css';
import type { MapPickerProps } from './types';

export type { MapPickerProps, MapPoint } from './types';

/** Leaflet alohida chunk'da — barrel (`@/shared/ui`) orqali hamma sahifaga tushib ketmasin. */
const MapPickerCanvas = lazy(() => import('./MapPickerCanvas'));

/** Koordinatani ko'rsatish formati: `41.31105, 69.27972`. */
function formatPoint(lat: number, lng: number): string {
  return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

/**
 * Xarita chunk'i yuklanmasa (offline, CDN/asset xatosi) butun forma yiqilmasligi kerak —
 * shu blok o'rniga izoh chiqadi, koordinata esa "qo'lda kiritish" orqali kiritiladi.
 */
class MapBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (this.state.failed) {
      return (
        <p className={styles.fallback}>
          Xaritani yuklab bo‘lmadi. Koordinatani quyidagi “Koordinatani qo‘lda kiritish” bo‘limidan
          kiriting.
        </p>
      );
    }
    return this.props.children;
  }
}

/**
 * Xaritadan koordinata tanlash (Leaflet + OpenStreetMap). Asosiy yo'l — xaritaga bosish yoki
 * markerni sudrash; zaxira yo'l (klaviatura, xarita yuklanmagan holat) — "Koordinatani qo'lda
 * kiritish" bloki (default yopiq).
 */
export function MapPicker({
  value,
  onChange,
  radiusM,
  label,
  height = 280,
  disabled = false,
  error,
}: MapPickerProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const valueId = `${id}-value`;
  const lat = value ? value.lat : null;
  const lng = value ? value.lng : null;

  // Qo'lda kiritish inputlari — xaritadan kelgan qiymat bilan sinxron.
  const [manual, setManual] = useState({ lat: '', lng: '' });
  useEffect(() => {
    setManual(
      lat === null || lng === null ? { lat: '', lng: '' } : { lat: `${lat}`, lng: `${lng}` },
    );
  }, [lat, lng]);

  function applyManual(next: { lat: string; lng: string }) {
    setManual(next);
    if (disabled) return;
    const rawLat = next.lat.trim().replace(',', '.');
    const rawLng = next.lng.trim().replace(',', '.');
    if (rawLat === '' || rawLng === '') return;
    const nextLat = Number(rawLat);
    const nextLng = Number(rawLng);
    if (!Number.isFinite(nextLat) || !Number.isFinite(nextLng)) return;
    if (nextLat < -90 || nextLat > 90 || nextLng < -180 || nextLng > 180) return;
    onChange({ lat: nextLat, lng: nextLng });
  }

  return (
    <div className={styles.wrap}>
      <span className={styles.label}>{label}</span>
      <div
        className={styles.map}
        style={{ '--map-picker-h': `${height}px` } as CSSProperties}
        role="application"
        aria-label={label}
        aria-describedby={cn(valueId, error ? errorId : '')}
        data-disabled={disabled || undefined}
      >
        <MapBoundary>
          <Suspense fallback={<p className={styles.fallback}>Xarita yuklanmoqda…</p>}>
            <MapPickerCanvas
              lat={lat}
              lng={lng}
              radiusM={radiusM}
              disabled={disabled}
              onPick={onChange}
            />
          </Suspense>
        </MapBoundary>
      </div>

      <p id={valueId} className={cn(styles.coords, lat === null ? styles.coordsEmpty : '')}>
        {lat !== null && lng !== null ? formatPoint(lat, lng) : 'Xaritadan joyni belgilang'}
      </p>

      <details className={styles.manual}>
        <summary className={styles.summary}>Koordinatani qo‘lda kiritish</summary>
        <div className={styles.manualRow}>
          <Input
            id={`${id}-lat`}
            label="Kenglik (lat)"
            variant="form"
            mono
            inputMode="decimal"
            autoComplete="off"
            placeholder="41.3111"
            value={manual.lat}
            onChange={(e) => applyManual({ ...manual, lat: e.target.value })}
            disabled={disabled}
          />
          <Input
            id={`${id}-lng`}
            label="Uzunlik (lng)"
            variant="form"
            mono
            inputMode="decimal"
            autoComplete="off"
            placeholder="69.2797"
            value={manual.lng}
            onChange={(e) => applyManual({ ...manual, lng: e.target.value })}
            disabled={disabled}
          />
        </div>
      </details>

      {error && (
        <p id={errorId} className={styles.error}>
          {error}
        </p>
      )}
    </div>
  );
}
