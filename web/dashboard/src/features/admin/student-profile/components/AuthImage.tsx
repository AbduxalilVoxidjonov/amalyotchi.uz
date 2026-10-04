import { useEffect, useState } from 'react';
import { cn } from '@/shared/ui';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';
import styles from './AuthImage.module.css';

export interface AuthImageProps {
  /** `/api/files/{id}` — nisbiy havola (kontrakt §0). */
  src: string;
  alt: string;
  className?: string | undefined;
  /** Yuklanayotgan/xato holatidagi blok balandligi (px). */
  height?: number;
}

type State = { kind: 'loading' } | { kind: 'ready'; objectUrl: string } | { kind: 'error' };

/**
 * Bearer token talab qiladigan rasm (`/api/files/...`) — oddiy `<img src>` 401 oladi.
 * Shuning uchun: `fetch` + `Authorization` sarlavhasi → `blob` → `URL.createObjectURL`.
 * Token manbai `@/shared/api` klientiniki bilan bir xil (`useAuthStore.accessToken`),
 * bazaviy manzil — `env.apiUrl`. Unmount'da `revokeObjectURL` chaqiriladi.
 */
export function AuthImage({ src, alt, className, height = 120 }: AuthImageProps) {
  const [state, setState] = useState<State>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    const controller = new AbortController();
    setState({ kind: 'loading' });

    const token = useAuthStore.getState().accessToken;
    void fetch(`${env.apiUrl}${src}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      signal: controller.signal,
    })
      .then(async (response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.blob();
      })
      .then((blob) => {
        if (!active) return;
        // jsdom/eski brauzerlarda `createObjectURL` bo'lmasligi mumkin — fallback ko'rsatamiz.
        if (typeof URL.createObjectURL !== 'function') throw new Error('createObjectURL yo‘q');
        objectUrl = URL.createObjectURL(blob);
        setState({ kind: 'ready', objectUrl });
      })
      .catch(() => {
        if (active) setState({ kind: 'error' });
      });

    return () => {
      active = false;
      controller.abort();
      if (objectUrl && typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(objectUrl);
    };
  }, [src]);

  if (state.kind === 'ready') {
    return <img className={cn(styles.img, className)} src={state.objectUrl} alt={alt} />;
  }

  return (
    <div
      className={cn(styles.box, className)}
      data-kind={state.kind}
      style={{ minHeight: `${height}px` }}
      role={state.kind === 'error' ? 'img' : 'status'}
      aria-label={state.kind === 'error' ? `${alt} — rasm yuklanmadi` : undefined}
      aria-live={state.kind === 'loading' ? 'polite' : undefined}
    >
      {state.kind === 'loading' ? 'Yuklanmoqda…' : 'Rasm yuklanmadi'}
    </div>
  );
}
