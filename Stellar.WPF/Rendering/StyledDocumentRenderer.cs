using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows;

using Stellar.WPF.Styling;
using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A colorizes that interprets a styling rule set and colors the document accordingly.
/// </summary>
public class StyledDocumentRenderer : DocumentRenderer
{
    private readonly ISyntax? syntax;
    private TextView? textView;
    private IStyler? styler;
    private readonly bool isFixedStyler;

    /// <summary>
    /// Creates a new StyledDocumentRenderer instance.
    /// </summary>
    /// <param name="syntax">The styling definition.</param>
    public StyledDocumentRenderer(ISyntax syntax)
    {
        this.syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
    }

    /// <summary>
    /// Creates a new StyledDocumentRenderer instance that uses a fixed styler instance.
    /// The colorizer can only be used with text views that show the document for which
    /// the styler was created.
    /// </summary>
    /// <param name="styler">The styler to be used.</param>
    public StyledDocumentRenderer(IStyler styler)
    {
        this.styler = styler ?? throw new ArgumentNullException(nameof(styler));
        isFixedStyler = true;
    }

    /// <summary>
    /// Creates a new StyledDocumentRenderer instance.
    /// Derived classes using this constructor must override the <see cref="CreateStyler"/> method.
    /// </summary>
    protected StyledDocumentRenderer()
    {
    }

    private void textView_DocumentChanged(object sender, EventArgs e)
    {
        var textView = (TextView)sender;
        
        UnregisterServices(textView);
        RegisterServices(textView);
    }

    /// <summary>
    /// This method is called when a text view is removed from this StyledDocumentRenderer,
    /// and also when the TextDocument on any associated text view changes.
    /// </summary>
    protected virtual void UnregisterServices(TextView textView)
    {
        if (styler != null)
        {
            if (isInStylingGroup)
            {
                styler.EndStyling();
                isInStylingGroup = false;
            }
            styler.StylingStateChanged -= OnStylingStateChanged;
            
            // remove styler if it is registered
            if (textView.Services.GetService(typeof(IStyler)) == styler)
            {
                textView.Services.RemoveService(typeof(IStyler));
            }

            if (!isFixedStyler)
            {
                styler?.Dispose();

                styler = null;
            }
        }
    }

    /// <summary>
    /// This method is called when a new text view is added to this StyledDocumentRenderer,
    /// and also when the TextDocument on any associated text view changes.
    /// </summary>
    protected virtual void RegisterServices(TextView textView)
    {
        if (textView.Document != null)
        {
            if (!isFixedStyler)
            {
                styler = textView.Document != null ? CreateStyler(textView, textView.Document) : null;
            }

            if (styler != null && styler.Document == textView.Document)
            {
                // add service only if it doesn't already exist
                if (textView.Services.GetService(typeof(IStyler)) == null)
                {
                    textView.Services.AddService(typeof(IStyler), styler);
                }
                styler.StylingStateChanged += OnStylingStateChanged;
            }
        }
    }

