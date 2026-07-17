// @ts-check
import tsPlugin from '@typescript-eslint/eslint-plugin';
import importPlugin from 'eslint-plugin-import';
import reactHooksPlugin from 'eslint-plugin-react-hooks';

/**
 * Flat config (ESLint 9+). Escopo: `src/` apenas (script `lint` roda `eslint src`).
 *
 * Camadas:
 *  1. `@typescript-eslint` "recommended" — já inclui o subconjunto de `eslint:recommended`
 *     relevante pra TS (com `no-undef` desligado pra .ts/.tsx, redundante com o compilador)
 *     mais as regras específicas de TypeScript (`no-unused-vars`, `no-explicit-any`, etc).
 *  2. `react-hooks` — só as duas regras pedidas (rules-of-hooks + exhaustive-deps). As demais
 *     regras novas do plugin (v7, voltadas ao React Compiler — `immutability`, `use-memo`,
 *     `set-state-in-effect`, ...) não se aplicam aqui: o projeto não usa o Compiler, e ligá-las
 *     seria fora de escopo desta tarefa.
 *  3. `import/order` — agrupado (builtin/external → internal `@/*` → parent/sibling → index) e
 *     alfabético dentro de cada grupo. As regras de resolução do plugin (`no-unresolved`,
 *     `named`, `namespace`, `default`) ficam de fora de propósito: exigiriam um resolver de
 *     paths do TS (pacote não instalado nesta tarefa) e dariam falso-positivo em todo import
 *     `@/...`; o `tsc` já garante que os imports resolvem.
 *  4. `consistent-type-imports` — força `import type` pra imports usados só como tipo. O
 *     `verbatimModuleSyntax` do tsconfig já exige isso no compilador; a regra só torna o estilo
 *     consistente e dá autofix.
 */
export default [
  {
    ignores: ['dist/**', 'coverage/**', 'node_modules/**'],
  },

  ...tsPlugin.configs['flat/recommended'],

  {
    files: ['**/*.{ts,tsx}'],
    plugins: {
      'react-hooks': reactHooksPlugin,
      import: importPlugin,
    },
    settings: {
      'import/resolver': {
        node: {
          extensions: ['.js', '.jsx', '.ts', '.tsx'],
        },
      },
      'import/internal-regex': '^@/',
    },
    rules: {
      // Parâmetro/variável prefixado com `_` = intencionalmente não usado (ex: stub de mock que
      // precisa bater com a assinatura de um callback, mas ainda não implementa o efeito real).
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],

      // react-hooks: só as duas regras clássicas (ver nota no topo do arquivo).
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',

      // import/order: agrupado + alfabético.
      'import/order': [
        'error',
        {
          groups: ['builtin', 'external', 'internal', ['parent', 'sibling', 'index'], 'object'],
          pathGroups: [
            {
              pattern: '@/**',
              group: 'internal',
              position: 'after',
            },
          ],
          pathGroupsExcludedImportTypes: ['builtin'],
          'newlines-between': 'always',
          alphabetize: { order: 'asc', caseInsensitive: true },
        },
      ],
      'import/no-duplicates': 'error',
      'import/newline-after-import': 'error',

      // Tipos sempre via `import type` (autofix) — consistente com `verbatimModuleSyntax`.
      '@typescript-eslint/consistent-type-imports': [
        'error',
        { prefer: 'type-imports', fixStyle: 'separate-type-imports' },
      ],
    },
  },
];
