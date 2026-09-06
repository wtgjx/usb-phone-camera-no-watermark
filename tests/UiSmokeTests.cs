using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace PhoneUsbCamera
{
    internal static class UiSmokeTests
    {
        [STAThread]
        private static int Main()
        {
            try { return Run(); }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL_TYPE=" + ex.GetType().FullName);
                Console.WriteLine("FAIL_MESSAGE=" + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }
        private static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Require(!BridgeService.HasNonBlackOutputFrames("lavfi.signalstats.YMAX=16\nlavfi.signalstats.YMAX=16\nlavfi.signalstats.YMAX=16"),
                "The reproduced all-black virtual-camera output must fail validation.");
            Require(!BridgeService.HasNonBlackOutputFrames("frame=3"), "Missing frame metadata must not pass.");
            Require(BridgeService.HasNonBlackOutputFrames("lavfi.signalstats.YMAX=16\nlavfi.signalstats.YMAX=82\nlavfi.signalstats.YMAX=91"),
                "Actual non-black frames should pass after an initial black frame.");
            string output = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine("Creating UCam form...");
            using (CameraStudioForm form = new CameraStudioForm())
            {
                // Create handles and lay out without showing a window or opening a camera.
                IntPtr handle = form.Handle;
                Console.WriteLine("UCam form created; checking layout...");
                form.PerformAutoScale();
                typeof(CameraStudioForm).GetMethod("ApplyState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(form, new object[] { new PhoneBridgeState() });
                CheckLayout(form);
                Require(!((ScrollableControl)FindNamed(form,"DeviceInspector")).VerticalScroll.Visible,
                    "Default inspector must not require scrolling.");
                Render(form, Path.Combine(output, "ucam-ui-default.png"));
                form.Size = form.MinimumSize;
                CheckLayout(form);
                Require(!((ScrollableControl)FindNamed(form,"DeviceInspector")).VerticalScroll.Visible,
                    "Minimum-size inspector must not require scrolling.");
                Render(form, Path.Combine(output, "ucam-ui-minimum.png"));
                typeof(CameraStudioForm).GetMethod("ToggleDetails",
                    BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new Type[] { typeof(bool) }, null).Invoke(form, new object[] { true });
                CheckLayout(form);
                Render(form, Path.Combine(output, "ucam-ui-details.png"));
                typeof(CameraStudioForm).GetMethod("ToggleDetails", BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new Type[] { typeof(bool) }, null).Invoke(form, new object[] { false });
                typeof(CameraStudioForm).GetMethod("ApplyState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(form, new object[] { new PhoneBridgeState {
                        UsbDevice = new AdbDevice { State = "device", Model = "界面状态测试（模拟设备）" },
                        CameraModeCompatible = true, AndroidRelease = "12+", NativeCameraRegistered = true
                    } });
                CheckLayout(form);
                Render(form, Path.Combine(output, "ucam-ui-connected-simulation.png"));
                Console.WriteLine("Component snapshots generated without showing the app; not a desktop screenshot.");
                Require(form.Icon != null && form.Text == "U镜 · UCam", "Window branding missing.");
                using (Image logo = UCamBrand.LoadImage()) Require(logo.Width > 0, "Brand resource missing.");
                Require(UCamBrand.CameraLabel(CameraInfo.AutoBack()) == "后置镜头 · 自动选择", "Camera label mismatch.");
            }
            Console.WriteLine("PASS: UCam branding, picker label, default/minimum layout and expanded logs; no camera started.");
            return 0;
        }

        private static void LayoutTree(Control parent)
        {
            parent.PerformLayout();
            foreach (Control child in parent.Controls) LayoutTree(child);
        }

        private static Control FindNamed(Control root, string name)
        {
            return root.Controls.Find(name, true).Single();
        }

        private static void Render(Form form, string path)
        {
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(form.BackColor);
                foreach (Control child in form.Controls) RenderControl(child, graphics, Point.Empty);
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        // Hidden forms do not print their children. Render each component explicitly
        // for design review, without Show(), device polling, or desktop interaction.
        private static void RenderControl(Control control, Graphics graphics, Point parent)
        {
            if (control.Width < 1 || control.Height < 1) return;
            Point offset = new Point(parent.X + control.Left, parent.Y + control.Top);
            System.Drawing.Drawing2D.GraphicsState state = graphics.Save();
            graphics.SetClip(new Rectangle(offset, control.Size), System.Drawing.Drawing2D.CombineMode.Intersect);
            using (Bitmap layer = new Bitmap(control.Width, control.Height))
            {
                control.DrawToBitmap(layer, new Rectangle(Point.Empty, control.Size));
                ComboBox combo = control as ComboBox;
                if (combo != null && combo.SelectedIndex >= 0)
                {
                    using (Graphics paint = Graphics.FromImage(layer))
                    {
                        paint.Clear(combo.BackColor);
                        DrawItemEventArgs args = new DrawItemEventArgs(paint, combo.Font,
                            new Rectangle(0,0,Math.Max(1,combo.Width-24),combo.Height),combo.SelectedIndex,DrawItemState.ComboBoxEdit);
                        combo.GetType().GetMethod("OnDrawItem",BindingFlags.NonPublic|BindingFlags.Instance)
                            .Invoke(combo,new object[] { args });
                    }
                }
                graphics.DrawImageUnscaled(layer, offset);
            }
            if (!(control is ComboBox))
                foreach (Control child in control.Controls) RenderControl(child, graphics, offset);
            graphics.Restore(state);
        }

        private static Control Find(Control root, string text)
        {
            foreach (Control child in root.Controls)
            {
                if (child.Text == text) return child;
                Control nested = Find(child, text);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void CheckLayout(Form form)
        {
            LayoutTree(form);
            Control title = Find(form, "U镜");
            Control subtitle = Find(form, "UCam");
            Require(title != null && subtitle != null && title.Bottom <= subtitle.Top,
                "Header title overlaps subtitle.");
            foreach (string text in new string[] { "摄像头预览", "等待连接", "左转 90°", "右转 90°", "水平镜像", "适应", "打开 OpenScreen", "停止输出" })
            {
                Control item = Find(form, text);
                Require(item != null && item.Width > 0 && item.Height >= item.Font.Height,
                    "Control is clipped: " + text);
                Require(item.Right <= item.Parent.ClientSize.Width && item.Bottom <= item.Parent.ClientSize.Height,
                    "Control exceeds its container: " + text + "; " + item.Bounds + "; parent=" + item.Parent.ClientSize);
            }
            Control start = Find(form, "启动摄像头");
            Require(start != null && start.Height >= start.Font.Height,
                "Start button is clipped.");
            Point offset = Point.Empty;
            for (Control current = start; current != form; current = current.Parent)
                offset.Offset(current.Location);
            Rectangle startBounds = new Rectangle(offset, start.Size);
            Require(form.ClientRectangle.Contains(startBounds), "Primary action must stay within the window: " + startBounds + "; form=" + form.ClientRectangle);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
