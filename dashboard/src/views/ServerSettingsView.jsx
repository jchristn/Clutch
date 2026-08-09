import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import ConfirmModal from '../components/ConfirmModal';
import { LoadingState, EmptyState, ErrorBanner } from '../components/States';
import { RefreshIcon } from '../components/Icons';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { principalCaps } from '../utils/principal';

// Fallback option sets for select fields. The current value is always merged in
// so a server-supplied value that is not in this list still renders correctly.
const DB_TYPES = ['Sqlite', 'SqlServer', 'Mysql', 'Postgresql'];
const SEVERITIES = ['Debug', 'Info', 'Warn', 'Error', 'Critical', 'Alert', 'Emergency'];

// Declarative section/field layout. `group` matches the settings sub-object key
// (except `node`, whose fields live at the settings root). `secret` fields render
// their redacted "***" value and are preserved by the backend if left unchanged.
const SECTIONS = [
  {
    group: 'node',
    root: true,
    fields: [
      { name: 'nodeId', type: 'text', readOnly: true },
      { name: 'createdUtc', type: 'text', readOnly: true }
    ]
  },
  {
    group: 'rest',
    fields: [
      { name: 'hostname', type: 'text' },
      { name: 'port', type: 'number' },
      { name: 'ssl', type: 'checkbox' }
    ]
  },
  {
    group: 'database',
    fields: [
      { name: 'type', type: 'select', options: DB_TYPES },
      { name: 'host', type: 'text' },
      { name: 'port', type: 'number' },
      { name: 'databaseName', type: 'text' },
      { name: 'username', type: 'text' },
      { name: 'password', type: 'text', secret: true },
      { name: 'maxPoolSize', type: 'number' }
    ]
  },
  {
    group: 'logging',
    fields: [
      { name: 'consoleLogging', type: 'checkbox' },
      { name: 'fileLogging', type: 'checkbox' },
      { name: 'logDirectory', type: 'text' },
      { name: 'logFilename', type: 'text' },
      { name: 'minimumSeverity', type: 'select', options: SEVERITIES }
    ]
  },
  {
    group: 'auth',
    fields: [
      { name: 'issuer', type: 'text' },
      { name: 'signingKey', type: 'text', secret: true },
      { name: 'tokenLifetimeMinutes', type: 'number' },
      { name: 'adminApiKey', type: 'text', secret: true }
    ]
  },
  {
    group: 'lock',
    fields: [
      { name: 'defaultLeaseMs', type: 'number' },
      { name: 'maxLeaseMs', type: 'number' },
      { name: 'maxHoldMs', type: 'number' },
      { name: 'maxWaitMs', type: 'number' },
      { name: 'waiterPollMs', type: 'number' },
      { name: 'sweepIntervalMs', type: 'number' }
    ]
  },
  {
    group: 'requestHistory',
    fields: [
      { name: 'enabled', type: 'checkbox' },
      { name: 'maxRequestBodyBytes', type: 'number' },
      { name: 'maxResponseBodyBytes', type: 'number' },
      { name: 'retentionDays', type: 'number' }
    ]
  },
  {
    group: 'mcp',
    fields: [
      { name: 'enable', type: 'checkbox' },
      { name: 'hostname', type: 'text' },
      { name: 'port', type: 'number' },
      { name: 'mcpPath', type: 'text' },
      { name: 'serverName', type: 'text' }
    ]
  },
  {
    group: 'telemetry',
    fields: [
      { name: 'enabled', type: 'checkbox' },
      { name: 'serviceName', type: 'text' },
      { name: 'prometheusEnable', type: 'checkbox' },
      { name: 'prometheusHostname', type: 'text' },
      { name: 'prometheusPort', type: 'number' },
      { name: 'prometheusPath', type: 'text' },
      { name: 'otlpEnable', type: 'checkbox' },
      { name: 'otlpEndpoint', type: 'text' },
      { name: 'metricsIncludeRuntime', type: 'checkbox' },
      { name: 'metricsIncludeProcess', type: 'checkbox' }
    ]
  }
];

function selectOptions(base, current) {
  const list = [...base];
  if (current != null && current !== '' && !list.includes(current)) list.unshift(current);
  return list;
}

