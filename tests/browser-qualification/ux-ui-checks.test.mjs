import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as core from './qualification-core.mjs';
import {validateRequest,assertOwnedIdentity,withCleanup,textInk} from './ux-ui-contract.mjs';
import * as contract from './ux-ui-contract.mjs';

test('opaque disabled text uses the product floor without skipping the control', () => {
  const result = core.assessDisabledText?.([0,0,0,1], [{background:[255,255,255,1],opacity:1}]);
  assert.equal(result?.ratio, 21);
  assert.equal(result?.minimum, 3);
  assert.equal(result?.passed, true);
});

test('unreadable disabled text fails independently of enabled-text exemptions', () => {
  const result = core.assessDisabledText?.([125,125,125,1], [{background:[125,125,125,1],opacity:1}]);
  assert.equal(result?.ratio, 1);
  assert.equal(result?.passed, false);
});

test('group opacity composites foreground and background together over the parent', () => {
  const result = core.assessDisabledText?.([255,255,255,1], [
    {background:[255,255,255,1],opacity:1},
    {background:[0,0,0,1],opacity:.5},
  ]);
  // White text remains white; the black group background becomes middle gray.
  assert.ok(Math.abs((result?.ratio ?? 0) - 3.976653024912438) < .000001);
});

test('nested disabled opacity cannot pass by measuring the unpainted foreground', () => {
  const result = core.assessDisabledText?.([0,0,0,1], [
    {background:[255,255,255,1],opacity:1},
    {background:[0,0,0,0],opacity:.5},
    {background:[255,255,255,1],opacity:.3},
  ]);
  assert.equal(result?.passed, false);
  assert.ok(result?.ratio > 1 && result?.ratio < 2);
});

test('matching actual native-date ink and full-date reference qualifies visible content', () => {
  assert.equal(core.assessNativeDate?.({actualInk:1000,referenceInk:1000,evidenceAvailable:true})?.qualification, 'pass');
});

test('a valid date value and valid outer geometry cannot excuse missing rendered date text', () => {
  const result = core.assessNativeDate?.({actualInk:80,referenceInk:1000,evidenceAvailable:true,value:'2026-11-03'});
  assert.equal(result?.qualification, 'fail');
  assert.equal(result?.kind, 'native-date-clipping');
});

test('missing or empty native reference evidence remains unavailable rather than passing', () => {
  assert.equal(core.assessNativeDate?.({actualInk:1000,referenceInk:1000,evidenceAvailable:false})?.qualification, 'unavailable');
  assert.equal(core.assessNativeDate?.({actualInk:0,referenceInk:0,evidenceAvailable:true})?.qualification, 'unavailable');
});

test('existing enabled contrast and geometry contracts remain available', () => {
  assert.equal(core.contrast([0,0,0],[255,255,255]), 21);
  assert.deepEqual(core.geometry({width:360,scrollWidth:360,controls:[]}), []);
});

const schemes={'gryphy-light':{isDark:false},'obsidian-glow':{isDark:true}};
const plan={task_key:'example',routes:['/todo/today'],matrix:{widths:[360,1280],text_percent:[100,200],schemes:Object.keys(schemes),engines:['chromium','webkit'],disabled_text_product_minimum:3}};
const adapter={journey:'./example.mjs',test_filter:'FullyQualifiedName~Example',fixtures:['today']};
const coverage={today:{file:'today.html',route:'/todo/today',state:'date-visible',interactionTest:'Date_changes'}};

test('route and state selection resolves only reviewed exact fixtures', () => {
  assert.deepEqual(validateRequest(plan,adapter,coverage,['/todo/today'],schemes),plan);
  assert.throws(()=>validateRequest({...plan,routes:['/missing']},adapter,coverage,['/todo/today'],schemes),/Unknown application route/);
  assert.throws(()=>validateRequest(plan,adapter,{},['/todo/today'],schemes),/No selected state/);
  assert.throws(()=>validateRequest(plan,null,coverage,['/todo/today'],schemes),/reviewed journey adapter/);
});

