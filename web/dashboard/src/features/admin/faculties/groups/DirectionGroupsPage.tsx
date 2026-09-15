import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog } from '@/shared/ui';
import { tableState, useListParams } from '../../shared/useListParams';
import { useDirectionQuery } from '../directions/hooks';
import { HierarchyListPage } from '../components/HierarchyListPage';
import { DirectionGroupsTable } from './components/DirectionGroupsTable';
import { GroupFormModal } from './components/GroupFormModal';
import { useDeleteGroup, useGroupsQuery, useSetGroupStatus } from './hooks';
import type { GroupRow } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; group: GroupRow } | null;

/** Admin · Yo'nalish ichidagi guruhlar (ierarxiya 4-daraja, yaproq). */
export function DirectionGroupsPage() {
  const {
    facultyId = '',
    departmentId = '',
    directionId = '',
  } = useParams<{ facultyId: string; departmentId: string; directionId: string }>();
  const directionQuery = useDirectionQuery(directionId);

  const list = useListParams();
  const query = useGroupsQuery(directionId, list.params);

  const setStatus = useSetGroupStatus(directionId, departmentId, facultyId);
  const deleteGroup = useDeleteGroup(directionId, departmentId, facultyId);

  const [form, setForm] = useState<FormState>(null);
  const [deleting, setDeleting] = useState<GroupRow | null>(null);

  function closeDelete() {
    setDeleting(null);
    deleteGroup.reset();
  }

  return (
    <HierarchyListPage
      detailQuery={directionQuery}
      buildBreadcrumb={(direction) => [
        { label: 'Fakultetlar', to: '/admin/faculties' },
        { label: direction.facultyName, to: `/admin/faculties/${direction.facultyId}` },
        {
          label: direction.departmentName,
          to: `/admin/faculties/${direction.facultyId}/departments/${direction.departmentId}`,
        },
        { label: direction.name },
      ]}
      loadingBreadcrumb={[
        { label: 'Fakultetlar', to: '/admin/faculties' },
        { label: '…', to: `/admin/faculties/${facultyId}` },
        { label: '…', to: `/admin/faculties/${facultyId}/departments/${departmentId}` },
        { label: '…' },
      ]}
      notFoundTitle="Yo'nalish topilmadi."
      backTo={`/admin/faculties/${facultyId}/departments/${departmentId}`}
      backLabel="Yo'nalishlarga qaytish"
      pageTitle={(direction) => direction.name}
    >
      <DirectionGroupsTable
        {...tableState(list, query)}
        onCreate={() => setForm({ mode: 'create' })}
        onEdit={(group) => setForm({ mode: 'edit', group })}
        onToggleStatus={(group) => setStatus.mutate({ id: group.id, isActive: !group.isActive })}
        onDelete={(group) => {
          deleteGroup.reset();
          setDeleting(group);
        }}
      />

      <GroupFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.group : null}
        directionId={directionId}
        departmentId={departmentId}
        facultyId={facultyId}
        onClose={() => setForm(null)}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Guruhni o'chirish"
        confirmLabel="O'chirish"
        description={
          deleting
            ? `«${deleting.code}» guruhini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`
            : undefined
        }
        danger
        isLoading={deleteGroup.isPending}
        error={deleteGroup.isError ? errorMessage(deleteGroup.error) : undefined}
        onCancel={closeDelete}
        onConfirm={() => {
          if (!deleting) return;
          deleteGroup.mutate(deleting.id, { onSuccess: closeDelete });
        }}
      />
    </HierarchyListPage>
  );
}

export default DirectionGroupsPage;
