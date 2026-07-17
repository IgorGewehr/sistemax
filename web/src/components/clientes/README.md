# Clientes

Gestão de clientes: lista/busca, KPIs de engajamento, ficha resumida (histórico de compras/OS)
e um Super Consultor read-only. CRUD via modal. Base pra CRM leve.

## Arquitetura

- `types.ts` é o spec (SDD) — `Cliente`, `HistoricoItem`, `ClienteFormValues`.
- `calc.ts` tem toda a matemática pura (segmentos, filtros) — testável sem `useState`/JSX.
- `useClientes.ts` concentra todo o estado; `Clientes.tsx`/`ClientesHome.tsx`/`ClienteView.tsx`
  ficam finos, só compondo seções a partir do view-model que o hook devolve.
- `mocks/clientes.ts` implementa `ClientesMock` — trocar por API não muda nenhuma tela.

## Decisões que não são óbvias

- **`status` (ativo/inativo) é cadastral; "sem comprar há 90d+" é sempre derivado** contra o "hoje"
  do cenário (`hojeLabel`), nunca persistido — evita a tag envelhecer e divergir da realidade.
- **Tags nunca guardam estado derivado** ("aniversariante", "sumiu") — só rótulos livres do
  operador ("vip", "atacado"). Estado derivado sempre recalcula.
- **Datas são strings pré-formatadas pt-BR**, nunca ISO — mesma convenção de `mocks/compras.ts`.
  Comparação de datas usa `Date.UTC` com números já parseados do split, nunca `new Date(string)`.
- **Chips locais (`chips.tsx`)**, não o `StatusChip` de `components/shared` — aquele é vocabulário
  de caixa (`sobra/falta/aberto/bateu`); aqui precisamos de `ativo/inativo/venda/os`, mesmo caso
  que `components/compras/chips.tsx` já resolveu.
- **Super Consultor é read-only** (Lei 2 do contrato do Financeiro, aplicada aqui por analogia):
  observa quem sumiu e oferece só um link de filtro — nunca "mandar mensagem"/"reengajar" como ação
  da IA.
- **Os 2 modais de CRUD (`ClienteFormModal`, `ConfirmStatusModal`) vivem no topo de `Clientes.tsx`**,
  não dentro de `ClientesHome`/`ClienteView` — o formulário de editar é acionado tanto da Home
  (criar) quanto da Ficha (editar), então o estado (`modalForm`) e a resolução do cliente-alvo
  (`clienteEmEdicao`/`clienteEmConfirmacao`, via `clienteById` contra a lista completa — nunca
  contra `clientesFiltrados`, que pode não conter o cliente sob edição) moram no hook, não numa
  das duas telas.
- **A cifra em R$ do card do Super Consultor nunca é hardcoded** — é sempre
  `somaGastoVidaCentavos(kpis.semComprar90d)` (`calc.ts`, computado em `useClientes.ts`), porque um
  valor fixo já divergiu do mock no passado (o texto chegou a afirmar quase o dobro do total real
  do segmento). A quantidade/nome/dias do texto continuam fixos — só a cifra é derivada.
- **Sem tom `info` em `chips.tsx`** (diferente de `components/compras/chips.tsx`): aqui o tom da
  linha de OS no histórico é sempre dinâmico via `statusHistoricoTone` (`pos/warn/faint`); um tom
  fixo `info` nunca seria exercitado, então não existe (YAGNI).
- **Sem mockup aprovado** (`docs/ui/mockups/clientes.html`) ao contrário de Financeiro/Compras —
  esta tela segue só o *padrão de arquitetura* (SDD, view-model tipado, componentes compartilhados),
  não a Lei 1 de "mockup como contrato". Antes de considerar o módulo fechado nos mesmos termos de
  Financeiro/Compras, produzir esse mockup (ou manter esta nota como a formalização de que ele não
  existe ainda).
