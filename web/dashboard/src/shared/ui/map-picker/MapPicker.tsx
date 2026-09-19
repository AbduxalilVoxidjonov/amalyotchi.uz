import {
  Component,
  lazy,
  Suspense,
  useEffect,
  useId,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from 'react';
import { Button, cn, Input } from '@amaliyotchi/shared/ui';
import styles from './MapPicker.module.css';
import { clampLat, clampLng, type MapPickerProps } from './types';

export type { MapPickerProps, MapPoint } from './types';

/** Leaflet alohida chunk'da — barrel (`@/shared/ui`) orqali hamma sahifaga tushib ketmasin. */
const MapPickerCanvas = lazy(() => import('./MapPickerCanvas'));

/** Geolokatsiya xabarlari — bitta joyda (test ham shu matnlarni kutadi). */
const GEO_UNSUPPORTED = 'Brauzer joylashuvni aniqlashni qo‘llab-quvvatlamaydi.';
const GEO_INSECURE =
  'Joylashuv faqat xavfsiz ulanishda (https) ishlaydi — xaritadan qo‘lda belgilang.';
const GEO_DENIED =
  'Joylashuvga ruxsat berilmadi — brauzer sozlamalaridan ruxsat bering yoki xaritadan belgilang.';
const GEO_FAILED = 'Joylashuvni aniqlab bo‘lmadi. Qaytadan urinib ko‘ring yoki xaritadan belgilang.';

/** `GeolocationPositionError.PERMISSION_DENIED` (jsdom'da konstanta bo'lmasligi mumkin). */
const PERMISSION_DENIED = 1;
/** Shu chegaradan yomon aniqlikda markerni qo'lda to'g'rilash taklif qilinadi. */
const ACCURACY_WARN_M = 200;
const GEO_OPTIONS: PositionOptions = {
  enableHighAccuracy: true,
  timeout: 10_000,
  maximumAge: 0,
};

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

  // Geolokatsiya holati: so'rov ketayotgani, xato matni va yomon aniqlik ogohlantirishi.
  const [locating, setLocating] = useState(false);
  const [geoError, setGeoError] = useState<string | null>(null);
  const [accuracyM, setAccuracyM] = useState<number | null>(null);

  // Javob unmount'dan keyin kelishi mumkin — `setState` chaqirilmasin.
  const aliveRef = useRef(true);
  useEffect(() => {
    aliveRef.current = true;
    return () => {
      aliveRef.current = false;
    };
  }, []);

  function locate() {
    if (disabled || locating) return;
    setAccuracyM(null);
    // https (yoki localhost) bo'lmasa brauzer so'rovni umuman bermaydi — darhol tushuntiramiz.
    if (typeof window !== 'undefined' && window.isSecureContext === false) {
      setGeoError(GEO_INSECURE);
      return;
    }
    const geo = typeof navigator === 'undefined' ? undefined : navigator.geolocation;
    if (!geo) {
      setGeoError(GEO_UNSUPPORTED);
      return;
    }
    setGeoError(null);
    setLocating(true);
    geo.getCurrentPosition(
      (position) => {
        if (!aliveRef.current) return;
        setLocating(false);
        const { latitude, longitude, accuracy } = position.coords;
        setAccuracyM(
          Number.isFinite(accuracy) && accuracy > ACCURACY_WARN_M ? Math.round(accuracy) : null,
        );
        // Markaz/zoom `value` o'zgarishi bilan `MapPickerCanvas` ichida sinxronlanadi.
        onChange({ lat: clampLat(latitude), lng: clampLng(longitude) });
      },
      (err) => {
        if (!aliveRef.current) return;
        setLocating(false);
        setGeoError(err?.code === PERMISSION_DENIED ? GEO_DENIED : GEO_FAILED);
      },
      GEO_OPTIONS,
    );
  }

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

      <div className={styles.actions}>
        <Button variant="secondary" size="xs" onClick={locate} disabled={disabled || locating}>
          {locating ? 'Aniqlanmoqda…' : 'Hozirgi joylashuvim'}
        </Button>
        <p id={valueId} className={cn(styles.coords, lat === null ? styles.coordsEmpty : '')}>
          {lat !== null && lng !== null ? formatPoint(lat, lng) : 'Xaritadan joyni belgilang'}
        </p>
      </div>

      {accuracyM !== null && (
        <p className={styles.hint}>
          Aniqlik ~{accuracyM} m — kerak bo‘lsa markerni sudrab to‘g‘rilang.
        </p>
      )}

      {geoError && (
        <p role="alert" className={styles.error}>
          {geoError}
        </p>
      )}

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