test('unknown schemes, unqualified dark-only matrices and duplicate routes fail before launch', () => {
  for(const changes of [{schemes:['obsidian-glow']},{schemes:'guess'},{widths:[]},{engines:['firefox']}])
    assert.throws(()=>validateRequest({...plan,matrix:{...plan.matrix,...changes}},adapter,coverage,['/todo/today'],schemes));
  assert.throws(()=>validateRequest({...plan,routes:['/todo/today','/todo/today']},adapter,coverage,['/todo/today'],schemes),/unique routes/);
  assert.throws(()=>validateRequest({...plan,mode:'harness-canaries-and-baseline-observation'},adapter,coverage,['/todo/today'],schemes),/restricted/);
});

test('a responding server cannot substitute for exact owner, checkout and build identity', () => {
  const identity={origin:'http://127.0.0.1:12345',session:'owner-A',projectRoot:'/work/project',build:'boot-sha'};
  assert.doesNotThrow(()=>assertOwnedIdentity(identity,identity));
  for(const field of Object.keys(identity))assert.throws(()=>assertOwnedIdentity(identity,{...identity,[field]:'other'}),/identity mismatch/);
});

test('failed qualification still awaits owned resource cleanup and retains the original failure', async () => {
  const events=[];
  await assert.rejects(withCleanup(async()=>{events.push('work');throw Error('seeded failure');},async()=>{await Promise.resolve();events.push('cleanup');}),/seeded failure/);
  assert.deepEqual(events,['work','cleanup']);
  await assert.rejects(withCleanup(async()=>{throw Error('work');},async()=>{throw Error('cleanup');}),AggregateError);
});

test('a successful session also closes resources before returning', async () => {
  let cleaned=false;
  assert.equal(await withCleanup(async()=>42,async()=>{cleaned=true;}),42);assert.equal(cleaned,true);
});

test('native ink probes use actual channel differences and reject changed screenshot geometry', () => {
  const hidden={width:1,height:1,data:Uint8Array.from([255,255,255,255])};
  assert.equal(textInk({...hidden,data:Uint8Array.from([0,0,0,255])},hidden),255);
  assert.equal(textInk(hidden,hidden),0);
  assert.throws(()=>textInk({...hidden,width:2},hidden),/preserve screenshot geometry/);
});

test('declared native fixtures cannot pass with zero measured date inputs', () => {
  assert.throws(()=>contract.checkDeclaredNativeDates?.({native_date_fixtures:['today']},'today',[]),/native date/i);
  assert.doesNotThrow(()=>contract.checkDeclaredNativeDates?.({},'inventory',[]));
});

test('strict non-date adapters can qualify without inventing a native-date journey', () => {
  const task={routes:['/inventory']},journey={routes:['/inventory'],checks:['filter by slot']};
  assert.equal(contract.checkJourneyEvidence?.(task,{},journey),true);
  assert.throws(()=>contract.checkJourneyEvidence?.(task,{native_date_journey:true},journey),/native date/i);
  assert.throws(()=>contract.checkJourneyEvidence?.(task,{}, {...journey,routes:[]}),/route/i);
});

test('cleanup attempts every browser and the server after a browser close failure', async () => {
  const events=[],state={contexts:false,server:false,profiles:false};
  const resources={contexts:new Set([{close:async()=>{events.push('bad');throw Error('close failed');}},{close:async()=>events.push('good')}]),profiles:['profile'],server:{closeAllConnections:()=>events.push('sockets'),listening:true,close:callback=>{events.push('server');callback();}},remove:async()=>events.push('remove'),state};
  await assert.rejects(contract.cleanupSession?.(resources)??Promise.resolve(),/cleanup/i);
  assert.ok(events.includes('good')&&events.includes('server'));
  assert.equal(events.includes('remove'),false,'Preserve profiles when browser closure is unverified');
  assert.deepEqual(state,{contexts:false,server:true,profiles:false});
});

test('header clearance rejects fully covered main content', () => {
  const result=core.headerClearance?.({header:{left:0,right:360,top:0,bottom:104},main:{left:0,right:360,top:48,bottom:300},content:[{left:20,right:200,top:60,bottom:90}]});
  assert.ok(result?.some(error=>error.kind==='header-content-overlap'));
});

