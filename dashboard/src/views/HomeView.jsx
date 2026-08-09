import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import ActivityChart from '../components/ActivityChart';
import LockChart from '../components/LockChart';
import DataTable from '../components/DataTable';
import { MethodPill, Badge } from '../components/StatusBadge';
import { EmptyState } from '../components/States';
import { LockIcon, ListIcon, PlayIcon, KeyIcon } from '../components/Icons';
import useTenantScope from '../hooks/useTenantScope';
import { requestRangeToParams, lockRangeToParams } from '../utils/constants';
import { formatNumber, formatDateTime, formatRelativeTime } from '../i18n/formatters';

const ATTENTION_EVENTS = new Set(['Denied', 'Expired', 'Revoked']);
const EVENT_TONE = { Denied: 'danger', Expired: 'warning', Revoked: 'danger' };

export default function HomeView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { tenants, tenantId, isSystemAdmin } = useTenantScope();

  const [reqRange, setReqRange] = useState('day');
  const [lockRange, setLockRange] = useState('day');
  const [reqSummary, setReqSummary] = useState(null);
  const [lockSummary, setLockSummary] = useState(null);
  const [holders, setHolders] = useState([]);
  const [credentials, setCredentials] = useState([]);
  const [recentEvents, setRecentEvents] = useState([]);

  // Request activity (global; not tenant-scoped).
  const loadReq = useCallback(() => {
    const p = requestRangeToParams(reqRange);
    apiClient.getRequestHistorySummary(p).then(setReqSummary).catch(() => setReqSummary(null));
  }, [apiClient, reqRange]);

  const loadLockChart = useCallback(() => {
    if (!tenantId) return;
    const p = lockRangeToParams(lockRange);
    apiClient.getLockAuditSummary(tenantId, p).then(setLockSummary).catch(() => setLockSummary(null));
  }, [apiClient, tenantId, lockRange]);

  useEffect(() => {
    loadReq();
  }, [loadReq]);
  useEffect(() => {
    loadLockChart();
  }, [loadLockChart]);

  // Tenant-scoped KPI panels (partial-failure tolerant).
  useEffect(() => {
    if (!tenantId) return;
    apiClient.listLocks(tenantId, {}).then((r) => setHolders(Array.isArray(r) ? r : [])).catch(() => setHolders([]));
    apiClient.listCredentials(tenantId).then((r) => setCredentials(Array.isArray(r) ? r : [])).catch(() => setCredentials([]));
    apiClient
      .getLockAudit(tenantId, { pageNumber: 1, pageSize: 50 })
      .then((r) => setRecentEvents((r?.items || []).filter((e) => ATTENTION_EVENTS.has(e.eventType)).slice(0, 8)))
      .catch(() => setRecentEvents([]));
  }, [apiClient, tenantId]);

  const writeHolders = holders.filter((h) => h.mode === 'Write').length;
  const tenantCount = isSystemAdmin ? tenants.length : 1;

  const kpis = [
    { label: t('views.home.kpis.activeLocks'), value: formatNumber(holders.length), icon: <LockIcon size={18} />, to: 'locks' },
    { label: t('views.home.kpis.writeHolders'), value: formatNumber(writeHolders), icon: <LockIcon size={18} />, to: 'locks' },
    { label: t('views.home.kpis.tenants'), value: formatNumber(tenantCount), icon: <ListIcon size={18} />, to: isSystemAdmin ? 'tenants' : null },
    { label: t('views.home.kpis.credentials'), value: formatNumber(credentials.length), icon: <KeyIcon size={18} />, to: 'credentials' }
  ];

  const ctas = [
    { label: t('views.home.cta.viewLocks'), icon: <LockIcon size={18} />, to: 'locks' },
    { label: t('views.home.cta.requestHistory'), icon: <ListIcon size={18} />, to: 'requests' },
    { label: t('views.home.cta.apiExplorer'), icon: <PlayIcon size={18} />, to: 'explorer' },
    { label: t('views.home.cta.addCredential'), icon: <KeyIcon size={18} />, to: 'credentials' }
  ];

  const eventColumns = [
    { key: 'createdUtc', label: t('views.home.eventColumns.time'), render: (r) => <span title={formatDateTime(r.createdUtc, i18n.language)}>{formatRelativeTime(r.createdUtc, i18n.language)}</span> },
    { key: 'lockKey', label: t('views.home.eventColumns.lockKey'), render: (r) => <code className="monospace">{r.lockKey}</code> },
    { key: 'mode', label: t('views.home.eventColumns.mode'), render: (r) => (r.mode ? <MethodPill method={r.mode} /> : '—') },
    { key: 'eventType', label: t('views.home.eventColumns.event'), render: (r) => <Badge tone={EVENT_TONE[r.eventType] || 'neutral'}>{r.eventType}</Badge> },
    { key: 'reason', label: t('views.home.eventColumns.reason'), render: (r) => r.reason || '—' }
  ];

  return (
    <div>
      <PageHeader title={t('views.home.title')} subtitle={t('views.home.subtitle')} />

      <div className="kpi-grid">
        {kpis.map((k) => (
          <div
            key={k.label}
            className={`kpi-card ${k.to ? 'clickable' : ''}`}
            onClick={k.to ? () => navigate(`/dashboard/${k.to}`) : undefined}
          >
            <div className="kpi-label">{k.label}</div>
            <div className="kpi-value">{k.value}</div>
          </div>
        ))}
      </div>

      <div className="section">
        <LockChart summary={lockSummary} rangeId={lockRange} onRangeChange={setLockRange} onRefresh={loadLockChart} />
      </div>

      <div className="section">
        <ActivityChart
          summary={reqSummary}
          rangeId={reqRange}
          onRangeChange={setReqRange}
          onRefresh={loadReq}
          onBucketClick={(b) =>
            navigate(`/dashboard/requests?fromUtc=${encodeURIComponent(b.bucketStartUtc)}&toUtc=${encodeURIComponent(b.bucketEndUtc)}`)
          }
        />
      </div>

      <div className="section">
        <div className="section-title">{t('views.home.recentEvents')}</div>
        <div className="table-frame">
          {recentEvents.length === 0 ? (
            <EmptyState title={t('views.home.recentEvents')} body={t('views.home.recentEventsEmpty')} />
          ) : (
            <DataTable columns={eventColumns} items={recentEvents} rowId={(r) => r.id} />
          )}
        </div>
      </div>

      <div className="section">
        <div className="section-title">{t('views.home.cta.title')}</div>
        <div className="kpi-grid">
          {ctas.map((c) => (
            <div key={c.label} className="kpi-card clickable" onClick={() => navigate(`/dashboard/${c.to}`)}>
              <div className="flex items-center gap-sm">
                {c.icon}
                <span style={{ fontWeight: 600 }}>{c.label}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
