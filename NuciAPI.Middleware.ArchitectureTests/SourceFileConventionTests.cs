using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

namespace NuciAPI.Middleware.ArchitectureTests
{
    [TestFixture]
    public sealed class SourceFileConventionTests
    {
        private static readonly string RepositoryRoot = FindRepositoryRoot();

        private static readonly string[] CheckedProjectRelativePaths =
        [
            "NuciAPI.Middleware",
            "NuciAPI.Middleware.ArchitectureTests",
            "NuciAPI.Middleware.UnitTests"
        ];

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingEligibleFiles_ThenAtLeastOneFileIsFound()
        {
            string[] eligibleFiles = CollectEligibleSourceFiles();

            Assert.That(
                eligibleFiles,
                Is.Not.Empty,
                "Violated convention: no handwritten source files were found for the checked projects.");
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingDeclaringFiles_ThenEachFileEndsWithAnEmptyLine()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                string text = File.ReadAllText(sourceFile);

                Assert.That(
                    text,
                    Does.EndWith(Environment.NewLine),
                    $"Violated convention in {sourceFile}: every file must end with an empty line.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingDeclaredTypes_ThenEachFileDeclaresExactlyOneType()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                TypeDeclarationSyntax[] typeDeclarations = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .ToArray();

                Assert.That(
                    typeDeclarations,
                    Has.Length.EqualTo(1),
                    $"Violated convention in {sourceFile}: every file must declare exactly one type, including nested declarations.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingTypeNames_ThenEachMatchesItsFileNameStemExactly()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                string fileNameStem = Path.GetFileNameWithoutExtension(sourceFile);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                string typeName = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Select(declaration => declaration.Identifier.ValueText)
                    .FirstOrDefault();

                if (typeName is null)
                {
                    continue;
                }

                string fileNameStemWithoutGenericArity = RemoveGenericArity(typeName);

                Assert.That(
                    fileNameStem,
                    Is.EqualTo(fileNameStemWithoutGenericArity),
                    $"Violated convention in {sourceFile}: the declared type must match the file name case-sensitively.");
            }
        }

        [Test]
        public void GivenHandwrittenProductionFiles_WhenCheckingNamespaces_ThenEachUsesBlockNamespaceFormat()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                NamespaceDeclarationSyntax[] fileScopedNamespaces = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .ToArray();

                Assert.That(
                    fileScopedNamespaces,
                    Is.Not.Empty,
                    $"Violated convention in {sourceFile}: every file must declare a block namespace.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingUsingPlacement_ThenAllUsingDirectivesAreOutsideTheNamespace()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

                UsingDirectiveSyntax[] usingInsideNamespace = root.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .SelectMany(@namespace => @namespace.Usings)
                    .ToArray();

                Assert.That(
                    usingInsideNamespace,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: all using directives must be placed above and outside the namespace.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingBlankLineBeforeNamespace_ThenThereIsExactlyOne()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                NamespaceDeclarationSyntax namespaceDeclaration = root.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                if (namespaceDeclaration is null)
                {
                    continue;
                }
                int namespaceStartLine = namespaceDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line;
                UsingDirectiveSyntax[] usingDirectives = root.Usings.ToArray();

                if (usingDirectives.Length == 0)
                {
                    continue;
                }

                int lastUsingEndLine = usingDirectives.Last().GetLocation().GetLineSpan().EndLinePosition.Line;
                int lineGap = namespaceStartLine - lastUsingEndLine - 1;

                Assert.That(
                    lineGap,
                    Is.EqualTo(1),
                    $"Violated convention in {sourceFile}: there must be exactly one blank line between the final using directive and the namespace.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingPreprocessorDirectives_ThenNoNullableEnableDirectiveIsUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                bool hasNullableEnableDirective = syntaxTree.GetRoot()
                    .DescendantTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.NullableDirectiveTrivia)
                        && trivia.ToString().Contains("enable", StringComparison.Ordinal));

                Assert.That(
                    hasNullableEnableDirective,
                    Is.False,
                    $"Violated convention in {sourceFile}: code must never use the #nullable enable compiler directive.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingPreprocessorDirectives_ThenNoRegionsAreDeclared()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                bool hasRegionDirective = syntaxTree.GetRoot()
                    .DescendantTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia)
                        || trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia));

