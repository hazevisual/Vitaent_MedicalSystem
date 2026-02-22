import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brandPrimary: 'var(--brand-primary)',
        brandSecondary: 'var(--brand-secondary)'
      }
    }
  },
  plugins: []
} satisfies Config;
