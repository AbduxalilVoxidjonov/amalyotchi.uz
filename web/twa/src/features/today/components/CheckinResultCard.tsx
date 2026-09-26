import type { ReactNode } from 'react';
import { Badge, Card, Eyebrow, FactGrid, type FactItem } from '@/shared/ui';
import { formatDate, formatMeters, formatTime } from '@/shared/lib/format';
import { ATTENDANCE_STATUS, type TodayDto } from '../types';
import styles from './CheckinResultCard.module.css';

export interface CheckinResultCardProps {
  today: TodayDto;
  title: string;
  note?: string | undefined;
  /** Muvaffaqiyat ekrani — katta ✓ belgi va `role="status"`. */
  success?: boolean;
  /** Pastdagi amal (masalan "Bosh ekranga"). */
  action?: ReactNode;
}

/**
 * Bugungi davomat natijasi (presentation): kelgan/ketgan vaqtlari, holat chip'i (Keldi / Kech keldi),
 * masofa. QR sahifasida muvaffaqiyat ekrani va "kun yakunlangan" holati shu karta.
 */
export function CheckinResultCard({
  today,
  title,
  note,
  success = false,
  action,
}: CheckinResultCardProps) {
  const { checkin } = today;
  const status = ATTENDANCE_STATUS[checkin.status];
  const autoClosed = checkin.autoClosed && !checkin.checkOutAt;
  const facts: FactItem[] = [
    { k: 'Kelgan vaqt', v: formatTime(checkin.checkInAt) || '—' },
    {
      k: 'Ketgan vaqt',
      v: autoClosed ? 'Avtomatik yopildi' : formatTime(checkin.checkOutAt) || '—',
      ...(autoClosed ? { tone: 'late' as const } : {}),
    },
    { k: 'Masofa', v: `${formatMeters(checkin.distanceM)} / ${formatMeters(checkin.radiusM)}` },
  ];

  return (
    <Card padded="lg" aria-labelledby="checkin-result-title">
      <div className={styles.head} role={success ? 'status' : undefined}>
        {success && (
          <span className={styles.check} aria-hidden="true">
            ✓
          </span>
        )}
        <div className={styles.headText}>
          <Eyebrow as="div" spacing="wide" margin="none">
            Bugun · {formatDate(today.date)}
          </Eyebrow>
          <h2 id="checkin-result-title" className={styles.title}>
            {title}
          </h2>
        </div>
        <Badge status={statusKind(status.tone)} size="lg">
          {status.label}
        </Badge>
      </div>
      {note && <p className={styles.note}>{note}</p>}

      <FactGrid items={facts} columns={2} className={styles.facts} />

      {checkin.suspicious && (
        <p className={styles.warn} role="note">
          Belgilanish shubhali deb belgilandi — tyutor tekshiradi.
        </p>
      )}
      {action && <div className={styles.action}>{action}</div>}
    </Card>
  );
}

/** FactGrid tonusi → Badge statusi. */
function statusKind(tone: NonNullable<FactItem['tone']>): 'ok' | 'late' | 'bad' | 'neu' {
  if (tone === 'ok' || tone === 'late' || tone === 'bad') return tone;
  return 'neu';
}
