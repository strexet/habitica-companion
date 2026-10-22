import type {WorkspaceDocument,CompanionTask,Entity} from './workspace-domain.ts';
import {queryTasks} from './task-query.ts';
import {queryUnblockedTodos} from './unblocked-todos.ts';
import type {TagGroupKind} from './tag-projection.ts';
import type {SavedTaskQuery} from './saved-task-views.ts';
export interface DesiredTagGroup {entityId:string;kind:TagGroupKind;name:string;archived:boolean;removed:boolean;taskIds:string[];retainMembership:boolean}
export interface DesiredTagMembership {sourceRevision:number;groups:DesiredTagGroup[]}

// A saved selection is materialized completely from one immutable source snapshot.
// Revision-bound reads cannot silently stop at the public API's first 100 rows.
function selection(document:WorkspaceDocument,query:SavedTaskQuery):string[]{
 const ids:string[]=[];let offset=0;
 do{
  const args={...query.filter,limit:100,offset,expectedRevision:document.revision};
  const page=query.source==='unblockedTodos'?queryUnblockedTodos(document,args):queryTasks(document,args);
  ids.push(...page.tasks.map(task=>task.id));if(page.nextOffset===null)break;offset=page.nextOffset;
 }while(offset<=document.tasks.length);
 if(offset>document.tasks.length)throw new Error('tag_selection_paging_invalid');
 return ids;
}
export function desiredTagMembership(document:WorkspaceDocument):DesiredTagMembership{
 const retired=new Set((document.sourceRetirements??[]).map(row=>row.sourceTaskId)),tasks=document.tasks.filter(task=>!retired.has(task.id));
 const groups:DesiredTagGroup[]=[],projects=new Map(document.projects.map(project=>[project.id,project])),sections=new Map(document.sections.map(section=>[section.id,section]));
 const historical=(task:CompanionTask)=>task.isArchived||task.state==='completed'||task.state==='cancelled';
 function add(rows:(Entity&{name:string})[],kind:TagGroupKind,member:(task:CompanionTask,row:Entity)=>boolean,removed:(row:Entity)=>boolean=()=>false){
  for(const row of rows){const isRemoved=removed(row);groups.push({entityId:row.id,kind,name:row.name,archived:row.isArchived,removed:isRemoved,retainMembership:row.isArchived&&!isRemoved,taskIds:tasks.filter(task=>member(task,row)&&(!isRemoved||historical(task))).map(task=>task.id)});}
 }
 add(document.projects,'project',(task,row)=>task.projectId===row.id,row=>projects.get(row.id)?.isRemoved===true);
 add(document.sections,'section',(task,row)=>task.sectionId===row.id,row=>projects.get(sections.get(row.id)!.projectId)?.isRemoved===true);
 add(document.categories,'category',(task,row)=>(task.categoryId??(task.projectId&&!projects.get(task.projectId)?.isRemoved?projects.get(task.projectId)?.categoryId:null))===row.id);
 add(document.labels,'label',(task,row)=>task.labelIds.includes(row.id));
 for(const row of document.goals)groups.push({entityId:row.id,kind:'goal',name:row.name,archived:row.isArchived,removed:false,retainMembership:row.isArchived,taskIds:row.taskIds.filter(id=>!retired.has(id))});
 for(const row of document.savedTaskViews??[])groups.push({entityId:row.id,kind:'savedTaskView',name:row.name,archived:row.isArchived,removed:false,retainMembership:row.isArchived,taskIds:selection(document,row.query).filter(id=>!retired.has(id))});
 return {sourceRevision:document.revision,groups};
}
