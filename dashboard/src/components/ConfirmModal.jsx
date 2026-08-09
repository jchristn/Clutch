import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';

/** Confirmation dialog. Set `danger` for destructive actions. */
export default function ConfirmModal({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel,
  danger = false,
  onConfirm,
  onCancel
}) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);

  const handleConfirm = async () => {
    try {
      setBusy(true);
      await onConfirm?.();
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onCancel}
      size="small"
      title={title || t('components.confirm.title')}
      footer={
        <>
          <button type="button" className="button-secondary" onClick={onCancel} disabled={busy}>
            {cancelLabel || t('common.actions.cancel')}
          </button>
          <button
            type="button"
            className={danger ? 'button-danger' : 'button-primary'}
            onClick={handleConfirm}
            disabled={busy}
          >
            {confirmLabel || t('common.actions.confirm')}
          </button>
        </>
      }
    >
      <p style={{ fontSize: 'var(--font-size-sm)' }}>{message || t('components.confirm.message')}</p>
    </Modal>
  );
}
