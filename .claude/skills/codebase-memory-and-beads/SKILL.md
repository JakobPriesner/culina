---
name: codebase-memory-and-beads
description: The working loop for culina-v2 — query the codebase-memory MCP graph before exploring or changing code, and track every piece of work as a bead (bd) issue rather than an ad-hoc note. Use at the start of any non-trivial task, before refactoring, when following up on discovered work, and when closing out a session.
---

# codebase-memory and beads

Two tools, two jobs: **codebase-memory answers "what is there and what touches
it"; beads answers "what needs doing and in what order".** Use both by default
on anything larger than a one-line change.

## codebase-memory (MCP) — before you read or change code

The graph is faster and more complete than grepping, and it is the only way to
answer impact questions honestly.

Order of operations:

1. `list_projects` — is this repo indexed? If not, `index_repository` once.
   (After a large external change, re-index or `detect_changes`.)
2. `get_architecture` for orientation in an unfamiliar area.
3. `search_graph` to find a symbol; `get_code_snippet` for its exact source.
4. `trace_path` for callers and callees — **always run this before changing or
   deleting anything**. "Nothing calls this" is a claim that needs the graph,
   not a hunch.
5. `query_graph` for multi-hop questions (what crosses a layer boundary, what
   an endpoint eventually touches).
6. `check_index_coverage` for every file a conclusion rests on. If coverage is
   incomplete, read those lines directly and say so — coverage is best-effort
   and is never proof of completeness.

Use plain `grep`/`Read` for literals, configuration, SQL, Markdown, and to
verify anything the graph told you. Graph first, grep to confirm; never state a
negative ("there are no other usages") from the graph alone.

The layering rules in `dotnet-project-setup` make impact questions cheap: ask
the graph before assuming a change is local.

## beads (`bd`) — before you start and before you stop

Every unit of work is a bead. A bead survives a context window, a session, and
a handoff; a note in a reply does not.

```bash
bd ready                      # claimable work, blockers resolved
bd show <id>                  # the full issue before starting
bd create "Add ETag to GET /recipes/{id}" -d "..." -p 1
bd update <id> --status in_progress
bd note <id> "Found that CsrfMiddleware also needs the session version"
bd link <new-id> blocks <other-id>
bd close <id>
```

The loop:

1. **Start**: `bd ready`, pick the issue, `bd show` it, set it `in_progress`.
   Work without a bead only for a trivial, self-contained fix.
2. **During**: when you discover work that is out of scope — a stale doc, a
   missing test, a TODO worth doing, a second call site that also needs the
   fix — `bd create` it immediately with `--deps discovered-from:<current>`
   and keep going. Discovered work is filed, never silently absorbed and never
   quietly dropped.
3. **Dependencies**: `bd link a blocks b` whenever one piece must land first.
   That is what makes `bd ready` trustworthy, and it is the reason to use beads
   rather than a checklist.
4. **Notes**: record decisions and dead ends on the bead (`bd note`), not only
   in chat. The next session reads the bead.
5. **Close**: close only when the work is actually done and verified — tests
   run, gates green. If something is left, it is a new bead, named.

Writing a good bead: the title is an imperative phrase; the description says
what and why and how to know it is done; anything a fresh session would need
(file paths, error codes, the failing command) is in the body, because the
reader will not have this conversation.

Do not: commit or push as part of closing a bead unless the repository has
explicitly opted into that; batch a session's worth of discoveries into one
vague bead; or close a bead to tidy the list when the work is not done.

## Together

Before a change: graph for impact → bead for scope. After a change: graph to
confirm nothing else was touched → bead closed, follow-ups filed.

## Checklist

- [ ] Repo indexed; structural questions answered from the graph, verified in
      the source.
- [ ] `trace_path` run before deleting, renaming, or changing a signature.
- [ ] `check_index_coverage` run for files a conclusion depends on; gaps stated.
- [ ] Work claimed from `bd ready` and set `in_progress`.
- [ ] Every discovery filed as a bead with `discovered-from`, linked with
      `blocks` where order matters.
- [ ] Beads closed only when verified; leftovers filed as new beads.
