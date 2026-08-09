import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import DataTable from '../components/DataTable';
import { MethodPill } from '../components/StatusBadge';
import ActionMenu from '../components/ActionMenu';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import CopyableId from '../components/CopyableId';
import { EmptyState, ErrorBanner } from '../components/States';
import { RefreshIcon } from '../components/Icons';
import useTenantScope from '../hooks/useTenantScope';
import { useToast } from '../context/ToastContext';
import { LOCK_MODES } from '../utils/constants';
import { formatDateTime, formatRelativeTime } from '../i18n/formatters';

export default function LocksView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { addToast } = useToast();
  const { tenants, tenantId, setTenantId, isSystemAdmin } = useTenantScope();

  const [name, setName] = useState('');
  const [mode, setMode] = useState('');
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [releaseTarget, setReleaseTarget] = useState(null);
  const [jsonValue, setJsonValue] = useState(null);

  const load = useCallback(() => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    apiClient
      .listLocks(tenantId, { name: name || undefined, mode: mode || undefined })
      .then((res) => setItems(Array.isArray(res) ? res : []))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [apiClient, tenantId, name, mode]);

  useEffect(() => {
    load();
  }, [load]);

  const doRelease = async () => {
    try {
      await apiClient.releaseLock(tenantId, releaseTarget.lockKey);
      addToast({ type: 'success', message: t('views.locks.released', { key: releaseTarget.lockKey }) });
      setReleaseTarget(null);
      load();
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setReleaseTarget(null);
    }
  };

  const columns = [
    { key: 'lockKey', label: t('views.locks.columns.lockKey'), render: (r) => <code className="monospace">{r.lockKey}</code> },
    { key: 'mode', label: t('views.locks.columns.mode'), render: (r) => <MethodPill method={r.mode} /> },
    { key: 'credentialId', label: t('views.locks.columns.credential'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.credentialId} /> },
    { key: 'sessionId', label: t('views.locks.columns.session'), cellClass: 'cell-id', render: (r) => <CopyableId value={r.sessionId} /> },
    { key: 'nodeId', label: t('views.locks.columns.node'), cellClass: 'cell-id', render: (r) => <code className="cell-id">{r.nodeId}</code> },
    { key: 'fencingToken', label: t('views.locks.columns.fencingToken'), cellClass: 'right', render: (r) => r.fencingToken },
    { key: 'acquiredUtc', label: t('views.locks.columns.acquiredUtc'), render: (r) => <span title={formatDateTime(r.acquiredUtc, i18n.language)}>{formatRelativeTime(r.acquiredUtc, i18n.language)}</span> },
    { key: 'leaseExpiresUtc', label: t('views.locks.columns.leaseExpiresUtc'), render: (r) => <span title={formatDateTime(r.leaseExpiresUtc, i18n.language)}>{formatRelativeTime(r.leaseExpiresUtc, i18n.language)}</span> },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => (
        <ActionMenu
          items={[
            { label: t('common.actions.viewJson'), onClick: () => setJsonValue(r) },
            { label: t('views.locks.forceRelease'), danger: true, onClick: () => setReleaseTarget(r) }
          ]}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader title={t('views.locks.title')} subtitle={t('views.locks.subtitle')} />

      <div className="panel" style={{ marginBottom: 'var(--spacing-md)' }}>
        <div className="filter-bar">
          {isSystemAdmin && (
            <div className="field">
              <label>{t('components.filters.tenant')}</label>
              <select value={tenantId} onChange={(e) => setTenantId(e.target.value)}>
                {tenants.map((tn) => (
                  <option key={tn.id} value={tn.id}>
                    {tn.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="field">
            <label>{t('components.filters.name')}</label>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder={t('components.filters.namePlaceholder')} />
          </div>
          <div className="field">
            <label>{t('components.filters.mode')}</label>
            <select value={mode} onChange={(e) => setMode(e.target.value)}>
              <option value="">{t('common.generic.all')}</option>
              {LOCK_MODES.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </div>
          <div className="filter-actions">
            <button type="button" className="button-primary button-sm" onClick={load}>
              {t('common.actions.apply')}
            </button>
            <button type="button" className="button-secondary button-sm" onClick={() => { setName(''); setMode(''); }}>
              {t('common.actions.clear')}
            </button>
            <button type="button" className="icon-button" onClick={load} title={t('common.actions.refresh')} aria-label={t('common.actions.refresh')}>
              <RefreshIcon size={16} />
            </button>
          </div>
        </div>
      </div>

      {error && <ErrorBanner message={error} onRetry={load} />}

      <div className="table-frame">
        {items.length === 0 && !loading ? (
          <EmptyState title={t('views.locks.emptyTitle')} body={t('views.locks.emptyBody')} />
        ) : (
          <DataTable columns={columns} items={items} loading={loading} rowId={(r) => r.id} />
        )}
      </div>

      <ConfirmModal
        open={!!releaseTarget}
        danger
        title={t('views.locks.confirmReleaseTitle')}
        message={t('views.locks.confirmReleaseMessage', { key: releaseTarget?.lockKey })}
        confirmLabel={t('views.locks.forceRelease')}
        onConfirm={doRelease}
        onCancel={() => setReleaseTarget(null)}
      />
      <JsonViewerModal open={!!jsonValue} value={jsonValue} onClose={() => setJsonValue(null)} />
    </div>
  );
}
