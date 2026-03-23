---
description: Break down plan objectives into agent-sized GitHub issues
on:
  workflow_dispatch:
    inputs:
      plan:
        description: "Path to the plan document (e.g. doc/plans/0001-initial.md)"
        required: true
        default: "doc/plans/0001-initial.md"
      objective:
        description: "Objective number to break down (e.g. 0, 1, 2)"
        required: true
permissions:
  contents: read
  issues: read
tools:
  github:
    toolsets: [issues]
safe-outputs:
  create-issue:
    max: 20
  add-comment:
    max: 5
  update-issue:
    max: 20
---

# Break Down Plan Objectives into GitHub Issues

You are a project planning agent. Your job is to read a plan document from this repository and decompose a specific objective into small, focused GitHub issues that a coding agent can complete autonomously in roughly 15 minutes each.

## Inputs

- **Plan document**: `${{ inputs.plan }}`
- **Objective number**: `${{ inputs.objective }}`

## Task Writing Skill

Read the file `.skills/write-plan-issue.md` in this repository for the issue template and rules. Follow its structure exactly when creating issues. Every issue must have: Title, Context (linking the plan and objective), Task (numbered steps), Acceptance criteria (checkboxes), Scope boundaries, and Files likely to change.

## Steps

1. **Read the plan**: Open and read `${{ inputs.plan }}` from the repository. Identify the objective specified by number `${{ inputs.objective }}`.

2. **Read the skill**: Open and read `.skills/write-plan-issue.md` for the issue writing template and rules.

3. **Read AGENTS.md**: Open and read `AGENTS.md` for build/test conventions and project context.

4. **Decompose the objective**: Break the objective into the smallest set of sequential sub-tasks. Each sub-task should be completable by an agent in ~15 minutes. Order them by dependency (each may depend on the one before it).

5. **Create a tracking issue**: Create a single parent tracking issue for this objective.
   - **Title**: `[Plan <number> / Obj ${{ inputs.objective }}] <objective title>` — where `<number>` is extracted from the plan file name and `<objective title>` is the objective's heading from the plan.
   - **Labels**: Add the label `planned`.
   - **Body**: Include the objective description from the plan, a link to `${{ inputs.plan }}`, and a note that child issues will be linked as sub-issues.

6. **Create child issues**: For each sub-task, create a GitHub issue following the skill template. Extract the plan number from the plan file name (e.g. `0001` from `doc/plans/0001-initial.md`).
   - **Title**: `[Plan <number> / Obj ${{ inputs.objective }}] <verb> <what>` — short, specific, searchable, where `<number>` is extracted from the plan file name.
   - **Labels**: Add the label `planned` to every issue.
   - **Body**: Follow the `.skills/write-plan-issue.md` template exactly:
     - **Context**: Link to the plan document and objective. Reference the tracking issue. Summarize what prior objectives accomplished.
     - **Task**: Numbered, concrete steps.
     - **Acceptance criteria**: Checkboxes per observable outcome, always including `npm test` passing.
     - **Scope boundaries**: Explicitly state what is in scope and out of scope.
     - **Files likely to change**: List specific file paths.
   - **Dependencies**: If an issue depends on a previous one, add a line in the Context section: `Depends on #<issue-number>` referencing the prior issue's number.
   - **Sub-issues**: After creating each child issue, add it as a sub-issue of the tracking issue using GitHub's sub-issues feature. Use the GitHub API to add the sub-issue relationship so it appears in the tracking issue's sub-issue list in the GitHub UI.

7. **Assign copilot to the first child issue**: After creating all child issues and linking them as sub-issues, add a comment on the very first child issue (the one with no dependencies) that says exactly: `@copilot` — this assigns copilot to work on it automatically. Do NOT assign copilot to the tracking issue.

8. **Summary comment**: Post a summary comment on the tracking issue listing all created child issues in dependency order with their issue numbers. Do this as the final step so the tracking issue has a complete overview.

## Important Rules

- Always create a tracking issue first, then create child issues and link them as sub-issues of the tracking issue using GitHub's sub-issues feature.
- Create child issues in dependency order (first child issue has no dependencies, subsequent ones depend on prior ones).
- Keep each child issue small and focused. One objective may produce 2–10 child issues.
- Always label every issue (tracking and child) with `planned`.
- Always reference the plan document path and objective number in every issue.
- Assign copilot to the first child issue only — not the tracking issue.
- Do not create issues for objectives other than the one specified.
- Use the exact verification command from AGENTS.md (typically `npm test`).
