using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using Stellar.WPF.Document;
using Stellar.WPF.Indentation;
using Stellar.WPF.Rendering;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Control that wraps a TextView and adds support for user input and the caret.
    /// </summary>
    public class TextArea : Control, IScrollInfo, IWeakEventListener, ITextEditorComponent, IServiceProvider
    {
        internal readonly ImeSupport ime;

        #region Constructor
        static TextArea()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TextArea), new FrameworkPropertyMetadata(typeof(TextArea)));
            
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata( typeof(TextArea), new FrameworkPropertyMetadata(Boxed.True));
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(TextArea), new FrameworkPropertyMetadata(KeyboardNavigationMode.None));
            
            FocusableProperty.OverrideMetadata(typeof(TextArea), new FrameworkPropertyMetadata(Boxed.True));
        }

        /// <summary>
        /// Creates a new TextArea instance.
        /// </summary>
        public TextArea() : this(new TextView())
        {
        }

        /// <summary>
        /// Creates a new TextArea instance.
        /// </summary>
        protected TextArea(TextView textView)
        {
            this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
            Options = textView.Options;

            selection = emptySelection = new EmptySelection(this);

            textView.Services.AddService(typeof(TextArea), this);

            textView.LineRenderers.Add(new SelectionRenderer(this));
            textView.InsertLayer(new SelectionLayer(this), KnownLayer.Selection, LayerInsertionPosition.Replace);

            caret = new Caret(this);
            caret.PositionChanged += (sender, e) => RequestSelectionValidation();
            caret.PositionChanged += CaretPositionChanged!;
            
            AttachTypingEvents();
            
            ime = new ImeSupport(this);

            leftMargins.CollectionChanged += leftMargins_CollectionChanged!;

            DefaultInputHandler = new TextAreaDefaultInputHandler(this);
            ActiveInputHandler = DefaultInputHandler;
        }
        #endregion

        #region InputHandler management
        /// <summary>
        /// Gets the default input handler.
        /// </summary>
        /// <remarks><inheritdoc cref="ITextAreaInputHandler"/></remarks>
        public TextAreaDefaultInputHandler DefaultInputHandler { get; private set; }

        ITextAreaInputHandler activeInputHandler;
        bool isChangingInputHandler;

        /// <summary>
        /// Gets/Sets the active input handler.
        /// This property does not return currently active stacked input handlers. Setting this property detached all stacked input handlers.
        /// </summary>
        /// <remarks><inheritdoc cref="ITextAreaInputHandler"/></remarks>
        public ITextAreaInputHandler ActiveInputHandler
        {
            get => activeInputHandler;
            set
            {
                if (value is not null && value.TextArea != this)
                {
                    throw new ArgumentException("The input handler was created for a different text area than this one.");
                }

                if (isChangingInputHandler)
                {
                    throw new InvalidOperationException("Cannot set ActiveInputHandler recursively");
                }

                if (activeInputHandler != value)
                {
                    isChangingInputHandler = true;
                    try
                    {
                        // pop the whole stack
                        PopStackedInputHandler(stackedInputHandlers.LastOrDefault()!);
                        Debug.Assert(stackedInputHandlers.IsEmpty);

                        activeInputHandler?.Detach();

                        activeInputHandler = value;
                        value?.Attach();
                    }
                    finally
                    {
                        isChangingInputHandler = false;
                    }
                    ActiveInputHandlerChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Occurs when the ActiveInputHandler property changes.
        /// </summary>
        public event EventHandler ActiveInputHandlerChanged;

        ImmutableStack<TextAreaStackedInputHandler> stackedInputHandlers = ImmutableStack<TextAreaStackedInputHandler>.Empty;

        /// <summary>
        /// Gets the list of currently active stacked input handlers.
        /// </summary>
        /// <remarks><inheritdoc cref="ITextAreaInputHandler"/></remarks>
        public ImmutableStack<TextAreaStackedInputHandler> StackedInputHandlers => stackedInputHandlers;

        /// <summary>
        /// Pushes an input handler onto the list of stacked input handlers.
        /// </summary>
        /// <remarks><inheritdoc cref="ITextAreaInputHandler"/></remarks>
        public void PushStackedInputHandler(TextAreaStackedInputHandler inputHandler)
        {
            if (inputHandler is null)
            {
                throw new ArgumentNullException(nameof(inputHandler));
            }

            stackedInputHandlers = stackedInputHandlers.Push(inputHandler);
            inputHandler.Attach();
        }

        /// <summary>
        /// Pops the stacked input handler (and all input handlers above it).
        /// If <paramref name="inputHandler"/> is not found in the currently stacked input handlers, or is null, this method
        /// does nothing.
        /// </summary>
        /// <remarks><inheritdoc cref="ITextAreaInputHandler"/></remarks>
        public void PopStackedInputHandler(TextAreaStackedInputHandler inputHandler)
        {
            if (stackedInputHandlers.Any(i => i == inputHandler))
            {
                ITextAreaInputHandler oldHandler;
                do
                {
                    oldHandler = stackedInputHandlers.Peek();
                    stackedInputHandlers = stackedInputHandlers.Pop();
                    oldHandler.Detach();
                } while (oldHandler != inputHandler);
            }
        }
        #endregion

        #region Document property
        /// <summary>
        /// Document property.
        /// </summary>
        public static readonly DependencyProperty DocumentProperty
            = TextView.DocumentProperty.AddOwner(typeof(TextArea), new FrameworkPropertyMetadata(OnDocumentChanged));

        /// <summary>
        /// Gets/Sets the document displayed by the text editor.
        /// </summary>
        public Document.Document Document
        {
            get => (Document.Document)GetValue(DocumentProperty);
            set => SetValue(DocumentProperty, value);
        }

        /// <inheritdoc/>
        public event EventHandler DocumentChanged;

        static void OnDocumentChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e) => ((TextArea)dp).OnDocumentChanged((Document.Document)e.OldValue, (Document.Document)e.NewValue);

        void OnDocumentChanged(Document.Document oldValue, Document.Document newValue)
        {
            if (oldValue is not null)
            {
                TextDocumentWeakEventManager.Changing.RemoveListener(oldValue, this);
                TextDocumentWeakEventManager.Changed.RemoveListener(oldValue, this);
                TextDocumentWeakEventManager.UpdateStarted.RemoveListener(oldValue, this);
                TextDocumentWeakEventManager.UpdateFinished.RemoveListener(oldValue, this);
            }
            textView.Document = newValue;
            if (newValue is not null)
            {
                TextDocumentWeakEventManager.Changing.AddListener(newValue, this);
                TextDocumentWeakEventManager.Changed.AddListener(newValue, this);
                TextDocumentWeakEventManager.UpdateStarted.AddListener(newValue, this);
                TextDocumentWeakEventManager.UpdateFinished.AddListener(newValue, this);
            }
            // Reset caret location and selection: this is necessary because the caret/selection might be invalid
            // in the new document (e.g. if new document is shorter than the old document).
            caret.Location = new Location(1, 1);
            ClearSelection();

            DocumentChanged?.Invoke(this, EventArgs.Empty);

            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Options property
        /// <summary>
        /// Options property.
        /// </summary>
        public static readonly DependencyProperty OptionsProperty
            = TextView.OptionsProperty.AddOwner(typeof(TextArea), new FrameworkPropertyMetadata(OnOptionsChanged));

        /// <summary>
        /// Gets/Sets the document displayed by the text editor.
        /// </summary>
        public TextEditorOptions Options
        {
            get => (TextEditorOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        /// <summary>
        /// Occurs when a text editor option has changed.
        /// </summary>
        public event PropertyChangedEventHandler OptionChanged;

        /// <summary>
        /// Raises the <see cref="OptionChanged"/> event.
        /// </summary>
        protected virtual void OnOptionChanged(PropertyChangedEventArgs e) => OptionChanged?.Invoke(this, e);

        static void OnOptionsChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e) => ((TextArea)dp).OnOptionsChanged((TextEditorOptions)e.OldValue, (TextEditorOptions)e.NewValue);

        void OnOptionsChanged(TextEditorOptions oldValue, TextEditorOptions newValue)
        {
            if (oldValue is not null)
            {
                PropertyChangedWeakEventManager.RemoveListener(oldValue, this);
            }

            textView.Options = newValue;
            
            if (newValue is not null)
            {
                PropertyChangedWeakEventManager.AddListener(newValue, this);
            }

            OnOptionChanged(new PropertyChangedEventArgs(null));
        }
        #endregion

        #region ReceiveWeakEvent
        /// <inheritdoc cref="IWeakEventListener.ReceiveWeakEvent"/>
        protected virtual bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (managerType == typeof(TextDocumentWeakEventManager.Changing))
            {
                OnDocumentChanging();
                return true;
            }
            else if (managerType == typeof(TextDocumentWeakEventManager.Changed))
            {
                OnDocumentChanged((DocumentChangeEventArgs)e);
                return true;
            }
            else if (managerType == typeof(TextDocumentWeakEventManager.UpdateStarted))
            {
                OnUpdateStarted();
                return true;
            }
            else if (managerType == typeof(TextDocumentWeakEventManager.UpdateFinished))
            {
                OnUpdateFinished();
                return true;
            }
            else if (managerType == typeof(PropertyChangedWeakEventManager))
            {
                OnOptionChanged((PropertyChangedEventArgs)e);
                return true;
            }
            return false;
        }

        bool IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e) => ReceiveWeakEvent(managerType, sender, e);
        #endregion

        #region Caret handling on document changes
        void OnDocumentChanging() => caret.OnDocumentChanging();

        void OnDocumentChanged(DocumentChangeEventArgs e)
        {
            caret.OnDocumentChanged(e);
            Selection = selection.UpdateOnDocumentChange(e);
        }

        void OnUpdateStarted() => Document.UndoStack.PushOptional(new RestoreCaretAndSelectionUndoAction(this));

        void OnUpdateFinished() => caret.OnDocumentUpdateFinished();

        sealed class RestoreCaretAndSelectionUndoAction : IUndoable
        {
            // keep textarea in weak reference because the IUndoableOperation is stored with the document
            WeakReference textAreaReference;
            TextViewPosition caretPosition;
            Selection selection;

            public RestoreCaretAndSelectionUndoAction(TextArea textArea)
            {
                textAreaReference = new WeakReference(textArea);
                // Just save the old caret position, no need to validate here.
                // If we restore it, we'll validate it anyways.
                caretPosition = textArea.Caret.NonValidatedPosition;
                selection = textArea.Selection;
            }

            public void Undo()
            {
                var textArea = (TextArea)textAreaReference.Target!;

                if (textArea is not null)
                {
                    textArea.Caret.Position = caretPosition;
                    textArea.Selection = selection;
                }
            }

            public void Redo() =>
                // redo=undo: we just restore the caret/selection state
                Undo();
        }
        #endregion

        #region TextView property
        readonly TextView textView;
        IScrollInfo scrollInfo;

        /// <summary>
        /// Gets the text view used to display text in this text area.
        /// </summary>
        public TextView TextView => textView;
        /// <inheritdoc/>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            scrollInfo = textView;
            ApplyScrollInfo();
        }
        #endregion

        #region Selection property
        internal readonly Selection emptySelection;
        Selection selection;

        /// <summary>
        /// Occurs when the selection has changed.
        /// </summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// Gets/Sets the selection in this text area.
        /// </summary>
        public Selection Selection
        {
            get => selection;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.textArea != this)
                {
                    throw new ArgumentException("Cannot use a Selection instance that belongs to another text area.");
                }

                if (!object.Equals(selection, value))
                {
                    //					Debug.WriteLine("Selection change from " + selection + " to " + value);
                    if (textView is not null)
                    {
                        ISegment oldSegment = selection.SurroundingSegment;
                        ISegment newSegment = value.SurroundingSegment;
                        if (!Selection.EnableVirtualSpace && (selection is SimpleSelection && value is SimpleSelection && oldSegment is not null && newSegment is not null))
                        {
                            // perf optimization:
                            // When a simple selection changes, don't redraw the whole selection, but only the changed parts.
                            int oldSegmentOffset = oldSegment.Offset;
                            int newSegmentOffset = newSegment.Offset;
                            if (oldSegmentOffset != newSegmentOffset)
                            {
                                textView.Redraw(Math.Min(oldSegmentOffset, newSegmentOffset),
                                                Math.Abs(oldSegmentOffset - newSegmentOffset),
                                                DispatcherPriority.Render);
                            }
                            int oldSegmentEndOffset = oldSegment.EndOffset;
                            int newSegmentEndOffset = newSegment.EndOffset;
                            if (oldSegmentEndOffset != newSegmentEndOffset)
                            {
                                textView.Redraw(Math.Min(oldSegmentEndOffset, newSegmentEndOffset),
                                                Math.Abs(oldSegmentEndOffset - newSegmentEndOffset),
                                                DispatcherPriority.Render);
                            }
                        }
                        else
                        {
                            textView.Redraw(oldSegment, DispatcherPriority.Render);
                            textView.Redraw(newSegment, DispatcherPriority.Render);
                        }
                    }
                    selection = value;
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    // a selection change causes commands like copy/paste/etc. to change status
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void ClearSelection() => Selection = emptySelection;

        /// <summary>
        /// The <see cref="SelectionBrush"/> property.
        /// </summary>
        public static readonly DependencyProperty SelectionBrushProperty =
            DependencyProperty.Register("SelectionBrush", typeof(Brush), typeof(TextArea));

        /// <summary>
        /// Gets/Sets the background brush used for the selection.
        /// </summary>
        public Brush SelectionBrush
        {
            get => (Brush)GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        /// <summary>
        /// The <see cref="SelectionForeground"/> property.
        /// </summary>
        public static readonly DependencyProperty SelectionForegroundProperty =
            DependencyProperty.Register("SelectionForeground", typeof(Brush), typeof(TextArea));

        /// <summary>
        /// Gets/Sets the foreground brush used for selected text.
        /// </summary>
        public Brush SelectionForeground
        {
            get => (Brush)GetValue(SelectionForegroundProperty);
            set => SetValue(SelectionForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="SelectionBorder"/> property.
        /// </summary>
        public static readonly DependencyProperty SelectionBorderProperty =
            DependencyProperty.Register("SelectionBorder", typeof(Pen), typeof(TextArea));

        /// <summary>
        /// Gets/Sets the pen used for the border of the selection.
        /// </summary>
        public Pen SelectionBorder
        {
            get => (Pen)GetValue(SelectionBorderProperty);
            set => SetValue(SelectionBorderProperty, value);
        }

        /// <summary>
        /// The <see cref="SelectionCornerRadius"/> property.
        /// </summary>
        public static readonly DependencyProperty SelectionCornerRadiusProperty =
            DependencyProperty.Register("SelectionCornerRadius", typeof(double), typeof(TextArea),
                                        new FrameworkPropertyMetadata(3.0));

        /// <summary>
        /// Gets/Sets the corner radius of the selection.
        /// </summary>
        public double SelectionCornerRadius
        {
            get => (double)GetValue(SelectionCornerRadiusProperty);
            set => SetValue(SelectionCornerRadiusProperty, value);
        }

        /// <summary>
        /// Gets/Sets the active mouse selection mode.
        /// 
        /// Setting this property to MouseSelectionMode.None will cancel mouse selection
        /// and release mouse capture.
        /// 
        /// Setting this property to another value will acquire mouse capture and
        /// activate the mouse selection mode.
        /// If mouse capture cannot be acquired, MouseSelectionMode will stay unchanged.
        /// 
        /// Currently, the setter only supports the values <c>None</c>, <c>Normal</c>
        /// and <c>Rectangular</c>.
        /// </summary>
        public MouseSelectionMode MouseSelectionMode
        {
            get
            {
                var mouseHandler = DefaultInputHandler.MouseSelection as SelectionMouseHandler;
                if (mouseHandler is not null)
                {
                    return mouseHandler.MouseSelectionMode;
                }
                else
                {
                    return MouseSelectionMode.None;
                }
            }
            set
            {
                var mouseHandler = DefaultInputHandler.MouseSelection as SelectionMouseHandler;
                if (mouseHandler is not null)
                {
                    mouseHandler.MouseSelectionMode = value;
                }
            }
        }
        #endregion

        #region Force caret to stay inside selection
        bool ensureSelectionValidRequested;
        int allowCaretOutsideSelection;

        void RequestSelectionValidation()
        {
            if (!ensureSelectionValidRequested && allowCaretOutsideSelection == 0)
            {
                ensureSelectionValidRequested = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(EnsureSelectionValid));
            }
        }

        /// <summary>
        /// Code that updates only the caret but not the selection can cause confusion when
        /// keys like 'Delete' delete the (possibly invisible) selected text and not the
        /// text around the caret.
        /// 
        /// So we'll ensure that the caret is inside the selection.
        /// (when the caret is not in the selection, we'll clear the selection)
        /// 
        /// This method is invoked using the Dispatcher so that code may temporarily violate this rule
        /// (e.g. most 'extend selection' methods work by first setting the caret, then the selection),
        /// it's sufficient to fix it after any event handlers have run.
        /// </summary>
        void EnsureSelectionValid()
        {
            ensureSelectionValidRequested = false;

            if (allowCaretOutsideSelection == 0)
            {
                if (!selection.IsEmpty && !selection.Contains(caret.Offset))
                {
                    Debug.WriteLine("Resetting selection because caret is outside");
                    
                    ClearSelection();
                }
            }
        }

        /// <summary>
        /// Temporarily allows positioning the caret outside the selection.
        /// Dispose the returned IDisposable to revert the allowance.
        /// </summary>
        /// <remarks>
        /// The text area only forces the caret to be inside the selection when other events
        /// have finished running (using the dispatcher), so you don't have to use this method
        /// for temporarily positioning the caret in event handlers.
        /// This method is only necessary if you want to run the WPF dispatcher, e.g. if you
        /// perform a drag'n'drop operation.
        /// </remarks>
        public IDisposable AllowCaretOutsideSelection()
        {
            VerifyAccess();

            allowCaretOutsideSelection++;
            
            return new FirstCallDisposable(
                delegate
                {
                    VerifyAccess();

                    allowCaretOutsideSelection--;
                    
                    RequestSelectionValidation();
                });
        }
        #endregion

        #region Properties
        readonly Caret caret;

        /// <summary>
        /// Gets the Caret used for this text area.
        /// </summary>
        public Caret Caret => caret;

        void CaretPositionChanged(object sender, EventArgs e)
        {
            if (textView is null)
            {
                return;
            }

            textView.HighlightedLine = Caret.Line;
        }

        ObservableCollection<UIElement> leftMargins = new ObservableCollection<UIElement>();

        /// <summary>
        /// Gets the collection of margins displayed to the left of the text view.
        /// </summary>
        public ObservableCollection<UIElement> LeftMargins => leftMargins;

        void leftMargins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (ITextViewConnector c in e.OldItems.OfType<ITextViewConnector>())
                {
                    c.DetachFrom(textView);
                }
            }
            if (e.NewItems is not null)
            {
                foreach (ITextViewConnector c in e.NewItems.OfType<ITextViewConnector>())
                {
                    c.AttachTo(textView);
                }
            }
        }

        IEditableSectionProvider editableSectionProvider = Editing.EditableSectionProvider.Instance;

        /// <summary>
        /// Gets/Sets an object that provides read-only sections for the text area.
        /// </summary>
        public IEditableSectionProvider EditableSectionProvider
        {
            get => editableSectionProvider;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                editableSectionProvider = value;

                CommandManager.InvalidateRequerySuggested(); // the read-only status effects Paste.CanExecute and the IME
            }
        }
        #endregion

        #region IScrollInfo implementation
        ScrollViewer scrollOwner;
        bool canVerticallyScroll, canHorizontallyScroll;

        void ApplyScrollInfo()
        {
            if (scrollInfo is not null)
            {
                scrollInfo.ScrollOwner = scrollOwner;
                scrollInfo.CanVerticallyScroll = canVerticallyScroll;
                scrollInfo.CanHorizontallyScroll = canHorizontallyScroll;
                scrollOwner = null;
            }
        }

        bool IScrollInfo.CanVerticallyScroll
        {
            get => scrollInfo is not null ? scrollInfo.CanVerticallyScroll : false;
            set
            {
                canVerticallyScroll = value;
                if (scrollInfo is not null)
                {
                    scrollInfo.CanVerticallyScroll = value;
                }
            }
        }

        bool IScrollInfo.CanHorizontallyScroll
        {
            get => scrollInfo is not null ? scrollInfo.CanHorizontallyScroll : false;
            set
            {
                canHorizontallyScroll = value;
                if (scrollInfo is not null)
                {
                    scrollInfo.CanHorizontallyScroll = value;
                }
            }
        }

        double IScrollInfo.ExtentWidth => scrollInfo is not null ? scrollInfo.ExtentWidth : 0;

        double IScrollInfo.ExtentHeight => scrollInfo is not null ? scrollInfo.ExtentHeight : 0;

        double IScrollInfo.ViewportWidth => scrollInfo is not null ? scrollInfo.ViewportWidth : 0;

        double IScrollInfo.ViewportHeight => scrollInfo is not null ? scrollInfo.ViewportHeight : 0;

        double IScrollInfo.HorizontalOffset => scrollInfo is not null ? scrollInfo.HorizontalOffset : 0;

        double IScrollInfo.VerticalOffset => scrollInfo is not null ? scrollInfo.VerticalOffset : 0;

        ScrollViewer IScrollInfo.ScrollOwner
        {
            get => scrollInfo?.ScrollOwner;
            set
            {
                if (scrollInfo is not null)
                {
                    scrollInfo.ScrollOwner = value;
                }
                else
                {
                    scrollOwner = value;
                }
            }
        }

        void IScrollInfo.LineUp() => scrollInfo?.LineUp();

        void IScrollInfo.LineDown() => scrollInfo?.LineDown();

        void IScrollInfo.LineLeft() => scrollInfo?.LineLeft();

        void IScrollInfo.LineRight() => scrollInfo?.LineRight();

        void IScrollInfo.PageUp() => scrollInfo?.PageUp();

        void IScrollInfo.PageDown() => scrollInfo?.PageDown();

        void IScrollInfo.PageLeft() => scrollInfo?.PageLeft();

        void IScrollInfo.PageRight() => scrollInfo?.PageRight();

        void IScrollInfo.MouseWheelUp() => scrollInfo?.MouseWheelUp();

        void IScrollInfo.MouseWheelDown() => scrollInfo?.MouseWheelDown();

        void IScrollInfo.MouseWheelLeft() => scrollInfo?.MouseWheelLeft();

        void IScrollInfo.MouseWheelRight() => scrollInfo?.MouseWheelRight();

        void IScrollInfo.SetHorizontalOffset(double offset) => scrollInfo?.SetHorizontalOffset(offset);

        void IScrollInfo.SetVerticalOffset(double offset) => scrollInfo?.SetVerticalOffset(offset);

        Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle)
        {
            if (scrollInfo is not null)
            {
                return scrollInfo.MakeVisible(visual, rectangle);
            }
            else
            {
                return Rect.Empty;
            }
        }
        #endregion

        #region Focus Handling (Show/Hide Caret)
        /// <inheritdoc/>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        /// <inheritdoc/>
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            // First activate IME, then show caret
            ime.OnGotKeyboardFocus(e);
            caret.Show();
        }

        /// <inheritdoc/>
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            caret.Hide();
            ime.OnLostKeyboardFocus(e);
        }
        #endregion

        #region OnTextInput / RemoveSelectedText / ReplaceSelectionWithText
        /// <summary>
        /// Occurs when the TextArea receives text input.
        /// This is like the <see cref="UIElement.TextInput"/> event,
        /// but occurs immediately before the TextArea handles the TextInput event.
        /// </summary>
        public event TextCompositionEventHandler TextEntering;

        /// <summary>
        /// Occurs when the TextArea receives text input.
        /// This is like the <see cref="UIElement.TextInput"/> event,
        /// but occurs immediately after the TextArea handles the TextInput event.
        /// </summary>
        public event TextCompositionEventHandler TextEntered;

        /// <summary>
        /// Raises the TextEntering event.
        /// </summary>
        protected virtual void OnTextEntering(TextCompositionEventArgs e) => TextEntering?.Invoke(this, e);

        /// <summary>
        /// Raises the TextEntered event.
        /// </summary>
        protected virtual void OnTextEntered(TextCompositionEventArgs e) => TextEntered?.Invoke(this, e);

        /// <inheritdoc/>
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            //Debug.WriteLine("TextInput: Text='" + e.Text + "' SystemText='" + e.SystemText + "' ControlText='" + e.ControlText + "'");
            base.OnTextInput(e);
            if (!e.Handled && Document is not null)
            {
                if (string.IsNullOrEmpty(e.Text) || e.Text == "\x1b" || e.Text == "\b")
                {
                    // ASCII 0x1b = ESC.
                    // WPF produces a TextInput event with that old ASCII control char
                    // when Escape is pressed. We'll just ignore it.

                    // A deadkey followed by backspace causes a textinput event for the BS character.

                    // Similarly, some shortcuts like Alt+Space produce an empty TextInput event.
                    // We have to ignore those (not handle them) to keep the shortcut working.
                    return;
                }
                HideMouseCursor();
                PerformTextInput(e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Performs text input.
        /// This raises the <see cref="TextEntering"/> event, replaces the selection with the text,
        /// and then raises the <see cref="TextEntered"/> event.
        /// </summary>
        public void PerformTextInput(string text)
        {
            TextComposition textComposition = new TextComposition(InputManager.Current, this, text);
            TextCompositionEventArgs e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, textComposition);
            e.RoutedEvent = TextInputEvent;
            PerformTextInput(e);
        }

        /// <summary>
        /// Performs text input.
        /// This raises the <see cref="TextEntering"/> event, replaces the selection with the text,
        /// and then raises the <see cref="TextEntered"/> event.
        /// </summary>
        public void PerformTextInput(TextCompositionEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (Document is null)
            {
                throw new InvalidOperationException("Document is null");
            }

            OnTextEntering(e);
            if (!e.Handled)
            {
                if (e.Text == "\n" || e.Text == "\r" || e.Text == "\r\n")
                {
                    ReplaceSelectionWithNewLine();
                }
                else
                {
                    if (OverstrikeMode && Selection.IsEmpty && Document.GetLineByNumber(Caret.Line).EndOffset > Caret.Offset)
                    {
                        EditingCommands.SelectRightByCharacter.Execute(null, this);
                    }

                    ReplaceSelectionWithText(e.Text);
                }
                OnTextEntered(e);
                caret.BringCaretToView();
            }
        }

        void ReplaceSelectionWithNewLine()
        {
            var newLine = Document.GetNewLineString(Caret.Line);

            using (Document.RunUpdate())
            {
                ReplaceSelectionWithText(newLine);

                if (IndentationStrategy is not null)
                {
                    var line = Document.GetLineByNumber(Caret.Line);

                    var deletable = GetDeletableSegments(line);
                    
                    if (deletable.Length == 1 && deletable[0].Offset == line.Offset && deletable[0].Length == line.Length)
                    {
                        // use indentation strategy only if the line is not read-only
                        IndentationStrategy.IndentLine(Document, line);
                    }
                }
            }
        }

        internal void RemoveSelectedText()
        {
            if (Document is null)
            {
                throw new InvalidOperationException("Document is null");
            }

            selection.ReplaceSelectionWithText(string.Empty);
#if DEBUG
            if (!selection.IsEmpty)
            {
                foreach (ISegment s in selection.Segments)
                {
                    Debug.Assert(!EditableSectionProvider.GetDeletableSegments(s).Any());
                }
            }
#endif
        }

        internal void ReplaceSelectionWithText(string newText)
        {
            if (newText is null)
            {
                throw new ArgumentNullException(nameof(newText));
            }

            if (Document is null)
            {
                throw new InvalidOperationException("Document is null");
            }

            selection.ReplaceSelectionWithText(newText);
        }

        internal ISegment[] GetDeletableSegments(ISegment segment)
        {
            var deletableSegments = EditableSectionProvider.GetDeletableSegments(segment);
            if (deletableSegments is null)
            {
                throw new InvalidOperationException("ReadOnlySectionProvider.GetDeletableSegments returned null");
            }

            var array = deletableSegments.ToArray();
            int lastIndex = segment.Offset;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Offset < lastIndex)
                {
                    throw new InvalidOperationException("ReadOnlySectionProvider returned incorrect segments (outside of input segment / wrong order)");
                }

                lastIndex = array[i].EndOffset;
            }
            if (lastIndex > segment.EndOffset)
            {
                throw new InvalidOperationException("ReadOnlySectionProvider returned incorrect segments (outside of input segment / wrong order)");
            }

            return array;
        }
        #endregion

        #region IndentationStrategy property
        /// <summary>
        /// IndentationStrategy property.
        /// </summary>
        public static readonly DependencyProperty IndentationStrategyProperty =
            DependencyProperty.Register("IndentationStrategy", typeof(IIndentationStrategy), typeof(TextArea),
                                        new FrameworkPropertyMetadata(new DefaultIndentationStrategy()));

        /// <summary>
        /// Gets/Sets the indentation strategy used when inserting new lines.
        /// </summary>
        public IIndentationStrategy IndentationStrategy
        {
            get => (IIndentationStrategy)GetValue(IndentationStrategyProperty);
            set => SetValue(IndentationStrategyProperty, value);
        }
        #endregion

        #region OnKeyDown/OnKeyUp
        /// <inheritdoc/>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            foreach (TextAreaStackedInputHandler h in stackedInputHandlers)
            {
                if (e.Handled)
                {
                    break;
                }

                h.OnPreviewKeyDown(e);
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            foreach (TextAreaStackedInputHandler h in stackedInputHandlers)
            {
                if (e.Handled)
                {
                    break;
                }

                h.OnPreviewKeyUp(e);
            }
        }

        // Make life easier for text editor extensions that use a different cursor based on the pressed modifier keys.
        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            TextView.InvalidateCursorIfMouseWithinTextView();
        }

        /// <inheritdoc/>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            TextView.InvalidateCursorIfMouseWithinTextView();
        }
        #endregion

        #region Hide Mouse Cursor While Typing

        bool isMouseCursorHidden;

        void AttachTypingEvents()
        {
            // Use the PreviewMouseMove event in case some other editor layer consumes the MouseMove event (e.g. SD's InsertionCursorLayer)
            MouseEnter += delegate { ShowMouseCursor(); };
            MouseLeave += delegate { ShowMouseCursor(); };
            PreviewMouseMove += delegate { ShowMouseCursor(); };
            TouchEnter += delegate { ShowMouseCursor(); };
            TouchLeave += delegate { ShowMouseCursor(); };
            PreviewTouchMove += delegate { ShowMouseCursor(); };
        }

        void ShowMouseCursor()
        {
            if (isMouseCursorHidden)
            {
                System.Windows.Forms.Cursor.Show();
                isMouseCursorHidden = false;
            }
        }

        void HideMouseCursor()
        {
            if (Options.HideCursorWhileTyping && !isMouseCursorHidden && IsMouseOver)
            {
                isMouseCursorHidden = true;
                System.Windows.Forms.Cursor.Hide();
            }
        }

        #endregion

        #region Overstrike mode

        /// <summary>
        /// The <see cref="OverstrikeMode"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OverstrikeModeProperty =
            DependencyProperty.Register("OverstrikeMode", typeof(bool), typeof(TextArea),
                                        new FrameworkPropertyMetadata(Boxed.False));

        /// <summary>
        /// Gets/Sets whether overstrike mode is active.
        /// </summary>
        public bool OverstrikeMode
        {
            get => (bool)GetValue(OverstrikeModeProperty);
            set => SetValue(OverstrikeModeProperty, value);
        }

        #endregion

        /// <inheritdoc/>
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TextAreaAutomationPeer(this);

        /// <inheritdoc/>
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
            // accept clicks even where the text area draws no background
            new PointHitTestResult(this, hitTestParameters.HitPoint);

        /// <inheritdoc/>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == SelectionBrushProperty
                || e.Property == SelectionBorderProperty
                || e.Property == SelectionForegroundProperty
                || e.Property == SelectionCornerRadiusProperty)
            {
                textView.Redraw();
            }
            else if (e.Property == OverstrikeModeProperty)
            {
                caret.UpdateIfVisible();
            }
        }

        /// <summary>
        /// Gets the requested service.
        /// </summary>
        /// <returns>Returns the requested service instance, or null if the service cannot be found.</returns>
        public virtual object GetService(Type serviceType) => textView.GetService(serviceType);

        /// <summary>
        /// Occurs when text inside the TextArea was copied.
        /// </summary>
        public event EventHandler<TextEventArgs> TextCopied;

        internal void OnTextCopied(TextEventArgs e) => TextCopied?.Invoke(this, e);
    }

    /// <summary>
    /// EventArgs with text.
    /// </summary>
    [Serializable]
    public class TextEventArgs : EventArgs
    {
        string text;

        /// <summary>
        /// Gets the text.
        /// </summary>
        public string Text => text;

        /// <summary>
        /// Creates a new TextEventArgs instance.
        /// </summary>
        public TextEventArgs(string text)
        {
            this.text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
