<script lang="ts">
  import { pencilGrip, rightHandlePath } from './geometry';
  import type { PoseSpec } from './poses';
  import type { Rig } from './rig.svelte';

  /** One pair of handles in every pose, bending toward a held prop; the drawing pose draws the brush's right handle itself. */
  let { prop, rig }: { prop: PoseSpec['prop']; rig: Rig } = $props();

  const gripsLeft = $derived(prop === 'pencil' || prop === 'brush' || prop === 'card');
  const gripsRight = $derived(prop === 'pencil' || prop === 'phone');
  const rightHandle = $derived(
    rightHandlePath(
      false,
      prop === 'pencil'
        ? pencilGrip(rig.pencilX.current, rig.pencilY.current, rig.penLift.current)
        : [87, 83]
    )
  );
</script>

<g class="handle handle-left">
  {#if gripsLeft}
    <path
      class="rim"
      d="M29 65 C13 64 12 90 31 97 L39 98 Q45 96 39 92 L32 90 C23 86 24 75 31 73 Z"
    />
  {:else}
    <rect
      class="rim"
      x="11"
      y="61"
      width="18"
      height="10"
      rx="5"
      transform="rotate({rig.armL.current} 27 66)"
    />
  {/if}
</g>
{#if prop !== 'brush'}
  <g class="handle handle-right">
    {#if gripsRight}
      <path class="rim" d={rightHandle} />
    {:else}
      <rect
        class="rim"
        x="91"
        y="61"
        width="18"
        height="10"
        rx="5"
        transform="rotate({-rig.armR.current} 93 66)"
      />
    {/if}
  </g>
{/if}
