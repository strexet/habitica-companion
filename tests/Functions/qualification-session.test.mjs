import {test} from 'node:test';
import assert from 'node:assert/strict';
import {QualificationCase,QualificationTimer,qualificationLimits} from '../browser-qualification/qualification-session.mjs';
const identity={caseId:'case-fixture-uuid',fixture:'/fixture.html',theme:'gryphy-light',width:390,textPercent:200,seed:null};
const phase=(name,status='waiting')=>({...identity,phase:name,status,elapsedMs:25});
const result=()=>({...identity,errors:[],unqualifiedSurfaces:[]});
test('startup prerequisite stays bounded and cannot become a measured success',()=>{
 const c=new QualificationCase(identity,0);assert.equal(c.result(result(),10),null);assert.equal(c.phase(phase('fonts'),100),true);assert.equal(c.timeout(4999),null);
 const failed=c.timeout(5000);assert.equal(failed.operationClass,'prerequisite_wait');assert.equal(failed.qualification,'unavailable');assert.equal(failed.controllerDiagnostics.phases[0].phase,'fonts');assert.equal(c.result(result(),5010),null);
});
test('measurement stays inside the original overall five-second deadline; phase chatter never renews the deadline',()=>{
 const c=new QualificationCase(identity,0);c.phase(phase('frame'),4000);assert.equal(c.deadline,5000);c.phase(phase('measurement'),4500);assert.equal(c.deadline,5000);
 c.phase(phase('measurement'),4900);assert.equal(c.deadline,5000);assert.equal(c.result(result(),4999).operationClass,'executed_control');assert.equal(c.result(result(),5000),null);
 const late=new QualificationCase(identity,0);late.phase(phase('measurement'),100);assert.equal(late.result(result(),5000),null);assert.equal(late.timeout(5000).operationClass,'executed_control');
});
test('stale, mismatched, malformed and terminal messages cannot replace the exact case',()=>{
 for(const mismatch of [{caseId:'old-case'},{fixture:'/other.html'},{theme:'obsidian-glow'},{width:360},{textPercent:100},{seed:'geometry'}]){
  const c=new QualificationCase(identity,0);assert.equal(c.phase({...phase('measurement'),...mismatch},10),false);assert.equal(c.result({...result(),...mismatch},20),null);assert.equal(c.state,'startup');
 }
 const c=new QualificationCase(identity,0);assert.equal(c.phase({...phase('fonts'),elapsedMs:-1},10),false);assert.equal(c.phase({...phase('unknown')},10),false);c.phase(phase('measurement'),20);assert.equal(c.phase(phase('fonts'),25),false);assert.equal(c.result({...identity},30),null);assert.equal(c.result(result(),40).qualification,'measured');assert.equal(c.phase(phase('measurement'),50),false);
});
test('frame exceptions remain unavailable and identify prerequisite versus executed measurement',()=>{
 for(const measured of [false,true]){const c=new QualificationCase(identity,0);if(measured)c.phase(phase('measurement'),1);const r=c.result({...identity,harnessError:'fixture failure'},20);assert.equal(r.qualification,'unavailable');assert.equal(r.operationClass,measured?'executed_control':'prerequisite_wait');}
 assert.equal(qualificationLimits.runMs,300000);
});
test('timer replacement, early wake, completed case and cleanup leave no pending callback',()=>{
 let now=0,next=0;const scheduled=new Map(),cancelled=[],expired=[];
 const timer=new QualificationTimer((fn,delay)=>{scheduled.set(++next,{fn,delay});return next;},id=>{cancelled.push(id);scheduled.delete(id);},()=>now,result=>expired.push(result));
 const c=new QualificationCase(identity,0);timer.arm(c);assert.equal(scheduled.get(1).delay,5000);now=100;c.phase(phase('measurement'),now);timer.arm(c);assert.deepEqual(cancelled,[1]);assert.equal(scheduled.get(2).delay,4900);
 const early=scheduled.get(2);scheduled.delete(2);now=4999;early.fn();assert.equal(expired.length,0);assert.equal(scheduled.get(3).delay,1);
 now=5000;const final=scheduled.get(3);scheduled.delete(3);final.fn();assert.equal(expired.length,1);assert.equal(expired[0].operationClass,'executed_control');timer.arm(c);assert.equal(scheduled.size,0);
 const done=new QualificationCase(identity,now);timer.arm(done);done.phase(phase('measurement'),now+1);done.result(result(),now+2);timer.clear();assert.equal(scheduled.size,0);
});

import {finishSnapshotTransitions} from '../browser-qualification/qualification-session.mjs';
test('static snapshot settling is restricted to running CSS transitions with ordinary finite timing',()=>{
 class Transition{constructor(end=250,state='running',rate=1){this.effect={getComputedTiming:()=>({endTime:end})};this.playState=state;this.playbackRate=rate;this.finishedCount=0;}finish(){this.finishedCount++;this.playState='finished';}}
 const ordinary=new Transition(),long=new Transition(1000),infinite=new Transition(Infinity),paused=new Transition(250,'paused'),zero=new Transition(250,'running',0),unknown={...new Transition(),finish(){throw Error('must not run');}};
 assert.deepEqual(finishSnapshotTransitions([ordinary,long,infinite,paused,zero,unknown],Transition),[ordinary]);assert.equal(ordinary.finishedCount,1);for(const other of [long,infinite,paused,zero])assert.equal(other.finishedCount,0);
 assert.deepEqual(finishSnapshotTransitions([ordinary],undefined),[]);const broken=new Transition();broken.finish=()=>{throw Error('private implementation text');};assert.throws(()=>finishSnapshotTransitions([broken],Transition),/Snapshot CSS transition final state is unavailable/);
});


test('animation diagnostics are bounded and discard arbitrary detail fields',()=>{
 const c=new QualificationCase(identity,0);c.phase({...phase('animations'),details:Array.from({length:30},()=>({name:'n'.repeat(200),classes:'c'.repeat(500),privateText:'not retained'}))},10);
 const details=c.phases[0].details;assert.equal(details.length,12);assert.equal(details[0].name.length,80);assert.equal(details[0].classes.length,160);assert.equal(details[0].privateText,undefined);c.phase({...phase('animations'),details:[null,undefined,3,'invalid']},20);assert.deepEqual(c.phases[1].details,[]);
});
