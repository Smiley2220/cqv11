'use client';
import {useCallback,useEffect,useState} from 'react';
import {HubConnectionBuilder,HubConnection} from '@microsoft/signalr';
import {api} from '../lib/api';
import {mapApiTicket,Ticket} from '../lib/data';

export function useTickets(interval=5000, office?:string){
 const [tickets,setTickets]=useState<Ticket[]>([]);
 const refresh=useCallback(async()=>{try{const data=office?await api.office(office):await api.queue();setTickets(data.map(mapApiTicket))}catch{}},[office]);
 useEffect(()=>{refresh();let connection:HubConnection|undefined;const id=setInterval(refresh,interval);const base=process.env.NEXT_PUBLIC_API_BASE_URL;
  if(base){connection=new HubConnectionBuilder().withUrl(`${base.replace(/\/$/,'')}/hubs/queue`).withAutomaticReconnect().build();connection.on('queueChanged',refresh);connection.on('ticketChanged',refresh);connection.start().then(()=>office?connection?.invoke('SubscribeToOffice',office):undefined).catch(()=>{});}
  return()=>{clearInterval(id);connection?.stop()}
 },[refresh,interval,office]);
 return tickets;
}
