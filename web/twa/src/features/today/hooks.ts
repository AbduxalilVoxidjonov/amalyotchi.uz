import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { errorMessage, isApiError } from '@/shared/api/client';
import { haptic } from '@/shared/auth/telegram';
import { getCurrentPosition, isGeoError, type GeoPoint } from '@/shared/lib/geolocation';
import { createPreviewUrl, revokePreviewUrl } from '@/shared/lib/image';
import { todayApi } from './api';
import { PHOTO_MESSAGES, preparePhoto } from './photo';
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
    mutationFn: ({ mode, point, photo }: ToggleCheckinInput): Promise<TodayDto> => {
      const body = { ...point, photo };
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

/** `idle` — tugma ko'rinadi · `capture` — kamera kutilmoqda · `preview` — rasm ko'rib chiqilmoqda. */
export type CheckinPhase = 'idle' | 'capture' | 'preview';

export interface CheckinPhoto {
  file: File;
  /** `URL.createObjectURL` — muhitda bo'lmasa `null`. */
  url: string | null;
}

export interface CheckinFlow {
  phase: CheckinPhase;
  mode: CheckinMode;
  photo: CheckinPhoto | null;
  /** Rasm siqilmoqda. */
  preparing: boolean;
  /** So'rov ketmoqda. */
  pending: boolean;
  /** Umumiy xato: joylashuv, tarmoq, server. */
  error: string | null;
  /** Rasmga oid xato: format, hajm, "rasm majburiy". */
  photoError: string | null;
  /** Tugma bosildi: joylashuv so'raladi va kamera ochiladi. */
  start: (mode: CheckinMode) => void;
  /** Kameradan/galereyadan fayl keldi (bekor qilinsa — `null`). */
  selectPhoto: (file: File | null | undefined) => void;
  /** Rasm bilan (yoki `withoutPhoto` bo'lsa rasmsiz) yuborish. */
  submit: (options?: { withoutPhoto?: boolean }) => void;
  cancel: () => void;
}

/** Joylashuv shu muddatdan eski bo'lsa qayta so'raladi (selfie uzoq olinishi mumkin). */
const POSITION_MAX_AGE_MS = 90_000;

/**
 * Check-in selfie oqimi (kontrakt §1): KELDIM → joylashuv so'rovi boshlanadi va SHU GESTURE'da
 * kamera ochiladi (`input[capture]` uchun foydalanuvchi harakati saqlanishi shart) → rasm
 * siqiladi → ko'rib chiqish → multipart so'rov.
 *
 * Joylashuv kameradan OLDIN so'raladi, lekin javobi rasm tasdiqlanganda kutiladi — shunda
 * ruxsat oynasi va kamera ketma-ket chiqadi, so'rov esa kechikmaydi.
 */
export function useCheckinFlow(): CheckinFlow {
  const toggle = useToggleCheckin();
  const [phase, setPhase] = useState<CheckinPhase>('idle');
  const [mode, setMode] = useState<CheckinMode>('checkin');
  const [photo, setPhoto] = useState<CheckinPhoto | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);

  // Har bir urinish o'z raqamiga ega — kechikkan async javoblar eskirgan bo'lsa e'tiborsiz qoladi.
  const runIdRef = useRef(0);
  const positionRef = useRef<{ promise: Promise<GeoPoint>; at: number } | null>(null);
  const photoRef = useRef<CheckinPhoto | null>(null);

  const setPhotoSafely = useCallback((next: CheckinPhoto | null) => {
    revokePreviewUrl(photoRef.current?.url);
    photoRef.current = next;
    setPhoto(next);
  }, []);

  // Komponent yo'q qilinganda preview URL bo'shatiladi.
  useEffect(() => () => revokePreviewUrl(photoRef.current?.url), []);

  const requestPosition = useCallback(() => {
    const runId = runIdRef.current;
    const promise = getCurrentPosition();
    // Ruxsat berilmasa — foydalanuvchi selfie olib bo'lgunicha kutmasdan, darhol ogohlantiramiz.
    // (`catch` shuningdek "unhandled rejection" ni ham oldini oladi; xato `submit` da qayta o'qiladi.)
    promise.catch((cause: unknown) => {
      if (runId === runIdRef.current && isGeoError(cause)) setError(cause.message);
    });
    positionRef.current = { promise, at: Date.now() };
    return promise;
  }, []);

  const start = useCallback(
    (next: CheckinMode) => {
      runIdRef.current += 1;
      setMode(next);
      setPhase('capture');
      setError(null);
      setPhotoError(null);
      setPhotoSafely(null);
      toggle.reset();
      requestPosition();
    },
    [requestPosition, setPhotoSafely, toggle],
  );

  const cancel = useCallback(() => {
    runIdRef.current += 1;
    positionRef.current = null;
    setPhase('idle');
    setPreparing(false);
    setError(null);
    setPhotoError(null);
    setPhotoSafely(null);
  }, [setPhotoSafely]);

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
      setError(null);
      setPhotoError(null);

      const cached = positionRef.current;
      const position =
        cached && Date.now() - cached.at <= POSITION_MAX_AGE_MS
          ? cached.promise
          : requestPosition();

      void position
        .then((point) => toggle.mutateAsync({ mode, point, photo: file }))
        .then(() => {
          if (runId !== runIdRef.current) return;
          runIdRef.current += 1;
          positionRef.current = null;
          setPhase('idle');
          setPhotoSafely(null);
        })
        .catch((cause: unknown) => {
          if (runId !== runIdRef.current) return;
          // Joylashuv eskirgan/xato — keyingi urinishda qaytadan so'raladi.
          positionRef.current = null;
          if (isGeoError(cause)) {
            setError(cause.message);
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
    [mode, requestPosition, setPhotoSafely, toggle],
  );

  return {
    phase,
    mode,
    photo,
    preparing,
    pending: toggle.isPending,
    error,
    photoError,
    start,
    selectPhoto,
    submit,
    cancel,
  };
}