test('header clearance rejects partial overlap and missing content evidence', () => {
  const header={left:0,right:360,top:0,bottom:104};
  assert.ok(core.headerClearance?.({header,main:{left:0,right:360,top:96,bottom:300},content:[{left:20,right:200,top:100,bottom:130}]})?.length>0);
  assert.ok(core.headerClearance?.({header,main:{left:0,right:360,top:104,bottom:300},content:[]})?.some(error=>error.kind==='missing-main-content'));
});

test('clear or horizontally separated content is not header overlap', () => {
  assert.deepEqual(core.headerClearance?.({header:{left:0,right:360,top:0,bottom:104},main:{left:0,right:360,top:104,bottom:300},content:[{left:20,right:200,top:120,bottom:150}]}),[]);
  assert.deepEqual(core.headerClearance?.({header:{left:0,right:200,top:0,bottom:104},main:{left:210,right:360,top:0,bottom:300},content:[{left:220,right:300,top:20,bottom:50}]}),[]);
});

test('parallel matrix coverage requires every declared case exactly once', async()=>{
  const {assertMatrixCoverage}=await import('./ux-ui-contract.mjs');
  const matrix={engines:['chromium','webkit'],schemes:['light','dark'],widths:[360],text_percent:[100]};
  const rows=[{engine:'chromium',theme:'light',width:360,text:100},{engine:'chromium',theme:'dark',width:360,text:100},{engine:'webkit',theme:'light',width:360,text:100},{engine:'webkit',theme:'dark',width:360,text:100}];
  assert.doesNotThrow(()=>assertMatrixCoverage(rows,matrix));
  assert.throws(()=>assertMatrixCoverage(rows.slice(1),matrix),/Missing/);
});
test('parallel coverage rejects duplicate cases even when counts match', async()=>{
  const {assertMatrixCoverage}=await import('./ux-ui-contract.mjs');
  const matrix={engines:['chromium'],schemes:['light','dark'],widths:[360],text_percent:[100]};
  const row={engine:'chromium',theme:'light',width:360,text:100};
  assert.throws(()=>assertMatrixCoverage([row,row],matrix),/Duplicate/);
});
test('parallel coverage rejects an unselected engine or text scale', async()=>{
  const {assertMatrixCoverage}=await import('./ux-ui-contract.mjs');
  const matrix={engines:['chromium'],schemes:['light'],widths:[360],text_percent:[100]};
  assert.throws(()=>assertMatrixCoverage([{engine:'webkit',theme:'light',width:360,text:100}],matrix),/Unexpected/);
  assert.throws(()=>assertMatrixCoverage([{engine:'chromium',theme:'light',width:360,text:200}],matrix),/Unexpected/);
});

test('disabled state evidence requires the exact visible native fields',()=>{
  const adapter={disabled_controls:{frozen:['title','notes','priority']}};
  const samples=['title','notes','priority'].map(id=>({id,source:'control-value'}));
  assert.doesNotThrow(()=>contract.checkDisabledControlEvidence(adapter,'frozen',samples));
  assert.throws(()=>contract.checkDisabledControlEvidence(adapter,'frozen',samples.filter(s=>s.id!=='notes')),/Missing.*notes/);
  assert.throws(()=>contract.checkDisabledControlEvidence(adapter,'frozen',samples.map(s=>({...s,source:'dom-text'}))),/Missing/);
});

test('hydrated appearance accepts the observed selected scheme and enlarged root text',()=>{
  assert.doesNotThrow(()=>contract.assertObservedAppearance({scheme:'obsidian-glow',text:200},{scheme:'obsidian-glow',fontSize:32}));
});
test('a light page cannot qualify as the requested dark scheme',()=>{
  assert.throws(()=>contract.assertObservedAppearance({scheme:'obsidian-glow',text:100},{scheme:'gryphy-light',fontSize:16}),/scheme mismatch/);
});
test('a requested enlarged-text case rejects an unchanged root font',()=>{
  assert.throws(()=>contract.assertObservedAppearance({scheme:'gryphy-light',text:200},{scheme:'gryphy-light',fontSize:16}),/text scale mismatch/);
});

