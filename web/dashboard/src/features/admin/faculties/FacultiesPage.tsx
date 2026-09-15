import { useState } from 'react';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog } from '@/shared/ui';
import { tableState, useListParams } from '../shared/useListParams';
import { FacultiesTable } from './components/FacultiesTable';
import { FacultyFormModal } from './components/FacultyFormModal';
import { useDeleteFaculty, useFacultiesQuery, useSetFacultyStatus } from './hooks';
import type { Faculty } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; faculty: Faculty } | null;

/** Admin · Fakultetlar (SPEC-SCREENS §9.3). Container: jadval + yaratish/tahrirlash/o'chirish/holat. */
export function FacultiesPage() {
  const list = useListParams();
  const query = useFacultiesQuery(list.params);
  const setStatus = useSetFacultyStatus();
  const deleteFaculty = useDeleteFaculty();

  const [form, setForm] = useState<FormState>(null);
  const [deleting, setDeleting] = useState<Faculty | null>(null);

  function closeDelete() {
    setDeleting(null);
    deleteFaculty.reset();
  }

  return (
    <>
      <FacultiesTable
        {...tableState(list, query)}
        // ❓ Excel eksporti — dizaynda yo'q, keyingi bosqich.
        onCreate={() => setForm({ mode: 'create' })}
        onExport={() => undefined}
        onEdit={(faculty) => setForm({ mode: 'edit', faculty })}
        onToggleStatus={(faculty) =>
          setStatus.mutate({ id: faculty.id, isActive: !faculty.isActive })
        }
        onDelete={(faculty) => {
          deleteFaculty.reset();
          setDeleting(faculty);
        }}
      />

      <FacultyFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.faculty : null}
        onClose={() => setForm(null)}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Fakultetni o'chirish"
        confirmLabel="O'chirish"
        description={
          deleting
            ? `«${deleting.name}» fakultetini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`
            : undefined
        }
        danger
        isLoading={deleteFaculty.isPending}
        error={deleteFaculty.isError ? errorMessage(deleteFaculty.error) : undefined}
        onCancel={closeDelete}
        onConfirm={() => {
          if (!deleting) return;
          deleteFaculty.mutate(deleting.id, { onSuccess: closeDelete });
        }}
      />
    </>
  );
}

export default FacultiesPage;
