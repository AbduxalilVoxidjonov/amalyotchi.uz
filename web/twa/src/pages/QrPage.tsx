import { AppLink, Button, EmptyState, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { formatTime } from '@/shared/lib/format';
import { CheckinCard } from '@/features/today/components/CheckinCard';
import { CheckinResultCard } from '@/features/today/components/CheckinResultCard';
import { PeriodGapCard } from '@/features/today/components/PeriodGapCard';
import { useCheckinFlow, useTodayQuery, type CheckinResult } from '@/features/today/hooks';
import { describeToday, qrView } from '@/features/today/status';
import type { TodayDto } from '@/features/today/types';
import styles from './pages.module.css';

/**
 * "QR" bo'limi — davomatni belgilash (bitta joy). Bugungi holatga (`GET /api/student/today`) qarab:
 *  - kelmagan va oyna ochiq → "Kelganini belgilash": 1) korxonadagi QR → 2) selfi → 3) joylashuv → yuborish;
 *  - kelgan, check-out oynasi ochiq → "Ketganini belgilash" (xuddi shu qadamlar, `POST /checkout`);
 *  - ikkalasi bajarilgan → yakuniy holat (vaqtlar, holat chip'i);
 *  - davr yo'q / davrlar oralig'i / korxona yo'q / dam olish / oyna yopiq → tushunarli bo'sh holat.
 * Muvaffaqiyatdan keyin — natija ekrani va "Bosh ekranga" (`today` keshi yangilanadi, `period-days` invalidate).
 */
export function QrPage() {
  const today = useTodayQuery();
  const flow = useCheckinFlow();

  if (flow.phase === 'done' && flow.result) {
    return (
      <div className={styles.stack}>
        <SuccessCard result={flow.result} />
      </div>
    );
  }
  if (today.isPending) return <LoadingState height={360} />;
  if (today.isError) {
    return (
      <ErrorState description={errorMessage(today.error)} onRetry={() => void today.refetch()} />
    );
  }

  const data = today.data;
  const view = qrView(data);
  // Eskirgan ekran xatosi (masalan, oyna yopilgan) — bugungi holat yangilangach shu yerda ko'rinadi.
  const staleError =
    flow.phase === 'idle' &&
    flow.error &&
    view.kind !== 'checkin' &&
    !(view.kind === 'checkout' && view.open)
      ? flow.error
      : null;

  return (
    <div className={styles.stack}>
      {staleError && <ErrorState title="Belgilab bo‘lmadi" description={staleError} />}
      <QrContent today={data} view={view} flow={flow} />
    </div>
  );
}

function QrContent({
  today,
  view,
  flow,
}: {
  today: TodayDto;
  view: ReturnType<typeof qrView>;
  flow: ReturnType<typeof useCheckinFlow>;
}) {
  const { checkin, window: win } = today;
  switch (view.kind) {
    case 'checkin':
      return <CheckinCard today={today} mode="checkin" flow={flow} />;
    case 'checkout':
      return view.open ? (
        <CheckinCard today={today} mode="checkout" flow={flow} />
      ) : (
        <CheckinResultCard
          today={today}
          title={`Kelganingiz belgilangan · ${formatTime(checkin.checkInAt)}`}
          note={checkin.note ?? `Ketganingizni belgilash ${win.checkoutAt} dan ochiladi.`}
        />
      );
    case 'finished':
      return (
        <CheckinResultCard
          today={today}
          title={checkin.checkOutAt ? 'Bugungi davomat yakunlangan' : 'Kun avtomatik yakunlandi'}
          note={describeToday(today).note}
        />
      );
    case 'no-period':
      return (
        <EmptyState
          title="Amaliyot davri biriktirilmagan"
          description="Davr biriktirilgach shu yerda korxonadagi QR kod orqali davomat belgilanadi."
        />
      );
    case 'gap':
      return today.period ? (
        <PeriodGapCard today={today} period={today.period} phase={view.phase} />
      ) : null;
    case 'no-place':
      return (
        <EmptyState
          title="Korxona biriktirilmagan"
          description={
            checkin.note ??
            'Amaliyot joyingiz tasdiqlangach shu yerda korxonadagi QR kod orqali davomat belgilanadi.'
          }
          action={
            <Button asChild size="sm">
              <AppLink to="/joyim">Amaliyot joyini ko‘rish</AppLink>
            </Button>
          }
        />
      );
    case 'closed': {
      const { title, note } = describeToday(today);
      return <EmptyState title={title} description={note} />;
    }
  }
}

/** Muvaffaqiyat ekrani: vaqt, holat (Keldi / Kech keldi), "Bosh ekranga". */
function SuccessCard({ result }: { result: CheckinResult }) {
  const { mode, today } = result;
  const { checkin, window: win } = today;
  const title =
    mode === 'checkin'
      ? `Kelganingiz belgilandi · ${formatTime(checkin.checkInAt)}`
      : `Ketganingiz belgilandi · ${formatTime(checkin.checkOutAt)}`;
  const note =
    mode === 'checkin'
      ? `Kun yakunlanishi uchun kundalik yozing va ish oxirida (${win.checkoutAt} dan) ketganingizni shu bo‘limda belgilang.`
      : 'Bugungi davomat yakunlandi. Kundalik yuborilgan bo‘lsa, kun to‘liq hisoblanadi.';
  return (
    <CheckinResultCard
      today={today}
      title={title}
      note={note}
      success
      action={
        <Button asChild variant="primary" radius="md2" block>
          <AppLink to="/">Bosh ekranga</AppLink>
        </Button>
      }
    />
  );
}

export default QrPage;
