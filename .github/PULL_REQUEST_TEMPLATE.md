## Summary

Brief description of what this PR does.

## Type of Change

- [ ] New agent
- [ ] New skill
- [ ] New hook or rule
- [ ] Bug fix
- [ ] Documentation improvement
- [ ] Other:

## Change traceability (game project)

> Required for any PR that touches a game path (`backend/`, `contracts/`,
> `design/`, `docs/architecture/`, `production/`). Framework-only PRs may
> write "not applicable — framework only" in any of these fields.
>
> Checked by `.github/scripts/check-traceability.sh`. Placeholder values
> ("TBD", "n/a", "-") count as missing. The FR/TC field needs an FR-, NFR-
> or TR- id; a TC id on its own does not trace to a requirement.

**Ticket:**

**FR/TC:**

**Changes:**

**Evidence:**

**Reviewer:**

## Changes

-
-
-

## Checklist

- [ ] I've tested this in a Claude Code session
- [ ] New agents include the Collaboration Protocol section
- [ ] New skills use the subdirectory format (`.claude/skills/<name>/SKILL.md`)
- [ ] Reference docs are updated (agent-roster, skills-reference, hooks-reference, rules-reference)
- [ ] Hooks use `grep -E` (POSIX) and fail gracefully without jq/python
- [ ] No hardcoded paths or platform-specific assumptions
