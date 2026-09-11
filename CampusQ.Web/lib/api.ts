export const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || '').replace(/\/$/, '');
export const apiUrl = (path:string) => `${API_BASE}${path}`;
export type ApiTicket = { ticketNumber:number; serviceTicketNumber:number; purpose:string; service:string; timeAdded:string; ticketLabel:string; status?:string; windowNumber?:number; isPriority?:boolean; calledAt?:string|null };
export type TicketStatus = { state:string; ticketNumber:number; ticketLabel?:string; service?:string; purpose?:string; peopleAhead:number; positionInLine:number; estimatedWaitMinutes:number; servedAt?:string };
async function request<T>(path:string, init?:RequestInit):Promise<T>{ let token:string|null=null; if(typeof window!=='undefined'){try{token=(JSON.parse(localStorage.getItem('campusq-session')||'null')||{}).accessToken||null}catch{}} const r=await fetch(apiUrl(path),{...init,headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{}) ,...(init?.headers||{})},cache:'no-store'}); if(!r.ok) throw new Error(await r.text() || `API ${r.status}`); if(r.status===204) return null as T; return r.json(); }
export const api={
  offices:()=>request<any[]>('/api/offices'),
  queue:()=>request<ApiTicket[]>('/api/queue'),
  office:(o:string)=>request<ApiTicket[]>(`/api/queue/${encodeURIComponent(o)}`),
  ticket:(n:number)=>request<ApiTicket>(`/api/queue/ticket/${n}`),
  status:(n:number)=>request<TicketStatus>(`/api/ticket/${n}/status`),
  add:(service:string,purpose:string,isPriority=false)=>request<ApiTicket>('/api/queue',{method:'POST',body:JSON.stringify({service,purpose,isPriority})}),
  next:(office:string,window:number)=>request<ApiTicket|null>(`/api/queue/${encodeURIComponent(office)}/next`,{method:'POST',body:JSON.stringify({window})}),
  complete:(n:number,window:number)=>request<any>(`/api/queue/${n}/complete`,{method:'POST',body:JSON.stringify({window})}),
  transfer:(n:number,targetService:string,reason:string,actor:string,window?:number)=>request<any>('/api/queue/transfer',{method:'POST',body:JSON.stringify({ticketNumber:n,targetService,reason,actor,window})}),
};
