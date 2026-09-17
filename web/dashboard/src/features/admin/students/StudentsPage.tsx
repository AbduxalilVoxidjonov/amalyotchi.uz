import { useCallback, useEffect, useState } from 'react';
import { ExcelImportModal } from '../components/ExcelImportModal';
import { tableState, useListParams } from '../shared/useListParams';
import { AssignCompanyModal } from './components/AssignCompanyModal';
import { StudentsTable } from './components/StudentsTable';
import { useImportStudents, useStudentImportTemplate, useStudentsQuery } from './hooks';

/** Admin · Talabalar (SPEC-SCREENS §9.6). Container. */
export function StudentsPage() {
  const list = useListParams();
  const query = useStudentsQuery(list.params);
  const template = useStudentImportTemplate();
  const importStudents = useImportStudents();
  const [importOpen, setImportOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<ReadonlySet<string>>(() => new Set());

  const { page, params } = list;
  const rows = query.data?.items;

  // Sahifa yoki qidiruv o'zgarsa tanlov ma'nosini yo'qotadi — tozalanadi.
  useEffect(() => {
    setSelectedIds(new Set());
  }, [page, params.q]);

  const toggleRow = useCallback((id: string, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback(
    (checked: boolean) => {
      const pageIds = (rows ?? []).map((r) => r.id);
      setSelectedIds((prev) => {
        const next = new Set(prev);
        for (const id of pageIds) {
          if (checked) next.add(id);
          else next.delete(id);
        }
        return next;
      });
    },
    [rows],
  );

  return (
    <>
      <StudentsTable
        {...tableState(list, query)}
        onDownloadTemplate={template.download}
        templateLoading={template.isLoading}
        templateError={template.error}
        onImportExcel={() => setImportOpen(true)}
        selectedIds={selectedIds}
        onToggleRow={toggleRow}
        onToggleAll={toggleAll}
        onAssignCompany={() => setAssignOpen(true)}
      />
      <ExcelImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        title="Talabalarni Excel orqali qo'shish"
        description="Shablonni yuklab oling, to'ldiring va shu yerga yuklang. Xato qatorlar qabul qilinmaydi — ular ro'yxat bo'lib chiqadi, to'g'rilari saqlanadi."
        hint={
          <>
            Ustunlar: <b>FISH*</b>, <b>HEMIS ID*</b>, <b>Guruh*</b>, Telefon. Sarlavha nomlarini
            o‘zgartirmang — fayl ustun nomlari bo‘yicha o‘qiladi, tartibi muhim emas. Guruhlar
            ro‘yxati shablonning «Guruhlar» varag‘ida.
          </>
        }
        emptyHint="Faylda talaba qatori topilmadi — shablonni to‘ldirganingizni tekshiring."
        template={template}
        mutation={importStudents}
      />
      {assignOpen && (
        <AssignCompanyModal
          studentIds={[...selectedIds]}
          onClose={() => setAssignOpen(false)}
          onAssigned={() => setSelectedIds(new Set())}
        />
      )}
    </>
  );
}

export default StudentsPage;
