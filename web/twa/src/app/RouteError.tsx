import { useEffect, useState } from 'react';
import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom';
import { isChunkLoadError, reloadOnceForChunkError } from '@/shared/lib/chunk-reload';
import { diagError } from '@/shared/lib/diag';
import { Button, LoadingState } from '@/shared/ui';
import styles from './RouteError.module.css';

export interface RouteErrorProps {
  /** `page` — AppShell ichida (tab-bar ishlashda davom etadi); `root` — butun ekran. */
  scope?: 'page' | 'root';
}

/** Xato → qisqa texnik tafsilot (`<details>` ichida). */
function technicalDetails(error: unknown): string {
  if (isRouteErrorResponse(error)) {
    const data = typeof error.data === 'string' ? error.data : '';
    return `${error.status} ${error.statusText}${data ? `\n${data}` : ''}`;
  }
  if (error instanceof Error) {
    const stack = error.stack?.split('\n').slice(0, 6).join('\n') ?? '';
    const head = `${error.name}: ${error.message}`;
    return stack.includes(error.message) ? stack : `${head}\n${stack}`.trim();
  }
  try {
    return typeof error === 'string' ? error : JSON.stringify(error);
  } catch {
    return String(error);
  }
}

/**
 * React Router `errorElement` — standart "Unexpected Application Error" o'rniga o'zbekcha do'stona ekran.
 * Chunk xatosida (deploydan keyin eski hash) — sahifa bir marta avtomatik qayta yuklanadi.
 */
export function RouteError({ scope = 'page' }: RouteErrorProps) {
  const error = useRouteError();
  const navigate = useNavigate();
  const chunkError = isChunkLoadError(error);
  const [reloading, setReloading] = useState(false);

  useEffect(() => {
    if (chunkError && reloadOnceForChunkError()) setReloading(true);
  }, [chunkError]);

  useEffect(() => {
    console.error('[RouteError]', error);
    diagError(scope === 'root' ? 'route-error-root' : 'route-error', error);
  }, [error, scope]);

  if (reloading) return <LoadingState height={240} label="Ilova yangilanmoqda…" />;

  const message = chunkError
    ? 'Ilovaning yangi versiyasi chiqdi. Sahifani qayta yuklang.'
    : isRouteErrorResponse(error) && error.status === 404
      ? "Bunday bo'lim topilmadi."
      : "Kutilmagan xato yuz berdi. Qayta yuklab ko'ring yoki bosh ekranga qayting.";

  const body = (
    <section className={styles.card} role="alert">
      <h2 className={styles.title}>Sahifani ochib bo'lmadi</h2>
      <p className={styles.text}>{message}</p>
      <details className={styles.details}>
        <summary>Texnik tafsilot</summary>
        <pre>{technicalDetails(error)}</pre>
      </details>
      <div className={styles.actions}>
        <Button variant="primary" size="sm" onClick={() => window.location.reload()}>
          Qayta yuklash
        </Button>
        <Button size="sm" onClick={() => void navigate('/', { replace: true })}>
          Bosh ekranga
        </Button>
      </div>
    </section>
  );

  return scope === 'root' ? <main className={styles.root}>{body}</main> : body;
}

export default RouteError;
