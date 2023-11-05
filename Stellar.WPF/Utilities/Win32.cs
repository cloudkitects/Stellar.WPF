using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using Stellar.WPF.Document;
using Stellar.WPF.Editing;
using Stellar.WPF.Rendering;

namespace Stellar.WPF.Utilities;

/// <summary>
/// Win32 function wrapper.
/// </summary>
internal static partial class Win32
{
    [SuppressUnmanagedCodeSecurity]
    public static partial class NativeMethods
    {
        #region caret
        [LibraryImport("user32.dll")]
        public static partial int GetCaretBlinkTime();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetCaretPos(int x, int y);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyCaret();
        #endregion

        #region IME support
        [LibraryImport("imm32.dll")]
        public static partial IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);
        [LibraryImport("imm32.dll")]
        internal static partial IntPtr ImmGetContext(IntPtr hWnd);
        [LibraryImport("imm32.dll")]
        internal static partial IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ImmNotifyIME(IntPtr hIMC, int dwAction, int dwIndex, int dwValue = 0);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ImmSetCompositionWindow(IntPtr hIMC, ref CompositionForm form);
        
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "source-generated P/Invoke cannot marshal font parameter")]
        internal static extern bool ImmSetCompositionFont(IntPtr hIMC, ref LOGFONT font);
        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "source-generated P/Invoke cannot marshal font parameter")]
        internal static extern bool ImmGetCompositionFont(IntPtr hIMC, out LOGFONT font);

        [DllImport("msctf.dll")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "source-generated P/Invoke cannot marshal threadMgr parameter")]
        public static extern int TF_CreateThreadMgr(out ITextFrameworkThreadManager threadMgr);
        #endregion
    }

    #region caret
    /// <summary>
    /// Gets the caret blink time.
    /// </summary>
    public static TimeSpan CaretBlinkTime => TimeSpan.FromMilliseconds(NativeMethods.GetCaretBlinkTime());

    /// <summary>
    /// Creates an invisible Win32 caret for the specified Visual
	/// with the specified size (coordinates local to the owner visual).
    /// </summary>
    public static bool CreateCaret(Visual owner, Size size)
	{
		if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (PresentationSource.FromVisual(owner) is HwndSource source)
        {
            var vector = owner.PointToScreen(new Point(size.Width, size.Height)) - owner.PointToScreen(new Point(0, 0));

            return NativeMethods.CreateCaret(source.Handle, IntPtr.Zero, (int)Math.Ceiling(vector.X), (int)Math.Ceiling(vector.Y));
        }
        
        return false;
    }

	/// <summary>
	/// Sets the position of the caret previously created using <see cref="CreateCaret"/>. position is relative to the owner visual.
	/// </summary>
	public static bool SetCaretPosition(Visual owner, Point position)
	{
		if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (PresentationSource.FromVisual(owner) is HwndSource source)
        {
            var pointOnRootVisual = owner.TransformToAncestor(source.RootVisual).Transform(position);
            var pointOnHwnd = pointOnRootVisual.TransformToDevice(source.RootVisual);

            return NativeMethods.SetCaretPos((int)pointOnHwnd.X, (int)pointOnHwnd.Y);
        }
        
        return false;
    }

	/// <summary>
	/// Destroys the caret previously created using <see cref="CreateCaret"/>.
	/// </summary>
	public static bool DestroyCaret()
	{
		return NativeMethods.DestroyCaret();
	}
    #endregion

    #region IME support
    private const int NI_COMPOSITIONSTR = 0x15;
    private const int CPS_CANCEL = 0x4;

    public const int WM_IME_COMPOSITION = 0x10F;
    public const int WM_IME_SETCONTEXT = 0x281;
    public const int WM_INPUTLANGCHANGE = 0x51;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CompositionForm
    {
        public int dwStyle;
        public POINT ptCurrentPos;
        public RECT rcArea;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct LOGFONT
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    [ComImport, Guid("aa80e801-2021-11d2-93e0-0060b067b86e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITextFrameworkThreadManager
    {
        void Activate(out int clientId);
        void Deactivate();
        void CreateDocumentMgr(out IntPtr docMgr);
        void EnumDocumentMgrs(out IntPtr enumDocMgrs);
        void GetFocus(out IntPtr docMgr);
        void SetFocus(IntPtr docMgr);
        void AssociateFocus(IntPtr hwnd, IntPtr newDocMgr, out IntPtr prevDocMgr);
        void IsThreadFocus([MarshalAs(UnmanagedType.Bool)] out bool isFocus);
        void GetFunctionProvider(ref Guid classId, out IntPtr funcProvider);
        void EnumFunctionProviders(out IntPtr enumProviders);
        void GetGlobalCompartment(out IntPtr compartmentMgr);
    }
    
    [ThreadStatic] public static bool textFrameworkThreadManagerInitialized;
    [ThreadStatic] public static ITextFrameworkThreadManager? textFrameworkThreadManager;

    public static readonly Rect RectVoid = new(0, 0, 0, 0);

    public static ITextFrameworkThreadManager? GetTextFrameworkThreadManager()
    {
        if (!textFrameworkThreadManagerInitialized)
        {
            textFrameworkThreadManagerInitialized = true;

            _ = NativeMethods.TF_CreateThreadMgr(out textFrameworkThreadManager);
        }
        
        return textFrameworkThreadManager;
    }

    public static bool NotifyIme(IntPtr hIMC)
    {
        return NativeMethods.ImmNotifyIME(hIMC, NI_COMPOSITIONSTR, CPS_CANCEL);
    }

    public static bool SetCompositionWindow(HwndSource source, IntPtr hIMC, TextArea textArea)
    {
        var textViewBounds = (textArea ?? throw new ArgumentNullException(nameof(textArea))).TextView.GetBounds(source);
        var characterBounds = textArea.TextView.GetCharacterBounds(textArea.Caret.Position, source);
        
        var form = new CompositionForm
        {
            dwStyle = 0x0020
        };

        form.ptCurrentPos.x = (int)Math.Max(characterBounds.Left, textViewBounds.Left);
        form.ptCurrentPos.y = (int)Math.Max(characterBounds.Top, textViewBounds.Top);
        form.rcArea.left = (int)textViewBounds.Left;
        form.rcArea.top = (int)textViewBounds.Top;
        form.rcArea.right = (int)textViewBounds.Right;
        form.rcArea.bottom = (int)textViewBounds.Bottom;
        
        return NativeMethods.ImmSetCompositionWindow(hIMC, ref form);
    }

    public static bool SetCompositionFont(HwndSource source, IntPtr hIMC, TextArea textArea)
    {
        var font = new LOGFONT();
        var bounds = (textArea ?? throw new ArgumentNullException(nameof(textArea))).TextView.GetCharacterBounds(textArea.Caret.Position, source);
        
        font.lfFaceName = textArea.FontFamily.Source;
        font.lfHeight = (int)bounds.Height;
        
        return NativeMethods.ImmSetCompositionFont(hIMC, ref font);
    }

    /// <summary>
    /// Docking layuot changes can cause the root visual to loose it's identity.
    /// </summary>
    private static Rect GetBounds(this TextView textView, HwndSource source)
    {
        if (source.RootVisual is null || !source.RootVisual.IsAncestorOf(textView))
        {
            return RectVoid;
        }

        var rect = new Rect(0, 0, textView.ActualWidth, textView.ActualHeight);
        
        return textView
            .TransformToAncestor(source.RootVisual)
            .TransformBounds(rect)          // on root visual
            .TransformToDevice(source.RootVisual); // on HWND
    }

    private static Rect GetCharacterBounds(this TextView textView, TextViewPosition pos, HwndSource source)
    {
        var visual = textView.GetVisualLine(pos.Line);
        
        if (visual is null || source.RootVisual is null || !source.RootVisual.IsAncestorOf(textView))
        {
            return RectVoid;
        }

        var line = visual.GetTextLine(pos.VisualColumn, pos.IsAtEndOfLine);
        Rect rect;

        // calculate the display rect for the current character
        if (pos.VisualColumn < visual.VisualLengthWithEndOfLineMarker)
        {
            rect = line.GetTextBounds(pos.VisualColumn, 1).First().Rectangle;
            rect.Offset(0, visual.GetTextLineVisualYPosition(line, VisualYPosition.Top));
        }
        else
        {
            // we are in virtual space; use one wide-space
            rect = new Rect(
                visual.GetVisualPosition(pos.VisualColumn, VisualYPosition.TextTop),
                new Size(textView.WideSpaceWidth, textView.DefaultLineHeight));
        }
        // adjust to current scrolling
        rect.Offset(-textView.ScrollOffset);
        
        return textView
            .TransformToAncestor(source.RootVisual)
            .TransformBounds(rect)
            .TransformToDevice(source.RootVisual);
    }
    #endregion
}
