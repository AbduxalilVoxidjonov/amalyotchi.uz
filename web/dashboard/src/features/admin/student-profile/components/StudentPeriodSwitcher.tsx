import { useRef, type KeyboardEvent } from 'react';
import { Badge, EmptyState } from '@/shared/ui';
import { fmtPeriodDates, PERIOD_PHASE_LABEL, periodPhase } from '../periods';
import type { StudentPeriodOption } from '../types';
import styles from './StudentPeriodSwitcher.module.css';

export interface StudentPeriodSwitcherProps {
  periods: readonly StudentPeriodOption[];
  /** Ko'rsatilayotgan (yoki hozir yuklanayotgan) davr. */
  selectedId: string | null;
  onSelect: (periodId: string) => void;
  /** Toshkent bo'yicha bugun (DateOnly) — "Tugagan" belgisi uchun. */
  today: string;
  /** Tanlangan davr hali yuklanmoqda — tugmalar `aria-busy`. */
  pending?: boolean;
}

function PhaseBadge({ period, today }: { period: StudentPeriodOption; today: string }) {
  const phase = PERIOD_PHASE_LABEL[periodPhase(period, today)];
  return (
    <Badge status={phase.kind} size="sm">
      {phase.label}
    </Badge>
  );
}

/**
 * Talaba profili sarlavhasi ostidagi davr tanlagichi (API v3.5, §4.6):
 * 2+ davr — segmentlar (nom, sanalar, holat); 1 davr — ma'lumot qatori; 0 — bo'sh holat.
 */
export function StudentPeriodSwitcher({
  periods,
  selectedId,
  onSelect,
  today,
  pending = false,
}: StudentPeriodSwitcherProps) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);

  if (periods.length === 0) {
    return (
      <EmptyState
        className={styles.empty}
        title="Talabaga hali amaliyot davri biriktirilmagan"
        description="Davr talaba guruhiga biriktirilgach, davomat va kundaliklar shu yerda ko'rinadi."
      />
    );
  }

  if (periods.length === 1) {
    const only = periods[0]!;
    return (
      <div className={styles.single} role="group" aria-label="Amaliyot davri">
        <span className={styles.singleLabel}>Amaliyot davri</span>
        <span className={styles.name}>{only.name}</span>
        <span className={styles.dates}>{fmtPeriodDates(only)}</span>
        <PhaseBadge period={only} today={today} />
      </div>
    );
  }

  const activeIndex = Math.max(
    0,
    periods.findIndex((p) => p.id === selectedId),
  );

  // Tab naqshi: chap/o'ng o'qlar bilan fokus va tanlov ko'chadi (WAI-ARIA tabs).
  function onKeyDown(e: KeyboardEvent<HTMLButtonElement>, index: number) {
    const n = periods.length;
    const next =
      e.key === 'ArrowRight'
        ? (index + 1) % n
        : e.key === 'ArrowLeft'
          ? (index - 1 + n) % n
          : e.key === 'Home'
            ? 0
            : e.key === 'End'
              ? n - 1
              : null;
    if (next === null || next === index) return;
    e.preventDefault();
    const target = periods[next];
    if (!target) return;
    refs.current[next]?.focus();
    onSelect(target.id);
  }

  return (
    <div className={styles.tabs} role="tablist" aria-label="Amaliyot davrlari">
      {periods.map((p, i) => {
        const selected = i === activeIndex;
        return (
          <button
            key={p.id}
            ref={(el) => {
              refs.current[i] = el;
            }}
            type="button"
            role="tab"
            aria-selected={selected}
            aria-busy={selected && pending ? true : undefined}
            tabIndex={selected ? 0 : -1}
            className={styles.tab}
            title={p.isDefault ? "Sukut bo'yicha ko'rsatiladigan davr" : undefined}
            onClick={() => {
              if (!selected) onSelect(p.id);
            }}
            onKeyDown={(e) => onKeyDown(e, i)}
          >
            <span className={styles.tabTop}>
              <span className={styles.name}>{p.name}</span>
              <PhaseBadge period={p} today={today} />
            </span>
            <span className={styles.dates}>{fmtPeriodDates(p)}</span>
          </button>
        );
      })}
    </div>
  );
}
