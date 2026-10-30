import { api } from './api';
import { isDecimal, parseDecimal, toApiString } from './decimal';
import type { PostingStatus } from './postingStatus';
import type { DocumentLineRow } from '../components/DocumentLinesGrid';

/** A posting intent as the API returns it (scope section 8). */
export interface PostingIntent {
  id: string;
  companyId: string;
  transactionId: string;
  sapObject: string;
  state: Exclude<PostingStatus, 'TimsSigned'>;
  requiresReview: boolean;
  submitAttempts: number;
  sapDocEntry: number | null;
  sapDocNum: number | null;
  sapDraftEntry: number | null;
  errorCode: string | null;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

/** States that will change without the user doing anything, so the screen keeps checking them. */
export const isSettling = (state: PostingIntent['state']) => state === 'Queued' || state === 'Submitting' || state === 'OutcomeUnknown';

/**
 * Builds the Service Layer sales order body (SAL02). Quantities and prices are written as exact JSON number text from
 * the decimal strings, never through a JavaScript number, so nothing is rounded on the way to SAP.
 */
export function buildSalesOrderPayload(cardCode: string, docDueDate: string, lines: DocumentLineRow[]): string {
  const code = cardCode.trim();
  if (!code) throw new Error('Enter a customer code.');
  const num = (value: string, field: string, line: number) => {
    const v = value.trim() || '0';
    if (!isDecimal(v)) throw new Error(`Line ${line}: ${field} "${value}" is not a valid number.`);
    return toApiString(parseDecimal(v));
  };
  const rows = lines.map((l) =>
    [
      `"ItemCode":${JSON.stringify(l.itemCode)}`,
      `"Quantity":${num(l.quantity, 'quantity', l.lineNum)}`,
      `"UnitPrice":${num(l.unitPrice, 'unit price', l.lineNum)}`,
      `"DiscountPercent":${num(l.discountPercent, 'discount', l.lineNum)}`,
      `"WarehouseCode":${JSON.stringify(l.warehouseCode)}`,
      `"VatGroup":${JSON.stringify(l.taxCode)}`,
    ].join(','),
  );
  return `{"CardCode":${JSON.stringify(code)},"DocDueDate":${JSON.stringify(docDueDate)},"DocumentLines":[${rows.map((r) => `{${r}}`).join(',')}]}`;
}

/** Creates and immediately submits a posting. The transaction ID is reused on retry so a double click cannot post twice. */
export function submitPosting(companyId: string, transactionId: string, sapObject: string, payloadJson: string): Promise<PostingIntent> {
  const body = `{"companyId":${JSON.stringify(companyId)},"transactionId":${JSON.stringify(transactionId)},"sapObject":${JSON.stringify(sapObject)},"submitNow":true,"payload":${payloadJson}}`;
  return api<PostingIntent>('/postings', { method: 'POST', body });
}

export const getPosting = (id: string) => api<PostingIntent>(`/postings/${id}`);

/** Asks the connector to look the document up in SAP by its transaction ID (outcome unknown → posted or retry). */
export const reconcilePosting = (id: string) => api<PostingIntent>(`/postings/${id}/reconcile`, { method: 'POST' });
