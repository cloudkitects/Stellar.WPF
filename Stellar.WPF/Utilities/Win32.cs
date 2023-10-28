using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Stellar.WPF.Utilities;


/// <summary>
/// Win32 function wrapper.
/// </summary>
static partial class Win32
{
    [SuppressUnmanagedCodeSecurity]
    static partial class NativeMethods
    {
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
    }

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
}
