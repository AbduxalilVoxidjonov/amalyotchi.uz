import { useMemo, useState } from 'react';
import { tableState, useListParams } from '../shared/useListParams';
import { TutorFormModal } from './components/TutorFormModal';
import { TutorsTable } from './components/TutorsTable';
import { useFacultyOptions, useSetTutorStatus, useTutorsQuery } from './hooks';
import type { Tutor, TutorListParams } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; tutor: Tutor } | null;

/** Admin · Tyutorlar (SPEC-SCREENS §9.5). Container: jadval + fakultet filtri + yaratish/tahrirlash/holat. */
export function TutorsPage() {
  const list = useListParams();
  const [facultyId, setFacultyId] = useState('');
  const params = useMemo<TutorListParams>(
    () => (facultyId ? { ...list.params, facultyId } : list.params),
    [list.params, facultyId],
  );
  const query = useTutorsQuery(params);
  const faculties = useFacultyOptions();
  const setStatus = useSetTutorStatus();

  const [form, setForm] = useState<FormState>(null);

  function handleFacultyChange(next: string) {
    setFacultyId(next);
    // Filtr o'zgarganda 1-sahifaga qaytish (`useListParams` faqat `q` bo'yicha qaytaradi).
    list.setPage(1);
  }

  return (
    <>
      <TutorsTable
        {...tableState(list, query)}
        facultyId={facultyId}
        facultyOptions={faculties.data ?? []}
        onFacultyChange={handleFacultyChange}
        onCreate={() => setForm({ mode: 'create' })}
        onEdit={(tutor) => setForm({ mode: 'edit', tutor })}
        onToggleStatus={(tutor) => setStatus.mutate({ id: tutor.id, isActive: !tutor.isActive })}
      />

      <TutorFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.tutor : null}
        onClose={() => setForm(null)}
      />
    </>
  );
}

export default TutorsPage;
