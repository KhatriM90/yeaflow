import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
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
  it('sends signed-out users to sign in', async () => {
    renderAt('/sales');
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('validates the email before continuing', async () => {
    renderAt('/');
    await userEvent.click(await screen.findByRole('button', { name: /continue/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter your work email address.');
  });

  it('shows the shell with every scoped module after sign-in', async () => {
    renderAt('/');
    await userEvent.type(await screen.findByLabelText('Work email'), 'wanjiru@acme.co.ke');
    await userEvent.click(screen.getByRole('button', { name: /continue/i }));

    const nav = await screen.findByRole('navigation');
    for (const module of ['Home', 'Sales', 'Purchasing', 'Business Partners', 'Inventory', 'Banking', 'Production', 'Reports', 'Approvals']) {
      expect(nav).toHaveTextContent(module);
    }
    expect(screen.getByRole('heading', { name: /karibu, wanjiru/i })).toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Switch company' })).toHaveTextContent('Demo Kenya Ltd');
  });

  it('offers the local demo people when the API runs in demo mode', async () => {
    const json = (body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } }));
    let signedIn = false;
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => {
        if (url === '/api/demo/personas') return json([{ key: 'sales', displayName: 'Wanjiru Kamau', description: 'Sales user' }]);
        if (url === '/api/demo/sign-in') {
          signedIn = true;
          return Promise.resolve(new Response(null, { status: 204 }));
        }
        if (url === '/api/v1/me' && signedIn) {
          return json({ displayName: 'Wanjiru Kamau', roles: ['sales'], permissions: [], companies: [{ id: 'c1', name: 'Demo Kenya Ltd' }] });
        }
        return Promise.resolve(new Response(null, { status: 401 }));
      }),
    );

    renderAt('/');
    await userEvent.click(await screen.findByRole('button', { name: /wanjiru kamau/i }));

    expect(await screen.findByRole('heading', { name: /karibu, wanjiru kamau/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Switch company' })).toHaveTextContent('Demo Kenya Ltd');
  });
});
