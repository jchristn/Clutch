import { useState, useEffect, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import ActivityChart from '../components/ActivityChart';
import FilterBar from '../components/FilterBar';
import RequestDetailsModal from '../components/RequestDetailsModal';
import ConfirmModal from '../components/ConfirmModal';
import { StatusBadge, MethodPill } from '../components/StatusBadge';
import ActionMenu from '../components/ActionMenu';
import { EmptyState, ErrorBanner } from '../components/States';
import { TrashIcon } from '../components/Icons';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { principalCaps } from '../utils/principal';
import { HTTP_METHODS, DEFAULT_PAGE_SIZE, requestRangeToParams } from '../utils/constants';
import { formatDateTime, formatRelativeTime, formatNumber, formatDuration, truncate } from '../i18n/formatters';

const EMPTY_FILTERS = { method: '', statusCode: '', pathContains: '', fromUtc: '', toUtc: '', tenantId: '' };

// datetime-local <-> UTC ISO helpers.
function toUtcIso(local) {
  if (!local) return undefined;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString();
}
function toLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function RequestHistoryView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const { principal } = useAuth();
  const { addToast } = useToast();
  const caps = principalCaps(principal);

  const [rangeId, setRangeId] = useState('day');
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [items, setItems] = useState([]);
  const [page, setPage] = useState({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE, totalCount: 0 });
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [chartLoading, setChartLoading] = useState(false);
  const [error, setError] = useState(null);
  const [detailId, setDetailId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [bulkOpen, setBulkOpen] = useState(false);

  const buildListQuery = useCallback(
    (f, pageNumber, pageSize) => ({
      pageNumber,
      pageSize,
      method: f.method || undefined,
      statusCode: f.statusCode || undefined,
      pathContains: f.pathContains || undefined,
      fromUtc: toUtcIso(f.fromUtc),
      toUtc: toUtcIso(f.toUtc),
      tenantId: f.tenantId || undefined
    }),
    []
  );

  const load = useCallback(
    (f = filters, pageNumber = page.pageNumber, pageSize = page.pageSize) => {
      setLoading(true);
      setChartLoading(true);
      setError(null);

      const listQuery = buildListQuery(f, pageNumber, pageSize);

      const rangeParams = requestRangeToParams(rangeId);
      const summaryQuery = {
        fromUtc: toUtcIso(f.fromUtc) || rangeParams.fromUtc,
        toUtc: toUtcIso(f.toUtc) || rangeParams.toUtc,
        bucketMinutes: rangeParams.bucketMinutes,
        method: f.method || undefined,
        statusCode: f.statusCode || undefined,
        pathContains: f.pathContains || undefined,
        tenantId: f.tenantId || undefined
      };

      Promise.all([
        apiClient.getRequestHistory(listQuery),
        apiClient.getRequestHistorySummary(summaryQuery).catch(() => null)
      ])
        .then(([list, sum]) => {
          setItems(list?.items || []);
          setPage({ pageNumber: list?.pageNumber || pageNumber, pageSize: list?.pageSize || pageSize, totalCount: list?.totalCount || 0 });
          setSummary(sum);
        })
        .catch((e) => setError(e.message))
        .finally(() => {
          setLoading(false);
          setChartLoading(false);
        });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiClient, rangeId, buildListQuery]
  );

  useEffect(() => {
    load(filters, page.pageNumber, page.pageSize);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rangeId]);

  useEffect(() => {
    load(filters, 1, page.pageSize);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const applyFilters = () => load(filters, 1, page.pageSize);
  const clearFilters = () => {
    setFilters(EMPTY_FILTERS);
    load(EMPTY_FILTERS, 1, page.pageSize);
  };

  const onBucketClick = (bucket) => {
    const next = { ...filters, fromUtc: toLocalInput(bucket.bucketStartUtc), toUtc: toLocalInput(bucket.bucketEndUtc) };
    setFilters(next);
    load(next, 1, page.pageSize);
  };

  const deleteOne = async () => {
    try {
      await apiClient.deleteRequestHistoryEntry(deleteTarget.id);
      addToast({ type: 'success', message: t('views.requests.deleted') });
      setDeleteTarget(null);
      load(filters, page.pageNumber, page.pageSize);
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setDeleteTarget(null);
    }
  };

  const bulkDelete = async () => {
    try {
      const q = buildListQuery(filters, 1, page.pageSize);
      delete q.pageNumber;
      delete q.pageSize;
      const res = await apiClient.deleteRequestHistoryBulk(q);
      addToast({ type: 'success', message: t('views.requests.deleted') + ` (${res?.deletedCount ?? 0})` });
      setBulkOpen(false);
      load(filters, 1, page.pageSize);
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setBulkOpen(false);
    }
  };

  const kpis = useMemo(
    () => [
      { label: t('views.requests.kpis.total'), value: formatNumber(summary?.totalCount ?? 0) },
      { label: t('views.requests.kpis.success'), value: formatNumber(summary?.totalSuccess ?? 0) },
      { label: t('views.requests.kpis.failure'), value: formatNumber(summary?.totalFailure ?? 0) },
      { label: t('views.requests.kpis.avgDuration'), value: formatDuration(summary?.averageDurationMs ?? 0, i18n.language) }
    ],
    [summary, t, i18n.language]
  );

  const filterFields = [
    { key: 'method', label: t('components.filters.method'), type: 'select', options: [{ value: '', label: t('common.generic.any') }, ...HTTP_METHODS.map((m) => ({ value: m, label: m }))] },
    { key: 'statusCode', label: t('components.filters.status'), type: 'text', placeholder: t('components.filters.statusPlaceholder') },
    { key: 'pathContains', label: t('components.filters.path'), type: 'text', placeholder: t('components.filters.pathPlaceholder') },
    { key: 'fromUtc', label: t('components.filters.from'), type: 'datetime' },
    { key: 'toUtc', label: t('components.filters.to'), type: 'datetime' },
    ...(caps.isSystemAdmin ? [{ key: 'tenantId', label: t('components.filters.tenant'), type: 'text', placeholder: 'ten_…' }] : [])
  ];

  const columns = [
    { key: 'createdUtc', label: t('views.requests.columns.time'), render: (r) => <span title={formatDateTime(r.createdUtc, i18n.language)}>{formatRelativeTime(r.createdUtc, i18n.language)}</span> },
    { key: 'method', label: t('views.requests.columns.method'), render: (r) => <MethodPill method={r.method} /> },
    { key: 'path', label: t('views.requests.columns.path'), render: (r) => <code className="monospace" title={r.path}>{truncate(r.path, 48)}</code> },
    { key: 'statusCode', label: t('views.requests.columns.status'), render: (r) => <StatusBadge code={r.statusCode} /> },
    { key: 'durationMs', label: t('views.requests.columns.duration'), cellClass: 'right', render: (r) => formatDuration(r.durationMs, i18n.language) },
    { key: 'principalName', label: t('views.requests.columns.principal'), render: (r) => r.principalName || '—' },
    {
      key: 'actions',
      label: t('common.generic.actions'),
      headerClass: 'right',
      cellClass: 'actions-cell',
      render: (r) => (
        <ActionMenu
          items={[
            { label: t('common.actions.viewDetails'), onClick: () => setDetailId(r.id) },
            { label: t('common.actions.delete'), danger: true, onClick: () => setDeleteTarget(r) }
          ]}
        />
      )
    }
  ];

  const hasFilters = Object.entries(filters).some(([, v]) => v);
  const emptyBody = hasFilters ? t('views.requests.emptyBodyNoMatch') : t('views.requests.emptyBodyNoTraffic');

  return (
    <div>
      <PageHeader title={t('views.requests.title')} subtitle={t('views.requests.subtitle')} />

      <div className="kpi-grid">
        {kpis.map((k) => (
          <div className="kpi-card" key={k.label}>
            <div className="kpi-label">{k.label}</div>
            <div className="kpi-value">{k.value}</div>
          </div>
        ))}
      </div>

      <div className="section">
        <ActivityChart
          summary={summary}
          rangeId={rangeId}
          onRangeChange={setRangeId}
          onRefresh={() => load(filters, page.pageNumber, page.pageSize)}
          onBucketClick={onBucketClick}
          loading={chartLoading}
          title={t('views.requests.title')}
        />
      </div>

      <div className="panel" style={{ marginBottom: 'var(--spacing-md)' }}>
        <FilterBar fields={filterFields} values={filters} onChange={setFilters} onApply={applyFilters} onClear={clearFilters} />
      </div>

      {error && <ErrorBanner message={error} onRetry={() => load(filters, page.pageNumber, page.pageSize)} />}

      {items.length === 0 && !loading ? (
        <div className="table-frame">
          <EmptyState title={t('views.requests.emptyTitle')} body={emptyBody} />
        </div>
      ) : (
        <TableFrame
          columns={columns}
          items={items}
          loading={loading}
          rowId={(r) => r.id}
          onRowClick={(r) => setDetailId(r.id)}
          totalRecords={page.totalCount}
          pageNumber={page.pageNumber}
          pageSize={page.pageSize}
          onPageChange={(p) => load(filters, p, page.pageSize)}
          onPageSizeChange={(s) => load(filters, 1, s)}
          onRefresh={() => load(filters, page.pageNumber, page.pageSize)}
          rightSlot={
            hasFilters && page.totalCount > 0 ? (
              <button type="button" className="button-danger button-sm" onClick={() => setBulkOpen(true)}>
                <TrashIcon size={14} /> {t('views.requests.bulkDeleteAll')}
              </button>
            ) : null
          }
        />
      )}

      <RequestDetailsModal open={!!detailId} apiClient={apiClient} requestId={detailId} onClose={() => setDetailId(null)} />
      <ConfirmModal
        open={!!deleteTarget}
        danger
        title={t('views.requests.deleteOneTitle')}
        message={t('views.requests.deleteOneMessage', { id: deleteTarget?.id })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={deleteOne}
        onCancel={() => setDeleteTarget(null)}
      />
      <ConfirmModal
        open={bulkOpen}
        danger
        title={t('views.requests.bulkDeleteTitle')}
        message={t('views.requests.bulkDeleteMessage', { count: page.totalCount })}
        confirmLabel={t('views.requests.bulkDeleteAll')}
        onConfirm={bulkDelete}
        onCancel={() => setBulkOpen(false)}
      />
    </div>
  );
}
