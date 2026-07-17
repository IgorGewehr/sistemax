# Financeiro — Contrato de UI (SDD)

> **Documento vinculante.** Toda tela do Financeiro (e, por extensão, o padrão de UI do
> SistemaX) obedece a este contrato. Os **mockups aprovados** em `docs/ui/mockups/*.html` são a
> **fonte da verdade** — não são inspiração, são especificação. Se a implementação diverge do
> mockup, a implementação está errada.

O coração do sistema é o financeiro; a UI existe para que um leigo enxergue como um consultor
sênior. Base impecável, tipada, documentada — o mesmo rigor DDD/SDD do backend .NET, agora no front.

---

## 1. As duas leis (inegociáveis)

### Lei 1 — O mockup é o contrato
Cada tela reproduz **1:1** o seu mockup: mesmas seções, mesma **ordem**, mesma **copy**, mesmas
**interações**, mesmos dados de exemplo. Não se inventa aba, seção, botão ou simulador que não
esteja no mockup. Não se remove nada que esteja. Reproduz-se o **resultado visual** do mockup
usando o design system do app (Tailwind + tokens + componentes) — **não** se copia o CSS cru do
mockup.

### Lei 2 — A IA é read-only
O **Super Consultor** apenas **observa, explica e aconselha** — inline, dentro da tela (como o card
"as faltas se concentram nas quintas à tarde… ponha um segundo conferente → *Ver as quintas*").
Ele **nunca age**: proibido "Sim, automatizar", "Quer que eu faça…", "Aplicar sugestão",
simuladores "E se…" apresentados como ação da IA, ou qualquer CTA que dê a entender que a IA
executa algo no sistema. O usuário é quem age; a IA informa.

---

## 2. Inventário de telas (rota ↔ mockup ↔ intenção)

| Rota | Mockup (fonte da verdade) | Intenção |
|---|---|---|
| `/financeiro` (index) | `visao-geral.html` | Visão geral — saúde, disponível p/ retirada, próximos 7 dias, insight |
| `/financeiro/entradas-saidas` | `entradas-saidas.html` | Lançamentos: entradas e saídas, filtros, tabela |
| `/financeiro/recorrentes` | `recorrentes.html` **+** `financeiro-assinaturas.html` | Contas fixas **e** Assinaturas (sub-visões) |
| `/financeiro/fluxo-de-caixa` | `fluxo-de-caixa.html` | Ritual do caixa em espécie (abertura/fechamento cego/sangria/quebra) |
| `/financeiro/bancario` | `bancario.html` | Contas bancárias, extrato, conciliação |
| `/financeiro/relatorios` | `relatorios.html` | DRE, fluxo, análises |

Abas do topo (exatamente estas 6, nesta ordem — vêm do mockup):
**Visão geral · Entradas & saídas · Recorrentes · Bancário · Fluxo de caixa · Relatórios.**
Sem aba "Consultor". Sem menu "Mais/em breve". Assinaturas é sub-visão de Recorrentes.

---

## 3. Mapa token → classe (o mockup usa CSS vars; o app usa Tailwind)

Os mockups declaram vars locais (`--pos`, `--surface-2`…). No app já existem como tokens que
**viram dark automaticamente** (via `.dark`). Use a classe do app; **nunca** hardcode HSL.

| Mockup var / classe | Classe do app |
|---|---|
| `--bg` | `bg-background` |
| `--surface` / `.card` | componente `<Surface>` (ou `bg-card`) |
| `--surface-2` | `bg-surface-2` |
| `--fg` | `text-foreground` |
| `--muted` (texto) | `text-muted-foreground` |
| `--faint` | `text-faint` |
| `--border` | `border-border` |
| `--primary` | `text-primary-600` / `bg-primary-600` |
| `--primary-soft` | `bg-primary-soft` |
| `--pos` / `--pos-soft` | `text-pos` / `bg-pos-soft` |
| `--crit` / `--crit-soft` | `text-crit` / `bg-crit-soft` |
| `--warn` / `--warn-soft` | `text-warn` / `bg-warn-soft` |
| `.num` (mono tabular) | `className="num"` (já existe em globals.css) |
| `.eyebrow` | componente `<Eyebrow>` |
| `.chip` (falta/sobra/aberto/bateu) | componente `<StatusChip>` |
| `.btn-primary` / `.btn-ghost` / `.btn-sm` | `<Button variant/size>` |

**Cores de estado são reservadas** — `pos/crit/warn` só significam estado (sobra/falta/atenção),
nunca "série 4" de um gráfico.

