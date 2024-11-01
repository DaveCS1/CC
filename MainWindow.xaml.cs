using System;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CodeAnalysis;

namespace CodeCleanUp
{
    public partial class MainWindow : Window
    {
        private Dictionary<int, int> _lineMapping = new Dictionary<int, int>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "VB.NET Files (*.vb)|*.vb|C# Files (*.cs)|*.cs|All Microsoft Programming Files|*.vb;*.cs;*.aspx;*.ascx;*.asax;*.ashx;*.asmx;*.axd;*.master;*.xaml;*.xamlx;*.xoml;*.svc;*.vbhtml;*.cshtml;*.vbhtm;*.shtm;*.rpt;*.rdlc;*.dxp;*.sitemap;*.skin;*.browser;*.webinfo;*.licx;*.resx;*.dbml;*.edmx";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                // Load the file into the AvalonEdit control using the file path
                avalonEditor.Load(filePath);
                
                // Set the appropriate syntax highlighting based on the file extension
                string fileExtension = System.IO.Path.GetExtension(filePath);
                if (fileExtension.Equals(".vb", System.StringComparison.OrdinalIgnoreCase))
                {
                    avalonEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
                }
                else if (fileExtension.Equals(".cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    avalonEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                }
                else
                {
                    avalonEditor.SyntaxHighlighting = null;
                }
            }
        }

        private void CleanCode_Click(object sender, RoutedEventArgs e)
        {
            _lineMapping.Clear(); // Reset line mapping
            string inputCode = avalonEditor.Text;
            StringBuilder allResults = new StringBuilder();
            
            // Header for results
            allResults.AppendLine("=== Code Analysis Results ===\n");
            
            // Original checks
            allResults.AppendLine("--- Method Summary Analysis ---");
            string methodResults = ProcessMethods(inputCode);
            allResults.AppendLine(methodResults);
            
            allResults.AppendLine("--- Ternary Condition Analysis ---");
            string ternaryResults = CheckTernaryConditions(inputCode);
            allResults.AppendLine(ternaryResults);
            
            allResults.AppendLine("--- Try-Catch Analysis ---");
            string tryCatchResults = CheckTryCatchBlocks(inputCode);
            allResults.AppendLine(tryCatchResults);
            
            // New best practices checks
            allResults.AppendLine("--- VB.NET Best Practices Analysis ---");
            string conventionResults = CheckCodingConventions(inputCode);
            allResults.AppendLine(conventionResults);

            // String interpolation check
            allResults.AppendLine("--- String Interpolation Opportunities ---");
            var tree = VisualBasicSyntaxTree.ParseText(inputCode);
            var root = tree.GetRoot();
            int outputLineNumber = _lineMapping.Count + 1;
            CheckStringInterpolation(root, allResults, ref outputLineNumber);
            allResults.AppendLine("\n--- Loop Efficiency Analysis ---");
            CheckLoopEfficiency(root, allResults, ref outputLineNumber);

            allResults.AppendLine("\n--- Naming and Property Conventions ---");
            CheckNamingAndPropertyConventions(root, allResults, ref outputLineNumber);

            allResults.AppendLine("\n--- VB.NET Specific Best Practices ---");
            CheckVBSpecificBestPractices(root, allResults, ref outputLineNumber);

            avalonEditorComments.Text = allResults.ToString();
       
        }

        private string ProcessMethods(string inputCode)
        {
            StringBuilder outputBuilder = new StringBuilder();

            var tree = VisualBasicSyntaxTree.ParseText(inputCode);
            var root = tree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodBlockSyntax>())
            {
                var result = ensureMethodSummary(method);
                if (result != null)
                {
                    outputBuilder.AppendLine(result);
                }
            }

            string ternaryResults = CheckTernaryConditions(inputCode);
            avalonEditorOutPutTernary.Text = ternaryResults;

            return outputBuilder.ToString();
        }

        private string CheckTernaryConditions(string inputCode)
        {
            StringBuilder outputBuilder = new StringBuilder();

            var tree = VisualBasicSyntaxTree.ParseText(inputCode);
            var root = tree.GetRoot();

            foreach (var ifStatement in root.DescendantNodes().OfType<SingleLineIfStatementSyntax>())
            {
                var result = AnalyzeTernaryCondition(ifStatement);
                if (result != null)
                {
                    outputBuilder.AppendLine(result);
                }
            }

            return outputBuilder.ToString();
        }

