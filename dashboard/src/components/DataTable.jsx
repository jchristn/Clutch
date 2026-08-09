import { useTranslation } from 'react-i18next';
import { LoadingState } from './States';

/**
 * Presentation-only table. `columns`: [{ key, label, cellClass?, render?(item), sortable? }].
 * Sorting/pagination are owned by the parent; this component only renders.
 */
export default function DataTable({
  columns = [],
  items = [],
  loading = false,
  emptyMessage,
  onRowClick = null,
  rowId = (item) => item.id,
  sortKey = null,
  sortDir = 'asc',
  onSort = null
}) {
  const { t } = useTranslation();

  if (loading) {
    return (
      <div className="table-scroll">
        <LoadingState label={t('components.table.loading')} />
      </div>
    );
  }

  return (
    <div className="table-scroll">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((col) => {
              const sortable = col.sortable && onSort;
              return (
                <th
                  key={col.key}
                  scope="col"
                  className={`${sortable ? 'sortable' : ''} ${col.headerClass || ''}`}
                  aria-sort={
                    sortKey === col.key ? (sortDir === 'asc' ? 'ascending' : 'descending') : undefined
                  }
                  onClick={sortable ? () => onSort(col.key) : undefined}
                >
                  {col.label}
                  {sortable && sortKey === col.key && (sortDir === 'asc' ? ' ▲' : ' ▼')}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr>
              <td className="table-empty" colSpan={columns.length}>
                {emptyMessage || t('components.table.empty')}
              </td>
            </tr>
          ) : (
            items.map((item, index) => (
              <tr
                key={rowId(item) || index}
                className={onRowClick ? 'clickable' : ''}
                onClick={onRowClick ? () => onRowClick(item) : undefined}
              >
                {columns.map((col) => (
                  <td key={col.key} className={col.cellClass || ''}>
                    {col.render ? col.render(item) : item[col.key]}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
