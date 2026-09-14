import { forwardRef, type InputHTMLAttributes } from 'react';
import { cn } from '../cn';
import { Field, fieldStyles, type FieldBaseProps } from './Field';

export interface InputProps
  extends FieldBaseProps, Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'className'> {
  id?: string;
  className?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { id, label, labelEnd, hint, error, mono, variant, wrapperClassName, className, ...rest },
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
      variant={variant ?? 'login'}
      wrapperClassName={wrapperClassName}
    >
      {({ id: fieldId, describedBy, invalid, controlProps }) => (
        <input
          ref={ref}
          id={fieldId}
          className={cn(fieldStyles.control, className)}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          {...controlProps}
          {...rest}
        />
      )}
    </Field>
  );
});
