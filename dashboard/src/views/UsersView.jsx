import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import DataTable from '../components/DataTable';
import TableAutoRefreshBar from '../components/TableAutoRefreshBar';
import Modal from '../components/Modal';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ActionMenu from '../components/ActionMenu';
import CopyableId from '../components/CopyableId';
import { ActiveBadge, Badge } from '../components/StatusBadge';
import { EmptyState, ErrorBanner } from '../components/States';
import { PlusIcon } from '../components/Icons';
import useTenantScope from '../hooks/useTenantScope';
import useAutoRefresh from '../hooks/useAutoRefresh';
import { useToast } from '../context/ToastContext';
import { DEFAULT_AUTO_REFRESH } from '../utils/constants';
import { formatDateTime } from '../i18n/formatters';

const BLANK = { email: '', password: '', firstName: '', lastName: '', isSystemAdmin: false, isTenantAdmin: false, active: true };

export default function UsersView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { addToast } = useToast();
  const { tenants, tenantId, setTenantId, isSystemAdmin } = useTenantScope();

  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [jsonValue, setJsonValue] = useState(null);
  const [busy, setBusy] = useState(false);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(DEFAULT_AUTO_REFRESH);

  const load = useCallback(() => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    apiClient
      .listUsers(tenantId)
      .then((r) => setItems(Array.isArray(r) ? r : []))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [apiClient, tenantId]);

  useEffect(() => {
    load();
  }, [load]);

  useAutoRefresh(load, autoRefreshSeconds);

  const save = async () => {
    setBusy(true);
    try {
      const d = editing.data;
      const body = {
        email: d.email,
        firstName: d.firstName,
        lastName: d.lastName,
        isSystemAdmin: !!d.isSystemAdmin,
        isTenantAdmin: !!d.isTenantAdmin,
        active: !!d.active
      };
      if (d.password) body.password = d.password;
      if (editing.mode === 'create') await apiClient.createUser(tenantId, body);
      else await apiClient.updateUser(tenantId, d.id, body);
      addToast({ type: 'success', message: t('common.actions.save') });
      setEditing(null);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
    } finally {
      setBusy(false);
    }
  };

  const doDelete = async () => {
    try {
      await apiClient.deleteUser(tenantId, deleteTarget.id);
      addToast({ type: 'success', message: t('common.actions.delete') });
      setDeleteTarget(null);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setDeleteTarget(null);
    }
  };

  const roleFor = (u) => (u.isSystemAdmin ? t('roles.systemAdmin') : u.isTenantAdmin ? t('roles.tenantAdmin') : t('roles.user'));

  const columns = [
    { key: 'email', label: t('views.users.columns.email'), render: (r) => <strong>{r.email}</strong> },
    { key: 'name', label: t('views.users.columns.name'), render: (r) => [r.firstName, r.lastName].filter(Boolean).join(' ') || '—' },
    { key: 'id', label: t('views.users.columns.id'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.id} /> },
    { key: 'role', label: t('views.users.columns.role'), render: (r) => <Badge tone={r.isSystemAdmin || r.isTenantAdmin ? 'info' : 'neutral'}>{roleFor(r)}</Badge> },
    { key: 'active', label: t('views.users.columns.active'), render: (r) => <ActiveBadge active={r.active} activeLabel={t('common.generic.active')} inactiveLabel={t('common.generic.inactive')} /> },
    { key: 'createdUtc', label: t('views.users.columns.created'), render: (r) => formatDateTime(r.createdUtc, i18n.language) },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => (
        <ActionMenu
          items={[
            { label: t('common.actions.edit'), onClick: () => setEditing({ mode: 'edit', data: { ...r, password: '' } }) },
            { label: t('common.actions.viewJson'), onClick: () => setJsonValue(r) },
            { label: t('common.actions.delete'), danger: true, hidden: r.isProtected, onClick: () => setDeleteTarget(r) }
          ]}
        />
      )
    }
  ];

  const setField = (k, v) => setEditing((e) => ({ ...e, data: { ...e.data, [k]: v } }));

  return (
    <div>
      <PageHeader
        title={t('views.users.title')}
        subtitle={t('views.users.subtitle')}
        actions={
          tenantId ? (
            <button type="button" className="button-primary" onClick={() => setEditing({ mode: 'create', data: { ...BLANK } })}>
              <PlusIcon /> {t('views.users.add')}
            </button>
          ) : null
        }
      />

      {isSystemAdmin && (
        <div className="panel" style={{ marginBottom: 'var(--spacing-md)' }}>
          <div className="field" style={{ maxWidth: 320 }}>
            <label>{t('components.filters.tenant')}</label>
            <select value={tenantId} onChange={(e) => setTenantId(e.target.value)}>
              <option value="">{t('views.users.selectTenant')}</option>
              {tenants.map((tn) => (
                <option key={tn.id} value={tn.id}>
                  {tn.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      )}

      {error && <ErrorBanner message={error} onRetry={load} />}

      <div className="table-frame">
        <TableAutoRefreshBar
          totalRecords={items.length}
          autoRefreshSeconds={autoRefreshSeconds}
          onAutoRefreshChange={setAutoRefreshSeconds}
          onRefresh={load}
          disabled={loading || !tenantId}
        />
        {!tenantId ? (
          <EmptyState title={t('views.users.title')} body={t('views.users.selectTenant')} />
        ) : items.length === 0 && !loading ? (
          <EmptyState title={t('views.users.emptyTitle')} body={t('views.users.emptyBody')} />
        ) : (
          <DataTable columns={columns} items={items} loading={loading} rowId={(r) => r.id} />
        )}
      </div>

      <Modal
        open={!!editing}
        onClose={() => setEditing(null)}
        title={editing?.mode === 'create' ? t('views.users.create') : t('views.users.edit')}
        footer={
          <>
            <button type="button" className="button-secondary" onClick={() => setEditing(null)} disabled={busy}>
              {t('common.actions.cancel')}
            </button>
            <button type="button" className="button-primary" onClick={save} disabled={busy || !editing?.data.email}>
              {t('common.actions.save')}
            </button>
          </>
        }
      >
        {editing && (
          <div className="modal-form">
            <div className="field">
              <label>{t('views.users.form.email')}</label>
              <input type="email" value={editing.data.email} onChange={(e) => setField('email', e.target.value)} />
            </div>
            <div className="field">
              <label>{t('views.users.form.password')}{editing.mode === 'edit' ? ` (${t('common.generic.optional')})` : ''}</label>
              <input type="password" value={editing.data.password} onChange={(e) => setField('password', e.target.value)} autoComplete="new-password" />
            </div>
            <div className="field">
              <label>{t('views.users.form.firstName')}</label>
              <input value={editing.data.firstName} onChange={(e) => setField('firstName', e.target.value)} />
            </div>
            <div className="field">
              <label>{t('views.users.form.lastName')}</label>
              <input value={editing.data.lastName} onChange={(e) => setField('lastName', e.target.value)} />
            </div>
            <div className="field field-checkbox">
              <input id="u-sysadmin" type="checkbox" checked={editing.data.isSystemAdmin} onChange={(e) => setField('isSystemAdmin', e.target.checked)} />
              <label htmlFor="u-sysadmin">{t('views.users.form.isSystemAdmin')}</label>
            </div>
            <div className="field field-checkbox">
              <input id="u-tenantadmin" type="checkbox" checked={editing.data.isTenantAdmin} onChange={(e) => setField('isTenantAdmin', e.target.checked)} />
              <label htmlFor="u-tenantadmin">{t('views.users.form.isTenantAdmin')}</label>
            </div>
            <div className="field field-checkbox">
              <input id="u-active" type="checkbox" checked={editing.data.active} onChange={(e) => setField('active', e.target.checked)} />
              <label htmlFor="u-active">{t('views.users.form.active')}</label>
            </div>
          </div>
        )}
      </Modal>

      <ConfirmModal
        open={!!deleteTarget}
        danger
        title={t('views.users.confirmDeleteTitle')}
        message={t('views.users.confirmDeleteMessage', { email: deleteTarget?.email })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={doDelete}
        onCancel={() => setDeleteTarget(null)}
      />
      <JsonViewerModal open={!!jsonValue} value={jsonValue} onClose={() => setJsonValue(null)} />
    </div>
  );
}
