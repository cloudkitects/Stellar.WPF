using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

using SpanStack = Stellar.WPF.Utilities.ImmutableStack<Stellar.WPF.Highlighting.Span>;

namespace Stellar.WPF.Highlighting
{
    /// <summary>
    /// This class can syntax-highlight a document.
    /// It automatically manages invalidating the highlighting when the document changes.
    /// </summary>
    public class Highlighter : ILineTracker, IHighlighter
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
        private bool isHighlighting;
        private bool isInHighlightingGroup;
        private bool isDisposed;
        private SpanStack initialSpanStack = SpanStack.Empty;

        /// <summary>
        /// The document being highlighted.
        /// </summary>
        public IDocument Document => document;

        /// <summary>
        /// Creates a new DocumentHighlighter instance.
        /// </summary>
        public Highlighter(Document.Document document, ISyntax syntax)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            this.syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));

            engine = new StylingEngine(syntax.RuleSet);
            
            document.VerifyAccess();
            
            lineTracker = WeakLineTracker.Register(document, this);
            
            InvalidateSpanStacks();
        }

        /// <summary>
        /// Disposes the document highlighter.
        /// </summary>
        public void Dispose()
        {
            lineTracker?.Unregister();

            isDisposed = true;
        }

        #region ILineTracker
        void ILineTracker.BeforeRemoving(Line line)
        {
            CheckIsHighlighting();
            
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
            CheckIsHighlighting();

            var number = line.Number;

            isValid[number] = false;
            
            if (number < firstInvalidLine)
            {
                firstInvalidLine = number;
            }
        }

        void ILineTracker.AfterInserting(Line line, Line newLine)
        {
            CheckIsHighlighting();

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

                InvalidateHighlighting();
            }
        }

        /// <summary>
        /// Invalidates all stored highlighting info.
        /// When the document changes, the highlighting is invalidated automatically, this method
        /// needs to be called only when there are changes to the highlighting rule set.
        /// </summary>
        public void InvalidateHighlighting()
        {
            InvalidateSpanStacks();
            OnHighlightStateChanged(1, document.LineCount); // force a redraw with the new highlighting
        }

        /// <summary>
        /// Invalidates stored highlighting info, but does not raise the HighlightingStateChanged event.
        /// </summary>
        private void InvalidateSpanStacks()
        {
            CheckIsHighlighting();

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
        public StyledLine HighlightLine(int lineNumber)
        {
            if (lineNumber < 1 || document.LineCount < lineNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), $"{lineNumber} < 1 or {document.LineCount} < {lineNumber}");
            }

            CheckIsHighlighting();
            
            isHighlighting = true;
            
            try
            {
                HighlightUpTo(lineNumber - 1);

                var line = document.GetLineByNumber(lineNumber);
                
                var result = engine.StyleLine(document, line);

                UpdateTreeList(lineNumber);
                
                return result;
            }
            finally
            {
                isHighlighting = false;
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
                UpdateHighlightingState(lineNumber);
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

        private void CheckIsHighlighting()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException("Highlighter");
            }

            if (isHighlighting)
            {
                throw new InvalidOperationException("Invalid call, a highlighting operation is currently running.");
            }
        }

        /// <inheritdoc/>
        public void UpdateHighlightingState(int lineNumber)
        {
            CheckIsHighlighting();

            isHighlighting = true;
            
            try
            {
                HighlightUpTo(lineNumber);
            }
            finally
            {
                isHighlighting = false;
            }
        }

        /// <summary>
        /// Sets the engine's CurrentSpanStack to the end of the target line.
        /// Updates the span stack for all lines up to (and including) the target line, if necessary.
        /// </summary>
        private void HighlightUpTo(int targetLineNumber)
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
                    OnHighlightStateChanged(lineNumber + 1, lineNumber + 1);
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
            // We must use value equality between the stacks because HighlightingColorizer.OnHighlightStateChanged
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
        public event HighlightingStateChangedEventHandler? HighlightingStateChanged;

        /// <summary>
        /// Is called when the highlighting state at the end of the specified line has changed.
        /// </summary>
        /// <remarks>This callback must not call HighlightLine or InvalidateHighlighting.
        /// It may call GetSpanStack, but only for the changed line and lines above.
        /// This method must not modify the document.</remarks>
        protected virtual void OnHighlightStateChanged(int fromLineNumber, int toLineNumber) => HighlightingStateChanged?.Invoke(fromLineNumber, toLineNumber);

        /// <inheritdoc/>
        public Style DefaultStyle => null!;

        /// <inheritdoc/>
        public void BeginHighlighting()
        {
            if (isInHighlightingGroup)
            {
                throw new InvalidOperationException("Highlighting group is already open");
            }

            isInHighlightingGroup = true;
        }

        /// <inheritdoc/>
        public void EndHighlighting()
        {
            if (!isInHighlightingGroup)
            {
                throw new InvalidOperationException("Highlighting group is not open");
            }

            isInHighlightingGroup = false;
        }

        /// <inheritdoc/>
        public Style GetStyle(string name)
        {
            return syntax.GetStyle(name);
        }
    }
}
