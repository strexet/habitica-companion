# Exact task adoption

Status: implemented locally; hosted accounts, installed agent packages and physical devices remain unqualified.

Owner: shared/adoption-tools.ts, Workspace adoption-api.ts, Auth projection.ts, MCP adoption-tools.ts and WorkspaceCloudClient. No credentials enter Workspace or public tool arguments.

## Shared contract

Browser POST /adoption-api and the three MCP tools use one whitelist and service. prepare_task_adoption accepts requestId, exact taskId, exact habiticaTaskId and optional expectedRevision. Account selectors, task names and aliases are refused. Preparation reads the exact canonical task, including archived tasks, and performs an owned native GET through Auth inspectAdoption. The bounded legacy get_task/list_tasks bootstrap cannot establish absence from retained completed history.

Preparation returns the complete canonical/native comparison, mappingBefore/mappingAfter, requestId, server commandId/preparedCommandId, reviewHash, access/connection identity, expectedRevision and ten-minute expiresAtUtc. Native metadata is untrusted content. The review discloses zero native writes and game effects. Task fields, schedules, timestamps, retained completion/score history and unrelated organization remain unchanged. Future accepted commands may use the adopted correspondence.

accept_task_adoption requires the original commandId and reviewHash after review of the concrete pair. The saved preparation binds profile, workspace, connection, access epoch, origin and MCP grant epoch. Acceptance checks the exact current revision, matching kind and Todo completion, unique native mapping, fresh owned native fingerprint and unresolved effects. Existing mapping IDs survive; a new mapping uses the ID displayed by the server preview. Task and reward mutations are absent from adoption.

The shared executor lease excludes simultaneous projection dispatch. Prepared/leased/executing/unknown effects on the target, global executing/leased/unknown task effects, uncertain tag work, retirement/recovery fences and Auth unknown attempts block acceptance. Workspace/store-version, active connection/access/grant epoch, preparation state/expiry and lease ownership are checked again in the conditional save. D1 batch saves the mapping and immutable original receipt in one transaction; a failed receipt write rolls back the mapping. Native inspection has a fifteen-second service deadline and a ten-second upstream deadline. MCP allows fifty-five seconds for the complete service call.

get_task_adoption_command reads the original command within the current active workspace/connection/access identity. accepted returns the immutable original receipt after later task, mapping or workspace revisions and grant renewal. A receipt proves the original correspondence save, never current native freshness or rewards. refused proves terminal non-acceptance; a new command requires a fresh comparison. prepared is nonterminal and permits only the original acceptance. Missing records return unavailable/outcomeUnknown, never inferred success or permission for replacement.

Lost preparation responses reuse the original requestId and arguments. Lost acceptance responses recover the original command before another action. Browser recovery looks up the receipt first, validates the complete reviewed identity and retries only the original prepared command when known unaccepted. Only an authenticated original refused result clears an uncertain browser fence. A generic later HTTP refusal cannot clear uncertainty. Confirmed adoption with failed refresh requires reload.

Old /projection/preview and /projection/adopt fail closed with adoption_review_required. A stale client cannot accept a mutable-revision-only approval. Migration 0015-task-adoption.sql is additive and local; deployment must include the schema and matching Worker/browser assets. No hosted migration or deployment follows from local acceptance.

## Storage and lifecycle

workspace_task_adoptions has profile-scoped unique request and command IDs and a workspace foreign key with deletion cascade. Original review/receipt metadata stays server-owned. At most eight unexpired prepared reviews and 8192 total records exist per profile. Review UTF-8 bytes plus a 1024-byte reservation and accepted receipt bytes count toward the existing eight-MiB archive quota; the canonical document retains the one-MiB limit. Expired reviews stay explicitly unaccepted and retained metadata never grants replay. Disconnect denies access; deleting the workspace purges adoption records. No raw native credentials, Auth check token or account selector is returned.

## Finite migration parity inventory

| Step | Reviewed agent workflow | Boundary or remaining work |
| --- | --- | --- |
| Source and canonical target reads | list_companion_tasks, get_companion_task, get_completed_history, get_checklist_conversion and get_task_recovery retain exact canonical/conversion/history IDs | get_companion_task is active-only; archived target comparison uses exact adoption preparation. Names do not select writes. |
| Exact owned native inspection | prepare_task_adoption uses Auth inspectAdoption for the supplied exact native UUID, including completed records | Legacy get_task/list_tasks searches are bounded and cannot prove historical absence. No arbitrary native HTTP tool exists. |
| Existing-record adoption | prepare_task_adoption, accept_task_adoption, get_task_adoption_command share the browser contract | One exact pair per reviewed command. No bulk or automatic adoption. |
| Consent and tag status | get_task_sync_status and get_group_tag_status expose separate readiness; inspect_group_tag_candidates and group-tag reviews bind exact groups | Browser credential enrollment, OAuth consent, independent tag consent and executor opt-in stay direct-user controls. Stale tag review and stored coverage defects remain T-ee09583d-26dc-4291-880a-0b7a4801842e and T-de7b04d1-82c9-4734-a089-aaded9d2dd20. |
| Checklist conversion | get_checklist_conversion, prepare_checklist_conversion, accept_checklist_conversion, get_checklist_conversion_command | Exact ordered source item IDs, existing/new targets and project effects require the separate review. Adoption never performs conversion. |
| Source retirement | prepare_source_retirement, verify_source_retirement, accept_source_retirement, get_source_retirement_command, reconcile_source_retirement | Separate irreversible action and exact native-source review remain required. Adoption grants no deletion or retirement permission. |
| Original receipts | Dedicated adoption, task, conversion, group-tag and retirement receipt tools retain original command identity | Current mapping/state, a pending effect or a successful metadata save cannot replace original completion/reward proof. Unknown native effects cannot be replayed. |
| Devices and host delivery | Existing browser/device controls own Web Push permission and enrollment | Agents cannot grant device permission, create credentials or claim installed-host/device qualification from synthetic checks. |

No additional required adoption dependency was found in the finite inventory. The independently queued tag work stays outside the selected root. Actual migration of user records, fresh verification of retained production mappings and release qualification remain separate owner actions.

## Validation boundary

Synthetic HTTP/D1 and SDK controls cover active/archived records, wrong owner/kind/completion/ID, duplicate/conflicting mapping, reused requests, stale/expired review, epochs, unresolved effects, lease exclusion, save rollback, lost acknowledgements and later-revision original receipts. C# transport/controller checks validate the same durable identity and uncertainty behavior. Rendered fixtures qualify captured link states only. Local source evidence lives in skill-tree/history/validation/task-adoption/. No live Habitica mutation, account permission, deployment, installed host, assistive technology or physical-device acceptance is claimed.
