import { serverErrorLines } from '../validation';
import styles from './ServerErrorBanner.module.css';

/** Server 400/409 xatosi: `detail` + `errors` ro'yxati, forma tepasida (role=alert). */
export function ServerErrorBanner({ error, title }: { error: unknown; title: string }) {
  const lines = serverErrorLines(error);
  if (!lines) return null;
  return (
    <div role="alert" className={styles.banner}>
      <strong className={styles.title}>{title}</strong>
      <p className={styles.message}>{lines.message}</p>
      {lines.details.length > 0 && (
        <ul className={styles.list}>
          {lines.details.map((d) => (
            <li key={d}>{d}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
