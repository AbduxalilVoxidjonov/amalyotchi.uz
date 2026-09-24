import { AppLink, Button, EmptyState } from '@/shared/ui';

export function NotFoundPage() {
  return (
    <EmptyState
      title="404 — Sahifa topilmadi"
      description="Bunday bo'lim yo'q."
      action={
        <Button asChild size="sm">
          <AppLink to="/">Bosh ekranga</AppLink>
        </Button>
      }
    />
  );
}

export default NotFoundPage;
