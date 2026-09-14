import { useId, type ReactNode } from 'react';
import { cn } from '../cn';
import styles from './Field.module.css';

export type FieldVariant = 'login' | 'form' | 'diary' | 'search' | 'readonly';

/** Input/Textarea/Select uchun umumiy wrapper props. */
export interface FieldBaseProps {
  label?: ReactNode;
  /** Label qatorining o'ng tomoni (masalan "Parolni tiklash" havolasi). */
  labelEnd?: ReactNode;
  hint?: ReactNode;
  /** Xato matni — ko'rsatilsa `aria-invalid` + `aria-describedby`. */
  error?: ReactNode;
  /** Mono shrift (HEMIS ID, sana, telefon). */
  mono?: boolean;
  /** login (default, r9 14px) · form (r8 13px) · diary · search · readonly. */
  variant?: FieldVariant;
  /** Wrapper class. */
  wrapperClassName?: string | undefined;
}

interface FieldRenderState {
  id: string;
  describedBy: string | undefined;
  invalid: boolean;
  controlProps: { 'data-mono': 'true' | undefined; 'data-variant': FieldVariant };
}

interface FieldProps extends FieldBaseProps {
  id?: string | undefined;
  children: (state: FieldRenderState) => ReactNode;
}

/** Label + control + hint/error tuzilmasi. Har control shu orqali render qilinadi. */
export function Field({
  id: idProp,
  label,
  labelEnd,
  hint,
  error,
  mono = false,
  variant = 'login',
  wrapperClassName,
  children,
}: FieldProps) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = cn(hint ? hintId : '', error ? errorId : '') || undefined;

  return (
    <div className={cn(styles.field, wrapperClassName)}>
      {(label || labelEnd) && (
        <div className={styles.labelRow}>
          {label ? (
            <label htmlFor={id} className={styles.label}>
              {label}
            </label>
          ) : (
            <span />
          )}
          {labelEnd && <span className={styles.labelEnd}>{labelEnd}</span>}
        </div>
      )}
      {children({
        id,
        describedBy,
        invalid: Boolean(error),
        controlProps: { 'data-mono': mono ? 'true' : undefined, 'data-variant': variant },
      })}
      {hint && !error && (
        <p id={hintId} className={styles.hint}>
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className={styles.error} role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

export const fieldStyles = styles;
