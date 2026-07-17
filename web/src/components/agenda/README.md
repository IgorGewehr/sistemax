# Agenda

Calendário de agendamentos (dia/semana/mês) — porte do módulo Agenda do
saas-erp (ServicePro), adaptado ao SistemaX: mock tipado em vez de Firebase,
dinheiro em centavos, sem i18n/MUI/date-fns.

## Arquitetura

- `types.ts` — o spec (SDD): `Agendamento`, `Cliente`, `Profissional`,
  `Servico`, a FSM de status (`AGENDAMENTO_TRANSICOES`) e o formulário
  (`AgendamentoFormData`, distinto do agendamento persistido).
- `calc.ts` — funções puras: datas (substituem `date-fns`, ausente do
  `package.json`), horário, conflito de agenda (mesma regra do
  `appointmentConflicts.ts` do saas-erp), agrupamento/resumo por status,
  geração de série recorrente, insight do Super Consultor e o vocabulário de
  status (`STATUS_LABEL`/`STATUS_TONE_CLASSES`). Só importa tipos de
  `types.ts`, nunca o inverso.
- `useAgenda.ts` — todo o estado (view/data atual, filtro de profissional,
  dialog ativo) + os derivados (agendamentos filtrados/agrupados/visíveis,
  resumo de status, insight do consultor) + os handlers de CRUD/FSM. A
  página e os componentes só consomem o `AgendaVm` que ele devolve —
  `salvar`/`mudarStatus`/`excluir` só fazem `setAgendamentos(prev => …)`
  (sem `businessId`/Firestore); quando a API existir, essas funções trocam
  de corpo sem a página mudar.
- `mocks/agenda.ts` — catálogos de cliente/profissional/serviço +
  agendamentos de exemplo, ancorados em `ANCHOR_HOJE = 2026-07-16`
  (quinta-feira — mesma data "hoje" do mock de Compras, pra manter as
  telas do SistemaX coerentes num mesmo passeio de demo).

## Componentes

`AgendaCalendarCard` é o "hero" da tela: hospeda `AgendaToolbar` (navegação +
`MiniCalendarPopover` + toggle Dia/Semana/Mês + botão "Novo agendamento"),
`StatusSummaryBar`, `ProfissionalFilterBar` (só aparece com 2+
profissionais) e a view ativa (`DayView`/`WeekView`/`MonthView`, alternadas
via `AnimatePresence` com slide horizontal). `TimeGridShell.tsx` exporta
`TimeColumn` (rótulos de hora, 1x por view) e `GridColumn` (linhas + linha do
"agora" + slots/blocos, repetido por coluna — 1x no Dia, 7x na Semana).

Os 3 dialogs (`AppointmentViewDialog`, `AppointmentFormDialog`,
`DeleteConfirmDialog`) usam o shell local `AgendaDialogShell` — substitui
`@mui/material/Dialog` (ausente do SistemaX) com `header`/`footer` livres,
que o `Modal` genérico de `components/ui/` não oferece. Todos os 3 são
renderizados incondicionalmente pela página e controlam sua própria
visibilidade via `vm.dialog.kind`, guardando o último agendamento visto em
estado local — sem isso, o conteúdo sumiria abruptamente em vez de esmaecer
junto com a animação de saída.

## Fora de escopo (Fase 2)

- **Turmas/capacidade em grupo** (`Servico.capacidade`, grade semanal,
  seletor de slots no form) — feature de academia; o campo `capacidade?`
  existe no tipo pra não fechar a porta, mas nenhuma UI foi construída.
- **Gerenciar Serviços (CRUD)** — o catálogo de serviços é somente-leitura
  no seletor do form; o editor completo (`ServiceManagementDialog` do
  source) fica pra depois.
- **Side-effects cross-módulo** (comissão, pontos de fidelidade, baixa de
  estoque do BOM do serviço, sync Google Calendar, lembretes WhatsApp) —
  todos dependiam de módulos Firebase que não existem no SistemaX. Ficam
  como comentário `// TODO(fase-2)` em `salvar`/`mudarStatus`, sem fingir
  que rodam.
- **Multi-tenant/roles** — toda ação fica liberada no mock (sem modelo de
  usuário/role no SistemaX ainda).
- **Edição em massa de série recorrente** — `salvar` só cria a série na
  criação e `excluirSerie` remove todas as ocorrências; editar 1 ocorrência
  não propaga pras demais (mesmo comportamento do source).

## Vocabulário de status

`AgendaStatusChip` (+ `STATUS_TONE_CLASSES` em `calc.ts`) define a paleta
dos 6 status do agendamento reusando os tokens reservados
(pos/warn/crit/primary/faint/surface-2) — mesmo princípio do `OsStatusChip`
de Ordem de Serviço: vocabulário próprio do módulo, nunca hex cru.
`AppointmentBlock` (bloco no calendário) usa a mesma tabela pra
`border-l-{tom}` + fundo suave, eliminando os `STATUS_COLORS`/
`STATUS_BG_COLORS` em hex do source.

## Datas — cuidado com fuso horário

`calc.ts` nunca usa `new Date('yyyy-mm-dd')` direto (parseia como UTC e
desalinha 1 dia em fusos negativos como o do Brasil). `parseISODate` sempre
força `T00:00:00` (meia-noite local) — mesmo cuidado que `safeDate` já
documenta em `lib/format.ts`. `isHojeReal` compara contra o relógio REAL do
dispositivo (não contra `ANCHOR_HOJE`) — é o que destaca "hoje" na grade e
mostra a linha do "agora"; se o relógio real não cair em 2026-07-16, a
grade simplesmente não realça nenhum dia (degradação graciosa, sem erro).
