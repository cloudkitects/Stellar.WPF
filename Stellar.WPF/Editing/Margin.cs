using System;
using System.Diagnostics;
using System.Windows;

using Stellar.WPF.Document;
using Stellar.WPF.Rendering;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Base class for margins.
    /// Margins don't have to derive from this class, it just helps maintaining a reference to the TextView
    /// and the TextDocument.
    /// AbstractMargin derives from FrameworkElement, so if you don't want to handle visual children and rendering
    /// on your own, choose another base class for your margin!
    /// </summary>
    public abstract class Margin : FrameworkElement, ITextViewConnector
    {
        private bool autoAttached;
        private Document.Document? document;

        /// <summary>
        /// TextView property.
        /// </summary>
        public static readonly DependencyProperty TextViewProperty =
            DependencyProperty.Register("TextView", typeof(TextView), typeof(Margin),
                                        new FrameworkPropertyMetadata(OnTextViewChanged));

        /// <summary>
        /// Gets/sets the text view for which line numbers are displayed.
        /// </summary>
        /// <remarks>Adding a margin to <see cref="TextArea.LeftMargins"/> will automatically set this property to the text area's TextView.</remarks>
        public TextView? TextView {
            get => (TextView)GetValue(TextViewProperty);
            set => SetValue(TextViewProperty, value);
        }

        private static void OnTextViewChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            var margin = (Margin)dp;
            
            margin.autoAttached = false;
            margin.OnTextViewChanged((TextView)e.OldValue, (TextView)e.NewValue);
        }

        void ITextViewConnector.AttachTo(TextView textView)
        {
            if (TextView is null)
            {
                TextView = textView;
                autoAttached = true;
            }
            else if (TextView != textView)
            {
                throw new InvalidOperationException("This margin belongs to a different TextView.");
            }
        }

        void ITextViewConnector.DetachFrom(TextView textView)
        {
            if (autoAttached && TextView == textView)
            {
                TextView = null;
                
                Debug.Assert(!autoAttached);
            }
        }


        /// <summary>
        /// Gets the document associated with the margin.
        /// </summary>
        public Document.Document Document => document!;

        /// <summary>
        /// Called when the <see cref="TextView"/> is changing.
        /// </summary>
        protected virtual void OnTextViewChanged(TextView oldTextView, TextView newTextView)
        {
            if (oldTextView is not null)
            {
                oldTextView.DocumentChanged -= TextViewDocumentChanged;
            }

            if (newTextView is not null)
            {
                newTextView.DocumentChanged += TextViewDocumentChanged;
            }

            TextViewDocumentChanged(null, null);
        }

        private void TextViewDocumentChanged(object? sender, EventArgs? e)
        {
            OnDocumentChanged(document, TextView?.Document);
        }

        /// <summary>
        /// Called when the <see cref="Document"/> is changing.
        /// </summary>
        protected virtual void OnDocumentChanged(Document.Document? oldDocument, Document.Document? newDocument)
        {
            document = newDocument;
        }
    }
}
