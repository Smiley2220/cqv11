'use client';
import {useState} from 'react';
import {useAuth} from '../../lib/auth';
import {ProtectedRoute} from '../../lib/auth';
import {Office,offices,windows} from '../../lib/data';
import {callNext,complete} from '../../lib/store';
import {api} from '../../lib/api';
import {useTickets} from '../../components/ClientShell';
import QueueCard from '../../components/QueueCard';

function Staff(){
 const {session}=useAuth(); const [office,setOffice]=useState<Office>((session?.office as Office)||'Registrar');
 const [win,setWin]=useState(1);
 const [busy,setBusy]=useState(false); const [transferTo,setTransferTo]=useState<Office>('Registrar'); const [reason,setReason]=useState('');
 const tickets=useTickets(2500);
 const waiting=tickets.filter(x=>x.office===office&&x.status==='Waiting').sort((a,b)=>Number(b.priority)-Number(a.priority)||a.id-b.id);
 const current=tickets.find(x=>x.office===office&&x.status==='Serving'&&x.window===win);
 async function next(){
   if(current){alert(`Window ${win} is currently serving ${current.label}. Complete it first.`);return;}
   setBusy(true);try{const t=await callNext(office,win);if(!t) alert('No waiting customers for this office.')}catch(e){alert(e instanceof Error?e.message:'API connection failed.')}finally{setBusy(false)}
 }
 async function transfer(){ if(!current)return; if(transferTo===office){alert('Choose a different office.');return;} if(!reason.trim()){alert('Enter a transfer reason.');return;} setBusy(true);try{await api.transfer(current.id,transferTo,reason.trim(),'',win);setReason('');alert(`Ticket transferred to ${transferTo}.`)}catch(e){alert(e instanceof Error?e.message:'Unable to transfer ticket.')}finally{setBusy(false)}}
 async function finish(){
   if(!current)return;
   setBusy(true);try{await complete(current.id,win)}catch(e){alert(e instanceof Error?e.message:'Unable to complete ticket.')}finally{setBusy(false)}
 }
 return <div className="container">
  <div className="section-head"><div><div className="eyebrow">Staff workspace</div><h2>{office} queue</h2><div className="muted">Window {win} · Live queue controls</div></div></div>
  <div className="panel">
   <div className="actions" style={{marginTop:0}}>{(session?.role==='Admin'?offices:[office]).map(o=><button key={o} className={`btn ${office===o?'btn-primary':'btn-outline'}`} onClick={()=>{setOffice(o);setWin(1)}}>{o}</button>)}</div>
   <div className="actions">
    <select className="input" value={win} onChange={e=>setWin(Number(e.target.value))}>{windows[office].map(w=><option key={w} value={w}>Window {w}</option>)}</select>
    <button disabled={busy||!!current} className="btn btn-primary" onClick={next}>{busy?'Calling…':'Call Next'}</button>
    <button disabled={busy||!current} className="btn btn-outline" onClick={finish}>{busy?'Updating…':'Complete Ticket'}</button><select className="input" value={transferTo} onChange={e=>setTransferTo(e.target.value as Office)}>{offices.filter(o=>o!==office).map(o=><option key={o} value={o}>Transfer to {o}</option>)}</select><input className="input" placeholder="Transfer reason" value={reason} onChange={e=>setReason(e.target.value)} disabled={!current}/><button disabled={busy||!current} className="btn btn-outline" onClick={transfer}>Transfer</button>
   </div>
  </div>
  <div className="grid cards"><div className="card"><div className="muted">Waiting</div><div className="big-stat">{waiting.length}</div></div><div className="card"><div className="muted">Current</div><div className="big-stat">{current?.label??'—'}</div></div><div className="card"><div className="muted">Window</div><div className="big-stat">W{win}</div></div></div>
  <div className="section-head"><div><h2>Waiting list</h2><div className="muted">Updates automatically from the CampusQ API.</div></div></div>
  <div className="panel">{waiting.length?waiting.map(t=><QueueCard t={t} key={t.id}/>):<div className="muted">No customers are currently waiting.</div>}</div>
 </div>
}

export default function ProtectedStaff(){return <ProtectedRoute roles={['Admin','Staff']}><Staff/></ProtectedRoute>}
