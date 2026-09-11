export type Office = 'Registrar'|'Cashier'|'Admission';
export type Status = 'Waiting'|'Serving'|'Completed';
export type Ticket = {id:number;label:string;office:Office;purpose:string;status:Status;position:number;created:string;window?:number;priority?:boolean};
export const offices:Office[]=['Registrar','Cashier','Admission'];
export const purposes:Record<Office,string[]>={Cashier:['Tuition Fee','Miscellaneous Fee','Other Payments'],Registrar:['Enrollment','Credentials','Other Inquiries'],Admission:['Application Status','Document Verification','General Inquiry']};
export const windows:Record<Office,number[]>={Cashier:[1,2,3,4],Registrar:[1,2,3,4],Admission:[1,2]};
export const seedTickets:Ticket[]=[];
export function prefix(office:Office,purpose:string){return office[0]+purpose.replace(/[^A-Za-z]/g,'')[0].toUpperCase();}
export function mapApiTicket(t:any):Ticket{return {id:t.ticketNumber,label:t.ticketLabel,office:t.service as Office,purpose:t.purpose,status:(t.status==='Serving'?'Serving':t.status==='Completed'?'Completed':'Waiting') as Status,position:0,created:new Date(t.timeAdded).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'}),window:t.windowNumber??undefined,priority:Boolean(t.isPriority)};}
