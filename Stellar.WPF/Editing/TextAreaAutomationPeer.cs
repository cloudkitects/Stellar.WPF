using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

class TextAreaAutomationPeer : FrameworkElementAutomationPeer, IValueProvider, ITextProvider
{
    public TextAreaAutomationPeer(TextArea owner)
        : base(owner)
    {
        owner.Caret.PositionChanged += OnSelectionChanged;
        owner.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine("RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged)");

        RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
    }

    private TextArea TextArea => (TextArea)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    internal IRawElementProviderSimple Provider => ProviderFromPeer(this);

    public bool IsReadOnly => TextArea.EditableSectionProvider == WritableSectionProvider.Instance;

    public void SetValue(string value) => TextArea.Document.Text = value;

    public string Value => TextArea.Document.Text;

    public ITextRangeProvider DocumentRange
    {
        get
        {
            Debug.WriteLine("TextAreaAutomationPeer.get_DocumentRange()");
            return new TextRangeProvider(TextArea, TextArea.Document, 0, TextArea.Document.TextLength);
        }
    }

    public ITextRangeProvider[] GetSelection()
    {
        Debug.WriteLine("TextAreaAutomationPeer.GetSelection()");
        if (TextArea.Selection.IsEmpty)
        {
            var anchor = TextArea.Document.CreateAnchor(TextArea.Caret.Offset);
            anchor.SurvivesDeletion = true;
            return new ITextRangeProvider[] { new TextRangeProvider(TextArea, TextArea.Document, new AnchorSegment(anchor, anchor)) };
        }
        return TextArea.Selection.Segments.Select(s => new TextRangeProvider(TextArea, TextArea.Document, s)).ToArray();
    }

    public ITextRangeProvider[] GetVisibleRanges() => throw new NotImplementedException("TextAreaAutomationPeer.GetVisibleRanges()");

    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement) => throw new NotImplementedException("TextAreaAutomationPeer.RangeFromChild()");

    public ITextRangeProvider RangeFromPoint(System.Windows.Point screenLocation) => throw new NotImplementedException("TextAreaAutomationPeer.RangeFromPoint()");

    public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;

    public override object GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Text)
        {
            return this;
        }

        if (patternInterface == PatternInterface.Value)
        {
            return this;
        }

        if (patternInterface == PatternInterface.Scroll)
        {
            if (TextArea.GetService(typeof(TextEditor)) is TextEditor editor)
            {
                var fromElement = FromElement(editor);
                
                if (fromElement is not null)
                {
                    return fromElement.GetPattern(patternInterface);
                }
            }
        }

        return base.GetPattern(patternInterface);
    }
}
