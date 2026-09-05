using System;
using System.Drawing;
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (CameraStudioForm form = new CameraStudioForm())
            {
                // Create handles and lay out without showing a window or opening a camera.
                IntPtr handle = form.Handle;
                form.PerformAutoScale();
                CheckLayout(form);
                form.Size = form.MinimumSize;
                CheckLayout(form);
                typeof(CameraStudioForm).GetMethod("ToggleDetails",
                    BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new Type[] { typeof(bool) }, null).Invoke(form, new object[] { true });
                CheckLayout(form);
            }
            Console.WriteLine("PASS: default/minimum window layout and expanded details; no camera started.");
            return 0;
        }

        private static void LayoutTree(Control parent)
        {
            parent.PerformLayout();
            foreach (Control child in parent.Controls) LayoutTree(child);
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
            Control title = Find(form, "USB 手机摄像头");
            Control subtitle = Find(form, "把安卓手机的高清镜头变成无水印电脑摄像头");
            Require(title != null && subtitle != null && title.Bottom <= subtitle.Top,
                "Header title overlaps subtitle.");
            foreach (string text in new string[] { "实时画面", "等待连接", "左转 90°", "右转 90°", "水平镜像", "适应" })
            {
                Control item = Find(form, text);
                Require(item != null && item.Width > 0 && item.Height >= item.Font.Height,
                    "Control is clipped: " + text);
                Require(item.Right <= item.Parent.ClientSize.Width && item.Bottom <= item.Parent.ClientSize.Height,
                    "Control exceeds its container: " + text);
            }
            Control start = Find(form, "启动摄像头");
            Require(start != null && start.Height >= start.Font.Height,
                "Start button is clipped.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
