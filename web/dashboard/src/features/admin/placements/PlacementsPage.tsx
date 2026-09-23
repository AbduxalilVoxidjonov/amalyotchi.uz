import { EmptyState } from '@/shared/ui';

/**
 * Admin · Amaliyotchiga. Bo'lim mazmuni keyinroq aniqlanadi — hozircha faqat bo'sh holat.
 * Sarlavha (h1) AppShell tomonidan nav label'idan olinadi.
 */
export function PlacementsPage() {
  return (
    <EmptyState
      title="Bo'lim tayyorlanmoqda"
      description="Amaliyotchiga bo'limining mazmuni keyingi bosqichda qo'shiladi."
    />
  );
}

export default PlacementsPage;
