import { useEffect, useState } from 'react';
import { Button } from '@/shared/ui';
import { copyText } from './copyText';
import styles from './StudentPasswordModal.module.css';

/**
 * Talabaga beriladigan kirish ma'lumoti (`studentCredentialsText`) + "Nusxalash" tugmasi. Yaratish
 * formasi va import natijasi bilan umumiy.
 */
export function CredentialsBox({ text, label = 'Nusxalash' }: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return undefined;
    const t = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(t);
  }, [copied]);

  return (
    <div className={styles.done}>
      <p className={styles.credentials} data-testid="student-credentials">
        {text}
      </p>
      <div className={styles.tools}>
        <Button
          type="button"
          size="xs"
          onClick={() => void copyText(text).then((ok) => setCopied(ok))}
        >
          {label}
        </Button>
        {copied && (
          <span role="status" className={styles.copied}>
            Nusxalandi
          </span>
        )}
      </div>
    </div>
  );
}
