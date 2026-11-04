using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using NUnit.Framework;

namespace NuciAPI.Middleware.ArchitectureTests
{
    [TestFixture]
    public sealed class ProjectConventionTests
    {
        private static readonly string RepositoryRoot = FindRepositoryRoot();

        private static readonly string ProductionProjectPath = Path.Combine(
            RepositoryRoot, "NuciAPI.Middleware", "NuciAPI.Middleware.csproj");

        private static readonly string UnitTestProjectPath = Path.Combine(
            RepositoryRoot, "NuciAPI.Middleware.UnitTests", "NuciAPI.Middleware.UnitTests.csproj");

        private static readonly string ArchitectureTestProjectPath = Path.Combine(
            RepositoryRoot, "NuciAPI.Middleware.ArchitectureTests", "NuciAPI.Middleware.ArchitectureTests.csproj");

        private static readonly string SolutionPath = Path.Combine(RepositoryRoot, "NuciAPI.Middleware.slnx");

        [Test]
        public void GivenProductionProject_WhenCheckingTargetFramework_ThenItTargetsNet10OrNewer()
        {
            XElement propertyGroup = LoadProjectPropertyGroup(ProductionProjectPath);

            Assert.That(
                propertyGroup.Element("TargetFramework")?.Value,
                Does.StartWith("net10"),
                "Violated convention: every project must target .NET 10.0 or newer.");
        }

        [Test]
        public void GivenProductionProject_WhenCheckingImplicitUsings_ThenTheyAreDisabled()
        {
            XElement propertyGroup = LoadProjectPropertyGroup(ProductionProjectPath);

            Assert.That(
                propertyGroup.Element("ImplicitUsings")?.Value,
                Is.EqualTo("disable"),
                "Violated convention: every project must disable implicit usings.");
        }

        [Test]
        public void GivenProductionProject_WhenCheckingLanguageVersion_ThenItIsLatest()
        {
            XElement propertyGroup = LoadProjectPropertyGroup(ProductionProjectPath);

            Assert.That(
                propertyGroup.Element("LangVersion")?.Value,
                Is.EqualTo("latest"),
                "Violated convention: every project must declare the latest language version.");
        }

        [Test]
        public void GivenProductionProject_WhenCheckingPackableFlag_ThenItMatchesPublishingIntent()
        {
            XElement propertyGroup = LoadProjectPropertyGroup(ProductionProjectPath);

            Assert.That(
                propertyGroup.Element("IsPackable")?.Value,
                Is.EqualTo("true"),
                "Violated convention: the production project is published as a NuGet package, so it must be packable.");
        }

        [Test]
        public void GivenProductionProject_WhenCheckingNullableReferences_ThenTheyAreNotDisabled()
        {
            XElement propertyGroup = LoadProjectPropertyGroup(ProductionProjectPath);

            Assert.That(
                propertyGroup.Element("Nullable")?.Value,
                Is.Not.EqualTo("disable"),
                "Violated convention: nullable reference types must be enabled.");
        }

        [Test]
        public void GivenAnyProject_WhenCheckingPackageReferences_ThenTheyAreAlphabeticallyOrdered()
        {
            string[] projectPaths =
            [
                ProductionProjectPath,
                UnitTestProjectPath,
                ArchitectureTestProjectPath
            ];

            foreach (string projectPath in projectPaths)
            {
                string[] packageNames = XDocument.Load(projectPath)
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .OfType<string>()
                    .ToArray();

                Assert.That(
                    packageNames,
                    Is.Ordered,
                    $"Violated convention: package references in {Path.GetFileName(projectPath)} must be alphabetically ordered.");
            }
        }

        [Test]
        public void GivenAnyTestProject_WhenCheckingTestFlags_ThenItDeclaresIsTestProjectAndExcludeFromCodeCoverage()
        {
            string[] testProjectPaths = [UnitTestProjectPath, ArchitectureTestProjectPath];

            foreach (string testProjectPath in testProjectPaths)
            {
                XElement propertyGroup = LoadProjectPropertyGroup(testProjectPath);

                Assert.That(
                    propertyGroup.Element("IsTestProject")?.Value,
                    Is.EqualTo("true"),
                    $"Violated convention: {Path.GetFileName(testProjectPath)} must declare IsTestProject.");
                Assert.That(
                    propertyGroup.Element("ExcludeFromCodeCoverage")?.Value,
                    Is.EqualTo("true"),
                    $"Violated convention: {Path.GetFileName(testProjectPath)} must declare ExcludeFromCodeCoverage.");
            }
        }

        [Test]
        public void GivenAnyTestProject_WhenCheckingPackableFlag_ThenItIsNotPackable()
        {
            string[] testProjectPaths = [UnitTestProjectPath, ArchitectureTestProjectPath];

            foreach (string testProjectPath in testProjectPaths)
            {
                XElement propertyGroup = LoadProjectPropertyGroup(testProjectPath);

                Assert.That(
                    propertyGroup.Element("IsPackable")?.Value,
                    Is.EqualTo("false"),
                    $"Violated convention: {Path.GetFileName(testProjectPath)} must not be packable.");
            }
        }

