using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PhoneUsbCamera
{
    /// <summary>
    /// Presents a live DWM thumbnail of a scrcpy SDL window in a WinForms layout.
    ///
    /// The scrcpy source deliberately remains an independent top-level window so
    /// OBS Windows Graphics Capture can continue to find and capture it.  This
    /// class only manages window presentation; it never terminates the process.
    ///
    /// The capture source must NEVER be WS_EX_TOOLWINDOW: OBS filters such
    /// windows during initial discovery and reacquisition. A hidden independent
    /// owner suppresses its taskbar button without changing capture eligibility.
    /// </summary>
    internal sealed class IntegratedPreviewHost : IDisposable
    {
        private const string DefaultWindowTitle = "Phone USB Camera";
        private const string ScrcpyWindowClass = "SDL_app";
        private const int DefaultFindTimeoutMilliseconds = 3000;

        private Form _mainForm;
        private Control _placeholder;
        private PreviewOverlayForm _overlay;
        private PreviewOverlayForm _sourceOwner;
        private IntPtr _thumbnail;
        private IntPtr _sourceHandle;
        private IntPtr _originalExtendedStyle;
        private IntPtr _originalOwner;
        private NativeMethods.RECT _originalBounds;
        private bool _originalWasVisible;
        private bool _originalWasMinimized;
        private bool _sourcePresentationChanged;
        private bool _fillMode;
        private bool _disposed;

        internal IntPtr SourceHandle
        {
            get { return _sourceHandle; }
        }

        internal bool IsAttached
        {
            get
            {
                return _thumbnail != IntPtr.Zero &&
                       _sourceHandle != IntPtr.Zero &&
                       NativeMethods.IsWindow(_sourceHandle);
            }
        }

        internal string LastError { get; private set; }

        /// <summary>
        /// False uses aspect-fit with letterboxing.  True fills the preview and
        /// center-crops the DWM source while preserving its aspect ratio.
        /// </summary>
        internal bool FillMode
        {
            get { return _fillMode; }
            set
            {
                if (_fillMode == value)
                {
                    return;
                }

                _fillMode = value;
                UpdateLayout();
            }
        }

        internal bool Attach(Form mainForm, Control placeholder, int processId)
        {
            return Attach(mainForm, placeholder, processId, DefaultWindowTitle);
        }

        internal bool Attach(Form mainForm, Control placeholder, int processId, string expectedTitle)
        {
            ThrowIfDisposed();
            if (mainForm == null)
            {
                throw new ArgumentNullException("mainForm");
            }
            if (placeholder == null)
            {
                throw new ArgumentNullException("placeholder");
            }
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException("processId");
            }
            if (mainForm.IsDisposed || placeholder.IsDisposed)
            {
                LastError = "The main form or preview placeholder has already been disposed.";
                return false;
            }

            if (mainForm.InvokeRequired)
            {
                return (bool)mainForm.Invoke(
                    new Func<bool>(delegate
                    {
                        return Attach(mainForm, placeholder, processId, expectedTitle);
                    }));
            }

            DetachCore();
            LastError = string.Empty;

            IntPtr source = FindSourceWindow(
                processId,
                string.IsNullOrEmpty(expectedTitle) ? DefaultWindowTitle : expectedTitle,
                DefaultFindTimeoutMilliseconds);
            if (source == IntPtr.Zero)
            {
                LastError = "No visible top-level SDL_app window was found for scrcpy process " + processId + ".";
                return false;
            }

            if (NativeMethods.GetAncestor(source, NativeMethods.GA_ROOT) != source)
            {
                LastError = "The located scrcpy window is not a top-level window; OBS WGC compatibility cannot be preserved.";
                return false;
            }

            bool compositionEnabled;
            int compositionResult = NativeMethods.DwmIsCompositionEnabled(out compositionEnabled);
            if (compositionResult != NativeMethods.S_OK || !compositionEnabled)
            {
                LastError = "Desktop Window Manager composition is unavailable (HRESULT 0x" +
                            compositionResult.ToString("X8") + ").";
                return false;
            }

            _mainForm = mainForm;
            _placeholder = placeholder;
            _sourceHandle = source;
            _originalExtendedStyle = NativeMethods.GetWindowLongPtr(source, NativeMethods.GWL_EXSTYLE);
            _originalOwner = NativeMethods.GetWindowLongPtr(source, NativeMethods.GWLP_HWNDPARENT);
            NativeMethods.GetWindowRect(source, out _originalBounds);
            _originalWasVisible = NativeMethods.IsWindowVisible(source);
            _originalWasMinimized = NativeMethods.IsIconic(source);

            try
            {
                if (!PrepareSourceWindow())
                {
                    string sourceError = LastError;
                    DetachCore();
                    LastError = sourceError;
                    return false;
                }

                _overlay = new PreviewOverlayForm();
                _overlay.Owner = mainForm;
                _overlay.BackColor = Color.Black;

                // Accessing Handle creates a top-level destination HWND without
                // showing a black frame before the thumbnail is registered.
                IntPtr destination = _overlay.Handle;
                int registerResult = NativeMethods.DwmRegisterThumbnail(
                    destination,
                    source,
                    out _thumbnail);
                if (registerResult != NativeMethods.S_OK || _thumbnail == IntPtr.Zero)
                {
                    LastError = "DwmRegisterThumbnail failed (HRESULT 0x" +
                                registerResult.ToString("X8") + ").";
                    DetachCore();
                    return false;
                }

                SubscribeLayoutEvents();
                UpdateLayoutCore();
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Unable to attach the integrated preview: " + ex.Message;
                DetachCore();
                return false;
            }
        }

        internal void SetFillMode(bool fill)
        {
            FillMode = fill;
        }

        internal void UpdateLayout()
        {
            if (_disposed)
            {
                return;
            }

            Form form = _mainForm;
            if (form != null && !form.IsDisposed && form.IsHandleCreated && form.InvokeRequired)
            {
                try
                {
                    form.BeginInvoke(new MethodInvoker(UpdateLayout));
                }
                catch (InvalidOperationException)
                {
                    // The owner may be closing while a background update arrives.
                }
                return;
            }

            UpdateLayoutCore();
        }

        internal void Detach()
        {
            Form form = _mainForm;
            if (form != null && !form.IsDisposed && form.IsHandleCreated && form.InvokeRequired)
            {
                try
                {
                    form.Invoke(new MethodInvoker(Detach));
                }
                catch (InvalidOperationException)
                {
                    // The form is already tearing down; finish best-effort below.
                    DetachCore();
                }
                return;
            }

            DetachCore();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Detach();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finds the best top-level SDL window for a process.  Exact title and
        /// SDL_app class matches are preferred, but PID ownership is mandatory.
        /// </summary>
        internal static IntPtr FindSourceWindow(int processId, string expectedTitle, int timeoutMilliseconds)
        {
            if (processId <= 0)
            {
                return IntPtr.Zero;
            }

            int wait = Math.Max(0, timeoutMilliseconds);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(wait);
            do
            {
                IntPtr found = FindSourceWindowOnce(processId, expectedTitle);
                if (found != IntPtr.Zero)
                {
                    return found;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }
                Thread.Sleep(50);
            }
            while (true);

            return IntPtr.Zero;
        }

        private static IntPtr FindSourceWindowOnce(int processId, string expectedTitle)
        {
            List<WindowCandidate> candidates = new List<WindowCandidate>();
            NativeMethods.EnumWindows(
                delegate(IntPtr window, IntPtr state)
                {
                    uint ownerProcessId;
                    NativeMethods.GetWindowThreadProcessId(window, out ownerProcessId);
                    if (ownerProcessId != (uint)processId || !NativeMethods.IsWindow(window))
                    {
                        return true;
                    }

                    // EnumWindows normally returns top-level windows only; this
                    // extra guard protects the OBS WGC invariant explicitly.
                    if (NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT) != window)
                    {
                        return true;
                    }

                    string title = ReadWindowText(window);
                    string className = ReadClassName(window);
                    bool titleMatches = string.Equals(
                        title,
                        expectedTitle,
                        StringComparison.OrdinalIgnoreCase);
                    bool classMatches = string.Equals(
                        className,
                        ScrcpyWindowClass,
                        StringComparison.OrdinalIgnoreCase);

                    int score = 0;
                    if (classMatches) score += 100;
                    if (titleMatches) score += 80;
                    if (NativeMethods.IsWindowVisible(window)) score += 20;
                    if (!NativeMethods.IsIconic(window)) score += 5;
                    if (title.IndexOf("scrcpy", StringComparison.OrdinalIgnoreCase) >= 0) score += 2;

                    // A class or exact title match is required so an unrelated
                    // helper window owned by scrcpy is never selected.
                    if (classMatches || titleMatches)
                    {
                        candidates.Add(new WindowCandidate(window, score));
                    }
                    return true;
                },
                IntPtr.Zero);

            IntPtr best = IntPtr.Zero;
            int bestScore = int.MinValue;
            foreach (WindowCandidate candidate in candidates)
            {
                if (candidate.Score > bestScore)
                {
                    best = candidate.Handle;
                    bestScore = candidate.Score;
                }
            }
            return best;
        }

        private bool PrepareSourceWindow()
        {
            if (_sourceHandle == IntPtr.Zero || !NativeMethods.IsWindow(_sourceHandle))
            {
                LastError = "The scrcpy source window no longer exists.";
                return false;
            }

            // Record that presentation has changed before the first mutation so
            // every failure path can restore the exact original window state.
            _sourcePresentationChanged = true;

            // An owned top-level window has no taskbar button. Keep its owner
            // independent of the main form so minimizing the UI cannot minimize
            // the capture source. GWLP_HWNDPARENT sets ownership, not SetParent.
            _sourceOwner = new PreviewOverlayForm();
            NativeMethods.SetWindowLongPtr(_sourceHandle,
                NativeMethods.GWLP_HWNDPARENT, _sourceOwner.Handle);
            long originalStyle = _originalExtendedStyle.ToInt64();
            long updatedStyle = originalStyle;
            updatedStyle &= ~NativeMethods.WS_EX_TOOLWINDOW;
            updatedStyle |= NativeMethods.WS_EX_NOACTIVATE;
            updatedStyle &= ~NativeMethods.WS_EX_APPWINDOW;
            NativeMethods.SetWindowLongPtr(
                _sourceHandle,
                NativeMethods.GWL_EXSTYLE,
                new IntPtr(updatedStyle));

            long verifiedStyle = NativeMethods.GetWindowLongPtr(
                _sourceHandle,
                NativeMethods.GWL_EXSTYLE).ToInt64();
            if ((verifiedStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
                (verifiedStyle & NativeMethods.WS_EX_NOACTIVATE) == 0 ||
                (verifiedStyle & NativeMethods.WS_EX_APPWINDOW) != 0 ||
                NativeMethods.GetWindowLongPtr(_sourceHandle, NativeMethods.GWLP_HWNDPARENT) != _sourceOwner.Handle)
            {
                LastError = "Unable to configure scrcpy as a capturable owned top-level window.";
                return false;
            }

            Screen sourceScreen = Screen.FromHandle(_sourceHandle);
            Rectangle workArea = sourceScreen == null
                ? SystemInformation.VirtualScreen
                : sourceScreen.WorkingArea;
            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                workArea = SystemInformation.VirtualScreen;
            }

            // Keep a 2x2 corner inside the work area.  The source remains shown,
            // non-minimized and at its original 4K client size, while almost all
            // of it is outside the user's normal workspace.
            int parkedX = workArea.Right - 2;
            int parkedY = workArea.Bottom - 2;
            bool positioned = NativeMethods.SetWindowPos(
                _sourceHandle,
                NativeMethods.HWND_BOTTOM,
                parkedX,
                parkedY,
                0,
                0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER |
                NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.ShowWindow(_sourceHandle, NativeMethods.SW_SHOWNOACTIVATE);
            bool shownAtParkingPosition = NativeMethods.SetWindowPos(
                _sourceHandle,
                NativeMethods.HWND_BOTTOM,
                parkedX,
                parkedY,
                0,
                0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER |
                NativeMethods.SWP_SHOWWINDOW);

            if (!positioned || !shownAtParkingPosition || NativeMethods.IsIconic(_sourceHandle))
            {
                LastError = "The scrcpy window could not be restored and parked without minimizing it.";
                return false;
            }

            return true;
        }

        private void UpdateLayoutCore()
        {
            if (_overlay == null || _overlay.IsDisposed ||
                _thumbnail == IntPtr.Zero || _sourceHandle == IntPtr.Zero)
            {
                return;
            }

            bool shouldShow = IsControlActuallyVisible(_placeholder) &&
                              _mainForm != null &&
                              !_mainForm.IsDisposed &&
                              _mainForm.Visible &&
                              _mainForm.WindowState != FormWindowState.Minimized &&
                              NativeMethods.IsWindow(_sourceHandle);
            if (!shouldShow)
            {
                if (_overlay.Visible)
                {
                    _overlay.Hide();
                }
                return;
            }

            Rectangle screenBounds = _placeholder.RectangleToScreen(_placeholder.ClientRectangle);
            if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
            {
                if (_overlay.Visible)
                {
                    _overlay.Hide();
                }
                return;
            }

            NativeMethods.SetWindowPos(
                _overlay.Handle,
                IntPtr.Zero,
                screenBounds.Left,
                screenBounds.Top,
                screenBounds.Width,
                screenBounds.Height,
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER |
                NativeMethods.SWP_NOZORDER);

            NativeMethods.SIZE sourceSize;
            int sizeResult = NativeMethods.DwmQueryThumbnailSourceSize(_thumbnail, out sourceSize);
            if (sizeResult != NativeMethods.S_OK || sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                LastError = "DwmQueryThumbnailSourceSize failed (HRESULT 0x" +
                            sizeResult.ToString("X8") + ").";
                return;
            }

            NativeMethods.RECT sourceRect;
            NativeMethods.RECT destinationRect;
            CalculateThumbnailRectangles(
                sourceSize.Width,
                sourceSize.Height,
                screenBounds.Width,
                screenBounds.Height,
                _fillMode,
                out sourceRect,
                out destinationRect);

            NativeMethods.DWM_THUMBNAIL_PROPERTIES properties =
                new NativeMethods.DWM_THUMBNAIL_PROPERTIES();
            properties.Flags = NativeMethods.DWM_TNP_RECTDESTINATION |
                               NativeMethods.DWM_TNP_RECTSOURCE |
                               NativeMethods.DWM_TNP_OPACITY |
                               NativeMethods.DWM_TNP_VISIBLE |
                               NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY;
            properties.Destination = destinationRect;
            properties.Source = sourceRect;
            properties.Opacity = 255;
            properties.Visible = true;
            properties.SourceClientAreaOnly = true;

            int updateResult = NativeMethods.DwmUpdateThumbnailProperties(_thumbnail, ref properties);
            if (updateResult != NativeMethods.S_OK)
            {
                LastError = "DwmUpdateThumbnailProperties failed (HRESULT 0x" +
                            updateResult.ToString("X8") + ").";
                return;
            }

            if (!_overlay.Visible)
            {
                _overlay.Show(_mainForm);
            }
        }

        private static void CalculateThumbnailRectangles(
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight,
            bool fill,
            out NativeMethods.RECT source,
            out NativeMethods.RECT destination)
        {
            source = new NativeMethods.RECT(0, 0, sourceWidth, sourceHeight);
            destination = new NativeMethods.RECT(0, 0, destinationWidth, destinationHeight);

            if (fill)
            {
                double sourceAspect = (double)sourceWidth / sourceHeight;
                double destinationAspect = (double)destinationWidth / destinationHeight;
                if (sourceAspect > destinationAspect)
                {
                    int cropWidth = Math.Max(1, (int)Math.Round(sourceHeight * destinationAspect));
                    int left = Math.Max(0, (sourceWidth - cropWidth) / 2);
                    source = new NativeMethods.RECT(left, 0, left + cropWidth, sourceHeight);
                }
                else if (sourceAspect < destinationAspect)
                {
                    int cropHeight = Math.Max(1, (int)Math.Round(sourceWidth / destinationAspect));
                    int top = Math.Max(0, (sourceHeight - cropHeight) / 2);
                    source = new NativeMethods.RECT(0, top, sourceWidth, top + cropHeight);
                }
                return;
            }

            double scale = Math.Min(
                (double)destinationWidth / sourceWidth,
                (double)destinationHeight / sourceHeight);
            int fittedWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int fittedHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = (destinationWidth - fittedWidth) / 2;
            int y = (destinationHeight - fittedHeight) / 2;
            destination = new NativeMethods.RECT(x, y, x + fittedWidth, y + fittedHeight);
        }

        private void SubscribeLayoutEvents()
        {
            _mainForm.LocationChanged += LayoutChanged;
            _mainForm.SizeChanged += LayoutChanged;
            _mainForm.VisibleChanged += LayoutChanged;
            _mainForm.Activated += LayoutChanged;
            _mainForm.FormClosed += MainFormClosed;
            _placeholder.LocationChanged += LayoutChanged;
            _placeholder.SizeChanged += LayoutChanged;
            _placeholder.VisibleChanged += LayoutChanged;
            _placeholder.ParentChanged += LayoutChanged;
        }

        private void UnsubscribeLayoutEvents()
        {
            if (_mainForm != null)
            {
                _mainForm.LocationChanged -= LayoutChanged;
                _mainForm.SizeChanged -= LayoutChanged;
                _mainForm.VisibleChanged -= LayoutChanged;
                _mainForm.Activated -= LayoutChanged;
                _mainForm.FormClosed -= MainFormClosed;
            }
            if (_placeholder != null)
            {
                _placeholder.LocationChanged -= LayoutChanged;
                _placeholder.SizeChanged -= LayoutChanged;
                _placeholder.VisibleChanged -= LayoutChanged;
                _placeholder.ParentChanged -= LayoutChanged;
            }
        }

        private void LayoutChanged(object sender, EventArgs eventArgs)
        {
            UpdateLayoutCore();
        }

        private void MainFormClosed(object sender, FormClosedEventArgs eventArgs)
        {
            DetachCore();
        }

        private void DetachCore()
        {
            UnsubscribeLayoutEvents();

            if (_thumbnail != IntPtr.Zero)
            {
                NativeMethods.DwmUnregisterThumbnail(_thumbnail);
                _thumbnail = IntPtr.Zero;
            }

            if (_overlay != null)
            {
                try
                {
                    _overlay.Owner = null;
                    _overlay.Close();
                    _overlay.Dispose();
                }
                catch
                {
                    // Best-effort cleanup during application shutdown.
                }
                _overlay = null;
            }

            RestoreSourceWindow();
            if (_sourceOwner != null)
            {
                _sourceOwner.Dispose();
                _sourceOwner = null;
            }
            _sourceHandle = IntPtr.Zero;
            _mainForm = null;
            _placeholder = null;
            _sourcePresentationChanged = false;
        }

        private void RestoreSourceWindow()
        {
            if (!_sourcePresentationChanged ||
                _sourceHandle == IntPtr.Zero ||
                !NativeMethods.IsWindow(_sourceHandle))
            {
                return;
            }

            // Restore ownership before destroying the hidden owner; destroying
            // an owner while still attached could also destroy its owned HWND.
            NativeMethods.SetWindowLongPtr(_sourceHandle,
                NativeMethods.GWLP_HWNDPARENT, _originalOwner);
            NativeMethods.SetWindowLongPtr(
                _sourceHandle,
                NativeMethods.GWL_EXSTYLE,
                _originalExtendedStyle);

            int width = Math.Max(1, _originalBounds.Right - _originalBounds.Left);
            int height = Math.Max(1, _originalBounds.Bottom - _originalBounds.Top);
            NativeMethods.SetWindowPos(
                _sourceHandle,
                IntPtr.Zero,
                _originalBounds.Left,
                _originalBounds.Top,
                width,
                height,
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_FRAMECHANGED);

            if (_originalWasVisible)
            {
                NativeMethods.ShowWindow(
                    _sourceHandle,
                    _originalWasMinimized
                        ? NativeMethods.SW_SHOWMINNOACTIVE
                        : NativeMethods.SW_SHOWNOACTIVATE);
            }
        }

        private static bool IsControlActuallyVisible(Control control)
        {
            Control current = control;
            while (current != null)
            {
                if (!current.Visible || current.IsDisposed)
                {
                    return false;
                }
                current = current.Parent;
            }
            return true;
        }

        private static string ReadWindowText(IntPtr window)
        {
            int length = NativeMethods.GetWindowTextLength(window);
            StringBuilder text = new StringBuilder(Math.Max(1, length + 1));
            NativeMethods.GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        private static string ReadClassName(IntPtr window)
        {
            StringBuilder className = new StringBuilder(256);
            NativeMethods.GetClassName(window, className, className.Capacity);
            return className.ToString();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(IntegratedPreviewHost).FullName);
            }
        }

        private sealed class WindowCandidate
        {
            internal WindowCandidate(IntPtr handle, int score)
            {
                Handle = handle;
                Score = score;
            }

            internal IntPtr Handle { get; private set; }
            internal int Score { get; private set; }
        }

        private sealed class PreviewOverlayForm : Form
        {
            internal PreviewOverlayForm()
            {
                AutoScaleMode = AutoScaleMode.None;
                BackColor = Color.Black;
                FormBorderStyle = FormBorderStyle.None;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowIcon = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Text = string.Empty;
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;
                    parameters.ExStyle |= (int)(NativeMethods.WS_EX_TOOLWINDOW |
                                                 NativeMethods.WS_EX_NOACTIVATE);
                    return parameters;
                }
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == NativeMethods.WM_NCHITTEST)
                {
                    message.Result = new IntPtr(NativeMethods.HTTRANSPARENT);
                    return;
                }
                base.WndProc(ref message);
            }
        }

        private static class NativeMethods
        {
            internal const int S_OK = 0;
            internal const int GWL_EXSTYLE = -20;
            internal const int GWLP_HWNDPARENT = -8;
            internal const uint GA_ROOT = 2;
            internal const int SW_HIDE = 0;
            internal const int SW_SHOWNOACTIVATE = 4;
            internal const int SW_SHOWMINNOACTIVE = 7;
            internal const int WM_NCHITTEST = 0x0084;
            internal const int HTTRANSPARENT = -1;

            internal const long WS_EX_TOOLWINDOW = 0x00000080L;
            internal const long WS_EX_APPWINDOW = 0x00040000L;
            internal const long WS_EX_NOACTIVATE = 0x08000000L;

            internal const uint SWP_NOSIZE = 0x0001;
            internal const uint SWP_NOZORDER = 0x0004;
            internal const uint SWP_NOACTIVATE = 0x0010;
            internal const uint SWP_FRAMECHANGED = 0x0020;
            internal const uint SWP_SHOWWINDOW = 0x0040;
            internal const uint SWP_NOOWNERZORDER = 0x0200;

            internal const uint DWM_TNP_RECTDESTINATION = 0x00000001;
            internal const uint DWM_TNP_RECTSOURCE = 0x00000002;
            internal const uint DWM_TNP_OPACITY = 0x00000004;
            internal const uint DWM_TNP_VISIBLE = 0x00000008;
            internal const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

            internal static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

            internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr state);

            [StructLayout(LayoutKind.Sequential)]
            internal struct RECT
            {
                internal int Left;
                internal int Top;
                internal int Right;
                internal int Bottom;

                internal RECT(int left, int top, int right, int bottom)
                {
                    Left = left;
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct SIZE
            {
                internal int Width;
                internal int Height;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct DWM_THUMBNAIL_PROPERTIES
            {
                internal uint Flags;
                internal RECT Destination;
                internal RECT Source;
                internal byte Opacity;

                [MarshalAs(UnmanagedType.Bool)]
                internal bool Visible;

                [MarshalAs(UnmanagedType.Bool)]
                internal bool SourceClientAreaOnly;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr state);

            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

            [DllImport("user32.dll")]
            internal static extern int GetWindowTextLength(IntPtr window);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWindow(IntPtr window);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWindowVisible(IntPtr window);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsIconic(IntPtr window);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetWindowRect(IntPtr window, out RECT rectangle);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ShowWindow(IntPtr window, int command);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetWindowPos(
                IntPtr window,
                IntPtr insertAfter,
                int x,
                int y,
                int width,
                int height,
                uint flags);

            [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
            private static extern int GetWindowLong32(IntPtr window, int index);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
            private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

            [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
            private static extern int SetWindowLong32(IntPtr window, int index, int value);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
            private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

            internal static IntPtr GetWindowLongPtr(IntPtr window, int index)
            {
                return IntPtr.Size == 8
                    ? GetWindowLongPtr64(window, index)
                    : new IntPtr(GetWindowLong32(window, index));
            }

            internal static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value)
            {
                return IntPtr.Size == 8
                    ? SetWindowLongPtr64(window, index, value)
                    : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));
            }

            [DllImport("dwmapi.dll")]
            internal static extern int DwmIsCompositionEnabled(
                [MarshalAs(UnmanagedType.Bool)] out bool enabled);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmRegisterThumbnail(
                IntPtr destinationWindow,
                IntPtr sourceWindow,
                out IntPtr thumbnail);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmUnregisterThumbnail(IntPtr thumbnail);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmUpdateThumbnailProperties(
                IntPtr thumbnail,
                ref DWM_THUMBNAIL_PROPERTIES properties);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmQueryThumbnailSourceSize(
                IntPtr thumbnail,
                out SIZE size);
        }
    }
}
