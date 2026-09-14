import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './MapPlaceholder.module.css';

export interface MapPlaceholderProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title?: ReactNode;
  /** Mono koordinata: "41.3111, 69.2797". */
  coords?: ReactNode;
  note?: ReactNode;
  /** px: 186 (ariza detail) · 150 (joyim) · 460 (xarita). */
  height?: number;
}

/** Xarita o'rnidagi dashed blok (haqiqiy xarita — keyingi bosqich). */
export function MapPlaceholder({
  title = 'Xarita',
  coords,
  note,
  height = 186,
  className,
  style,
  ...rest
}: MapPlaceholderProps) {
  return (
    <div
      className={cn(styles.map, className)}
      role="img"
      aria-label={typeof title === 'string' ? title : 'Xarita'}
      style={{ ...style, '--map-h': `${height}px` } as CSSProperties}
      {...rest}
    >
      <div className={styles.title}>{title}</div>
      {coords && <div className={styles.coords}>{coords}</div>}
      {note && <div className={styles.note}>{note}</div>}
    </div>
  );
}
