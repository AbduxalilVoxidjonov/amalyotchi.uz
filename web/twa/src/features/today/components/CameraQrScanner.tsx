import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Button } from '@/shared/ui';
import {
  type CameraScanOutcome,
  cameraErrorMessage,
  createQrFrameDecoder,
  openBackCamera,
  stopStream,
} from '../qr-scanner';
import { QR_MESSAGES } from '../qr';
import styles from './CameraQrScanner.module.css';

export interface CameraQrScannerProps {
  /** Natija bir marta chaqiriladi — skanerlangan matn, bekor qilish yoki kamera xatosi. */
  onDone: (outcome: CameraScanOutcome) => void;
}

/** Kadrlar orasidagi pauza (ms) — batareya va CPU tejaladi. */
const SCAN_INTERVAL_MS = 180;

/**
 * Brauzer QR skaneri (Telegram tashqarisida): orqa kamera (`facingMode: environment`) → video sheet →
 * `BarcodeDetector` / `jsqr` bilan kadrma-kadr dekodlash. Birinchi topilgan QR matni bilan yopiladi.
 */
export function CameraQrScanner({ onDone }: CameraQrScannerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const doneRef = useRef(onDone);
  const [ready, setReady] = useState(false);
  const cancelRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    doneRef.current = onDone;
  }, [onDone]);

  useEffect(() => {
    let stopped = false;
    let stream: MediaStream | null = null;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const finish = (outcome: CameraScanOutcome) => {
      if (stopped) return;
      stopped = true;
      if (timer) clearTimeout(timer);
      stopStream(stream);
      doneRef.current(outcome);
    };

    void (async () => {
      try {
        stream = await openBackCamera();
        if (stopped) {
          stopStream(stream);
          return;
        }
        const video = videoRef.current;
        if (!video) return;
        video.srcObject = stream;
        try {
          await video.play();
        } catch {
          /* autoplay rad etilsa ham `autoPlay` + `muted` bilan kadr keladi */
        }
        const decoder = await createQrFrameDecoder();
        if (stopped) return;
        setReady(true);
        const tick = async () => {
          if (stopped) return;
          const text = await decoder.decode(video).catch(() => null);
          if (text) {
            finish({ kind: 'scanned', text });
            return;
          }
          timer = setTimeout(() => void tick(), SCAN_INTERVAL_MS);
        };
        void tick();
      } catch (cause) {
        finish({ kind: 'error', message: cameraErrorMessage(cause) });
      }
    })();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') finish({ kind: 'cancelled' });
    };
    window.addEventListener('keydown', onKey);
    // Bekor qilish tugmasi shu yopilish orqali ishlaydi.
    cancelRef.current = () => finish({ kind: 'cancelled' });

    return () => {
      window.removeEventListener('keydown', onKey);
      cancelRef.current = null;
      stopped = true;
      if (timer) clearTimeout(timer);
      stopStream(stream);
    };
  }, []);

  return createPortal(
    <div className={styles.backdrop}>
      <div className={styles.sheet} role="dialog" aria-modal="true" aria-label="QR skaner">
        <div className={styles.viewport}>
          <video ref={videoRef} className={styles.video} muted playsInline autoPlay />
          <div className={styles.frame} aria-hidden="true" />
          {!ready && <div className={styles.loading}>Kamera ochilmoqda…</div>}
        </div>
        <p className={styles.prompt}>{QR_MESSAGES.scanPrompt}</p>
        <Button
          variant="secondary"
          radius="md2"
          className={styles.cancel}
          onClick={() => cancelRef.current?.()}
        >
          Bekor qilish
        </Button>
      </div>
    </div>,
    document.body,
  );
}
