/**
 * Guarda de UX do wizard de 1º-boot (`TrocarPinObrigatorio`) — o backend (`Usuario.ValidarFormatoPin`,
 * ver `Usuario.cs`) só exige 4-8 dígitos numéricos, sem checar trivialidade. Recusar PINs óbvios
 * aqui é decisão só do cliente: evita que o founder troque "1234" (o PIN provisório do seed) por
 * outro igualmente fraco como "0000" ou "4321".
 */
export function ehPinTrivial(pin: string): boolean {
  if (/^(\d)\1*$/.test(pin)) return true; // todos os dígitos iguais: 0000, 1111, 999999...
  const crescente = '0123456789';
  const decrescente = '9876543210';
  return crescente.includes(pin) || decrescente.includes(pin); // sequência: 1234, 2345, 4321...
}
