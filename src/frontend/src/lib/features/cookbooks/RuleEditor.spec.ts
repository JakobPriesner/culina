import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import RuleEditor from './RuleEditor.svelte';
import { renderWithProviders } from '$lib/test/render';

vi.mock('./stores/tags.svelte', () => ({ tags: { items: [], load: vi.fn() } }));

const rules = { tags: ['quick'], ingredients: [], maxMinutes: null };

describe('the cookbook rule editor', () => {
  it('adds a typed ingredient on Enter without submitting the surrounding form', async () => {
    const onchange = vi.fn();
    const onsubmit = vi.fn((event: Event) => event.preventDefault());

    // The sheet wraps the editor in a form, where Enter would save without the typed ingredient.
    const { container } = renderWithProviders(RuleEditor, {
      props: { householdId: 'h1', rules, onchange }
    });
    const form = document.createElement('form');

    form.addEventListener('submit', onsubmit);
    form.append(...container.childNodes);
    container.append(form);

    await userEvent.type(screen.getByRole('textbox', { name: /ingredient|zutat/i }), 'tofu{Enter}');

    expect(onchange).toHaveBeenCalledWith({ ...rules, ingredients: ['tofu'] });
    expect(onsubmit).not.toHaveBeenCalled();
  });
});
