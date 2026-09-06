using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PhoneUsbCamera
{
    internal static class UCamBrand
    {
        internal static readonly Color Accent = Color.FromArgb(193, 69, 30);
        internal static readonly Color AccentSoft = Color.FromArgb(255, 240, 232);
        internal static readonly Color Muted = Color.FromArgb(102, 108, 119);
        internal static readonly Color Ink = Color.FromArgb(33, 37, 45);
        internal static Image LoadImage()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UCam.Logo.png"))
            {
                if (stream == null) throw new InvalidOperationException("Missing embedded UCam logo.");
                using (Image image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }
        internal static Icon LoadIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UCam.Icon.ico"))
            {
                if (stream == null) throw new InvalidOperationException("Missing embedded UCam icon.");
                using (Icon icon = new Icon(stream, 32, 32)) return (Icon)icon.Clone();
            }
        }
        internal static Font Font(float size, FontStyle style) { return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
        internal static string CameraLabel(object value)
        {
            CameraInfo camera = value as CameraInfo;
            if (camera == null) return Convert.ToString(value);
            if (camera.Id == null) return "后置镜头 · 自动选择";
            string facing = camera.Facing == "front" ? "前置镜头" : camera.Facing == "back" ? "后置镜头" : "外接镜头";
            return (camera.Recommended ? "后置主摄" : facing) + " · ID " + camera.Id;
        }
    }

    // Keep native keyboard navigation, accessibility and popup behavior while
    // supplying the same typography and selection colors as the rest of UCam.
    internal sealed class UCamComboBox : ComboBox
    {
        internal UCamComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.FromArgb(246, 247, 249);
            ForeColor = UCamBrand.Ink;
            Font = UCamBrand.Font(9F, FontStyle.Regular);
            ItemHeight = 30;
            IntegralHeight = false;
            DropDownHeight = 250;
            MaxDropDownItems = 7;
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemHeight = Math.Max(32, Font.Height + 14);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = Math.Max(Font.Height + 14, (int)Math.Round(32 * DeviceDpi / 96.0));
        }
        protected override void OnDropDown(EventArgs e)
        {
            DropDownWidth = Width;
            base.OnDropDown(e);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Bounds.Width < 1 || e.Bounds.Height < 1) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            Color fill = selected && !edit ? UCamBrand.AccentSoft : BackColor;
            Color fore = Enabled ? (selected && !edit ? UCamBrand.Accent : ForeColor) : UCamBrand.Muted;
            using (Brush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
            string label = e.Index >= 0 ? UCamBrand.CameraLabel(Items[e.Index]) : Text;
            Rectangle bounds = new Rectangle(e.Bounds.X+10, e.Bounds.Y, Math.Max(1,e.Bounds.Width-18),e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics,label,Font,bounds,fore,
                TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
    }
}
