import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { workspaceCommand, commandHash } from '../../shared/workspace-commands.ts';
const fixture = JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/command-v1.json', import.meta.url), 'utf8'));
test('neutral command fixture preserves UTF-8 identity across C# and Worker contracts', async () => {
  const command = workspaceCommand(fixture.command);
  assert.equal(await commandHash(command), fixture.payloadHash);
  assert.notEqual(await commandHash({ ...command, patch: { ...command.patch, notes: null } }), fixture.payloadHash);
});
test('Worker command contract rejects selectors, numeric coercion and ignored fields', () => {
  const valid = fixture.command;
  for (const bad of [{ ...valid, profileId: 'other' }, { ...valid, count: '1' }, { ...valid, direction: 'up' }, { ...valid, expectedRevision: Number.MAX_SAFE_INTEGER + 1 }, { ...valid, patch: { ...valid.patch, userId: 'other' } }, { ...valid, patch: { ...valid.patch, priority: 2 } }, { ...valid, patch: { ...valid.patch, deadline: '2026-02-30' } }, { ...valid, operation: 'genericProxy' }, { ...valid, operation: ['createTodo'] }, { ...valid, patch: { ...valid.patch, priority: ['high'] } }, { ...valid, patch: { ...valid.patch, text: '\u0085' } }]) assert.throws(() => workspaceCommand(bad));
});
