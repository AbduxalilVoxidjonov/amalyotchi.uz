import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';

export interface AuthFileState {
  status: 'idle' | 'loading' | 'ready' | 'error';
  /** `URL.createObjectURL` havolasi (`status === 'ready'`). */
  objectUrl: string | null;
  /** Serverdan kelgan MIME turi ("application/pdf", "image/jpeg", …). */
  contentType: string | null;
  error: string | null;
}

const IDLE: AuthFileState = { status: 'idle', objectUrl: null, contentType: null, error: null };

/**
 * `/api/files/{id}` — Bearer token talab qiladi, shuning uchun oddiy `<a href>` yangi oynada 401 oladi
 * (backend ustiga `Content-Disposition: attachment` qo'yadi — brauzer ko'rsatmay, yuklab oladi).
 * Bu hook faylni token bilan `fetch` qilib blob'ga aylantiradi: blob havolasini `<iframe>`/`<img>` da
 * ko'rsatish ham, yuklab olish ham mumkin. Havola unmount'da (va qayta yuklashda) bo'shatiladi.
 */
export function useAuthFile(url: string) {
  const [state, setState] = useState<AuthFileState>(IDLE);
  const objectUrlRef = useRef<string | null>(null);

  const release = useCallback(() => {
    if (objectUrlRef.current && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(objectUrlRef.current);
    }
    objectUrlRef.current = null;
  }, []);

  useEffect(() => release, [release]);
  // Havola o'zgarsa — eski blob kerak emas.
  useEffect(() => {
    release();
    setState(IDLE);
  }, [url, release]);

  const load = useCallback(async () => {
    if (objectUrlRef.current) return;
    setState({ status: 'loading', objectUrl: null, contentType: null, error: null });
    try {
      const token = useAuthStore.getState().accessToken;
      const response = await fetch(`${env.apiUrl}${url}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const blob = await response.blob();
      if (typeof URL.createObjectURL !== 'function') throw new Error('createObjectURL yo‘q');
      const objectUrl = URL.createObjectURL(blob);
      objectUrlRef.current = objectUrl;
      setState({ status: 'ready', objectUrl, contentType: blob.type || null, error: null });
    } catch {
      setState({
        status: 'error',
        objectUrl: null,
        contentType: null,
        error: "Faylni ochib bo'lmadi.",
      });
    }
  }, [url]);

  return { ...state, load, release };
}
