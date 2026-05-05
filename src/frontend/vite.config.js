import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const envDir = path.resolve(__dirname, '../..');

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, envDir, '');
  const frontendHost = env.VITE_FRONTEND_HOST || '127.0.0.1';
  const frontendPort = Number(env.VITE_FRONTEND_PORT || 5173);
  const previewPort = Number(env.VITE_FRONTEND_PREVIEW_PORT || 4173);
  const apiProxyTarget = env.VITE_API_PROXY_TARGET || 'http://127.0.0.1:5044';
  const healthProxyTarget = env.VITE_HEALTH_PROXY_TARGET || apiProxyTarget;

  return {
    envDir,
    plugins: [react()],
    server: {
      host: frontendHost,
      port: frontendPort,
      proxy: {
        '/api': apiProxyTarget,
        '/health': healthProxyTarget
      }
    },
    preview: {
      host: frontendHost,
      port: previewPort
    }
  };
});
