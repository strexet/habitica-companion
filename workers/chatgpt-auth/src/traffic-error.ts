import {WorkspaceError} from '../../shared/workspace-http.ts';
export class TrafficLimit extends WorkspaceError {
 readonly retryAtUtc:string;
 readonly retryAfterSeconds:number;
 constructor(retryAfterSeconds:number){super('rate_limited',429);this.retryAfterSeconds=retryAfterSeconds;this.retryAtUtc=new Date(Date.now()+retryAfterSeconds*1000).toISOString();}
}
