import { describe, expect, it } from 'vitest';

import { ehPinTrivial } from './pin';

describe('ehPinTrivial', () => {
  it('recusa dígitos repetidos', () => {
    expect(ehPinTrivial('0000')).toBe(true);
    expect(ehPinTrivial('1111')).toBe(true);
    expect(ehPinTrivial('99999999')).toBe(true);
  });

  it('recusa sequência crescente', () => {
    expect(ehPinTrivial('1234')).toBe(true);
    expect(ehPinTrivial('2345')).toBe(true);
    expect(ehPinTrivial('012345')).toBe(true);
  });

  it('recusa sequência decrescente', () => {
    expect(ehPinTrivial('4321')).toBe(true);
    expect(ehPinTrivial('9876')).toBe(true);
  });

  it('aceita PIN não-trivial', () => {
    expect(ehPinTrivial('1902')).toBe(false);
    expect(ehPinTrivial('7531')).toBe(false);
    expect(ehPinTrivial('4827')).toBe(false);
  });
});
