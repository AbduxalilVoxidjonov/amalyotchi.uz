import { forwardRef, type SelectHTMLAttributes } from 'react';
import { cn } from '../cn';
import { Field, fieldStyles, type FieldBaseProps } from './Field';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface SelectProps
  extends FieldBaseProps, Omit<SelectHTMLAttributes<HTMLSelectElement>, 'id' | 'className'> {
  id?: string;
  className?: string;
  /** `options` berilsa `<option>`lar shundan; aks holda children. */
  options?: readonly SelectOption[];
  placeholder?: string;
}

/**
 * ❓ Dizaynda `<select>` yo'q (SPEC-TOKENS 4.8) — `readonly` variant uslubida (mono, r7) qilingan.
 */
export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  {
    id,
    label,
    labelEnd,
    hint,
    error,
    mono,
    variant,
    wrapperClassName,
    className,
    options,
    placeholder,
    children,
    ...rest
  },
  ref,
) {
  return (
    <Field
      id={id}
      label={label}
      labelEnd={labelEnd}
      hint={hint}
      error={error}
      mono={mono ?? false}
      variant={variant ?? 'readonly'}
      wrapperClassName={wrapperClassName}
    >
      {({ id: fieldId, describedBy, invalid, controlProps }) => (
        <select
          ref={ref}
          id={fieldId}
          className={cn(fieldStyles.control, fieldStyles.select, className)}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          {...controlProps}
          {...rest}
        >
          {placeholder !== undefined && (
            <option value="" disabled>
              {placeholder}
            </option>
          )}
          {options
            ? options.map((o) => (
                <option key={o.value} value={o.value} disabled={o.disabled}>
                  {o.label}
                </option>
              ))
            : children}
        </select>
      )}
    </Field>
  );
});
