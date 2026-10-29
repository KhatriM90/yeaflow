import { NavLink, Outlet } from 'react-router-dom';
import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import {
  Banknote,
  BarChart3,
  Boxes,
  CheckSquare,
  ChevronDown,
  Factory,
  Home,
  Search,
  ShoppingCart,
  Truck,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { Logo } from '../components/Logo';
import { useSession } from './session';

export interface ModuleNavItem {
  to: string;
  label: string;
  icon: LucideIcon;
}

/** Sidebar modules in the order of 04_Brand_and_UI_Guide, Layout. */
export const modules: ModuleNavItem[] = [
  { to: '/', label: 'Home', icon: Home },
  { to: '/sales', label: 'Sales', icon: ShoppingCart },
  { to: '/purchasing', label: 'Purchasing', icon: Truck },
  { to: '/business-partners', label: 'Business Partners', icon: Users },
  { to: '/inventory', label: 'Inventory', icon: Boxes },
  { to: '/banking', label: 'Banking', icon: Banknote },
  { to: '/production', label: 'Production', icon: Factory },
  { to: '/reports', label: 'Reports', icon: BarChart3 },
  { to: '/approvals', label: 'Approvals', icon: CheckSquare },
];

export function AppShell() {
  return (
    <div className="yf-shell">
      <aside className="yf-sidebar" aria-label="Modules">
        <div className="yf-sidebar__brand">
          <Logo onDark />
        </div>
        <nav>
          <ul>
            {modules.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink to={to} end={to === '/'} aria-label={label} title={label} className={({ isActive }) => `yf-nav${isActive ? ' is-active' : ''}`}>
                  <Icon size={18} aria-hidden="true" />
                  <span>{label}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <p className="yf-sidebar__footer">A Yeasoft product · Works with SAP Business One</p>
      </aside>
      <div className="yf-main">
        <TopBar />
        <main className="yf-content" id="main">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function TopBar() {
  const { session, switchCompany, signOut } = useSession();
  const company = session?.companies.find((c) => c.id === session.companyId);

  return (
    <header className="yf-topbar">
      <form className="yf-search" role="search" onSubmit={(e) => e.preventDefault()}>
        <Search size={16} aria-hidden="true" />
        <input type="search" aria-label="Search documents, business partners, items, batches and serials" placeholder="Search documents, partners, items, batches…" />
      </form>

      <DropdownMenu.Root>
        <DropdownMenu.Trigger className="yf-switcher" aria-label="Switch company">
          <span className="yf-switcher__label">Company</span>
          <span className="yf-switcher__value">{company?.name ?? 'Select company'}</span>
          <ChevronDown size={16} aria-hidden="true" />
        </DropdownMenu.Trigger>
        <DropdownMenu.Portal>
          <DropdownMenu.Content className="yf-menu" align="end" sideOffset={6}>
            <DropdownMenu.RadioGroup value={session?.companyId} onValueChange={switchCompany}>
              {session?.companies.map((c) => (
                <DropdownMenu.RadioItem key={c.id} value={c.id} className="yf-menu__item">
                  {c.name}
                </DropdownMenu.RadioItem>
              ))}
            </DropdownMenu.RadioGroup>
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>

      <DropdownMenu.Root>
        <DropdownMenu.Trigger className="yf-account" aria-label="Account">
          <span className="yf-avatar" aria-hidden="true">
            {session?.displayName.slice(0, 1).toUpperCase()}
          </span>
          <span className="yf-account__name">{session?.displayName}</span>
        </DropdownMenu.Trigger>
        <DropdownMenu.Portal>
          <DropdownMenu.Content className="yf-menu" align="end" sideOffset={6}>
            <DropdownMenu.Label className="yf-menu__label">{session?.roleLabel}</DropdownMenu.Label>
            <DropdownMenu.Item className="yf-menu__item" onSelect={signOut}>
              Sign out
            </DropdownMenu.Item>
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>
    </header>
  );
}
