import {
  cloneElement,
  forwardRef,
  isValidElement,
  type ButtonHTMLAttributes,
  type ReactElement,
  type ReactNode,
} from 'react';
import { cn } from '../cn';
import styles from './Button.module.css';

export type ButtonVariant =
  'primary' | 'secondary' | 'danger' | 'dashed' | 'alert' | 'ghost-dark' | 'score' | 'checkin';
export type ButtonSize = 'xs' | 'sm' | 'md' | 'lg';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** SPEC-TOKENS 4.1. Default — `secondary` (oq, chegarali). */
  variant?: ButtonVariant;
  /** xs 12.5px · sm 13px · md 13.5px (default) · lg 15px (login). */
  size?: ButtonSize;
  /** Form tugmalari (Yuborish) — radius 8px. */
  radius?: 'md' | 'md2';
  /** width:100%. */
  block?: boolean;
  /** `checkin` varianti uchun: KELDIM (accent) / KETDIM (dark). */
  tone?: 'accent' | 'dark';
  /** Bolani (masalan `<Link>`) tugma ko'rinishida render qiladi — class'lar unga qo'shiladi. */
  asChild?: boolean;
  /** Matndan oldingi ikon/slot. */
  leading?: ReactNode;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    variant = 'secondary',
    size = 'md',
    radius = 'md',
    block = false,
    tone = 'accent',
    asChild = false,
    leading,
    className,
    type = 'button',
    children,
    ...rest
  },
  ref,
) {
  const dataProps = {
    'data-variant': variant,
    'data-size': size,
    'data-radius': radius,
    'data-block': block ? 'true' : undefined,
    'data-tone': variant === 'checkin' ? tone : undefined,
  } as const;
  const classes = cn(styles.btn, className);

  if (asChild && isValidElement(children)) {
    const child = children as ReactElement<{ className?: string; children?: ReactNode }>;
    return cloneElement(child, {
      ...dataProps,
      ...rest,
      className: cn(classes, child.props.className),
    } as Partial<typeof child.props>);
  }

  return (
    <button ref={ref} type={type} className={classes} {...dataProps} {...rest}>
      {leading}
      {children}
    </button>
  );
});
