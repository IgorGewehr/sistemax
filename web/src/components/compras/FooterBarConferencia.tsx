import { Button } from '@/components/ui/Button';
import { Surface } from '@/components/ui/Surface';

interface FooterBarConferenciaProps {
  itensQueEntram: number;
  parcelasTxt: string;
  podeConfirmar: boolean;
  pendCount: number;
  onDescartar: () => void;
  onConfirmar: () => void;
}

/** Barra sticky de ação da conferência (`.footer-bar` do mockup) — some quando a nota é só-leitura. */
export function FooterBarConferencia({ itensQueEntram, parcelasTxt, podeConfirmar, pendCount, onDescartar, onConfirmar }: FooterBarConferenciaProps) {
  return (
    <Surface padding="none" className="sticky bottom-0 mt-1 flex flex-wrap items-center justify-between gap-3.5 px-[18px] py-3.5 shadow-[0_-6px_22px_-14px_rgba(0,0,0,0.3)]">
      <div className="text-[12.5px] text-muted-foreground">
        Ao confirmar: estoque <b className="font-bold text-foreground">+{itensQueEntram} itens</b> · conta a pagar{' '}
        <b className="font-bold text-foreground">{parcelasTxt}</b> · custo médio atualiza
      </div>
      <div className="flex items-center gap-2.5">
        {!podeConfirmar && (
          <span className="text-xs font-semibold text-warn">
            {pendCount} pendência{pendCount > 1 ? 's' : ''}
          </span>
        )}
        <Button variant="outline" size="sm" onClick={onDescartar}>
          Descartar nota
        </Button>
        <Button size="sm" onClick={onConfirmar} disabled={!podeConfirmar}>
          ✓ Confirmar recebimento
        </Button>
      </div>
    </Surface>
  );
}
