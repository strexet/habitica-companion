import assert from 'node:assert/strict';

export function assertMatrixCoverage(rows,matrix) {
  const key=row=>JSON.stringify([row.engine,row.theme,row.width,row.text]);
  const expected=new Set();
  for(const engine of matrix.engines)for(const theme of matrix.schemes)
    for(const width of matrix.widths)for(const text of matrix.text_percent)expected.add(key({engine,theme,width,text}));
  const seen=new Set();
  for(const row of rows){const id=key(row);assert.ok(expected.has(id),'Unexpected matrix case: '+id);assert.ok(!seen.has(id),'Duplicate matrix case: '+id);seen.add(id);}
  assert.equal(seen.size,expected.size,'Missing required matrix cases');
}

export function checkDisabledControlEvidence(adapter,name,samples) {
  for(const id of adapter.disabled_controls?.[name]??[])
    assert.ok(samples.some(sample=>sample.id===id&&sample.source==='control-value'),'Missing required disabled native value: '+id);
}

export function validateRequest(plan, adapter, coverage, routes, schemes) {
  assert.ok(plan && /^[a-z][a-z0-9-]*$/.test(plan.task_key), 'An exact task key is required');
  assert.ok(adapter && adapter.journey && adapter.test_filter, 'A reviewed journey adapter and capture filter are required');
  for(const kind of ['user','party','content'])if(adapter[`synthetic_${kind}_fixture`]!==undefined)
    assert.match(adapter[`synthetic_${kind}_fixture`],/^\.\/ux-ui-[a-z0-9-]+\.json$/,'One owned synthetic '+kind+' fixture filename is required');
  assert.equal(adapter.workers_per_engine??1,1,'One isolated profile per engine is qualified on this host');
  assert.ok(adapter.reuse_hydrated_page===undefined||typeof adapter.reuse_hydrated_page==='boolean','Hydrated page reuse must be explicitly declared');
  assert.ok(Array.isArray(plan.routes) && plan.routes.length && new Set(plan.routes).size===plan.routes.length, 'Exact unique routes are required');
  assert.ok(Array.isArray(adapter.fixtures) && adapter.fixtures.length, 'Exact state fixtures are required');
  for(const route of plan.routes) {
    assert.ok(routes.includes(route), 'Unknown application route: '+route);
    assert.ok(adapter.fixtures.some(name=>coverage[name]?.route===route), 'No selected state for route: '+route);
  }
  for(const name of adapter.fixtures) {
    const item=coverage[name];
    assert.ok(item?.file && item.state && item.interactionTest && plan.routes.includes(item.route), 'Missing or out-of-scope fixture provenance: '+name);
    assert.ok(/^[a-z0-9-]+\.html$/.test(item.file), 'Unsafe fixture filename');
  }
  for(const key of ['native_date_fixtures','disabled_fixtures'])if(adapter[key])
    assert.ok(Array.isArray(adapter[key])&&adapter[key].every(name=>adapter.fixtures.includes(name)), 'Required controls must belong to selected fixtures: '+key);
  if(adapter.disabled_controls)for(const [name,ids] of Object.entries(adapter.disabled_controls))
    assert.ok(adapter.fixtures.includes(name)&&Array.isArray(ids)&&ids.length&&ids.every(id=>typeof id==='string'&&id.length),'Exact disabled native controls require a selected fixture');
  const m=plan.matrix;
  for(const key of ['widths','text_percent','engines'])assert.ok(Array.isArray(m?.[key])&&m[key].length&&new Set(m[key]).size===m[key].length,'Explicit matrix axis required: '+key);
  assert.ok(m.widths.every(n=>Number.isInteger(n)&&n>=320&&n<=2560), 'Invalid matrix width');
  assert.ok(m.text_percent.every(n=>[100,200].includes(n)), 'Invalid text scale');
  assert.ok(m.engines.every(n=>['chromium','webkit'].includes(n)), 'Unsupported browser engine');
  assert.ok(Array.isArray(m.schemes)||m.schemes==='all built-in schemes from ColorSchemeCatalog; explicitly enumerate resolved IDs in report', 'Explicit scheme IDs or the declared all-built-in selection are required');
  const selected=Array.isArray(m.schemes)?m.schemes:Object.keys(schemes).filter(k=>!k.startsWith('generated-'));
  assert.ok(selected.length&&selected.every(k=>schemes[k]), 'Unknown or unavailable color scheme');
  assert.ok(selected.includes('obsidian-glow')&&selected.some(k=>!schemes[k].isDark), 'A readable light scheme and obsidian-glow are required');
  assert.ok(Number.isFinite(m.disabled_text_product_minimum)&&m.disabled_text_product_minimum>=3&&m.disabled_text_product_minimum<=21, 'An explicit disabled-text floor is required');
  if(plan.mode)assert.ok(plan.task_key==='ui-qualification'&&plan.mode==='harness-canaries-and-baseline-observation', 'Baseline observation is restricted to the shared harness task');
  return {...plan,matrix:{...m,schemes:selected}};
}

