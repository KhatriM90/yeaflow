import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLocation, useNavigate } from 'react-router-dom';
import { Logo } from '../components/Logo';
import { useSession } from '../app/session';

const schema = z.object({
  email: z.email('Enter your work email address.'),
});

type LoginForm = z.infer<typeof schema>;

interface Persona {
  key: string;
  displayName: string;
  description: string;
}

/** People offered by the API's local demo mode; the endpoint only exists when demo mode is on (Development only). */
function useDemoPersonas(): Persona[] {
  const [personas, setPersonas] = useState<Persona[]>([]);
  useEffect(() => {
    let cancelled = false;
    fetch('/api/demo/personas', { credentials: 'include' })
      .then((r) => (r.ok ? (r.json() as Promise<Persona[]>) : []))
      .then((list) => !cancelled && setPersonas(list))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);
  return personas;
}

/**
 * Sign-in entry. Production sign-in is OpenID Connect per tenant (Microsoft 365 / Google) with MFA for privileged roles,
 * delivered in milestone 2.2; this screen starts that redirect. Until then it offers the API's local demo people when demo
 * mode is on, and otherwise opens a display-only preview session.
 */
export function LoginPage() {
  const { signIn, signInAs } = useSession();
  const personas = useDemoPersonas();
  const [demoError, setDemoError] = useState<string | null>(null);
  const navigate = useNavigate();
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema) });

  const goBack = () => navigate((location.state as { from?: string } | null)?.from ?? '/', { replace: true });

  const onSubmit = (form: LoginForm) => {
    signIn(form.email);
    goBack();
  };

  const onDemo = async (persona: string) => {
    setDemoError(null);
    try {
      await signInAs(persona);
      goBack();
    } catch {
      setDemoError('Could not sign in to the local demo. Is the API running?');
    }
  };

  return (
    <div className="yf-login">
      <section className="yf-card yf-login__card" aria-labelledby="login-title">
        <Logo />
        <h1 id="login-title">Sign in</h1>
        <p className="yf-muted">Your SAP Business One operations — sales, purchasing, stock, banking and production — in one fast web app.</p>
        {personas.length > 0 && (
          <section className="yf-demo" aria-labelledby="demo-title">
            <h2 id="demo-title">Local demo</h2>
            <p className="yf-muted">Sign in as a demo person. Postings go to simulated SAP through the connector.</p>
            {personas.map((p) => (
              <button key={p.key} type="button" className="yf-btn-primary yf-demo__person" onClick={() => void onDemo(p.key)}>
                {p.displayName}
                <span>{p.description}</span>
              </button>
            ))}
            {demoError && (
              <p role="alert" className="yf-error">
                {demoError}
              </p>
            )}
          </section>
        )}
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <label htmlFor="email" className="yf-label">
            Work email
          </label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            className="yf-input"
            aria-invalid={errors.email ? 'true' : 'false'}
            aria-describedby={errors.email ? 'email-error' : undefined}
            {...register('email')}
          />
          {errors.email && (
            <p id="email-error" role="alert" className="yf-error">
              {errors.email.message}
            </p>
          )}
          <button type="submit" className="yf-btn-primary yf-login__submit" disabled={isSubmitting}>
            Continue with your organisation
          </button>
        </form>
        <p className="yf-login__legal">A Yeasoft product · Works with SAP Business One</p>
      </section>
    </div>
  );
}