                Assert.That(
                    hasRegionDirective,
                    Is.False,
                    $"Violated convention in {sourceFile}: regions are prohibited.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingIndentation_ThenNoTabCharactersAreUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                string text = File.ReadAllText(sourceFile);

                Assert.That(
                    text,
                    Does.Not.Contain('\t'),
                    $"Violated convention in {sourceFile}: indentation must use four spaces, never tabs.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingBlankLines_ThenNoTwoConsecutiveBlankLinesExist()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                string text = File.ReadAllText(sourceFile).Replace("\r", string.Empty);

                Assert.That(
                    text,
                    Does.Not.Contain("\n\n\n"),
                    $"Violated convention in {sourceFile}: two or more consecutive blank lines are prohibited.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingVarUsage_ThenNoImplicitlyTypedLocalsAreUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                VariableDeclarationSyntax[] implicitDeclarations = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<VariableDeclarationSyntax>()
                    .Where(declaration => declaration.Type.IsVar)
                    .ToArray();

                Assert.That(
                    implicitDeclarations,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: local variables must use explicit types, never var.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingTernaries_ThenNoConditionalExpressionsAreUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                ConditionalExpressionSyntax[] conditionalExpressions = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ConditionalExpressionSyntax>()
                    .ToArray();

                Assert.That(
                    conditionalExpressions,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: ternary conditional expressions are prohibited; use if/else instead.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingControlFlowBodies_ThenEveryBodyUsesBraces()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                StatementSyntax[] bracelessControlFlowBodies = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<StatementSyntax>()
                    .Where(statement => statement is IfStatementSyntax ifStatement && !(ifStatement.Statement is BlockSyntax))
                    .ToArray();

                Assert.That(
                    bracelessControlFlowBodies,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: every control-flow body must use braces.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingParameters_ThenNoMethodExceedsSevenParameters()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                ParameterListSyntax[] oversizedParameterLists = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ParameterListSyntax>()
                    .Where(parameterList => parameterList.Parameters.Count > 7)
                    .ToArray();

                Assert.That(
                    oversizedParameterLists,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: methods, constructors, and lambdas must have at most seven parameters.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingTuples_ThenNoTupleTypesAreUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                TupleTypeSyntax[] tupleTypes = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<TupleTypeSyntax>()
                    .ToArray();

                Assert.That(
                    tupleTypes,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: tuples are prohibited; return a dedicated type for multiple values.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingOptionalParameters_ThenNoDefaultParameterValuesAreDeclared()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                ParameterSyntax[] optionalParameters = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ParameterSyntax>()
                    .Where(parameter => parameter.Default is not null)
                    .ToArray();

                Assert.That(
                    optionalParameters,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: optional/default parameters are prohibited; use overloads instead.");
            }
        }

        [Test]
        public void GivenHandwrittenSourceFiles_WhenCheckingRefParameters_ThenNoRefParametersAreUsed()
        {
            foreach (string sourceFile in CollectEligibleSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                ParameterSyntax[] refParameters = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ParameterSyntax>()
                    .Where(parameter => parameter.Modifiers.Any(modifier => modifier.ValueText == "ref"))
                    .ToArray();

                Assert.That(
                    refParameters,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: ref parameters are prohibited.");
            }
        }

        [Test]
        public void GivenHandwrittenProductionFiles_WhenCheckingTypeAccessibility_ThenEveryMemberDeclaresExplicitAccessibility()
        {
            foreach (string sourceFile in CollectProductionSourceFiles())
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                MemberDeclarationSyntax[] membersWithoutAccessibility = syntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<MemberDeclarationSyntax>()
                    .Where(member => member is FieldDeclarationSyntax
                        || member is MethodDeclarationSyntax
                        || member is PropertyDeclarationSyntax)
                    .Where(member => !member.Modifiers.Any(SyntaxKind.PublicKeyword)
                        && !member.Modifiers.Any(SyntaxKind.ProtectedKeyword)
                        && !member.Modifiers.Any(SyntaxKind.PrivateKeyword)
                        && !member.Modifiers.Any(SyntaxKind.InternalKeyword))
                    .ToArray();

                Assert.That(
                    membersWithoutAccessibility,
                    Is.Empty,
                    $"Violated convention in {sourceFile}: every field, property, and method must declare explicit accessibility.");
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NuciAPI.Middleware.slnx")))
            {
                directory = directory.Parent;
            }

            if (directory is null)
            {
                Assert.Fail("Required solution file is unavailable: the repository root could not be located.");
            }

            return directory!.FullName;
        }

        private static string[] CollectEligibleSourceFiles()
        {
            List<string> eligibleFiles = [];

            foreach (string relativeProjectPath in CheckedProjectRelativePaths)
            {
                string projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);

                if (!Directory.Exists(projectPath))
                {
                    continue;
                }

                eligibleFiles.AddRange(Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !string.Equals(Path.GetFileName(path), "AssemblyInfo.cs", StringComparison.Ordinal)
                        && !Path.GetFileName(path).EndsWith(".AssemblyInfo.cs"))
                    .OrderBy(path => path, StringComparer.Ordinal));
            }

            return [.. eligibleFiles];
        }

        private static string[] CollectProductionSourceFiles()
            => CollectEligibleSourceFiles()
                .Where(path => path.Contains("NuciAPI.Middleware.UnitTests") is false
                    && path.Contains("NuciAPI.Middleware.ArchitectureTests") is false)
                .ToArray();

        private static string RemoveGenericArity(string typeName)
        {
            int aritySeparatorIndex = typeName.IndexOf('`');

            if (aritySeparatorIndex > 0)
            {
                return typeName.Substring(0, aritySeparatorIndex);
            }

            return typeName;
        }
    }
}
