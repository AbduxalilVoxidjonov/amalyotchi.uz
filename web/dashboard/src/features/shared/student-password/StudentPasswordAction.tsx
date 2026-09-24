import { useState } from 'react';
import { Badge, Button } from '@/shared/ui';
import type { StudentPasswordArea } from './api';
import { StudentPasswordModal } from './StudentPasswordModal';
import styles from './StudentPasswordModal.module.css';

export interface StudentPasswordActionProps {
  area: StudentPasswordArea;
  student: { id: string; name: string; hemisId: string; hasPassword: boolean };
}

/**
 * Talaba profili sarlavhasidagi amal: parol holati belgisi + "Parol o'rnatish" tugmasi va modal.
 * Admin va tyutor sahifalarida bir xil (`area` faqat endpoint'ni tanlaydi).
 */
export function StudentPasswordAction({ area, student }: StudentPasswordActionProps) {
  const [open, setOpen] = useState(false);
  return (
    <div className={styles.action}>
      <Badge status={student.hasPassword ? 'ok' : 'neu'} size="md">
        {student.hasPassword ? "Parol o'rnatilgan" : "Parol yo'q"}
      </Badge>
      <Button size="xs" onClick={() => setOpen(true)}>
        Parol o'rnatish
      </Button>
      <StudentPasswordModal
        open={open}
        area={area}
        student={student}
        onClose={() => setOpen(false)}
      />
    </div>
  );
}
