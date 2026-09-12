'use client';
<<<<<<< HEAD
import {useEffect,useState} from 'react';
import Link from 'next/link';
import QRCode from 'qrcode';
import {Office,offices,purposes,Ticket} from '../../lib/data';
import {issueTicket} from '../../lib/store';

const officeInfo:Record<Office,{icon:string;description:string}>={
 Registrar:{icon:'▣',description:'Student records, documents, and inquiries'},
 Cashier:{icon:'₱',description:'Tuition payments and financial transactions'},
 Admission:{icon:'◈',description:'Enrollment and admission services'},
};

const serviceInfo:Record<string,string>={
 Enrollment:'Enrollment and registration support',
 Credentials:'Request official school documents',
 'Other Inquiries':'Questions about student records',
 'Tuition Fee':'Pay tuition and school fees',
 'Miscellaneous Fee':'Pay other campus charges',
 'Other Payments':'Questions about your payment',
 'Application Status':'Check your admission application',
 'Document Verification':'Verify admission documents',
 'General Inquiry':'Questions about admission',
};

export default function Kiosk(){
 const [office,setOffice]=useState<Office|null>(null);
 const [purpose,setPurpose]=useState<string|null>(null);
 const [priority,setPriority]=useState(false);
 const [ticket,setTicket]=useState<Ticket|null>(null);
 const [busy,setBusy]=useState(false);
 const [qrCode,setQrCode]=useState<string|null>(null);
 const [qrError,setQrError]=useState(false);
 function choose(o:Office){setOffice(o);setPurpose(null);setPriority(false)}
 async function generate(){if(!office||!purpose)return;setBusy(true);try{setTicket(await issueTicket(office,purpose,priority))}catch{alert('Unable to connect to CampusQ API. Check NEXT_PUBLIC_API_BASE_URL.')}finally{setBusy(false)}}
 useEffect(()=>{if(!ticket)return;let cancelled=false;setQrCode(null);setQrError(false);const url=`${window.location.origin}/queue/${ticket.id}`;QRCode.toDataURL(url,{errorCorrectionLevel:'H',width:280,margin:4,color:{dark:'#15301c',light:'#ffffff'}}).then(code=>{if(!cancelled)setQrCode(code)}).catch(()=>{if(!cancelled)setQrError(true)});return()=>{cancelled=true}},[ticket]);
 if(ticket)return <div className="container kiosk-shell"><div className="ticket generated-ticket"><div className="eyebrow">Ticket generated</div><div className="ticket-number">{ticket.label}</div><h2>{ticket.office}</h2><p className="muted">{ticket.purpose}</p><div className="panel ticket-summary"><div className="muted">Your queue position</div><div className="big-stat">#{ticket.id}</div><div className="login-note">Keep this number for your visit.</div></div><div className="ticket-qr" aria-live="polite">{qrCode?<img src={qrCode} alt={`QR code for queue ticket ${ticket.label}`} />:qrError?<div className="qr-error">QR code unavailable. Please use the ticket number to check the queue.</div>:<div className="qr-loading">Preparing your QR code…</div>}<b>Scan to follow your queue online</b><span>View your position and estimated waiting time in real time.</span></div><div className="actions ticket-actions" style={{justifyContent:'center'}}><button className="btn btn-outline" onClick={()=>window.print()}>Print Ticket</button><Link className="btn btn-primary" href={`/queue/${ticket.id}`}>Open Virtual Queue</Link><button className="btn btn-outline" onClick={()=>{setTicket(null);setOffice(null);setPurpose(null);setQrCode(null);setQrError(false)}}>New Ticket</button></div></div></div>;
 const step=purpose?3:office?2:1;
 return <div className="container kiosk-shell">
  <div className="kiosk-heading">
   <div><div className="eyebrow">Self-service kiosk</div><h1>Join the Campus Queue</h1><p className="muted">Choose a service desk and purpose to get your queue ticket.</p></div>
   <div className="kiosk-progress" aria-label={`Step ${step} of 3`}>
	<div className={step>=1?'current':''}><span>01</span><b>Choose Desk</b></div><i/><div className={step>=2?'current':''}><span>02</span><b>Choose Service</b></div><i/><div className={step>=3?'current':''}><span>03</span><b>Get Ticket</b></div>
   </div>
  </div>
  <div className="kiosk-panel">
   <section className="kiosk-step" aria-labelledby="desk-heading"><div className="kiosk-step-title"><span className="step-number">1</span><div><div className="eyebrow">Step one</div><h2 id="desk-heading">Choose a service desk</h2></div></div><div className="kiosk-office-grid">{offices.map(o=><button type="button" className={`kiosk-choice office-choice ${office===o?'active':''}`} aria-pressed={office===o} onClick={()=>choose(o)} key={o}><span className="kiosk-choice-top"><span className="kiosk-icon" aria-hidden="true">{officeInfo[o].icon}</span><span className="kiosk-choice-mark" aria-hidden="true">{office===o?'✓':'›'}</span></span><b>{o}</b><span>{officeInfo[o].description}</span><small>{purposes[o].length} services available</small></button>)}</div></section>
   <section className="kiosk-step" aria-labelledby="service-heading"><div className="kiosk-step-title"><span className="step-number">2</span><div><div className="eyebrow">Step two</div><h2 id="service-heading">Choose a service</h2></div></div><div className="kiosk-purpose-grid">{office?purposes[office].map(p=><button type="button" className={`kiosk-choice service-choice ${purpose===p?'active':''}`} aria-pressed={purpose===p} onClick={()=>setPurpose(p)} key={p}><span className="kiosk-choice-mark" aria-hidden="true">{purpose===p?'✓':'›'}</span><b>{p}</b><span>{serviceInfo[p]||'Join this service line'}</span></button>):<div className="kiosk-empty"><span className="kiosk-empty-icon" aria-hidden="true">1</span><b>Choose a service desk above</b><span>Available services will appear here.</span></div>}</div></section>
   <section className="priority-card" aria-label="Priority queue option"><div className="priority-icon" aria-hidden="true">+</div><div><b>Priority Queue</b><span>For eligible students only</span></div><button type="button" className={`priority-toggle ${priority?'active':''}`} role="switch" aria-checked={priority} aria-label="Priority Queue" disabled={!office||!purpose} onClick={()=>setPriority(!priority)}><span/></button><span className="priority-info" title="Priority service is available only to eligible students." aria-label="Priority service is for eligible students only">i</span></section>
   <div className="kiosk-footer"><div className="kiosk-summary"><span className="summary-label">Your selection</span>{office&&purpose?<><b>{office}</b><span>{purpose}{priority?' · Priority':''}</span></>:<span>Complete the steps above</span>}</div><button disabled={busy||!office||!purpose} className="btn btn-primary kiosk-submit" onClick={generate}>{busy?'Generating…':office&&purpose?'Generate Queue Ticket →':'Complete your selections'}</button></div>
  </div>
 </div>;
}
=======
import {useState} from 'react'; import Link from 'next/link'; import {Office,offices,purposes,Ticket} from '../../lib/data'; import {issueTicket} from '../../lib/store';
export default function Kiosk(){const [office,setOffice]=useState<Office>('Registrar');const [purpose,setPurpose]=useState(purposes.Registrar[0]);const [priority,setPriority]=useState(false);const [ticket,setTicket]=useState<Ticket|null>(null);const [busy,setBusy]=useState(false);function choose(o:Office){setOffice(o);setPurpose(purposes[o][0]);setPriority(false)}async function generate(){setBusy(true);try{setTicket(await issueTicket(office,purpose,priority))}catch(e){alert('Unable to connect to CampusQ API. Check NEXT_PUBLIC_API_BASE_URL.')}finally{setBusy(false)}}if(ticket)return <div className="container"><div className="ticket"><div className="eyebrow">Ticket generated</div><div className="ticket-number">{ticket.label}</div><h2>{ticket.office}</h2><p className="muted">{ticket.purpose}</p><div className="panel"><div className="muted">Your queue ticket</div><div className="big-stat">#{ticket.id}</div><div className="login-note">Print this ticket and scan its QR code to follow the queue online.</div></div><div className="actions" style={{justifyContent:'center'}}><Link className="btn btn-primary" href={`/queue/${ticket.id}`}>Open virtual queue</Link><button className="btn btn-outline" onClick={()=>setTicket(null)}>New ticket</button></div></div></div>;return <div className="container"><div className="section-head"><div><div className="eyebrow">Self-service kiosk</div><h2>Select a campus service</h2><div className="muted">Choose the office and purpose you need.</div></div></div><div className="panel"><div className="field"><label>1. Office</label><div className="office-grid">{offices.map(o=><div className={`select-card ${office===o?'active':''}`} onClick={()=>choose(o)} key={o}><b>{o}</b><div className="muted">{purposes[o].length} services</div></div>)}</div></div><div className="field" style={{marginTop:28}}><label>2. Purpose</label><div className="purpose-grid">{purposes[office].map(p=><div className={`select-card ${purpose===p?'active':''}`} onClick={()=>setPurpose(p)} key={p}><b>{p}</b></div>)}</div></div><label className="priority-option"><input type="checkbox" checked={priority} onChange={e=>setPriority(e.target.checked)}/> Priority queue</label><div style={{marginTop:28,display:'flex',justifyContent:'space-between',alignItems:'center',gap:16,flexWrap:'wrap'}}><div className="login-note">Selected: <b>{office}</b> · {purpose} {priority && '· Priority'}</div><button disabled={busy} className="btn btn-primary" onClick={generate}>{busy?'Generating…':'Generate Queue Ticket'}</button></div></div></div>}
>>>>>>> 65cc0f2b8bdde2305c4ecce9125bfa8559279964
