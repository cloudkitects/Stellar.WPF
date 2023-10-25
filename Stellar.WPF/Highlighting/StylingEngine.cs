using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Stellar.WPF.Document;

using SpanStack = Stellar.WPF.Utilities.ImmutableStack<Stellar.WPF.Highlighting.Span>;

namespace Stellar.WPF.Highlighting
{
    /// <summary>
    /// Regex-based styling engine.
    /// </summary>
    public class StylingEngine
    {
        #region fields and props
        private readonly RuleSet ruleSet;

        // local variables from HighlightLineInternal (are member because they are accessed by HighlighLine helper methods)
        private string lineText = string.Empty;
        private int lineOffset;
        private int currentIndex;

        private Stack<StyledSection>? StyledSections;
        private StyledSection? currentSection;

        /// <summary>
        /// The line being augmented with styles.
        /// </summary>
        /// <remarks>
        /// Only the span state is updated when null.
        /// </remarks>
        private StyledLine? styledLine;

        private SpanStack spanStack = SpanStack.Empty;

        public SpanStack CurrentSpanStack
        {
            get => spanStack;
            set => spanStack = value ?? SpanStack.Empty;
        }
        #endregion

        /// <summary>
        /// Creates a new styling engine instance.
        /// </summary>
        public StylingEngine(RuleSet ruleSet)
        {
            this.ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
        }

        /// <summary>
        /// The rule set passed-in to the constructor or the
        /// current span's rule set if actively styling.
        /// </summary>
        private RuleSet GetCurrentRuleSet()
        {
            return spanStack.IsEmpty
                ? ruleSet
                : spanStack.Peek().RuleSet ?? new() { Name = "<Empty>" };
        }

        /// <summary>
        /// Creates and returns a styled line out of the specified line in the specified document.
        /// </summary>
        /// <remarks>
        /// <see cref="CurrentSpanStack"/> must be set to the proper state for the beginning of
        /// this line before calling this method, and will be updated to represent the state
        /// after all styles are applied.
        /// </remarks>
        public StyledLine StyleLine(IDocument document, ILine line)
        {
            lineOffset = line.Offset;
            lineText = document.GetText(line);

            try
            {
                styledLine = new StyledLine(document, line);

                Style();

                return styledLine;
            }
            finally
            {
                styledLine = null;
                lineText = null!;
                lineOffset = 0;
            }
        }

        /// <summary>
        /// Scans the specified line in the specified document to update the <see cref="CurrentSpanStack"/>.
        /// </summary>
        /// <remarks>
        /// See <see cref="StyleLine(IDocument, ILine)"/> remarks. 
        /// </remarks>
        public void ScanLine(IDocument document, ILine line)
        {
            lineText = document.GetText(line);

            try
            {
                Debug.Assert(styledLine is null);

                Style();
            }
            finally
            {
                lineText = null!;
            }
        }

        /// <summary>
        /// The engine's main purpose: apply styles based on the current rule set and span matches.
        /// </summary>
        private void Style()
        {
            currentIndex = 0;

            ResetStyledSections();

            var currentRuleSet = GetCurrentRuleSet();
            var matchesStack = new Stack<Match[]>();
            var matches = RedimMatches(currentRuleSet.Spans.Count);
            Match endMatch = null!;

            while (true)
            {
                // populate matches
                for (var i = 0; i < matches.Length; i++)
                {
                    if (matches[i] is null || (matches[i].Success && matches[i].Index < currentIndex))
                    {
                        matches[i] = currentRuleSet.Spans[i].StartRegex!.Match(lineText, currentIndex);
                    }
                }

                // get the end/closing match
                if (endMatch is null && !spanStack.IsEmpty)
                {
                    endMatch = spanStack.Peek().EndRegex!.Match(lineText, currentIndex);
                }

                // find the first match before the closing one
                var firstMatch = FirstOrDefault(matches, endMatch);

                if (firstMatch is null)
                {
                    break;
                }

                // style in between
                StyleUntil(firstMatch.Index);

                Debug.Assert(currentIndex == firstMatch.Index);

                if (firstMatch == endMatch)
                {
                    var poppedSpan = spanStack.Peek();

                    if (!poppedSpan.StyleIncludesEnd)
                    {
                        PopSection();
                    }

                    PushSection(poppedSpan.EndStyle);

                    currentIndex = firstMatch.Index + firstMatch.Length;

                    PopSection();

                    if (poppedSpan.StyleIncludesEnd)
                    {
                        PopSection();
                    }

                    spanStack = spanStack.Pop();

                    currentRuleSet = GetCurrentRuleSet();

                    if (matchesStack.Count > 0)
                    {
                        matches = matchesStack.Pop();

                        var index = currentRuleSet.Spans.IndexOf(poppedSpan);

                        Debug.Assert(index >= 0 && index < matches.Length);

                        if (matches[index].Index == currentIndex)
                        {
                            throw new InvalidOperationException(
                                $"{poppedSpan.StartRegex} or {poppedSpan.EndRegex} must match at least one character to prevent an endless loop.");
                        }
                    }
                    else
                    {
                        matches = RedimMatches(currentRuleSet.Spans.Count);
                    }
                }
                else
                {
                    var index = Array.IndexOf(matches, firstMatch);

                    Debug.Assert(index >= 0);

                    var newSpan = currentRuleSet.Spans[index];

                    spanStack = spanStack.Push(newSpan);

                    currentRuleSet = GetCurrentRuleSet();

                    matchesStack.Push(matches);

                    matches = RedimMatches(currentRuleSet.Spans.Count);


                    if (newSpan.StyleIncludesStart)
                    {
                        PushSection(newSpan.Style);
                    }

                    PushSection(newSpan.StartStyle);

                    currentIndex = firstMatch.Index + firstMatch.Length;

                    PopSection();

                    if (!newSpan.StyleIncludesStart)
                    {
                        PushSection(newSpan.Style);
                    }
                }

                endMatch = null!;
            }

            StyleUntil(lineText.Length);

            PopAllSections();
        }

