import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    strictPort: false,
    // Dev com HMR: a janela aponta pro Vite (SISTEMAX_UI_URL), mas o `fetch('/api/...')` do
    // cliente HTTP (lib/api/client.ts) precisa ser proxied pro Kestrel do Host.Desktop — ver
    // docs/arquitetura/bridge-http-local.md §3. SISTEMAX_PORT define a porta fixa dos dois lados.
    proxy: {
      '/api': {
        target: `http://127.0.0.1:${process.env.SISTEMAX_PORT ?? '5090'}`,
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    target: 'es2022',
  },
});
