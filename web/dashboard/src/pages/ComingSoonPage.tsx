import { useLocation } from 'react-router-dom';
import { EmptyState } from '@/shared/ui';

/** Shell ichidagi hali qurilmagan bo'limlar uchun placeholder (nav havolalari 404 ga tushmasin). */
export function ComingSoonPage() {
  const { pathname } = useLocation();
  return (
    <EmptyState
      title="Bu bo'lim hali qurilmagan"
      description={
        <>
          <code>{pathname}</code> ekrani keyingi bosqichda qo'shiladi.
        </>
      }
    />
  );
}

export default ComingSoonPage;