---

## 4. Padrão de arquitetura de tela (o que faz a base ser invejável)

```
pages/financeiro/<Tela>.tsx          página FINA — só compõe as seções, sem lógica de dados
components/financial/<tela>/          seções da tela (uma responsabilidade cada)
components/financial/shared/          primitivos reusados (PageHeader, KpiCard, StatusChip…)
components/financial/<tela>/types.ts  VIEW-MODEL tipado da tela = o spec (SDD)
mocks/financeiro/<tela>.ts            dados de exemplo do mockup, implementando o view-model
```

Regras:
- **Zero `any`.** Todo dado tem tipo. O view-model é declarado **antes** da tela (SDD) e a tela
  consome `z.infer`/`type` — nunca interface paralela.
- **Página fina.** `<Tela>.tsx` importa seções e passa o view-model; nada de `fetch`/cálculo
  pesado inline. Cálculo derivado vai em helper puro e testável.
- **Dados = view-model, hoje mock, amanhã API.** O mock **implementa o mesmo tipo** que a API vai
  devolver. Trocar mock→API = trocar a origem, não a tela. Marque o mock com
  `// MOCK — trocar por <endpoint> quando a API existir`.
- **JSDoc do "porquê".** Comentário explica decisão não-óbvia, nunca descreve o óbvio.
- **Acessibilidade & motion.** `framer-motion` com `AnimatePresence`; **nunca** `active:scale`
  em alvo clicável (encolhe a hitbox e cancela o clique — use `active:brightness-95`);
  `touch-action: manipulation` já é global.
- **Dark mode** sai de graça pelos tokens; onde precisar de variante, use `dark:`.

---

## 5. Componentes compartilhados (vocabulário — `components/financial/shared/`)

| Componente | Papel |
|---|---|
| `PageHeader` | eyebrow + título + subtítulo + ações à direita (período, botão primário) |
| `Eyebrow` | rótulo pequeno maiúsculo (`Financeiro · Fluxo de Caixa`) |
| `KpiCard` | card de KPI: rótulo, valor grande (num), delta/foot opcional, variante `hero` |
| `ConsultorInsight` | card do Super Consultor: ícone + título + parágrafo + link de drill (READ-ONLY) |
| `StatusChip` | chip de estado: `falta`/`sobra`/`aberto`/`bateu`/neutro (usa tokens pos/crit/warn) |
| `SectionCard` | `<Surface>` com header (`h2.sec` + hint) padronizado |
| `DataTable` | tabela responsiva (scroll-x próprio), thead uppercase, linhas clicáveis opcionais |
| `MoneyValue` | valor em centavos formatado, com cor de sinal. Props `signed`, `whole`, `tone` |
| `MoneyWhole` | atalho de `<MoneyValue whole>` — reais inteiros sem centavos |

**Dinheiro (regra dura):** valor é sempre **centavos inteiros** (`Centavos`, espelha o `Money` .NET).
Formatadores em `@/lib/money` — **fonte única, nunca duplicar por tela**: `formatCentavos`/
`formatSignedCentavos` (2 casas, p/ precisão: margem "R$ 0,18", diferença média) e
`formatCentavosWhole`/`formatSignedCentavosWhole` (reais inteiros — como a maioria dos mockups do
Financeiro exibe: `brl()`/`money()` arredonda e não mostra centavos). Na dúvida, siga o mockup.

Utilidades: `formatCurrency`, `formatSignedCurrency`, `formatPercent`, `formatDate*` de
`@/lib/format`. `Surface`, `Button`, `Kbd` de `@/components/ui/*`.

---

## 6. Checklist de verificação (o verify roda isto contra o mockup)

Uma tela só passa se **todas** forem verdade:
1. **Seções** — todas as seções do mockup existem, na **mesma ordem**. Nenhuma seção extra.
2. **Copy** — textos/rótulos/números batem com o mockup (mesmos exemplos).
3. **Interações** — cliques/drills/modais/toggles do mockup funcionam igual.
4. **IA read-only** — zero copy de IA-que-age (grep: `automatiz`, `aplicar sugest`, `quer que eu`).
5. **Tokens** — cores de estado via `pos/crit/warn`; nada de HSL hardcoded ou cor errada.
6. **Arquitetura** — view-model tipado, página fina, zero `any`, mock tipado, JSDoc de porquê.
7. **Compila** — `pnpm typecheck` limpo; sem `active:scale` em botão.

Divergência em qualquer item = correção antes de entregar.
