'use client';
import { createContext, useContext, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

export type Role = 'Admin' | 'Staff';
export type Session = { username:string; role:Role; office?:string; accessToken:string; expiresAt?:string };
const KEY='campusq-session';
const API=(process.env.NEXT_PUBLIC_API_BASE_URL||'').replace(/\/$/,'');

type AuthContextValue={session:Session|null;loading:boolean;login:(u:string,p:string)=>Promise<boolean>;logout:()=>void};
const AuthContext=createContext<AuthContextValue|null>(null);

export function AuthProvider({children}:{children:React.ReactNode}){
 const [session,setSession]=useState<Session|null>(null); const [loading,setLoading]=useState(true);
 useEffect(()=>{try{const raw=localStorage.getItem(KEY); if(raw){const s=JSON.parse(raw) as Session; if(!s.expiresAt||new Date(s.expiresAt)>new Date())setSession(s);else localStorage.removeItem(KEY);}}catch{} setLoading(false)},[]);
 async function login(username:string,password:string){
  const r=await fetch(`${API}/api/auth/login`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username,password}),cache:'no-store'});
  if(!r.ok)return false; const data=await r.json(); const next:Session={username:data.username,role:data.role,office:data.office,accessToken:data.accessToken,expiresAt:data.expiresAt}; localStorage.setItem(KEY,JSON.stringify(next));setSession(next);return true;
 }
 function logout(){localStorage.removeItem(KEY);setSession(null)}
 return <AuthContext.Provider value={{session,loading,login,logout}}>{children}</AuthContext.Provider>;
}
export function useAuth(){const v=useContext(AuthContext);if(!v)throw new Error('useAuth must be used inside AuthProvider');return v;}
export function ProtectedRoute({children,roles}:{children:React.ReactNode;roles:Role[]}){const {session,loading}=useAuth();const router=useRouter();useEffect(()=>{if(!loading&&(!session||!roles.includes(session.role)))router.replace('/login')},[loading,session,router,roles]);if(loading||!session||!roles.includes(session.role))return <div className="container"><div className="panel"><div className="muted">Checking access…</div></div></div>;return <>{children}</>}
export function getAccessToken(){try{const raw=localStorage.getItem(KEY);return raw?(JSON.parse(raw) as Session).accessToken:null}catch{return null}}
