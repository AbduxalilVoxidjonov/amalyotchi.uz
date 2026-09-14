import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './Chip.module.css';

export interface ChipProps extends HTMLAttributes<HTMLSpanElement> {
  /** file (fayl nomi, chegarali) · fmt (format: "PDF"). */
  variant?: 'file' | 'fmt';
}

export function Chip({ variant = 'file', className, ...rest }: ChipProps) {
  return <span className={cn(styles.chip, className)} data-variant={variant} {...rest} />;
}

/** Chip'lar qatori (gap 8px, wrap). */
export function ChipRow({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn(styles.row, className)} {...rest} />;
}

export interface FileBoxProps extends HTMLAttributes<HTMLDivElement> {
  name: ReactNode;
  meta?: ReactNode;
}

/** Fayl bloki: nom + meta (SPEC-TOKENS 4.14 file-box). */
export function FileBox({ name, meta, className, ...rest }: FileBoxProps) {
  return (
    <div className={cn(styles.fileBox, className)} {...rest}>
      <div className={styles.fileName}>{name}</div>
      {meta && <div className={styles.fileMeta}>{meta}</div>}
    </div>
  );
}
