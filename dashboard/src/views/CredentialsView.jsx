import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import DataTable from '../components/DataTable';
import Modal from '../components/Modal';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ActionMenu from '../components/ActionMenu';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import { ActiveBadge } from '../components/StatusBadge';
import { EmptyState, ErrorBanner } from '../components/States';
import { PlusIcon } from '../components/Icons';
import useTenantScope from '../hooks/useTenantScope';
import { useToast } from '../context/ToastContext';
import { formatDateTime } from '../i18n/formatters';

export default function CredentialsView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { addToast } = useToast();
  const { tenants, tenantId, setTenantId, isSystemAdmin } = useTenantScope();

  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [creating, setCreating] = useState(null); // { name, userId, expiresUtc }
  const [secret, setSecret] = useState(null); // created credential incl. secretKey
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [jsonValue, setJsonValue] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    apiClient
      .listCredentials(tenantId)
      .then((r) => setItems(Array.isArray(r) ? r : []))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [apiClient, tenantId]);

  useEffect(() => {
    load();
  }, [load]);

  const create = async () => {
    setBusy(true);
    try {
      const body = { name: creating.name };
      if (creating.userId) body.userId = creating.userId;
      if (creating.expiresUtc) body.expiresUtc = new Date(creating.expiresUtc).toISOString();
      const res = await apiClient.createCredential(tenantId, body);
      setCreating(null);
      setSecret(res);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
    } finally {
      setBusy(false);
    }
  };

  const doDelete = async () => {
    try {
      await apiClient.deleteCredential(tenantId, deleteTarget.id);
      addToast({ type: 'success', message: t('common.actions.delete') });
      setDeleteTarget(null);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setDeleteTarget(null);
    }
  };

  const columns = [
    { key: 'name', label: t('views.credentials.columns.name'), render: (r) => <strong>{r.name}</strong> },
    { key: 'id', label: t('views.credentials.columns.id'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.id} /> },
    { key: 'accessKey', label: t('views.credentials.columns.accessKey'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.accessKey} max={28} /> },
    { key: 'secretKeyLast4', label: t('views.credentials.columns.secretLast4'), render: (r) => <code className="monospace">····{r.secretKeyLast4}</code> },
    { key: 'expiresUtc', label: t('views.credentials.columns.expires'), render: (r) => (r.expiresUtc ? formatDateTime(r.expiresUtc, i18n.language) : '—') },
    { key: 'lastUsedUtc', label: t('views.credentials.columns.lastUsed'), render: (r) => (r.lastUsedUtc ? formatDateTime(r.lastUsedUtc, i18n.language) : '—') },
    { key: 'active', label: t('views.credentials.columns.active'), render: (r) => <ActiveBadge active={r.active} activeLabel={t('common.generic.active')} inactiveLabel={t('common.generic.inactive')} /> },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => (
        <ActionMenu
          items={[
            { label: t('common.actions.viewJson'), onClick: () => setJsonValue(r) },
            { label: t('common.actions.delete'), danger: true, hidden: r.isProtected, onClick: () => setDeleteTarget(r) }
          ]}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title={t('views.credentials.title')}
        subtitle={t('views.credentials.subtitle')}
        actions={
          tenantId ? (
            <button type="button" className="button-primary" onClick={() => setCreating({ name: '', userId: '', expiresUtc: '' })}>
              <PlusIcon /> {t('views.credentials.add')}
            </button>
          ) : null
        }
      />

      {isSystemAdmin && (
        <div className="panel" style={{ marginBottom: 'var(--spacing-md)' }}>
          <div className="field" style={{ maxWidth: 320 }}>
            <label>{t('components.filters.tenant')}</label>
            <select value={tenantId} onChange={(e) => setTenantId(e.target.value)}>
              <option value="">{t('views.credentials.selectTenant')}</option>
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
        {!tenantId ? (
          <EmptyState title={t('views.credentials.title')} body={t('views.credentials.selectTenant')} />
        ) : items.length === 0 && !loading ? (
          <EmptyState title={t('views.credentials.emptyTitle')} body={t('views.credentials.emptyBody')} />
        ) : (
          <DataTable columns={columns} items={items} loading={loading} rowId={(r) => r.id} />
        )}
      </div>

      {/* Create modal */}
      <Modal
        open={!!creating}
        onClose={() => setCreating(null)}
        title={t('views.credentials.create')}
        footer={
          <>
            <button type="button" className="button-secondary" onClick={() => setCreating(null)} disabled={busy}>
              {t('common.actions.cancel')}
            </button>
            <button type="button" className="button-primary" onClick={create} disabled={busy || !creating?.name}>
              {t('common.actions.create')}
            </button>
          </>
        }
      >
        {creating && (
          <div className="modal-form">
            <div className="field">
              <label>{t('views.credentials.form.name')}</label>
              <input value={creating.name} onChange={(e) => setCreating({ ...creating, name: e.target.value })} />
            </div>
            <div className="field">
              <label>{t('views.credentials.form.userId')}</label>
              <input value={creating.userId} onChange={(e) => setCreating({ ...creating, userId: e.target.value })} placeholder="usr_…" />
            </div>
            <div className="field">
              <label>{t('views.credentials.form.expiresUtc')}</label>
              <input type="datetime-local" value={creating.expiresUtc} onChange={(e) => setCreating({ ...creating, expiresUtc: e.target.value })} />
            </div>
          </div>
        )}
      </Modal>

      {/* Secret-shown-once modal */}
      <Modal
        open={!!secret}
        onClose={() => setSecret(null)}
        title={t('views.credentials.secretTitle')}
        footer={
          <button type="button" className="button-primary" onClick={() => setSecret(null)}>
            {t('views.credentials.secretDone')}
          </button>
        }
      >
        {secret && (
          <div className="modal-form">
            <div className="error-banner" style={{ color: 'var(--color-warning)', background: 'color-mix(in srgb, var(--color-warning) 12%, transparent)', borderColor: 'color-mix(in srgb, var(--color-warning) 40%, transparent)' }}>
              {t('views.credentials.secretIntro')}
            </div>
            <div className="field">
              <label>{t('views.credentials.accessKeyLabel')}</label>
              <div className="explorer-url">
                <span style={{ flex: 1 }}>{secret.accessKey}</span>
                <CopyButton value={secret.accessKey} />
              </div>
            </div>
            <div className="field">
              <label>{t('views.credentials.secretKeyLabel')}</label>
              <div className="explorer-url">
                <span style={{ flex: 1 }}>{secret.secretKey}</span>
                <CopyButton value={secret.secretKey} />
              </div>
            </div>
          </div>
        )}
      </Modal>

      <ConfirmModal
        open={!!deleteTarget}
        danger
        title={t('views.credentials.confirmDeleteTitle')}
        message={t('views.credentials.confirmDeleteMessage', { name: deleteTarget?.name })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={doDelete}
        onCancel={() => setDeleteTarget(null)}
      />
      <JsonViewerModal open={!!jsonValue} value={jsonValue} onClose={() => setJsonValue(null)} />
    </div>
  );
}
