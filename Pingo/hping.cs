using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace Pingo
{

	public class HPing
	{
		[DllImport("kernel32.dll")]
		extern static short QueryPerformanceCounter(ref long x);
		[DllImport("kernel32.dll")]
		extern static short QueryPerformanceFrequency(ref long x);

		private		long	freq=10000000;
		private		Socket socket;
		private 	IPEndPoint target=null;
		private 	EndPoint remoteEp=null;
		private		UInt16	pid=0;
		private 	int		oid;
		private		UInt16	sq=0;
		private static string[] error_message={
			"No Error",
			"Host not Found",
			"Not Initialized",
			"SendError",
			"Size too short"
		};
		private static int objectID=1;
		
		public int Recv(ref int sq, ref uint delay){
			Byte[] 		rbuff=new Byte[4000];
			int			nBytes=0;
			IPEndPoint	remoteIP;
			int			rest;//milli seconds
			int			start;
			int			current;
			uint		recv_time;
			int			ret_code;
			long		recv_ticks=0;
			
			ret_code=0;
			start=System.Environment.TickCount;
			for(rest=1000;;){
				current=System.Environment.TickCount;
				rest=rest-(current-start);
				if(rest<0) {
					//Console.WriteLine("Timeout");
					ret_code=-1005;
					break;
				}
				IAsyncResult ar=socket.BeginReceiveFrom(
					rbuff,0,4000,SocketFlags.None, ref remoteEp,null,null);
				bool result=ar.AsyncWaitHandle.WaitOne(rest,false);
				QueryPerformanceCounter(ref recv_ticks);
				recv_time= (uint)(recv_ticks*1000000.0/freq);
				//Console.WriteLine("RecvTime:{0} ",recv_time);
				if(result==true ){
					nBytes=socket.EndReceiveFrom(ar,ref remoteEp);
					if(nBytes>0){
						/*
						Console.WriteLine("OID:{0}:Recv:{1}Bytes {2}",
										this.oid,nBytes,this.remoteEp);
						*/
						//Check Peer End Point
						if(remoteEp.AddressFamily!=
									AddressFamily.InterNetwork) {
							//Console.WriteLine("Not IP");
							continue;
						}
						remoteIP=(IPEndPoint)remoteEp;
						if(remoteIP.Address.Equals(target.Address)==false){
							//Console.WriteLine("Not Target Reply");
							continue;
						}
						/* Data Dump
						for(int i=0; i<36;i++){
							Console.Write("{0} ",Convert.ToString(rbuff[i],16));
						}
						//Console.WriteLine("");
						*/
						//Convert ICMP
						Byte[] rdata=new Byte[nBytes];
						Array.Copy(rbuff,0,rdata,0,nBytes);
						ICMP icmp=ICMP.ToICMP(rdata);
						//Check ICMP Contents
						/*
						Console.WriteLine(
							"Recv:id={0},sq={1},oid={2},send_time={3}",
							icmp.id,icmp.sq,icmp.oid,icmp.send_time);
						*/
						//Check id
						if(icmp.id!=this.pid){
							//Console.WriteLine("PID Unmatch");
							continue;
						}
						if(icmp.oid!=this.oid){
							//Console.WriteLine("OID Unmatch");
							continue;
						}
						//Sequence  Number Check
						sq=icmp.sq;
						//This is what we are searching
						if(recv_time<icmp.send_time){
							delay=0xFFFFFFFF-icmp.send_time+recv_time;
						}else{
							delay=recv_time-icmp.send_time;
						}
						/*
						Console.WriteLine("delay={0}-{1}={2}",
								recv_time,
								icmp.send_time,
								delay);
						*/
						break;
					}	
				}else{
					ret_code = -1006;
					break;
				}
			}
			return ret_code;
		}

        public int Send()
        {
            return Send(24);
        }

		public int Send(int lsize){
			long	send_ticks=0;
			Byte[] 	sbuff;
			
			if(lsize<=8) return -1004;
			if(target==null) return -1002;
			ICMP icmp=new ICMP(lsize);
			icmp.id=pid;
			icmp.sq=sq++;
			if(icmp.sq>=1000) sq=1;
			QueryPerformanceCounter(ref send_ticks);
			icmp.send_time=(uint)(send_ticks*1000000.0/freq);//milisec
			//Console.Write("SendTime:{0} ",icmp.send_time);
			icmp.oid=oid;
			sbuff=icmp.ToBuffer();
			//
			/*
			Console.WriteLine("Send:id={0},sq={1},oid={2},send_time={3}",
					icmp.id,icmp.sq,icmp.oid,icmp.send_time);
			*/
			int nBytes=socket.SendTo(sbuff,sbuff.Length,0,target);
			if(nBytes!=sbuff.Length){
				return -1003;
			}
			return 0;
		}

		public static string GetError(int retcode){
			int index=-retcode -1000;
			if(index<0 || index>10){
				return "code invalid";
			}else {
				return error_message[index];
			}	
		}
		public static HPing ObjectFactory(string host){
			HPing  ping=new HPing();
            try
            {
                ping.socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Raw,
                ProtocolType.Icmp);
                ping.socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.SendTimeout,
                    1000);
            }
            catch (Exception)
            {
                MessageBox.Show("Raw Socket Creation failed, Are you a super-user?", "Error", 
                    MessageBoxButtons.OK,MessageBoxIcon.Error);
                return null;
            }
			try{
				ping.target= new IPEndPoint(
						Dns.GetHostEntry(host).AddressList[0],0);
                string hn=Dns.GetHostName();
                IPHostEntry en = Dns.GetHostEntry(hn);
                IPAddress ipa=null;
                foreach (IPAddress ip in en.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ipa = ip;
                        break;
                    }
                }
                ping.remoteEp = new IPEndPoint(en.AddressList[5], 0);
				ping.socket.Bind(ping.remoteEp);
			}catch(Exception){
                MessageBox.Show("Failed Bind,Is the Address sure?", "Error",
                   MessageBoxButtons.OK, MessageBoxIcon.Error);
				return null;
			}	
			ping.pid=(UInt16)(Process.GetCurrentProcess().Id);
			ping.oid=objectID++;
			QueryPerformanceFrequency(ref ping.freq);
			return ping;
		}
        public string GetTargetAddress()
        {
            if (target == null) return null;
            return target.Address.ToString();
        }

        /*
		static void Main(string[] args)
		{	
			int	sq=0;
			uint delay=0;
			
			HPing ping=HPing.ObjectFactory(args[0]);
			if(ping==null ){
				Console.WriteLine("Err:{0}",HPing.GetError(-1001));
			}
			for(int i=0;i<10;i++){
				ping.Send(32);
				ping.Recv(ref sq, ref delay);
				if(i==0) continue;
				Console.WriteLine("{0} {1} ",sq,delay);
				System.Threading.Thread.Sleep(100);
			}
		}
        */
	}

	class ICMP
    {
		public Byte type;
		public Byte subcode;
		public UInt16   checkSum;
		public UInt16   id;
		public UInt16   sq;
		public int body_size;
		public int	oid;
		public uint send_time;
		public Byte[]   body;


		public ICMP(){
			type=0;
			subcode=0;
			checkSum=0;
			id=0;
			sq=0;
		}

		public ICMP(int lsize){
			type=8;
			subcode=0;
			checkSum=0;
			id=0;
			sq=0;
			body_size=lsize;
			body=new Byte[lsize];
			for(int i=0;i<lsize;i++){
				body[i]=(Byte)(0xA5);
			}
		}	  
		
		private  Byte[] packetize(ICMP icmp)
		{
			Byte[] buff=new Byte[icmp.body_size+8];
			//Type
			buff[0]=icmp.type;
			//SubCode
			buff[1]=icmp.subcode;
			//CheckSum
			buff[2]=(Byte)(icmp.checkSum>>8);
			buff[3]=(Byte)(icmp.checkSum&0xFF);
			//ID
			buff[4]=(Byte)(icmp.id>>8);
			buff[5]=(Byte)(icmp.id&0xFF);
			//Sequence
			buff[6]=(Byte)(icmp.sq>>8);
			buff[7]=(Byte)(icmp.sq&0xFF);
			//Data
			Array.Copy(icmp.body,0,buff,8,icmp.body_size);
			//OID
			buff[8]=(Byte)(icmp.oid>>24);
			buff[9]=(Byte)(icmp.oid>>16);
			buff[10]=(Byte)(icmp.oid>>8);
			buff[11]=(Byte)(icmp.oid&0xFF);
			//TimeTick
			buff[12]=(Byte)(icmp.send_time>>24);
			buff[13]=(Byte)(icmp.send_time>>16);
			buff[14]=(Byte)(icmp.send_time>>8);
			buff[15]=(Byte)(icmp.send_time&0xFF);
			return buff;
		}

		private  UInt16 checksum(Byte[] buff){
			int ssize=buff.Length/2;
			int cksum=0;

			for(int i=0;i<ssize;i++){
				cksum+=(int)((buff[i*2]<<8)+buff[i*2+1]);//Watch Order
			}
			if(ssize*2 != buff.Length){//Odd Number Length
				cksum+=(int)(buff[buff.Length-1]<<8);//Adds the Finale Byte
			}
			cksum=(cksum>>16)+(cksum&0xFFFF);
			cksum+=(cksum>>16);
			return (UInt16)(~cksum);
		}

		public  Byte[] ToBuffer(){
			Byte[] buff=packetize(this);
			this.checkSum=checksum(buff);
			buff=packetize(this);
			return buff;
		}
		public  static ICMP ToICMP(Byte[] rdata){
			if(rdata.Length<36) return null;
			ICMP icmp=new ICMP();
			//deCode
			icmp.type=rdata[20];
			icmp.subcode=rdata[21];
			icmp.checkSum=(UInt16)((rdata[22]<<8)+rdata[23]);
			icmp.id=(UInt16)((rdata[24]<<8)+rdata[25]);
			icmp.sq=(UInt16)((rdata[26]<<8)+rdata[27]);
			icmp.body_size=0;
			icmp.body=null;
			icmp.oid=(rdata[28]<<24)+(rdata[29]<<16)+(rdata[30]<<8)+rdata[31];
			icmp.send_time=(uint)((rdata[32]<<24)+(rdata[33]<<16)
				+(rdata[34]<<8)+rdata[35]);
			return icmp;
		}
	}
}
