import { describe, expect, it } from 'vitest';
import { add, applyDiscount, formatDecimal, multiply, parseDecimal, round, toApiString } from './decimal';

describe('decimal', () => {
  it('adds without floating-point error', () => {
    expect(toApiString(add(parseDecimal('0.1'), parseDecimal('0.2')))).toBe('0.3');
  });

  it('multiplies quantity by price exactly', () => {
    expect(toApiString(multiply(parseDecimal('12'), parseDecimal('329.99')))).toBe('3959.88');
  });

  it('rounds half away from zero like SAP', () => {
    expect(toApiString(round(parseDecimal('2.345'), 2))).toBe('2.35');
    expect(toApiString(round(parseDecimal('-2.345'), 2))).toBe('-2.35');
  });

  it('applies line discounts', () => {
    expect(toApiString(applyDiscount(parseDecimal('3959.88'), parseDecimal('2.5')))).toBe('3860.883');
  });

  it('formats with grouping and fixed places', () => {
    expect(formatDecimal(parseDecimal('1234567.891'), 2, 'en-KE')).toBe('1,234,567.89');
  });

  it('rejects floats with too many places and junk', () => {
    expect(() => parseDecimal('1.1234567')).toThrow();
    expect(() => parseDecimal('1e3')).toThrow();
  });
});
