using Microsoft.Win32;
using System.Windows;

using Stellar.WPF.Styling;
using System.Windows.Input;

namespace Stellar.WPF.Sample;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string fileName = string.Empty;
    public MainWindow()
    {
        //ISyntax pseudocode;

        //using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Stellar.WPF.Sample.Pseudocode.yaml"))
        //{
        //    if (stream is null)
        //    {
        //        throw new InvalidOperationException("Could not find embedded resource");
        //    }

        //    using (var reader = new System.IO.StreamReader(stream))
        //    {
        //        //pseudocode = Styling.IO.Loader.Load(reader.ReadToEnd());
        //        // customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.
        //        // HighlightingLoader.Load(reader, HighlightingManager.Instance);
        //    }
        //}

        //StylingManager.Instance.RegisterSyntax("Custom Syntax", new string[] { ".cool" }, pseudocode);


        InitializeComponent();

        //Editor.TextArea.TextEntering += Editor_TextArea_TextEntering;
        //Editor.TextArea.TextEntered += Editor_TextArea_TextEntered;
    }

    private void Editor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void Editor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        throw new NotImplementedException();
    }

    void OpenFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true
        };

        if (dialog.ShowDialog() ?? false)
        {
            fileName = dialog.FileName;

            Editor.Load(fileName);

            //textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(currentFileName));
        }
    }

    void SaveFileClick(object sender, EventArgs e)
    {
        if (fileName is null)
        {
            var dialog = new SaveFileDialog
            {
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() ?? false)
            {
                fileName = dialog.FileName;
            }
            else
            {
                return;
            }
        }

        //Editor.Save(fileName);
    }

    void PropertyGridComboBoxSelectionChanged(object sender, RoutedEventArgs e)
    {
        //if (propertyGrid == null)
        //{
        //    return;
        //}

        //switch (propertyGridComboBox.SelectedIndex)
        //{
        //    case 0:
        //        propertyGrid.SelectedObject = textEditor;
        //        break;
        //    case 1:
        //        propertyGrid.SelectedObject = textEditor.TextArea;
        //        break;
        //    case 2:
        //        propertyGrid.SelectedObject = textEditor.Options;
        //        break;
        //}
    }

    void StylingDropdown_SelectionChanged(object sender, RoutedEventArgs e)
    {

    }

}