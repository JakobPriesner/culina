<script lang="ts">
  /**
   * One of several, all visible at once.
   *
   * Native radios inside a `Field` with `group`, so arrow keys move between
   * them and the group's label is announced before the chosen option — both of
   * which a set of styled buttons would have to reimplement and would get
   * subtly wrong.
   */
  export interface RadioOption {
    readonly value: string;
    readonly label: string;
    /** One line under the option, for a choice that needs a reason. */
    readonly description?: string;
    readonly disabled?: boolean;
  }

  interface Props {
    /** Shared by every input, which is what makes them one group. */
    name: string;
    value: string;
    options: readonly RadioOption[];
    describedBy?: string | undefined;
    onchange?: (value: string) => void;
  }

  let { name, value = $bindable(), options, describedBy, onchange }: Props = $props();
</script>

<div class="group" aria-describedby={describedBy}>
  {#each options as option (option.value)}
    <label class="option" class:disabled={option.disabled}>
      <input
        type="radio"
        {name}
        value={option.value}
        checked={value === option.value}
        disabled={option.disabled}
        onchange={() => {
          value = option.value;
          onchange?.(option.value);
        }}
      />
      <span class="text">
        <span class="label">{option.label}</span>
        {#if option.description}
          <span class="description">{option.description}</span>
        {/if}
      </span>
    </label>
  {/each}
</div>

<style>
  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .option {
    display: flex;
    align-items: flex-start;
    gap: var(--space-3);
    min-height: var(--control-sm);
    padding-block: var(--space-2);
    cursor: pointer;
  }

  .option.disabled {
    color: var(--text-subtle);
    cursor: not-allowed;
  }

  input {
    flex: none;
    width: var(--space-6);
    height: var(--space-6);
    margin: 0;
    accent-color: var(--accent);
    cursor: inherit;
  }

  .text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .description {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
