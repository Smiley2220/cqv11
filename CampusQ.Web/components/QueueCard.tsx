import {Ticket} from '../lib/data';
export default function QueueCard({t}:{t:Ticket}){return <div className="queue-row"><div className="queue-number">{t.position||'—'}</div><div className="queue-main"><b>{t.label} {t.priority && <small className="priority-badge">PRIORITY</small>}</b><span>{t.purpose}</span></div><span className={`status ${t.status.toLowerCase()}`}>{t.status}</span></div>}
