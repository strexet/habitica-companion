import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdtemp,rm,mkdir} from 'node:fs/promises';
import {join,resolve,extname,dirname} from 'node:path';
import {tmpdir} from 'node:os';
import {createRequire} from 'node:module';
import {createHash} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {validateRequest,assertOwnedIdentity,assertObservedAppearance,withCleanup,checkDeclaredNativeDates,checkJourneyEvidence,cleanupSession,assertMatrixCoverage,checkDisabledControlEvidence} from './ux-ui-contract.mjs';
import {nativeDateEvidence} from './ux-ui-native-date.mjs';

const require=createRequire(import.meta.url);
const playwrightPath=process.env.PLAYWRIGHT_MODULE||'/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright';
const pw=require(playwrightPath),runtimeRequire=createRequire(require.resolve(playwrightPath+'/package.json'));
const {PNG}=runtimeRequire('pngjs');
const config=JSON.parse(await readFile(process.argv[2],'utf8'));
const {projectRoot,runDirectory:out,adapter,session}=config;
assert.equal(resolve(projectRoot),resolve(process.cwd()));
const fixtures=join(out,'fixtures'),web=join(out,'published/wwwroot');
const coverage=JSON.parse(await readFile(join(fixtures,'coverage.json'),'utf8'));
const schemes=JSON.parse(await readFile(join(fixtures,'schemes.json'),'utf8'));
const inventory=JSON.parse(await readFile(join(fixtures,'route-inventory.json'),'utf8'));
const plan=validateRequest(config.plan,adapter,coverage,Object.keys(inventory.routes),schemes);
const syntheticUserFile=adapter.synthetic_user_fixture?resolve(dirname(new URL(import.meta.url).pathname),adapter.synthetic_user_fixture):null;
const syntheticUserBytes=syntheticUserFile?await readFile(syntheticUserFile):null;
const syntheticUser=syntheticUserBytes?JSON.parse(syntheticUserBytes.toString('utf8')):{profile:{name:'Synthetic UI owner'},stats:{class:'warrior',lvl:1,hp:50,maxHealth:50,mp:10,maxMP:10},preferences:{dayStart:0,timezoneOffset:0},items:{gear:{equipped:{},owned:{}}}};
assert.ok(syntheticUser.profile&&syntheticUser.stats&&syntheticUser.items,'Owned synthetic user fixture must contain explicit user fields');
const extraProviderFixtures={};
for(const kind of ['party','content'])if(adapter[`synthetic_${kind}_fixture`]) {
  const file=adapter[`synthetic_${kind}_fixture`],bytes=await readFile(resolve(dirname(new URL(import.meta.url).pathname),file));
  extraProviderFixtures[kind]={file,sha256:createHash('sha256').update(bytes).digest('hex'),data:JSON.parse(bytes.toString('utf8'))};
}
if(extraProviderFixtures.party)assert.ok(extraProviderFixtures.party.data.party&&Array.isArray(extraProviderFixtures.party.data.members),'Owned party fixture requires party and members');
if(extraProviderFixtures.content)assert.ok(extraProviderFixtures.content.data.gear?.flat,'Owned content fixture requires gear.flat');
const journeyPath=resolve(dirname(new URL(import.meta.url).pathname),adapter.journey);
assert.equal(dirname(journeyPath),dirname(new URL(import.meta.url).pathname));
const {runJourney}=await import(pathToFileURL(journeyPath));assert.equal(typeof runJourney,'function');
const contexts=new Set(),profiles=[],results=[],baseline=[],journeys=[],cleanup={contexts:false,server:false,profiles:false},requests={syntheticProviderGets:0,syntheticAssetGets:0,blocked:[]};
let origin,identity,lastPage,failureUrl,shuttingDown=false,deadlineTimer;
const mime={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.dat':'application/octet-stream','.dll':'application/octet-stream','.woff2':'font/woff2','.png':'image/png','.svg':'image/svg+xml'};
function canary(kind) {
  const bad=kind==='disabled'?'#aaa':'#111';
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/mud.css"><link rel="stylesheet" href="/app.css"><style>
    body{margin:0!important;padding:16px!important;background:white!important;background-image:${kind==='positive'?'linear-gradient(180deg,#bbd2ff,#f5c2cf)':'none'}!important;color:#111!important;font-family:Arial,sans-serif!important}main{max-width:900px}label{display:block;margin:8px 0}input,textarea,button{font:inherit;box-sizing:border-box;max-width:100%;height:52px;padding:8px;background:white!important;color:#111!important;-webkit-text-fill-color:#111!important;opacity:1!important;border:1px solid #333}input,textarea{width:100%}button{min-width:44px}*:focus-visible{outline:3px solid #0040a0!important;outline-offset:2px}#disabled-input{color:#111!important;-webkit-text-fill-color:#111!important;opacity:1!important;background:white!important}#disabled-area{color:${bad}!important;-webkit-text-fill-color:${bad}!important;opacity:1!important;background:white!important}#disabled-text{color:${bad}!important;-webkit-text-fill-color:${bad}!important}#nested{opacity:${kind==='disabled'?'.1':'1'}}#canary-date{width:${kind==='date'?'70px':'100%'}!important;background:${kind==='positive'?'rgba(255,255,255,.5)':'white'}!important}
    </style></head><body><main><h1>Harness paint controls</h1><p>Independent synthetic paint; application behavior is qualified separately.</p><label>Disabled input<input id="disabled-input" disabled value="Readable disabled value"></label><label>Disabled textarea<textarea id="disabled-area" disabled>Readable disabled textarea</textarea></label><button id="disabled-text" disabled>Disabled action</button><div id="nested"><button disabled>Nested disabled action</button></div>${kind==='positive'?'<fieldset disabled style="margin:0;padding:0;border:0;min-width:0">':''}<label>Selected civil date<input id="canary-date" type="date" value="2026-11-03"></label>${kind==='positive'?'</fieldset>':''}<button id="canary-primary" data-layout-primary>Keyboard action</button></main><script src="/colorSchemes.js"></script><script type="module" src="/qualification-frame.mjs"></script></body></html>`;
}
const server=createServer(async(req,res)=>{
  try {
    const path=decodeURIComponent(new URL(req.url,'http://fixture.invalid').pathname);
    if(req.method!=='GET'){res.writeHead(405);res.end();return;}
    if(path==='/__qualification/identity'){res.setHeader('Content-Type','application/json');res.end(JSON.stringify(identity));return;}
    if(path.startsWith('/__qualification/canary/')){res.setHeader('Content-Type','text/html');res.end(canary(path.split('/').pop()));return;}
    if(['/__qualification/seed','/__qualification/reference'].includes(path)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><html lang="en"><meta charset="utf-8"><title>Owned synthetic control</title><body></body></html>');return;}
    let base=web,name=path;
    if(path.startsWith('/matrix/')){base=fixtures;name=path.slice('/matrix'.length);}
    else if(['/schemes.json','/mud.css','/app.css','/colorSchemes.js','/qualification-frame.mjs','/qualification-core.mjs','/qualification-session.mjs','/ux-ui-equipment-metrics.mjs','/ux-ui-notes-metrics.mjs'].includes(path))base=fixtures;
    else if(!extname(path))name='/index.html';
    const file=resolve(base,'.'+name);assert.ok(file.startsWith(resolve(base)+'/'));
    res.setHeader('Content-Type',mime[extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');res.end(await readFile(file));
  } catch {res.writeHead(404);res.end();}
});
async function newContext(engine) {
  assert.equal(shuttingDown,false,'The execution deadline forbids new browser launches');
  const profile=await mkdtemp(join(tmpdir(),'habitica-ux-ui-'));profiles.push(profile);
  const context=await pw[engine].launchPersistentContext(profile,{headless:true,serviceWorkers:'block',locale:'en-US',timeout:15000});contexts.add(context);
  context.setDefaultTimeout(12000);
  await context.route('**/*',async route=>{
    const request=route.request(),url=request.url();
    if(new URL(url).origin===origin)return route.continue();
    if(url.startsWith('https://habitica.com/api/v3/')&&request.method()==='GET') {
      requests.syntheticProviderGets++;
      const path=new URL(url).pathname;
      const data=path==='/api/v3/user'?syntheticUser:path==='/api/v3/tasks/user'?[]:
        path==='/api/v3/groups/party'&&extraProviderFixtures.party?extraProviderFixtures.party.data.party:
        path==='/api/v3/groups/party/members'&&extraProviderFixtures.party?extraProviderFixtures.party.data.members:
        path==='/api/v3/groups/party/chat'&&extraProviderFixtures.party?[]:
        path==='/api/v3/content'&&extraProviderFixtures.content?extraProviderFixtures.content.data:{gear:{flat:{}}};
      return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data})});
    }
    if(url.startsWith('https://habitica-assets.s3.amazonaws.com/')&&request.method()==='GET') {
      requests.syntheticAssetGets++;
      return route.fulfill({status:200,contentType:'image/png',body:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64')});
    }
    requests.blocked.push({method:request.method(),origin:new URL(url).origin});return route.abort();
  });
  return context;
}
async function measure(page,path,{engine,theme,width,text,kind,name}) {
  assert.equal(shuttingDown,false,'Qualification deadline expired');
  await page.setViewportSize({width,height:900});
  const caseId=`${session}-${engine}-${theme}-${width}-${text}-${kind==='canary'?'canary':'product'}-${name}`;
  await page.goto(origin+path+`?theme=${theme}&text=${text}&case=${caseId}&disabledMinimum=${plan.matrix.disabled_text_product_minimum}&extended=1`,{waitUntil:'domcontentloaded'});
  await page.waitForFunction(id=>{const el=document.getElementById('qualification-result');return el&&JSON.parse(el.textContent).caseId===id;},caseId,{timeout:30000});
  const row=JSON.parse(await page.locator('#qualification-result').textContent());
  assert.equal(row.caseId,caseId,'A receipt belongs to this exact engine/palette/width/text/fixture case');
  assert.ok(!row.harnessError,row.harnessError);assert.ok(row.controls>0&&row.textSamples>0,'Nonzero rendered controls and text');
  const metadata={engine,theme,width,text,kind,name};
  const dates=[];
  if(kind==='canary'||adapter.native_date_fixtures?.includes(name))for(const input of await page.locator('input[type="date"]').all()) {
    if(!await input.isVisible())continue;
    const date=await nativeDateEvidence(input,{context:page.context(),origin,out:join(out,'pixels'),id:`${engine}-${theme}-${width}-${text}-${name}-${dates.length}`,PNG});dates.push(date);
  }
  checkDeclaredNativeDates(adapter,name,dates);
  if(kind==='canary') {
    assert.deepEqual(row.unqualifiedSurfaces,[]);
    assert.ok(row.disabledSamples.length>=4&&row.disabledSkipped===0,'Disabled values and nested text are actually sampled');
    assert.equal(row.disabledSamples.filter(sample=>sample.id==='disabled-area'&&sample.source==='control-value').length,1,'Native textarea value is measured exactly once');
    if(name==='disabled') {
      assert.ok(row.errors.some(e=>e.kind==='disabled-text-contrast'&&e.id==='disabled-text'));
      assert.ok(row.errors.some(e=>e.kind==='disabled-text-contrast'&&e.id==='disabled-area'&&e.source==='control-value'));
      assert.ok(row.errors.some(e=>e.kind==='disabled-text-contrast'&&e.text.includes('Nested')));
    } else assert.deepEqual(row.errors,[],JSON.stringify({...metadata,errors:row.errors}));
    assert.equal(dates.length,1);assert.equal(dates[0].qualification,name==='date'?'fail':'pass',JSON.stringify({...metadata,dates}));
    assert.equal(dates[0].disabled,name==='positive','Native date reference preserves effective inherited fieldset disability');
    const primary=page.getByRole('button',{name:'Keyboard action',exact:true});await primary.focus();
    assert.equal(await primary.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true);
    const disabled=page.locator('#disabled-text');assert.equal(await disabled.isDisabled(),true);
    assert.equal(await disabled.evaluate(el=>{let calls=0;const count=()=>calls++;el.addEventListener('click',count);el.click();el.removeEventListener('click',count);return calls;}),0,'Disabled native controls refuse click events');
    results.push({...metadata,measurement:row,nativeDates:dates,harnessPassed:true,expectedNegative:name!=='positive'});
  } else {
    if(adapter.disabled_fixtures?.includes(name))assert.ok(row.disabledSamples.length>0,'A disabled-state fixture must sample disabled text');
    checkDisabledControlEvidence(adapter,name,row.disabledSamples);
    baseline.push({...metadata,measurement:row,nativeDates:dates,productPassed:row.errors.length===0&&row.unqualifiedSurfaces.length===0&&dates.every(d=>d.qualification==='pass'),qualification:'Product baseline observation; no fix acceptance'});
    if(!plan.mode){assert.deepEqual(row.errors,[],JSON.stringify(metadata));assert.deepEqual(row.unqualifiedSurfaces,[]);assert.ok(dates.every(d=>d.qualification==='pass'),'Native date evidence unavailable or clipped');}
  }
  if(width===360&&text===200)await page.screenshot({path:join(out,`${engine}-${theme}-${name}.png`),fullPage:true});
}
async function cleanupOwned() {
  shuttingDown=true;clearTimeout(deadlineTimer);
  await cleanupSession({contexts,profiles,server,remove:rm,state:cleanup});
}
let cancel;
const interrupted=new Promise((_,reject)=>{cancel=()=>{shuttingDown=true;reject(Error('Owned qualification execution deadline or termination'));};});
// The bounded launch barrier still settles every launch before owned cleanup.
interrupted.catch(()=>{});
process.once('SIGTERM',cancel);process.once('SIGINT',cancel);
try {
  await withCleanup(async()=>{
    await mkdir(join(out,'pixels'),{recursive:true});
    await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(0,'127.0.0.1',resolve);});
    origin='http://127.0.0.1:'+server.address().port;
    identity={origin,session,projectRoot,build:createHash('sha256').update(await readFile(join(web,'_framework/blazor.boot.json'))).digest('hex'),pid:process.pid,playwright:runtimeRequire('playwright/package.json').version,assets:JSON.parse(await readFile(join(fixtures,'asset-fingerprints.json'),'utf8'))};
    if(syntheticUserBytes)identity.syntheticUserFixture={file:adapter.synthetic_user_fixture,sha256:createHash('sha256').update(syntheticUserBytes).digest('hex')};
    for(const [kind,{file,sha256}] of Object.entries(extraProviderFixtures))identity[`synthetic${kind[0].toUpperCase()+kind.slice(1)}Fixture`]={file,sha256};
    deadlineTimer=setTimeout(cancel,(config.operationLimits?.browserSeconds??560)*1000);
    const workerCount=Math.min(adapter.workers_per_engine??1,plan.matrix.schemes.length);
    // Each worker owns one persistent profile and its native databases. Complete
    // every bounded launch before starting work or cleanup, including failures.
    const launches=await Promise.allSettled(plan.matrix.engines.flatMap(engine=>Array.from({length:workerCount},async()=>({engine,context:await newContext(engine)}))));
    const launchFailure=launches.find(result=>result.status==='rejected');if(launchFailure)throw launchFailure.reason;
    const queues=new Map(plan.matrix.engines.map(engine=>[engine,[...plan.matrix.schemes]]));
    let firstFailure;
    await Promise.race([interrupted,(async()=>{
      const workers=await Promise.allSettled(launches.map(async({value:{engine,context}})=>{
        const page=await context.newPage();lastPage=page;
        let debugPage=page;
        const journeyPage=adapter.reuse_hydrated_page?await context.newPage():page;
        try {
          await page.goto(origin+'/__qualification/seed');
          const observed=await page.evaluate(async()=>await(await fetch('/__qualification/identity')).json());assertOwnedIdentity(identity,observed);
          assert.throws(()=>assertOwnedIdentity({...identity,session:'another-owner'},observed),/identity mismatch/);
          if(journeyPage!==page){await journeyPage.goto(origin+'/__qualification/seed');const liveObserved=await journeyPage.evaluate(async()=>await(await fetch('/__qualification/identity')).json());assertOwnedIdentity(identity,liveObserved);}
          let firstTheme=true;
          for(let theme;(theme=queues.get(engine).shift())&&!shuttingDown;) {
            for(const width of plan.matrix.widths)for(const text of plan.matrix.text_percent) {
              debugPage=page;
              if(adapter.reuse_hydrated_page)await journeyPage.setViewportSize({width,height:900});
              // Canary colors are explicit (including a translucent date surface);
              // paint depends on engine/width/text, not the product palette.
              if(firstTheme)for(const name of ['positive','disabled','date'])await measure(page,'/__qualification/canary/'+name,{engine,theme,width,text,kind:'canary',name});
              // Static inspection gives a retained app time to settle its real
              // responsive layout without a fixed sleep or a fresh CLR boot.
              if(adapter.reuse_hydrated_page)for(const name of adapter.fixtures)await measure(page,'/matrix/'+coverage[name].file,{engine,theme,width,text,kind:'Razor baseline',name});
              debugPage=journeyPage;
              let nativeProbe=0;
              const journey=await runJourney({page:journeyPage,origin,engine,scheme:schemes[theme],width,text,web,routes:plan.routes,
                qualifyNativeDate:input=>nativeDateEvidence(input,{context,origin,out:join(out,'pixels'),id:`${engine}-${theme}-${width}-${text}-hydrated-${nativeProbe++}`,PNG})});
              const appearance=await journeyPage.evaluate(()=>({scheme:document.documentElement.dataset.colorScheme,fontSize:parseFloat(getComputedStyle(document.documentElement).fontSize)}));
              assertObservedAppearance({scheme:theme,text},appearance);journey.appearance=appearance;
              checkJourneyEvidence(plan,adapter,journey);journeys.push(journey);
              debugPage=page;
              if(!adapter.reuse_hydrated_page)for(const name of adapter.fixtures)await measure(page,'/matrix/'+coverage[name].file,{engine,theme,width,text,kind:'Razor baseline',name});
            }
            firstTheme=false;
            console.log(JSON.stringify({completedPalette:{engine,theme},journeys:journeys.length}));
          }
        } catch(error) {
          if(!firstFailure){firstFailure=error;lastPage=debugPage;failureUrl=debugPage.url();shuttingDown=true;
            await writeFile(join(out,'failure.html'),await debugPage.content().catch(()=>''));
            await debugPage.screenshot({path:join(out,'failure.png'),fullPage:true,timeout:10000}).catch(()=>{});
          }
          throw error;
        }
      }));
      if(firstFailure)throw firstFailure;
      const failure=workers.find(result=>result.status==='rejected');if(failure)throw failure.reason;
    })()]);
  },cleanupOwned);
} catch(error) {
  await writeFile(join(out,'failure.json'),JSON.stringify({error:String(error),stack:error.stack,identity,cleanup,results,baseline,journeys,requests,url:failureUrl??lastPage?.url()},null,2)+'\n');throw error;
} finally {process.off('SIGTERM',cancel);process.off('SIGINT',cancel);}
const cases=plan.matrix.engines.length*plan.matrix.schemes.length*plan.matrix.widths.length*plan.matrix.text_percent.length;
const workerCount=Math.min(adapter.workers_per_engine??1,plan.matrix.schemes.length);
assert.equal(results.length,plan.matrix.engines.length*workerCount*plan.matrix.widths.length*plan.matrix.text_percent.length*3);
assert.equal(baseline.length,cases*adapter.fixtures.length);assert.equal(journeys.length,cases);
assertMatrixCoverage(journeys,plan.matrix);
if(adapter.reuse_hydrated_page)assert.equal(journeys.filter(row=>row.coldStart).length,plan.matrix.engines.length*workerCount,'Exactly one owned cold start per engine worker; no hidden reload recovery');
for(const name of adapter.fixtures)assertMatrixCoverage(baseline.filter(row=>row.name===name),plan.matrix);
assert.deepEqual(cleanup,{contexts:true,server:true,profiles:true});assert.deepEqual(requests.blocked,[]);
const assertions=results.length+baseline.length+journeys.length+1;
await writeFile(join(out,'report.json'),JSON.stringify({identity,plan,coverage,counts:{expected:assertions,discovered:assertions,passed:assertions,failed:0,skipped:0},
  countMeaning:'Harness canary cases, completed baseline measurements, actual hydrated journeys and owned-identity guard. Paint canaries run once per isolated engine worker across widths/text sizes; every product fixture and hydrated journey retains the full palette matrix. Negative controls must be detected; product baseline failures are reported separately and never counted as fix acceptance.',
  execution:{workersPerEngine:workerCount,isolatedProfilesCreated:profiles.length,reusesHydratedPage:!!adapter.reuse_hydrated_page,canaryScope:"Independent important white/black paint, once per worker across all engine/width/text combinations; product palettes remain exhaustive."},
  results,baseline,journeys,requests,cleanup,
  limitations:['Desktop emulation only; physical devices, mobile keyboards and assistive technology remain unqualified.','Native pixel checks use same-engine en-US input paint and a97-percent ink-retention tolerance; unsupported or missing text-paint evidence is unavailable, never a pass.','Foundation mode observes existing product defects. Dependent task mode rejects errors and unavailable required evidence.','Service workers are blocked; hosted OAuth, redirects, caches and provider execution are outside the owned fixture.']},null,2)+'\n');
