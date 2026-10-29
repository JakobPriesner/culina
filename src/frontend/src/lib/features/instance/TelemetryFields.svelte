<script lang="ts">
  import { Field, Select } from '$ds';
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import { variables, type OtlpProtocol, type ServerDraft, type ServerFacts } from './types';

  /** Where traces, metrics and logs are exported to, if anywhere. */
  interface Props {
    draft: ServerDraft;
    facts: ServerFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();

  const protocolPinned = $derived(facts.pinned.has(variables.otlpProtocol));

  const protocols = [
    { value: 'grpc', label: m['server.telemetry.grpc']() },
    { value: 'http_protobuf', label: m['server.telemetry.http_protobuf']() }
  ];
</script>

<SettingField
  label={m['server.telemetry.endpoint']()}
  hint={m['server.telemetry.endpoint.hint']()}
  variable={variables.otlpEndpoint}
  pinned={facts.pinned.has(variables.otlpEndpoint)}
  type="url"
  inputmode="url"
  placeholder="http://collector:4317"
  {disabled}
  bind:value={draft.otlpEndpoint}
/>

<Field
  label={m['server.telemetry.protocol']()}
  hint={protocolPinned ? m['server.pinned']({ variable: variables.otlpProtocol }) : undefined}
>
  {#snippet children({ id, describedBy, invalid })}
    <Select
      {id}
      {describedBy}
      {invalid}
      disabled={disabled || protocolPinned}
      value={draft.otlpProtocol}
      options={protocols}
      onchange={(value) => (draft.otlpProtocol = value as OtlpProtocol)}
    />
  {/snippet}
</Field>
