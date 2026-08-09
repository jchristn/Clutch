import { useState, useRef, useLayoutEffect, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { MoreVerticalIcon } from './Icons';

/**
 * Three-dot row action menu. Portaled to document.body so it is never clipped by
 * table overflow. `items`: [{ label, onClick, danger?, hidden? }].
 */
export default function ActionMenu({ items = [] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [pos, setPos] = useState({ top: 0, left: 0 });
  const triggerRef = useRef(null);
  const menuRef = useRef(null);

  const visible = items.filter((it) => !it.hidden);

  useLayoutEffect(() => {
    if (!open || !triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    const menuH = visible.length * 34 + 8;
    const menuW = 180;
    let top = rect.bottom + 4;
    if (window.innerHeight - rect.bottom < menuH + 8) top = rect.top - menuH - 4;
    let left = rect.right - menuW;
    left = Math.max(8, Math.min(left, window.innerWidth - menuW - 8));
    setPos({ top, left });
  }, [open, visible.length]);

  useEffect(() => {
    if (!open) return undefined;
    const close = (e) => {
      if (triggerRef.current?.contains(e.target) || menuRef.current?.contains(e.target)) return;
      setOpen(false);
    };
    const closeNow = () => setOpen(false);
    document.addEventListener('mousedown', close);
    window.addEventListener('scroll', closeNow, true);
    window.addEventListener('resize', closeNow);
    return () => {
      document.removeEventListener('mousedown', close);
      window.removeEventListener('scroll', closeNow, true);
      window.removeEventListener('resize', closeNow);
    };
  }, [open]);

  if (visible.length === 0) return null;

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="action-menu-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('common.generic.actions')}
        title={t('common.generic.actions')}
        onClick={(e) => {
          e.stopPropagation();
          setOpen((v) => !v);
        }}
      >
        <MoreVerticalIcon />
      </button>
      {open &&
        createPortal(
          <div
            ref={menuRef}
            className="action-menu-dropdown"
            role="menu"
            style={{ top: pos.top, left: pos.left }}
            onClick={(e) => e.stopPropagation()}
          >
            {visible.map((it, i) => (
              <button
                key={i}
                type="button"
                role="menuitem"
                className={`action-menu-item ${it.danger ? 'danger' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  setOpen(false);
                  it.onClick?.();
                }}
              >
                {it.label}
              </button>
            ))}
          </div>,
          document.body
        )}
    </>
  );
}