        private string AnalyzeTernaryCondition(SingleLineIfStatementSyntax ifStatement)
        {
            var ifStatementLine = ifStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
            var ifStatementText = ifStatement.ToString().Trim();

            // Check if the if statement can be converted to a ternary expression
            if (CanConvertToTernary(ifStatement))
            {
                return $"Line: {ifStatementLine}, If Statement: {ifStatementText}, Suggestion: Can be converted to a ternary expression";
            }

            return null;
        }

        private bool CanConvertToTernary(SingleLineIfStatementSyntax ifStatement)
        {
            // Check if the if statement has exactly one statement in both if and else blocks
            if (ifStatement.Statements.Count != 1 || ifStatement.ElseClause == null || 
                ifStatement.ElseClause.Statements.Count != 1)
                return false;

            // Check if both statements are simple assignments or return statements
            var thenStatement = ifStatement.Statements.First();
            var elseStatement = ifStatement.ElseClause.Statements.First();

            bool isValidStatement(StatementSyntax stmt) =>
                stmt is AssignmentStatementSyntax ||
                stmt is ReturnStatementSyntax;

            return isValidStatement(thenStatement) && isValidStatement(elseStatement);
        }

        public static string ensureMethodSummary(MethodBlockSyntax method)
        {
            var methodStartLine = method.GetLocation().GetLineSpan().StartLinePosition.Line;
            var methodName = method.SubOrFunctionStatement.Identifier.ValueText;
            var summary = method.GetLeadingTrivia().ToFullString().Trim();

            if (string.IsNullOrEmpty(summary) || summary.Length < 10)
            {
                return $"Line: {methodStartLine}, Method: {methodName}, Summary: ****** NEEDS SUMMARY ******";
            }

            return null;
        }

        public static string ensureMethodSummary1(MethodBlockSyntax method)
        {
            var methodStartLine = method.GetLocation().GetLineSpan().StartLinePosition.Line;
            var methodName = method.SubOrFunctionStatement.Identifier.ValueText;
            var summary = method.GetLeadingTrivia().ToFullString().Trim();

            if (string.IsNullOrEmpty(summary) || summary.Length < 10)
            {
                return $"Line: {methodStartLine}, Method: {methodName}, Summary: ****** NEEDS SUMMARY ******";
            }

            return null;
        }

        private string CheckTryCatchBlocks(string inputCode)
        {
            StringBuilder outputBuilder = new StringBuilder();
            var tree = VisualBasicSyntaxTree.ParseText(inputCode);
            var root = tree.GetRoot();
            int outputLineNumber = _lineMapping.Count + 1;

            foreach (var method in root.DescendantNodes().OfType<MethodBlockSyntax>())
            {
                if (!method.DescendantNodes().OfType<TryBlockSyntax>().Any())
                {
                    var methodStartLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var methodName = method.SubOrFunctionStatement.Identifier.ValueText;
                    outputBuilder.AppendLine($"Line: {methodStartLine}, Method: {methodName}, Warning: Method lacks try-catch block");
                    _lineMapping[outputLineNumber++] = methodStartLine;
                }
            }

            foreach (var catchBlock in root.DescendantNodes().OfType<CatchBlockSyntax>())
            {
                var catchStartLine = catchBlock.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                
                if (!catchBlock.Statements.Any())
                {
                    outputBuilder.AppendLine($"Line: {catchStartLine}, Warning: Empty catch block detected");
                    _lineMapping[outputLineNumber++] = catchStartLine;
                }
                
                if (catchBlock.CatchStatement.IdentifierName?.Identifier.ValueText == "Exception")
                {
                    bool hasLogging = catchBlock.DescendantNodes()
                        .Any(node => node.ToString().Contains("Log") || 
                                    node.ToString().Contains("Console.Write") ||
                                    node.ToString().Contains("Debug.Write"));

                    if (!hasLogging)
                    {
                        outputBuilder.AppendLine($"Line: {catchStartLine}, Warning: Catch block with generic Exception lacks proper error logging");
                        _lineMapping[outputLineNumber++] = catchStartLine;
                    }
                }
            }

            return outputBuilder.ToString();
        }

