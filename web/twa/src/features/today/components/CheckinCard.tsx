import { useRef } from 'react';
import { Button, Card, Eyebrow, FactGrid, type FactItem } from '@/shared/ui';
import { formatDate, formatMeters, formatTime } from '@/shared/lib/format';
import type { CheckinFlow, CheckinMode } from '../hooks';
import { PHOTO_ACCEPT } from '../photo';
import { isQrRequired } from '../qr';
import type { TodayDto } from '../types';
import { CameraQrScanner } from './CameraQrScanner';
import { CheckinSteps } from './CheckinSteps';
import { LocationStep } from './LocationStep';
import { QrConfirmed, QrScanPanel } from './QrScan';
import { SelfieCapture } from './SelfieCapture';
import styles from './CheckinCard.module.css';

export interface CheckinCardProps {
  today: TodayDto;
  /** `checkin` — "Kelganini belgilash" · `checkout` — "Ketganini belgilash". */
  mode: CheckinMode;
  /** Davomat oqimi (`useCheckinFlow`) — xato/yuklanish holatlari shu yerda. */
  flow: CheckinFlow;
}

const COPY: Record<CheckinMode, { title: string; button: string }> = {
  checkin: { title: 'Kelganini belgilash', button: 'Kelganini belgilash' },
  checkout: { title: 'Ketganini belgilash', button: 'Ketganini belgilash' },
};

/**
 * QR sahifasi — belgilanish kartasi (presentation): boshlash tugmasi va qadamlar
 * **1) QR → 2) selfi → 3) joylashuv**. Sozlamada QR talab qilinmasa 1-qadam o'tkazib yuboriladi.
 */
export function CheckinCard({ today, mode, flow }: CheckinCardProps) {
  const { checkin, window: win } = today;
  const cameraRef = useRef<HTMLInputElement>(null);
  // Sozlamalar (TodayDto `checkin`): flag yo'q bo'lsa — ikkalasi ham majburiy (backend sukuti `true`).
  const qrRequired = isQrRequired(checkin.qrRequired);
  const photoRequired = checkin.photoRequired !== false;
  const inFlow = flow.phase !== 'idle' && flow.phase !== 'done';
  const selfieStep = flow.phase === 'capture' || flow.phase === 'preview';
  const copy = COPY[mode];

  /**
   * Kamera SHU foydalanuvchi harakatida ochiladi (`input.click()`) — `await` dan keyin
   * chaqirilsa iOS/Telegram WebView bloklaydi. QR talab qilinsa boshlash tugmasi QR skanerini ochadi;
   * kamera keyin "Rasmga olish" tugmasi bilan ochiladi.
   */
  function openCamera() {
    const el = cameraRef.current;
    if (!el) return;
    el.value = '';
    el.click();
  }

  const note =
    mode === 'checkin'
      ? `Oyna ${win.start}–${win.closesAt}. ${win.end} dan keyingi belgilanish "Kech keldi" bo'ladi.`
      : `Kelgan vaqtingiz: ${formatTime(checkin.checkInAt)}. Ish kuni oxirida ketganingizni belgilang.`;

  const facts: FactItem[] =
    mode === 'checkin'
      ? [
          { k: 'Oyna', v: `${win.start}–${win.closesAt}` },
          { k: 'Korxona radiusi', v: formatMeters(checkin.radiusM ?? today.place?.radiusM) },
        ]
      : [
          { k: 'Kelgan vaqt', v: formatTime(checkin.checkInAt) || '—' },
          { k: 'Check-out oynasi', v: `${win.checkoutAt} dan` },
        ];

  return (
    <Card padded="lg" aria-labelledby="checkin-title">
      <Eyebrow as="div" spacing="wide" margin="none">
        Bugun · {formatDate(today.date)}
      </Eyebrow>
      <h2 id="checkin-title" className={styles.title}>
        {copy.title}
      </h2>
      <p className={styles.note}>{note}</p>

      <input
        ref={cameraRef}
        type="file"
        hidden
        accept={PHOTO_ACCEPT}
        capture="user"
        aria-label="Selfie olish"
        onChange={(e) => flow.selectPhoto(e.target.files?.[0])}
      />

      {flow.phase === 'idle' && (
        <>
          {flow.error && (
            <p className={styles.error} role="alert">
              {flow.error}
            </p>
          )}
          <Button
            variant="checkin"
            tone={mode === 'checkout' ? 'dark' : 'accent'}
            className={styles.button}
            onClick={() => {
              flow.start(mode, { qrRequired, photoRequired });
              if (!qrRequired) openCamera();
            }}
          >
            {copy.button}
          </Button>
          <p className={styles.hint}>
            {qrRequired
              ? 'Qadamlar: 1) korxonadagi QR kodni skanerlash · 2) selfi · 3) joylashuv.'
              : 'Qadamlar: 1) selfi · 2) joylashuv.'}
          </p>
        </>
      )}

      {inFlow && <CheckinSteps flow={flow} />}
      {inFlow && flow.phase === 'qr' && <QrScanPanel flow={flow} />}
      {selfieStep && flow.requirements.qrRequired && flow.qr && <QrConfirmed flow={flow} />}
      {selfieStep && <SelfieCapture flow={flow} onOpenCamera={openCamera} />}
      {flow.phase === 'location' && <LocationStep flow={flow} />}
      {inFlow && flow.cameraOpen && <CameraQrScanner onDone={flow.finishCameraScan} />}

      <FactGrid items={facts} columns={2} className={styles.facts} />

      <p className={styles.hint}>
        Belgilanish faqat korxona radiusi ichidan qabul qilinadi. Har bir urinish — muvaffaqiyatsizi
        ham — tyutorga ko'rinadi.
      </p>
    </Card>
  );
}
