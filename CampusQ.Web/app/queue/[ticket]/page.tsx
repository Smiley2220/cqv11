'use client';
import Link from 'next/link';
import {useParams} from 'next/navigation';
import {useEffect,useState} from 'react';
import {HubConnectionBuilder} from '@microsoft/signalr';
import {api,TicketStatus} from '../../../lib/api';

export default function QueuePage(){
 const {ticket}=useParams<{ticket:string}>(); const id=Number(ticket); const [s,setS]=useState<TicketStatus|null>(null); const [err,setErr]=useState(false);
 useEffect(()=>{let mounted=true;let connection:any;const load=async()=>{try{const next=await api.status(id);if(mounted)setS(next);setErr(false)}catch{if(mounted)setErr(true)}};load();const x=setInterval(load,5000);const base=process.env.NEXT_PUBLIC_API_BASE_URL;
  if(base){connection=new HubConnectionBuilder().withUrl(`${base.replace(/\/$/,'')}/hubs/queue`).withAutomaticReconnect().build();connection.on('ticketChanged',load);connection.on('queueChanged',load);connection.start().then(()=>connection.invoke('SubscribeToTicket',id)).catch(()=>{});}
  return()=>{mounted=false;clearInterval(x);connection?.stop()}
 },[id]);
 if(err)return <div className="container"><div className="ticket"><h2>Unable to connect</h2><p className="muted">The CampusQ API is unavailable. Please try again.</p><Link className="btn btn-outline" href="/">Back</Link></div></div>;
 if(!s)return <div className="container"><div className="ticket"><h2>Loading queue…</h2></div></div>;
 if(s.state==='NotFound')return <div className="container"><div className="ticket"><h2>Queue ticket not found</h2><Link className="btn btn-primary" href="/kiosk">Get a new ticket</Link></div></div>;
 const label=s.state==='Serving'?'Now':s.state==='Completed'?'✓':s.positionInLine;
 return <div className="container"><div className="ticket"><div className="eyebrow">Virtual queue</div><div className="ticket-number">{s.ticketLabel}</div><p className="muted">{s.service} · {s.purpose}</p><div className="stat-grid"><div className="card"><div className="muted">Position</div><div className="big-stat">{label}</div></div><div className="card"><div className="muted">Estimated wait</div><div className="big-stat">{s.state==='Completed'||s.state==='Serving'?0:s.estimatedWaitMinutes}m</div></div><div className="card"><div className="muted">People ahead</div><div className="big-stat">{s.peopleAhead}</div></div><div className="card"><div className="muted">Status</div><div style={{marginTop:8}} className={`status ${s.state.toLowerCase()}`}>{s.state}</div></div></div><div className="panel" style={{marginTop:18,textAlign:'left'}}><b>{s.state==='Serving'?'Please proceed to the service window.':s.state==='Completed'?'Your transaction is complete.':'Live queue tracking'}</b><p className="muted">{s.state==='Waiting'?`There ${s.peopleAhead===1?'is':'are'} ${s.peopleAhead} ${s.peopleAhead===1?'person':'people'} ahead of you. This page updates automatically.`:'CampusQ is keeping this ticket status synchronized with the office queue.'}</p></div><Link className="btn btn-outline" href="/">Back to CampusQ</Link></div></div>
}
