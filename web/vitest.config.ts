import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// A maioria dos testes é lógica pura (money/format/calc.ts) — não precisa de DOM,
// então o ambiente padrão é `node` (mais rápido). Suítes que precisam de DOM podem
// declarar `// @vitest-environment jsdom` no topo do arquivo.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'node',
    globals: false,
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      include: ['src/lib/money.ts', 'src/lib/format.ts', 'src/components/**/calc.ts'],
    },
  },
});
