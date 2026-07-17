import type { ReactNode } from 'react';

interface PageHeaderProps {
  /**
   * Linha descritiva da tela (o subtítulo). NÃO repetimos o título aqui: a aba ativa da barra do
   * Financeiro já nomeia a seção — eyebrow + H1 repetindo o nome da aba só comem espaço vertical.
   */
  subtitle?: ReactNode;
  /** Ações à direita (seletor de período, botão primário). */
  actions?: ReactNode;
  /** Selo discreto (ex.: `<MockBadge />`) logo após o subtítulo — tela 100% mock, ainda sem API. */
  badge?: ReactNode;
}

/** Cabeçalho enxuto de tela: subtítulo à esquerda, ações à direita. O título é a própria aba. */
export function PageHeader({ subtitle, actions, badge }: PageHeaderProps) {
  return (
    <header className="mb-5 flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
      <div className="flex flex-wrap items-center gap-2.5">
        {subtitle ? <p className="text-sm text-muted-foreground">{subtitle}</p> : <span />}
        {badge}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2.5">{actions}</div>}
    </header>
  );
}
