import CopyButton from './CopyButton';
import { truncate } from '../i18n/formatters';

/** A monospace, non-wrapping ID with an inline copy button. */
export default function CopyableId({ value, max = 20 }) {
  if (!value) return <span className="text-muted">—</span>;
  return (
    <span className="copyable-id">
      <code className="cell-id" title={value}>
        {truncate(value, max)}
      </code>
      <CopyButton value={value} />
    </span>
  );
}