    /// <summary>
    /// Creates the IStyler instance for the specified text document.
    /// </summary>
    protected virtual IStyler CreateStyler(TextView textView, Document.Document document)
    {
        if (syntax != null)
        {
            return new Styler(document, syntax);
        }
        else
        {
            throw new NotSupportedException("Cannot create a styler because no ISyntax was specified, and the CreateStyler() method was not overridden.");
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachTo(TextView textView)
    {
        if (this.textView != null)
        {
            throw new InvalidOperationException("Cannot use a StyledDocumentRenderer instance in multiple text views. Please create a separate instance for each text view.");
        }
        
        base.OnAttachTo(textView);
        this.textView = textView;
        
        textView.DocumentChanged += textView_DocumentChanged;
        textView.VisualLineConstructionStarting += textView_VisualLineConstructionStarting;
        textView.VisualLinesChanged += textView_VisualLinesChanged;
        
        RegisterServices(textView);
    }

    /// <inheritdoc/>
    protected override void OnDetachFrom(TextView textView)
    {
        UnregisterServices(textView);

        textView.DocumentChanged -= textView_DocumentChanged;
        textView.VisualLineConstructionStarting -= textView_VisualLineConstructionStarting;
        textView.VisualLinesChanged -= textView_VisualLinesChanged;
        
        base.OnDetachFrom(textView);
        this.textView = null;
    }

    private bool isInStylingGroup;

    private void textView_VisualLineConstructionStarting(object sender, VisualLineConstructionStartEventArgs e)
    {
        if (styler != null)
        {
            // Force update of styling state up to the position where we start generating visual lines.
            // This is necessary in case the document gets modified above the FirstLineInView so that the styling state changes.
            // We need to detect this case and issue a redraw (through OnStylingStateChanged)
            // before the visual line construction reuses existing lines that were built using the invalid styling state.
            lineBeingStyled = e.FirstLineInView.Number - 1;
            
            if (!isInStylingGroup)
            {
                // avoid opening group twice if there was an exception during the previous visual line construction
                // (not ideal, but better than throwing InvalidOperationException "group already open"
                // without any way of recovering)
                styler.BeginStyling();
                
                isInStylingGroup = true;
            }

            styler.UpdateStylingState(lineBeingStyled);
            
            lineBeingStyled = 0;
        }
    }

    private void textView_VisualLinesChanged(object sender, EventArgs e)
    {
        if (styler != null && isInStylingGroup)
        {
            styler.EndStyling();

            isInStylingGroup = false;
        }
    }

    private Line? lastStyledLine;

    /// <inheritdoc/>
    protected override void Render(ITextRunContext context)
    {
        lastStyledLine = null;

        base.Render(context);

        // the last line within the visual line can be missed, e.g., when the line ends with a fold marker.
        if (lastStyledLine != context.VisualLine.LastLine)
        {
            if (styler != null)
            {
                // update the styling state so that the proof within OnStylingStateChanged holds.
                lineBeingStyled = context.VisualLine.LastLine.Number;

                styler.UpdateStylingState(lineBeingStyled);
                
                lineBeingStyled = 0;
            }
        }

        lastStyledLine = null;
    }

    private int lineBeingStyled;

    /// <inheritdoc/>
    protected override void RenderLine(Line line)
    {
        if (styler != null)
        {
            lineBeingStyled = line.Number;
            var hl = styler.StyleLine(lineBeingStyled);
            
            lineBeingStyled = 0;
            
            foreach (var section in hl.Sections)
            {
                if (section.Style!.IsNullOrEmpty())
                {
                    continue;
                }

                RenderLineRange(
                    section.Offset,
                    section.Offset + section.Length,
                    element => ApplyStyle(element, section.Style!));
            }
        }
        lastStyledLine = line;
    }

    /// <summary>
    /// Applies a styling color to a visual line element.
    /// </summary>
    protected virtual void ApplyStyle(VisualLineElement element, Styling.Style style)
    {
        ApplyStyle(element, style, CurrentContext!);
    }

    internal static void ApplyStyle(VisualLineElement element, Styling.Style style, ITextRunContext context)
    {
        if (style.Foreground != null)
        {
            var brush = style.Foreground.GetBrush(context);
            
            if (brush != null)
            {
                element.TextRunProperties!.SetForegroundBrush(brush);
            }
        }
        
        if (style.Background != null)
        {
            var brush = style.Background.GetBrush(context);
            
            if (brush != null)
            {
                element.BackgroundBrush = brush;
            }
        }
        if (style.FontStyle != null || style.FontWeight != null || style.FontFamily != null)
        {
            var typeFace = element.TextRunProperties!.Typeface;
            
            element.TextRunProperties.SetTypeface(new Typeface(
                style.FontFamily ?? typeFace.FontFamily,
                style.FontStyle  ?? typeFace.Style,
                style.FontWeight ?? typeFace.Weight,
                typeFace.Stretch
            ));
        }
        
        if (style.Underline ?? false)
        {
            element.TextRunProperties!.SetTextDecorations(TextDecorations.Underline);
        }

        if (style.Strikethrough ?? false)
        {
            element.TextRunProperties!.SetTextDecorations(TextDecorations.Strikethrough);
        }

        if (style.FontSize.HasValue)
        {
            element.TextRunProperties!.SetFontRenderingEmSize(style.FontSize.Value);
        }
    }

    /// <summary>
    /// This method is responsible for telling the TextView to redraw lines when the styling state has changed.
    /// </summary>
    /// <remarks>
    /// Creation of a VisualLine triggers the syntax styler (which works on-demand), so it says:
    /// Hey, the user typed "/*". Don't just recreate that line, but also the next one
    /// because my styling state (at end of line) changed!
    /// </remarks>
    private void OnStylingStateChanged(int frLineNumber, int toLineNumber)
    {
        if (lineBeingStyled != 0)
        {
            // Ignore notifications for any line except the one we're interested in.
            // This improves the performance as Redraw() can take quite some time when called repeatedly
            // while scanning the document (above the visible area) for styling changes.
            if (toLineNumber <= lineBeingStyled)
            {
                return;
            }
        }

        // The user may have inserted "/*" into the current line, and so far only that line got redrawn.
        // So when the styling state is changed, we issue a redraw for the line immediately below.
        // If the styling state change applies to the lines below, too, the construction of each line
        // will invalidate the next line, and the construction pass will regenerate all lines.

        Debug.WriteLine(string.Format("OnStylingStateChanged forces redraw of lines {0} to {1}", frLineNumber, toLineNumber));

        // If the VisualLine construction is in progress, we have to avoid sending redraw commands for
        // anything above the line currently being constructed.
        // It takes some explanation to see why this cannot happen.
        // VisualLines always get constructed from top to bottom.
        // Each VisualLine construction calls into the styler and thus forces an update of the
        // styling state for all lines up to the one being constructed.

        // To guarantee that we don't redraw lines we just constructed, we need to show that when
        // a VisualLine is being reused, the styling state at that location is still up-to-date.

        // This isn't exactly trivial and the initial implementation was incorrect in the presence of external document changes
        // (e.g. split view).

        // For the first line in the view, the TextView.VisualLineConstructionStarting event is used to check that the
        // styling state is up-to-date. If it isn't, this method will be executed, and it'll mark the first line
        // in the view as requiring a redraw. This is safely possible because that event occurs before any lines are reused.

        // Once we take care of the first visual line, we won't get in trouble with other lines due to the top-to-bottom
        // construction process.

        // We'll prove that: if line N is being reused, then the styling state is up-to-date until (end of) line N-1.

        // Start of induction: the first line in view is reused only if the styling state was up-to-date
        // until line N-1 (no change detected in VisualLineConstructionStarting event).

        // Induction step:
        // If another line N+1 is being reused, then either
        //     a) the previous line (the visual line containing document line N) was newly constructed
        // or  b) the previous line was reused
        // In case a, the construction updated the styling state. This means the stack at end of line N is up-to-date.
        // In case b, the styling state at N-1 was up-to-date, and the text of line N was not changed.
        //   (if the text was changed, the line could not have been reused).
        // From this follows that the styling state at N is still up-to-date.

        // The above proof holds even in the presence of folding: folding only ever hides text in the middle of a visual line.
        // Our Colorize-override ensures that the styling state is always updated for the LastDocumentLine,
        // so it will always invalidate the next visual line when a folded line is constructed
        // and the styling stack has changed.

        if (frLineNumber == toLineNumber)
        {
            textView!.Redraw(textView.Document.GetLineByNumber(frLineNumber));
        }
        else
        {
            // If there are multiple lines marked as changed; only the first one really matters
            // for the styling during rendering.
            // However this callback is also called outside of the rendering process, e.g. when a styler
            // decides to re-style some section based on external feedback (e.g. semantic styling).
            var frLine = textView!.Document.GetLineByNumber(frLineNumber);
            var toLine = textView!.Document.GetLineByNumber(toLineNumber);
            var offset = frLine.Offset;
            
            textView.Redraw(offset, toLine.EndOffset - offset);
        }

        /*
         * Meta-comment: "why does this have to be so complicated?"
         * 
         * The problem is that I want to re-style only on-demand and incrementally;
         * and at the same time only repaint changed lines.
         * So the styler and the VisualLine construction both have to run in a single pass.
         * The styler must take care that it never touches already constructed visual lines;
         * if it detects that something must be redrawn because the styling state changed,
         * it must do so early enough in the construction process.
         * But doing it too early means it doesn't have the information necessary to re-style and redraw only the desired parts.
         */
    }
}
