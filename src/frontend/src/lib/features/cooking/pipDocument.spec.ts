import { afterEach, expect, it, vi } from 'vitest';
import { mirrorPipDocument } from './pipDocument';

let dispose: (() => void) | undefined;
let added: Element[] = [];
afterEach(() => {
  dispose?.();
  added.forEach((node) => node.remove());
  added = [];
  document.documentElement.removeAttribute('data-kitchen-lighting');
  document.documentElement.style.removeProperty('--pip-gap');
});

it('mirrors live root styles through CSSOM without writing a style attribute', async () => {
  document.documentElement.style.setProperty('--pip-gap', '8px', 'important');
  const target = document.implementation.createHTMLDocument();
  const attributes = vi.spyOn(target.documentElement, 'setAttribute');
  dispose = mirrorPipDocument(document, target);
  expect(target.documentElement.style.getPropertyValue('--pip-gap')).toBe('8px');
  expect(target.documentElement.style.getPropertyPriority('--pip-gap')).toBe('important');
  document.documentElement.style.removeProperty('--pip-gap');
  await vi.waitFor(() =>
    expect(target.documentElement.style.getPropertyValue('--pip-gap')).toBe('')
  );
  expect(attributes).not.toHaveBeenCalledWith('style', expect.any(String));
});

it('carries inline CSS, linked font resources and live appearance into the child', async () => {
  const style = document.createElement('style');
  style.textContent = '.companion { color: red; }';
  const font = document.createElement('link');
  font.rel = 'preload';
  font.setAttribute('as', 'font');
  font.href = '/fonts/editorial.woff2';
  font.crossOrigin = 'anonymous';
  document.head.append(style, font);
  added.push(style, font);
  document.documentElement.dataset['theme'] = 'warm-paper';
  document.documentElement.dataset['mode'] = 'dark';
  document.documentElement.lang = 'de';
  const target = document.implementation.createHTMLDocument();
  const originalChildren = target.head.childElementCount;
  dispose = mirrorPipDocument(document, target);
  expect(target.head.querySelector('style')?.textContent).toContain('color: red');
  expect(target.head.querySelector('link[as="font"]')?.getAttribute('crossorigin')).toBe(
    'anonymous'
  );
  expect(target.head.querySelector('link')?.href).toBe(font.href);
  expect(target.head.querySelector('base')).toBeNull();
  expect(target.documentElement.dataset['mode']).toBe('dark');
  expect(target.documentElement.lang).toBe('de');
  document.documentElement.dataset['kitchenLighting'] = 'oled';
  await vi.waitFor(() => expect(target.documentElement.dataset['kitchenLighting']).toBe('oled'));
  style.textContent = '.companion { color: blue; }';
  await vi.waitFor(() =>
    expect(target.head.querySelector('style')?.textContent).toContain('color: blue')
  );
  dispose();
  dispose = undefined;
  document.documentElement.dataset['mode'] = 'light';
  await Promise.resolve();
  expect(target.documentElement.dataset['mode']).toBe('dark');
  expect(target.head.children).toHaveLength(originalChildren);
});

it('preserves the production nonce and rebases relative resources from linked stylesheets', () => {
  const style = document.createElement('style');
  style.nonce = 'culina-nonce';
  style.textContent = '.companion { background-image: url("./fonts/editorial.woff2"); }';
  document.head.append(style);
  added.push(style);
  Object.defineProperty(style.sheet!, 'href', { value: 'https://culina.test/assets/kitchen.css' });
  const target = document.implementation.createHTMLDocument();
  dispose = mirrorPipDocument(document, target);
  const copied = target.head.querySelector('style');
  expect(copied?.nonce).toBe('culina-nonce');
  expect(copied?.textContent).toContain('https://culina.test/assets/fonts/editorial.woff2');
});
