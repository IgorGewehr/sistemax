import { useEffect } from 'react';

export interface HotkeyBinding {
  /** Tecla física, minúscula — ex: '1', 'n', 'escape', '/'. */
  key: string;
  handler: (e: KeyboardEvent) => void;
  /** Se true, ignora quando o foco está num campo editável (padrão: true). */
  ignoreInInputs?: boolean;
}

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable;
}

/**
 * Atalhos globais de navegação por teclado — ergonomia de PDV/balcão.
 * Números trocam de aba, `N` abre o lançamento rápido, `Escape` fecha overlays,
 * `/` foca a busca. Ignora quando o usuário está digitando num campo.
 */
export function useHotkeys(bindings: HotkeyBinding[]): void {
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      const editable = isEditableTarget(e.target);
      const key = e.key.toLowerCase();

      for (const binding of bindings) {
        const ignoreInInputs = binding.ignoreInInputs ?? true;
        if (editable && ignoreInInputs && key !== 'escape') continue;
        if (binding.key.toLowerCase() === key) {
          binding.handler(e);
          return;
        }
      }
    }

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [bindings]);
}
