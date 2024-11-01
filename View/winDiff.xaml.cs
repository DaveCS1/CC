using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // This might be needed for VisualTreeHelper
using ICSharpCode.AvalonEdit.Rendering;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Folding;

namespace CodeCleanUp.View
{
    public partial class winDiff : Window
    {
        // Define properties for the file paths
        public string LeftViewerFilePath { get; set; } = "Subb.vb";
        public string RightViewerFilePath { get; set; } = "output.vb";

        private HighlightingBackgroundRenderer leftHighlighter;
        private HighlightingBackgroundRenderer rightHighlighter;

        public winDiff()
        {
            InitializeComponent();
            LoadFileIntoLeftViewer();
            LoadFileIntoRightViewer();

            // Initialize your custom renderer and add it to the TextEditors
            leftHighlighter = new HighlightingBackgroundRenderer(LeftDiffViewer);
            LeftDiffViewer.TextArea.TextView.BackgroundRenderers.Add(leftHighlighter);

            rightHighlighter = new HighlightingBackgroundRenderer(RightDiffViewer);
            RightDiffViewer.TextArea.TextView.BackgroundRenderers.Add(rightHighlighter);

            this.Loaded += (s, e) => 
            {
                SynchronizeBothDiffViewers(); // Ensure components are loaded
                ApplySyntaxHighlighting();
              //  HighlightDifferences(); // Highlight differences after files are loaded
          //  FoldAllMethods(); fold methods not working
            };
           
        }

        private void LoadFileIntoLeftViewer()
        {
            // Use the LeftViewerFilePath property
            if (File.Exists(LeftViewerFilePath))
            {
                string fileContent = File.ReadAllText(LeftViewerFilePath);
                LeftDiffViewer.Text = fileContent;
            }
            else
            {
                MessageBox.Show($"The file '{LeftViewerFilePath}' does not exist.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFileIntoRightViewer()
        {
            // Use the RightViewerFilePath property
            if (File.Exists(RightViewerFilePath))
            {
                try
                {
                    using (FileStream stream = File.Open(RightViewerFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        stream.Close();
                    }

                    string fileContent = File.ReadAllText(RightViewerFilePath);
                    RightDiffViewer.Text = fileContent;
                }
                catch (IOException)
                {
                    MessageBox.Show("The file is currently locked or in use. Please try again later.", "File Access Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show($"The file '{RightViewerFilePath}' does not exist.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDiff_Click(object sender, RoutedEventArgs e)
        {

        }
        
        private ScrollViewer GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer)
            {
                return o as ScrollViewer;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void SynchronizeBothDiffViewers() 
        {
            var leftScrollViewer = GetScrollViewer(LeftDiffViewer);
            var rightScrollViewer = GetScrollViewer(RightDiffViewer);
            if (leftScrollViewer != null && rightScrollViewer != null)
            {
                leftScrollViewer.ScrollChanged += (s, e) => 
                {
                    if (e.VerticalChange != 0)
                    {
                        rightScrollViewer.ScrollToVerticalOffset(leftScrollViewer.VerticalOffset);
                    }
                };

                rightScrollViewer.ScrollChanged += (s, e) => 
                {
                    if (e.VerticalChange != 0)
                    {
                        leftScrollViewer.ScrollToVerticalOffset(rightScrollViewer.VerticalOffset);
                    }
                };
            }
        }
        //can we add syntax highlighting for vb.net
    private void ApplySyntaxHighlighting()
    {
        LeftDiffViewer.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
        RightDiffViewer.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
    }
   

    private void DocumentChangedOrLoaded(object sender, EventArgs e)
    {
       // LoadSyntaxHighlightingForVB();
    }

    private void HighlightDifferences()
    {
        var leftLines = LeftDiffViewer.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var rightLines = RightDiffViewer.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int minLineCount = Math.Min(leftLines.Length, rightLines.Length);

        for (int i = 0; i < minLineCount; i++)
        {
            if (leftLines[i] != rightLines[i])
            {
                HighlightLine(LeftDiffViewer, i, Brushes.Yellow);
                HighlightLine(RightDiffViewer, i, Brushes.Yellow);
            }
        }
    }

    private void HighlightLine(ICSharpCode.AvalonEdit.TextEditor editor, int lineNumber, SolidColorBrush color)
    {
        // Assuming you have initialized this renderer somewhere like in the constructor of your window or form
        // For example: var myHighlighter = new HighlightingBackgroundRenderer(editor);
        // And added it to the editor: editor.TextArea.TextView.BackgroundRenderers.Add(myHighlighter);

        // Now, instead of setting BackgroundBrush, you add the line to be highlighted
        if (editor == LeftDiffViewer)
        {
            leftHighlighter.AddHighlightedLine(lineNumber + 1); // Adjusting line number for zero-based index
        }
        else if (editor == RightDiffViewer)
        {
            rightHighlighter.AddHighlightedLine(lineNumber + 1);
        }
        editor.TextArea.TextView.InvalidateVisual(); // Force the editor to redraw and apply the highlights
    }

    public class HighlightingBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Selection;

        private ICSharpCode.AvalonEdit.TextEditor _editor;
        private HashSet<int> _highlightedLines = new HashSet<int>();
        private SolidColorBrush _highlightBrush = new SolidColorBrush(Colors.Yellow);

        public HighlightingBackgroundRenderer(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            _editor = editor;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null)
                return;

            textView.EnsureVisualLines();
            foreach (var lineNumber in _highlightedLines)
            {
                var line = _editor.Document.GetLineByNumber(lineNumber);
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
                {
                    drawingContext.DrawRectangle(_highlightBrush, null, new Rect(rect.Location, new Size(textView.ActualWidth, rect.Height)));
                }
            }
        }

        public void AddHighlightedLine(int lineNumber)
        {
            _highlightedLines.Add(lineNumber);
        }

        public void ClearHighlights()
        {
            _highlightedLines.Clear();
        }
    }
    private void FoldAllMethods()
    {
        FoldMethods(LeftDiffViewer);
        FoldMethods(RightDiffViewer);
    }

    private void FoldMethods(ICSharpCode.AvalonEdit.TextEditor editor)
    {
        if (editor.Document == null)
            return;

        var foldingManager = FoldingManager.Install(editor.TextArea);
        var foldingStrategy = new XmlFoldingStrategy();

        foldingStrategy.UpdateFoldings(foldingManager, editor.Document);
    }
        
    }
}

