import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/shared/ui';
import { ExcelImportModal } from '../components/ExcelImportModal';
import { tableState } from '../shared/useListParams';
import { AssignCompanyModal } from './components/AssignCompanyModal';
import { StudentFiltersBar } from './components/StudentFiltersBar';
import { StudentsTable } from './components/StudentsTable';
import {
  useImportStudents,
  useStudentFilters,
  useStudentImportTemplate,
  useStudentsQuery,
} from './hooks';
import { useStudentListParams } from './useStudentListParams';
import styles from './StudentsPage.module.css';

/** Admin · Talabalar (SPEC-SCREENS §9.6). Container: jadval + qidiruv/filtrlar (URL'da) + import/biriktirish. */
export function StudentsPage() {
  const list = useStudentListParams();
  const query = useStudentsQuery(list.params);
  const filterOptions = useStudentFilters();
  const template = useStudentImportTemplate();
  const importStudents = useImportStudents();
  const [importOpen, setImportOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<ReadonlySet<string>>(() => new Set());

  const { page, params, filters, setFilters, hasFilters, clearFilters } = list;
  const rows = query.data?.items;

  // Sahifa, qidiruv yoki filtr o'zgarsa tanlov ma'nosini yo'qotadi — tozalanadi (yashirin qolgan
  // talabalar ko'rinmasdan korxonaga biriktirilib ketmasligi uchun).
  useEffect(() => {
    setSelectedIds(new Set());
  }, [page, params.q, params.facultyId, params.directionId, params.course]);

  /** Fakultet o'zgarsa va tanlangan yo'nalish unga tegishli bo'lmasa — yo'nalish tozalanadi. */
  function handleFacultyChange(facultyId: string) {
    const direction = filterOptions.data?.directions.find((d) => d.id === filters.directionId);
    const keepDirection = !facultyId || !direction || direction.facultyId === facultyId;
    setFilters(keepDirection ? { facultyId } : { facultyId, directionId: '' });
  }

  const clearButton = (
    <Button size="xs" onClick={clearFilters}>
      Filtrlarni tozalash
    </Button>
  );

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
        filters={
          <StudentFiltersBar
            options={filterOptions.data}
            isLoading={filterOptions.isPending}
            isError={filterOptions.isError}
            values={filters}
            onFacultyChange={handleFacultyChange}
            onDirectionChange={(directionId) => setFilters({ directionId })}
            onCourseChange={(course) => setFilters({ course })}
            hasFilters={hasFilters}
            onClear={clearFilters}
            total={query.isPending || query.isError ? undefined : query.data?.total}
          />
        }
        emptyDescription={
          hasFilters ? (
            <>
              Tanlangan filtrlar bo'yicha talaba topilmadi.
              <span className={styles.emptyAction}>{clearButton}</span>
            </>
          ) : undefined
        }
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
