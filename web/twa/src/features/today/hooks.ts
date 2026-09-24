import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { errorMessage, isApiError } from '@/shared/api/client';
import { closeTelegramQrScanner, haptic, scanTelegramQr } from '@/shared/auth/telegram';
import { getCurrentPosition, isGeoError, type GeoPoint } from '@/shared/lib/geolocation';
import { createPreviewUrl, revokePreviewUrl } from '@/shared/lib/image';
import { todayApi } from './api';
import { PHOTO_MESSAGES, preparePhoto } from './photo';
import { parseCheckinQr, QR_MESSAGES } from './qr';
import { canUseCamera, qrScannerKind, type CameraScanOutcome } from './qr-scanner';
import type { TodayDto } from './types';

export const todayKeys = {
  all: ['student', 'today'] as const,
};

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
 * KELDIM ⇄ KETDIM so'rovi (multipart). Joylashuv va rasm `useCheckinFlow` da tayyorlanadi.
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
      // Kalendar/portfolio davomati o'zgargan bo'lishi mumkin.
      void qc.invalidateQueries({ queryKey: ['student', 'calendar'] });
      void qc.invalidateQueries({ queryKey: ['student', 'portfolio'] });
    },
    onError: () => haptic('error'),
  });
}

/**
 * `idle` — tugma ko'rinadi · `qr` — amaliyot joyi QR kodi kutilmoqda · `capture` — kamera kutilmoqda ·
 * `preview` — rasm ko'rib chiqilmoqda.
 */
export type CheckinPhase = 'idle' | 'qr' | 'capture' | 'preview';

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
  photo: CheckinPhoto | null;
  /** Rasm siqilmoqda. */
  preparing: boolean;
  /** So'rov ketmoqda. */
  pending: boolean;
  /** Umumiy xato: joylashuv, tarmoq, server. */
  error: string | null;
  /** Rasmga oid xato: format, hajm, "rasm majburiy". */
  photoError: string | null;
  /**
   * Tugma bosildi. QR talab qilinsa — SHU GESTURE'da skaner ochiladi; aks holda joylashuv so'raladi
   * (kamerani chaqiruvchi o'zi ochadi).
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
  /** Rasm bilan (yoki `withoutPhoto` bo'lsa rasmsiz) yuborish. */
  submit: (options?: { withoutPhoto?: boolean }) => void;
  cancel: () => void;
}

/** Joylashuv shu muddatdan eski bo'lsa qayta so'raladi (selfie uzoq olinishi mumkin). */
const POSITION_MAX_AGE_MS = 90_000;

const DEFAULT_REQUIREMENTS: CheckinRequirements = { qrRequired: true, photoRequired: true };

/** Server QR'ni rad etdi: 400 `errors.Qr` (yo'q) yoki 409 `qrInvalid` (begona korxona). */
function qrRejection(cause: unknown): string | null {
  if (!isApiError(cause)) return null;
  const field = cause.fieldError('qr');
  if (field) return field;
  if (cause.status !== 409) return null;
  const reason = (cause.problem as { rejectReason?: unknown } | undefined)?.rejectReason;
  return reason === 'qrInvalid' || /\bQR\b/i.test(cause.message) ? cause.message : null;
}

/**
 * Check-in oqimi (kontrakt §1): **QR → joylashuv → selfie → yuborish**.
 * KELDIM → (QR talab qilinsa) Telegram skaneri shu gesture'da ochiladi → format tekshiriladi →
 * joylashuv so'raladi → "Rasmga olish" (kamera faqat foydalanuvchi harakatida ochiladi) → preview →
 * multipart so'rov (`qr`, `photo`). QR talab qilinmasa — avvalgidek: KELDIM joylashuvni so'raydi va
 * shu gesture'da kamerani ochadi.
 */
