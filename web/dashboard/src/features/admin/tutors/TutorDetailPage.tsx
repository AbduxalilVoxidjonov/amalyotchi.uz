import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog, type BreadcrumbItem } from '@/shared/ui';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { GroupsPickerModal } from './components/GroupsPickerModal';
import { PasswordResetModal } from './components/PasswordResetModal';
import { TutorDetailView } from './components/TutorDetailView';
import { TutorFormModal } from './components/TutorFormModal';
import { useSetTutorStatus, useTutorQuery } from './hooks';

const ROOT: BreadcrumbItem[] = [{ label: 'Tyutorlar', to: '/admin/tutors' }];

type Dialog = 'edit' | 'password' | 'status' | 'groups' | null;

/** Admin · Tyutor sahifasi (`/admin/tutors/:tutorId`): ma'lumotlar, parol tiklash, holat, guruh biriktirish. */
export function TutorDetailPage() {
  const { tutorId = '' } = useParams<{ tutorId: string }>();
  const tutorQuery = useTutorQuery(tutorId);
  const setStatus = useSetTutorStatus();

  const [dialog, setDialog] = useState<Dialog>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // Muvaffaqiyat xabari bir necha soniyadan keyin yo'qoladi.
  useEffect(() => {
    if (!notice) return undefined;
    const t = setTimeout(() => setNotice(null), 5000);
    return () => clearTimeout(t);
  }, [notice]);

  function open(next: Exclude<Dialog, null>) {
    setNotice(null);
    setStatus.reset();
    setDialog(next);
  }

  function close() {
    setDialog(null);
  }

  const tutor = tutorQuery.data;

  return (
    <HierarchyListPage
      detailQuery={tutorQuery}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(t) => [...ROOT, { label: t.fullName }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Tyutor topilmadi."
      backTo="/admin/tutors"
      backLabel="Tyutorlarga qaytish"
      pageTitle={(t) => t.fullName}
    >
      {tutor ? (
        <TutorDetailView
          tutor={tutor}
          notice={notice}
          onEdit={() => open('edit')}
          onResetPassword={() => open('password')}
          onToggleStatus={() => open('status')}
          onEditGroups={() => open('groups')}
        />
      ) : (
        <LoadingState />
      )}

      {tutor && (
        <>
          <TutorFormModal open={dialog === 'edit'} mode="edit" initial={tutor} onClose={close} />

          <PasswordResetModal
            open={dialog === 'password'}
            tutorId={tutor.id}
            tutorName={tutor.fullName}
            onClose={close}
            onSuccess={() => setNotice('Parol yangilandi.')}
          />

          <GroupsPickerModal
            open={dialog === 'groups'}
            tutorId={tutor.id}
            currentGroupIds={tutor.groups.map((g) => g.groupId)}
            onClose={close}
            onSaved={() => setNotice('Guruhlar saqlandi.')}
          />

          <ConfirmDialog
            open={dialog === 'status'}
            title={tutor.isActive ? 'Tyutorni faol emas qilish' : 'Tyutorni faollashtirish'}
            confirmLabel={tutor.isActive ? 'Faol emas qilish' : 'Faollashtirish'}
            description={
              tutor.isActive
                ? `${tutor.fullName} tizimga kira olmaydi va guruhlari bo'yicha amallar bajara olmaydi. Davom etasizmi?`
                : `${tutor.fullName} yana tizimga kira oladi. Davom etasizmi?`
            }
            danger={tutor.isActive}
            isLoading={setStatus.isPending}
            error={setStatus.isError ? errorMessage(setStatus.error) : undefined}
            onCancel={close}
            onConfirm={() =>
              setStatus.mutate(
                { id: tutor.id, isActive: !tutor.isActive },
                {
                  onSuccess: () => {
                    close();
                    setNotice(
                      tutor.isActive ? 'Tyutor faol emas qilindi.' : 'Tyutor faollashtirildi.',
                    );
                  },
                },
              )
            }
          />
        </>
      )}
    </HierarchyListPage>
  );
}

export default TutorDetailPage;
