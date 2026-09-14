import { ErrorState, LoadingState } from '@/features/admin/components/PageStatus';
import { ReportsView } from './components/ReportsView';
import { useReportsCatalogQuery } from './hooks';

/**
 * Hisobotlar (SPEC-SCREENS §12) — admin (`/admin/reports`) va tyutor (`/tutor/reports`) uchun bitta ekran.
 * ❓ Rolga bog'liq matn dizaynda yo'q; ko'lam serverdan (Bearer bo'yicha) keladi.
 * Holat komponentlari admin feature'idan qayta ishlatiladi (shared/ui ga yangi komponent qo'shilmadi).
 */
export function ReportsPage() {
  const query = useReportsCatalogQuery();

  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  return (
    <ReportsView
      data={query.data}
      // TODO: GET /api/reports/{id}/download — backend'da yo'q, hozircha no-op.
      onDownload={() => undefined}
    />
  );
}

export default ReportsPage;
