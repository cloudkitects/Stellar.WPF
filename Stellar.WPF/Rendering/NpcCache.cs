using System;
using System.Collections.Generic;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// a cache of non-printable character (NPC) texts.
/// </summary>
internal sealed class NpcCache : IDisposable
{
    private TextFormatter? formatter;
    private Dictionary<string, TextLine>? npcTexts;

    public TextLine? GetText(string text, ITextRunContext context)
    {
        npcTexts ??= new Dictionary<string, TextLine>();

        if (!npcTexts.TryGetValue(text, out TextLine? textLine))
        {
            var properties = new VisualLineTextRunProperties(context.GlobalTextRunProperties);

            properties.SetForegroundBrush(context.TextView.NonPrintableCharacterBrush);

            formatter ??= TextFormatterFactory.Create(context.TextView);

            textLine = FormattedTextElement.PrepareText(formatter, text, properties);

            npcTexts[text] = textLine;
        }

        return textLine;
    }

    public void Dispose()
    {
        if (npcTexts is not null)
        {
            foreach (var line in npcTexts.Values)
            {
                line.Dispose();
            }
        }

        formatter?.Dispose();
    }
}