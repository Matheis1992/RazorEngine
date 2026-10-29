using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// Checks that templates compiled with the Roslyn backend can use modern C# features
    /// (the CodeDom backend, <c>CodeDomCompilerServiceFactory</c>, only supports C# 5).
    /// </summary>
    [TestFixture]
    public class RoslynLanguageVersionTestFixture
    {
        private static string Run(string template, Person model)
        {
            string result = null;
            RoslynCompilerTestFixture.RunTestHelper(service =>
            {
                result = service.RunCompile(template, template.GetHashCode().ToString(), typeof(Person), model);
            });
            return result;
        }

        private static string RunWithLanguageVersion(string template, LanguageVersion languageVersion)
        {
            string result = null;
            RoslynCompilerTestFixture.RunTestHelper(service =>
            {
                result = service.RunCompile(template, template.GetHashCode().ToString());
            }, config =>
            {
                config.CompilerServiceFactory = new DefaultCompilerServiceFactory { LanguageVersion = languageVersion };
            });
            return result;
        }

        /// <summary>
        /// A template that names a member "field" and returns it from a property accessor.
        /// </summary>
        private const string FieldTemplate =
            "@functions { string field = \"member\"; public string Value { get { return field; } } }[@(Value ?? \"null\")]";

        /// <summary>
        /// C# 14: "field" inside a property accessor is the property's backing field, so the template's member is not used.
        /// This is the default, because the default language version is the newest one.
        /// </summary>
        [Test]
        public void Roslyn_FieldKeyword_IsBackingFieldByDefault()
        {
            Assert.That(RunWithLanguageVersion(FieldTemplate, LanguageVersion.Default), Is.EqualTo("[null]"));
        }

        /// <summary>
        /// Pinning C# 13 keeps the old meaning: "field" is the template's member, as with CodeDom.
        /// </summary>
        [Test]
        public void Roslyn_FieldKeyword_IsMemberWithCSharp13()
        {
            Assert.That(RunWithLanguageVersion(FieldTemplate, LanguageVersion.CSharp13), Is.EqualTo("[member]"));
        }

        /// <summary>
        /// A pinned language version is really used: C# 7.3 rejects a switch expression (C# 8).
        /// </summary>
        [Test]
        public void Roslyn_PinnedLanguageVersion_RejectsNewerSyntax()
        {
            const string template = "@{ var text = 1 switch { 1 => \"one\", _ => \"other\" }; }@text";
            Assert.That(RunWithLanguageVersion(template, LanguageVersion.Default), Is.EqualTo("one"));
            Assert.Throws<TemplateCompilationException>(() => RunWithLanguageVersion(template, LanguageVersion.CSharp7_3));
        }

        /// <summary>
        /// C# 6: string interpolation.
        /// </summary>
        [Test]
        public void Roslyn_StringInterpolation()
        {
            var result = Run("@($\"Hello {Model.Forename}!\")", new Person { Forename = "Matt" });
            Assert.That(result, Is.EqualTo("Hello Matt!"));
        }

        /// <summary>
        /// C# 6: null-conditional operator.
        /// </summary>
        [Test]
        public void Roslyn_NullConditional()
        {
            var result = Run("@(Model.Surname?.ToUpper() ?? \"none\")", new Person { Forename = "Matt" });
            Assert.That(result, Is.EqualTo("none"));
        }

        /// <summary>
        /// C# 6: nameof.
        /// </summary>
        [Test]
        public void Roslyn_NameOf()
        {
            var result = Run("@(nameof(Model.Forename))", new Person());
            Assert.That(result, Is.EqualTo("Forename"));
        }

        /// <summary>
        /// C# 7: tuples and deconstruction.
        /// </summary>
        [Test]
        public void Roslyn_TupleDeconstruction()
        {
            var result = Run("@{ var (a, b) = (Model.Age, 2); }@(a * b)", new Person { Age = 21 });
            Assert.That(result, Is.EqualTo("42"));
        }

        /// <summary>
        /// C# 8: switch expression with patterns.
        /// </summary>
        [Test]
        public void Roslyn_SwitchExpression()
        {
            var result = Run(
                "@{ object age = Model.Age; var text = age switch { int n when n >= 18 => \"adult\", int => \"child\", _ => \"unknown\" }; }@text",
                new Person { Age = 42 });
            Assert.That(result, Is.EqualTo("adult"));
        }
    }
}
