# Skill Tree Workflow Proposals

Status: upstream proposals only; no Skill Tree source changes or Companion implementation selection.

Evidence baseline: Skill Tree revision `0f91954`, including the structural refactor `91f0bd1`, next-action handoff `973084b`, evidence-based limits `e8ae9a9`, operation classification `6ac4399`, and declarative-document contract `0f91954`.

## Explicit migration and provenance

The current metadata contract requires explicit setup and provides no conversion helper. Existing Companion contracts require stable IDs, integer priorities, imported Pending provenance, blocking statuses, path relocation and dependency mapping. Add an explicit dry-run/apply migration command with a source-hash guard, an identity map, checkpoint preservation and a compact import receipt. Migration must never assert Process Pending research, accepted product decisions or completed task outcomes.

## Dependency mapping during decomposition

`skill-tree/common/scripts/workflow-state.py` assigns new T- IDs inside `process`; `depends_on` accepts existing T- IDs only. Companion E02–E07 decomposition needs dependencies between newly split tasks and existing MCP contracts. Add payload-local task keys resolved atomically into generated IDs, plus explicit linking from a retained Pending scope gate to promoted required tasks. Validate dangling references and cycles before saving. Keep unresolved epic gates blocked until mapping and required evidence exist.

## Target manifest and index validation

`validate-repository-documentation.py` checks required file existence and task structure. The validator does not parse or compare the target manifest and `future.json`. The target manifest also emits an empty installed-skill catalog. Extend validation to check JSON schemas, source hashes, index agreement, installed workflow entrypoints, dependency closure and preserved active state. Populate installed-skill navigation without requiring the Skill Tree source checkout. Companion agents need a trustworthy document map and exact task identity lookup.

## Prose lint with useful diagnostics

`validate-document-rules.py` rejects every occurrence of words such as `that`, `this` and `my`; the history pattern also flags legitimate `may be used to` wording. Product names such as `My Day` require escaping for lint despite exact naming. Add line/column locations, separate warning/error categories, quoted-label handling and an explicit scoped legacy cleanup mode. Keep English and declarative-contract requirements; avoid forcing broad wording changes during a structural migration.

## Snapshot scope and local contracts

The Companion snapshot contract supports one file, selected files and directories, a root output archive, and `.snapshot-YYYY-MM-DD-HH-MM.md` members. The refreshed generic helper accepts a repository target and optional output only, emits kebab-case snapshot members, and defaults to ignored artifacts. Add explicit include/scope and naming/output-policy options so the helper can honor repository-owned contracts. Preserve explicit-request-only snapshot creation and avoid including unrelated workflow packages or evidence.

## Validation boundaries

The migration checks structure, links, index consistency, installed package contents and selected source claims. Application builds, application tests, deployment, live Habitica calls, OAuth compatibility and hands-on competitor/mobile usability remain outside the migration verdict. Current-state prose cleanup remains a deferred Companion Backlog item; upstream proposals remain reference material.
