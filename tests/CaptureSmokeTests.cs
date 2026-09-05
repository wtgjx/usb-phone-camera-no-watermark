using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhoneUsbCamera
{
    // Opt-in hardware test: briefly uses the connected phone and virtual camera.
    // No UI is shown, no frames are saved, and owned processes are always stopped.
    internal static class CaptureSmokeTests
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            int exitCode = 1;
            if (args.Length != 1) throw new ArgumentException("Pass the built dist directory.");
            BridgeService bridge = new BridgeService(args[0]);
            using (Form controller = new Form())
            using (IntegratedPreviewHost preview = new IntegratedPreviewHost())
            using (Timer start = new Timer())
            {
                controller.ShowInTaskbar = false;
                controller.Size = new Size(800, 600);
                Panel viewport = new Panel { Dock = DockStyle.Fill };
                controller.Controls.Add(viewport);
                IntPtr handle = controller.Handle;
                IntPtr viewportHandle = viewport.Handle;
                start.Interval = 100;
                start.Tick += async delegate
                {
                    start.Stop();
                    try
                    {
                        bool attached = false;
                        SessionResult result = await bridge.StartSessionAsync(
                            CameraInfo.AutoBack(), QualityPreset.All()[2], Console.WriteLine,
                            delegate(int processId)
                            {
                                controller.Invoke(new Action(delegate
                                {
                                    attached = preview.Attach(controller, viewport, processId);
                                    if (!attached) throw new InvalidOperationException(preview.LastError);
                                }));
                            }, false);
                        if (result.Success && attached)
                        {
                            Console.WriteLine("PASS: integrated source remains capturable and virtual camera has non-black frames.");
                            exitCode = 0;
                        }
                        else Console.WriteLine("FAIL: " + result.Message);
                    }
                    catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); }
                    finally
                    {
                        preview.Detach();
                        bridge.StopSession();
                        Application.ExitThread();
                    }
                };
                start.Start();
                Application.Run();
            }
            return exitCode;
        }
    }
}
