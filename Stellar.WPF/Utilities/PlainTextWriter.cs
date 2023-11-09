using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stellar.WPF.Utilities;

/// <summary>
/// Plain, text-only styled text writer implementation.
/// </summary>
internal class PlainTextWriter : StyledTextWriter
{
    /// <summary>
    /// The text writer passed in to the constructor.
    /// </summary>
    protected readonly TextWriter textWriter;
    
    string indentationString = "\t";
    
    int indentationLevel;
    
    char prevChar;

    /// <summary>
    /// Creates a new PlainTextWriter instance.
    /// </summary>
    public PlainTextWriter(TextWriter textWriter)
    {
        this.textWriter = textWriter ?? throw new ArgumentNullException(nameof(textWriter));
    }

    /// <summary>
    /// Gets/Sets the string used to indent by one level.
    /// </summary>
    public string IndentationString
    {
        get => indentationString;
        set => indentationString = value;
    }

    /// <inheritdoc/>
    protected override void BeginUnhandledSpan()
    {
    }

    /// <inheritdoc/>
    public override void EndSpan()
    {
    }

    void WriteIndentation()
    {
        for (var i = 0; i < indentationLevel; i++)
        {
            textWriter.Write(indentationString);
        }
    }

    /// <summary>
    /// Writes the indentation, if necessary.
    /// </summary>
    protected void WriteIndentationIfNecessary()
    {
        if (prevChar != '\n')
        {
            return;
        }

        WriteIndentation();

        prevChar = '\0';
    }

    /// <summary>
    /// Is called after a write operation.
    /// </summary>
    protected virtual void AfterWrite()
    {
    }

    /// <inheritdoc/>
    public override void Write(char value)
    {
        if (prevChar == '\n')
        {
            WriteIndentation();
        }

        textWriter.Write(value);
        
        prevChar = value;
        
        AfterWrite();
    }

    /// <inheritdoc/>
    public override void Indent()
    {
        indentationLevel++;
    }

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
    public override Encoding Encoding => textWriter.Encoding;

    /// <inheritdoc/>
    public override IFormatProvider FormatProvider => textWriter.FormatProvider;

    /// <inheritdoc/>
    public override string NewLine
    {
        get => textWriter.NewLine;
        set => textWriter.NewLine = value;
    }
}