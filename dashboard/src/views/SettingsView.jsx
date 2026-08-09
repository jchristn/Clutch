import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import CopyButton from '../components/CopyButton';
import LanguageSelector from '../components/LanguageSelector';
import { LoadingState, EmptyState, ErrorBanner } from '../components/States';
import { useAuth } from '../context/AuthContext';
import { formatBoolean, formatNumber } from '../i18n/formatters';

function InfoRow({ label, value, copy }) {
  return (
    <div className="info-row">
      <span className="info-label">{label}</span>
      <span className="info-value">
        {value ?? '—'}
        {copy && value ? <CopyButton value={String(value)} /> : null}
      </span>
    </div>
  );
}

export default function SettingsView({ apiClient }) {
  const { t } = useTranslation();
  const { serverUrl } = useAuth();
  const [info, setInfo] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const load = () => {
    setLoading(true);
    setError(null);
    apiClient
      .getServerInfo()
      .then(setInfo)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient]);

  const f = (key) => t(`views.settings.fields.${key}`);

  return (
    <div>
      <PageHeader title={t('views.settings.title')} subtitle={t('views.settings.subtitle')} />

      {error && <ErrorBanner message={error} onRetry={load} />}

      {loading ? (
        <LoadingState />
      ) : !info ? (
        <div className="panel">
          <EmptyState title={t('views.settings.emptyTitle')} body={t('views.settings.emptyBody')} />
        </div>
      ) : (
        <div className="info-grid">
          <div className="panel">
            <div className="section-title">{t('views.settings.sections.server')}</div>
            <InfoRow label={f('product')} value={info.product} />
            <InfoRow label={f('version')} value={info.version} copy />
            <InfoRow label={f('node')} value={info.node} copy />
            <InfoRow label={f('database')} value={info.database} />
            <InfoRow label={f('webSocketConnections')} value={formatNumber(info.webSocketConnections)} />
          </div>

          <div className="panel">
            <div className="section-title">{t('views.settings.sections.connection')}</div>
            <InfoRow label={f('serverUrl')} value={serverUrl} copy />
          </div>

          {info.telemetry && (
            <div className="panel">
              <div className="section-title">{t('views.settings.sections.telemetry')}</div>
              <InfoRow label={f('telemetryEnabled')} value={formatBoolean(info.telemetry.enabled)} />
              <InfoRow label={f('prometheusPort')} value={info.telemetry.prometheusPort} />
              <InfoRow label={f('prometheusPath')} value={info.telemetry.prometheusPath} copy />
            </div>
          )}

          {info.principal && (
            <div className="panel">
              <div className="section-title">{t('views.settings.sections.principal')}</div>
              <InfoRow label={f('authenticated')} value={formatBoolean(info.principal.authenticated)} />
              <InfoRow label={f('principalName')} value={info.principal.principalName} />
              <InfoRow label={f('tenantId')} value={info.principal.tenantId} copy />
              <InfoRow label={f('isAdmin')} value={formatBoolean(info.principal.isAdmin)} />
              <InfoRow label={f('isTenantAdmin')} value={formatBoolean(info.principal.isTenantAdmin)} />
            </div>
          )}

          <div className="panel">
            <div className="section-title">{t('common.language.label')}</div>
            <div style={{ marginTop: 'var(--spacing-sm)' }}>
              <LanguageSelector />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
