﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using ITypeInfo = Xunit.Abstractions.ITypeInfo;

public class XunitTestFrameworkTests
{
    public class Construction
    {
        [Fact]
        public void GuardClause()
        {
            // TODO: Guard clause for assembly in ctor
        }
    }

    public class FindByAssembly
    {
        [Fact]
        public void GuardClause()
        {
            var framework = TestableXunitTestFramework.Create();
            var sink = new Mock<IMessageSink>();

            ExceptionAssert.ThrowsArgumentNull(
                () => framework.Find(includeSourceInformation: false, messageSink: null),
                "messageSink"
            );
        }

        [Fact]
        public void AssemblyWithNoTypes_ReturnsNoTestCases()
        {
            var framework = TestableXunitTestFramework.Create();

            framework.Find();

            CollectionAssert.Collection(framework.Messages,
                message => Assert.IsAssignableFrom<IDiscoveryCompleteMessage>(message)
            );
        }

        [Fact]
        public void RequestsOnlyPublicTypesFromAssembly()
        {
            var framework = TestableXunitTestFramework.Create();

            framework.Find();

            framework.Assembly.Verify(a => a.GetTypes(/*includePrivateTypes*/ false), Times.Once());
        }

        [Fact]
        public void CallsFindImplWhenTypesAreFoundInAssembly()
        {
            var objectTypeInfo = Reflector2.Wrap(typeof(object));
            var intTypeInfo = Reflector2.Wrap(typeof(int));
            var mockAssembly = new MockAssemblyInfo(types: new[] { objectTypeInfo, intTypeInfo });
            var mockFramework = new Mock<TestableXunitTestFramework>(mockAssembly) { CallBase = true };

            mockFramework.Object.Find();

            mockFramework.Verify(f => f.FindImpl(objectTypeInfo, false), Times.Once());
            mockFramework.Verify(f => f.FindImpl(intTypeInfo, false), Times.Once());
        }

        [Fact]
        public void DoesNotCallSourceProviderWhenNotAskedFor()
        {
            var sourceProvider = new Mock<ISourceInformationProvider>(MockBehavior.Strict);
            var typeInfo = Reflector2.Wrap(typeof(ClassWithSingleTest));
            var mockAssembly = new MockAssemblyInfo(types: new[] { typeInfo });
            var framework = TestableXunitTestFramework.Create(mockAssembly, sourceProvider.Object);

            framework.Find();

            sourceProvider.Verify(sp => sp.GetSourceInformation(It.IsAny<ITestCase>()), Times.Never());
        }

        [Fact]
        public void CallsSourceProviderWhenTypesAreFoundInAssembly()
        {
            var sourceProvider = new Mock<ISourceInformationProvider>();
            sourceProvider.Setup(sp => sp.GetSourceInformation(It.IsAny<ITestCase>()))
                          .Returns(Tuple.Create<string, int?>("Source File", 42));
            var typeInfo = Reflector2.Wrap(typeof(ClassWithSingleTest));
            var mockAssembly = new MockAssemblyInfo(types: new[] { typeInfo });
            var framework = TestableXunitTestFramework.Create(mockAssembly, sourceProvider.Object);

            framework.Find(includeSourceInformation: true);

            CollectionAssert.Collection(framework.Messages,
                message =>
                {
                    var discoveryMessage = Assert.IsAssignableFrom<ITestCaseDiscoveryMessage>(message);
                    Assert.Equal("XunitTestFrameworkTests+ClassWithSingleTest.TestMethod", discoveryMessage.TestCase.DisplayName);
                    Assert.Equal("Source File", discoveryMessage.TestCase.SourceFileName);
                    Assert.Equal(42, discoveryMessage.TestCase.SourceFileLine);
                },
                message => Assert.IsAssignableFrom<IDiscoveryCompleteMessage>(message)
            );
        }
    }

    public class FindByType
    {
        [Fact]
        public void GuardClauses()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = new Mock<ITypeInfo>();
            var sink = new Mock<IMessageSink>();

