import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import { useAuth } from '../auth/AuthContext';

type TenantMe = {
  tenantId: string;
  slug: string;
  name: string;
};

type RefreshResponse = {
  accessToken: string;
};

export function DashboardPage() {
  const { user, accessToken, updateToken, clearSession } = useAuth();
  const navigate = useNavigate();

  const tenantQuery = useQuery({
    queryKey: ['tenant-me'],
    queryFn: () => apiFetch<TenantMe>('/api/tenant/me?tenant=clinic1', { token: accessToken })
  });

  const refreshSession = async () => {
    const result = await apiFetch<RefreshResponse>('/api/auth/refresh?tenant=clinic1', {
      method: 'POST'
    });
    updateToken(result.accessToken);
  };

  const signOut = async () => {
    await apiFetch<void>('/api/auth/sign-out?tenant=clinic1', {
      method: 'POST',
      token: accessToken
    });
    clearSession();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-slate-50 p-8">
      <div className="max-w-3xl mx-auto bg-white rounded-xl shadow p-6 space-y-4">
        <h1 className="text-2xl font-semibold text-brandPrimary">Dashboard</h1>
        <div className="text-sm space-y-1">
          <p>User: {user?.email ?? '-'}</p>
          <p>Role: {user?.role ?? '-'}</p>
          <p>Tenant slug: {tenantQuery.data?.slug ?? 'Loading...'}</p>
          <p>Tenant name: {tenantQuery.data?.name ?? 'Loading...'}</p>
        </div>

        <div className="flex gap-3">
          <Link className="rounded-lg bg-brandPrimary text-white px-4 py-2" to="/schedule">
            Open schedule
          </Link>
          <button className="rounded-lg bg-brandSecondary text-white px-4 py-2" onClick={refreshSession}>
            Refresh session
          </button>
          <button className="rounded-lg bg-slate-800 text-white px-4 py-2" onClick={signOut}>
            Sign out
          </button>
        </div>
      </div>
    </div>
  );
}
