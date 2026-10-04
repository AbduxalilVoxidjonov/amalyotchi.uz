import { useMemo, useState } from 'react';
import { useAuth } from '@/shared/auth/useAuth';
import { ConfirmDialog } from '@/shared/ui';
import { errorMessage } from '@/shared/api';
import { DEFAULT_PAGE_SIZE, type Paged } from '../shared/types';
import { useListParams } from '../shared/useListParams';
import { AdminFormModal } from './components/AdminFormModal';
import { AdminsTable } from './components/AdminsTable';
import { TemporaryPasswordModal } from './components/TemporaryPasswordModal';
import { useAdminsQuery, useResetAdminPassword, useSetAdminStatus } from './hooks';
import type { AdminAccount } from './types';
import styles from './Admins.module.css';

type FormState = { mode: 'create' } | { mode: 'edit'; admin: AdminAccount } | null;
type ConfirmState = { kind: 'block' | 'reset'; admin: AdminAccount } | null;
type SecretState = {
  title: string;
  adminName: string;
  login: string;
  password: string;
} | null;

/** Ro'yxat serverdan to'liq keladi (`{ items }`) — qidiruv va sahifalash client tomonda. */
function toPaged(items: readonly AdminAccount[], q: string, page: number): Paged<AdminAccount> {
  const needle = q.toLowerCase();
  const filtered = needle
    ? items.filter((a) =>
        [a.fullName, a.login, a.phoneNumber ?? ''].some((v) => v.toLowerCase().includes(needle)),
      )
    : items;
  const start = (page - 1) * DEFAULT_PAGE_SIZE;
  return {
    items: filtered.slice(start, start + DEFAULT_PAGE_SIZE),
    page,
    pageSize: DEFAULT_PAGE_SIZE,
    total: filtered.length,
  };
}

/**
 * Admin · Adminlar (`/admin/admins`, v3.28) — faqat bosh admin (marshrut `RequireSuperAdmin` bilan
 * himoyalangan, server ham 403 beradi). Yaratish (vaqtinchalik parol bir marta ko'rsatiladi),
 * tahrirlash, bloklash/faollashtirish (bosh admin va o'zi uchun o'chiq), parolni tiklash.
 */
export function AdminsPage() {
  const { user } = useAuth();
  const list = useListParams();
  const query = useAdminsQuery();
  const setStatus = useSetAdminStatus();
  const resetPassword = useResetAdminPassword();

  const [form, setForm] = useState<FormState>(null);
  const [confirm, setConfirm] = useState<ConfirmState>(null);
  const [secret, setSecret] = useState<SecretState>(null);
  const [flash, setFlash] = useState<string | null>(null);

  const data = useMemo(
    () => (query.data ? toPaged(query.data.items, list.params.q, list.page) : undefined),
    [query.data, list.params.q, list.page],
  );

  function closeConfirm() {
    if (setStatus.isPending || resetPassword.isPending) return;
    setConfirm(null);
    setStatus.reset();
    resetPassword.reset();
  }

  function handleToggle(admin: AdminAccount) {
    setFlash(null);
    if (admin.isActive) {
      setConfirm({ kind: 'block', admin });
      return;
    }
    setStatus.mutate(
      { id: admin.id, isActive: true },
      { onSuccess: () => setFlash(`${admin.fullName} faollashtirildi.`) },
    );
  }

  function handleConfirm() {
    if (!confirm) return;
    const { admin } = confirm;
    if (confirm.kind === 'block') {
      setStatus.mutate(
        { id: admin.id, isActive: false },
        {
          onSuccess: () => {
            setConfirm(null);
            setFlash(`${admin.fullName} bloklandi.`);
          },
        },
      );
      return;
    }
    resetPassword.mutate(admin.id, {
      onSuccess: ({ temporaryPassword }) => {
        setConfirm(null);
        setSecret({
          title: 'Parol tiklandi',
          adminName: admin.fullName,
          login: admin.login,
          password: temporaryPassword,
        });
      },
    });
  }

  const confirmPending = setStatus.isPending || resetPassword.isPending;
  const confirmError =
    confirm?.kind === 'block' && setStatus.isError
      ? errorMessage(setStatus.error)
      : confirm?.kind === 'reset' && resetPassword.isError
        ? errorMessage(resetPassword.error)
        : undefined;
  // Faollashtirish xatosi (tasdiq oynasisiz) — jadval ustida.
  const inlineError = !confirm && setStatus.isError ? errorMessage(setStatus.error) : null;

  return (
    <>
      {flash && (
        <p role="status" className={styles.flash}>
          {flash}
        </p>
      )}
      {inlineError && (
        <p role="alert" className={styles.error}>
          {inlineError}
        </p>
      )}
      <AdminsTable
        data={data}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        search={list.search}
        onSearchChange={list.setSearch}
        page={list.page}
        pageSize={list.params.pageSize}
        onPageChange={list.setPage}
        currentUserId={user?.id}
        onCreate={() => {
          setFlash(null);
          setForm({ mode: 'create' });
        }}
        onEdit={(admin) => {
          setFlash(null);
          setForm({ mode: 'edit', admin });
        }}
        onToggleStatus={handleToggle}
        onResetPassword={(admin) => {
          setFlash(null);
          setConfirm({ kind: 'reset', admin });
        }}
      />

      <AdminFormModal
        open={form !== null}
        mode={form?.mode ?? 'create'}
        initial={form?.mode === 'edit' ? form.admin : null}
        onClose={() => setForm(null)}
        onCreated={(created) => {
          if (created.temporaryPassword) {
            setSecret({
              title: 'Admin yaratildi',
              adminName: created.fullName,
              login: created.login,
              password: created.temporaryPassword,
            });
          } else {
            setFlash(`${created.fullName} admin sifatida qo'shildi.`);
          }
        }}
      />

      <ConfirmDialog
        open={confirm !== null}
        title={confirm?.kind === 'block' ? 'Adminni bloklash' : 'Parolni tiklash'}
        description={
          confirm?.kind === 'block'
            ? `${confirm.admin.fullName} tizimga kira olmay qoladi. Keyin qayta faollashtirish mumkin.`
            : confirm
              ? `${confirm.admin.fullName} uchun yangi vaqtinchalik parol yaratiladi. Eski parol ishlamay qoladi.`
              : undefined
        }
        confirmLabel={confirm?.kind === 'block' ? 'Bloklash' : 'Tiklash'}
        danger={confirm?.kind === 'block'}
        isLoading={confirmPending}
        error={confirmError}
        onConfirm={handleConfirm}
        onCancel={closeConfirm}
      />

      <TemporaryPasswordModal
        shown={secret}
        title={secret?.title ?? ''}
        onClose={() => setSecret(null)}
      />
    </>
  );
}

export default AdminsPage;
