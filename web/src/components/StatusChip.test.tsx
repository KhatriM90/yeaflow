import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StatusChip } from './StatusChip';

describe('StatusChip', () => {
  it.each([
    ['OutcomeUnknown', 'Outcome unknown', 'yf-chip--unknown'],
    ['AwaitingApproval', 'Awaiting approval', 'yf-chip--approval'],
    ['TimsSigned', 'TIMS signed', 'yf-chip--tims'],
  ] as const)('renders %s with brand label and tone', (status, label, tone) => {
    render(<StatusChip status={status} />);
    const chip = screen.getByText(label);
    expect(chip).toHaveClass('yf-chip', tone);
  });
});
