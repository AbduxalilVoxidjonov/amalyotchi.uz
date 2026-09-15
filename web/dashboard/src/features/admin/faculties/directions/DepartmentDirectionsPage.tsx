import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog } from '@/shared/ui';
import { tableState, useListParams } from '../../shared/useListParams';
import { EntityFormModal } from '../components/EntityFormModal';
import { HierarchyListPage } from '../components/HierarchyListPage';
import { useDepartmentQuery } from '../departments/hooks';
import { DirectionsTable } from './components/DirectionsTable';
import {
  useCreateDirection,
  useDeleteDirection,
  useDirectionsQuery,
  useSetDirectionStatus,
  useUpdateDirection,
} from './hooks';
import type { DirectionRow } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; direction: DirectionRow } | null;

/** Admin · Kafedra ichidagi yo'nalishlar (ierarxiya 3-daraja). */
export function DepartmentDirectionsPage() {
  const { facultyId = '', departmentId = '' } = useParams<{
    facultyId: string;
    departmentId: string;
  }>();
  const departmentQuery = useDepartmentQuery(departmentId);

  const list = useListParams();
  const query = useDirectionsQuery(departmentId, list.params);

  const createDirection = useCreateDirection(departmentId, facultyId);
  const updateDirection = useUpdateDirection(departmentId, facultyId);
  const setStatus = useSetDirectionStatus(departmentId, facultyId);
  const deleteDirection = useDeleteDirection(departmentId, facultyId);

  const [form, setForm] = useState<FormState>(null);
  const [deleting, setDeleting] = useState<DirectionRow | null>(null);

  function closeForm() {
    setForm(null);
    createDirection.reset();
    updateDirection.reset();
  }

  function closeDelete() {
    setDeleting(null);
    deleteDirection.reset();
  }

  const activeMutation = form?.mode === 'edit' ? updateDirection : createDirection;

  return (
    <HierarchyListPage
      detailQuery={departmentQuery}
      buildBreadcrumb={(department) => [
        { label: 'Fakultetlar', to: '/admin/faculties' },
        { label: department.facultyName, to: `/admin/faculties/${department.facultyId}` },
        { label: department.name },
      ]}
      loadingBreadcrumb={[
        { label: 'Fakultetlar', to: '/admin/faculties' },
        { label: '…', to: `/admin/faculties/${facultyId}` },
        { label: '…' },
      ]}
      notFoundTitle="Kafedra topilmadi."
      backTo={`/admin/faculties/${facultyId}`}
      backLabel="Kafedralarga qaytish"
      pageTitle={(department) => department.name}
    >
      <DirectionsTable
        facultyId={facultyId}
        departmentId={departmentId}
        {...tableState(list, query)}
        onCreate={() => setForm({ mode: 'create' })}
        onEdit={(direction) => setForm({ mode: 'edit', direction })}
        onToggleStatus={(direction) =>
          setStatus.mutate({ id: direction.id, isActive: !direction.isActive })
        }
        onDelete={(direction) => {
          deleteDirection.reset();
          setDeleting(direction);
        }}
      />

      <EntityFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.direction : null}
        titleCreate="Yangi yo'nalish"
        titleEdit="Yo'nalishni tahrirlash"
        isPending={activeMutation.isPending}
        isError={activeMutation.isError}
        error={activeMutation.error}
        onSubmit={(values) => {
          if (form?.mode === 'edit' && form.direction) {
            updateDirection.mutate(
              { id: form.direction.id, body: values },
              { onSuccess: closeForm },
            );
          } else {
            createDirection.mutate(values, { onSuccess: closeForm });
          }
        }}
        onClose={closeForm}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Yo'nalishni o'chirish"
        confirmLabel="O'chirish"
        description={
          deleting
            ? `«${deleting.name}» yo'nalishini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`
            : undefined
        }
        danger
        isLoading={deleteDirection.isPending}
        error={deleteDirection.isError ? errorMessage(deleteDirection.error) : undefined}
        onCancel={closeDelete}
        onConfirm={() => {
          if (!deleting) return;
          deleteDirection.mutate(deleting.id, { onSuccess: closeDelete });
        }}
      />
    </HierarchyListPage>
  );
}

export default DepartmentDirectionsPage;
