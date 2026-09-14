import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './MapPlaceholder.module.css';

export interface MapPlaceholderProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title?: ReactNode;
  /** Mono koordinata: "41.3111, 69.2797". */
  coords?: ReactNode;
  /** px: 150 (joyim). */
  height?: number;
}

/** Xarita o'rnidagi dashed blok (SPEC-TOKENS 4.15). Xarita kutubxonasi TWA bundle'iga qo'shilmaydi. */
export function MapPlaceholder({
  title = 'Xarita',
  coords,
  height = 150,
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
    </div>
  );
}
