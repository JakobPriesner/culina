<script lang="ts">
  /**
   * Culina's pot on a burner that has gone out.
   *
   * Shown when a session ended under somebody. Each character of the password
   * lights one more flame, so typing it back in is something to watch rather
   * than a chore, and the pot warms from grey to its own colour once all of
   * them are lit. Decoration only: the heading next to it says it in words.
   */
  interface Props {
    /** How many flames are lit, from none to all of them. */
    lit: number;
  }

  let { lit }: Props = $props();

  const flames = Array.from({ length: 9 }, (_, i) => 52 + i * 15);
  const hot = $derived(lit >= flames.length);
</script>

<svg class="stove" class:hot viewBox="0 0 220 150" aria-hidden="true">
  <g class="steam">
    <path d="M92 30 q-8 -12 0 -24 q8 -12 0 -24" />
    <path d="M110 28 q-8 -12 0 -24 q8 -12 0 -24" />
    <path d="M128 30 q-8 -12 0 -24 q8 -12 0 -24" />
  </g>

  <g class="pot" transform="translate(110 80) scale(0.36) translate(-256 -278)">
    <path d="M128 222 C128 186 180 172 256 172 C332 172 384 186 384 222 Z" />
    <circle cx="256" cy="148" r="22" />
    <path d="M144 244 H368 V316 A68 68 0 0 1 300 384 H212 A68 68 0 0 1 144 316 Z" />
    <rect x="108" y="252" width="52" height="32" rx="16" />
    <rect x="352" y="252" width="52" height="32" rx="16" />
  </g>

  <g class="frost">
    <path d="M178 46 v16 M170 54 h16 M172 48 l12 12 M184 48 l-12 12" />
    <circle cx="44" cy="70" r="2.5" />
    <circle cx="36" cy="58" r="1.6" />
  </g>

  <rect class="grate" x="40" y="137" width="140" height="5" rx="2.5" />

  <g transform="translate(0 136)">
    {#each flames as x, i (x)}
      <path class="flame" class:on={i < lit} d="M{x} 0 c-5 -6 -4 -12 0 -18 c4 6 5 12 0 18z" />
    {/each}
  </g>
</svg>

<style>
  .stove {
    display: block;
    width: min(100%, 15rem);
    overflow: visible;
  }

  .pot {
    fill: var(--border-strong);
    transition: fill var(--duration-slow) var(--ease-out);
  }

  .hot .pot {
    fill: var(--accent);
  }

  .frost {
    fill: var(--border-strong);
    stroke: var(--border-strong);
    stroke-width: 2;
    stroke-linecap: round;
    transition: opacity var(--duration-slow);
  }

  .hot .frost {
    opacity: 0;
  }

  .grate {
    fill: var(--border-strong);
  }

  .flame {
    fill: var(--border);
    transform-box: fill-box;
    transform-origin: 50% 100%;
    transform: scaleY(0.25);
    transition:
      transform var(--duration-base) cubic-bezier(0.3, 1.6, 0.5, 1),
      fill var(--duration-base);
  }

  .flame.on {
    fill: var(--brand-accent);
    transform: scaleY(1);
  }

  .steam path {
    fill: none;
    stroke: var(--border-strong);
    stroke-width: 3;
    stroke-linecap: round;
    opacity: 0;
  }

  .hot .steam path {
    opacity: 0.7;
  }

  @media (prefers-reduced-motion: no-preference) {
    .flame.on {
      animation: flicker 0.5s ease-in-out infinite alternate;
    }

    .flame.on:nth-child(odd) {
      animation-delay: -0.25s;
    }

    .hot .steam path {
      animation: rise 2.2s ease-out infinite;
    }

    .hot .steam path:nth-child(2) {
      animation-delay: 0.7s;
    }

    .hot .steam path:nth-child(3) {
      animation-delay: 1.4s;
    }
  }

  @keyframes flicker {
    to {
      transform: scaleY(0.82) skewX(3deg);
    }
  }

  @keyframes rise {
    0% {
      opacity: 0;
      transform: translateY(8px);
    }
    30% {
      opacity: 0.8;
    }
    100% {
      opacity: 0;
      transform: translateY(-22px);
    }
  }
</style>
