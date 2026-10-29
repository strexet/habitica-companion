import {test} from 'node:test';
import assert from 'node:assert/strict';
import {reminderCivilInstant,reminderIntent,nextReminder} from '../../shared/reminder-calendar.ts';
const id=()=>crypto.randomUUID();
function intent(repeat=null){return reminderIntent({operation:'set',scheduleId:null,expectedRevision:0,taskId:id(),reviewAfterUtc:'2026-10-26T09:00:00Z',timeZoneId:'UTC',repeat,targets:[{deviceId:id(),revision:1,subscriptionId:id()}],consentVersion:'scheduled-reminders-v1'});}
test('explicit DST policies skip gaps and choose or skip overlap without inventing a local time',()=>{
 assert.equal(reminderCivilInstant('2026-03-08','02:30','America/New_York','earlier'),null);
 assert.equal(reminderCivilInstant('2026-11-01','01:30','America/New_York','earlier'),'2026-11-01T05:30:00.0000000Z');
 assert.equal(reminderCivilInstant('2026-11-01','01:30','America/New_York','later'),'2026-11-01T06:30:00.0000000Z');
 assert.equal(reminderCivilInstant('2026-11-01','01:30','America/New_York','skip'),null);
 assert.equal(reminderCivilInstant('2026-10-26','09:00','Asia/Novosibirsk','earlier'),'2026-10-26T02:00:00.0000000Z');
 assert.equal(reminderCivilInstant('2026-10-26','00:00','UTC','earlier'),'2026-10-26T00:00:00.0000000Z');
});
test('one-time exact UTC and bounded recurring calendars retain monthly gaps, leap days and changed offsets',()=>{
 const single=intent();assert.deepEqual(nextReminder(single,'2026-10-26T08:59:59Z'),{atUtc:'2026-10-26T09:00:00.0000000Z',civilDate:'2026-10-26'});assert.equal(nextReminder(single,single.reviewAfterUtc),null);
 const recurring=intent({rule:{frequency:'monthly',interval:1,startDate:'2026-01-01',timeZoneId:'UTC',weekdays:[],daysOfMonth:[31]},time:'09:00',through:'2026-12-31',gap:'skip',overlap:'earlier'});recurring.reviewAfterUtc='2026-01-31T09:00:00.0000000Z';assert.equal(nextReminder(recurring,recurring.reviewAfterUtc).atUtc,'2026-03-31T09:00:00.0000000Z');
 const dst=intent({rule:{frequency:'daily',interval:1,startDate:'2026-03-07',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]},time:'02:30',through:'2026-03-10',gap:'skip',overlap:'later'});dst.timeZoneId=dst.repeat.rule.timeZoneId='America/New_York';dst.reviewAfterUtc='2026-03-07T07:30:00.0000000Z';assert.equal(nextReminder(dst,dst.reviewAfterUtc).atUtc,'2026-03-09T06:30:00.0000000Z');
});
test('malformed dates, missing policies, unknown timezone, targets and oversized horizon refuse',()=>{
 for(const args of [['2026-02-30','09:00','UTC','earlier'],['2026-01-01','25:00','UTC','earlier'],['2026-01-01','09:00','Missing/Zone','earlier']])assert.throws(()=>reminderCivilInstant(...args));
 const good=intent();for(const patch of [{targets:[]},{expectedRevision:1},{timeZoneId:'Missing/Zone'},{consentVersion:'workspace-v1'},{repeat:{rule:{frequency:'daily',interval:1,startDate:'2026-01-01',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]},time:'09:00',through:'2027-01-02',gap:'skip',overlap:'earlier'}}])assert.throws(()=>reminderIntent({...good,...patch}));
});

 test('independent CSharp wire hash fixtures agree with validated Worker reminder identity',async()=>{
 const {readFile}=await import('node:fs/promises');const {workspaceTokenHash}=await import('../../shared/workspace-http.ts');
 const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/reminder-command-v1.json',import.meta.url),'utf8'));
 for(const row of fixture.cases)assert.equal(await workspaceTokenHash(JSON.stringify({expectedWorkspaceRevision:row.expectedWorkspaceRevision,intent:reminderIntent(row.intent)})),row.hash);
});
