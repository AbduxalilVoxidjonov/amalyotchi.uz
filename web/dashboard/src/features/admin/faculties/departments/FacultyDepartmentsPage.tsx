import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog } from '@/shared/ui';
import { tableState, useListParams } from '../../shared/useListParams';
import { EntityFormModal } from '../components/EntityFormModal';
import { HierarchyListPage } from '../components/HierarchyListPage';
import { useFacultyQuery } from '../hooks';
import { DepartmentsTable } from './components/DepartmentsTable';
import {
  useCreateDepartment,
  useDeleteDepartment,
  useDepartmentsQuery,
  useSetDepartmentStatus,
  useUpdateDepartment,
} from './hooks';
import type { DepartmentRow } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; department: DepartmentRow } | null;

/** Admin · Fakultet ichidagi kafedralar (ierarxiya 2-daraja). */
export function FacultyDepartmentsPage() {
  const { facultyId = '' } = useParams<{ facultyId: string }>();
  const facultyQuery = useFacultyQuery(facultyId);

  const list = useListParams();
  const query = useDepartmentsQuery(facultyId, list.params);

  const createDepartment = useCreateDepartment(facultyId);
  const updateDepartment = useUpdateDepartment(facultyId);
  const setStatus = useSetDepartmentStatus(facultyId);
  const deleteDepartment = useDeleteDepartment(facultyId);

  const [form, setForm] = useState<FormState>(null);
  const [deleting, setDeleting] = useState<DepartmentRow | null>(null);

  function closeForm() {
    setForm(null);
    createDepartment.reset();
    updateDepartment.reset();
  }

  function closeDelete() {
    setDeleting(null);
    deleteDepartment.reset();
  }

  const activeMutation = form?.mode === 'edit' ? updateDepartment : createDepartment;

  return (
    <HierarchyListPage
      detailQuery={facultyQuery}
      buildBreadcrumb={(faculty) => [
        { label: 'Fakultetlar', to: '/admin/faculties' },
        { label: faculty.name },
      ]}
      loadingBreadcrumb={[{ label: 'Fakultetlar', to: '/admin/faculties' }, { label: '…' }]}
      notFoundTitle="Fakultet topilmadi."
      backTo="/admin/faculties"
      backLabel="Fakultetlarga qaytish"
      pageTitle={(faculty) => faculty.name}
    >
      <DepartmentsTable
        facultyId={facultyId}
        {...tableState(list, query)}
        onCreate={() => setForm({ mode: 'create' })}
        onEdit={(department) => setForm({ mode: 'edit', department })}
        onToggleStatus={(department) =>
          setStatus.mutate({ id: department.id, isActive: !department.isActive })
        }
        onDelete={(department) => {
          deleteDepartment.reset();
          setDeleting(department);
        }}
      />

      <EntityFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.department : null}
        titleCreate="Yangi kafedra"
        titleEdit="Kafedrani tahrirlash"
        isPending={activeMutation.isPending}
        isError={activeMutation.isError}
        error={activeMutation.error}
        onSubmit={(values) => {
          if (form?.mode === 'edit' && form.department) {
            updateDepartment.mutate(
              { id: form.department.id, body: values },
              { onSuccess: closeForm },
            );
          } else {
            createDepartment.mutate(values, { onSuccess: closeForm });
          }
        }}
        onClose={closeForm}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Kafedrani o'chirish"
        confirmLabel="O'chirish"
        description={
          deleting
            ? `«${deleting.name}» kafedrasini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`
            : undefined
        }
        danger
        isLoading={deleteDepartment.isPending}
        error={deleteDepartment.isError ? errorMessage(deleteDepartment.error) : undefined}
        onCancel={closeDelete}
        onConfirm={() => {
          if (!deleting) return;
          deleteDepartment.mutate(deleting.id, { onSuccess: closeDelete });
        }}
      />
    </HierarchyListPage>
  );
}

export default FacultyDepartmentsPage;
