import { Navigate, Route, Routes } from 'react-router-dom';

import { AppShell } from '@/components/layout/AppShell';
import { Agenda } from '@/pages/Agenda';
import { Clientes } from '@/pages/Clientes';
import { Compras } from '@/pages/Compras';
import { Configuracoes } from '@/pages/Configuracoes';
import { Dashboard } from '@/pages/Dashboard';
import { Estoque } from '@/pages/Estoque';
import { Bancario } from '@/pages/financeiro/Bancario';
import { EntradasSaidas } from '@/pages/financeiro/EntradasSaidas';
import { FinanceiroLayout } from '@/pages/financeiro/FinanceiroLayout';
import { FluxoCaixa } from '@/pages/financeiro/FluxoCaixa';
import { Recorrentes } from '@/pages/financeiro/Recorrentes';
import { Relatorios } from '@/pages/financeiro/Relatorios';
import { VisaoGeral } from '@/pages/financeiro/VisaoGeral';
import { OrdemServico } from '@/pages/OrdemServico';
import { Pdv } from '@/pages/Pdv';
import { Vendas } from '@/pages/Vendas';

export function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="financeiro" element={<FinanceiroLayout />}>
          <Route index element={<VisaoGeral />} />
          <Route path="entradas-saidas" element={<EntradasSaidas />} />
          <Route path="recorrentes" element={<Recorrentes />} />
          <Route path="bancario" element={<Bancario />} />
          <Route path="fluxo-de-caixa" element={<FluxoCaixa />} />
          <Route path="relatorios" element={<Relatorios />} />
        </Route>
        <Route path="pdv" element={<Pdv />} />
        <Route path="estoque" element={<Estoque />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="compras" element={<Compras />} />
        <Route path="ordens" element={<OrdemServico />} />
        <Route path="clientes" element={<Clientes />} />
        <Route path="vendas" element={<Vendas />} />
        <Route path="agenda" element={<Agenda />} />
        <Route path="configuracoes" element={<Configuracoes />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Route>
    </Routes>
  );
}
