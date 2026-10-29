/**
 * Exact decimal arithmetic for amounts and quantities. The API sends money as strings (03_Tech_Stack_and_Conventions,
 * Money), and the browser never turns them into floating-point numbers. Values are held as BigInt scaled by 10^6,
 * matching numeric(19,6) on the server.
 */
const SCALE = 6;
const FACTOR = 10n ** BigInt(SCALE);
const PATTERN = /^(-)?(\d+)(?:\.(\d{1,6}))?$/;

export type Decimal = bigint & { readonly __decimal: unique symbol };

export function parseDecimal(value: string): Decimal {
  const match = PATTERN.exec(value.trim());
  if (!match) {
    throw new Error(`"${value}" is not a decimal with at most ${SCALE} places.`);
  }
  const [, sign, whole, fraction = ''] = match;
  const scaled = BigInt(whole!) * FACTOR + BigInt(fraction.padEnd(SCALE, '0'));
  return (sign ? -scaled : scaled) as Decimal;
}

export function isDecimal(value: string): boolean {
  return PATTERN.test(value.trim());
}

export function add(a: Decimal, b: Decimal): Decimal {
  return (a + b) as Decimal;
}

/** a × b, rounded half away from zero back to 6 places (SAP rounding). */
export function multiply(a: Decimal, b: Decimal): Decimal {
  return divideRounded(a * b, FACTOR);
}

/** Applies a percentage discount: amount × (100 − percent) / 100. */
export function applyDiscount(amount: Decimal, percent: Decimal): Decimal {
  const hundred = 100n * FACTOR;
  return divideRounded(amount * (hundred - percent), hundred);
}

/** Rounds to the SAP currency precision (0–6 places), half away from zero. */
export function round(value: Decimal, places: number): Decimal {
  const step = 10n ** BigInt(SCALE - places);
  return (divideRounded(value, step) * step) as Decimal;
}

export function formatDecimal(value: Decimal, places = 2, locale = 'en-KE'): string {
  const rounded = round(value, places);
  const negative = rounded < 0n;
  const abs = negative ? -rounded : rounded;
  const whole = abs / FACTOR;
  const fraction = (abs % FACTOR).toString().padStart(SCALE, '0').slice(0, places);
  const group = new Intl.NumberFormat(locale).format(whole);
  return `${negative ? '-' : ''}${group}${places > 0 ? `.${fraction}` : ''}`;
}

export function toApiString(value: Decimal): string {
  const negative = value < 0n;
  const abs = negative ? -value : value;
  const fraction = (abs % FACTOR).toString().padStart(SCALE, '0').replace(/0+$/, '');
  return `${negative ? '-' : ''}${abs / FACTOR}${fraction ? `.${fraction}` : ''}`;
}

function divideRounded(numerator: bigint, denominator: bigint): Decimal {
  const negative = numerator < 0n !== denominator < 0n;
  const n = numerator < 0n ? -numerator : numerator;
  const d = denominator < 0n ? -denominator : denominator;
  const quotient = (n + d / 2n) / d;
  return (negative ? -quotient : quotient) as Decimal;
}
