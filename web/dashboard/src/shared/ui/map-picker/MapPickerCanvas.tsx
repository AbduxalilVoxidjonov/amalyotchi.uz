import { useEffect, useRef } from 'react';
import * as L from 'leaflet';
import { Circle, MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import styles from './MapPicker.module.css';
import { clampLat, clampLng, PICKED_ZOOM, START_ZOOM, TASHKENT, type MapPoint } from './types';

/**
 * Leaflet qatlami — `MapPicker` uni `lazy()` bilan yuklaydi, shuning uchun leaflet kodi
 * `@/shared/ui` barrel'i orqali hamma sahifaga tushmaydi (alohida chunk).
 */

const OSM_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION = '&copy; OpenStreetMap';

/**
 * Standart Leaflet marker ikonkasi bundler'da sinadi (`leaflet/dist/images/marker-icon.png`
 * CSS'dan nisbiy yo'l bilan izlanadi) — shuning uchun ikonka `divIcon` bilan CSS'dan chiziladi:
 * na tashqi rasm, na `data:` URI kerak.
 */
const PIN_ICON = L.divIcon({
  className: styles.pinWrap,
  html: `<span class="${styles.pin}"></span>`,
  iconSize: [22, 22],
  iconAnchor: [11, 11],
});

/** Xaritaga bosilganda nuqtani belgilaydi (react-leaflet'da hodisa faqat bola komponentdan olinadi). */
function ClickCatcher({ disabled, onPick }: { disabled: boolean; onPick: (p: MapPoint) => void }) {
  useMapEvents({
    click(e) {
      if (disabled) return;
      onPick({ lat: clampLat(e.latlng.lat), lng: clampLng(e.latlng.lng) });
    },
  });
  return null;
}

/**
 * Birinchi nuqta paydo bo'lganda (klik yoki tahrirlashda yuklangan qiymat) xarita o'sha yerga
 * yaqinlashadi; keyin foydalanuvchi zoom'i saqlanadi va faqat nuqta ko'rinmay qolgan bo'lsa
 * markaz suriladi. Modal ichida ochilgani uchun konteyner o'lchami qayta hisoblanadi.
 */
function ViewSync({ lat, lng }: { lat: number | null; lng: number | null }) {
  const map = useMap();
  const zoomedRef = useRef(false);

  useEffect(() => {
    // Modal animatsiyasi/layout tugagach o'lcham o'zgaradi — plitalar bo'sh qolmasin.
    const id = window.setTimeout(() => map.invalidateSize(), 60);
    return () => window.clearTimeout(id);
  }, [map]);

  useEffect(() => {
    if (lat === null || lng === null) {
      zoomedRef.current = false;
      return;
    }
    const latLng = L.latLng(lat, lng);
    if (!zoomedRef.current) {
      zoomedRef.current = true;
      map.setView(latLng, Math.max(map.getZoom(), PICKED_ZOOM));
    } else if (!map.getBounds().contains(latLng)) {
      map.panTo(latLng);
    }
  }, [map, lat, lng]);

  return null;
}

export interface MapPickerCanvasProps {
  lat: number | null;
  lng: number | null;
  radiusM: number | undefined;
  disabled: boolean;
  onPick: (point: MapPoint) => void;
}

export default function MapPickerCanvas({
  lat,
  lng,
  radiusM,
  disabled,
  onPick,
}: MapPickerCanvasProps) {
  const center: [number, number] =
    lat !== null && lng !== null ? [lat, lng] : [TASHKENT.lat, TASHKENT.lng];

  return (
    <MapContainer
      center={center}
      zoom={lat !== null ? PICKED_ZOOM : START_ZOOM}
      scrollWheelZoom={!disabled}
      dragging={!disabled}
      doubleClickZoom={!disabled}
      className={styles.canvas ?? ''}
    >
      <TileLayer url={OSM_URL} attribution={OSM_ATTRIBUTION} maxZoom={19} />
      <ClickCatcher disabled={disabled} onPick={onPick} />
      <ViewSync lat={lat} lng={lng} />
      {lat !== null && lng !== null && (
        <>
          {radiusM ? (
            <Circle
              center={[lat, lng]}
              radius={radiusM}
              pathOptions={{ className: styles.fence, weight: 1, fillOpacity: 0.12 }}
            />
          ) : null}
          <Marker
            position={[lat, lng]}
            icon={PIN_ICON}
            draggable={!disabled}
            eventHandlers={{
              dragend(e) {
                const p = (e.target as L.Marker).getLatLng();
                onPick({ lat: clampLat(p.lat), lng: clampLng(p.lng) });
              },
            }}
          />
        </>
      )}
    </MapContainer>
  );
}
