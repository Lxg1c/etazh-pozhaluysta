---
name: agents-md-guard
description: Validate proposed reusable project conventions before writing them to docs/rules/. Use when the user or agent wants to record a new rule, convention, or lesson.
---

# AGENTS.md Guard

## Validate the rule

Read the proposal and identify the closest topic from the routing table in the project’s `AGENTS.md`. Read that topic file if it exists, plus any directly relevant existing skill or rule needed for the duplication check. Ask the user to choose the topic when the boundaries in the table do not resolve it.

Apply all checks before proposing a write:

- The rule begins with an imperative such as “Use,” “Do not,” “Always,” or “Never,” or an equivalent imperative in the language of the project rules.
- The rule includes a one-sentence reason that explains when or why compliance matters.
- The rule is reusable across future projects where the topic applies. Keep one-off implementation details, asset specifications, and decisions about the current game in its design documentation.
- The rule adds something distinct from the existing project rules and applicable skills. Look for semantic duplication, including paraphrases of established architecture or Unity guidance.

If a check fails, reject the candidate, identify the failed check, and explain what would make the rule reusable or useful. Offer a revised candidate when the user’s intent is clear.

## Route and record

Use the exact topic destinations below. Choose one primary topic and place the rule under the matching section, or add a short section if necessary.

| Topic | Destination |
| --- | --- |
| Sprites, pixel art, Aseprite, and 2D import | `docs/rules/2d-art.md` |
| 3D models, shaders, and lighting | `docs/rules/3d-art.md` |
| HUD, menus, and interface | `docs/rules/ui-ux.md` |
| Music, sound, and audio buses | `docs/rules/audio.md` |
| Code structure, assembly definitions, and architecture | `docs/rules/architecture.md` |
| Tests, EditMode, PlayMode, and manual checks | `docs/rules/testing.md` |
| Workflow, review, and rule conventions | `docs/rules/workflow.md` |

Show the exact proposed wording and destination to the user. Wait for confirmation of that wording and destination before writing. Reuse an existing confirmation that covers both.

Preview the insertion and count all resulting lines. If it would put the topic above 50 lines, propose a specific less relevant rule to archive in a separate Legacy file or remove, and wait for the user's decision. Archive or remove an existing rule only with authorization for that change.

After confirmation, append the approved rule in its topic file. Read the result back and verify that the rule is present once, the file has at most 50 lines, and the write matches the approved scope.
