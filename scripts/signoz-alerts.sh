#!/usr/bin/env bash
#
# Keeps SigNoz's alert rules, notification channels and dashboards in step with
# deploy/observability/signoz.
#
#   scripts/signoz-alerts.sh check    # validate the files; no network (also `make alerts-check`)
#   scripts/signoz-alerts.sh plan     # what apply would create, update and delete
#   scripts/signoz-alerts.sh apply    # do it (also `make alerts-apply`)
#
# plan and apply need SIGNOZ_URL (e.g. http://casaos.local:7890) and SIGNOZ_API_KEY (SigNoz →
# Settings → Service Accounts, admin: channels are admin-only). Channels are filled from the
# environment and skipped while a variable they use is unset (a *_TOKEN is optional):
#
#   CULINA_ALERT_NTFY_URL              the ntfy server, e.g. https://ntfy.example.com
#   CULINA_ALERT_NTFY_TOKEN            optional, for protected topics
#   CULINA_ALERT_EMAIL_TO              where ntfy mails alerts (the ntfy server needs SMTP)
#   CULINA_ALERT_TELEGRAM_RELAY_URL    the relay's webhook, e.g. http://prometheus-bot:9087/alert/<chat id>
#   CULINA_API_SERVICE / CULINA_WEB_SERVICE   service names when OTEL_SERVICE_NAME is not culina-api
#
# Rules carry the label, dashboards the tag, managed_by=culina-repo. One of those with no file here
# is deleted by apply; one without it is never touched, even when its name matches. Neither is a
# locked dashboard: unlock it in SigNoz first.

set -euo pipefail

command -v jq >/dev/null || { echo "signoz-alerts: needs jq" >&2; exit 1; }

DIR="$(cd "$(dirname "$0")/../deploy/observability/signoz" && pwd)"
MANAGED=culina-repo

export CULINA_API_SERVICE="${CULINA_API_SERVICE:-culina-api}"
export CULINA_WEB_SERVICE="${CULINA_WEB_SERVICE:-culina-web}"

# ${VAR} in every string replaced from the environment, unset as empty.
FILL='walk(if type == "string" then gsub("\\$\\{(?<v>[A-Z0-9_]+)\\}"; $ENV[.v] // "") else . end)'

fail() { echo "signoz-alerts: $*" >&2; exit 1; }

