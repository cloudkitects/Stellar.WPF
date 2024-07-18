using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Media;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling
{
    /// <summary>
    /// RichTextWriter implementation that produces HTML.
    /// </summary>
    internal class HtmlStyledTextWriter : StyledTextWriter
    {
        private static readonly char[] specialChars = { ' ', '\t', '\r', '\n' };

        private readonly TextWriter htmlWriter;
        private readonly HtmlOptions options;
        private readonly Stack<string> endTagStack = new();
        private bool spaceNeedsEscaping = true;
        private bool hasSpace;
        private bool needIndentation = true;
        private int indentationLevel;

        /// <summary>
        /// Creates a new HtmlRichTextWriter instance.
        /// </summary>
        /// <param name="htmlWriter">
        /// The text writer where the raw HTML is written to.
        /// The HtmlRichTextWriter does not take ownership of the htmlWriter;
        /// disposing the HtmlRichTextWriter will not dispose the underlying htmlWriter!
        /// </param>
        /// <param name="options">Options that control the HTML output.</param>
        public HtmlStyledTextWriter(TextWriter htmlWriter, HtmlOptions options = null)
        {
            this.htmlWriter = htmlWriter ?? throw new ArgumentNullException(nameof(htmlWriter));
            this.options = options ?? new HtmlOptions();
        }

        /// <inheritdoc/>
        public override Encoding Encoding => htmlWriter.Encoding;

        /// <inheritdoc/>
        public override void Flush()
        {
            FlushSpace(true); // next char potentially might be whitespace

            htmlWriter.Flush();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FlushSpace(true);
            }
            
            base.Dispose(disposing);
        }

        private void FlushSpace(bool nextIsWhitespace)
        {
            if (hasSpace)
            {
                htmlWriter.Write(spaceNeedsEscaping || nextIsWhitespace ? "&nbsp;" : ' ');

                hasSpace = false;
                spaceNeedsEscaping = true;
            }
        }

        private void WriteIndentation()
        {
            if (needIndentation)
            {
                for (var i = 0; i < indentationLevel; i++)
                {
                    WriteChar('\t');
                }

                needIndentation = false;
            }
        }

        /// <inheritdoc/>
        public override void Write(char value)
        {
            WriteIndentation();

            WriteChar(value);
        }

        private void WriteChar(char c)
        {
            var isWhitespace = char.IsWhiteSpace(c);
            
            FlushSpace(isWhitespace);
            
            switch (c)
            {
                case ' ':
                    if (spaceNeedsEscaping)
                    {
                        htmlWriter.Write("&nbsp;");
                    }
                    else
                    {
                        hasSpace = true;
                    }

                    break;
                
                case '\t':
                    for (var i = 0; i < options.TabSize; i++)
                    {
                        htmlWriter.Write("&nbsp;");
                    }
                    break;

                case '\r':
                    break; // ignore; we'll write the <br/> with the following \n
                
                case '\n':
                    htmlWriter.Write("<br/>");
                    needIndentation = true;
                    break;
                
                default:
                    WebUtility.HtmlEncode(c.ToString(), htmlWriter);
                    break;
            }
            
            // If we just handled a space by setting hasSpace = true,
            // we mustn't set spaceNeedsEscaping as doing so would affect our own space,
            // not just the following spaces.
            if (c != ' ')
            {
                // Following spaces must be escaped if c was a newline/tab;
                // and they don't need escaping if c was a normal character.
                spaceNeedsEscaping = isWhitespace;
            }
        }

        /// <inheritdoc/>
        public override void Write(string? value)
        {
            var pos = 0;
            
            do
            {
                var endPos = value.IndexOfAny(specialChars, pos);
                
                if (endPos < 0)
                {
                    WriteSimpleString(value.Substring(pos));
                    return; // reached end of string
                }
                
                if (endPos > pos)
                {
                    WriteSimpleString(value.Substring(pos, endPos - pos));
                }

                WriteChar(value[endPos]);
                
                pos = endPos + 1;
            }
            while (pos < value.Length);
        }

        private void WriteIndentationAndSpace()
        {
            WriteIndentation();

            FlushSpace(false);
        }

        private void WriteSimpleString(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            WriteIndentationAndSpace();
            
            WebUtility.HtmlEncode(value, htmlWriter);
        }

        /// <inheritdoc/>
        public override void Indent() => indentationLevel++;

        /// <inheritdoc/>
        public override void Unindent()
        {
            if (indentationLevel == 0)
            {
                throw new NotSupportedException();
            }

            indentationLevel--;
        }

        /// <inheritdoc/>
        protected override void BeginUnhandledSpan() => endTagStack.Push(null!);

        /// <inheritdoc/>
        public override void EndSpan() => htmlWriter.Write(endTagStack.Pop());

        /// <inheritdoc/>
        public override void BeginSpan(Color foregroundColor) => BeginSpan(new Style { Foreground = new SimpleBrush(foregroundColor) });

        /// <inheritdoc/>
        public override void BeginSpan(FontFamily fontFamily) => BeginUnhandledSpan(); // TODO

        /// <inheritdoc/>
        public override void BeginSpan(FontStyle fontStyle) => BeginSpan(new Style { FontStyle = fontStyle });

        /// <inheritdoc/>
        public override void BeginSpan(FontWeight fontWeight) => BeginSpan(new Style { FontWeight = fontWeight });

        /// <inheritdoc/>
        public override void BeginSpan(Style style)
        {
            WriteIndentationAndSpace();

            if (options.ColorNeedsSpanForStyling(style))
            {
                htmlWriter.Write("<span");
                
                options.WriteStyleAttributeForColor(htmlWriter, style);
                
                htmlWriter.Write('>');
                
                endTagStack.Push("</span>");
            }
            else
            {
                endTagStack.Push(null!);
            }
        }

        /// <inheritdoc/>
        public override void BeginHyperlinkSpan(Uri uri)
        {
            WriteIndentationAndSpace();
            
            var link = WebUtility.HtmlEncode(uri.ToString());
            
            htmlWriter.Write("<a href=\"" + link + "\">");
            
            endTagStack.Push("</a>");
        }
    }
}
