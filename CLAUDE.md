# Project Instructions for AI Agents

This file provides instructions and context for AI coding agents working on this project.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:6cd5cc61 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Track work in `bd`, not TodoWrite, TaskCreate, or markdown TODO lists, so it survives across sessions and handoffs
- Run `bd prime` for detailed command reference and session close protocol
- Keep persistent knowledge in `bd remember`, not MEMORY.md files, which fragment across accounts

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/SYNC_CONCEPTS.md for details and anti-patterns.

## Git and sync policy

Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Report git state** - Run `git status` and propose the commit/push commands; wait for approval
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step; if one is blocked, report the exact command and error
<!-- END BEADS INTEGRATION -->

## Conventions & Patterns

- **Never publish source maps.** No `.map` files in the frontend build output,
  the container image, or anything the server serves, and no `sourceMappingURL`
  pointing at one. Reported web app stacks stay minified; don't propose making
  them readable by shipping maps.
