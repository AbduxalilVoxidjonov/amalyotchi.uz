import {
  useEffect,
  useId,
  useRef,
  type CSSProperties,
  type KeyboardEvent as ReactKeyboardEvent,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';
import { Button, cn, type ButtonVariant } from '@amaliyotchi/shared/ui';
import styles from './Modal.module.css';

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  children?: ReactNode;
  /** Pastki qator (odatda tugmalar). */
  footer?: ReactNode;
  /** Panel kengligi, masalan `'440px'`. Default — CSS'dagi qiymat. */
  width?: string;
  className?: string;
}

const FOCUSABLE =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), ' +
  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Umumiy modal (SPEC-TOKENS'da nomlanmagan, `Card` uslubiga mos). Esc yopadi, overlay bosilsa yopadi,
 * ochilganda fokus ichkariga o'tadi va Tab shu doirada aylanadi, `body` scroll'i qulflanadi.
 */
export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  width,
  className,
}: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const bodyRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descId = useId();

  // body scroll qulfi — bir nechta modal ochilib yopilsa ham to'g'ri tiklanishi uchun sanoq.
  useEffect(() => {
    if (!open) return undefined;
    const root = document.body;
    const prev = root.style.overflow;
    root.style.overflow = 'hidden';
    return () => {
      root.style.overflow = prev;
    };
  }, [open]);

  // Ochilganda fokusni ichkariga o'tkazish, yopilganda oldingi elementga qaytarish.
  useEffect(() => {
    if (!open) return undefined;
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const panel = panelRef.current;
    // Avval mazmun (forma maydoni) ichidan qidiramiz — bo'lmasa panel ichidan (masalan yopish tugmasi).
    const target =
      bodyRef.current?.querySelector<HTMLElement>(FOCUSABLE) ??
      panel?.querySelector<HTMLElement>(FOCUSABLE) ??
      panel;
    target?.focus();
    return () => {
      previouslyFocused?.focus?.();
    };
  }, [open]);

  if (!open) return null;

  function handleKeyDown(e: ReactKeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') {
      e.stopPropagation();
      onClose();
      return;
    }
    if (e.key !== 'Tab') return;
    const panel = panelRef.current;
    if (!panel) return;
    const focusables = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (focusables.length === 0) return;
    const first = focusables[0]!;
    const last = focusables[focusables.length - 1]!;
    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  }

  function handleOverlayMouseDown(e: ReactMouseEvent<HTMLDivElement>) {
    if (e.target === e.currentTarget) onClose();
  }

  return createPortal(
    <div className={styles.overlay} onMouseDown={handleOverlayMouseDown}>
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descId : undefined}
        className={cn(styles.panel, className)}
        style={width ? ({ '--modal-w': width } as CSSProperties) : undefined}
        tabIndex={-1}
        onKeyDown={handleKeyDown}
      >
        <div className={styles.head}>
          <h2 id={titleId} className={styles.title}>
            {title}
          </h2>
          <button type="button" className={styles.close} onClick={onClose} aria-label="Yopish">
            ×
          </button>
        </div>
        {description && (
          <p id={descId} className={styles.description}>
            {description}
          </p>
        )}
        {children && (
          <div ref={bodyRef} className={styles.body}>
            {children}
          </div>
        )}
        {footer && <div className={styles.footer}>{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

export interface ConfirmDialogProps {
  open: boolean;
  title: ReactNode;
  description?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  /** `true` — tasdiqlash tugmasi `danger` (o'chirish kabi qaytarib bo'lmas amallar). */
  danger?: boolean;
  isLoading?: boolean;
  /** So'rov xatosi — dialog ichida `role="alert"` bilan ko'rsatiladi. */
  error?: ReactNode;
  onConfirm: () => void;
  onCancel: () => void;
}

/** `Modal` ustidan tasdiqlash dialogi (o'chirish, holat almashtirish kabi qaytarib bo'lmas/muhim amallar). */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = 'Tasdiqlash',
  cancelLabel = 'Bekor qilish',
  danger = false,
  isLoading = false,
  error,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const confirmVariant: ButtonVariant = danger ? 'danger' : 'primary';
  return (
    <Modal
      open={open}
      onClose={onCancel}
      title={title}
      description={description}
      footer={
        <>
          <Button type="button" onClick={onCancel} disabled={isLoading}>
            {cancelLabel}
          </Button>
          <Button type="button" variant={confirmVariant} onClick={onConfirm} disabled={isLoading}>
            {isLoading ? 'Bajarilmoqda…' : confirmLabel}
          </Button>
        </>
      }
    >
      {error ? (
        <p role="alert" className={styles.confirmError}>
          {error}
        </p>
      ) : null}
    </Modal>
  );
}