export function assertObservedAppearance(expected, observed) {
  assert.equal(observed?.scheme,expected.scheme,'Hydrated color scheme mismatch');
  const font=expected.text===200?32:16;
  assert.ok(Number.isFinite(observed?.fontSize)&&Math.abs(observed.fontSize-font)<.01,'Hydrated text scale mismatch');
  return observed;
}

export function assertOwnedIdentity(expected, observed) {
  for(const field of ['origin','session','projectRoot','build'])assert.ok(expected[field]&&expected[field]===observed?.[field], 'Owned fixture identity mismatch: '+field);
}

export async function withCleanup(work, cleanup) {
  let failure;
  try {return await work();} catch(error) {failure=error;throw error;}
  finally {
    try {await cleanup();} catch(error) {
      if(failure)throw new AggregateError([failure,error], 'Qualification failed and cleanup is incomplete');
      throw error;
    }
  }
}

export function checkDeclaredNativeDates(adapter,name,dates) {
  if(adapter.native_date_fixtures?.includes(name))assert.ok(dates.length>0,'Declared native date fixture produced no date evidence: '+name);
}

export function checkJourneyEvidence(plan,adapter,journey) {
  assert.ok(Array.isArray(journey?.checks)&&journey.checks.length,'The reviewed journey must execute semantic checks');
  assert.ok(Array.isArray(journey.routes)&&journey.routes.every(route=>plan.routes.includes(route)),'Journey routes must belong to the selected task');
  if(!plan.mode) {
    assert.ok(plan.routes.every(route=>journey.routes.includes(route)), 'Every selected route requires a hydrated journey');
    if(adapter.native_date_journey||journey.nativeDate)assert.equal(journey.nativeDate?.qualification,'pass','Required native date journey evidence is unavailable or clipped');
  }
  return true;
}

export async function cleanupSession({contexts,profiles,server,remove,state}) {
  const errors=[];
  async function bounded(operation) {
    let timer;
    try {return await Promise.race([operation(),new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('Owned cleanup deadline exceeded')),15000);})]);}
    finally {clearTimeout(timer);}
  }
  const closed=await Promise.allSettled([...contexts].map(context=>bounded(()=>context.close())));
  for(const result of closed)if(result.status==='rejected')errors.push(result.reason);
  state.contexts=errors.length===0;if(state.contexts)contexts.clear();
  try {
    server.closeAllConnections();
    if(server.listening)await bounded(()=>new Promise((resolve,reject)=>server.close(error=>error?reject(error):resolve())));
    state.server=true;
  } catch(error) {errors.push(error);}
  if(state.contexts) {
    const removed=await Promise.allSettled(profiles.map(path=>remove(path,{recursive:true,force:true})));
    state.profiles=removed.every(result=>result.status==='fulfilled');
    for(const result of removed)if(result.status==='rejected')errors.push(result.reason);
  }
  if(errors.length)throw new AggregateError(errors,'Owned qualification cleanup is incomplete');
}

// Sum channel differences, excluding nearly identical raster noise. A hidden-text
// screenshot removes native value text without changing control geometry.
export function textInk(normal, hidden) {
  assert.ok(normal.width===hidden.width&&normal.height===hidden.height,'Text probes must preserve screenshot geometry');
  let ink=0;
  for(let i=0;i<normal.data.length;i+=4) {
    const delta=Math.max(...[0,1,2].map(c=>Math.abs(normal.data[i+c]-hidden.data[i+c])));
    if(delta>8)ink+=delta;
  }
  return ink;
}
