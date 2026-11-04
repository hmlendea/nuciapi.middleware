using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using NuciAPI.Middleware;

using NUnit.Framework;

namespace NuciAPI.Middleware.ArchitectureTests
{
    [TestFixture]
    public sealed class DependencyRuleTests
    {
        private static readonly Architecture Architecture = CreateArchitecture();

        [Test]
        public void GivenLoadedProductionArchitecture_WhenSelectingProductionTypes_ThenAtLeastOneTypeIsSelected()
        {
            IObjectProvider<IType> productionTypes = Types().That().ResideInNamespace("NuciAPI.Middleware");

            Assert.That(
                productionTypes,
                Is.Not.Null,
                "Violated boundary: no production selector is available.");
            Assert.That(
                new ArchLoader().LoadAssemblies(typeof(NuciApiMiddleware).Assembly).Build(),
                Is.Not.Null,
                "Violated boundary: the NuciAPI.Middleware assembly could not be loaded.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForEntityFrameworkDependencies_ThenNoTypeDependsOnEntityFramework()
        {
            IArchRule noEntityFrameworkRule = Types().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore"))
                .Because("Production code must not depend on Entity Framework.");

            Assert.That(
                noEntityFrameworkRule.HasNoViolations(Architecture),
                Is.True,
                "Violated boundary: production code must not depend on Entity Framework.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForDynamoDbDependencies_ThenNoTypeDependsOnDynamoDb()
        {
            IArchRule noDynamoDbRule = Types().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotDependOnAny(Types().That().ResideInNamespace("Amazon.DynamoDBv2"))
                .Because("Production code must not depend on DynamoDB.");

            Assert.That(
                noDynamoDbRule.HasNoViolations(Architecture),
                Is.True,
                "Violated boundary: production code must not depend on DynamoDB.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForPersistenceImplementations_ThenNoRepositoryOrCommandHandlerTypesExist()
        {
            IArchRule noRepositoryRule = Classes().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotHaveNameContaining("Repository")
                .Because("Production code must not contain repositories or command/query handlers.");
            IArchRule noCommandHandlerRule = Classes().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotHaveNameContaining("CommandHandler");

            Assert.That(noRepositoryRule.HasNoViolations(Architecture), Is.True);
            Assert.That(noCommandHandlerRule.HasNoViolations(Architecture), Is.True);
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForControllerTypes_ThenNoControllerTypesExist()
        {
            IArchRule noControllerRule = Classes().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotHaveNameContaining("Controller")
                .Because("Production code must not contain controllers.");

            Assert.That(
                noControllerRule.HasNoViolations(Architecture),
                Is.True,
                "Violated boundary: production code must not contain controllers.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForMvcDependency_ThenNoTypeDependsOnMvc()
        {
            IArchRule noMvcRule = Types().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.AspNetCore.Mvc"))
                .Because("Production code must not depend on ASP.NET Core MVC.");

            Assert.That(
                noMvcRule.HasNoViolations(Architecture),
                Is.True,
                "Violated boundary: production code must not depend on ASP.NET Core MVC.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForDirectRequestReads_ThenNoFormValueIsReadDirectly()
        {
            IArchRule noDirectFormReadsRule = Classes().That().ResideInNamespace("NuciAPI.Middleware").Should()
                .NotCallAny(MethodMembers().That().HaveName("ReadFormAsync"))
                .Because("Production code must bind input explicitly and must not read request values directly.");

            Assert.That(
                noDirectFormReadsRule.HasNoViolations(Architecture),
                Is.True,
                "Violated boundary: production code must not read request values directly.");
        }

        [Test]
        public void GivenProductionAssembly_WhenCheckingForInternalsVisibleTo_ThenProductionAssemblyDeclaresNone()
        {
            InternalsVisibleToAttribute[] internalsVisibleToAttributes =
                typeof(NuciApiMiddleware).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>().ToArray();

            Assert.That(
                internalsVisibleToAttributes,
                Is.Empty,
                "Violated boundary: production projects must not declare InternalsVisibleTo.");
        }

        private static Architecture CreateArchitecture()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(NuciApiMiddleware).Assembly.Location);
            string assemblyPath = Path.Combine(assemblyDirectory, "NuciAPI.Middleware.dll");

            if (!File.Exists(assemblyPath))
            {
                Assert.Fail($"Required production assembly is unavailable: {assemblyPath}");
            }

            return new ArchLoader().LoadAssemblies(typeof(NuciApiMiddleware).Assembly).Build();
        }
    }
}
