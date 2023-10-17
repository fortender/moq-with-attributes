using Moq;
using System.Reflection;

namespace MoqWithAttributes.Tests
{
    public class MoqtilsTests
    {

        public interface IAmBeingProxied
        {
            void Foo();
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class FooAttribute : Attribute
        {

            public FooAttribute(string value)
            {
                Value = value;
            }

            public string Value { get; init; }

        }

        [AttributeUsage(AttributeTargets.Class)]
        public class BarAttribute : Attribute
        {

            public BarAttribute(string value)
            {
                Value = value;
            }

            public string Value { get; init; }

        }

        [Fact]
        public void GetObjectWithAttributes_HasProxyWithCustomAttributeOnIt()
        {
            var mock = new Mock<IAmBeingProxied>();
            IAmBeingProxied mocked = mock.GetObjectWithAttributes(() => new FooAttribute("Foobar"));
            FooAttribute? fooAttribute = mocked.GetType().GetCustomAttribute<FooAttribute>();
            Assert.NotNull(fooAttribute);
            Assert.Equal("Foobar", fooAttribute.Value);
        }

        [Fact]
        public void GetObjectWithAttributes_HasProxyWithMultipleCustomAttributesOnIt()
        {
            var mock = new Mock<IAmBeingProxied>();
            IAmBeingProxied mocked = mock.GetObjectWithAttributes(
                () => new FooAttribute("Foobar"),
                () => new BarAttribute("Barfoo"));

            Type proxyType = mocked.GetType();

            FooAttribute? fooAttribute = proxyType.GetCustomAttribute<FooAttribute>();
            Assert.NotNull(fooAttribute);
            Assert.Equal("Foobar", fooAttribute.Value);

            BarAttribute? barAttribute = proxyType.GetCustomAttribute<BarAttribute>();
            Assert.NotNull(barAttribute);
            Assert.Equal("Barfoo", barAttribute.Value);
        }

        [Fact]
        public void GetObjectWithAttributes_RestoresAdditionalAttributesAfter()
        {
            GetObjectWithAttributes_HasProxyWithCustomAttributeOnIt();

            var mock = new Mock<IAmBeingProxied>();
            FooAttribute? fooAttribute = mock.Object.GetType().GetCustomAttribute<FooAttribute>();
            Assert.Null(fooAttribute);
        }

    }
}