import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { api } from '../lib/api';

/**
 * Session until OIDC sign-in lands (milestone 2.2). Two kinds:
 * - `demo`: the API's local demo mode signed this browser in as a seeded person; data is real (from the API).
 * - `preview`: no API available; a display-only session for looking at the layout.
 * Holds only display data either way; every permission is enforced by the API, so nothing here grants access.
 */
export interface Company {
  id: string;
  name: string;
}

export interface Session {
  mode: 'demo' | 'preview';
  displayName: string;
  roleLabel: string;
  /** Display hints only (which buttons to show); the API checks every permission itself. */
  permissions: string[];
  companies: Company[];
  companyId: string;
}

interface Me {
  displayName: string | null;
  roles: string[];
  permissions: string[];
  companies: Company[];
}

interface SessionState {
  session: Session | null;
  /** True while checking whether the browser already has a demo sign-in (first load only). */
  restoring: boolean;
  signIn: (email: string) => void;
  signInAs: (persona: string) => Promise<void>;
  signOut: () => void;
  switchCompany: (companyId: string) => void;
}

const SessionContext = createContext<SessionState | null>(null);

const demoCompanies: Company[] = [
  { id: 'demo-ke', name: 'Demo Kenya Ltd' },
  { id: 'demo-ug', name: 'Demo Uganda Ltd' },
];

const roleNames: Record<string, string> = {
  sales: 'Sales',
  purchasing: 'Purchasing',
  warehouse: 'Warehouse',
  banking: 'Banking',
  production: 'Production',
  approver: 'Approver',
  report_viewer: 'Report viewer',
  app_admin: 'App administrator',
};

async function loadDemoSession(): Promise<Session> {
  const me = await api<Me>('/me');
  return {
    mode: 'demo',
    displayName: me.displayName ?? 'Demo user',
    roleLabel: me.roles.map((r) => roleNames[r] ?? r).join(', '),
    permissions: me.permissions,
    companies: me.companies,
    companyId: me.companies[0]?.id ?? '',
  };
}

export function SessionProvider({ children, initial = null }: { children: ReactNode; initial?: Session | null }) {
  const [session, setSession] = useState<Session | null>(initial);
  const [restoring, setRestoring] = useState(initial === null);

  // A demo sign-in survives a page reload through its cookie; pick it back up if the API says we are signed in.
  useEffect(() => {
    if (initial) return;
    let cancelled = false;
    loadDemoSession()
      .then((s) => !cancelled && setSession((current) => current ?? s))
      .catch(() => undefined)
      .finally(() => !cancelled && setRestoring(false));
    return () => {
      cancelled = true;
    };
  }, [initial]);

  const signInAs = useCallback(async (persona: string) => {
    await fetch('/api/demo/sign-in', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ persona }),
    }).then((r) => {
      if (!r.ok) throw new Error('Demo sign-in failed.');
    });
    setSession(await loadDemoSession());
  }, []);

  const value: SessionState = {
    session,
    restoring,
    signIn: (email) =>
      setSession({
        mode: 'preview',
        displayName: email.split('@')[0] ?? email,
        roleLabel: 'Sales',
        permissions: [],
        companies: demoCompanies,
        companyId: demoCompanies[0]!.id,
      }),
    signInAs,
    signOut: () => {
      if (session?.mode === 'demo') {
        void fetch('/api/demo/sign-out', { method: 'POST', credentials: 'include' });
      }
      setSession(null);
    },
    switchCompany: (companyId) => setSession((s) => (s ? { ...s, companyId } : s)),
  };

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionState {
  const value = useContext(SessionContext);
  if (!value) {
    throw new Error('useSession must be used inside SessionProvider');
  }
  return value;
}
