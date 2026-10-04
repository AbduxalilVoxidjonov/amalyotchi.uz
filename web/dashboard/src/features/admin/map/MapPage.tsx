import { MapPlaceholder } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { fmtDayMonth } from '../format';
import { MapPointList } from './components/MapPointList';
import { useMapQuery } from './hooks';
import styles from './MapPage.module.css';

/**
 * Tyutor · Xarita (SPEC-SCREENS §11) — container.
 * ❓ Xarita kutubxonasi (Leaflet/Yandex) hali tanlanmagan — MapPlaceholder; `points[].lat/lng` tayyor.
 */
export function MapPage() {
  const query = useMapQuery();
  return (
    <QueryState status={query.status} data={query.data} error={query.error} refetch={query.refetch}>
      {(data) => (
        <div className={styles.grid}>
          <MapPlaceholder
            className={styles.map}
            height={460}
            title="Xarita maydoni"
            note={
              <span className={styles.note}>
                Bugungi belgilanish nuqtalari korxona doiralari bilan shu yerda ko'rsatiladi. Real
                karta ulanganda bu blok o'rnini oladi.
              </span>
            }
          />
          <MapPointList
            title={`Bugungi nuqtalar · ${fmtDayMonth(data.date)}`}
            points={data.points}
          />
        </div>
      )}
    </QueryState>
  );
}

export default MapPage;