export default function ServerSettingsView({ apiClient }) {
  const { t } = useTranslation();
  const { principal } = useAuth();
  const { addToast } = useToast();
  const caps = principalCaps(principal);
  const canEdit = caps.isSystemAdmin;

  const [form, setForm] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [restartOpen, setRestartOpen] = useState(false);
  const [restarting, setRestarting] = useState(false);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    apiClient
      .getSettings()
      .then((s) => setForm(s || null))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  const getValue = (group, name, root) => {
    if (!form) return '';
    return (root ? form[name] : form[group]?.[name]) ?? '';
  };

  const setValue = (group, name, value, root) => {
    setForm((prev) => {
      if (!prev) return prev;
      if (root) return { ...prev, [name]: value };
      return { ...prev, [group]: { ...(prev[group] || {}), [name]: value } };
    });
  };

  const save = async () => {
    setBusy(true);
    try {
      const res = await apiClient.updateSettings(form);
      if (res?.settings) setForm(res.settings);
      addToast({
        type: 'success',
        message: res?.restartRequired ? t('views.serverSettings.restartRequiredToast') : t('views.serverSettings.saved')
      });
    } catch (e) {
      addToast({ type: 'error', message: e.message });
    } finally {
      setBusy(false);
    }
  };

  const doRestart = async () => {
    try {
      await apiClient.restartServer();
      setRestartOpen(false);
      setRestarting(true);
      addToast({ type: 'success', message: t('views.serverSettings.restartToast') });
    } catch (e) {
      addToast({ type: 'error', message: e.message });
      setRestartOpen(false);
    }
  };

  const label = (group, name) => t(`views.serverSettings.fields.${group}.${name}`);

  const renderField = (group, field, root) => {
    const { name, type, secret, readOnly, options } = field;
    const disabled = !canEdit || readOnly || restarting;
    const id = `set-${group}-${name}`;
    const value = getValue(group, name, root);

    if (type === 'checkbox') {
      return (
        <div className="field field-checkbox" key={name}>
          <input
            id={id}
            type="checkbox"
            checked={!!value}
            disabled={disabled}
            onChange={(e) => setValue(group, name, e.target.checked, root)}
          />
          <label htmlFor={id}>{label(group, name)}</label>
        </div>
      );
    }

    return (
      <div className="field" key={name}>
        <label htmlFor={id}>{label(group, name)}</label>
        {type === 'select' ? (
          <select id={id} value={value} disabled={disabled} onChange={(e) => setValue(group, name, e.target.value, root)}>
            {selectOptions(options, value).map((opt) => (
              <option key={opt} value={opt}>
                {opt}
              </option>
            ))}
          </select>
        ) : (
          <input
            id={id}
            type={type === 'number' ? 'number' : 'text'}
            value={value}
            disabled={disabled}
            onChange={(e) =>
              setValue(group, name, type === 'number' ? (e.target.value === '' ? '' : Number(e.target.value)) : e.target.value, root)
            }
          />
        )}
        {secret && canEdit && <span className="field-hint">{t('views.serverSettings.secretHint')}</span>}
      </div>
    );
  };

  return (
    <div>
      <PageHeader
        title={t('views.serverSettings.title')}
        subtitle={t('views.serverSettings.subtitle')}
        actions={
          <button type="button" className="icon-button" onClick={load} title={t('common.actions.refresh')} aria-label={t('common.actions.refresh')}>
            <RefreshIcon size={16} />
          </button>
        }
      />

      {error && <ErrorBanner message={error} onRetry={load} />}

      {loading ? (
        <LoadingState />
      ) : !form ? (
        <div className="panel">
          <EmptyState title={t('views.serverSettings.emptyTitle')} body={t('views.serverSettings.emptyBody')} />
        </div>
      ) : restarting ? (
        <div className="panel">
          <EmptyState title={t('views.serverSettings.restart')} body={t('views.serverSettings.restarting')} />
        </div>
      ) : (
        <>
          {!canEdit && <div className="settings-note settings-note-neutral">{t('views.serverSettings.readOnlyNotice')}</div>}

          {canEdit && (
            <div className="settings-toolbar">
              <button type="button" className="button-primary" onClick={save} disabled={busy}>
                {busy ? t('common.generic.saving') : t('views.serverSettings.save')}
              </button>
              <button type="button" className="button-danger" onClick={() => setRestartOpen(true)} disabled={busy}>
                {t('views.serverSettings.restart')}
              </button>
            </div>
          )}

          <div className="settings-note">{t('views.serverSettings.restartBanner')}</div>

          <div className="settings-grid">
            {SECTIONS.map((section) => (
              <div className="panel" key={section.group}>
                <div className="section-title">{t(`views.serverSettings.sections.${section.group}`)}</div>
                <div className="modal-form">
                  {section.fields.map((field) => renderField(section.group, field, section.root))}
                </div>
              </div>
            ))}
          </div>
        </>
      )}

      <ConfirmModal
        open={restartOpen}
        danger
        title={t('views.serverSettings.restartConfirmTitle')}
        message={t('views.serverSettings.restartConfirmMessage')}
        confirmLabel={t('views.serverSettings.restart')}
        onConfirm={doRestart}
        onCancel={() => setRestartOpen(false)}
      />
    </div>
  );
}
