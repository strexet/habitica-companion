const phases = new Set(['theme', 'fonts', 'frame', 'animations', 'measurement']);
export const qualificationLimits = Object.freeze({caseMs:5000, startupMs:5000, measurementMs:5000, runMs:300000});

// Browser lifecycle waits and measurements share the original explicit case deadline.
export class QualificationCase {
    constructor(identity, startedAt, limits = qualificationLimits) {
        this.identity = Object.freeze({...identity});this.startedAt = startedAt;this.limits = limits;
        this.caseDeadline = startedAt + limits.caseMs;this.deadline = Math.min(this.caseDeadline,startedAt + limits.startupMs);this.state = 'startup';this.phases = [];this.ignoredMessages = 0;
    }
    matches(value) {
        const e=this.identity;
        return value && value.caseId===e.caseId && value.fixture===e.fixture && value.theme===e.theme
            && value.width===e.width && value.textPercent===e.textPercent && (value.seed??null)===(e.seed??null);
    }
    phase(value, now) {
        if(this.state==='finished'||now>=this.deadline||!this.matches(value)||!phases.has(value.phase)
            ||!['waiting','complete'].includes(value.status)||!Number.isFinite(value.elapsedMs)||value.elapsedMs<0||this.phases.length>=12){this.ignoredMessages++;return false;}
        if(this.state==='measurement'&&value.phase!=='measurement'){this.ignoredMessages++;return false;}
        this.phases.push({phase:value.phase,status:value.status,frameElapsedMs:value.elapsedMs,matrixElapsedMs:now-this.startedAt,...(Array.isArray(value.details)?{details:value.details.slice(0,12).filter(row=>row&&typeof row==='object').map(row=>({name:String(row.name??'').slice(0,80),state:String(row.state??'').slice(0,20),pending:row.pending===true,rate:Number.isFinite(row.rate)?row.rate:null,time:Number.isFinite(row.time)?row.time:null,endTime:Number.isFinite(row.endTime)?row.endTime:null,target:String(row.target??'').slice(0,20),classes:String(row.classes??'').slice(0,160),settlement:String(row.settlement??'').slice(0,40)}))}:{})});
        if(value.phase==='measurement'&&value.status==='waiting'&&this.state==='startup'){
            this.state='measurement';this.deadline=Math.min(this.caseDeadline,now+this.limits.measurementMs);
        }
        return true;
    }
    remaining(now) {return Math.max(0,this.deadline-now);}
    diagnostics(now) {return {caseLimitMs:this.limits.caseMs,startupLimitMs:this.limits.startupMs,measurementLimitMs:this.limits.measurementMs,matrixElapsedMs:now-this.startedAt,phases:this.phases.slice(),ignoredMessages:this.ignoredMessages};}
    timeout(now) {
        if(this.state==='finished'||now<this.deadline)return null;
        const prerequisite=this.state==='startup';this.state='finished';
        return {...this.identity,harnessError:prerequisite?'Case deadline exceeded during startup prerequisite':'Case deadline exceeded during measurement',qualification:'unavailable',operationClass:prerequisite?'prerequisite_wait':'executed_control',controllerDiagnostics:this.diagnostics(now)};
    }
    result(value, now) {
        if(this.state==='finished'||now>=this.deadline||!this.matches(value)
            ||(!value.harnessError&&(this.state!=='measurement'||!Array.isArray(value.errors)||!Array.isArray(value.unqualifiedSurfaces)))){this.ignoredMessages++;return null;}
        const operationClass=this.state==='startup'?'prerequisite_wait':'executed_control';this.state='finished';
        return {...value,qualification:value.harnessError?'unavailable':'measured',operationClass,controllerDiagnostics:this.diagnostics(now)};
    }
}

export class QualificationTimer {
    constructor(schedule,cancel,now,expired){this.schedule=schedule;this.cancel=cancel;this.now=now;this.expired=expired;}
    clear(){if(this.handle!==undefined){this.cancel(this.handle);this.handle=undefined;}}
    arm(control){
        this.clear();if(control.state==='finished')return;
        this.handle=this.schedule(()=>{this.handle=undefined;const result=control.timeout(this.now());if(result)this.expired(result);else this.arm(control);},control.remaining(this.now()));
    }
}

// Static snapshot policy: settle ordinary short CSS transitions to their actual final values.
// Animation behavior and intermediate states are outside static geometry/contrast qualification.
export function finishSnapshotTransitions(animations, TransitionType) {
    if(typeof TransitionType!=='function')return [];
    const settled=[];
    for(const animation of animations){
        const end=animation.effect?.getComputedTiming().endTime;
        if(animation instanceof TransitionType&&animation.playState==='running'&&animation.playbackRate===1&&Number.isFinite(end)&&end>=0&&end<=250){
            try{animation.finish();}catch{throw new Error('Snapshot CSS transition final state is unavailable');}
            settled.push(animation);
        }
    }
    return settled;
}
