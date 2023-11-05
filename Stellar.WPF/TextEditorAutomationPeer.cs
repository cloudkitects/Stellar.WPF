using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

using Stellar.WPF.Utilities;

namespace Stellar.WPF
{
    /// <summary>
    /// Exposes <see cref="Stellar.WPF.TextEditor"/> to automation.
    /// </summary>
    public class TextEditorAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
    {
        /// <summary>
        /// Creates a new TextEditorAutomationPeer instance.
        /// </summary>
        public TextEditorAutomationPeer(TextEditor owner) : base(owner)
        {
            Debug.WriteLine("TextEditorAutomationPeer created");
        }

        private TextEditor TextEditor => (TextEditor)Owner;

        void IValueProvider.SetValue(string value) => TextEditor.Text = value;

        string IValueProvider.Value => TextEditor.Text;

        bool IValueProvider.IsReadOnly => TextEditor.IsReadOnly;

        /// <inheritdoc/>
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        /// <inheritdoc/>
        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Value)
            {
                return this;
            }

            if (patternInterface == PatternInterface.Scroll)
            {
                var scrollViewer = TextEditor.ScrollViewer;
                
                if (scrollViewer is not null)
                {
                    return FromElement(scrollViewer);
                }
            }

            if (patternInterface == PatternInterface.Text)
            {
                return FromElement(TextEditor.TextArea);
            }

            return base.GetPattern(patternInterface);
        }

        internal void RaiseIsReadOnlyChanged(bool oldValue, bool newValue) => RaisePropertyChangedEvent(ValuePatternIdentifiers.IsReadOnlyProperty, Boxed.Box(oldValue), Boxed.Box(newValue));
    }
}
