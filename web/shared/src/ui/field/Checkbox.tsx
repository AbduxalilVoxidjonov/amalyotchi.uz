import { forwardRef, type InputHTMLAttributes, type ReactNode } from 'react';
import { cn } from '../cn';
import styles from './Field.module.css';

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode;
  wrapperClassName?: string | undefined;
}

/** 15px, accent-color; label bilan birga (SPEC-TOKENS 4.8). */
export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, wrapperClassName, className, ...rest },
  ref,
) {
  return (
    <label className={cn(styles.check, wrapperClassName)}>
      <input ref={ref} type="checkbox" className={cn(styles.checkInput, className)} {...rest} />
      <span>{label}</span>
    </label>
  );
});
