import { useEffect, useState } from 'react';
import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom';
import { isApiError } from '@amaliyotchi/shared';
import { isChunkLoadError, reloadOnceForChunkError } from '@/shared/lib/chunk-reload';
import { Button } from '@/shared/ui';
import styles from './RouteError.module.css';

export interface RouteErrorProps {
  /** `page` — AppShell ichida (sidebar/topbar ishlashda davom etadi); `root` — butun ekran. */
  scope?: 'page' | 'root';
}

/** Qisqa texnik tafsilot (stack'siz — production'da minifikatsiyalangan, foydasiz). */
function technicalDetails(error: unknown): string {
  if (isRouteErrorResponse(error)) return `${error.status} ${error.statusText}`;
  if (isApiError(error))
    return `${error.status} ${error.message}${error.traceId ? ` (traceId: ${error.traceId})` : ''}`;
  if (error instanceof Error) return `${error.name}: ${error.message}`.slice(0, 300);
  return String(error).slice(0, 300);
}

/**
 * React Router `errorElement` — standart inglizcha "Unexpected Application Error!" (stack bilan)
 * o'rniga o'zbekcha do'stona ekran. Chunk xatosida sahifa bir marta avtomatik qayta yuklanadi.
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
  }, [error]);

  const notFound = isRouteErrorResponse(error) && error.status === 404;
  const message = reloading
    ? 'Ilova yangilanmoqda…'
    : chunkError
      ? 'Ilovaning yangi versiyasi chiqdi. Sahifani qayta yuklang.'
      : notFound
        ? 'Bunday sahifa topilmadi.'
        : "Kutilmagan xato yuz berdi. Sahifani qayta yuklab ko'ring yoki bosh sahifaga qayting.";

  const body = (
    <section className={styles.card} role="alert">
      <h2 className={styles.title}>Sahifani ochib bo'lmadi</h2>
      <p className={styles.text}>{message}</p>
      {!reloading && !chunkError && !notFound && (
        <details className={styles.details}>
          <summary>Texnik tafsilot</summary>
          <pre>{technicalDetails(error)}</pre>
        </details>
      )}
      <div className={styles.actions}>
        <Button variant="primary" size="sm" onClick={() => window.location.reload()}>
          Qayta yuklash
        </Button>
        <Button size="sm" onClick={() => void navigate('/', { replace: true })}>
          Bosh sahifaga
        </Button>
      </div>
    </section>
  );

  return scope === 'root' ? (
    <main className={styles.root}>{body}</main>
  ) : (
    <div className={styles.page}>{body}</div>
  );
}

export default RouteError;
