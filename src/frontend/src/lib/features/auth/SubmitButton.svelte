<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import type { Submission } from './submission.svelte';

  /** The auth form's button: disabled while in flight or rate-limited (saying *when*), never because a field looks wrong, which hides it as someone reaches for it. */
  interface Props {
    label: string;
    submission: Submission;
  }

  let { label, submission }: Props = $props();

  const secondsLeft = $derived(submission.retryIn);
</script>

<Button
  type="submit"
  variant="primary"
  size="lg"
  full
  disabled={secondsLeft !== null}
  loading={submission.showingProgress}
>
  {secondsLeft === null ? label : m['auth.retryIn']({ seconds: secondsLeft })}
</Button>
