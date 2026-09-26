import {
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  ErrorState,
  FileBox,
  LoadingState,
  MapPlaceholder,
} from '@/shared/ui';
import { errorMessage, isApiError } from '@/shared/api/client';
import { openExternal } from '@/shared/auth/telegram';
import { formatDate, formatPeriod, formatPhone } from '@/shared/lib/format';
import { PlaceSelectForm } from '@/features/place/components/PlaceSelectForm';
import { usePlaceQuery } from '@/features/place/hooks';
import {
  APPLICATION_STATUS,
  formatTin,
  isPastPlace,
  type PracticeContractDto,
  type PracticePlaceDto,
} from '@/features/place/types';
import { periodPhase } from '@/features/period/types';
import { useTodayQuery } from '@/features/today/hooks';
import pages from './pages.module.css';
import styles from './PlacePage.module.css';

const formatSize = (bytes: number) => `${(bytes / 1_048_576).toFixed(1).replace('.', ',')} MB`;
const formatCoords = (p: PracticePlaceDto) => `${p.lat.toFixed(4)}, ${p.lng.toFixed(4)}`;
const person = (name: string | null, phone: string | null) =>
  name ? (phone ? `${name} · ${formatPhone(phone)}` : name) : '—';

const contractMeta = (c: PracticeContractDto) =>
  [
    c.pages !== null ? `${c.pages} bet` : null,
    formatSize(c.sizeBytes),
    `${formatDate(c.uploadedAt)} da yuklangan`,
  ]
    .filter(Boolean)
    .join(' · ');

/** O'tgan davr ko'rinishidagi neytral holat (ariza holati o'rniga). */
const PAST_BADGE = { label: 'Yakunlangan', kind: 'neu' } as const;

