import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { closeTelegramQrScanner, haptic, scanTelegramQr } from '@/shared/auth/telegram';
import { getCurrentPosition, isGeoError, type GeoPoint } from '@/shared/lib/geolocation';
import { createPreviewUrl, revokePreviewUrl } from '@/shared/lib/image';
import { todayApi } from './api';
import { classifyCheckinError } from './checkin-errors';
import { PHOTO_MESSAGES, preparePhoto } from './photo';
import { parseCheckinQr, QR_MESSAGES } from './qr';
import { canUseCamera, qrScannerKind, type CameraScanOutcome } from './qr-scanner';
import type { TodayDto } from './types';

export const todayKeys = {
  all: ['student', 'today'] as const,
};

/** `period-days` query prefiksi (bosh ekran kunlari) — check-in'dan keyin yangilanadi. */
const PERIOD_DAYS_KEY = ['student', 'period-days'] as const;

export function useTodayQuery() {
  return useQuery({
    queryKey: todayKeys.all,
    queryFn: ({ signal }) => todayApi.get(signal),
    // Oyna/holat vaqtga bog'liq — 1 daqiqada yangilanadi.
    refetchInterval: 60_000,
  });
}

export type CheckinMode = 'checkin' | 'checkout';

export interface ToggleCheckinInput {
  mode: CheckinMode;
  point: GeoPoint;
  /** Selfie — sozlamaga qarab majburiy (kontrakt §1.2). */
  photo: File | null;
  /** Amaliyot joyi QR payload'i (`AMLQR:1:...`) — sozlamaga qarab majburiy. */
  qr: string | null;
}

/**
 * Kelganini ⇄ ketganini belgilash so'rovi (multipart). Joylashuv va rasm `useCheckinFlow` da tayyorlanadi.
 * Server yangi TodayDto qaytaradi — keshga to'g'ridan-to'g'ri yoziladi (qayta so'rovsiz).
 * TODO(offline): tarmoq xatosida (ApiError.kind === 'network') urinishni navbatga qo'yish.
 */
export function useToggleCheckin() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'checkin'],
    mutationFn: ({ mode, point, photo, qr }: ToggleCheckinInput): Promise<TodayDto> => {
      const body = { ...point, photo, qr };
      return mode === 'checkin' ? todayApi.checkin(body) : todayApi.checkout(body);
    },
    onSuccess: (data) => {
      haptic('success');
      qc.setQueryData(todayKeys.all, data);
      // Bosh ekrandagi kunlar ro'yxati (bugungi qator holati) o'zgargan.
      void qc.invalidateQueries({ queryKey: PERIOD_DAYS_KEY });
    },
    onError: () => haptic('error'),
  });
}

/**
 * `idle` — boshlash tugmasi · `qr` — 1-qadam: amaliyot joyi QR kodi · `capture` — 2-qadam: selfi (kamera
 * kutilmoqda) · `preview` — selfi ko'rib chiqilmoqda · `location` — 3-qadam: joylashuv aniqlanib, so'rov
 * yuborilmoqda (xato bo'lsa shu yerda "Qayta urinish") · `done` — muvaffaqiyat (`result`).
 */
export type CheckinPhase = 'idle' | 'qr' | 'capture' | 'preview' | 'location' | 'done';

/** Joylashuv so'rovi holati (qadamlar ko'rsatkichi uchun). */
export type LocationStatus = 'idle' | 'pending' | 'ok' | 'error';

export interface CheckinPhoto {
  file: File;
  /** `URL.createObjectURL` — muhitda bo'lmasa `null`. */
  url: string | null;
}

/** Urinish boshida TodayDto'dan olinadigan talablar (sozlamalar). */
export interface CheckinRequirements {
  /** `checkin.qrRequired` — yo'q bo'lsa `true`. */
  qrRequired: boolean;
  /** `checkin.photoRequired` — yo'q bo'lsa `true` ("Rasmsiz davom etish" ko'rsatilmaydi). */
  photoRequired: boolean;
}

