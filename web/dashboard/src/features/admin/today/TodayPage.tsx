import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { TopbarActions } from '@/app/layout';
import { Button, Pill, PillGroup, StatGrid, StatTile } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { AlertBanner } from './components/AlertBanner';
import { AttendanceTable } from './components/AttendanceTable';
import { useTodayQuery } from './hooks';
import { buildStatTiles, presentAlert } from './present';
import { ATTENDANCE_FILTERS, isAttendanceFilter, type AttendanceFilter } from './types';
import styles from './TodayPage.module.css';

type View = 'list' | 'company';

/** views (SPEC-NAV §4.1): Kalendar/Xarita → alohida ekran; Korxona → korxona bo'yicha saralash ❓. */
const VIEWS: { key: View | 'calendar' | 'map'; label: string; to?: string }[] = [
  { key: 'list', label: "Ro'yxat" },
  { key: 'calendar', label: 'Kalendar', to: '/tutor/calendar' },
  { key: 'map', label: 'Xarita', to: '/tutor/map' },
  { key: 'company', label: 'Korxona' },
];

/** Tyutor · Bugun (SPEC-SCREENS §3) — container. */
export function TodayPage() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const status: AttendanceFilter = isAttendanceFilter(params.get('status'))
    ? (params.get('status') as AttendanceFilter)
    : 'all';
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1);
  const [view, setView] = useState<View>('list');
  const [alertsDismissed, setAlertsDismissed] = useState(false);

  const query = useTodayQuery({ status, page });

  const setFilter = (next: AttendanceFilter) => {
    const sp = new URLSearchParams(params);
    if (next === 'all') sp.delete('status');
    else sp.set('status', next);
    sp.delete('page');
    setParams(sp);
  };
  const setPage = (next: number) => {
    const sp = new URLSearchParams(params);
    if (next <= 1) sp.delete('page');
    else sp.set('page', String(next));
    setParams(sp);
  };

  const rows = useMemo(() => {
    const list = query.data?.rows.items ?? [];
    return view === 'company'
      ? [...list].sort((a, b) => (a.company ?? '').localeCompare(b.company ?? '', 'uz'))
      : list;
  }, [query.data, view]);
  const alerts = useMemo(() => (query.data?.alerts ?? []).map(presentAlert), [query.data]);

  const toolbar = (
    <>
      <PillGroup aria-label="Kun filtri">
        {ATTENDANCE_FILTERS.map((f) => (
          <Pill key={f.value} active={status === f.value} onClick={() => setFilter(f.value)}>
            {f.label}
          </Pill>
        ))}
      </PillGroup>
      <PillGroup aria-label="Ko'rinish">
        {VIEWS.map((v) => (
          <Pill
            key={v.key}
            shape="square"
            active={v.key === view}
            onClick={() => (v.to ? void navigate(v.to) : setView(v.key as View))}
          >
            {v.label}
          </Pill>
        ))}
      </PillGroup>
    </>
  );

  return (
    <div className={styles.page}>
      <TopbarActions>
        {/* TODO: Eksport — dizaynda handler yo'q ❓ (Excel / hisobot ekrani). */}
        <Button size="sm">Eksport</Button>
      </TopbarActions>
      <QueryState
        status={query.status}
        data={query.data}
        error={query.error}
        refetch={query.refetch}
      >
        {(data) => (
          <>
            {/* min 140: 5 ta tile 780px kenglikda ham bir qatorda */}
            <StatGrid min={140}>
              {buildStatTiles(data.stats).map((s) => (
                <StatTile key={s.key} label={s.label} value={s.value} note={s.note} dot={s.dot} />
              ))}
            </StatGrid>
            {!alertsDismissed && (
              <AlertBanner alerts={alerts} onDismiss={() => setAlertsDismissed(true)} />
            )}
            <AttendanceTable
              rows={rows}
              pagination={data.rows}
              toolbar={toolbar}
              onPageChange={setPage}
            />
          </>
        )}
      </QueryState>
    </div>
  );
}

export default TodayPage;
