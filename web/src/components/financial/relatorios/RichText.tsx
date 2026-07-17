import type { RichTextPart } from './types';

/** Renderiza trechos com negrito opcional — preserva o `<b>` do mockup a partir de dado plano. */
export function RichText({ parts }: { parts: RichTextPart[] }) {
  return (
    <>
      {parts.map((part, i) =>
        part.bold ? (
          <b key={i} className="font-bold text-foreground">
            {part.text}
          </b>
        ) : (
          <span key={i}>{part.text}</span>
        ),
      )}
    </>
  );
}
