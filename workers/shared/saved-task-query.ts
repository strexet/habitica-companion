import {workspaceDocument,type WorkspaceDocument} from './workspace-domain.ts';
import {queryTasks} from './task-query.ts';import {queryUnblockedTodos} from './unblocked-todos.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export function querySavedTaskView(document:WorkspaceDocument,viewId:string,input:unknown){
 const id=uuid(viewId),source=workspaceDocument(document),view=source.savedTaskViews?.find(row=>row.id===id&&!row.isArchived);
 if(!view)throw new WorkspaceError('saved_view_not_found',404);
 const page=workspaceObject(input,['limit','offset','expectedRevision']),filter={...view.query.filter,...page};
 return (view.query.source==='unblockedTodos'?queryUnblockedTodos:queryTasks)(source,filter);
}
