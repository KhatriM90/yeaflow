import { useMemo } from 'react';
import { AgGridReact } from 'ag-grid-react';
import {
  AllCommunityModule,
  ModuleRegistry,
  themeQuartz,
  type ColDef,
  type ValueFormatterParams,
  type ValueGetterParams,
} from 'ag-grid-community';
import { applyDiscount, formatDecimal, isDecimal, multiply, parseDecimal } from '../lib/decimal';

ModuleRegistry.registerModules([AllCommunityModule]);

/** One document line as the API sends it: amounts and quantities are decimal strings. */
export interface DocumentLineRow {
  lineNum: number;
  itemCode: string;
  description: string;
  quantity: string;
  unitPrice: string;
  discountPercent: string;
  warehouseCode: string;
  taxCode: string;
}

const yeaflowGridTheme = themeQuartz.withParams({
  accentColor: '#0F766E',
  foregroundColor: '#0B1F2A',
  borderColor: '#DDE3E7',
  headerBackgroundColor: '#F6F8F9',
  headerTextColor: '#5A6B76',
  fontFamily: 'Inter, system-ui, sans-serif',
  fontSize: 14,
  borderRadius: 10,
  wrapperBorderRadius: 16,
  rowHeight: 44,
});

const decimalCell = { cellClass: 'yf-num', headerClass: 'ag-right-aligned-header', type: 'rightAligned' } as const;

/** Line total = quantity × price less line discount, computed exactly (never floating point). */
export function lineTotal(row: DocumentLineRow): string | null {
  if (![row.quantity, row.unitPrice, row.discountPercent || '0'].every(isDecimal)) {
    return null;
  }
  const gross = multiply(parseDecimal(row.quantity), parseDecimal(row.unitPrice));
  return formatDecimal(applyDiscount(gross, parseDecimal(row.discountPercent || '0')), 2);
}

const formatAmount = (p: ValueFormatterParams<DocumentLineRow, string>) =>
  p.value && isDecimal(p.value) ? formatDecimal(parseDecimal(p.value), 2) : (p.value ?? '');

/**
 * Keyboard-driven lines grid for document screens (04_Brand_and_UI_Guide, Document screen).
 * Item lookup, stock/price context and SAP validation plug in per module (SAL01, PUR02 …).
 */
export function DocumentLinesGrid({
  rows,
  editable = true,
  onChange,
}: {
  rows: DocumentLineRow[];
  editable?: boolean;
  onChange?: (rows: DocumentLineRow[]) => void;
}) {
  const columns = useMemo<ColDef<DocumentLineRow>[]>(
    () => [
      { field: 'lineNum', headerName: '#', width: 48, editable: false, cellClass: 'yf-num' },
      { field: 'itemCode', headerName: 'Item', width: 100, cellClass: 'yf-docno' },
      { field: 'description', headerName: 'Description', flex: 1, minWidth: 150 },
      { field: 'quantity', headerName: 'Qty', width: 76, ...decimalCell },
      { field: 'unitPrice', headerName: 'Price', width: 110, valueFormatter: formatAmount, ...decimalCell },
      { field: 'discountPercent', headerName: 'Disc %', width: 80, ...decimalCell },
      { field: 'warehouseCode', headerName: 'Whse', width: 84 },
      { field: 'taxCode', headerName: 'Tax', width: 76 },
      {
        colId: 'total',
        headerName: 'Total',
        width: 120,
        editable: false,
        valueGetter: (p: ValueGetterParams<DocumentLineRow>) => (p.data ? lineTotal(p.data) : null),
        ...decimalCell,
      },
    ],
    [],
  );

  return (
    <div className="yf-grid" style={{ height: Math.min(56 + rows.length * 44, 480) }}>
      <AgGridReact<DocumentLineRow>
        theme={yeaflowGridTheme}
        rowData={rows}
        columnDefs={columns}
        defaultColDef={{ editable, sortable: false, resizable: true }}
        singleClickEdit
        stopEditingWhenCellsLoseFocus
        enterNavigatesVertically
        enterNavigatesVerticallyAfterEdit
        getRowId={(p) => String(p.data.lineNum)}
        onCellValueChanged={() => onChange?.([...rows])}
      />
    </div>
  );
}
