// Isolated no-OBS hardware prototype. Does not register cameras or record video.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

internal static class DirectUsbProbe
{
    internal sealed class LiveOptions {
        internal CancellationToken Cancellation;
        internal Action<string> Log;
        internal Action<Exception> Error;
        internal Action<IntPtr,int> Decoded;
    }
    [DllImport("PhoneCameraNative.dll", CallingConvention=CallingConvention.Cdecl)]
    static extern int PhoneDecoderCreate(int width, int height, out IntPtr decoder);
    [DllImport("PhoneCameraNative.dll", CallingConvention=CallingConvention.Cdecl)]
    static extern int PhoneDecoderFeed(IntPtr decoder, byte[] packet, int size, long pts, out int frames);
    [DllImport("PhoneCameraNative.dll", CallingConvention=CallingConvention.Cdecl)]
    static extern void PhoneDecoderClose(IntPtr decoder);
    [DllImport("PhoneCameraNative.dll", CallingConvention=CallingConvention.Cdecl)]
    static extern ulong PhoneDecoderFingerprint(IntPtr decoder);
    [DllImport("PhoneCameraNative.dll", CallingConvention=CallingConvention.Cdecl)]
    static extern void PhoneDecoderTimings(IntPtr decoder,[Out] double[] times);

    static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
    static Process Start(string file, string args) {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(file, args) {
            UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardOutput=true, RedirectStandardError=true
        };
        process.Start(); return process;
    }
    static string Run(string file, string args) {
        using(Process process=Start(file,args)) {
            var output=process.StandardOutput.ReadToEndAsync();
            var error=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(12000)) { process.Kill(); throw new TimeoutException("ADB command timed out."); }
            if(process.ExitCode!=0) throw new IOException("ADB failed: " + error.Result);
            return output.Result.Trim();
        }
    }
    static byte[] Read(Stream stream,int size) {
        if(size<0 || size>32*1024*1024) throw new IOException("Invalid packet size.");
        byte[] data=new byte[size]; int offset=0;
        while(offset<size) { int n=stream.Read(data,offset,size-offset); if(n==0) throw new EndOfStreamException("Phone video stream ended."); offset+=n; }
        return data;
    }
    static uint U32(byte[] data,int offset) { return (uint)data[offset]<<24 | (uint)data[offset+1]<<16 | (uint)data[offset+2]<<8 | data[offset+3]; }
    static ulong U64(byte[] data) { return (ulong)U32(data,0)<<32 | U32(data,4); }
    static void Check(int result) { if(result<0) Marshal.ThrowExceptionForHR(result); }

    static int Main(string[] args) { return RunSession(args,null); }
    internal static int RunSession(string[] args,LiveOptions live) {
        Action<string> write=live==null ? new Action<string>(Console.WriteLine) : live.Log;
        CancellationToken cancellation=live==null ? CancellationToken.None : live.Cancellation;
        if(args.Length<1) { Console.Error.WriteLine("Usage: DirectUsbProbe.exe <scrcpy-directory> [seconds=12] [width=1920] [height=1080] [camera-id=0]"); return 2; }
        string runtime=Path.GetFullPath(args[0]);
        string adb=Path.Combine(runtime,"adb.exe");
        string server=Path.Combine(runtime,"scrcpy-server");
        int seconds=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture):12;
        int width=args.Length>2?int.Parse(args[2],CultureInfo.InvariantCulture):1920;
        int height=args.Length>3?int.Parse(args[3],CultureInfo.InvariantCulture):1080;
        string camera=args.Length>4?args[4]:"0";
        if((live==null && (seconds<4 || seconds>120)) || width<320 || height<240 || width>3840 || height>2160 || !Regex.IsMatch(camera,"^[0-9]{1,3}$")) return 2;
        string serial=null, port=null;
        string scid=(new Random().Next(1,int.MaxValue)).ToString("x8");
        string remote="/data/local/tmp/phone-usb-camera-"+scid+".jar";
        Process shell=null; TcpClient socket=null; IntPtr decoder=IntPtr.Zero;
        bool pushed=false, ownsProducer=false;
        Mutex producer=new Mutex(false,"Local\\PhoneUsbCamera.v3.Producer");
        CancellationTokenRegistration cancelRegistration=cancellation.Register(delegate { try { if(socket!=null) socket.Close(); } catch(ObjectDisposedException) {} });
        try {
            cancellation.ThrowIfCancellationRequested();
            try { ownsProducer=producer.WaitOne(0); } catch(AbandonedMutexException) { ownsProducer=true; }
            if(!ownsProducer) throw new InvalidOperationException("Another independent camera session is running.");
            if(Process.GetProcessesByName("scrcpy").Length>0) throw new InvalidOperationException("Stop the existing phone camera session first; this test will not stop it.");
            string devices=Run(adb,"devices -l");
            foreach(string line in devices.Split('\n')) {
                Match m=Regex.Match(line,@"^([A-Za-z0-9._-]+)\s+device\s+.*\bmodel:");
                if(!m.Success) continue;
                if(serial!=null) throw new InvalidOperationException("Connect only one USB phone for this prototype.");
                serial=m.Groups[1].Value;
            }
            if(serial==null) throw new IOException("No authorized USB phone found.");
            Run(adb,"-s "+Quote(serial)+" push "+Quote(server)+" "+remote); pushed=true;
            port=Run(adb,"-s "+Quote(serial)+" forward tcp:0 localabstract:scrcpy_"+scid);
            int portNumber;
            if(!int.TryParse(port,out portNumber) || portNumber<1 || portNumber>65535) throw new IOException("Invalid ADB port.");
            string command="-s "+Quote(serial)+" shell CLASSPATH="+remote+" app_process / com.genymobile.scrcpy.Server 4.1"+
                " scid="+scid+" tunnel_forward=true audio=false control=false cleanup=false"+
                " send_device_meta=false send_dummy_byte=false video_source=camera camera_id="+camera+
                " camera_size="+width+"x"+height+" camera_fps=30 video_codec=h264 video_bit_rate=20000000";
            shell=Start(adb,command);
            shell.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null) write("phone: "+e.Data); };
            shell.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null) write("phone: "+e.Data); };
            shell.BeginOutputReadLine(); shell.BeginErrorReadLine();
            Stopwatch connect=Stopwatch.StartNew();
            while(true) {
                cancellation.ThrowIfCancellationRequested();
                try {
                    socket=new TcpClient(); socket.ReceiveTimeout=1500;
                    socket.Connect(IPAddress.Loopback,portNumber);
                    byte[] codec=Read(socket.GetStream(),4);
                    if(U32(codec,0)!=0x68323634) throw new IOException("Expected scrcpy 4.1 H.264 codec header.");
                    socket.ReceiveTimeout=5000; break;
                } catch(IOException) { if(socket!=null) socket.Close(); if(shell.HasExited || connect.ElapsedMilliseconds>10000) throw; Thread.Sleep(150); }
            }
            write("DIRECT_USB_CONNECTED (no OBS, no scrcpy desktop window)");
            Stopwatch clock=Stopwatch.StartNew();
            long total=0, changed=0, previousCount=0, reported=-1; ulong lastHash=0;
            byte[] config=null;
            Stream stream=socket.GetStream();
            while(live!=null || clock.Elapsed.TotalSeconds<seconds) {
                cancellation.ThrowIfCancellationRequested();
                byte[] header=Read(stream,12);
                if((header[0]&0x80)!=0) {
                    int actualWidth=checked((int)U32(header,4)); int actualHeight=checked((int)U32(header,8));
                    if(decoder!=IntPtr.Zero) PhoneDecoderClose(decoder);
                    decoder=IntPtr.Zero;
                    Check(PhoneDecoderCreate(actualWidth,actualHeight,out decoder));
                    config=null;
                    write("SOURCE_SIZE="+actualWidth+"x"+actualHeight); continue;
                }
                byte[] packet=Read(stream,checked((int)U32(header,8)));
                ulong flags=U64(header);
                if((flags&(1UL<<62))!=0) { config=packet; continue; }
                if(decoder==IntPtr.Zero) throw new IOException("Missing session header.");
                if(config!=null) {
                    byte[] merged=new byte[config.Length+packet.Length];
                    Buffer.BlockCopy(config,0,merged,0,config.Length); Buffer.BlockCopy(packet,0,merged,config.Length,packet.Length);
                    config=null; packet=merged;
                }
                int frames;
                Check(PhoneDecoderFeed(decoder,packet,packet.Length,(long)(flags&((1UL<<61)-1)),out frames));
                total+=frames;
                if(live!=null && frames>0) live.Decoded(decoder,frames);
                ulong hash=PhoneDecoderFingerprint(decoder);
                if(frames>0 && hash!=lastHash) changed++;
                lastHash=hash;
                long second=(long)clock.Elapsed.TotalSeconds;
                if(second!=reported) {
                    write("second="+second+" decoded="+total+" changed="+changed+" framesSinceReport="+(total-previousCount));
                    previousCount=total; reported=second;
                }
            }
            Console.WriteLine("RESULT decoded="+total+" changed="+changed+" seconds="+clock.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture));
            double[] timings=new double[3]; PhoneDecoderTimings(decoder,timings);
            Console.WriteLine("averageMs total="+timings[0].ToString("F2")+" convert="+timings[1].ToString("F2")+" publish="+timings[2].ToString("F2"));
            if(total<seconds*24 || changed<seconds*20) throw new IOException("Continuous changing frames test failed. No success claimed.");
            Console.WriteLine("PASS: continuous phone frames decoded and published without OBS; no video saved.");
            return 0;
        } catch(Exception e) {
            if(cancellation.IsCancellationRequested) return 0;
            if(live!=null) { live.Error(e); write(e.Message); } else Console.Error.WriteLine(e);
            return 1;
        }
        finally {
            cancelRegistration.Dispose();
            if(socket!=null) socket.Close();
            if(decoder!=IntPtr.Zero) PhoneDecoderClose(decoder);
            if(shell!=null) { if(!shell.WaitForExit(2500)) shell.Kill(); shell.Dispose(); }
            if(serial!=null && port!=null && Regex.IsMatch(port,"^[0-9]+$")) {
                try { Run(adb,"-s "+Quote(serial)+" forward --remove tcp:"+port); } catch(Exception e) { Console.Error.WriteLine("Cleanup forward: "+e.Message); }
            }
            if(serial!=null && pushed) {
                try { Run(adb,"-s "+Quote(serial)+" shell rm "+remote); } catch(Exception e) { Console.Error.WriteLine("Cleanup server: "+e.Message); }
            }
            if(ownsProducer) producer.ReleaseMutex();
            producer.Dispose();
        }
    }
}
