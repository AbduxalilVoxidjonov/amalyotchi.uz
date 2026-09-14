import type { HTMLAttributes } from 'react';
import { cn } from '../cn';
import styles from './Avatar.module.css';
import { initialsOf } from './initials';

export type AvatarVariant = 'table' | 'card' | 'detail' | 'sidebar';

export interface AvatarProps extends HTMLAttributes<HTMLSpanElement> {
  /** To'liq ism — bosh harflar avtomatik. */
  name?: string | null;
  /** Bosh harflarni qo'lda berish (name'dan ustun). */
  initials?: string;
  /** table 28px · card 32px · detail 46px r10 · sidebar 32px dark. */
  variant?: AvatarVariant;
}

export function Avatar({ name, initials, variant = 'table', className, ...rest }: AvatarProps) {
  return (
    <span
      className={cn(styles.avatar, className)}
      data-variant={variant}
      aria-hidden={rest['aria-label'] ? undefined : true}
      {...rest}
    >
      {initials ?? initialsOf(name)}
    </span>
  );
}
