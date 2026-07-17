import { useCallback, useEffect, useRef, useState } from 'react';

interface Disclosure<T extends HTMLElement> {
  open: boolean;
  ref: React.RefObject<T | null>;
  toggle: (ev?: { stopPropagation: () => void }) => void;
  close: () => void;
}

/**
 * Dropdown com fechamento ao clicar fora — padrão dos menus "Período" e "Enviar" do mockup
 * (listener global de `click` que fecha qualquer popover aberto fora do seu próprio wrapper).
 */
export function useDisclosure<T extends HTMLElement = HTMLDivElement>(): Disclosure<T> {
  const [open, setOpen] = useState(false);
  const ref = useRef<T>(null);

  useEffect(() => {
    if (!open) return;
    function onDocumentClick(ev: MouseEvent) {
      if (ref.current && !ref.current.contains(ev.target as Node)) setOpen(false);
    }
    document.addEventListener('click', onDocumentClick);
    return () => document.removeEventListener('click', onDocumentClick);
  }, [open]);

  const toggle = useCallback((ev?: { stopPropagation: () => void }) => {
    ev?.stopPropagation();
    setOpen((v) => !v);
  }, []);
  const close = useCallback(() => setOpen(false), []);

  return { open, ref, toggle, close };
}
