using Castle.DynamicProxy;
using Moq;
using System.Linq.Expressions;
using System.Reflection;

namespace MoqWithAttributes
{
    public static class Moqtils
    {

        private static readonly Func<Mock, bool> ObjectIsInitializedAccessor = CreateIsObjectInitializedAccessor();

        private static Func<Mock, bool> CreateIsObjectInitializedAccessor()
        {
            PropertyInfo propertyInfo = typeof(Mock).GetProperty("IsObjectInitialized", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Unable to check");

            var mockParameter = Expression.Parameter(typeof(Mock));
            return Expression.Lambda<Func<Mock, bool>>(Expression.Property(mockParameter, propertyInfo), mockParameter).Compile();
        }

        private static ProxyGenerationOptions? GetProxyGenerationOptions()
        {
            Type? proxyFactoryType = typeof(Mock).Assembly.GetTypes().FirstOrDefault(type => type.Name == "ProxyFactory");
            if (proxyFactoryType == null)
                return null;

            object? proxyFactory = proxyFactoryType.InvokeMember("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty, null, null, null);
            if (proxyFactory == null || proxyFactory.GetType().Name != "CastleProxyFactory")
                return null;

            return (ProxyGenerationOptions?)proxyFactory.GetType().GetField("generationOptions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(proxyFactory);
        }

        internal static object SyncLock { get; } = new object();

        public static bool IsInitialized(this Mock mock) => ObjectIsInitializedAccessor(mock);

        public static T GetObjectWithAttributes<T>(this Mock<T> mock, params Expression<Func<Attribute>>[] attributeFactoryExpressions) where T : class
        {
            if (mock.IsInitialized())
                throw new ArgumentException("Mock is already initialized", nameof(mock));

            ProxyGenerationOptions options = GetProxyGenerationOptions() ?? throw new InvalidOperationException("Hack is gone");
            
            lock (SyncLock)
            {
                // Shallow copy of the list
                var capturedAttributes = options.AdditionalAttributes.ToList();

                try
                {
                    // Clear the list first
                    options.AdditionalAttributes.Clear();

                    // Then add our attributes
                    foreach (var attributeFactoryExpression in attributeFactoryExpressions)
                        options.AdditionalAttributes.Add(CustomAttributeInfo.FromExpression(attributeFactoryExpression));

                    // Let Moq create the proxy
                    return mock.Object;
                }
                finally
                {
                    // Restore the initial list
                    options.AdditionalAttributes.Clear();
                    foreach (var attribute in capturedAttributes)
                        options.AdditionalAttributes.Add(attribute);
                }
            }
        }

    }

}