/** Muvaffaqiyatli belgilanish: server qaytargan yangi TodayDto (vaqt, holat, masofa). */
export interface CheckinResult {
  mode: CheckinMode;
  today: TodayDto;
}

export interface CheckinFlow {
  phase: CheckinPhase;
  mode: CheckinMode;
  /** Joriy urinish talablari (`start` da o'rnatiladi). */
  requirements: CheckinRequirements;
  /** Format bo'yicha tasdiqlangan QR payload (`AMLQR:1:...`) yoki `null`. */
  qr: string | null;
  /** QR skaner ochiq (Telegram popup yoki brauzer kamerasi). */
  scanning: boolean;
  /** Brauzer kamera skaneri (sheet) ochiq — Telegram tashqarisida. */
  cameraOpen: boolean;
  /** QR xatosi: begona QR, bekor qilish, eski Telegram, server 400/409. */
  qrError: string | null;
  location: LocationStatus;
  /** Aniqlangan joylashuv (3-qadamda aniqlik ko'rsatiladi). */
  point: GeoPoint | null;
  /** Joylashuv xatosi: ruxsat yo'q, GPS aniqligi yetarli emas, radius tashqarisi (server 400/409). */
  locationError: string | null;
  photo: CheckinPhoto | null;
  /** Rasm siqilmoqda. */
  preparing: boolean;
  /** So'rov ketmoqda. */
  pending: boolean;
  /** Umumiy xato: tarmoq, server, eskirgan ekran (oyna yopilgan, allaqachon belgilangan …). */
  error: string | null;
  /** Rasmga oid xato: format, hajm, "rasm majburiy". */
  photoError: string | null;
  /** `done` bosqichida — server javobi. */
  result: CheckinResult | null;
  /**
   * Tugma bosildi. QR talab qilinsa — SHU GESTURE'da skaner ochiladi; aks holda selfi qadamiga o'tiladi
   * (kamerani chaqiruvchi o'zi ochadi) va joylashuv fonda so'raladi.
   */
  start: (mode: CheckinMode, requirements?: Partial<CheckinRequirements>) => void;
  /**
   * QR skanerni ochish (qayta skanerlash ham): Telegram ichida — native popup, brauzerda — kamera sheet.
   * `camera: true` — eski Telegram'da ham kamera skanerini majburan ochish.
   */
  scanQr: (options?: { camera?: boolean }) => void;
  /** "Qayta skanerlash": tasdiqlangan QR bekor qilinadi, 1-qadamga qaytib skaner ochiladi. */
  rescanQr: () => void;
  /** Brauzer kamera skaneri natijasi (`CameraQrScanner.onDone`). */
  finishCameraScan: (outcome: CameraScanOutcome) => void;
  /** Skanerlangan/kiritilgan matnni tekshirish (native callback va dev "Test QR" shu yerdan o'tadi). */
  acceptQr: (text: string) => void;
  /** Kameradan/galereyadan fayl keldi (bekor qilinsa — `null`). */
  selectPhoto: (file: File | null | undefined) => void;
  /** Selfi tasdiqlandi (yoki `withoutPhoto`) → 3-qadam: joylashuv aniqlanadi va so'rov yuboriladi. */
  submit: (options?: { withoutPhoto?: boolean }) => void;
  /** 3-qadamda: joylashuv qaytadan so'raladi va so'rov qayta yuboriladi (QR va selfi saqlanadi). */
  retry: () => void;
  cancel: () => void;
}

/** Joylashuv shu muddatdan eski bo'lsa qayta so'raladi (selfie uzoq olinishi mumkin). */
const POSITION_MAX_AGE_MS = 90_000;

const DEFAULT_REQUIREMENTS: CheckinRequirements = { qrRequired: true, photoRequired: true };

