<script lang="ts">
  import type { AppError } from '$api';
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import AuthIntro from './AuthIntro.svelte';
  import FormFailure from './FormFailure.svelte';
  import { decideAgentAuthorization, type AgentAuthorization } from './agentAuthorization';

  /**
   * The consent screen of Culina's OAuth server: which app asks, who vouches for its name, and where the browser goes
   * back to (the MCP authorization spec requires showing that host). The app's name is its own word, so it is only ever
   * text. Approving is an ordinary unsafe request, so a page elsewhere cannot approve on someone's behalf.
   */
  interface Props {
    authorization: AgentAuthorization;
    /** Called with the app's return address once answered; the page navigates there. */
    onanswered: (redirectUri: string) => void;
  }

  let { authorization, onanswered }: Props = $props();

  let answering = $state<'allow' | 'deny' | null>(null);
  let failure = $state<AppError | null>(null);

  async function answer(approved: boolean) {
    answering = approved ? 'allow' : 'deny';
    failure = null;

    const result = await decideAgentAuthorization(authorization.id, approved);

    // Stays disabled on the way out: a second answer would only be refused.
    if (result.ok) {
      onanswered(result.value);

      return;
    }

    failure = result.error;
    answering = null;
  }
</script>

<section class="consent">
  <AuthIntro
    eyebrow={m['authorize.eyebrow']()}
    title={m['authorize.title']({ client: authorization.clientName })}
    subtitle={m['authorize.intro']({ client: authorization.clientName })}
    long
  />

  <dl class="facts">
    <div>
      <dt>{m['authorize.app']()}</dt>
      <dd>{authorization.clientName}</dd>
    </div>
    <div>
      <dt>{m['authorize.vouchedFor']()}</dt>
      <dd>{authorization.vouchedForBy ?? m['authorize.unvouched']()}</dd>
    </div>
    <div>
      <dt>{m['authorize.returnsTo']()}</dt>
      <dd class="host">{authorization.redirectHost}</dd>
    </div>
  </dl>

  {#if authorization.redirectsToThisComputer}
    <p class="warning">{m['authorize.thisComputer']()}</p>
  {/if}

  <p>{m['authorize.can']()}</p>
  <p class="later">{m['authorize.later']()}</p>

  <FormFailure {failure} />

  <div class="actions">
    <Button
      variant="primary"
      size="lg"
      full
      loading={answering === 'allow'}
      disabled={answering !== null}
      onclick={() => answer(true)}
    >
      {m['authorize.allow']()}
    </Button>
    <Button
      size="lg"
      full
      loading={answering === 'deny'}
      disabled={answering !== null}
      onclick={() => answer(false)}
    >
      {m['authorize.deny']()}
    </Button>
  </div>
</section>

<style>
  .consent {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .facts {
    display: grid;
    gap: var(--space-3);
    margin: 0;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
  }

  .facts div {
    display: grid;
    gap: var(--space-1);
  }

  dt {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  dd {
    margin: 0;
    font-weight: 600;
  }

  .host {
    overflow-wrap: anywhere;
  }

  .warning {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--warning-subtle);
    border: 1px solid var(--warning);
  }

  .later {
    color: var(--text-muted);
  }

  .actions {
    display: grid;
    gap: var(--space-3);
  }
</style>