            ExceptionAssert.ThrowsArgumentNull(
                () => framework.Find(type: null, includeSourceInformation: false, messageSink: sink.Object),
                "type"
            );
            ExceptionAssert.ThrowsArgumentNull(
                () => framework.Find(type: type.Object, includeSourceInformation: false, messageSink: null),
                "messageSink"
            );
        }

        [Fact]
        public void RequestsPublicAndPrivateMethodsFromType()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = new Mock<ITypeInfo>();

            framework.Find(type.Object);

            type.Verify(t => t.GetMethods(/*includePrivateMethods*/ true), Times.Once());
        }

        [Fact]
        public void CallsFindImplWhenMethodsAreFoundOnType()
        {
            var mockFramework = new Mock<TestableXunitTestFramework> { CallBase = true };
            var objectTypeInfo = Reflector2.Wrap(typeof(object));

            mockFramework.Object.Find(objectTypeInfo);

            mockFramework.Verify(f => f.FindImpl(objectTypeInfo, false), Times.Once());
        }

        [Fact]
        public void DoesNotCallSourceProviderWhenNotAskedFor()
        {
            var sourceProvider = new Mock<ISourceInformationProvider>(MockBehavior.Strict);
            var framework = TestableXunitTestFramework.Create(sourceProvider.Object);
            var typeInfo = Reflector2.Wrap(typeof(ClassWithSingleTest));

            framework.Find(typeInfo);

            sourceProvider.Verify(sp => sp.GetSourceInformation(It.IsAny<ITestCase>()), Times.Never());
        }

        [Fact]
        public void CallsSourceProviderWhenTypesAreFoundInAssembly()
        {
            var sourceProvider = new Mock<ISourceInformationProvider>();
            sourceProvider.Setup(sp => sp.GetSourceInformation(It.IsAny<ITestCase>()))
                          .Returns(Tuple.Create<string, int?>("Source File", 42));
            var framework = TestableXunitTestFramework.Create(sourceProvider.Object);
            var typeInfo = Reflector2.Wrap(typeof(ClassWithSingleTest));

            framework.Find(typeInfo, includeSourceInformation: true);

            CollectionAssert.Collection(framework.Messages,
                message =>
                {
                    var discoveryMessage = Assert.IsAssignableFrom<ITestCaseDiscoveryMessage>(message);
                    Assert.Equal("XunitTestFrameworkTests+ClassWithSingleTest.TestMethod", discoveryMessage.TestCase.DisplayName);
                    Assert.Equal("Source File", discoveryMessage.TestCase.SourceFileName);
                    Assert.Equal(42, discoveryMessage.TestCase.SourceFileLine);
                },
                message => Assert.IsAssignableFrom<IDiscoveryCompleteMessage>(message)
            );
        }
    }

    public class FindImpl
    {
        class ClassWithNoTests
        {
            public void NonTestMethod() { }
        }

        [Fact]
        public void ClassWithNoTests_ReturnsNoTestCases()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = Reflector2.Wrap(typeof(ClassWithNoTests));

            framework.FindImpl(type);

            Assert.Empty(framework.Messages);
        }

        class ClassWithOneFact
        {
            [Fact2]
            public void TestMethod() { }
        }

        [Fact]
        public void AssemblyWithFact_ReturnsOneTestCaseOfTypeXunitTestCase()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = Reflector2.Wrap(typeof(ClassWithOneFact));

            framework.FindImpl(type);

            var discoveryMessage = (ITestCaseDiscoveryMessage)Assert.Single(framework.Messages, msg => msg is ITestCaseDiscoveryMessage);
            Assert.IsType<XunitTestCase>(discoveryMessage.TestCase);
        }

        class ClassWithMixOfFactsAndNonFacts
        {
            [Fact2]
            public void TestMethod1() { }

            [Fact2]
            public void TestMethod2() { }

            public void NonTestMethod() { }
        }

        [Fact]
        public void AssemblyWithMixOfFactsAndNonTests_ReturnsTestCasesOnlyForFacts()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = Reflector2.Wrap(typeof(ClassWithMixOfFactsAndNonFacts));

            framework.FindImpl(type);

            var results = framework.Messages
                                   .OfType<ITestCaseDiscoveryMessage>()
                                   .Select(msg => msg.TestCase)
                                   .Cast<IMethodTestCase>()
                                   .ToArray();
            Assert.Equal(2, results.Count());
            Assert.Single(results, t => t.DisplayName == "XunitTestFrameworkTests+FindImpl+ClassWithMixOfFactsAndNonFacts.TestMethod1");
            Assert.Single(results, t => t.DisplayName == "XunitTestFrameworkTests+FindImpl+ClassWithMixOfFactsAndNonFacts.TestMethod2");
        }

        class TheoryWithInlineData
        {
            [Theory2]
            [InlineData2("Hello world")]
            [InlineData2(42)]
            public void TheoryMethod(object value) { }
        }

        [Fact]
        public void AssemblyWithTheoryWithInlineData_ReturnsOneTestCasePerDataRecord()
        {
            var framework = TestableXunitTestFramework.Create();
            var type = Reflector2.Wrap(typeof(TheoryWithInlineData));

            framework.FindImpl(type);

            var results = framework.Messages
                                   .OfType<ITestCaseDiscoveryMessage>()
                                   .Select(msg => msg.TestCase)
                                   .Cast<IMethodTestCase>()
                                   .ToArray();
            Assert.Equal(2, results.Count());
            Assert.Single(results, t => t.DisplayName == "XunitTestFrameworkTests+FindImpl+TheoryWithInlineData.TheoryMethod(value: \"Hello world\")");
            Assert.Single(results, t => t.DisplayName == "XunitTestFrameworkTests+FindImpl+TheoryWithInlineData.TheoryMethod(value: 42)");
        }
    }

    class ClassWithSingleTest
    {
        [Fact2]
        public void TestMethod() { }
    }

    public class TestableXunitTestFramework : XunitTestFramework, IMessageSink
    {
        protected TestableXunitTestFramework()
            : base(new MockAssemblyInfo().Object) { }

        protected TestableXunitTestFramework(MockAssemblyInfo assembly)
            : base(assembly.Object)
        {
            Assembly = assembly;
        }

        TestableXunitTestFramework(MockAssemblyInfo assembly, ISourceInformationProvider sourceProvider)
            : base(assembly.Object, sourceProvider)
        {
            Assembly = assembly;
        }

        public MockAssemblyInfo Assembly { get; private set; }

        public List<ITestMessage> Messages = new List<ITestMessage>();

        public static TestableXunitTestFramework Create()
        {
            return new TestableXunitTestFramework(new MockAssemblyInfo());
        }

        public static TestableXunitTestFramework Create(ISourceInformationProvider sourceProvider)
        {
            return new TestableXunitTestFramework(new MockAssemblyInfo(), sourceProvider);
        }

        public static TestableXunitTestFramework Create(MockAssemblyInfo assembly, ISourceInformationProvider sourceProvider)
        {
            return new TestableXunitTestFramework(assembly, sourceProvider);
        }

        public void Find(bool includeSourceInformation = false)
        {
            base.Find(includeSourceInformation, this);
        }

        public void Find(ITypeInfo type, bool includeSourceInformation = false)
        {
            base.Find(type, includeSourceInformation, this);
        }

        public virtual void FindImpl(ITypeInfo type, bool includeSourceInformation = false)
        {
            base.FindImpl(type, includeSourceInformation, this);
        }

        protected override void FindImpl(ITypeInfo type, bool includeSourceInformation, IMessageSink messageSink)
        {
            FindImpl(type, includeSourceInformation);
        }

        public void OnMessage(ITestMessage message)
        {
            Messages.Add(message);
        }

        public void Dispose() { }
    }
}