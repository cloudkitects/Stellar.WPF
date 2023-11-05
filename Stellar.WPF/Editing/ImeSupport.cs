using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing;

internal class ImeSupport
{
    private readonly TextArea textArea;

    private IntPtr currentContext;
    private IntPtr previousContext;
    private IntPtr defaultImeWnd;

    private HwndSource? hwndSource;

    /// <summary>
    /// // A long-lived event handler instance given CommandManager.RequerySuggested uses weak references
    /// </summary>
    private readonly EventHandler requerySuggestedHandler;
    
    private bool isReadOnly;

    public ImeSupport(TextArea textArea)
    {
        this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));

        InputMethod.SetIsInputMethodSuspended(this.textArea, textArea.Options.EnableImeSupport);
        
        // We listen to CommandManager.RequerySuggested for both caret offset changes and changes to the set of read-only sections.
        // This is because there's no dedicated event for read-only section changes; but RequerySuggested needs to be raised anyways
        // to invalidate the Paste command.
        requerySuggestedHandler = OnRequerySuggested;
        
        CommandManager.RequerySuggested += requerySuggestedHandler;
        
        textArea.OptionChanged += TextAreaOptionChanged;
    }

    private void OnRequerySuggested(object? sender, EventArgs e)
    {
        UpdateImeEnabled();
    }

    private void TextAreaOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "EnableImeSupport")
        {
            InputMethod.SetIsInputMethodSuspended(textArea, textArea.Options.EnableImeSupport);
            
            UpdateImeEnabled();
        }
    }

    public void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        UpdateImeEnabled();
    }

    public void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        if (e.OldFocus == textArea && currentContext != IntPtr.Zero)
            Win32.NotifyIme(currentContext);
        ClearContext();
    }

    private void UpdateImeEnabled()
    {
        if (textArea.Options.EnableImeSupport && textArea.IsKeyboardFocused)
        {
            bool newReadOnly = !textArea.EditableSectionProvider.CanInsert(textArea.Caret.Offset);
            if (hwndSource is null || isReadOnly != newReadOnly)
            {
                ClearContext(); // clear existing context (on read-only change)
                isReadOnly = newReadOnly;
                CreateContext();
            }
        }
        else
        {
            ClearContext();
        }
    }

    private void ClearContext()
    {
        if (hwndSource is not null)
        {
            Win32.NativeMethods.ImmAssociateContext(hwndSource.Handle, previousContext);
            Win32.NativeMethods.ImmReleaseContext(defaultImeWnd, currentContext);
            currentContext = IntPtr.Zero;
            defaultImeWnd = IntPtr.Zero;
            hwndSource.RemoveHook(WndProc);
            hwndSource = null!;
        }
    }

    private void CreateContext()
    {
        hwndSource = (HwndSource)PresentationSource.FromVisual(textArea);
        if (hwndSource is not null)
        {
            if (isReadOnly)
            {
                defaultImeWnd = IntPtr.Zero;
                currentContext = IntPtr.Zero;
            }
            else
            {
                defaultImeWnd = Win32.NativeMethods.ImmGetDefaultIMEWnd(IntPtr.Zero);
                currentContext = Win32.NativeMethods.ImmGetContext(defaultImeWnd);
            }
            previousContext = Win32.NativeMethods.ImmAssociateContext(hwndSource.Handle, currentContext);
            hwndSource.AddHook(WndProc);
            // UpdateCompositionWindow() will be called by the caret becoming visible

            var threadMgr = Win32.GetTextFrameworkThreadManager();
            
            // Even though the docs says passing null is invalid, this seems to help
            // activating the IME on the default input context that is shared with WPF
            threadMgr?.SetFocus(IntPtr.Zero);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Win32.WM_INPUTLANGCHANGE:
                // Don't mark the message as handled; other windows
                // might want to handle it as well.

                // If we have a context, recreate it
                if (hwndSource is not null)
                {
                    ClearContext();
                    CreateContext();
                }
                break;
            case Win32.WM_IME_COMPOSITION:
                UpdateCompositionWindow();
                break;
        }
        return IntPtr.Zero;
    }

    public void UpdateCompositionWindow()
    {
        if (currentContext != IntPtr.Zero)
        {
            Win32.SetCompositionFont(hwndSource, currentContext, textArea);
            Win32.SetCompositionWindow(hwndSource, currentContext, textArea);
        }
    }
}
