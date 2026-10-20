import { routes, type VercelConfig } from '@vercel/config/v1';

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
    deploymentEnabled: false,
  },
  framework: 'vite',
  buildCommand: 'npm run build',
  outputDirectory: 'dist',
  rewrites: [
    routes.rewrite('/api/(.*)', `${apiOrigin.origin}/api/$1`),
    routes.rewrite('/(.*)', '/index.html'),
  ],
};
