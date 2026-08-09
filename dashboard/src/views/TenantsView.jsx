import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import NukeTenantModal from '../components/NukeTenantModal';
import ActionMenu from '../components/ActionMenu';
import CopyableId from '../components/CopyableId';
import { ActiveBadge } from '../components/StatusBadge';
import { EmptyState, ErrorBanner } from '../components/States';
import { PlusIcon } from '../components/Icons';
import useAutoRefresh from '../hooks/useAutoRefresh';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { principalCaps } from '../utils/principal';
import { DEFAULT_AUTO_REFRESH, DEFAULT_PAGE_SIZE } from '../utils/constants';
import { formatDateTime, formatNumber } from '../i18n/formatters';

const BLANK = { name: '', lockHistoryRetentionDays: 7, defaultLeaseMs: 30000, maxLeaseMs: 300000 };

export default function TenantsView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { addToast } = useToast();
  const { principal } = useAuth();
  const isSystemAdmin = principalCaps(principal).isSystemAdmin;

  const [items, setItems] = useState([]);
  const [page, setPage] = useState({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE, totalCount: 0 });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(null); // { mode:'create'|'edit', data }
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [nukeTarget, setNukeTarget] = useState(null);
  const [jsonValue, setJsonValue] = useState(null);
  const [busy, setBusy] = useState(false);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(DEFAULT_AUTO_REFRESH);

  const load = useCallback(
    (pageNumber = 1, pageSize = DEFAULT_PAGE_SIZE) => {
      setLoading(true);
      setError(null);
      apiClient
        .listTenants({ pageNumber, pageSize })
        .then((r) => {
          setItems(r.items || []);
          setPage({ pageNumber: r.pageNumber, pageSize: r.pageSize, totalCount: r.totalCount });
        })
        .catch((e) => setError(e.message))
        .finally(() => setLoading(false));
    },
    [apiClient]
  );

  useEffect(() => {
    load(1, DEFAULT_PAGE_SIZE);
  }, [load]);

  useAutoRefresh(() => load(page.pageNumber, page.pageSize), autoRefreshSeconds);

  const save = async () => {
    setBusy(true);
    try {
      const d = editing.data;
      const body = {
        name: d.name,
        lockHistoryRetentionDays: Number(d.lockHistoryRetentionDays),
        defaultLeaseMs: Number(d.defaultLeaseMs),
        maxLeaseMs: Number(d.maxLeaseMs)
      };
      if (editing.mode === 'create') await apiClient.createTenant(body);
      else await apiClient.updateTenant(d.id, body);
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
      await apiClient.deleteTenant(deleteTarget.id);
      addToast({ type: 'success', message: t('common.actions.delete') });
      setDeleteTarget(null);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setDeleteTarget(null);
    }
  };

  const columns = [
    { key: 'name', label: t('views.tenants.columns.name'), render: (r) => <strong>{r.name}</strong> },
    { key: 'id', label: t('views.tenants.columns.id'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.id} /> },
    { key: 'lockHistoryRetentionDays', label: t('views.tenants.columns.retention'), render: (r) => t('views.tenants.days', { count: r.lockHistoryRetentionDays }) },
    { key: 'defaultLeaseMs', label: t('views.tenants.columns.defaultLease'), cellClass: 'right', render: (r) => formatNumber(r.defaultLeaseMs) },
    { key: 'maxLeaseMs', label: t('views.tenants.columns.maxLease'), cellClass: 'right', render: (r) => formatNumber(r.maxLeaseMs) },
    { key: 'active', label: t('views.tenants.columns.active'), render: (r) => <ActiveBadge active={r.active} activeLabel={t('common.generic.active')} inactiveLabel={t('common.generic.inactive')} /> },
    { key: 'createdUtc', label: t('views.tenants.columns.created'), render: (r) => formatDateTime(r.createdUtc, i18n.language) },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => (
        <ActionMenu
          items={[
            { label: t('common.actions.edit'), onClick: () => setEditing({ mode: 'edit', data: { ...r } }) },
            { label: t('common.actions.viewJson'), onClick: () => setJsonValue(r) },
            { label: t('common.actions.delete'), danger: true, hidden: r.isProtected, onClick: () => setDeleteTarget(r) },
            { label: t('views.tenants.nuke.action'), danger: true, hidden: !isSystemAdmin || r.isProtected, onClick: () => setNukeTarget(r) }
          ]}
        />
      )
    }
  ];

  const setField = (k, v) => setEditing((e) => ({ ...e, data: { ...e.data, [k]: v } }));

  return (
    <div>
      <PageHeader
        title={t('views.tenants.title')}
        subtitle={t('views.tenants.subtitle')}
        actions={
          <button type="button" className="button-primary" onClick={() => setEditing({ mode: 'create', data: { ...BLANK } })}>
            <PlusIcon /> {t('views.tenants.add')}
          </button>
        }
      />

      {error && <ErrorBanner message={error} onRetry={load} />}

      {items.length === 0 && !loading ? (
        <div className="table-frame">
          <EmptyState title={t('views.tenants.emptyTitle')} body={t('views.tenants.emptyBody')} />
        </div>
      ) : (
        <TableFrame
          columns={columns}
          items={items}
          loading={loading}
          rowId={(r) => r.id}
          onRowClick={(r) => setEditing({ mode: 'edit', data: { ...r } })}
          totalRecords={page.totalCount}
          pageNumber={page.pageNumber}
          pageSize={page.pageSize}
          onPageChange={(p) => load(p, page.pageSize)}
          onPageSizeChange={(s) => load(1, s)}
          onRefresh={() => load(page.pageNumber, page.pageSize)}
          autoRefreshSeconds={autoRefreshSeconds}
          onAutoRefreshChange={setAutoRefreshSeconds}
        />
      )}

      <Modal
        open={!!editing}
        onClose={() => setEditing(null)}
        title={editing?.mode === 'create' ? t('views.tenants.create') : t('views.tenants.edit')}
        footer={
          <>
            <button type="button" className="button-secondary" onClick={() => setEditing(null)} disabled={busy}>
              {t('common.actions.cancel')}
            </button>
            <button type="button" className="button-primary" onClick={save} disabled={busy || !editing?.data.name}>
              {t('common.actions.save')}
            </button>
          </>
        }
      >
        {editing && (
          <div className="modal-form">
            <div className="field">
              <label>{t('views.tenants.form.name')}</label>
              <input value={editing.data.name} onChange={(e) => setField('name', e.target.value)} />
            </div>
            <div className="field">
              <label>{t('views.tenants.form.retention')}</label>
              <input type="number" value={editing.data.lockHistoryRetentionDays} onChange={(e) => setField('lockHistoryRetentionDays', e.target.value)} />
            </div>
            <div className="field">
              <label>{t('views.tenants.form.defaultLease')}</label>
              <input type="number" value={editing.data.defaultLeaseMs} onChange={(e) => setField('defaultLeaseMs', e.target.value)} />
            </div>
            <div className="field">
              <label>{t('views.tenants.form.maxLease')}</label>
              <input type="number" value={editing.data.maxLeaseMs} onChange={(e) => setField('maxLeaseMs', e.target.value)} />
            </div>
          </div>
        )}
      </Modal>

      <ConfirmModal
        open={!!deleteTarget}
        danger
        title={t('views.tenants.confirmDeleteTitle')}
        message={t('views.tenants.confirmDeleteMessage', { name: deleteTarget?.name })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={doDelete}
        onCancel={() => setDeleteTarget(null)}
      />
      <JsonViewerModal open={!!jsonValue} value={jsonValue} onClose={() => setJsonValue(null)} />

      <NukeTenantModal
        open={!!nukeTarget}
        tenant={nukeTarget}
        apiClient={apiClient}
        onClose={() => setNukeTarget(null)}
        onComplete={load}
      />
    </div>
  );
}