check() {
  local problems=0 file

  for file in "$DIR"/defaults.json "$DIR"/routing.json "$DIR"/channels/*.json "$DIR"/rules/*.json "$DIR"/dashboards/*.json; do
    jq empty "$file" 2>/dev/null || { echo "  $file: not valid JSON" >&2; problems=1; }
  done
  [ "$problems" -eq 0 ] || fail "fix the JSON above first"

  local channels problems
  channels="$(jq -s 'map(.name)' "$DIR"/channels/*.json)"

  problems="$(
    jq -r --argjson channels "$channels" '
      to_entries[] | .key as $tier | .value[] | select(. as $c | $channels | index($c) | not)
      | "  routing.json: \($tier) names \(.), which no file in channels/ defines"' "$DIR/routing.json"

    for file in "$DIR"/rules/*.json; do
      jq -r --slurpfile routing "$DIR/routing.json" --arg file "${file##*/}" '
        [ (if (.alert // "") == "" then "no alert name" else empty end),
          (if (.alertType // "") == "" then "no alertType" else empty end),
          (if (.labels.area // "") == "" then "no area label" else empty end),
          (if (.labels.service // "") == "" then "no service label" else empty end),
          (if (.condition.thresholds.spec // []) == [] then "no thresholds" else empty end),
          (.condition.thresholds.spec // [] | .[].name | select($routing[0][.] == null)
            | "threshold \(.) has no entry in routing.json"),
          (.condition.thresholds.spec // [] | map(.name) | if . != sort then
            "list critical before warning: the first threshold silences the ones after it" else empty end),
          (.condition.selectedQueryName as $q | select($q != null)
            | if [.condition.compositeQuery.queries[].spec.name] | index($q) then empty
              else "selectedQueryName \($q) is not one of the queries" end)
        ] | .[] | "  \($file): \(.)"' "$file"
    done

    jq -s -r 'group_by(.alert)[] | select(length > 1) | "  two rules are named \(.[0].alert)"' "$DIR"/rules/*.json

    # What SigNoz checks on save, plus a panel it would accept and never show.
    for file in "$DIR"/dashboards/*.json; do
      jq -r --arg file "${file##*/}" '
        [ (if .name != ($file | rtrimstr(".json")) then "name must be the file name" else empty end),
          (if .name | test("^[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?$") | not then "name must be a lowercase DNS label" else empty end),
          (if .schemaVersion != "v6" then "schemaVersion must be v6" else empty end),
          (.spec.panels as $panels
            | [.spec.layouts[].spec.items[].content["$ref"] | ltrimstr("#/spec/panels/")] as $placed
            | ($panels | keys[] | select(. as $k | $placed | index($k) | not) | "panel \(.) is in no layout, so never shown"),
              ($placed | group_by(.)[] | select(length > 1) | "panel \(.[0]) is placed twice"),
              ($placed[] | select(. as $k | $panels | has($k) | not) | "layout refers to unknown panel \(.)")),
          (.spec.layouts[].spec.items[] | select(.x + .width > 12) | "an item is wider than the 12-column grid"),
          (.spec.panels | to_entries[] | select((.value.spec.queries | length) != (if .value.spec.plugin.kind == "signoz/TextPanel" then 0 else 1 end))
            | "panel \(.key) must have exactly one query")
        ] | unique | .[] | "  \($file): \(.)"' "$file" 2>/dev/null || echo "  ${file##*/}: could not be checked"
    done
    jq -s -r 'group_by(.spec.display.name)[] | select(length > 1) | "  two dashboards are titled \(.[0].spec.display.name)"' "$DIR"/dashboards/*.json
  )"

  if [ -n "$problems" ]; then
    echo "$problems" >&2
    fail "fix the above first"
  fi

  echo "signoz-alerts: $(ls "$DIR"/rules/*.json | wc -l | tr -d ' ') rules, $(ls "$DIR"/channels/*.json | wc -l | tr -d ' ') channels and $(ls "$DIR"/dashboards/*.json | wc -l | tr -d ' ') dashboards are valid"
}

api() { # METHOD PATH [BODY]
  local method="$1" path="$2" response status
  local data=()
  [ -z "${3:-}" ] || data=(--data-binary "$3")
  response="$(curl -sS -X "$method" "${SIGNOZ_URL%/}$path" \
    -H "SIGNOZ-API-KEY: $SIGNOZ_API_KEY" -H 'Content-Type: application/json' \
    "${data[@]+"${data[@]}"}" -w '\n%{http_code}')"
  status="${response##*$'\n'}"
  response="${response%$'\n'*}"
  case "$status" in
    2??) printf '%s' "$response" ;;
    *) echo "signoz-alerts: $method $path answered $status: $response" >&2; return 1 ;;
  esac
}

# The channel with the environment filled in, or nothing while a variable it needs is unset. An
# empty optional token drops the authorization rather than sending "Bearer ".
render_channel() {
  local var
  for var in $(grep -o '\${[A-Z0-9_]*}' "$1" | tr -d '${}' | sort -u); do
    case "$var" in *_TOKEN) ;; *) [ -n "${!var:-}" ] || return 0 ;; esac
  done
  jq -c "$FILL"' | del(.. | .http_config? | select(.authorization.credentials == ""))' "$1"
}

# A tier none of whose channels is configured is left out: SigNoz refuses a threshold that routes
# nowhere.
render_rule() { # FILE AVAILABLE_CHANNELS
  jq -c --slurpfile defaults "$DIR/defaults.json" --slurpfile routing "$DIR/routing.json" \
    --argjson available "$2" "$FILL"' | ($defaults[0] * .)
    | .condition.thresholds.spec |= (map(.channels = [$routing[0][.name][] | select(. as $c | $available | index($c))])
      | map(select(.channels != [])))' "$1"
}

sync() { # plan | apply
  local mode="$1"
  [ -n "${SIGNOZ_URL:-}" ] || fail "set SIGNOZ_URL, e.g. http://casaos.local:7890"
  [ -n "${SIGNOZ_API_KEY:-}" ] || fail "set SIGNOZ_API_KEY (SigNoz → Settings → API Keys)"
  check >/dev/null

  local existing_channels existing_rules available='[]' file body name id
  existing_channels="$(api GET /api/v1/channels | jq -c '.data // []')"
  existing_rules="$(api GET /api/v2/rules | jq -c '.data // []')"

  for file in "$DIR"/channels/*.json; do
    name="$(jq -r .name "$file")"
    body="$(render_channel "$file")"
    if [ -z "$body" ]; then
      echo "  skip    channel $name (set $(grep -o '\${[A-Z0-9_]*}' "$file" | tr -d '${}' | grep -v '_TOKEN$' | sort -u | paste -sd ' ' -))"
      continue
    fi
    available="$(jq -c --arg n "$name" '. + [$n]' <<<"$available")"
    id="$(jq -r --arg n "$name" 'map(select(.name == $n))[0].id // empty' <<<"$existing_channels")"
    if [ -n "$id" ]; then
      echo "  update  channel $name"
      [ "$mode" = plan ] || api PUT "/api/v1/channels/$id" "$body" >/dev/null
    else
      echo "  create  channel $name"
      [ "$mode" = plan ] || api POST /api/v1/channels "$body" >/dev/null
    fi
  done

  # SigNoz refuses a threshold that routes nowhere, so without a channel no rule can be created.
  if [ "$available" = '[]' ]; then
    [ "$mode" = plan ] || fail "SigNoz refuses rules that notify nobody: set the variables of at least one channel"
    echo "signoz-alerts: no channel is configured; apply will refuse until one is" >&2
  fi

  local wanted='[]'
  for file in "$DIR"/rules/*.json; do
    body="$(render_rule "$file" "$available")"
    name="$(jq -r .alert <<<"$body")"
    wanted="$(jq -c --arg n "$name" '. + [$n]' <<<"$wanted")"
    id="$(jq -r --arg n "$name" 'map(select(.alert == $n))[0] | select(. != null)
      | if .labels.managed_by == "'"$MANAGED"'" then .id else "unmanaged" end' <<<"$existing_rules")"
    if jq -e '.condition.thresholds.spec == []' <<<"$body" >/dev/null; then
      echo "  skip    rule \"$name\": no channel of its tiers is configured"
      continue
    fi
    case "$id" in
      unmanaged) echo "  keep    rule \"$name\": made by hand in SigNoz; rename or delete it there to manage it here" ;;
      "")
        echo "  create  rule \"$name\""
        [ "$mode" = plan ] || api POST /api/v2/rules "$body" >/dev/null ;;
      *)
        echo "  update  rule \"$name\""
        [ "$mode" = plan ] || api PUT "/api/v2/rules/$id" "$body" >/dev/null ;;
    esac
    jq -r --slurpfile file "$file" '($file[0].condition.thresholds.spec | map(.name)) - (.condition.thresholds.spec | map(.name))
      | .[] | "          without its \(.) tier: no channel of it is configured"' <<<"$body"
  done

  jq -r --argjson wanted "$wanted" '.[] | select(.labels.managed_by == "'"$MANAGED"'")
    | select(.alert as $a | $wanted | index($a) | not) | "\(.id)\t\(.alert)"' <<<"$existing_rules" |
    while IFS=$'\t' read -r id name; do
      echo "  delete  rule \"$name\" (no file for it any more)"
      [ "$mode" = plan ] || api DELETE "/api/v2/rules/$id" >/dev/null
    done

  # Dashboards are matched by name, which SigNoz never lets change; PUT replaces the whole of one.
  local existing_dashboards found
  existing_dashboards="$(api GET '/api/v2/dashboards?limit=200' | jq -c '.data.dashboards // []')"
  wanted='[]'
  for file in "$DIR"/dashboards/*.json; do
    body="$(jq -c --arg managed "$MANAGED" "$FILL"' | .tags = ([.tags[]? | select(.key != "managed_by")] + [{key: "managed_by", value: $managed}])' "$file")"
    name="$(jq -r .name <<<"$body")"
    wanted="$(jq -c --arg n "$name" '. + [$n]' <<<"$wanted")"
    found="$(jq -c --arg n "$name" 'map(select(.name == $n))[0] // empty' <<<"$existing_dashboards")"
    if [ -z "$found" ]; then
      echo "  create  dashboard $name"
      [ "$mode" = plan ] || api POST /api/v2/dashboards "$body" >/dev/null
    elif ! jq -e '.tags // [] | any(.key == "managed_by" and .value == "'"$MANAGED"'")' <<<"$found" >/dev/null; then
      echo "  keep    dashboard $name: made by hand in SigNoz; delete it there to manage it here"
    elif jq -e '.locked' <<<"$found" >/dev/null; then
      echo "  keep    dashboard $name: locked in SigNoz; unlock it to update it"
    else
      echo "  update  dashboard $name"
      [ "$mode" = plan ] || api PUT "/api/v2/dashboards/$(jq -r .id <<<"$found")" "$body" >/dev/null
    fi
  done

  jq -r --argjson wanted "$wanted" '.[] | select(.tags // [] | any(.key == "managed_by" and .value == "'"$MANAGED"'"))
    | select(.name as $n | $wanted | index($n) | not) | "\(.id)\t\(.name)\t\(.locked)"' <<<"$existing_dashboards" |
    while IFS=$'\t' read -r id name locked; do
      if [ "$locked" = true ]; then
        echo "  keep    dashboard $name: no file for it any more, but locked in SigNoz"
        continue
      fi
      echo "  delete  dashboard $name (no file for it any more)"
      [ "$mode" = plan ] || api DELETE "/api/v2/dashboards/$id" >/dev/null
    done
}

case "${1:-}" in
  check) check ;;
  plan | apply) sync "$1" ;;
  *) sed -n '3,8p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 2 ;;
esac
