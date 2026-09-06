using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace PhoneUsbCamera
{
    // Explicitly opt-in hardware test. No GUI, OBS, saved frames or recording.
    internal static class CaptureSmokeTests
    {
        private static int Main(string[] args)
        {
            try { Run(args).GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.WriteLine("FAIL: " + ex); return 1; }
        }
        private static async Task Run(string[] args)
        {
            if (args.Length != 2) throw new ArgumentException("Pass runtime directory and FilterProbe.exe.");
            NativeBridgeService bridge = new NativeBridgeService(args[0]);
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                try
                {
                    Console.WriteLine("START_CYCLE=" + cycle);
                    SessionResult result = await bridge.StartSessionAsync(CameraInfo.AutoBack(),
                        QualityPreset.Stable1080(), Console.WriteLine, null);
                    Require(result.Success, result.Message);
                    Require(bridge.OutputActive, "No live output after startup.");
                    await Task.Delay(250);
                    using (Bitmap preview = bridge.CopyPreview())
                        Require(preview != null && preview.Width == 640 && preview.Height == 360,
                            "1080p landscape preview is missing or has unexpected dimensions.");
                    using (Process consumer = new Process())
                    {
                        consumer.StartInfo = new ProcessStartInfo(Path.GetFullPath(args[1]), "--registered 8 1920 1080") {
                            UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardOutput = true, RedirectStandardError = true
                        };
                        consumer.Start();
                        Task<string> output = consumer.StandardOutput.ReadToEndAsync();
                        Task<string> error = consumer.StandardError.ReadToEndAsync();
                        await Task.Delay(1200);
                        bridge.SetTransform(90, true);
                        await Task.Delay(650);
                        using (Bitmap preview = bridge.CopyPreview())
                            Require(preview != null && preview.Width == 360 && preview.Height == 640,
                                "Rotated portrait preview is missing.");
                        bridge.PreviewRequested = false;
                        Console.WriteLine("PREVIEW_DISABLED_WITH_LIVE_OUTPUT");
                        await Task.Delay(3200);
                        Require(bridge.OutputActive, "Disabling preview stopped camera output.");
                        bridge.SetTransform(0, false);
                        bridge.PreviewRequested = true;
                        bool finished = await Task.Run(delegate { return consumer.WaitForExit(15000); });
                        if (!finished) { consumer.Kill(); throw new TimeoutException("Consumer test timed out."); }
                        Console.WriteLine(await output);
                        Console.WriteLine(await error);
                        Require(consumer.ExitCode == 0, "Registered camera did not deliver continuous changing frames.");
                    }
                }
                finally
                {
                    OperationResult stop = bridge.StopSession();
                    Console.WriteLine(stop.Message);
                    Require(stop.Success && !bridge.OwnsSession && !bridge.OutputActive, "Session did not stop cleanly.");
                    using (Bitmap preview = bridge.CopyPreview()) Require(preview == null, "Stale preview retained after stop.");
                }
            }
            Console.WriteLine("PASS: two native start/stop cycles, preview transforms, preview-disabled output and registered DirectShow consumption. Not an OpenScreen recording test.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