        private void AvalonEditorComments_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Get the clicked line's text
            var clickedLine = textBox.GetLineText(textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex));
            
            // Extract line number from the text (format is "Line: X, ...")
            if (clickedLine.StartsWith("Line: "))
            {
                var lineText = clickedLine.Substring(6); // Skip "Line: "
                var comma = lineText.IndexOf(',');
                if (comma > 0 && int.TryParse(lineText.Substring(0, comma), out int lineNumber))
                {
                    // Scroll to the line in the main editor
                    avalonEditor.ScrollToLine(lineNumber - 1);  // AvalonEdit is 0-based
                    
                    // Select the line
                    var line = avalonEditor.Document.GetLineByNumber(lineNumber);
                    avalonEditor.Select(line.Offset, line.Length);
                }
            }
        }

        private string CheckCodingConventions(string inputCode)
        {
            StringBuilder outputBuilder = new StringBuilder();
            var tree = VisualBasicSyntaxTree.ParseText(inputCode);
            var root = tree.GetRoot();
            int outputLineNumber = _lineMapping.Count + 1;

            // Header for results
            outputBuilder.AppendLine("=== Best Practices Analysis ===\n");

            // Check each category and append results
            outputBuilder.AppendLine("--- Option Settings ---");
            CheckOptionSettings(root, outputBuilder, ref outputLineNumber);

            outputBuilder.AppendLine("\n--- Late Binding Analysis ---");
            CheckLateBinding(root, outputBuilder, ref outputLineNumber);

            outputBuilder.AppendLine("\n--- Naming Conventions ---");
            CheckNamingConventions(root, outputBuilder, ref outputLineNumber);

            outputBuilder.AppendLine("\n--- Method Analysis ---");
            CheckMethodAnalysis(root, outputBuilder, ref outputLineNumber);

            outputBuilder.AppendLine("\n--- LINQ Opportunities ---");
            CheckLinqOpportunities(root, outputBuilder, ref outputLineNumber);

            outputBuilder.AppendLine("\n--- Try-Catch Analysis ---");
            CheckTryCatchBlocks(root, outputBuilder, ref outputLineNumber);

            // Naming and Property Conventions
            outputBuilder.AppendLine("\n--- Naming and Property Conventions ---");
            CheckNamingAndPropertyConventions(root, outputBuilder, ref outputLineNumber);

            return outputBuilder.ToString();
        }

        private void CheckOptionSettings(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            if (!root.GetText().ToString().Contains("Option Strict On"))
            {
                outputBuilder.AppendLine("Line: 1, Warning: 'Option Strict On' is not set. This allows late binding which can cause runtime errors and performance issues");
                _lineMapping[outputLineNumber++] = 1;
            }

            if (!root.GetText().ToString().Contains("Option Explicit On"))
            {
                outputBuilder.AppendLine("Line: 1, Warning: 'Option Explicit On' is not set. Variables should be explicitly declared");
                _lineMapping[outputLineNumber++] = 1;
            }
        }

        private void CheckLateBinding(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            // Check variables declared as Object
            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                string variableType = "";
                if (variable.AsClause != null)
                {
                    variableType = variable.AsClause.ToString().Replace("As ", "");
                }
                
                var variableName = variable.Names.First().Identifier.ValueText;
                var line = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (variableType == "Object" || variableType == "System.Object")
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Variable '{variableName}' is late-bound (type Object). Consider using early binding for better performance and compile-time checking");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check CreateObject usage
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression.ToString().Contains("CreateObject"))
                {
                    var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: CreateObject call detected. Consider using early binding with 'New' keyword instead");
                    _lineMapping[outputLineNumber++] = line;
                }
            }
        }

        private void CheckNamingConventions(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                var variableName = variable.Names.First().Identifier.ValueText;
                var line = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (variableName.StartsWith("str") || variableName.StartsWith("int") || variableName.StartsWith("bln"))
                {
                    outputBuilder.AppendLine($"Line: {line}, Suggestion: Variable '{variableName}' appears to use Hungarian notation. Consider using descriptive names instead");
                    _lineMapping[outputLineNumber++] = line;
                }

                if (variableName.StartsWith("My", StringComparison.OrdinalIgnoreCase))
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Variable '{variableName}' should not use 'My' prefix as it conflicts with VB.NET My feature");
                    _lineMapping[outputLineNumber++] = line;
                }
            }
        }

        private void CheckMethodAnalysis(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var method in root.DescendantNodes().OfType<MethodBlockSyntax>())
            {
                var methodLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var methodName = method.SubOrFunctionStatement.Identifier.ValueText;

                var xmlDoc = method.GetLeadingTrivia()
                    .Any(t => t.IsKind(SyntaxKind.DocumentationCommentTrivia));
                if (!xmlDoc)
                {
                    outputBuilder.AppendLine($"Line: {methodLine}, Suggestion: Method '{methodName}' should have XML documentation comments");
                    _lineMapping[outputLineNumber++] = methodLine;
                }

                var leadingTrivia = method.GetLeadingTrivia().ToString();
                if (!leadingTrivia.Contains("'") && !leadingTrivia.Contains("'''"))
                {
                    outputBuilder.AppendLine($"Line: {methodLine}, Suggestion: Method '{methodName}' should have comments explaining its purpose");
                    _lineMapping[outputLineNumber++] = methodLine;
                }
            }
        }

        private void CheckLinqOpportunities(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var loop in root.DescendantNodes().OfType<ForBlockSyntax>())
            {
                var loopLine = loop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var loopBody = loop.Statements.ToString();

                if (loopBody.Contains("Select") || loopBody.Contains("Where"))
                {
                    continue; // Already using LINQ
                }

                if (loopBody.Contains("If") && loopBody.Contains("Then"))
                {
                    outputBuilder.AppendLine($"Line: {loopLine}, Suggestion: Consider using LINQ Where() instead of For loop with If statement");
                    _lineMapping[outputLineNumber++] = loopLine;
                }

                if (loopBody.Contains("New") || loopBody.Contains("Add"))
                {
                    outputBuilder.AppendLine($"Line: {loopLine}, Suggestion: Consider using LINQ Select() or other LINQ methods instead of For loop");
                    _lineMapping[outputLineNumber++] = loopLine;
                }
            }
        }

        private void CheckTryCatchBlocks(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var catchBlock in root.DescendantNodes().OfType<CatchBlockSyntax>())
            {
                var catchStartLine = catchBlock.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                
                if (!catchBlock.Statements.Any())
                {
                    outputBuilder.AppendLine($"Line: {catchStartLine}, Warning: Empty catch block detected");
                    _lineMapping[outputLineNumber++] = catchStartLine;
                }
                
                if (catchBlock.CatchStatement.IdentifierName?.Identifier.ValueText == "Exception")
                {
                    bool hasLogging = catchBlock.DescendantNodes()
                        .Any(node => node.ToString().Contains("Log") || 
                                    node.ToString().Contains("Console.Write") ||
                                    node.ToString().Contains("Debug.Write"));

                    if (!hasLogging)
                    {
                        outputBuilder.AppendLine($"Line: {catchStartLine}, Warning: Catch block with generic Exception lacks proper error logging");
                        _lineMapping[outputLineNumber++] = catchStartLine;
                    }
                }
            }
        }

        private void CheckStringInterpolation(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var concatenation in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                // Check for string concatenation operators
                if (concatenation.OperatorToken.IsKind(SyntaxKind.AmpersandToken) || 
                    concatenation.OperatorToken.IsKind(SyntaxKind.PlusToken))
                {
                    var line = concatenation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var expression = concatenation.ToString();

                    // Check if this looks like string concatenation with variables
                    if (expression.Contains("&") || expression.Contains("+"))
                    {
                        outputBuilder.AppendLine($"Line: {line}, Suggestion: Consider using string interpolation instead of concatenation. Example:");
                        outputBuilder.AppendLine($"    Original: {expression}");
                        
                        // Create interpolated string suggestion
                        var suggestion = expression
                            .Replace(" & ", "")
                            .Replace(" + ", "");
                        outputBuilder.AppendLine($"    Suggested: $\"{suggestion}\"");
                        
                        _lineMapping[outputLineNumber++] = line;
                    }
                }
            }
        }

        private void CheckSelectCaseStatements(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            foreach (var selectCase in root.DescendantNodes().OfType<SelectBlockSyntax>())
            {
                var caseCount = selectCase.CaseBlocks.Count;
                if (caseCount < 5)
                {
                    var line = selectCase.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var expression = selectCase.SelectStatement.Expression.ToString();
                    
                    outputBuilder.AppendLine($"Line: {line}, Suggestion: Select Case for '{expression}' has only {caseCount} conditions. Consider using If/ElseIf instead for better readability");
                    outputBuilder.AppendLine($"    Hint: Select Case is more efficient with 5 or more conditions");
                    _lineMapping[outputLineNumber++] = line;
                }
            }
        }

        private void CheckLoopEfficiency(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            // Check for nested loops
            foreach (var outerLoop in root.DescendantNodes().OfType<ForBlockSyntax>())
            {
                var nestedLoops = outerLoop.DescendantNodes().OfType<ForBlockSyntax>().ToList();
                if (nestedLoops.Any())
                {
                    var line = outerLoop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: Nested loop detected. Consider refactoring to improve performance");
                    outputBuilder.AppendLine($"    Suggestion: Could this be simplified using LINQ or a different data structure?");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for nested ForEach loops
            foreach (var outerLoop in root.DescendantNodes().OfType<ForEachBlockSyntax>())
            {
                var nestedLoops = outerLoop.DescendantNodes().OfType<ForEachBlockSyntax>().ToList();
                if (nestedLoops.Any())
                {
                    var line = outerLoop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: Nested ForEach loop detected. Consider using a join or lookup");
                    outputBuilder.AppendLine($"    Suggestion: Consider using LINQ Join() or GroupBy() instead");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for array access in loops
            foreach (var loop in root.DescendantNodes().OfType<ForBlockSyntax>())
            {
                var arrayAccesses = loop.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(i => i.ToString().Contains("GetLength") || i.ToString().Contains("Length"));
                
                if (arrayAccesses.Any())
                {
                    var line = loop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Suggestion: Array length/bounds check inside loop. Consider moving outside loop");
                    outputBuilder.AppendLine($"    Performance: Cache the length before the loop starts");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for potential collection modifications inside loops
            foreach (var loop in root.DescendantNodes().OfType<ForEachBlockSyntax>())
            {
                var modifications = loop.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(i => i.ToString().Contains("Add") || i.ToString().Contains("Remove"));
                
                if (modifications.Any())
                {
                    var line = loop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: Collection modification inside loop detected");
                    outputBuilder.AppendLine($"    Suggestion: Consider using a temporary collection or different approach");
                    _lineMapping[outputLineNumber++] = line;
                }
            }
        }

        private void CheckNamingAndPropertyConventions(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            // Check local variables for naming conventions
            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                var variableName = variable.Names.First().Identifier.ValueText;
                var line = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                // Skip module-level or class-level variables (fields)
                var isLocalVariable = variable.Parent.Parent is MethodBlockSyntax;
                if (!isLocalVariable) continue;

                // Check if local variable starts with uppercase
                if (char.IsUpper(variableName[0]))
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Local variable '{variableName}' should start with lowercase letter");
                    _lineMapping[outputLineNumber++] = line;
                }

                // Check for Hungarian notation
                if (variableName.StartsWith("str") || variableName.StartsWith("int") || 
                    variableName.StartsWith("bln") || variableName.StartsWith("obj"))
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Variable '{variableName}' uses Hungarian notation. Use descriptive names instead");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check properties that could be auto-implemented
            foreach (var property in root.DescendantNodes().OfType<PropertyBlockSyntax>())
            {
                var propertyName = property.PropertyStatement.Identifier.ValueText;
                var line = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                // Check if property has both getter and setter
                var getter = property.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorBlock));
                var setter = property.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorBlock));

                if (getter != null && setter != null)
                {
                    // Look for simple get/set patterns that just return/set a backing field
                    var getterStatements = getter.DescendantNodes().OfType<ReturnStatementSyntax>();
                    var setterStatements = setter.DescendantNodes().OfType<AssignmentStatementSyntax>();

                    // Check if the property is a candidate for auto-implementation
                    if (getterStatements.Count() == 1 && setterStatements.Count() == 1 &&
                        !getter.DescendantNodes().Any(n => n is IfStatementSyntax) &&
                        !setter.DescendantNodes().Any(n => n is IfStatementSyntax))
                    {
                        outputBuilder.AppendLine($"Line: {line}, Suggestion: Property '{propertyName}' could be converted to an auto-implemented property");
                        outputBuilder.AppendLine($"    Example: Public Property {propertyName} As {property.PropertyStatement.AsClause?.ToString() ?? "Type"} {{ Get; Set; }}");
                        _lineMapping[outputLineNumber++] = line;
                    }
                }
            }
        }

        private void CheckVBSpecificBestPractices(SyntaxNode root, StringBuilder outputBuilder, ref int outputLineNumber)
        {
            // Check for And/Or instead of AndAlso/OrElse
            foreach (var binaryExpr in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                var line = binaryExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                
                // Check logical operators
                if (binaryExpr.OperatorToken.IsKind(SyntaxKind.AndKeyword))
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Use 'AndAlso' instead of 'And' for conditional logic to ensure short-circuit evaluation");
                    _lineMapping[outputLineNumber++] = line;
                }
                else if (binaryExpr.OperatorToken.IsKind(SyntaxKind.OrKeyword))
                {
                    outputBuilder.AppendLine($"Line: {line}, Warning: Use 'OrElse' instead of 'Or' for conditional logic to ensure short-circuit evaluation");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for DirectCast vs CType
            foreach (var conversion in root.DescendantNodes().OfType<CTypeExpressionSyntax>())
            {
                var line = conversion.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                outputBuilder.AppendLine($"Line: {line}, Suggestion: Consider using 'DirectCast' instead of 'CType' when the type is known for better performance");
                _lineMapping[outputLineNumber++] = line;
            }

            // Check for IsNot usage
            foreach (var comparison in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (comparison.OperatorToken.IsKind(SyntaxKind.IsKeyword))
                {
                    var line = comparison.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Suggestion: Consider using 'IsNot' instead of 'Is Not' for more concise code");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for With blocks opportunities
            foreach (var method in root.DescendantNodes().OfType<MethodBlockSyntax>())
            {
                var memberAccesses = method.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Where(m => m.Expression != null)
                    .GroupBy(m => m.Expression.ToString())
                    .Where(g => g.Key != null);

                foreach (var group in memberAccesses)
                {
                    if (group.Count() >= 3) // If same object accessed 3 or more times
                    {
                        var line = group.First().GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        outputBuilder.AppendLine($"Line: {line}, Suggestion: Consider using 'With' block for multiple accesses to '{group.Key}'");
                        _lineMapping[outputLineNumber++] = line;
                    }
                }
            }

            // Check for IIF vs If operator
            foreach (var iif in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (iif.Expression.ToString().Equals("IIf", StringComparison.OrdinalIgnoreCase))
                {
                    var line = iif.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: Use 'If' operator instead of 'IIf' for short-circuit evaluation");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for proper Enum usage
            foreach (var enumDecl in root.DescendantNodes().OfType<EnumBlockSyntax>())
            {
                var hasFlags = enumDecl.EnumStatement.AttributeLists
                    .Any(a => a.ToString().Contains("Flags"));
                
                // Check for enum members using bitwise operations
                var hasBitwiseOperations = enumDecl.Members
                    .Any(m => m.ToString().Contains(" Or "));

                if (!hasFlags && hasBitwiseOperations)
                {
                    var line = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Warning: Enum uses bitwise 'Or' but lacks <Flags> attribute");
                    _lineMapping[outputLineNumber++] = line;
                }
            }

            // Check for proper String.Empty usage
            foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (literal.Token.ValueText == "")
                {
                    var line = literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    outputBuilder.AppendLine($"Line: {line}, Suggestion: Use String.Empty instead of \"\" for better readability");
                    _lineMapping[outputLineNumber++] = line;
                }
            }
        }
    }
}