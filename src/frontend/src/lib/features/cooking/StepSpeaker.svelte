<script lang="ts">
  import { Button } from '$ds';
  import { m, type Locale } from '$shell/i18n';
  import { speech } from './speech.svelte';

  /** Reads the step being cooked aloud, or stops it; absent where the browser has no speech engine. */
  interface Props {
    /** What is said, already in spoken form. */
    text: string;
    /** The language of the words, which picks the voice. */
    language: Locale;
  }

  let { text, language }: Props = $props();
</script>

{#if speech.supported && text}
  <Button size="sm" onclick={() => speech.toggle(text, language)}>
    {#snippet icon()}
      {#if speech.speaking}
        <svg viewBox="0 0 24 24" fill="currentColor"
          ><rect x="7" y="7" width="10" height="10" rx="2" /></svg
        >
      {:else}
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M4 10v4h4l5 4V6L8 10H4Z" stroke-linejoin="round" />
          <path d="M16.5 9a4 4 0 0 1 0 6M19 6.5a7.5 7.5 0 0 1 0 11" stroke-linecap="round" />
        </svg>
      {/if}
    {/snippet}
    {speech.speaking ? m['cooking.speech.stop']() : m['cooking.speech.read']()}
  </Button>
{/if}
