import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { SessionProvider } from './session';

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <SessionProvider>
        <App />
      </SessionProvider>
    </MemoryRouter>,
  );
}

describe('App', () => {
  it('sends signed-out users to sign in', () => {
    renderAt('/sales');
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('validates the email before continuing', async () => {
    renderAt('/');
    await userEvent.click(screen.getByRole('button', { name: /continue/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter your work email address.');
  });

  it('shows the shell with every scoped module after sign-in', async () => {
    renderAt('/');
    await userEvent.type(screen.getByLabelText('Work email'), 'wanjiru@acme.co.ke');
    await userEvent.click(screen.getByRole('button', { name: /continue/i }));

    const nav = await screen.findByRole('navigation');
    for (const module of ['Home', 'Sales', 'Purchasing', 'Business Partners', 'Inventory', 'Banking', 'Production', 'Reports', 'Approvals']) {
      expect(nav).toHaveTextContent(module);
    }
    expect(screen.getByRole('heading', { name: /karibu, wanjiru/i })).toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Switch company' })).toHaveTextContent('Demo Kenya Ltd');
  });
});
