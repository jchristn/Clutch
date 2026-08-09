import { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import CopyableId from './CopyableId';
import { TrashIcon, CheckIcon } from './Icons';
import { formatNumber } from '../i18n/formatters';

const MIN_REASON = 10;

/**
 * System-admin-only destructive tenant purge. Requires typing the exact tenant ID plus an
 * administrative reason before the danger action is enabled. Mirrors the Xeno nuke workflow:
 * confirm-by-ID, mandatory reason, scope toggles, and an inline result panel with deletion counts.
 */
export default function NukeTenantModal({ open, tenant, apiClient, onClose, onComplete }) {
  const { t } = useTranslation();
  const [confirmId, setConfirmId] = useState('');
  const [reason, setReason] = useState('');
  const [includeAuditRecords, setIncludeAuditRecords] = useState(true);
  const [includeRequestHistory, setIncludeRequestHistory] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);
  const resultRef = useRef(null);

  // Reset all state whenever the modal is (re)opened for a tenant.
  useEffect(() => {
    if (open) {
      setConfirmId('');
      setReason('');
      setIncludeAuditRecords(true);
      setIncludeRequestHistory(true);
      setBusy(false);
      setError('');
      setResult(null);
    }
  }, [open, tenant]);

  useEffect(() => {
    if (result) resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [result]);

  const targetId = tenant?.id || '';
  const canExecute = confirmId === targetId && reason.trim().length >= MIN_REASON && !busy;

  const execute = async () => {
    setBusy(true);
    setError('');
    try {
      const res = await apiClient.nukeTenant({
        tenantId: targetId,
        confirmTenantId: confirmId,
        reason: reason.trim(),
        includeAuditRecords,
        includeRequestHistory
      });
      setResult(res);
      onComplete?.();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const deletedEntries = result?.deleted
    ? Object.entries(result.deleted).filter(([, n]) => Number(n || 0) > 0)
    : [];

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="large"
      title={t('views.tenants.nuke.title')}
      footer={
        <>
          <button type="button" className="button-secondary" onClick={onClose} disabled={busy}>
            {result ? t('common.actions.close') : t('common.actions.cancel')}
          </button>
          {!result && (
            <button type="button" className="button-danger" onClick={execute} disabled={!canExecute}>
              <TrashIcon size={14} /> {busy ? t('common.generic.saving') : t('views.tenants.nuke.execute')}
            </button>
          )}
        </>
      }
    >
      <div className="modal-form">
        <div className="nuke-warning">{t('views.tenants.nuke.warning')}</div>

        <dl className="nuke-summary">
          <dt>{t('views.tenants.columns.name')}</dt>
          <dd>{tenant?.name}</dd>
          <dt>{t('views.tenants.columns.id')}</dt>
          <dd><CopyableId value={targetId} max={48} /></dd>
        </dl>

        <div className="field">
          <label>{t('views.tenants.nuke.confirmId')}</label>
          <input
            autoFocus
            value={confirmId}
            onChange={(e) => setConfirmId(e.target.value)}
            placeholder={targetId}
            disabled={!!result}
          />
        </div>

        <div className="field">
          <label>{t('views.tenants.nuke.reason')}</label>
          <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} disabled={!!result} />
        </div>

        {!result && (
          <>
            <div className="field field-checkbox">
              <input
                id="nuke-audit"
                type="checkbox"
                checked={includeAuditRecords}
                onChange={(e) => setIncludeAuditRecords(e.target.checked)}
              />
              <label htmlFor="nuke-audit">{t('views.tenants.nuke.includeAudit')}</label>
            </div>
            <div className="field field-checkbox">
              <input
                id="nuke-requests"
                type="checkbox"
                checked={includeRequestHistory}
                onChange={(e) => setIncludeRequestHistory(e.target.checked)}
              />
              <label htmlFor="nuke-requests">{t('views.tenants.nuke.includeRequests')}</label>
            </div>
            <p className="nuke-hint">{t('views.tenants.nuke.hint')}</p>
          </>
        )}

        {error && <div className="nuke-warning" role="alert">{error}</div>}

        {result && (
          <div className="nuke-result-panel" ref={resultRef}>
            <div className="nuke-result-title">
              <CheckIcon size={18} /> {t('views.tenants.nuke.resultTitle', { name: result.tenantName })}
            </div>
            <table className="nuke-count-table">
              <thead>
                <tr>
                  <th>{t('views.tenants.nuke.entity')}</th>
                  <th>{t('views.tenants.nuke.deleted')}</th>
                </tr>
              </thead>
              <tbody>
                {deletedEntries.map(([key, count]) => (
                  <tr key={key}>
                    <td>{t(`views.tenants.nuke.entities.${key}`, { defaultValue: key })}</td>
                    <td>{formatNumber(count)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Modal>
  );
}
