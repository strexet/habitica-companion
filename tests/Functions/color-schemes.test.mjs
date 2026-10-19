import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import vm from 'node:vm';

const source = readFileSync(new URL('../../src/Habitica.WebApp/wwwroot/js/colorSchemes.js', import.meta.url), 'utf8');
const saved = { id: 'saved-dark', tokens: { background: '#12091e', primary: '#a569d7' } };
const preview = { id: 'transient', tokens: { background: '#e8e7de', primary: '#345678' } };
const preferences = { selectedSchemeId: saved.id, selectedAtUtc: '2026-10-19T00:00:00Z', customSchemes: [saved] };

function browser(storage = new Map(), unavailable = false) {
  const properties = new Map();
  const root = { style: { setProperty: (name, value) => properties.set(name, value) }, dataset: {} };
  const context = { document: { documentElement: root, querySelector: () => null }, window: { localStorage: {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => { if (unavailable) throw new Error('Storage unavailable'); storage.set(key, value); },
    removeItem: key => storage.delete(key),
  } } };
  vm.runInNewContext(source, context);
  return { api: context.window.HabiticaColorScheme, root, properties, storage };
}

test('saved import updates live CSS and exact preferences without restamping', () => {
  const page = browser();
  page.api.applyAndStore(saved, preferences);
  assert.equal(page.root.dataset.colorScheme, saved.id);
  assert.equal(page.properties.get('--bg'), '#12091e');
  assert.deepEqual(JSON.parse(JSON.stringify(page.api.getPreferences())), preferences);
});

test('cache-only import preserves a transient preview and reload applies the saved selection', () => {
  const page = browser();
  page.api.applyColorScheme(preview);
  page.api.applyAndStore(saved, preferences, false);
  assert.equal(page.root.dataset.colorScheme, preview.id);
  assert.equal(page.properties.get('--bg'), '#e8e7de');
  assert.equal(page.api.getPreferences().selectedSchemeId, saved.id);
  const reloaded = browser(page.storage);
  assert.equal(reloaded.root.dataset.colorScheme, saved.id);
  assert.equal(reloaded.properties.get('--bg'), '#12091e');
  assert.equal(reloaded.api.getPreferences().selectedAtUtc, preferences.selectedAtUtc);
});

test('preview alone leaves saved preferences and the reload cache untouched', () => {
  const page = browser();
  page.api.applyAndStore(saved, preferences);
  const originalCache = [...page.storage];
  page.api.applyColorScheme(preview);
  assert.deepEqual([...page.storage], originalCache);
  assert.equal(browser(page.storage).root.dataset.colorScheme, saved.id);
});

test('unavailable localStorage does not prevent live appearance application', () => {
  const page = browser(new Map(), true);
  page.api.applyAndStore(saved, preferences);
  assert.equal(page.properties.get('--bg'), '#12091e');
});