test('synthetic provider data rejects external or traversing fixture paths before browser launch',()=>{
  for(const path of ['../private.json','https://example.test/user.json','/tmp/user.json','./nested/user.json'])
    assert.throws(()=>validateRequest(plan,{...adapter,synthetic_user_fixture:path},coverage,['/todo/today'],schemes),/synthetic user fixture/);
});
test('synthetic provider data accepts one explicit owned local fixture filename',()=>{
  assert.doesNotThrow(()=>validateRequest(plan,{...adapter,synthetic_user_fixture:'./ux-ui-companions-user.json'},coverage,['/todo/today'],schemes));
});
for(const kind of ['party','content']) {
  test('synthetic '+kind+' data rejects external and traversing paths',()=>{
    for(const path of ['../private.json','https://example.test/data.json','/tmp/data.json','./nested/data.json'])
      assert.throws(()=>validateRequest(plan,{...adapter,[`synthetic_${kind}_fixture`]:path},coverage,['/todo/today'],schemes),new RegExp('synthetic '+kind+' fixture'));
  });
  test('synthetic '+kind+' data accepts one owned reviewed filename',()=>{
    assert.doesNotThrow(()=>validateRequest(plan,{...adapter,[`synthetic_${kind}_fixture`]:'./ux-ui-game-'+kind+'.json'},coverage,['/todo/today'],schemes));
  });
}

test('equipment readable copy may stack but retains a full ordinary-word area',()=>{
  assert.deepEqual(core.assessEquipmentReadability({identities:[{width:180,availableWidth:180,wordWidth:120},{width:100,availableWidth:100,wordWidth:140}],stats:[{width:90,inkWidth:88,lineCount:1}]}),[]);
});
test('equipment glyph-wide copy fails even when the outer document fits',()=>{
  assert.equal(core.assessEquipmentReadability({identities:[{width:9,availableWidth:180,wordWidth:120}],stats:[]})[0].kind,'equipment-name-column');
  assert.equal(core.assessEquipmentReadability({identities:[{kind:'action-label',width:100,availableWidth:100,wordWidth:140,wordLineCounts:[2]}],stats:[]})[0].kind,'equipment-name-column','Fixed English action words must fit completely even when a narrow parent uses all available space');
});
test('equipment stat labels and numeric values cannot split across lines',()=>{
  assert.equal(core.assessEquipmentReadability({identities:[],stats:[{width:90,inkWidth:88,lineCount:2}]})[0].kind,'equipment-stat-token');
  assert.equal(core.assessEquipmentReadability({identities:[],stats:[{width:9,inkWidth:88,lineCount:1}]})[0].kind,'equipment-stat-token');
});
test('missing equipment text geometry is unavailable rather than accepted',()=>{
  assert.equal(core.assessEquipmentReadability({identities:[{width:0,availableWidth:180,wordWidth:120}],stats:[]})[0].kind,'equipment-name-unavailable');
  assert.equal(core.assessEquipmentReadability({identities:[],stats:[{width:90,inkWidth:88,lineCount:0}]})[0].kind,'equipment-stat-unavailable');
});

 test('equipment action word line boxes reject a split inside the geometry tolerance',()=>{
  assert.deepEqual(core.assessEquipmentReadability({identities:[{kind:'action-label',width:139.5,availableWidth:139.5,wordWidth:140,wordLineCounts:[1,1]}],stats:[]}),[]);
  assert.equal(core.assessEquipmentReadability({identities:[{kind:'action-label',width:139.5,availableWidth:139.5,wordWidth:140,wordLineCounts:[1,2]}],stats:[]})[0].kind,'equipment-name-column');
  assert.equal(core.assessEquipmentReadability({identities:[{kind:'action-label',width:140,availableWidth:140,wordWidth:140,wordLineCounts:[]}],stats:[]})[0].kind,'equipment-name-unavailable');
});


test('count labels reject a split ordinary word even inside geometry tolerance',()=>{
 const row={kind:'count-label',text:'Hatching potions',width:99.5,availableWidth:240,wordWidth:100,wordLineCounts:[2,1]};
 assert.equal(core.assessEquipmentReadability({identities:[row],stats:[]}).length,1);
 assert.deepEqual(core.assessEquipmentReadability({identities:[{...row,width:240,wordLineCounts:[1,1]}],stats:[]}),[]);
});