export function useCheckinFlow(): CheckinFlow {
  const toggle = useToggleCheckin();
  const [phase, setPhase] = useState<CheckinPhase>('idle');
  const [mode, setMode] = useState<CheckinMode>('checkin');
  const [requirements, setRequirements] = useState<CheckinRequirements>(DEFAULT_REQUIREMENTS);
  const [qr, setQr] = useState<string | null>(null);
  const [scanning, setScanning] = useState(false);
  const [cameraOpen, setCameraOpen] = useState(false);
  const [qrError, setQrError] = useState<string | null>(null);
  const [location, setLocation] = useState<LocationStatus>('idle');
  const [photo, setPhoto] = useState<CheckinPhoto | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);

  // Har bir urinish o'z raqamiga ega — kechikkan async javoblar eskirgan bo'lsa e'tiborsiz qoladi.
  const runIdRef = useRef(0);
  const positionRef = useRef<{ promise: Promise<GeoPoint>; at: number } | null>(null);
  const photoRef = useRef<CheckinPhoto | null>(null);
  const qrRef = useRef<string | null>(null);
  const scanningRef = useRef(false);

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
    // Ruxsat berilmasa — foydalanuvchi selfie olib bo'lgunicha kutmasdan, darhol ogohlantiramiz.
    // (`catch` shuningdek "unhandled rejection" ni ham oldini oladi; xato `submit` da qayta o'qiladi.)
    promise.then(
      () => {
        if (runId === runIdRef.current) setLocation('ok');
      },
      (cause: unknown) => {
        if (runId !== runIdRef.current) return;
        setLocation('error');
        if (isGeoError(cause)) setError(cause.message);
      },
    );
    positionRef.current = { promise, at: Date.now() };
    return promise;
  }, []);

  /** Yangi (yoki eskirmagan) joylashuv — QR'dan keyin va yuborishda. */
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
      // 2-qadam: joylashuv (QR'dan keyin), 3-qadam: selfie — rasm avval olingan bo'lsa preview'ga qaytiladi.
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

  const start = useCallback(
    (next: CheckinMode, reqs: Partial<CheckinRequirements> = {}) => {
      const resolved: CheckinRequirements = { ...DEFAULT_REQUIREMENTS, ...reqs };
      runIdRef.current += 1;
      positionRef.current = null;
      setMode(next);
      setRequirements(resolved);
      setError(null);
      setPhotoError(null);
      setQrError(null);
      setQrSafely(null);
      setScanningSafely(false);
      setCameraOpen(false);
      setLocation('idle');
      setPhotoSafely(null);
      toggle.reset();
      if (resolved.qrRequired) {
        setPhase('qr');
        scanQr();
        return;
      }
      setPhase('capture');
      requestPosition();
    },
    [requestPosition, scanQr, setPhotoSafely, setQrSafely, setScanningSafely, toggle],
  );

  const cancel = useCallback(() => {
    runIdRef.current += 1;
    if (scanningRef.current) closeTelegramQrScanner();
    positionRef.current = null;
    setPhase('idle');
    setPreparing(false);
    setError(null);
    setPhotoError(null);
    setQrError(null);
    setQrSafely(null);
    setScanningSafely(false);
    setCameraOpen(false);
    setLocation('idle');
    setPhotoSafely(null);
  }, [setPhotoSafely, setQrSafely, setScanningSafely]);

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
        .then((result) => {
          if (runId !== runIdRef.current) return;
          if (!result.ok) {
            setPhotoError(result.error);
            return;
          }
          setPhotoSafely({ file: result.file, url: createPreviewUrl(result.file) });
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

  const submit = useCallback(
    ({ withoutPhoto = false }: { withoutPhoto?: boolean } = {}) => {
      const runId = runIdRef.current;
      const file = withoutPhoto ? null : (photoRef.current?.file ?? null);
      const qrPayload = qrRef.current;
      if (requirements.qrRequired && !qrPayload) {
        // Himoya: QR'siz yuborilmaydi — 1-qadamga qaytiladi.
        setPhase('qr');
        return;
      }
      setError(null);
      setPhotoError(null);

      void freshPosition()
        .then((point) => toggle.mutateAsync({ mode, point, photo: file, qr: qrPayload }))
        .then(() => {
          if (runId !== runIdRef.current) return;
          runIdRef.current += 1;
          positionRef.current = null;
          setPhase('idle');
          setPhotoSafely(null);
          setQrSafely(null);
          setLocation('idle');
        })
        .catch((cause: unknown) => {
          if (runId !== runIdRef.current) return;
          // Joylashuv eskirgan/xato — keyingi urinishda qaytadan so'raladi.
          positionRef.current = null;
          if (isGeoError(cause)) {
            setError(cause.message);
            return;
          }
          const qrMessage = qrRejection(cause);
          if (qrMessage) {
            // QR qayta skanerlanadi; olingan rasm saqlanadi (skanerdan keyin preview'ga qaytiladi).
            setQrSafely(null);
            setQrError(qrMessage);
            setPhase('qr');
            return;
          }
          const fieldError = isApiError(cause) ? cause.fieldError('photo') : undefined;
          if (fieldError) {
            setPhotoError(fieldError);
            setPhase('capture');
            setPhotoSafely(null);
            return;
          }
          setError(errorMessage(cause));
        });
    },
    [freshPosition, mode, requirements.qrRequired, setPhotoSafely, setQrSafely, toggle],
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
    photo,
    preparing,
    pending: toggle.isPending,
    error,
    photoError,
    start,
    scanQr,
    rescanQr,
    finishCameraScan,
    acceptQr,
    selectPhoto,
    submit,
    cancel,
  };
}
