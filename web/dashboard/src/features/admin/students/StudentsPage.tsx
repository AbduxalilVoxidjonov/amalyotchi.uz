import { tableState, useListParams } from '../shared/useListParams';
import { StudentsTable } from './components/StudentsTable';
import { useStudentsQuery } from './hooks';

/** Admin · Talabalar (SPEC-SCREENS §9.6). Container. */
export function StudentsPage() {
  const list = useListParams();
  const query = useStudentsQuery(list.params);
  return (
    <StudentsTable
      {...tableState(list, query)}
      // TODO: import/HEMIS/eksport oqimlari dizaynda yo'q (❓) — hozircha no-op.
      onImportExcel={() => undefined}
      onPullHemis={() => undefined}
      onExport={() => undefined}
    />
  );
}

export default StudentsPage;
