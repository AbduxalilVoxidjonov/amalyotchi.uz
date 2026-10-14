import { Card, CardBody, CardHeader, FactGrid } from '@/shared/ui';
import { fmtTime } from '../../shared/fmt';
import { fmtMinutes } from '../derive';
import type { LocationPingsSummary } from '../types';

export interface TrackSummaryProps {
  summary: LocationPingsSummary;
  mockedCount: number;
}

/** Kun xulosasi: jami · ichkarida · tashqarida · tashqarida daqiqa · eng uzun uzilish · oxirgi ping. */
export function TrackSummary({ summary, mockedCount }: TrackSummaryProps) {
  const items = [
    { k: 'Jami ping', v: String(summary.total) },
    { k: 'Ichkarida', v: String(summary.inside) },
    { k: 'Tashqarida', v: String(summary.outside) },
    { k: 'Aniqlik past', v: String(summary.inaccurate) },
    { k: 'Tashqarida vaqt', v: fmtMinutes(summary.outsideMinutes) },
    { k: 'Eng uzun uzilish', v: fmtMinutes(summary.longestGapMinutes) },
    { k: 'Oxirgi ping', v: summary.lastPingAt ? fmtTime(summary.lastPingAt) : '—' },
    { k: 'Soxta ping', v: String(mockedCount) },
  ];
  return (
    <Card as="section" aria-labelledby="track-summary-title">
      <CardHeader title={<span id="track-summary-title">Kun xulosasi</span>} />
      <CardBody>
        <FactGrid items={items} min={130} aria-label="Kun xulosasi" />
      </CardBody>
    </Card>
  );
}
