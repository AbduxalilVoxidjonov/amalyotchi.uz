import { useEffect, useState } from 'react';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';
import styles from './FacePhoto.module.css';

type State = { kind: 'loading' } | { kind: 'ready'; url: string } | { kind: 'error' };

/**
 * Etalon rasmi (`/api/files/{id}`) — Bearer talab qiladi, oddiy `<img src>` 401 oladi:
 * `fetch` + `Authorization` → blob → object URL (dashboard `AuthImage` naqshi). Unmount'da bo'shatiladi.
 */
export function FacePhoto({ src, alt }: { src: string; alt: string }) {
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
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.blob();
      })
      .then((blob) => {
        if (!active) return;
        if (typeof URL.createObjectURL !== 'function') throw new Error('createObjectURL yo‘q');
        objectUrl = URL.createObjectURL(blob);
        setState({ kind: 'ready', url: objectUrl });
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

  if (state.kind === 'ready') return <img className={styles.img} src={state.url} alt={alt} />;
  return (
    <div
      className={styles.box}
      role={state.kind === 'error' ? 'img' : 'status'}
      aria-label={state.kind === 'error' ? `${alt} — rasm yuklanmadi` : undefined}
    >
      {state.kind === 'loading' ? 'Yuklanmoqda…' : 'Rasm yuklanmadi'}
    </div>
  );
}