/** SPEC-SCREENS §14 `isJoyim` — Korxonam (avval "Amaliyot joyim"). */
export function PlacePage() {
  const place = usePlaceQuery();
  // Ariza davri: `today.period` (§4.6 "current") — tugagan davrga ariza berilmaydi (POST 409),
  // shuning uchun faqat davom etayotgan yoki kelgusi davr nomi ko'rsatiladi. Place javobida davr nomi yo'q.
  const today = useTodayQuery().data;
  const enrollmentPeriod =
    today?.period && periodPhase(today.period, today.date) !== 'ended' ? today.period : null;

  if (place.isPending) return <LoadingState height={320} />;
  if (place.isError) {
    // Joy hali biriktirilmagan → korxonani STIR orqali tanlash formasi.
    if (isApiError(place.error) && place.error.kind === 'not-found') {
      return (
        <div className={pages.stack}>
          <PlaceSelectForm periodName={enrollmentPeriod?.name ?? null} />
        </div>
      );
    }
    return (
      <ErrorState description={errorMessage(place.error)} onRetry={() => void place.refetch()} />
    );
  }
  const p = place.data;
  // O'tgan (yopilgan/tugagan) davr: talaba hozir bu korxonaga biriktirilmagan — faqat o'qish uchun.
  const isPast = isPastPlace(p);
  const status = isPast ? PAST_BADGE : APPLICATION_STATUS[p.status];
  // Qaytarilgan/rad etilgan ariza — talaba boshqa STIR bilan qayta yuborishi mumkin.
  const canResubmit = !isPast && (p.status === 'revisionNeeded' || p.status === 'rejected');
  // O'tgan davrdan keyin ochiq (davom etayotgan/kelgusi) davr bo'lsa — yangi davr uchun joy tanlash.
  const canSelectForNewPeriod =
    isPast && enrollmentPeriod !== null && enrollmentPeriod.id !== p.periodId;
  const title = isPast ? "O'tgan amaliyot davri" : "Korxona ma'lumotlari";
  const periodDates = formatPeriod(p.periodFrom, p.periodTo);
  const subtitle = isPast
    ? p.periodName
      ? `${p.periodName} · ${periodDates}`
      : periodDates
    : undefined;
  const fields = [
    { k: 'Korxona', v: p.company },
    { k: 'STIR', v: formatTin(p.tin) },
    { k: 'Faoliyat turi', v: p.activity },
    { k: 'Manzil', v: p.address },
    { k: 'Rahbar', v: person(p.supervisorName, p.supervisorPhone) },
    { k: 'Mentor', v: person(p.mentorName, p.mentorPhone) },
    { k: 'Amaliyot davri', v: formatPeriod(p.periodFrom, p.periodTo) },
    { k: 'Geofence radiusi', v: `${p.radiusM} m` },
    { k: 'Koordinata', v: formatCoords(p) },
  ];

  return (
    <div className={pages.stack}>
      <Card aria-label={title}>
        <CardHeader
          title={title}
          subtitle={subtitle}
          actions={<Badge status={status.kind}>{status.label}</Badge>}
        />
        {p.comment && canResubmit && (
          <p className={styles.pendingNote} role="status">
            Tyutor izohi: {p.comment}
          </p>
        )}
        {!isPast && p.status === 'submitted' && (
          <p className={styles.pendingNote} role="status">
            Ariza tyutorga yuborildi — ko'rib chiqilmoqda.
          </p>
        )}
        <dl className={styles.fields}>
          {fields.map((f) => (
            <div key={f.k} className={styles.field}>
              <dt className={styles.k}>{f.k}</dt>
              <dd className={styles.v}>{f.v}</dd>
            </div>
          ))}
        </dl>
        {isPast ? (
          <p className={styles.note} role="status">
            Hozirda siz hech bir korxonaga biriktirilmagansiz.
          </p>
        ) : (
          <p className={styles.note}>
            Tasdiqlangandan keyin koordinatani o'zgartirish mumkin emas. Korxona manzili o'zgargan
            bo'lsa tyutorga murojaat qiling.
          </p>
        )}
      </Card>

      {canResubmit && <PlaceSelectForm resubmit periodName={enrollmentPeriod?.name ?? null} />}
      {canSelectForNewPeriod && <PlaceSelectForm periodName={enrollmentPeriod.name} />}

      {isPast ? (
        p.contract && (
          <Card padded aria-labelledby="contract-title">
            <h2 id="contract-title" className={pages.sectionTitle}>
              Shartnoma
            </h2>
            <FileBox
              className={styles.fileBox}
              name={p.contract.fileName}
              meta={contractMeta(p.contract)}
            />
            {p.contract.approvedAt && (
              <p className={styles.approved}>
                {formatDate(p.contract.approvedAt)} da tyutor {p.contract.approvedBy} tasdiqladi
              </p>
            )}
          </Card>
        )
      ) : (
        <Card padded aria-labelledby="contract-title">
          <h2 id="contract-title" className={pages.sectionTitle}>
            Shartnoma
          </h2>
          {p.contract ? (
            <>
              <FileBox
                className={styles.fileBox}
                name={p.contract.fileName}
                meta={contractMeta(p.contract)}
              />
              {p.contract.approvedAt ? (
                <p className={styles.approved}>
                  {formatDate(p.contract.approvedAt)} da tyutor {p.contract.approvedBy} tasdiqladi
                </p>
              ) : (
                <p className={styles.pendingNote}>Tyutor tasdig'i kutilmoqda</p>
              )}
              {p.contract.templateUrl && (
                <Button
                  size="sm"
                  className={styles.template}
                  onClick={() => openExternal(p.contract!.templateUrl!)}
                >
                  Shablonni yuklab olish
                </Button>
              )}
            </>
          ) : (
            <EmptyState
              className={styles.emptyContract}
              title="Shartnoma hali yuklanmagan"
              description="Shartnomani tyutor orqali yuklang."
            />
          )}
        </Card>
      )}

      <Card padded aria-labelledby="geofence-title">
        <h2 id="geofence-title" className={pages.sectionTitle}>
          Geofence
        </h2>
        <MapPlaceholder
          className={styles.map}
          height={150}
          title={`Xarita (nuqta + ${p.radiusM} m doira)`}
          coords={formatCoords(p)}
        />
      </Card>
    </div>
  );
}

export default PlacePage;