/**
 * Davomat oqimi (QR sahifasi): **1) QR → 2) selfi (yuz) → 3) joylashuv → yuborish**.
 * Boshlash → (QR talab qilinsa) Telegram skaneri shu gesture'da ochiladi → format tekshiriladi →
 * selfi (kamera faqat foydalanuvchi harakatida ochiladi; joylashuv shu paytda FONDA so'raladi — tezlik uchun) →
 * preview → "Tasdiqlash va yuborish" → 3-qadam: joylashuv (fondagi natija yoki yangi so'rov) → multipart so'rov
 * (`qr`, `photo`). Server xatosi `classifyCheckinError` bo'yicha tegishli qadamga qaytaradi.
 * QR talab qilinmasa — 1-qadam o'tkazib yuboriladi; selfi majburiy bo'lmasa — "Rasmsiz davom etish".
 */
export function useCheckinFlow(): CheckinFlow {
  const qc = useQueryClient();
  const toggle = useToggleCheckin();
  const [phase, setPhase] = useState<CheckinPhase>('idle');
  const [mode, setMode] = useState<CheckinMode>('checkin');
  const [requirements, setRequirements] = useState<CheckinRequirements>(DEFAULT_REQUIREMENTS);
  const [qr, setQr] = useState<string | null>(null);
  const [scanning, setScanning] = useState(false);
  const [cameraOpen, setCameraOpen] = useState(false);
  const [qrError, setQrError] = useState<string | null>(null);
  const [location, setLocation] = useState<LocationStatus>('idle');
  const [point, setPoint] = useState<GeoPoint | null>(null);
  const [locationError, setLocationError] = useState<string | null>(null);
  const [photo, setPhoto] = useState<CheckinPhoto | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [result, setResult] = useState<CheckinResult | null>(null);

  // Har bir urinish o'z raqamiga ega — kechikkan async javoblar eskirgan bo'lsa e'tiborsiz qoladi.
  const runIdRef = useRef(0);
  const positionRef = useRef<{ promise: Promise<GeoPoint>; at: number } | null>(null);
  const photoRef = useRef<CheckinPhoto | null>(null);
  const qrRef = useRef<string | null>(null);
  const scanningRef = useRef(false);
  /** "Rasmsiz davom etish" tanlangan — `retry` da ham rasmsiz yuboriladi. */
  const skipPhotoRef = useRef(false);

  const setPhotoSafely = useCallback((next: CheckinPhoto | null) => {
    revokePreviewUrl(photoRef.current?.url);
    photoRef.current = next;
    setPhoto(next);
  }, []);

  const setQrSafely = useCallback((next: string | null) => {
    qrRef.current = next;
    setQr(next);
  }, []);

  const setScanningSafely = useCallback((next: boolean) => {
    scanningRef.current = next;
    setScanning(next);
  }, []);

  // Komponent yo'q qilinganda preview URL bo'shatiladi, ochiq QR popup yopiladi.
  useEffect(
    () => () => {
      revokePreviewUrl(photoRef.current?.url);
      if (scanningRef.current) closeTelegramQrScanner();
    },
    [],
  );

  const requestPosition = useCallback(() => {
    const runId = runIdRef.current;
    const promise = getCurrentPosition();
    setLocation('pending');
    setPoint(null);
    // Natija 3-qadamda o'qiladi (`send`); bu yerda faqat holat. `catch` — "unhandled rejection" oldini oladi.
    promise.then(
      (next) => {
        if (runId !== runIdRef.current) return;
        setLocation('ok');
        setPoint(next);
      },
      () => {
        if (runId === runIdRef.current) setLocation('error');
      },
    );
    positionRef.current = { promise, at: Date.now() };
    return promise;
  }, []);

  /** Yangi (yoki eskirmagan) joylashuv — selfi paytida fonda va yuborishda. */
  const freshPosition = useCallback(() => {
    const cached = positionRef.current;
    return cached && Date.now() - cached.at <= POSITION_MAX_AGE_MS
      ? cached.promise
      : requestPosition();
  }, [requestPosition]);

  const acceptQr = useCallback(
    (text: string) => {
      const payload = parseCheckinQr(text);
      if (!payload) {
        haptic('error');
        setQrError(QR_MESSAGES.foreign);
        return;
      }
      haptic('success');
      setQrSafely(payload);
      setQrError(null);
      setError(null);
      // 2-qadam: selfi — rasm avval olingan bo'lsa (server QR'ni rad etgach qayta skanerlash) preview'ga.
      // 3-qadam (joylashuv) tezlik uchun fonda boshlanadi.
      void freshPosition().catch(() => undefined);
      setPhase(photoRef.current ? 'preview' : 'capture');
    },
    [freshPosition, setQrSafely],
  );

  const scanQr = useCallback(
    ({ camera = false }: { camera?: boolean } = {}) => {
      if (scanningRef.current) return;
      const kind = camera && canUseCamera() ? 'camera' : qrScannerKind();
      if (kind === 'outdated' || kind === 'unavailable') {
        setQrError(kind === 'outdated' ? QR_MESSAGES.outdated : QR_MESSAGES.unavailable);
        return;
      }
      setQrError(null);
      setScanningSafely(true);
      if (kind === 'camera') {
        setCameraOpen(true);
        return;
      }
      const runId = runIdRef.current;
      scanTelegramQr(QR_MESSAGES.scanPrompt)
        .then((text) => {
          if (runId !== runIdRef.current) return;
          if (text === null) {
            setQrError(QR_MESSAGES.cancelled);
            return;
          }
          acceptQr(text);
        })
        .catch(() => {
          if (runId === runIdRef.current) setQrError(QR_MESSAGES.failed);
        })
        .finally(() => {
          if (runId === runIdRef.current) setScanningSafely(false);
        });
    },
    [acceptQr, setScanningSafely],
  );

  const finishCameraScan = useCallback(
    (outcome: CameraScanOutcome) => {
      setCameraOpen(false);
      setScanningSafely(false);
      if (outcome.kind === 'scanned') acceptQr(outcome.text);
      else if (outcome.kind === 'cancelled') setQrError(QR_MESSAGES.cancelled);
      else setQrError(outcome.message);
    },
    [acceptQr, setScanningSafely],
  );

  const rescanQr = useCallback(() => {
    setQrSafely(null);
    setPhase('qr');
    scanQr();
  }, [scanQr, setQrSafely]);

  /** Holatni boshlang'ich ko'rinishga qaytaradi (yangi urinish raqami bilan). */
  const resetAttempt = useCallback(() => {
    runIdRef.current += 1;
    if (scanningRef.current) closeTelegramQrScanner();
    positionRef.current = null;
    skipPhotoRef.current = false;
    setPreparing(false);
    setError(null);
    setPhotoError(null);
    setQrError(null);
    setLocationError(null);
    setQrSafely(null);
    setScanningSafely(false);
    setCameraOpen(false);
    setLocation('idle');
    setPoint(null);
    setPhotoSafely(null);
    setResult(null);
  }, [setPhotoSafely, setQrSafely, setScanningSafely]);

  const start = useCallback(
    (next: CheckinMode, reqs: Partial<CheckinRequirements> = {}) => {
      const resolved: CheckinRequirements = { ...DEFAULT_REQUIREMENTS, ...reqs };
      resetAttempt();
      setMode(next);
      setRequirements(resolved);
      toggle.reset();
      if (resolved.qrRequired) {
        setPhase('qr');
        scanQr();
        return;
      }
      setPhase('capture');
      void requestPosition().catch(() => undefined);
    },
    [requestPosition, resetAttempt, scanQr, toggle],
  );

  const cancel = useCallback(() => {
    resetAttempt();
    setPhase('idle');
  }, [resetAttempt]);

  const selectPhoto = useCallback(
    (file: File | null | undefined) => {
      if (!file) {
        // iOS'da kamera bekor qilinsa `change` umuman chiqmaydi; bu — bo'sh tanlov holati.
        setPhotoError(PHOTO_MESSAGES.cameraCancelled);
        return;
      }
      const runId = runIdRef.current;
      setPhotoError(null);
      setPreparing(true);
      void preparePhoto(file)
        .then((prepared) => {
          if (runId !== runIdRef.current) return;
          if (!prepared.ok) {
            setPhotoError(prepared.error);
            return;
          }
          skipPhotoRef.current = false;
          setPhotoSafely({ file: prepared.file, url: createPreviewUrl(prepared.file) });
          setPhase('preview');
        })
        .catch(() => {
          if (runId === runIdRef.current) setPhotoError(PHOTO_MESSAGES.failed);
        })
        .finally(() => {
          if (runId === runIdRef.current) setPreparing(false);
        });
    },
    [setPhotoSafely],
  );

  /** 3-qadam: joylashuv → so'rov. Xato bo'lsa — tegishli qadamga (`classifyCheckinError`). */
  const send = useCallback(() => {
    const runId = runIdRef.current;
    const file = skipPhotoRef.current ? null : (photoRef.current?.file ?? null);
    const qrPayload = qrRef.current;
    if (requirements.qrRequired && !qrPayload) {
      // Himoya: QR'siz yuborilmaydi — 1-qadamga qaytiladi.
      setPhase('qr');
      return;
    }
    setPhase('location');
    setError(null);
    setLocationError(null);
    setPhotoError(null);

    void freshPosition()
      .then((next) => {
        if (runId !== runIdRef.current) return null;
        setPoint(next);
        return toggle.mutateAsync({ mode, point: next, photo: file, qr: qrPayload });
      })
      .then((data) => {
        if (!data || runId !== runIdRef.current) return;
        runIdRef.current += 1;
        positionRef.current = null;
        setPhotoSafely(null);
        setQrSafely(null);
        setResult({ mode, today: data });
        setPhase('done');
      })
      .catch((cause: unknown) => {
        if (runId !== runIdRef.current) return;
        // Joylashuv eskirgan/xato — keyingi urinishda qaytadan so'raladi.
        positionRef.current = null;
        if (isGeoError(cause)) {
          setLocation('error');
          setLocationError(cause.message);
          return;
        }
        const verdict = classifyCheckinError(cause);
        switch (verdict.step) {
          case 'qr':
            // QR qayta skanerlanadi; olingan rasm saqlanadi (skanerdan keyin preview'ga qaytiladi).
            setQrSafely(null);
            setQrError(verdict.message);
            setLocation('idle');
            setPhase('qr');
            return;
          case 'photo':
            setPhotoError(verdict.message);
            setPhotoSafely(null);
            skipPhotoRef.current = false;
            setLocation('idle');
            setPhase('capture');
            return;
          case 'location':
            setLocation('error');
            setLocationError(verdict.message);
            return;
          case 'stale':
            // Amal hozir mumkin emas (oyna yopilgan, allaqachon belgilangan …) — bugungi holat yangilanadi.
            resetAttempt();
            setError(verdict.message);
            setPhase('idle');
            void qc.invalidateQueries({ queryKey: todayKeys.all });
            void qc.invalidateQueries({ queryKey: PERIOD_DAYS_KEY });
            return;
          default:
            setError(verdict.message);
        }
      });
  }, [
    freshPosition,
    mode,
    qc,
    requirements.qrRequired,
    resetAttempt,
    setPhotoSafely,
    setQrSafely,
    toggle,
  ]);

  const submit = useCallback(
    ({ withoutPhoto = false }: { withoutPhoto?: boolean } = {}) => {
      skipPhotoRef.current = withoutPhoto;
      send();
    },
    [send],
  );

  return {
    phase,
    mode,
    requirements,
    qr,
    scanning,
    cameraOpen,
    qrError,
    location,
    point,
    locationError,
    photo,
    preparing,
    pending: toggle.isPending,
    error,
    photoError,
    result,
    start,
    scanQr,
    rescanQr,
    finishCameraScan,
    acceptQr,
    selectPhoto,
    submit,
    retry: send,
    cancel,
  };
}
