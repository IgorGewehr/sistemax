import { motion } from 'framer-motion';
import { Sparkles } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { Button } from '@/components/ui/Button';

interface OptInEmptyStateProps {
  titulo: string;
  descricao: string;
}

/**
 * Estado vazio de uma feature opt-in DESLIGADA (`analisePorProjetoAtiva`/`imobilizadoRoiAtivo`) —
 * "Ative X em Configurações" em vez de tabela/gráfico vazios. Reusado por Projetos e Investimento
 * & ROI (as duas telas do 2º toggle). Link único, sem CTA que a IA aciona sozinha — é sempre o
 * usuário quem decide ligar o toggle na tela de Configurações do Financeiro.
 */
export function OptInEmptyState({ titulo, descricao }: OptInEmptyStateProps) {
  const navigate = useNavigate();

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border/70 px-8 py-16 text-center"
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-soft text-primary-600">
        <Sparkles className="h-5 w-5" />
      </div>
      <h3 className="font-display text-base font-semibold text-foreground">{titulo}</h3>
      <p className="max-w-sm text-sm text-muted-foreground">{descricao}</p>
      <Button variant="primary" size="sm" onClick={() => navigate('/financeiro/configuracoes')}>
        Ir para Configurações
      </Button>
    </motion.div>
  );
}
