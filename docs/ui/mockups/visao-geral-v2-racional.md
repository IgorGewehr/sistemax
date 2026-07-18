# Visão Geral v2 — racional

> Mockup: `docs/ui/mockups/visao-geral-v2.html`. Substitui o conceito da `visao-geral.html` (v1).
> Motivação: com 9 abas no Financeiro, a Visão Geral virou "mais uma tela cheia". A v2 é o
> **resumo holístico**: o pior sinal de cada área numa linha, detalhe nenhum — detalhe é papel
> das abas. Regra de ouro do dono: *"simples e passa o que importa rápido, MAS leva tudo em conta"*.

## O conceito em uma frase

**3 blocos, 3 perguntas**: ① *o que preciso fazer hoje?* (Super Consultor, só a prioridade nº 1),
② *o negócio está saudável?* (painel de saúde: 1 número-herói + 2 vitais + 7 linhas, uma por área),
③ *o caixa aguenta o mês?* (único gráfico: projeção 30 dias). Tudo o mais é drill — cada linha
clicável abre a aba dona do assunto. A tela **aponta, nunca gerencia**.

## Bloco a bloco

### ① Super Consultor — strip curto no topo (era o 5º bloco, virou o 1º)

| O que mostra | Read-model | Por que aqui |
|---|---|---|
| SÓ o insight de maior score, 1 frase + 1 ação de drill + "ver como calculamos" (fatos determinísticos, sem LLM) | `GET /financeiro/consultor` (topN=1) | "O que exige atenção AGORA" tem que ser a primeira coisa que o olho encontra. Lei 2 mantida: badge "somente leitura", botão só navega. Os demais insights ficam nos consultores das abas. |

### ② Painel de saúde (card duplo — o coração da tela)

**Esquerda — herói:**

| Elemento | Read-model | Por quê |
|---|---|---|
| Veredito (chip Verde/Âmbar/Vermelho) | derivado no front: pior tom entre os sinais abaixo (fôlego, atrasados, a pagar 30d, margem) | O dono quer a resposta de "está saudável?" em 1 símbolo. Tooltip ⓘ explica a régua — nada de score mágico. |
| **R$ 3.300 livre de verdade** (número-herói) + decomposição em 2 linhas clicáveis (banco+gaveta → Bancário; "já tem dono" → E&S) | `GET /financeiro/disponivel-retirada` | Mesmo herói da v1 (aprovado) — é O número do dono de PME. A gaveta do caixa físico entra aqui dentro (por isso a aba Fluxo de caixa não precisa de linha própria). |
| Vital "Fôlego": dia em que o caixa cruza zero + probabilidade de mês no vermelho | `GET /financeiro/previsao-caixa` (`primeiroDiaP50Negativo`, `probabilidadeSaldoNegativoEm30Dias`) | Resume o card "Runway" da antiga seção Sobrevivência em 1 linha, amarrada à MESMA história do gráfico e do Consultor (27/07, folha). |
| Vital "O mês já se paga": breakeven batido/dia + faturado acumulado | `GET /financeiro/ponto-equilibrio` | Resume o card "Ponto de equilíbrio" em 1 linha. Pergunta quant central: "o mês se sustenta?" |

**Direita — "O negócio por área", uma linha por dimensão** (dot de estado + sinal-chave + drill):

| Linha | Sinal exibido | Read-model | Drill |
|---|---|---|---|
| A receber | total em aberto · atrasado (nº clientes) · líquido esperado | `relatorios/contas-em-aberto` + `inadimplencia` (provisão por faixa vira só o "líquido esperado") | Entradas & saídas |
| A pagar · 30 dias | total · os 2 maiores vencimentos | `extrato` (KPIs a pagar previsto) | Entradas & saídas |
| Resultado do mês | resultado (competência) · margem % · **mini-split das 3 correntes** (barra Serviço/Recorrente/Comércio) | `relatorios/dre` por corrente | Relatórios |
| Assinaturas | MRR · ativas · churn do mês · MRR novo | `receita-recorrente` | Recorrentes |
| Simples Nacional | alíquota efetiva · faixa · distância ao degrau (+ meses projetados) | `radar-simples` | Relatórios |
| Investimento & ROI *(opt-in)* | % recuperado · falta R$ · mês do ROI completo · TIR | `roi-negocio` | Investimento & ROI |
| Projetos *(opt-in)* | quantos no azul · melhor margem · pior sinal (ociosidade) | `projetos` + `projetos/{id}/painel` | Projetos |

