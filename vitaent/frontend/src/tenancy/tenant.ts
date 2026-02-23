const DEFAULT_TENANT = import.meta.env.VITE_TENANT_FALLBACK ?? 'clinic1';

function hostTenantSlug(hostname: string): string | null {
  if (!hostname || hostname === 'localhost' || hostname === '127.0.0.1') {
    return null;
  }

  const labels = hostname.split('.').filter(Boolean);
  if (labels.length < 2) {
    return null;
  }

  if (hostname.endsWith('.localhost')) {
    return labels[0] ?? null;
  }

  if (labels.length >= 3) {
    return labels[0] ?? null;
  }

  return null;
}

export function getTenantSlug(): string {
  const tenantFromQuery = new URLSearchParams(window.location.search).get('tenant');
  if (tenantFromQuery) {
    return tenantFromQuery;
  }

  return hostTenantSlug(window.location.hostname) ?? DEFAULT_TENANT;
}

export function withTenant(path: string): string {
  const tenantSlug = getTenantSlug();
  const url = new URL(path, window.location.origin);
  url.searchParams.set('tenant', tenantSlug);
  return `${url.pathname}${url.search}`;
}