        /// <summary>
        /// Continue styling past an identified and styled span. 
        /// </summary>
        private void StyleUntil(int stop)
        {
            Debug.Assert(currentIndex <= stop);

            if (currentIndex == stop)
            {
                return;
            }

            if (styledLine is not null)
            {
                var rules = GetCurrentRuleSet().Rules;
                var matches = RedimMatches(rules.Count);

                while (true)
                {
                    for (var i = 0; i < matches.Length; i++)
                    {
                        if (matches[i] is null || (matches[i].Success && matches[i].Index < currentIndex))
                        {
                            matches[i] = rules[i].Regex.Match(lineText, currentIndex, stop - currentIndex);
                        }
                    }

                    var firstMatch = FirstOrDefault(matches, null);

                    if (firstMatch is null)
                    {
                        break;
                    }

                    currentIndex = firstMatch.Index;

                    var ruleIndex = Array.IndexOf(matches, firstMatch);

                    if (firstMatch.Length == 0)
                    {
                        throw new InvalidOperationException(
                                $"{rules[ruleIndex].Regex} must match at least one character to prevent an endless loop.");
                    }

                    PushSection(rules[ruleIndex].Style);

                    currentIndex = firstMatch.Index + firstMatch.Length;

                    PopSection();
                }
            }

            currentIndex = stop;
        }

        /// <summary>
        /// Reset the styled sections stack.
        /// </summary>
        private void ResetStyledSections()
        {
            Debug.Assert(currentIndex == 0);

            currentSection = null;
            
            if (styledLine is null)
            {
                StyledSections = null;
            }
            else
            {
                StyledSections = new Stack<StyledSection>();

                foreach (var span in spanStack.Reverse())
                {
                    PushSection(span.Style);
                }
            }
        }

        /// <summary>
        /// Push a styled section into the stack.
        /// </summary>
        /// <param name="style">A style used to identify the current section.</param>
        private void PushSection(Style style)
        {
            if (styledLine is null)
            {
                return;
            }

            if (style is null)
            {
                StyledSections?.Push(null!);
            }
            else if (currentSection is not null &&
                     currentSection.Style == style &&
                     currentSection.Offset + currentSection.Length == currentIndex + lineOffset)
            {
                StyledSections?.Push(currentSection);
                currentSection = null;
            }
            else
            {
                var section = new StyledSection
                {
                    Offset = currentIndex + lineOffset,
                    Style = style
                };

                styledLine.Sections.Add(section);
                
                StyledSections?.Push(section);
                
                currentSection = null;
            }
        }

        /// <summary>
        /// Pop a styled section out of the stack.
        /// </summary>
        private void PopSection()
        {
            if (styledLine is null)
            {
                return;
            }

            var section = StyledSections?.Pop();

            if (section is not null)
            {
                section.Length = (currentIndex + lineOffset) - section.Offset;

                if (section.Length == 0)
                {
                    styledLine.Sections.Remove(section);
                }
                else
                {
                    currentSection = section;
                }
            }
        }

        private void PopAllSections()
        {
            if (StyledSections is not null)
            {
                while (StyledSections.Count > 0)
                {
                    PopSection();
                }
            }
        }

        /// <summary>
        /// The first successful match from the array or the default.
        /// </summary>
        private static Match FirstOrDefault(Match[] matches, Match? def)
        {
            Match min = null!;

            foreach (var match in matches)
            {
                if (match.Success && (min is null || match.Index < min.Index))
                {
                    min = match;
                }
            }

            return def is not null && def.Success && (min is null || def.Index < min.Index)
                ? def
                : min;
        }

        /// <summary>
        /// Return a <see cref="Match"/> array with the specified capacity.
        /// </summary>
        /// <param name="capacity"></param>
        /// <returns></returns>
        private static Match[] RedimMatches(int capacity)
        {
            return capacity == 0
                ? Array.Empty<Match>()
                : (new Match[capacity]);
        }
    }
}
