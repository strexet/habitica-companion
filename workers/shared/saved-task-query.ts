import {evaluateRelativeDates,hasRelativeDates,matchesRelativeMissingDates} from './relative-saved-dates.ts';
import {workspaceDocument,type WorkspaceDocument} from './workspace-domain.ts';
import {queryTasks} from './task-query.ts';import {queryUnblockedTodos} from './unblocked-todos.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export function querySavedTaskView(document:WorkspaceDocument,viewId:string,input:unknown,asOfUtc=new Date().toISOString()){
 const id=uuid(viewId),source=workspaceDocument(document),view=source.savedTaskViews?.find(row=>row.id===id&&!row.isArchived);
 if(!view)throw new WorkspaceError('saved_view_not_found',404);
 const {expectedEvaluationDay,...page}=workspaceObject(input,['limit','offset','expectedRevision','expectedEvaluationDay']);
 if(hasRelativeDates(view.query)&&Number(page.offset??0)>0&&expectedEvaluationDay===undefined)throw new WorkspaceError('evaluation_day_required',422);
 if(expectedEvaluationDay!==undefined){try{const value=String(expectedEvaluationDay);if(typeof expectedEvaluationDay!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value)||new Date(value).toISOString().slice(0,10)!==value||value==='0001-01-01'||value.startsWith('0000'))throw new Error();}catch{throw new WorkspaceError('invalid_evaluation_context',422);}}
 const evaluation=evaluateRelativeDates(view.query,asOfUtc,expectedEvaluationDay as string|undefined),{plannedRelative,deadlineRelative,...absolute}=view.query.filter;
 const filter={...absolute,...(evaluation?.planned?.from?{plannedFrom:evaluation.planned.from}:{}),...(evaluation?.planned?.through?{plannedTo:evaluation.planned.through}:{}),...(evaluation?.deadline?.from?{deadlineFrom:evaluation.deadline.from}:{}),...(evaluation?.deadline?.through?{deadlineTo:evaluation.deadline.through}:{}),...page};
 const result=(view.query.source==='unblockedTodos'?queryUnblockedTodos:queryTasks)(source,filter,task=>matchesRelativeMissingDates(task,evaluation));
 return {...result,...(evaluation?{evaluation}:{})};
}
