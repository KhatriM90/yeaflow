import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { lazy, Suspense, type ReactNode } from 'react';
import { AppShell } from './AppShell';
import { useSession } from './session';
import { HomePage } from '../pages/HomePage';
import { LoginPage } from '../pages/LoginPage';
import { ModulePlaceholder } from '../pages/ModulePlaceholder';

// The lines grid (AG Grid) is heavy; load it only on document screens.
const SalesOrderPreview = lazy(() => import('../pages/SalesOrderPreview').then((m) => ({ default: m.SalesOrderPreview })));

function RequireSession({ children }: { children: ReactNode }) {
  const { session } = useSession();
  const location = useLocation();
  return session ? children : <Navigate to="/login" replace state={{ from: location.pathname }} />;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireSession>
            <AppShell />
          </RequireSession>
        }
      >
        <Route index element={<HomePage />} />
        <Route
          path="sales"
          element={
            <Suspense fallback={<p className="yf-muted">Loading…</p>}>
              <SalesOrderPreview />
            </Suspense>
          }
        />
        <Route path="purchasing" element={<ModulePlaceholder title="Purchasing" ids="PUR01–PUR05" />} />
        <Route path="business-partners" element={<ModulePlaceholder title="Business Partners" ids="BP01–BP03" />} />
        <Route path="inventory" element={<ModulePlaceholder title="Inventory" ids="INV01–INV06" />} />
        <Route path="banking" element={<ModulePlaceholder title="Banking" ids="BNK01–BNK04" />} />
        <Route path="production" element={<ModulePlaceholder title="Production" ids="PRO01–PRO04" />} />
        <Route path="reports" element={<ModulePlaceholder title="Reports" ids="module reports, scope sections 5–7" />} />
        <Route path="approvals" element={<ModulePlaceholder title="Approvals" ids="approvals inbox, scope section 4" />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
