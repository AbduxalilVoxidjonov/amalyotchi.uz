import { useState } from 'react';
import { Button, Modal } from '@/shared/ui';
import { useSetPracticePeriodGroups } from '../hooks';
import type { PracticePeriodDetail, PracticePeriodGroup } from '../types';
import { GroupPicker } from './GroupPicker';
import { ServerErrorBanner } from './ServerErrorBanner';
import styles from './PeriodDetail.module.css';

export interface AddGroupsModalProps {
  period: PracticePeriodDetail;
  onClose: () => void;
}

/**
 * "Guruh qo'shish": yaratishdagi `GroupPicker`; mavjud guruhlar oldindan belgilangan.
 * Saqlash — `PUT /groups` (to'liq ro'yxat). Har ochilishda yangi mount.
 */
export function AddGroupsModal({ period, onClose }: AddGroupsModalProps) {
  const setGroups = useSetPracticePeriodGroups(period.id);
  const [selected, setSelected] = useState<PracticePeriodGroup[]>(period.groups);

  const initialIds = new Set(period.groups.map((g) => g.id));
  const changed =
    selected.length !== initialIds.size || selected.some((g) => !initialIds.has(g.id));

  function close() {
    if (!setGroups.isPending) onClose();
  }

  return (
    <Modal
      open
      onClose={close}
      title="Guruh qo'shish"
      description={`«${period.name}» davriga biriktiriladigan guruhlarni tanlang.`}
      width="min(880px, 96vw)"
      footer={
        <>
          <Button type="button" onClick={close} disabled={setGroups.isPending}>
            Bekor qilish
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={!changed || setGroups.isPending}
            onClick={() =>
              setGroups.mutate(
                selected.map((g) => g.id),
                { onSuccess: onClose },
              )
            }
          >
            {setGroups.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <div className={styles.modalBody}>
        <ServerErrorBanner error={setGroups.error} title="Saqlab bo'lmadi" />
        <GroupPicker
          selected={selected}
          onChange={setSelected}
          startDate={period.startDate}
          endDate={period.endDate}
          currentPeriodId={period.id}
          disabled={setGroups.isPending}
        />
      </div>
    </Modal>
  );
}
