# Documents Snapshot Flow

Last updated: 2026-06-08
Primary audience: AI agents creating dated documentation snapshot archives

## Trigger

Use this flow only when the user explicitly asks for a document, documentation, markdown, or `.md` snapshot archive.

Do not run this flow for ordinary documentation edits.

## Goal

Create a zip archive at the repository root that contains dated snapshot copies of project documentation.

The archive must help future AI agents understand:

- each file is a snapshot, not live source;
- the original source path is still the authoritative location;
- the live document and implementation may drift after the snapshot date;
- the snapshot is reliable for the captured repository state.

Never edit source documents while creating snapshot copies.

## Snapshot Timestamp

Generate the snapshot timestamp from the current local date and time when the user requests the snapshot.

Format:

```text
YYYY-MM-DD-HH-MM
```

Example command:

```bash
date '+%Y-%m-%d-%H-%M'
```

Do not hard-code example dates from this document. The timestamp must match the actual snapshot creation time.

## Source Selection

The user may choose what files go into the snapshot.

Default scope:

- include all tracked project markdown documents;
- exclude documents that are not project-related, such as dependency, SDK, build-output, and vendor docs.

Use tracked project markdown files as the default all-documents source set:

```bash
git ls-files '*.md'
```

Explicit user scope overrides the default.

Supported explicit scopes:

- one named markdown file;
- several named markdown files;
- one directory of markdown files;
- a glob-like description, such as "only docs under `docs/`";
- a newly edited source markdown file from the same request.

When the user asks to snapshot only one file, create a zip archive that contains only that file's snapshot copy.

If the same user request also creates or edits a source markdown document and asks to snapshot that file, include that intentional source document even before it is committed. Do not add unrelated markdown files in this case.

If the user's requested scope is ambiguous, make the narrowest reasonable interpretation from the request and report the chosen scope.

Include a file only when it is relevant to this repository, such as:

- product or feature documentation;
- architecture, stack, storage, sync, deployment, or testing documentation;
- Habitica API behavior or integration rules;
- UI/UX rules tied to this app;
- implementation plans or specs for this app;
- AI-agent workflow rules for this repository.

Exclude ignored dependency, SDK, build-output, and vendor documents unless the user explicitly asks for them. Common excluded examples:

- `node_modules/**`
- `dotnet/sdk/**`
- `obj/**`
- generated package documentation

If a tracked markdown file is not clearly repository-specific, exclude it and report why.

## Required Relevance Check

Before creating the archive, inspect the selected candidate documents enough to confirm they contain real codebase-related information.

Recommended checks:

```bash
git ls-files '*.md'
rg -n "src/Habitica|tests/Habitica|Habitica\\.|Cloudflare|wrangler|Blazor|Razor|Dapper|LiteDB|IndexedDB|Habitica API|AI-agent|workflow|deployment|feature|architecture" *.md docs -g '*.md'
```

Also inspect headings when needed:

```bash
git ls-files '*.md' | while IFS= read -r file; do
  printf '%s\n' "$file"
  rg -n '^#{1,3} ' "$file" | sed -n '1,12p'
done
```

Do not claim relevance that was not checked.

For explicit single-file snapshots, inspect that file directly. It is acceptable for the file to document this snapshot workflow itself; that is repository-specific AI-agent workflow information.

## Archive Naming

Use the user's requested archive name when provided.

If no name is provided, use:

```text
habitica-tool-documents-snapshot-YYYY-MM-DD-HH-MM.zip
```

If the user asks for a single-file snapshot and does not provide an archive name, prefer:

```text
habitica-tool-document-snapshot-<source-file-stem>-YYYY-MM-DD-HH-MM.zip
```

Place the zip file at repository root.

## Member File Naming

Do not preserve folder structure inside the zip. Every snapshot document must be at the zip root.

For each source file, use:

```text
<original-file-name-without-.md>.snapshot-YYYY-MM-DD-HH-MM.md
```

Example:

```text
README.md -> README.snapshot-2026-06-08-13-23.md
docs/UX_UI_MANIFEST.md -> UX_UI_MANIFEST.snapshot-2026-06-08-13-23.md
docs/DOCUMENTS_SNAPSHOT.md -> DOCUMENTS_SNAPSHOT.snapshot-2026-06-08-13-23.md
```

If two files would produce the same archive member name, do not overwrite either file. Use a deterministic flattened relative path for the colliding files:

```text
docs__nested__example.snapshot-YYYY-MM-DD-HH-MM.md
```

Report any collision handling in the final response.

## Snapshot Notice

Prepend this HTML comment to every copied markdown file:

```markdown
<!--
SNAPSHOT DOCUMENT
Snapshot date: YYYY-MM-DD
Snapshot time: HH-MM
Original source path: path/from/repository/root.md
Archive file name: NAME.snapshot-YYYY-MM-DD-HH-MM.md
Relevance check: Included as Habitica-tool project documentation after confirming the source document contains repository-specific product, architecture, API, deployment, UI, testing, implementation-plan, or AI-agent workflow information.
Snapshot warning: This is a dated snapshot copy for AI-agent context. The live repository document and implementation may drift after archive creation. Treat this snapshot as reliable for the captured state, but check the live source path and current codebase before changing behavior.
-->
```

Keep the original markdown content after one blank line.

## Creation Steps

1. Check worktree status.
2. Determine source scope from the user request.
3. Gather selected candidate markdown files. Use all tracked project markdown files only when the user did not request a narrower scope.
4. Exclude ignored dependency, SDK, build-output, and vendor documents unless explicitly requested.
5. If the user selected one file, keep the candidate list to that one file.
6. If the user selected a newly created or edited markdown file from the same request, include it even if it is not tracked yet.
7. Inspect candidates for repository relevance.
8. Create a temporary staging directory outside the repository, such as `/private/tmp/habitica-tool-documents-snapshot-YYYY-MM-DD-HH-MM`.
9. Copy each selected source markdown file into staging with the snapshot filename.
10. Prepend the snapshot notice to each staged copy.
11. Zip only staged markdown files from inside the staging directory, so archive entries are root-only.
12. Verify archive entries with `unzip -l`.
13. Verify at least one file header with `unzip -p`.
14. Check `git status --short`.
15. Report included count, selected scope, exclusions, archive path, and source-doc status.

## Verification Requirements

After creating the archive, verify:

- archive exists at repository root;
- member files are at zip root, with no preserved source folders;
- every member filename has `.snapshot-YYYY-MM-DD-HH-MM.md`;
- every member starts with `SNAPSHOT DOCUMENT`;
- single-file snapshot requests produce a zip with exactly one markdown member;
- source markdown files were not edited by the snapshot-copy operation;
- `git status --short` shows the new or updated zip and any intentional source documentation changes only.

## Final Response Requirements

Report:

- zip path;
- number of included markdown files;
- selected source scope;
- whether ignored/vendor/toolchain markdown files were excluded;
- whether source markdown documents were untouched by snapshot-copy generation;
- intentional source documentation changes, if the same request included them;
- any collision handling;
- build and test instructions required by project handoff rules;
- suggested commit message when files changed.

Do not state that build or tests were run unless the user explicitly asked for them and they were actually run.