        [Test]
        public void GivenAnyTestProject_WhenCheckingProjectDependencies_ThenNoTestProjectIsReferencedByProductionCode()
        {
            string productionProjectDocument = File.ReadAllText(ProductionProjectPath);

            Assert.That(
                productionProjectDocument,
                Does.Not.Contain("NuciAPI.Middleware.UnitTests.csproj").And.Not.Contains("NuciAPI.Middleware.ArchitectureTests.csproj"),
                "Violated boundary: production code must not reference test projects.");
        }

        [Test]
        public void GivenUnitTestProject_WhenCheckingProjectReference_ThenItReferencesTheMainProject()
        {
            string[] references = LoadProjectReferences(UnitTestProjectPath);

            Assert.That(
                references,
                    Does.Contain("..\\NuciAPI.Middleware\\NuciAPI.Middleware.csproj"),
                "Violated convention: the unit test project must reference the main project.");
        }

        [Test]
        public void GivenSolutionFile_WhenCheckingStructure_ThenItIsSdkStyleWithoutXmlDeclarationOrAttributes()
        {
            string solutionText = File.ReadAllText(SolutionPath);

            Assert.That(
                solutionText,
                Does.Not.StartWith("<?xml"),
                "Violated convention: the .slnx file must not contain an XML declaration.");
            Assert.That(
                solutionText,
                Does.StartWith("<Solution>"),
                "Violated convention: the .slnx file must start with the <Solution> element.");
            Assert.That(
                solutionText,
                Does.Not.Contain("<Version>"),
                "Violated convention: the .slnx file must not contain a Version element.");
        }

        [Test]
        public void GivenSolutionFile_WhenCheckingProjectEntries_ThenEachEntryHasOnlyThePathAttribute()
        {
            XElement[] projectElements = XDocument.Load(SolutionPath)
                .Descendants("Project")
                .ToArray();

            Assert.That(
                projectElements,
                Is.Not.Empty,
                "Violated convention: the solution file must contain at least one project entry.");

            foreach (XElement projectElement in projectElements)
            {
                Assert.That(
                    projectElement.Attributes().Select(attribute => attribute.Name.LocalName),
                    Is.EqualTo(new[] { "Path" }),
                    $"Violated convention: project entry {projectElement.Attribute("Path")?.Value} must declare only the Path attribute.");
            }
        }

        [Test]
        public void GivenSolutionFile_WhenCheckingProjectOrder_ThenTheMainProjectIsFirstAndTestProjectsFollowAlphabetically()
        {
            string[] projectPaths = XDocument.Load(SolutionPath)
                .Descendants("Project")
                .Select(element => element.Attribute("Path")?.Value)
                .OfType<string>()
                .ToArray();

            Assert.That(
                projectPaths.First(),
                Does.Contain("NuciAPI.Middleware/NuciAPI.Middleware.csproj"),
                "Violated convention: the main project must be the first entry in the solution file.");
            Assert.That(
                projectPaths.Skip(1),
                Is.Ordered,
                "Violated convention: non-main projects must be alphabetically ordered in the solution file.");
        }

        [Test]
        public void GivenSolutionFile_WhenCheckingSolutionName_ThenItMatchesTheMainProjectName()
        {
            Assert.That(
                Path.GetFileName(SolutionPath),
                Is.EqualTo("NuciAPI.Middleware.slnx"),
                "Violated convention: the solution file must be named after the main project.");
        }

        [Test]
        public void GivenAnyTestProject_WhenCheckingSolutionInclusion_ThenEveryTestProjectIsIncluded()
        {
            string solutionText = File.ReadAllText(SolutionPath);

            Assert.That(
                solutionText,
                Does.Contain("NuciAPI.Middleware.ArchitectureTests.csproj"),
                "Violated convention: every test project must be included in the solution file.");
            Assert.That(
                solutionText,
                Does.Contain("NuciAPI.Middleware.UnitTests.csproj"),
                "Violated convention: every test project must be included in the solution file.");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(Assembly.GetExecutingAssembly().Location);

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

        private static XElement LoadProjectPropertyGroup(string projectPath)
        {
            if (!File.Exists(projectPath))
            {
                Assert.Fail($"Required project file is unavailable: {projectPath}");
            }

            return XDocument.Load(projectPath).Descendants("PropertyGroup").First();
        }

        private static string[] LoadProjectReferences(string projectPath)
        {
            if (!File.Exists(projectPath))
            {
                Assert.Fail($"Required project file is unavailable: {projectPath}");
            }

            return XDocument.Load(projectPath)
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>()
                .ToArray();
        }
    }
}
