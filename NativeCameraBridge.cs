using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PhoneUsbCamera {
    internal sealed class NativeBridgeService {
        private readonly BridgeService _devices;
        private readonly string _dependencyDirectory;
        private readonly object _framesLock=new object();
        private CancellationTokenSource _cancel;
        private Task _worker;
        private Bitmap _preview;
        private long _lastFrameTicks, _frameCount;
        private readonly Stopwatch _previewClock=Stopwatch.StartNew();
        private long _previousPreview;
        private volatile int _rotation, _mirror;
        internal NativeBridgeService() : this(AppDomain.CurrentDomain.BaseDirectory) { }
        internal NativeBridgeService(string dependencyDirectory) {
            _dependencyDirectory=Path.GetFullPath(dependencyDirectory);
            _devices=new BridgeService(_dependencyDirectory);
        }
        internal volatile bool PreviewRequested=true;
        internal string LastError { get; private set; }
        internal PhoneBridgeState LastState { get; private set; }
        internal bool OwnsSession { get { return _worker!=null && !_worker.IsCompleted; } }
        internal bool OutputActive { get { return OwnsSession && DateTime.UtcNow.Ticks-Interlocked.Read(ref _lastFrameTicks)<TimeSpan.FromSeconds(2).Ticks; } }
        internal static bool IsRegistered() {
            using(RegistryKey root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
            using(RegistryKey key=root.OpenSubKey(@"Software\Classes\CLSID\{87493A16-2CF8-4781-9A51-8B0674F10010}\InprocServer32")) {
                return key!=null && File.Exists(Convert.ToString(key.GetValue("")));
            }
        }
        internal async Task<PhoneBridgeState> InspectAsync() {
            PhoneBridgeState state=await Task.Run(delegate { return _devices.Inspect(false); });
            state.NativeCameraRegistered=IsRegistered();
            state.NativeOutputActive=OutputActive;
            state.NativeReceiverRunning=OwnsSession;
            state.Summary="USB: "+(state.UsbDevice==null?"未连接":state.UsbDevice.DisplayName)+
                " · Phone USB Camera: "+(state.NativeCameraRegistered?"已注册":"未注册")+
                " · "+(OutputActive?"连续输出中":"未输出");
            LastState=state; return state;
        }
        internal Task<CameraScanResult> ScanCamerasAsync(Action<string> log) { return _devices.ScanCamerasAsync(log,false); }
        internal OperationResult LaunchOpenScreen() { return _devices.LaunchOpenScreen(); }
        internal async Task<SessionResult> StartSessionAsync(CameraInfo camera,QualityPreset preset,Action<string> log,Action readyPreview) {
            if(OwnsSession) return SessionResult.Fail("本程序的摄像头会话仍在运行，请先停止。");
            if(!IsRegistered()) return SessionResult.Fail("Phone USB Camera 组件未安装或已损坏，请重新运行 U镜安装程序进行修复。");
            if(!File.Exists(Path.Combine(_dependencyDirectory,"PhoneCameraNative.dll"))) return SessionResult.Fail("缺少独立摄像头运行组件，请解压完整文件夹。");
            StopSession(); // Dispose resources from an already completed or failed session.
            _cancel=new CancellationTokenSource(); _lastFrameTicks=0; _frameCount=0; LastError=null;
            CancellationToken cancellation=_cancel.Token;
            _rotation=0; _mirror=0;
            var ready=new TaskCompletionSource<bool>();
            string[] args={Path.Combine(_dependencyDirectory,"scrcpy"),"0",preset.Width.ToString(),preset.Height.ToString(),camera.Id??"0"};
            _worker=Task.Factory.StartNew(delegate {
                try {
                    var options=new DirectUsbProbe.LiveOptions {
                        Cancellation=cancellation, Log=log,
                        Error=delegate(Exception ex) { LastError=ex.Message; },
                        Decoded=delegate(IntPtr decoder,int frames) {
                            Interlocked.Exchange(ref _lastFrameTicks,DateTime.UtcNow.Ticks);
                            long count=Interlocked.Add(ref _frameCount,frames);
                            NativeMethods.PhoneDecoderSetTransform(decoder,_rotation,_mirror);
                            if(count>=30) ready.TrySetResult(true);
                            if(PreviewRequested && _previewClock.ElapsedMilliseconds-_previousPreview>=100) {
                                _previousPreview=_previewClock.ElapsedMilliseconds;
                                int w,h; NativeMethods.PhoneDecoderFrameSize(decoder,out w,out h);
                                double scale=Math.Min(640.0/w,640.0/h);
                                int pw=Math.Max(1,(int)(w*scale)),ph=Math.Max(1,(int)(h*scale));
                                Bitmap next=new Bitmap(pw,ph,PixelFormat.Format32bppArgb);
                                BitmapData data=next.LockBits(new Rectangle(0,0,pw,ph),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                                int result;
                                try { result=NativeMethods.PhoneDecoderCopyPreview(decoder,data.Scan0,pw,ph,data.Stride); }
                                finally { next.UnlockBits(data); }
                                if(result<0) { next.Dispose(); Marshal.ThrowExceptionForHR(result); }
                                lock(_framesLock) { Bitmap previous=_preview; _preview=next; if(previous!=null) previous.Dispose(); }
                            }
                        }
                    };
                    DirectUsbProbe.RunSession(args,options);
                } catch(Exception ex) {
                    LastError=ex.Message;
                    if(log!=null) log("USB 会话异常："+ex.Message);
                } finally { Interlocked.Exchange(ref _lastFrameTicks,0); ready.TrySetResult(false); }
            },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            Task winner=await Task.WhenAny(ready.Task,Task.Delay(25000));
            if(winner!=ready.Task || !await ready.Task) {
                await Task.Run(new Func<OperationResult>(StopSession));
                return SessionResult.Fail(LastError??"等待手机连续视频帧超时，请确认手机镜头未被其他应用占用。");
            }
            if(readyPreview!=null) readyPreview();
            return SessionResult.Ok("已直接输出到 Phone USB Camera，无需 OBS。",preset.Width+"×"+preset.Height+" · H.264");
        }
        internal Bitmap CopyPreview() { lock(_framesLock) { return _preview==null?null:(Bitmap)_preview.Clone(); } }
        internal void SetTransform(int rotation,bool mirror) { _rotation=rotation; _mirror=mirror?1:0; }
        internal OperationResult StopSession() {
            if(_cancel!=null) _cancel.Cancel();
            try {
                if(_worker!=null && !_worker.Wait(15000)) return OperationResult.Fail("正在等待本次 USB 会话清理，请稍后重试停止。");
            } catch(AggregateException ex) { LastError=ex.GetBaseException().Message; }
            _worker=null;
            Interlocked.Exchange(ref _lastFrameTicks,0);
            if(_cancel!=null) { _cancel.Dispose(); _cancel=null; }
            lock(_framesLock) { if(_preview!=null) _preview.Dispose(); _preview=null; }
            return OperationResult.Ok("独立手机摄像头已停止；未启动或停止 OBS，OpenScreen 保持打开。");
        }
    }
    internal static class NativeMethods {
        [DllImport("PhoneCameraNative.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void PhoneDecoderSetTransform(IntPtr decoder,int rotation,int mirror);
        [DllImport("PhoneCameraNative.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void PhoneDecoderFrameSize(IntPtr decoder,out int width,out int height);
        [DllImport("PhoneCameraNative.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int PhoneDecoderCopyPreview(IntPtr decoder,IntPtr pixels,int width,int height,int stride);
    }
    internal sealed class NativePreviewHost : IDisposable {
        private readonly NativeBridgeService _bridge;
        private readonly System.Windows.Forms.Timer _timer=new System.Windows.Forms.Timer();
        private PreviewCanvas _canvas;
        private Form _owner;
        internal bool IsAttached { get { return _canvas!=null; } }
        internal NativePreviewHost(NativeBridgeService bridge) {
            _bridge=bridge; _timer.Interval=100; _timer.Tick+=delegate { UpdateLayout(); };
        }
        internal void Attach(Form owner,PreviewCanvas canvas) { _owner=owner; _canvas=canvas; _timer.Start(); }
        internal void UpdateLayout() {
            if(_canvas==null || _canvas.IsDisposed) return;
            _bridge.PreviewRequested=_owner.WindowState!=FormWindowState.Minimized;
            if(!_bridge.PreviewRequested) return;
            Bitmap frame=_bridge.OutputActive?_bridge.CopyPreview():null;
            _canvas.SetFrame(frame);
        }
        internal void SetFillMode(bool fill) { if(_canvas!=null) { _canvas.FillFrame=fill; _canvas.Invalidate(); } }
        internal void Detach() { _timer.Stop(); if(_canvas!=null) _canvas.SetFrame(null); _canvas=null; _owner=null; }
        public void Dispose() { Detach(); _timer.Dispose(); }
    }
}
