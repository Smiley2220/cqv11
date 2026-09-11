'use client';
import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '../../lib/auth';

export default function Login(){
 const {login}=useAuth(); const router=useRouter(); const [username,setUsername]=useState(''); const [password,setPassword]=useState(''); const [error,setError]=useState(''); const [busy,setBusy]=useState(false);
 async function submit(e:FormEvent){e.preventDefault();setError('');setBusy(true);const ok=await login(username,password);setBusy(false);if(!ok){setError('Invalid username or password.');return;}try{const session=JSON.parse(localStorage.getItem('campusq-session')||'null');router.replace(session?.role==='Admin'?'/admin':'/staff');}catch{router.replace('/staff');}}
 return <div className="container login-page"><div className="login-card"><div className="login-brand"><img src="/campusq-logo.png"/><div><div className="eyebrow">CampusQ secure access</div><h1>Welcome to CampusQ</h1></div></div><p className="muted">Sign in to open your staff workspace and manage campus queues.</p><form onSubmit={submit}><div className="field"><label>Username</label><input className="input" value={username} onChange={e=>setUsername(e.target.value)} autoComplete="username" required /></div><div className="field" style={{marginTop:16}}><label>Password</label><input className="input" type="password" value={password} onChange={e=>setPassword(e.target.value)} autoComplete="current-password" required /></div>{error&&<div className="error-box">{error}</div>}<button className="btn btn-primary login-submit" disabled={busy}>{busy?'Signing in…':'Sign in'}</button></form><div className="demo-box"><b>Demo accounts</b><br/>Admin: <code>admin / admin123 (change in production)</code><br/>Staff: <code>staff / staff123 (change in production)</code></div></div></div>
}
