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

/**
 * Sign-in entry. Production sign-in is OpenID Connect per tenant (Microsoft 365 / Google) with MFA for privileged roles,
 * delivered in milestone 2.2; this screen starts that redirect. Until then it opens a local preview session.
 */
export function LoginPage() {
  const { signIn } = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema) });

  const onSubmit = (form: LoginForm) => {
    signIn(form.email);
    const from = (location.state as { from?: string } | null)?.from ?? '/';
    navigate(from, { replace: true });
  };

  return (
    <div className="yf-login">
      <section className="yf-card yf-login__card" aria-labelledby="login-title">
        <Logo />
        <h1 id="login-title">Sign in</h1>
        <p className="yf-muted">Your SAP Business One operations — sales, purchasing, stock, banking and production — in one fast web app.</p>
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
