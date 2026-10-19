import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import vm from 'node:vm';

const source = readFileSync(new URL('../../src/Habitica.WebApp/wwwroot/js/colorSchemes.js', import.meta.url), 'utf8');
const saved = { id: 'saved-dark', tokens: { background: '#12091e', primary: '#a569d7' } };
const preview = { id: 'transient', tokens: { background: '#e8e7de', primary: '#345678' } };
const preferences = { selectedSchemeId: saved.id, selectedAtUtc: '2026-10-19T00:00:00Z', customSchemes: [saved] };

function browser(storage = new Map(), unavailable = false, cards = []) {
  const properties = new Map();
  const root = { style: { setProperty: (name, value) => properties.set(name, value), getPropertyValue: name => properties.get(name)??"" }, dataset: {} };
  const context = { getComputedStyle: element => element === root ? {getPropertyValue: name=>properties.get(name)??""} : {backgroundColor:element.background}, document: { documentElement: root, querySelector: () => null, querySelectorAll: () => cards }, window: { localStorage: {
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


test('derived foreground uses measured contrast on gold and middle-tone surfaces', () => {
  const luminance = hex => {
    const channels = hex.replace('#', '').match(/../g).map(v => parseInt(v, 16) / 255)
      .map(v => v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4);
    return channels.reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
  };
  const ratio = (a,b) => (Math.max(luminance(a), luminance(b)) + .05) / (Math.min(luminance(a), luminance(b)) + .05);
  for (const background of ['#f0a400','#d99416','#777777','#899099','#7334bd','#12081f']) {
    const page = browser();
    page.api.applyColorScheme({id:'contrast-canary',tokens:{primary:background,accent:background,danger:background,success:background,appBarBackground:background,primaryButtonText:'#aaaaaa'}});
    for (const key of ['--button-text','--button-text-accent','--button-text-danger','--button-text-success','--appbar-text'])
      assert.ok(ratio(background,page.properties.get(key)) >= 4.5, `${background} ${key} ${page.properties.get(key)}`);
  }
});
test('a qualifying authored foreground stays unchanged', () => {
  const page = browser();
  page.api.applyColorScheme({id:'authored',tokens:{primary:'#12081f',primaryButtonText:'#fff7ff'}});
  assert.equal(page.properties.get('--button-text'),'#fff7ff');
});


test('surface text derives readable gold, status and muted colors without changing preferences', () => {
  const page = browser();
  const before = [...page.storage];
  page.api.applyColorScheme({id:'surface-canary',tokens:{background:'#ffffff',cardBackground:'#ffffff',surface:'#ffffff',surfaceStrong:'#ffffff',inputBackground:'#ffffff',ink:'#201136',primary:'#7b2dd6',accent:'#f0a400',danger:'#d94768',success:'#1f9f7a',muted:'#aaaaaa'}});
  const parse = value => value.startsWith('#') ? value.slice(1).match(/../g).map(v=>parseInt(v,16)) : value.match(/[\d.]+/g).slice(0,3).map(Number);
  const lum = rgb => rgb.map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
  for (const name of ['primary','accent','danger','success','muted']) {
    const foreground = lum(parse(page.properties.get('--text-'+name)));
    assert.ok((1.05)/(foreground+.05)>=4.5,name);
  }
  assert.notEqual(page.properties.get('--text-accent'),'#f0a400');
  assert.deepEqual([...page.storage],before);
});
test('readable surface authoring stays exact while transparent pale text is repaired', () => {
  const page = browser();
  page.api.applyColorScheme({id:'surface-alpha',tokens:{background:'#ffffff',ink:'#111111',primary:'#333333',muted:'rgba(0,0,0,0.05)'}});
  assert.equal(page.properties.get('--text-primary'),'#333333');
  assert.notEqual(page.properties.get('--text-muted'),'rgba(0,0,0,0.05)');
});


test('task foreground follows actual task color on theme application and explicit rerender refresh', () => {
  const values = new Map();
  const card = {background:'rgb(42, 105, 64)',style:{getPropertyValue:key=>values.get(key)??'',setProperty:(key,value)=>values.set(key,value)}};
  const page=browser(new Map(),false,[card]);
  page.api.applyColorScheme({id:'task-contrast',tokens:{background:'#ffffff',ink:'#162423'}});
  assert.equal(values.get('--task-readable-ink'),'#ffffff');
  card.background='rgb(255, 240, 240)';page.api.refreshTaskContrast();
  assert.equal(values.get('--task-readable-ink'),'#162423');
});
