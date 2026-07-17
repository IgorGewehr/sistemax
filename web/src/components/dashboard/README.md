# Dashboard

Visão do negócio inteiro num olhar — a primeira tela que o dono vê ao abrir o SistemaX. Modelada
no mesmo padrão SDD do `Financeiro › Visão Geral` (ver `docs/ui/financeiro-ui.md`), mas sem
mockup HTML próprio: o Dashboard não existia como mockup aprovado, então este README + `types.ts`
são a especificação de origem.

## Seções (ordem fixa)

1. **Header enxuto** — `PageHeader` com subtítulo + selo "Atualizado agora" (`FreshnessBadge`).
   Sem eyebrow, sem título repetindo "Dashboard" (a Sidebar já nomeia a tela).
2. **Fileira de KPIs** (`KpiRow`) — um número por módulo, ordem Vendas → Financeiro → Estoque →
   Compras → OS. "Faturamento hoje" é o único `hero`. Card inteiro é clicável (drill).
3. **Precisa de atenção agora** (`AtencaoSection`) — um achado por módulo, mais urgente primeiro.
   Some inteira se a lista filtrada vier vazia (não existe estado "tudo certo por aqui").
4. **Super Consultor** (`ConsultorDashboard`) — um único insight cross-módulo (hoje: Estoque ×
   Vendas). Read-only (Lei 2 do contrato do Financeiro, reaplicada aqui): só observa/aconselha,
   nunca uma ação que a IA executa. Só aparece se as permissões dos dois módulos citados na frase
   estiverem de pé.

Não há FAB nem seletor de período: o Dashboard não lança dados (isso é do Financeiro) e não tem
período pra trocar (é sempre "agora").

## Permission-aware

`usePermissoesDashboard()` retorna 5 flags mockadas (todas `true`): `podeVerFinanceiro`,
`podeVerVendas`, `podeVerEstoque`, `podeVerCompras`, `podeVerOs`. `Dashboard.tsx` filtra `kpis` e
`atencao` por essas flags **uma única vez**, antes de passar pras seções — nenhuma seção reavalia
permissão sozinha. Quando o RBAC existir, troca-se só o corpo do hook; a assinatura e o resto do
módulo não mudam.

## Drill entre módulos

`useDashboardDrill()` navega de verdade (`useNavigate`) + mostra um toast de contexto, igual ao
`useDrillNav` do Financeiro. Diferença: `DrillTarget.disponivel` — quando `false` (hoje, só
Vendas, que ainda não tem rota em `App.tsx`), o clique **só** mostra o toast, sem navegar, pra não
cair no catch-all e redirecionar pro Financeiro sem aviso.

## Arquivos

```
pages/Dashboard.tsx                        página fina — filtra por permissão e compõe as seções
components/dashboard/types.ts              view-model (o spec)
components/dashboard/usePermissoesDashboard.ts
components/dashboard/useDashboardDrill.ts
components/dashboard/KpiRow.tsx
components/dashboard/AtencaoSection.tsx
components/dashboard/ConsultorDashboard.tsx
components/dashboard/FreshnessBadge.tsx
mocks/dashboard.ts                         dados de exemplo, implementando o view-model
```

Rota (`/dashboard`) e item do `Sidebar` (já existe, hoje sem `live: true`) são ligados pelo
orquestrador quando este módulo entrar em produção.
