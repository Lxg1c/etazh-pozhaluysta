---
name: game-production-pipeline
description: Assess the current stage of a game project, plan milestones from pitch to launch, or identify the next deliverable and readiness check after a pitch, GDD, prototype, or build. Apply to any game engine and use Unity-specific testing guidance when relevant.
---

# Game Production Pipeline

Use the five stages below for project planning and readiness reviews. Keep Prototype, Vertical Slice, Alpha, Beta, and Gold/Launch distinct when mapping milestones. The prototype can be organized at the end of Concept or the beginning of Pre-Production, depending on the project process. Follow agreed checkpoints. If the user explicitly authorizes proceeding with an unmet checkpoint, record the exception and its impact.

## Determine the current stage

Read the relevant instructions in the project's `AGENTS.md`. Look for the pitch, GDD, technical design, current production plan, and evidence such as builds, playtest notes, and test results. A planned artifact or a file name alone does not prove a checkpoint has passed.

When asked what comes next, give the current stage, the next concrete deliverable, and the check that will show it is ready. Distinguish confirmed evidence from pending checks. If a source is missing, state the gap and use clearly labeled assumptions for planning when the available materials are sufficient.

## 1. Concept and Pitch

- **Purpose.** Describe the intended experience, audience, core loop, and hook, and identify the main risk in that loop.
- **Artifacts.** Pitch in `design/gdd/game-concept.md` or the existing equivalent. Use a small playable prototype to test the core loop before committing to production. Draft the rules needed for the prototype in the GDD when the project process calls for it.
- **Readiness.** The pitch states what players do and why they want to repeat it. Where the core loop is unproven, a playtest of the prototype shows whether to develop it further or revise it. Set a check specific to the game, such as finishing one complete shift and explaining a decision worth changing on a replay.

## 2. Pre-Production and Vertical Slice

- **Purpose.** Define the game's rules and technical approach and demonstrate that the team can deliver the target quality.
- **Artifacts.** A GDD or equivalent in `design/gdd/game-design.md` and a technical design in `design/tech/technical-design.md` or the project's existing equivalent. Cover relevant mechanics, edge cases, balance, UI, audio, and narrative where applicable. Record engine, architecture, versions and dependencies, and the asset pipeline.
- **Vertical slice.** Build one representative, complete segment with target-quality gameplay, art, audio, and UI. Size the slice by the game's natural unit of play, such as one shift or one level. Use it to validate the pipeline as well as the player experience.
- **Readiness.** The slice plays from start to finish in a standalone build, meets the agreed quality checks, and demonstrates a repeatable workflow for producing the rest of the game. Review the slice before committing to the full production scope.

## 3. Production and Alpha (Feature Complete)

- **Purpose.** Implement and connect every feature in the agreed release scope.
- **Artifacts.** An Alpha build and evidence that the complete game flow works. Use short iterations and prefer getting the entire flow working early. Placeholder content can fill any remaining slots.
- **Readiness.** Each required system has a working entry point, required systems work together, and the supported playthrough can be completed. Record remaining content and defects with owners and priorities when known.

## 4. Stabilization and Beta (Content Complete)

- **Purpose.** Integrate final content, tune the experience, and stabilize the build.
- **Artifacts.** Final game assets and data, localization where applicable, regression results, performance measurements, and external playtest findings about clarity, pacing, and difficulty.
- **Readiness.** All required content is in the build and the release scope is frozen. No known defect blocks progression. Test the whole game flow and performance on the supported configurations. After Beta, limit changes to release-blocking fixes and other agreed stabilization work.

## 5. Gold, Launch, and Post-Release

- **Purpose.** Release a verified build and prepare to support it.
- **Artifacts.** A versioned release candidate, test evidence, the Gold build, and the applicable distribution or certification materials. Plan triage and hotfix verification. Include DLC or live operations only if they are in scope.
- **Gold readiness.** The release candidate passes the acceptance checklist and channel checks, including certification when required. Archive the verified build and its version.
- **Launch verification.** Confirm that players receive the verified version from the distribution channel and can install or unpack it, launch it, and complete the critical game flow. Publishing and other external actions require the corresponding authorization.

## Write a production plan

1. Inventory the systems and subsystems supported by the pitch or GDD. Mark them **Core** for essential play, **Support** for enabling features, or **Polish** for presentation refinements. Identify dependencies and list unresolved features as design questions or explicit assumptions.
2. Map systems to Prototype → Vertical Slice → Alpha → Beta → Gold/Launch. For each milestone, name its deliverable, included and excluded scope, dependencies, and observable exit criteria. Use testable outcomes to establish readiness. Provide calendar dates only when the user requests scheduling.
3. Assign milestone tests. For Unity, use EditMode for rules, timers, formulas, and data validation. Use PlayMode for scene and UI integration or real adapter lifecycles. Use manual playtests to evaluate feel, tempo, and readability. Verify the actual release build at Gold. Use equivalent tests for other engines.
4. Record the main technical, art, and production risks that apply to each milestone. For each risk, give a mitigation and the evidence that will determine whether it is resolved.
5. When asked to create or update the plan, use `design/production-plan.md` unless the user or project identifies a different path. Read the existing plan before updating it and preserve agreed scope and decisions. A request for guidance about the next stage can be answered directly from the existing artifacts.

## Unity references

Read the relevant project reference when applying this workflow to Unity.

- [Architecture and technologies](../../../unity-architecture.md) for a technical design or assembly boundaries.
- [Testing and acceptance](../../../unity-testing.md) for the milestone test plan.
- [C# style and review](../../../csharp-code-review.md) for implementation and review criteria.
