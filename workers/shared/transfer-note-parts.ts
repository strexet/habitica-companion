import {WorkspaceError,workspaceTokenHash} from './workspace-http.ts';
import type {NotesNotebook} from './notes.ts';
// Split exact source bytes at complete PersonalNote boundaries. Metadata gaps contain no note body.
export async function splitTransferNotes(text:string,book:NotesNotebook){
 let at=0;const ranges:{start:number;end:number}[]=[];
 const whitespace=()=>{while(/\s/.test(text[at]??'')&&at<text.length)at++;};
 function string(){const start=at++;while(at<text.length){const c=text[at++];if(c==='"')return JSON.parse(text.slice(start,at)) as string;if(c==='\\')at++;}throw new WorkspaceError('invalid_transfer_notes',422);}
 function value(path:string){whitespace();const c=text[at];if(c==='"'){string();return;}
  if(c==='{'){at++;whitespace();if(text[at]==='}'){at++;return;}const names=new Set<string>();while(at<text.length){whitespace();if(text[at]!=='"')break;const name=string();if(names.has(name))throw new WorkspaceError('duplicate_transfer_notes_field',422);names.add(name);whitespace();if(text[at++]!==':')break;value(path==='root'&&name==='notes'?'notes':'nested');whitespace();const separator=text[at++];if(separator==='}')return;if(separator!==',')break;}throw new WorkspaceError('invalid_transfer_notes',422);}
  if(c==='['){at++;whitespace();if(text[at]===']'){at++;return;}while(at<text.length){whitespace();const start=at;value('nested');if(path==='notes')ranges.push({start,end:at});whitespace();const separator=text[at++];if(separator===']')return;if(separator!==',')break;}throw new WorkspaceError('invalid_transfer_notes',422);}
  const start=at;while(at<text.length&&!/[\s,}\]]/.test(text[at]))at++;if(at===start)throw new WorkspaceError('invalid_transfer_notes',422);
 }
 value('root');whitespace();if(at!==text.length||ranges.length!==book.notes.length)throw new WorkspaceError('invalid_transfer_notes',422);
 const template:({text:string}|{noteId:string})[]=[],parts:{part:string;text:string}[]=[],hashes:Record<string,string>={};let previous=0;
 for(let index=0;index<ranges.length;index++){const range=ranges[index],noteId=book.notes[index].id,raw=text.slice(range.start,range.end);template.push({text:text.slice(previous,range.start)},{noteId});parts.push({part:'note:'+noteId,text:raw});hashes[noteId]=await workspaceTokenHash(raw);previous=range.end;}
 template.push({text:text.slice(previous)});parts.unshift({part:'notes-template',text:JSON.stringify(template)});return {parts,hashes};
}
export function joinTransferNotes(templateText:string,parts:Map<string,string>,redact=false){
 const template=JSON.parse(templateText) as ({text:string}|{noteId:string})[];let complete=true;
 const text=template.map(segment=>{if('text'in segment)return segment.text;const raw=parts.get('note:'+segment.noteId);if(raw===undefined){complete=false;if(!redact)throw new WorkspaceError('transfer_source_notes_purged',409);return 'null';}return raw;}).join('');
 if(complete)return {text,complete};const value=JSON.parse(text);value.notes=value.notes.filter((note:unknown)=>note!==null);return {text:JSON.stringify(value),complete};
}
