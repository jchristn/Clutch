import TablePagination from './TablePagination';
import DataTable from './DataTable';

/**
 * Composition of the above-table control bar + the data table in a single
 * bordered frame. Views use this rather than wiring the pieces themselves.
 */
export default function TableFrame({
  columns,
  items,
  loading = false,
  emptyMessage,
  onRowClick,
  rowId,
  sortKey,
  sortDir,
  onSort,
  // pagination
  totalRecords,
  pageNumber,
  pageSize,
  onPageChange,
  onPageSizeChange,
  onRefresh,
  leftSlot,
  rightSlot,
  autoRefreshSeconds,
  onAutoRefreshChange
}) {
  return (
    <div className="table-frame">
      <div className="table-toolbar">
        <TablePagination
          totalRecords={totalRecords}
          pageNumber={pageNumber}
          pageSize={pageSize}
          onPageChange={onPageChange}
          onPageSizeChange={onPageSizeChange}
          onRefresh={onRefresh}
          disabled={loading}
          leftSlot={leftSlot}
          rightSlot={rightSlot}
          autoRefreshSeconds={autoRefreshSeconds}
          onAutoRefreshChange={onAutoRefreshChange}
        />
      </div>
      <DataTable
        columns={columns}
        items={items}
        loading={loading}
        emptyMessage={emptyMessage}
        onRowClick={onRowClick}
        rowId={rowId}
        sortKey={sortKey}
        sortDir={sortDir}
        onSort={onSort}
      />
    </div>
  );
}
