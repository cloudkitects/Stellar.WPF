﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

using Stellar.WPF.Document;
using Stellar.WPF.Styling;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Base class for selections.
    /// </summary>
    public abstract class Selection
	{
        internal readonly TextArea textArea;
        
		/// <summary>
        /// Creates a new simple selection that selects the text from startOffset to endOffset.
        /// </summary>
        public static Selection Create(TextArea textArea, int startOffset, int endOffset)
		{
            if (startOffset == endOffset)
            {
                return textArea.emptySelection;
            }
            
			return new SimpleSelection(
				textArea ?? throw new ArgumentNullException(nameof(textArea)),
				new TextViewPosition(textArea.Document.GetLocation(startOffset)),
				new TextViewPosition(textArea.Document.GetLocation(endOffset)));
        }

		internal static Selection Create(TextArea textArea, TextViewPosition start, TextViewPosition end)
		{
			if (textArea is null)
            {
                throw new ArgumentNullException(nameof(textArea));
            }

            if (textArea.Document.GetOffset(start.Location) == textArea.Document.GetOffset(end.Location) &&
				start.VisualColumn == end.VisualColumn)
            {
                return textArea.emptySelection;
            }
            
			return new SimpleSelection(textArea, start, end);
        }

		/// <summary>
		/// Creates a new simple selection that selects the text in the specified segment.
		/// </summary>
		public static Selection Create(TextArea textArea, ISegment segment)
		{
            return Create(textArea ?? throw new ArgumentNullException(nameof(segment)), segment.Offset, segment.EndOffset);
		}

		/// <summary>
		/// Constructor for Selection.
		/// </summary>
		protected Selection(TextArea textArea)
		{
            this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
		}

		/// <summary>
		/// Gets the start position of the selection.
		/// </summary>
		public abstract TextViewPosition StartPosition { get; }

		/// <summary>
		/// Gets the end position of the selection.
		/// </summary>
		public abstract TextViewPosition EndPosition { get; }

		/// <summary>
		/// Gets the selected text segments.
		/// </summary>
		public abstract IEnumerable<SelectionSegment> Segments { get; }

		/// <summary>
		/// Gets the smallest segment that contains all segments in this selection.
		/// May return null if the selection is empty.
		/// </summary>
		public abstract ISegment SurroundingSegment { get; }

		/// <summary>
		/// Replaces the selection with the specified text.
		/// </summary>
		public abstract void ReplaceSelectionWithText(string newText);

		internal string AddSpacesIfRequired(string newText, TextViewPosition start, TextViewPosition end)
		{
			if (EnableVirtualSpace && InsertVirtualSpaces(newText, start, end))
			{
				var line = textArea.Document.GetLineByNumber(start.Line);
				var lineText = textArea.Document.GetText(line);
				var vLine = textArea.TextView.GetOrConstructVisualLine(line);
				
				var colDiff = start.VisualColumn - vLine.VisualLengthWithEndOfLineMarker;

				if (colDiff > 0)
				{
					var additionalSpaces = string.Empty;

					if (!textArea.Options.ConvertTabsToSpaces && lineText.Trim('\t').Length == 0)
					{
						var tabCount = colDiff / textArea.Options.IndentationSize;
						
						additionalSpaces = new string('\t', tabCount);
						
						colDiff -= tabCount * textArea.Options.IndentationSize;
					}

					additionalSpaces += new string(' ', colDiff);
					
					return additionalSpaces + newText;
				}
			}

			return newText;
		}

		bool InsertVirtualSpaces(string newText, TextViewPosition start, TextViewPosition end)
		{
			return (!string.IsNullOrEmpty(newText) || !(IsInVirtualSpace(start) && IsInVirtualSpace(end)))
				&& newText != "\r\n"
				&& newText != "\n"
				&& newText != "\r";
		}

		bool IsInVirtualSpace(TextViewPosition pos)
		{
			return pos.VisualColumn > textArea.TextView.GetOrConstructVisualLine(textArea.Document.GetLineByNumber(pos.Line)).VisualLength;
		}

		/// <summary>
		/// Updates the selection when the document changes.
		/// </summary>
		public abstract Selection UpdateOnDocumentChange(DocumentChangeEventArgs e);

		/// <summary>
		/// Gets whether the selection is empty.
		/// </summary>
		public virtual bool IsEmpty {
			get { return Length == 0; }
		}

		/// <summary>
		/// Gets whether virtual space is enabled for this selection.
		/// </summary>
		public virtual bool EnableVirtualSpace {
			get { return textArea.Options.EnableVirtualSpace; }
		}

		/// <summary>
		/// Gets the selection length.
		/// </summary>
		public abstract int Length { get; }

		/// <summary>
		/// Returns a new selection with the changed end point.
		/// </summary>
		/// <exception cref="NotSupportedException">Cannot set endpoint for empty selection</exception>
		public abstract Selection SetEndpoint(TextViewPosition endPosition);

		/// <summary>
		/// If this selection is empty, starts a new selection from <paramref name="startPosition"/> to
		/// <paramref name="endPosition"/>, otherwise, changes the endpoint of this selection.
		/// </summary>
		public abstract Selection StartSelectionOrSetEndpoint(TextViewPosition startPosition, TextViewPosition endPosition);

		/// <summary>
		/// Gets whether the selection is multi-line.
		/// </summary>
		public virtual bool IsMultiline {
			get {
				ISegment surroundingSegment = this.SurroundingSegment;
				if (surroundingSegment == null)
                {
                    return false;
                }

                var start = surroundingSegment.Offset;
				var end = start + surroundingSegment.Length;
				
				var document = textArea.Document ?? throw new InvalidOperationException("The text area document is null");

                return document.GetLineByOffset(start) != document.GetLineByOffset(end);
			}
		}

		/// <summary>
		/// Gets the selected text.
		/// </summary>
		public virtual string GetText()
		{
			var document = textArea.Document ?? throw new InvalidOperationException("The text area document is null");

            var b = new StringBuilder();
			string? text = null;

            foreach (var segment in Segments)
            {
				if (text is not null)
				{
					
					if (b == null)
                    {
                        b = new StringBuilder(text);
                    }
                    else
                    {
                        b.Append(text);
                    }
                }

				text = document.GetText(segment);
			}

			if (b is not null)
			{
				if (text is not null)
                {
                    b.Append(text);
                }

                return b.ToString();
			}
			
			return text ?? string.Empty;
		}

		
		/// <summary>
		/// Creates a HTML fragment for the selected text.
		/// </summary>
		public string CreateHtmlFragment(HtmlOptions options)
		{
			if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IStyler highlighter = textArea.GetService(typeof(IStyler)) as IStyler;
			StringBuilder html = new StringBuilder();
			bool first = true;
			foreach (ISegment selectedSegment in this.Segments) {
				if (first)
                {
                    first = false;
                }
                else
                {
                    html.AppendLine("<br>");
                }

                html.Append(HtmlClipboard.CreateHtmlFragment(textArea.Document, highlighter, selectedSegment, options));
			}
			return html.ToString();
		}
		
		/// <inheritdoc/>
		public abstract override bool Equals(object obj);

		/// <inheritdoc/>
		public abstract override int GetHashCode();

		/// <summary>
		/// Gets whether the specified offset is included in the selection.
		/// </summary>
		/// <returns>True, if the selection contains the offset (selection borders inclusive);
		/// otherwise, false.</returns>
		public virtual bool Contains(int offset)
		{
			if (this.IsEmpty)
            {
                return false;
            }

            if (this.SurroundingSegment.Contains(offset, 0)) {
				foreach (ISegment s in this.Segments) {
					if (s.Contains(offset, 0)) {
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Creates a data object containing the selection's text.
		/// </summary>
		public virtual DataObject CreateDataObject(TextArea textArea)
		{
			var data = new DataObject();

			// Ensure we use the appropriate newline sequence for the OS
			string text = GetText().NormalizeNewLines(Environment.NewLine)!;

			// Enable drag/drop to Word, Notepad++ and others
			if (EditingCommandHandler.ConfirmDataFormat(textArea, data, DataFormats.UnicodeText))
			{
				data.SetText(text);
			}

			// Enable drag/drop to SciTe:
			// We cannot use SetText, thus we need to use typeof(string).FullName as data format.
			// new DataObject(object) calls SetData(object), which in turn calls SetData(Type, data),
			// which then uses Type.FullName as format.
			// We immitate that behavior here as well:
			if (EditingCommandHandler.ConfirmDataFormat(textArea, data, typeof(string).FullName))
			{
				data.SetData(typeof(string).FullName, text);
			}

			// Also copy text in HTML format to clipboard - good for pasting text into Word
			// or to the SharpDevelop forums.
			if (EditingCommandHandler.ConfirmDataFormat(textArea, data, DataFormats.Html))
			{
				HtmlClipboard.SetHtml(data, CreateHtmlFragment(new HtmlOptions(textArea.Options)));
			}

			return data;
		}
	}
}
