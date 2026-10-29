import { createContext, useContext, useState, type ReactNode } from 'react';

/**
 * Placeholder session until OIDC sign-in lands (milestone 2.2). Holds only display data; every permission is
 * enforced by the API, so nothing here grants access.
 */
export interface Company {
  id: string;
  name: string;
}

export interface Session {
  displayName: string;
  roleLabel: string;
  companies: Company[];
  companyId: string;
}

interface SessionState {
  session: Session | null;
  signIn: (email: string) => void;
  signOut: () => void;
  switchCompany: (companyId: string) => void;
}

const SessionContext = createContext<SessionState | null>(null);

const demoCompanies: Company[] = [
  { id: 'demo-ke', name: 'Demo Kenya Ltd' },
  { id: 'demo-ug', name: 'Demo Uganda Ltd' },
];

export function SessionProvider({ children, initial = null }: { children: ReactNode; initial?: Session | null }) {
  const [session, setSession] = useState<Session | null>(initial);

  const value: SessionState = {
    session,
    signIn: (email) =>
      setSession({
        displayName: email.split('@')[0] ?? email,
        roleLabel: 'Sales',
        companies: demoCompanies,
        companyId: demoCompanies[0]!.id,
      }),
    signOut: () => setSession(null),
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
