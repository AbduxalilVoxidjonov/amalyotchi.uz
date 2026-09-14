import { forwardRef, type TextareaHTMLAttributes } from 'react';
import { cn } from '../cn';
import { Field, fieldStyles, type FieldBaseProps } from './Field';

export interface TextareaProps
  extends FieldBaseProps, Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'id' | 'className'> {
  id?: string;
  className?: string;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
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
        <textarea
          ref={ref}
          id={fieldId}
          className={cn(fieldStyles.control, fieldStyles.textarea, className)}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          {...controlProps}
          {...rest}
        />
      )}
    </Field>
  );
});
