# Skill: Write a GitHub Issue for a Plan Task

Break down a plan objective into a focused GitHub issue that an agent can complete in ~15 minutes.

## Issue structure

```markdown
## Title
<verb> <what> — short, specific, searchable

## Context
- Link to the plan: `doc/plans/0001-making-ylmish-functional.md`, Objective N
- One sentence on why this task exists (the goal it serves)
- What has already been done (prior objectives, existing code)

## Task
Numbered steps. Each step is a concrete action:
1. Do X in file Y
2. Add tests for Z
3. Run `npm test` and confirm all pass

## Acceptance criteria
- [ ] Checkbox per observable outcome
- [ ] Tests pass: `npm test`
- [ ] No regressions in existing tests

## Scope boundaries
- **In scope**: only what's listed above
- **Out of scope / defer**: anything adjacent that belongs to a different objective

## Files likely to change
- `path/to/file.fs`
```

## Rules

1. **One objective, one issue.** If an objective is too large for ~15 min, split it into sub-issues and list them in a parent tracking issue.
2. **Link back.** Every issue must reference the plan document and objective number.
3. **Summarise prior work.** State which objectives are already complete so the agent has context without re-reading the whole plan.
4. **Be explicit about scope boundaries.** Name things to defer—agents will otherwise expand scope.
5. **Include the verification command.** Always tell the agent how to confirm success (e.g. `npm test`, `dotnet build`).
6. **Prefer concrete file paths** over vague descriptions. If you know which files change, list them.
7. **Keep it short.** An agent doesn't need motivation or background essays—it needs steps and criteria.
