# Skill Tree Workflow Integration

Status: installed generic workflows from Skill Tree source commit `e8448615cc853ec58f8b1fd7df05732b60e650a0`. Companion roadmap execution and product implementation require separate task selection.

## Dependency mapping during decomposition

Process Pending accepts a lowercase payload-local `key` for new or reused tasks. `depends_on` accepts existing T- IDs and references such as `@storage`, including forward references. Processing resolves stable IDs for the entire batch and rejects unknown/duplicate keys, missing dependencies and cycles before saving queue, index or checkpoint changes. The receipt returns `task_keys`; persisted task headers contain T- IDs only.

Companion E02–E07 decomposition can express dependencies between newly split tasks and existing MCP contracts. Existing Pending epic gates stay unresolved until researched tasks, required edges and completion evidence exist. No processing or implementation is started by installation.

## Manifest and index validation

The target manifest catalogs all eight repository-local installed generic Skill Tree packages. Repository validation checks JSON structure, repository-relative paths, canonical source-digest coverage/freshness, installed entrypoints/resources, task-index agreement, queue hashes, required dependencies/cycles and active-run identities. Validation reads persisted state without refreshing or replacing checkpoints. Stale audit coverage produces a warning; recorded historical verdicts remain unchanged.

## Located prose diagnostics

Diagnostics report rule, severity, file, line, column and offending source excerpt. Definite language/naming/personal-pronoun/historical-narrative violations are errors; ambiguous demonstratives and unquoted title-case labels are review warnings. Quoted labels, code and technical identifiers retain exact syntax. Relative/complement clauses containing `that` and current-purpose wording such as `may be used to` remain valid.

Default lint fails on errors; `--warnings-as-errors` and strict repository validation also fail on warnings. Existing current-state prose cleanup remains deferred in Backlog; installing the validator does not rewrite product contracts.

## Scoped snapshots

The snapshot helper accepts repeated `--include` file/directory selections, `--member-style dotted`, `--output-dir root`, explicit output paths and timestamps. Directory scopes select relevant tracked Markdown; an explicitly selected new file may be untracked. Overlapping selections deduplicate; missing/excluded/external selections reject the whole request before archive creation. Live documents remain unchanged and member notices identify source paths.

The default helper uses kebab-case members and ignored artifacts. [documents-snapshot.md](documents-snapshot.md) owns the Companion root-output and dotted-member contract. Snapshot creation still requires an explicit developer request.

## Validation boundaries

Source qualification passes 79 Python and 24 Node behavioral/installation/provider tests, synchronization, strict source validation and installer dry runs. The ancillary quick validator lacks PyYAML; strict source validation checks package frontmatter and resource integrity.

Companion integration checks installed package contents, planning metadata consistency, scoped prose and snapshot dry-run selection. Application builds/tests, deployments, live Habitica calls, OAuth compatibility and mobile usability remain outside workflow-helper qualification.
