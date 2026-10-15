import {workspaceObject,WorkspaceError,workspaceTokenHash} from './workspace-http.ts';
export const supportedSpell={id:'healAll',name:'Blessing',className:'healer',manaCost:25,unlockLevel:14,target:'party',maxCount:3} as const;
export const spellScopes={context:['profile:read','account:read','party:read'],cast:['profile:read','account:read','party:read','habitica.spells.execute'],lookup:['profile:read','account:read']} as const;
export function spellEligibility(value:{className:string;level:number;mana:number;health:number;classesDisabled?:boolean},count=1){
 if(!Number.isInteger(count)||count<1||count>supportedSpell.maxCount)return 'invalid_count';
 if(value.className!==supportedSpell.className)return 'wrong_class';
 if(value.classesDisabled)return 'classes_disabled';
 if(!Number.isSafeInteger(value.level)||value.level<supportedSpell.unlockLevel)return 'spell_locked';
 if(!Number.isFinite(value.health)||value.health<=0)return 'not_alive';
 if(!Number.isFinite(value.mana)||value.mana<supportedSpell.manaCost*count)return 'insufficient_mana';
 return null;
}
export function spellStats(value:unknown){
 const stats=workspaceObject(value);const keys=['class','lvl','hp','mp','str','int','con','per','exp','gp','points','training','buffs'];
 if(typeof stats.class!=='string'||stats.class.length>32||!Number.isSafeInteger(stats.lvl)||Number(stats.lvl)<1||typeof stats.hp!=='number'||!Number.isFinite(stats.hp)||typeof stats.mp!=='number'||!Number.isFinite(stats.mp)||stats.mp<0)throw new WorkspaceError('invalid_spell_snapshot',502);
 return Object.fromEntries(keys.map(key=>[key,stats[key]??null]));
}
function stable(value:unknown,depth=0):unknown{
 if(depth>8)throw new WorkspaceError('invalid_spell_snapshot',502);
 if(value===null||typeof value==='string'||typeof value==='boolean'||typeof value==='number'&&Number.isFinite(value))return value;
 if(Array.isArray(value)){if(value.length>256)throw new WorkspaceError('invalid_spell_snapshot',502);return value.map(v=>stable(v,depth+1));}
 const o=workspaceObject(value);if(Object.keys(o).length>256)throw new WorkspaceError('invalid_spell_snapshot',502);
 return Object.fromEntries(Object.keys(o).sort().map(key=>[key,stable(o[key],depth+1)]));
}
export async function spellStateHash(critical:unknown){const json=JSON.stringify(stable(critical));if(new TextEncoder().encode(json).length>65536)throw new WorkspaceError('invalid_spell_snapshot',502);return workspaceTokenHash(json);}
