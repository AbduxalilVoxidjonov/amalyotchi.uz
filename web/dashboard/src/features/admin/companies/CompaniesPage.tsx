import { useState } from 'react';
import { errorMessage } from '@/shared/api';
import { ConfirmDialog } from '@/shared/ui';
import { ExcelImportModal } from '../components/ExcelImportModal';
import { tableState, useListParams } from '../shared/useListParams';
import { CompaniesTable } from './components/CompaniesTable';
import { CompanyFormModal } from './components/CompanyFormModal';
import {
  useCompaniesQuery,
  useCompanyImportTemplate,
  useCompanyQuery,
  useDeleteCompany,
  useImportCompanies,
  useSetCompanyStatus,
} from './hooks';
import type { Company } from './types';

type FormState = { mode: 'create' } | { mode: 'edit'; company: Company } | null;

/** Excel shablonidagi ustunlar (backend `CompanyImportColumns`) — `*` majburiy. */
const IMPORT_HINT =
  'Ustunlar: Nomi*, STIR*, Faoliyat turi*, Manzil*, Kenglik (lat)*, Uzunlik (lng)*, ' +
  'Radius (m), Rahbar FISH*, Rahbar telefoni*, Mentor FISH, Mentor telefoni.';

/**
 * Admin · Korxonalar (SPEC-SCREENS §9.7). Container: jadval + yaratish/tahrirlash/holat/o'chirish
 * va Excel import. Tahrirlashda lat/lng va rahbar/mentor kerak — ular faqat `CompanyDetail` da bor,
 * shuning uchun modal ochilgach detail so'raladi va forma kelgan qiymatlar bilan to'ladi.
 */
export function CompaniesPage() {
  const list = useListParams();
  const query = useCompaniesQuery(list.params);
  const setStatus = useSetCompanyStatus();
  const deleteCompany = useDeleteCompany();
  const importCompanies = useImportCompanies();
  const template = useCompanyImportTemplate();

  const [form, setForm] = useState<FormState>(null);
  const [deleting, setDeleting] = useState<Company | null>(null);
  const [importOpen, setImportOpen] = useState(false);

  const editingId = form?.mode === 'edit' ? form.company.id : '';
  const editing = useCompanyQuery(editingId);

  function closeDelete() {
    setDeleting(null);
    deleteCompany.reset();
  }

  return (
    <>
      <CompaniesTable
        {...tableState(list, query)}
        onCreate={() => setForm({ mode: 'create' })}
        onDownloadTemplate={template.download}
        templateLoading={template.isLoading}
        templateError={template.error}
        onImportExcel={() => setImportOpen(true)}
        onEdit={(company) => setForm({ mode: 'edit', company })}
        onToggleStatus={(company) =>
          setStatus.mutate({ id: company.id, isActive: !company.isActive })
        }
        onDelete={(company) => {
          deleteCompany.reset();
          setDeleting(company);
        }}
      />

      <CompanyFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? (editing.data ?? null) : null}
        loading={form?.mode === 'edit' && editing.isPending}
        onClose={() => setForm(null)}
      />

      <ExcelImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        title="Korxonalarni Excel'dan yuklash"
        description="Shablonni yuklab oling, to'ldiring va shu yerga qaytib yuklang."
        hint={IMPORT_HINT}
        emptyHint="Faylda ma'lumot qatori topilmadi — shablonning sarlavha qatorini o'zgartirmang."
        template={template}
        mutation={importCompanies}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Korxonani o'chirish"
        confirmLabel="O'chirish"
        description={
          deleting
            ? `«${deleting.name}» korxonasini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`
            : undefined
        }
        danger
        isLoading={deleteCompany.isPending}
        error={deleteCompany.isError ? errorMessage(deleteCompany.error) : undefined}
        onCancel={closeDelete}
        onConfirm={() => {
          if (!deleting) return;
          deleteCompany.mutate(deleting.id, { onSuccess: closeDelete });
        }}
      />
    </>
  );
}

export default CompaniesPage;
