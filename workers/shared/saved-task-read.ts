import {workspaceDocument,type WorkspaceDocument} from './workspace-domain.ts';
import {taskFilter} from './task-query.ts';import {querySavedTaskView} from './saved-task-query.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export type SavedTaskReadTool='list_saved_task_views'|'query_saved_task_view';
export function savedTaskReadInput(name:SavedTaskReadTool,input:unknown):Record<string,unknown>{
 const raw=workspaceObject(input,['limit','offset','expectedRevision',...(name==='query_saved_task_view'?['viewId']:[])]),{viewId,...page}=raw;
 const valid=taskFilter(page);return {...valid,...(name==='query_saved_task_view'?{viewId:uuid(viewId)}:{})};
}
export function readSavedTaskViews(document:WorkspaceDocument,name:SavedTaskReadTool,input:unknown){
 const args=savedTaskReadInput(name,input),source=workspaceDocument(document);
 if(name==='query_saved_task_view'){const {viewId,...page}=args;return {...querySavedTaskView(source,String(viewId),page),viewId};}
 const f=taskFilter(args);if(f.expectedRevision!==undefined&&f.expectedRevision!==source.revision)throw new WorkspaceError('stale_revision',409);
 const rows=(source.savedTaskViews??[]).filter(row=>!row.isArchived),offset=f.offset??0,limit=f.limit??50,items=rows.slice(offset,offset+limit),next=offset+items.length;
 return {provenance:'companion-canonical',workspaceId:source.id,revision:source.revision,items,matchedCount:rows.length,truncated:next<rows.length,offset,nextOffset:next<rows.length?next:null};
}
