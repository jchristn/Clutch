import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import LockChart from '../components/LockChart';
import { MethodPill, Badge } from '../components/StatusBadge';
import CopyableId from '../components/CopyableId';
import { EmptyState, ErrorBanner } from '../components/States';
import ActionMenu from '../components/ActionMenu';
import JsonViewerModal from '../components/JsonViewerModal';
import useTenantScope from '../hooks/useTenantScope';
import useAutoRefresh from '../hooks/useAutoRefresh';
import { LOCK_MODES, DEFAULT_PAGE_SIZE, DEFAULT_AUTO_REFRESH, lockRangeToParams } from '../utils/constants';
import { formatDateTime, formatRelativeTime } from '../i18n/formatters';

const EVENT_TONE = {
  Denied: 'danger',
  Expired: 'warning',
  Revoked: 'danger',
  Acquired: 'success',
  Released: 'neutral',
  Waited: 'info',
  PolicyCreated: 'info',
  HeartbeatRenewed: 'neutral'
};

export default function LockActivityView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { tenants, tenantId, setTenantId, isSystemAdmin } = useTenantScope();

  const [rangeId, setRangeId] = useState('day');
  const [name, setName] = useState('');
  const [mode, setMode] = useState('');
  const [summary, setSummary] = useState(null);
  const [items, setItems] = useState([]);
  const [page, setPage] = useState({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE, totalCount: 0 });
  const [loading, setLoading] = useState(false);
  const [chartLoading, setChartLoading] = useState(false);
  const [error, setError] = useState(null);
  const [jsonValue, setJsonValue] = useState(null);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(DEFAULT_AUTO_REFRESH);

  const loadChart = useCallback(() => {
    if (!tenantId) return;
    setChartLoading(true);
    const { fromUtc, toUtc, bucketCount } = lockRangeToParams(rangeId);
    apiClient
      .getLockAuditSummary(tenantId, { name: name || undefined, mode: mode || undefined, fromUtc, toUtc, bucketCount })
      .then(setSummary)
      .catch(() => setSummary(null))
      .finally(() => setChartLoading(false));
  }, [apiClient, tenantId, rangeId, name, mode]);

  const loadTable = useCallback(
    (pageNumber = page.pageNumber, pageSize = page.pageSize) => {
      if (!tenantId) return;
      setLoading(true);
      setError(null);
      apiClient
        .getLockAudit(tenantId, { name: name || undefined, mode: mode || undefined, pageNumber, pageSize })
        .then((res) => {
          setItems(res?.items || []);
          setPage({ pageNumber: res?.pageNumber || pageNumber, pageSize: res?.pageSize || pageSize, totalCount: res?.totalCount || 0 });
        })
        .catch((e) => setError(e.message))
        .finally(() => setLoading(false));
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiClient, tenantId, name, mode]
  );

  useEffect(() => {
    loadChart();
  }, [loadChart]);
  useEffect(() => {
    loadTable(1, page.pageSize);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId]);

  useAutoRefresh(() => loadTable(page.pageNumber, page.pageSize), autoRefreshSeconds);

  const applyFilters = () => {
    loadChart();
    loadTable(1, page.pageSize);
  };

  const columns = [
    { key: 'createdUtc', label: t('views.lockActivity.auditColumns.time'), render: (r) => <span title={formatDateTime(r.createdUtc, i18n.language)}>{formatRelativeTime(r.createdUtc, i18n.language)}</span> },
    { key: 'lockKey', label: t('views.lockActivity.auditColumns.lockKey'), render: (r) => <code className="monospace">{r.lockKey}</code> },
    { key: 'mode', label: t('views.lockActivity.auditColumns.mode'), render: (r) => (r.mode ? <MethodPill method={r.mode} /> : '—') },
    { key: 'eventType', label: t('views.lockActivity.auditColumns.event'), render: (r) => <Badge tone={EVENT_TONE[r.eventType] || 'neutral'}>{r.eventType}</Badge> },
    { key: 'credentialId', label: t('views.lockActivity.auditColumns.credential'), cellClass: 'cell-id', render: (r) => (r.credentialId ? <CopyableId value={r.credentialId} /> : '—') },
    { key: 'fencingToken', label: t('views.lockActivity.auditColumns.fencingToken'), cellClass: 'right', render: (r) => r.fencingToken ?? '—' },
    { key: 'reason', label: t('views.lockActivity.auditColumns.reason'), render: (r) => r.reason || '—' },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => <ActionMenu items={[{ label: t('common.actions.viewJson'), onClick: () => setJsonValue(r) }]} />
    }
  ];

  return (
    <div>
      <PageHeader title={t('views.lockActivity.title')} subtitle={t('views.lockActivity.subtitle')} />

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
            <button type="button" className="button-primary button-sm" onClick={applyFilters}>
              {t('common.actions.apply')}
            </button>
            <button
              type="button"
              className="button-secondary button-sm"
              onClick={() => {
                setName('');
                setMode('');
                setTimeout(applyFilters, 0);
              }}
            >
              {t('common.actions.clear')}
            </button>
          </div>
        </div>
      </div>

      <div className="section">
        <LockChart summary={summary} rangeId={rangeId} onRangeChange={setRangeId} onRefresh={loadChart} loading={chartLoading} />
      </div>

      {error && <ErrorBanner message={error} onRetry={() => loadTable(page.pageNumber, page.pageSize)} />}

      {items.length === 0 && !loading ? (
        <div className="table-frame">
          <EmptyState title={t('views.lockActivity.emptyTitle')} body={t('views.lockActivity.emptyBody')} />
        </div>
      ) : (
        <TableFrame
          columns={columns}
          items={items}
          loading={loading}
          rowId={(r) => r.id}
          totalRecords={page.totalCount}
          pageNumber={page.pageNumber}
          pageSize={page.pageSize}
          onPageChange={(p) => loadTable(p, page.pageSize)}
          onPageSizeChange={(s) => loadTable(1, s)}
          onRefresh={() => loadTable(page.pageNumber, page.pageSize)}
          autoRefreshSeconds={autoRefreshSeconds}
          onAutoRefreshChange={setAutoRefreshSeconds}
        />
      )}

      <JsonViewerModal open={!!jsonValue} value={jsonValue} onClose={() => setJsonValue(null)} />
    </div>
  );
}
