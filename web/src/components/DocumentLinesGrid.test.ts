import { describe, expect, it } from 'vitest';
import { lineTotal, type DocumentLineRow } from './DocumentLinesGrid';

const row: DocumentLineRow = {
  lineNum: 1, itemCode: 'A', description: 'x', quantity: '12', unitPrice: '329.99', discountPercent: '2.5', warehouseCode: 'W', taxCode: 'T',
};

describe('lineTotal', () => {
  it('computes quantity × price less discount exactly', () => {
    expect(lineTotal(row)).toBe('3,860.88');
  });

  it('returns null while a value is being typed', () => {
    expect(lineTotal({ ...row, quantity: '12.' })).toBeNull();
  });
});
