import { apiFetch } from '../api/client';

type BrandingPayload = {
  json: string;
};

const defaultBranding = {
  primary: '#0ea5e9',
  secondary: '#22c55e'
};

function applyBranding(branding: { primary?: string; secondary?: string }) {
  const root = document.documentElement;
  root.style.setProperty('--brand-primary', branding.primary ?? defaultBranding.primary);
  root.style.setProperty('--brand-secondary', branding.secondary ?? defaultBranding.secondary);
}

export async function loadBranding() {
  try {
    const payload = await apiFetch<BrandingPayload>('/api/tenant/branding?tenant=clinic1');
    const parsed = JSON.parse(payload.json) as { primary?: string; primaryColor?: string; secondary?: string };

    applyBranding({
      primary: parsed.primary ?? parsed.primaryColor,
      secondary: parsed.secondary
    });
  } catch {
    applyBranding(defaultBranding);
  }
}
