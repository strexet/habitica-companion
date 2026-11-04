import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {readActivityStatistics} from '../src/activity-streaks.ts';
import {utcTicks} from '../../shared/workspace-domain.ts';
import {activityStreakFilter} from '../../shared/activity-streak-filter.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/activity-streaks-v1.json',import.meta.url)));
for(const row of fixture.cases)test('portable activity days: '+row.name,async()=>{const before=JSON.stringify(row.snapshot),actual=await readActivityStatistics(row.snapshot,row.request);assert.equal(utcTicks(actual.asOfUtc),utcTicks(row.expected.asOfUtc));assert.deepEqual({...actual,asOfUtc:row.expected.asOfUtc},row.expected);assert.equal(JSON.stringify(row.snapshot),before);assert.equal(actual.lifetimeBest,null);});
test('paging binds the original revision, as-of, timezone, identity and range',async()=>{
 const row=fixture.cases[0],first=await readActivityStatistics(row.snapshot,{...row.request,limit:1});assert.equal(first.nextOffset,1);const request={...row.request,limit:1,offset:1,expectedRevision:first.revision,expectedContext:first.context},second=await readActivityStatistics(row.snapshot,request);assert.equal(second.items[0].day,'2026-10-28');assert.equal(second.context,first.context);
 for(const patch of [{asOfUtc:'2026-10-30T12:00:00Z'},{fromDay:'2026-10-28'},{timeZoneId:'Europe/Prague'},{expectedRevision:first.revision+1},{expectedContext:'0'.repeat(64)}])await assert.rejects(readActivityStatistics(row.snapshot,{...request,...patch}));
});
test('malformed and foreign selectors never fall back to an unscoped read',async()=>{
 const row=fixture.cases[0];for(const patch of [{ownerScope:'other'},{accountId:'other'},{taskId:crypto.randomUUID()},{taskId:'title'},{fromDay:'2026-99-99'},{timeZoneId:'Mars/Olympus'},{offset:1},{asOfUtc:'2026-10-29T00:00:00+03:00'}])await assert.rejects(async()=>readActivityStatistics(row.snapshot,{...row.request,...patch}));assert.throws(()=>activityStreakFilter({...row.request,limit:0}));
});
test('fabricated coverage cannot become complete historical inactivity',async()=>{const row=structuredClone(fixture.cases[0]);row.snapshot.document.activityCoverage[0].payloadHash='0'.repeat(64);await assert.rejects(readActivityStatistics(row.snapshot,row.request));});

test('legacy missing command snapshots label current goal fallback without inventing historical coverage',async()=>{const row=structuredClone(fixture.cases.find(c=>c.name==='legacy-positive-evidence-retains-a-lower-bound'));row.snapshot.commands=[];row.snapshot.events=[];const result=await readActivityStatistics(row.snapshot,row.request);assert.equal(result.modeSource,'currentTask');assert.equal(result.currentQuality,'lowerBound');assert.equal(result.currentStreak,2);assert.equal(result.lifetimeBest,null);});
