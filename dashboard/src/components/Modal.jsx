import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { XIcon } from './Icons';

/**
 * Base modal. Renders via portal. Closes on ESC and backdrop click, locks body
 * scroll while open. `size` is 'small' | 'medium' | 'large' | 'xlarge'.
 */
export default function Modal({ open, onClose, title, headerMeta, children, footer, size = 'medium' }) {
  const { t } = useTranslation();

  useEffect(() => {
    if (!open) return undefined;
    const onKey = (e) => {
      if (e.key === 'Escape') onClose?.();
    };
    document.addEventListener('keydown', onKey);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prevOverflow;
    };
  }, [open, onClose]);

  if (!open) return null;

  const sizeClass = size === 'medium' ? '' : `modal-${size}`;

  return createPortal(
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className={`modal ${sizeClass}`}
        role="dialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <div>
            <div className="modal-title">{title}</div>
            {headerMeta && <div className="modal-header-meta">{headerMeta}</div>}
          </div>
          <button
            type="button"
            className="icon-button"
            onClick={onClose}
            aria-label={t('common.actions.close')}
            title={t('common.actions.close')}
          >
            <XIcon />
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>,
    document.body
  );
}
