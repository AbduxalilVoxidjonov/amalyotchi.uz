import { Link } from 'react-router-dom';
import { Button, EmptyState } from '@/shared/ui';

export function NotFoundPage() {
  return (
    <EmptyState
      title="404 — Sahifa topilmadi"
      description="Bunday bo'lim yo'q."
      action={
        <Button asChild size="sm">
          <Link to="/">Bosh ekranga</Link>
        </Button>
      }
    />
  );
}

export default NotFoundPage;
