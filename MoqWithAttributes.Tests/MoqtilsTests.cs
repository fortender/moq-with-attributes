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

        [Fact]
        public void GetObjectWithAttributes_HasProxyWithCustomAttributeOnIt()
        {
            var mock = new Mock<IAmBeingProxied>();
            IAmBeingProxied mocked = mock.GetObjectWithAttributes(() => new FooAttribute("Foobar"));
            FooAttribute? fooAttribute = mocked.GetType().GetCustomAttribute<FooAttribute>();
            Assert.NotNull(fooAttribute);
            Assert.Equal("Foobar", fooAttribute.Value);
        }
    }
}