**Opt-in respeitado:** com os toggles desligados (`configuracoes`), as duas linhas somem e entra
**um** convite discreto tracejado ("ligue em Configurações") — nunca card vazio, nunca dado zerado.
O botão "⏻ opt-in" no mockup demonstra os dois estados (é só do mockup, não vai pro produto).

### ③ O caixa nos próximos 30 dias (único gráfico — mantido da v1)

| O que mostra | Read-model | Por quê |
|---|---|---|
| Realizado sólido + previsto tracejado, 1 callout no cruzamento de zero, clique por dia = evento do dia | `GET /financeiro/fluxo` | É a única visual que responde "o mês passa?" melhor que um número. O hint amarra ao Consultor: "o trecho vermelho some se os atrasados entrarem" — mesma história em três alturas da tela. |

## O que foi CORTADO da v1 (o declutter, provado)

- **Bloco "Próximos 7 dias"** (4 cards de vencimento) — morto. O vencimento crítico já está no
  Consultor; os demais estão no clique-por-dia do gráfico; a lista completa é da aba E&S.
- **Bloco "Sobrevivência"** (4 cards: Runway, Breakeven, Inadimplência, Radar do Simples) —
  dissolvido: Runway → vital "Fôlego"; Breakeven → vital "O mês já se paga"; Inadimplência →
  sub-linha de "A receber" (só o líquido esperado; as faixas de aging ficam em Relatórios);
  Radar → linha "Simples Nacional". 4 cards viraram 2 linhas + 2 subtítulos.
- **Card "Lucro do mês"** — virou a linha "Resultado do mês" (com o split de correntes, que a v1
  nem tinha). Cortados: delta vs mês passado, "de cada R$ 1 sobram…", ponte lucro×caixa (a ponte
  agora é a frase do Consultor, quando relevante).
- **Consultor gordo no rodapé** — virou strip de 1 frase no topo. O "ver como calculamos" fica,
  colapsado (é a assinatura de confiança do produto).

**Contagem**: v1 = 6 blocos empilhados (~14 números soltos + 4 cards + 4 vencimentos). v2 =
**3 blocos**: 1 frase de ação, 1 herói + 2 vitais + 7 linhas (cada uma UM sinal), 1 gráfico.
Nenhuma dimensão do negócio ficou de fora — cobertura das 9 abas: E&S (2 linhas), Recorrentes
(1), Projetos (1, opt-in), Bancário (drill do herói), Fluxo de caixa (gaveta dentro do herói —
o ritual de abrir/fechar caixa é operação, não saúde), Investimento & ROI (1, opt-in),
Relatórios (2 linhas), Configurações (convite do opt-in).

## Regras mantidas

- **Reais inteiros** em tudo (`formatCentavosWhole`); centavos só onde já é regra (nunca aqui).
- **Lei 2**: Consultor e veredito são read-only; todo botão só navega/filtra, nunca age.
- **Tokens idênticos** aos mockups aprovados (HSL, claro/escuro, `.num` tabular, `.card`,
  `.consultor`, `.drow`); zero CDN; fragmento começando em `<style>`.
- **Uma história só**: 8.400 − 12.050 + 3.970 = 320 no fim do mês; fundo do poço −1.340 em 27/07;
  +1.850 atrasados ⇒ pior dia +510. Herói, vitais, linhas, Consultor e gráfico batem entre si —
  o dono quant confere e fecha.
