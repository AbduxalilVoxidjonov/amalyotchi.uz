import type { HTMLAttributes } from 'react';
import { cn } from '../cn';
import styles from './Eyebrow.module.css';

export interface EyebrowProps extends HTMLAttributes<HTMLElement> {
  /** Semantik teg: h2/h3 (section sarlavhasi) yoki div/span. Default `h3`. */
  as?: 'h2' | 'h3' | 'h4' | 'div' | 'span';
  /** .09em (default) yoki .1em (crumb, "Diqqat talab qiladi"). */
  spacing?: 'normal' | 'wide';
  tone?: 'default' | 'alert';
  /** margin-bottom:10px (default) yoki 0. */
  margin?: 'default' | 'none';
}

export function Eyebrow({
  as: Tag = 'h3',
  spacing = 'normal',
  tone = 'default',
  margin = 'default',
  className,
  ...rest
}: EyebrowProps) {
  return (
    <Tag
      className={cn(styles.eyebrow, className)}
      data-spacing={spacing}
      data-tone={tone}
      data-margin={margin}
      {...rest}
    />
  );
}
