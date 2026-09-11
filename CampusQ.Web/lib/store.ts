import {api} from './api'; import {Ticket,Office,mapApiTicket} from './data';
const KEY='campusq-demo-tickets';
export async function loadTickets():Promise<Ticket[]>{ try { const data=await api.queue(); return data.map(mapApiTicket); } catch { if(typeof window==='undefined') return []; try{return JSON.parse(localStorage.getItem(KEY)||'[]')}catch{return []} } }
export async function issueTicket(office:Office,purpose:string,isPriority=false){ const t=await api.add(office,purpose,isPriority); const mapped=mapApiTicket(t); return mapped; }
export async function callNext(office:Office,window:number){ const t=await api.next(office,window); if(!t) return null; return {...mapApiTicket(t),status:'Serving' as const,window}; }
export async function complete(id:number,window:number){await api.complete(id,window)}

export async function transfer(id:number,target:Office,reason:string,window?:number){await api.transfer(id,target,reason,'',window)}
