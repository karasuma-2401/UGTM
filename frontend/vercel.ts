import type { VercelConfig } from '@vercel/config/v1';

const configuredApiOrigin = process.env.API_ORIGIN?.trim();
if (!configuredApiOrigin) {
  throw new Error('API_ORIGIN must be configured in the Vercel project.');
}

const apiOrigin = new URL(configuredApiOrigin);
if (
  apiOrigin.protocol !== 'https:' ||
  apiOrigin.username ||
  apiOrigin.password ||
  apiOrigin.pathname !== '/' ||
  apiOrigin.search ||
  apiOrigin.hash
) {
  throw new Error(
    'API_ORIGIN must be an HTTPS origin without credentials, path, query, or fragment.',
  );
}

export const config: VercelConfig = {
  git: {
    deploymentEnabled: {
      '*': false,
      production: false,
    },
  },
  framework: 'vite',
  buildCommand: 'npm run build',
  outputDirectory: 'dist',
  rewrites: [
    { source: '/api/(.*)', destination: `${apiOrigin.origin}/api/$1` },
    { source: '/(.*)', destination: '/index.html' },
  ],
};
