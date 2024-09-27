using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

using SpanStack = Stellar.WPF.Utilities.ImmutableStack<Stellar.WPF.Styling.Span>;

namespace Stellar.WPF.Styling
{
    /// <summary>
    /// A document styler, encapsulates invalidating the styling state
    /// when the document changes.
    /// </summary>
    public class Styler : ILineTracker, IStyler
    {
        /// <summary>
        /// Stores the span state at the end of each line.
        /// storedSpanStacks[0] = state at beginning of document
        /// storedSpanStacks[i] = state after line i
        /// </summary>
        private readonly CompactTree<SpanStack> spanStacks = new(ReferenceEquals);
        private readonly CompactTree<bool> isValid = new((a, b) => a == b);
        private readonly IDocument document;
        private readonly ISyntax syntax;
        private readonly StylingEngine engine;
        private readonly WeakLineTracker lineTracker;
        private bool isStyling;
        private bool isInStylingGroup;
        private bool isDisposed;
        private SpanStack initialSpanStack = SpanStack.Empty;

        /// <summary>
        /// The document being styled.
        /// </summary>
        public IDocument Document => document;

        /// <summary>
        /// Creates a new Styler instance.
        /// </summary>
        public Styler(Document.Document document, ISyntax syntax)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            this.syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));

            engine = new StylingEngine(this.syntax.RuleSet);
            
            document.VerifyAccess();
            
            lineTracker = WeakLineTracker.Register(document, this);
            
            InvalidateSpanStacks();
        }

        /// <summary>
        /// Disposes the document styler.
        /// </summary>
        public void Dispose()
        {
            lineTracker?.Unregister();

            isDisposed = true;
        }

        #region ILineTracker
        void ILineTracker.BeforeRemoving(Line line)
        {
            CheckIsStyling();
            
            var number = line.Number;
            
            spanStacks.RemoveAt(number);
            isValid.RemoveAt(number);
            
            if (number < isValid.Count)
            {
                isValid[number] = false;
                
                if (number < firstInvalidLine)
                {
                    firstInvalidLine = number;
                }
            }
        }

        void ILineTracker.ResetLength(Line line, int _)
        {
            CheckIsStyling();

            var number = line.Number;

            isValid[number] = false;
            
            if (number < firstInvalidLine)
            {
                firstInvalidLine = number;
            }
        }

        void ILineTracker.AfterInserting(Line line, Line newLine)
        {
            CheckIsStyling();

            Debug.Assert(line.Number + 1 == newLine.Number);

            var lineNumber = newLine.Number;

            spanStacks.Insert(lineNumber, null!);
            
            isValid.Insert(lineNumber, false);
            
            if (lineNumber < firstInvalidLine)
            {
                firstInvalidLine = lineNumber;
            }
        }

        void ILineTracker.Rebuild()
        {
            InvalidateSpanStacks();
        }

        void ILineTracker.AfterChange(DocumentChangeEventArgs e)
        {
        }
        #endregion

        /// <summary>
        /// Gets/sets the initial span stack of the document. Default value is <see cref="SpanStack.Empty" />.
        /// </summary>
        public SpanStack InitialSpanStack
        {
            get => initialSpanStack;
            set
            {
                initialSpanStack = value ?? SpanStack.Empty;

                InvalidateStyling();
            }
        }

        /// <summary>
        /// Invalidates all stored styling info.
        /// When the document changes, the styling is invalidated automatically, this method
        /// needs to be called only when there are changes to the styling rule set.
        /// </summary>
        public void InvalidateStyling()
        {
            InvalidateSpanStacks();
            OnStylingStateChanged(1, document.LineCount); // force a redraw
        }

        /// <summary>
        /// Invalidates stored styling info, but does not raise the StylingStateChanged event.
        /// </summary>
        private void InvalidateSpanStacks()
        {
            CheckIsStyling();

            spanStacks.Clear();
            spanStacks.Add(initialSpanStack);
            spanStacks.InsertRange(1, document.LineCount, null!);
            
            isValid.Clear();
            isValid.Add(true);
            isValid.InsertRange(1, document.LineCount, false);
            
            firstInvalidLine = 1;
        }

        private int firstInvalidLine;

        /// <inheritdoc/>
        public StyledLine StyleLine(int lineNumber)
        {
            if (lineNumber < 1 || document.LineCount < lineNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), $"{lineNumber} < 1 or {document.LineCount} < {lineNumber}");
            }

            CheckIsStyling();
            
            isStyling = true;
            
            try
            {
                StyleUpTo(lineNumber - 1);

                var line = document.GetLineByNumber(lineNumber);
                
                var result = engine.StyleLine(document, line);

                UpdateTreeList(lineNumber);
                
                return result;
            }
            finally
            {
                isStyling = false;
            }
        }

        /// <summary>
        /// Gets the span stack at the end of the specified line.
        /// -> GetSpanStack(1) returns the spans at the start of the second line.
        /// </summary>
        /// <remarks>
        /// GetSpanStack(0) is valid and will return <see cref="InitialSpanStack"/>.
        /// The elements are returned in inside-out order (first element of result enumerable is the color of the innermost span).
        /// </remarks>
        public SpanStack GetSpanStack(int lineNumber)
        {
            if (lineNumber < 0 || document.LineCount < lineNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), $"{lineNumber} < 1 or {document.LineCount} < {lineNumber}");
            }

            if (firstInvalidLine <= lineNumber)
            {
                UpdateStylingState(lineNumber);
            }
            
            return spanStacks[lineNumber];
        }

        /// <inheritdoc/>
        public IEnumerable<Style> GetStyles(int lineNumber)
        {
            return GetSpanStack(lineNumber)
                .Where(span => span is not null)
                .Select(span => span.Style);
        }

        private void CheckIsStyling()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException("Styler");
            }

            if (isStyling)
            {
                throw new InvalidOperationException("Invalid call, a styling operation is currently running.");
            }
        }

        /// <inheritdoc/>
        public void UpdateStylingState(int lineNumber)
        {
            CheckIsStyling();

            isStyling = true;
            
            try
            {
                StyleUpTo(lineNumber);
            }
            finally
            {
                isStyling = false;
            }
        }

        /// <summary>
        /// Sets the engine's CurrentSpanStack to the end of the target line.
        /// Updates the span stack for all lines up to (and including) the target line, if necessary.
        /// </summary>
        private void StyleUpTo(int targetLineNumber)
        {
            for (var currentLine = 0; currentLine <= targetLineNumber; currentLine++)
            {
                if (firstInvalidLine > currentLine)
                {
                    // (this branch is always taken on the first loop iteration, as firstInvalidLine > 0)

                    if (firstInvalidLine <= targetLineNumber)
                    {
                        // Skip valid lines to next invalid line:
                        engine.CurrentSpanStack = spanStacks[firstInvalidLine - 1];
                        currentLine = firstInvalidLine;
                    }
                    else
                    {
                        // Skip valid lines to target line:
                        engine.CurrentSpanStack = spanStacks[targetLineNumber];
                        break;
                    }
                }
                Debug.Assert(EqualSpanStacks(engine.CurrentSpanStack, spanStacks[currentLine - 1]));

                engine.ScanLine(document, document.GetLineByNumber(currentLine));
                
                UpdateTreeList(currentLine);
            }

            Debug.Assert(EqualSpanStacks(engine.CurrentSpanStack, spanStacks[targetLineNumber]));
        }

        private void UpdateTreeList(int lineNumber)
        {
            if (!EqualSpanStacks(engine.CurrentSpanStack, spanStacks[lineNumber]))
            {
                isValid[lineNumber] = true;

                spanStacks[lineNumber] = engine.CurrentSpanStack;

                if (lineNumber + 1 < isValid.Count)
                {
                    isValid[lineNumber + 1] = false;
                    firstInvalidLine = lineNumber + 1;
                }
                else
                {
                    firstInvalidLine = int.MaxValue;
                }

                if (lineNumber + 1 < document.LineCount)
                {
                    OnStylingStateChanged(lineNumber + 1, lineNumber + 1);
                }
            }
            else if (firstInvalidLine == lineNumber)
            {
                isValid[lineNumber] = true;

                firstInvalidLine = isValid.IndexOf(false);

                if (firstInvalidLine < 0)
                {
                    firstInvalidLine = int.MaxValue;
                }
            }
        }

        private static bool EqualSpanStacks(SpanStack a, SpanStack b)
        {
            // We must use value equality between the stacks because Styler.OnStyleStateChanged
            // depends on the fact that equal input state + unchanged line contents produce equal output state.
            if (a == b)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            while (!a.IsEmpty && !b.IsEmpty)
            {
                if (a.Peek() != b.Peek())
                {
                    return false;
                }

                a = a.Pop();
                b = b.Pop();

                if (a == b)
                {
                    return true;
                }
            }

            return a.IsEmpty && b.IsEmpty;
        }

        /// <inheritdoc/>
        public event StylingStateChangedEventHandler? StylingStateChanged;

        /// <summary>
        /// Is called when the styling state at the end of the specified line has changed.
        /// </summary>
        /// <remarks>This callback must not call styleLine or InvalidateStyling.
        /// It may call GetSpanStack, but only for the changed line and lines above.
        /// This method must not modify the document.</remarks>
        protected virtual void OnStylingStateChanged(int fromLineNumber, int toLineNumber) => StylingStateChanged?.Invoke(fromLineNumber, toLineNumber);

        /// <inheritdoc/>
        public Style DefaultStyle => null!;

        /// <inheritdoc/>
        public void BeginStyling()
        {
            if (isInStylingGroup)
            {
                throw new InvalidOperationException("Styling group is already open");
            }

            isInStylingGroup = true;
        }

        /// <inheritdoc/>
        public void EndStyling()
        {
            if (!isInStylingGroup)
            {
                throw new InvalidOperationException("Styling group is not open");
            }

            isInStylingGroup = false;
        }
    }
}
