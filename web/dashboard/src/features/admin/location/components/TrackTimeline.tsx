import type { CSSProperties } from 'react';
import { Card, CardBody, CardHeader } from '@/shared/ui';
import { fmtTime } from '../../shared/fmt';
import { fmtMinutes, outsideIntervals, type SegmentKind, type Timeline } from '../derive';
import styles from './TrackTimeline.module.css';

const KIND_LABEL: Record<SegmentKind, string> = {
  inside: 'Ichkarida',
  outside: 'Tashqarida',
  inaccurate: 'Aniqlik past',
  gap: "Signal yo'q",
};

const iso = (ms: number) => new Date(ms).toISOString();

export interface TrackTimelineProps {
  timeline: Timeline | null;
  checkInAt: string | null;
  checkOutAt: string | null;
}

/** Vaqt chizig'i: check-in → … → check-out; tashqarida bo'lgan oraliqlar qizil. */
export function TrackTimeline({ timeline, checkInAt, checkOutAt }: TrackTimelineProps) {
  const intervals = outsideIntervals(timeline);
  return (
    <Card as="section" aria-labelledby="track-timeline-title">
      <CardHeader title={<span id="track-timeline-title">Vaqt chizig'i</span>} />
      <CardBody className={styles.body}>
        {!timeline ? (
          <p className={styles.empty}>Bu kunda check-in va joylashuv ma'lumoti yo'q.</p>
        ) : (
          <>
            <div className={styles.ends}>
              <span>Check-in {checkInAt ? fmtTime(checkInAt) : '—'}</span>
              <span>
                {checkOutAt
                  ? `Check-out ${fmtTime(checkOutAt)}`
                  : `Hozir ${fmtTime(iso(timeline.end))}`}
              </span>
            </div>
            <ol className={styles.bar} aria-label="Kun bo'yicha holatlar">
              {timeline.segments.map((s) => {
                const total = timeline.end - timeline.start || 1;
                const style = {
                  left: `${((s.from - timeline.start) / total) * 100}%`,
                  width: `${((s.to - s.from) / total) * 100}%`,
                } as CSSProperties;
                const text = `${fmtTime(iso(s.from))}–${fmtTime(iso(s.to))} · ${KIND_LABEL[s.kind]}`;
                return (
                  <li
                    key={`${s.kind}-${s.from}`}
                    className={styles.segment}
                    data-kind={s.kind}
                    style={style}
                    title={text}
                    aria-label={text}
                  />
                );
              })}
            </ol>
            <ul className={styles.legend} aria-hidden>
              {(['inside', 'outside', 'inaccurate', 'gap'] as const).map((k) => (
                <li key={k}>
                  <span className={styles.swatch} data-kind={k} />
                  {KIND_LABEL[k]}
                </li>
              ))}
            </ul>
            <h3 className={styles.subTitle}>Tashqarida bo'lgan oraliqlar</h3>
            {intervals.length === 0 ? (
              <p className={styles.empty}>Talaba korxona hududidan chiqmagan.</p>
            ) : (
              <ul className={styles.intervals}>
                {intervals.map((it) => (
                  <li key={it.from} className={styles.interval}>
                    <span className={styles.intervalTime}>{it.label}</span>
                    <span>{fmtMinutes(it.minutes)}</span>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </CardBody>
    </Card>
  );
}
