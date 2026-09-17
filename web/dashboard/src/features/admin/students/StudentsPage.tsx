import { useState } from 'react';
import { tableState, useListParams } from '../shared/useListParams';
import { StudentImportModal } from './components/StudentImportModal';
import { StudentsTable } from './components/StudentsTable';
import { useStudentImportTemplate, useStudentsQuery } from './hooks';

/** Admin · Talabalar (SPEC-SCREENS §9.6). Container. */
export function StudentsPage() {
  const list = useListParams();
  const query = useStudentsQuery(list.params);
  const template = useStudentImportTemplate();
  const [importOpen, setImportOpen] = useState(false);

  return (
    <>
      <StudentsTable
        {...tableState(list, query)}
        onDownloadTemplate={template.download}
        templateLoading={template.isLoading}
        templateError={template.error}
        onImportExcel={() => setImportOpen(true)}
        // TODO: eksport oqimi dizaynda yo'q (❓) — hozircha no-op.
        onExport={() => undefined}
      />
      <StudentImportModal open={importOpen} onClose={() => setImportOpen(false)} />
    </>
  );
}

export default StudentsPage;
