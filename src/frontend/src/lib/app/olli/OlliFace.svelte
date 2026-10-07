<script lang="ts">
  import { eyeTransform } from './geometry';
  import type { PoseSpec } from './poses';
  import type { Rig } from './rig.svelte';

  /** The cheeks, eyes, mouth and brows. Eyes shut by scaling with the rig's lid. */
  let { spec, rig }: { spec: PoseSpec; rig: Rig } = $props();

  const eyesAt = [50, 70];
</script>

<ellipse class="cheek" cx="40" cy="84" rx="4.8" ry="3" />
<ellipse class="cheek" cx="80" cy="84" rx="4.8" ry="3" />

<g transform="translate({rig.lookX.current} {rig.lookY.current})">
  {#each eyesAt as x (x)}
    <g transform={eyeTransform(x, rig.lid.current)}>
      {#if spec.eyes === 'happy'}
        <path class="none thick" d="M{x - 5} 76.5 q5 -7 10 0" />
      {:else if spec.eyes === 'closed'}
        <path class="none thick" d="M{x - 5} 74.5 q5 5 10 0" />
      {:else}
        <ellipse class="ink bare" cx={x} cy="75" rx="4.6" ry="5.8" />
        <circle class="glint bare" cx={x - 1.5} cy="72.8" r="1.6" />
      {/if}
    </g>
  {/each}
</g>

{#if spec.mouth === 'open'}
  <path class="mouth" d="M55 84 q5 8 10 0 z" />
{:else if spec.mouth === 'o'}
  <ellipse class="mouth" cx="60" cy="86" rx="2.6" ry="3.2" />
{:else if spec.mouth === 'flat'}
  <path class="none" d="M56 86 h8" />
{:else if spec.mouth === 'wobble'}
  <path class="none" d="M54 86.5 q3 -2.5 6 0 q3 2.5 6 0" />
{:else}
  <path class="none" d="M56 85 q4 4 8 0" />
{/if}

{#if spec.brows === 'worried'}
  <path class="none thin" d="M45 67 l8 -3 M75 67 l-8 -3" />
{:else if spec.brows === 'puzzled'}
  <path class="none thin" d="M66 64 q4 -3 8 0" />
{/if}

<style>
  .cheek {
    fill: var(--mascot-cheek);
    stroke: none;
    opacity: 0.6;
  }

  .glint {
    fill: var(--mascot-hat);
  }

  .mouth {
    fill: var(--mascot-mouth);
    stroke-width: 2.2;
  }
</style>
