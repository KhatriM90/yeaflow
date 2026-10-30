import { describe, expect, it } from 'vitest';
import { buildSalesOrderPayload } from './postings';

const line = { lineNum: 1, itemCode: 'A1', description: 'x', quantity: '24', unitPrice: '329.99', discountPercent: '2.5', warehouseCode: 'NBO01', taxCode: 'VAT16' };

describe('buildSalesOrderPayload', () => {
  it('writes amounts as exact number text', () => {
    const json = buildSalesOrderPayload(' C001 ', '2026-10-31', [{ ...line, unitPrice: '0.10', quantity: '3' }]);
    expect(json).toContain('"UnitPrice":0.1,');
    expect(json).toContain('"Quantity":3,');
    expect(JSON.parse(json).CardCode).toBe('C001');
  });

  it('keeps precision a float would lose', () => {
    const json = buildSalesOrderPayload('C001', '2026-10-31', [{ ...line, unitPrice: '12345678901234.123456' }]);
    expect(json).toContain('"UnitPrice":12345678901234.123456');
  });

  it('rejects bad input before anything is sent', () => {
    expect(() => buildSalesOrderPayload('', '2026-10-31', [line])).toThrow('customer code');
    expect(() => buildSalesOrderPayload('C001', '2026-10-31', [{ ...line, quantity: 'abc' }])).toThrow('Line 1: quantity');
  });
});
