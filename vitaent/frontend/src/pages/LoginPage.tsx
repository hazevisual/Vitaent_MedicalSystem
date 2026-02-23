import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { apiFetch } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { withTenant } from '../tenancy/tenant';

const loginSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1)
});

type SignInResponse = {
  accessToken: string;
  user: {
    id: string;
    email: string;
    role: string;
  };
};

export function LoginPage() {
  const navigate = useNavigate();
  const { setSession } = useAuth();
  const [email, setEmail] = useState('admin@clinic1.local');
  const [password, setPassword] = useState('Admin123!');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    const parsed = loginSchema.safeParse({ email, password });
    if (!parsed.success) {
      setError(parsed.error.errors[0]?.message ?? 'Invalid form');
      return;
    }

    setLoading(true);

    try {
      const response = await apiFetch<SignInResponse>(withTenant('/api/auth/sign-in'), {
        method: 'POST',
        body: JSON.stringify(parsed.data)
      });

      setSession(response.accessToken, response.user);
      navigate('/');
    } catch {
      setError('Invalid credentials');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-100 flex items-center justify-center p-6">
      <form className="w-full max-w-sm rounded-xl bg-white shadow-md p-6 space-y-4" onSubmit={onSubmit}>
        <h1 className="text-2xl font-semibold text-brandPrimary">Vitaent Login</h1>
        <div>
          <label className="block text-sm mb-1">Email</label>
          <input
            className="w-full border rounded-lg px-3 py-2"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        <div>
          <label className="block text-sm mb-1">Password</label>
          <input
            className="w-full border rounded-lg px-3 py-2"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          className="w-full rounded-lg bg-brandPrimary text-white py-2 disabled:opacity-50"
          type="submit"
          disabled={loading}
        >
          {loading ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
