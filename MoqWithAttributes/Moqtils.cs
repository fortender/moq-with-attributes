﻿using Castle.DynamicProxy;
using Moq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

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

        private static readonly Func<ProxyGenerationOptions?>? ProxyGenerationOptionsAccessor = CreateProxyGenerationOptionsAccessor();

        private static Func<ProxyGenerationOptions?>? CreateProxyGenerationOptionsAccessor()
        {
            Assembly assembly = typeof(Mock).Assembly;

            Type? proxyFactoryType = assembly.GetType("Moq.ProxyFactory");
            if (proxyFactoryType == null)
                return null;

            PropertyInfo? proxyFactoryInstancePropertyInfo = proxyFactoryType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (proxyFactoryInstancePropertyInfo == null)
                return null;
            
            Type? castleProxyFactoryType = assembly.GetType("Moq.CastleProxyFactory");
            if (castleProxyFactoryType == null)
                return null;
            
            FieldInfo? generationOptionsFieldInfo = castleProxyFactoryType.GetField("generationOptions", BindingFlags.Instance | BindingFlags.NonPublic);
            if (generationOptionsFieldInfo == null)
                return null;

            // ProxyFactory.Instance as CastleProxyFactory
            var castleProxyInstanceExpr = Expression.TypeAs(
                Expression.Property(null, proxyFactoryInstancePropertyInfo), 
                castleProxyFactoryType
            );

            return Expression.Lambda<Func<ProxyGenerationOptions?>>(
                Expression.Condition(
                    // if (castleProxyInstance == null)
                    Expression.Equal(castleProxyInstanceExpr, Expression.Constant(null)),
                    // true:  return (ProxyGenerationOptions?)null
                    Expression.Constant(null, typeof(ProxyGenerationOptions)),
                    // false: return (ProxyFactory.Instance as CastleProxyFactory).generationOptionsFieldInfo
                    Expression.Field(castleProxyInstanceExpr, generationOptionsFieldInfo)
                )
            ).Compile();
        }

        internal static object SyncLock { get; } = new object();

        public static bool IsInitialized(this Mock mock) => ObjectIsInitializedAccessor(mock);

        public static T GetObjectWithAttributes<T>(this Mock<T> mock, params Expression<Func<Attribute>>[] attributeFactoryExpressions) where T : class
        {
            if (mock.IsInitialized())
                throw new ArgumentException("Mock is already initialized", nameof(mock));
            
            ProxyGenerationOptions options = ProxyGenerationOptionsAccessor?.Invoke() ?? throw new InvalidOperationException("Hack is gone");

            return SwapoutDelegate == null ?
                GetObjectWithAttributesSafely(mock, options, attributeFactoryExpressions) :
                GetObjectWithAttributesRatherUnsafe(mock, options, attributeFactoryExpressions);
        }

        private static T GetObjectWithAttributesRatherUnsafe<T>(Mock<T> mock, ProxyGenerationOptions options, params Expression<Func<Attribute>>[] attributeFactoryExpressions) where T : class
        {
            IList<CustomAttributeInfo> customAttributes = attributeFactoryExpressions.Select(expr => CustomAttributeInfo.FromExpression(expr)).ToList();
            IList<CustomAttributeInfo> capturedAttributes = SwapoutDelegate!(options, customAttributes);

            try
            {
                return mock.Object;
            }
            finally
            {
                SwapoutDelegate!(options, capturedAttributes);
            }
        }

        private static T GetObjectWithAttributesSafely<T>(Mock<T> mock, ProxyGenerationOptions options, params Expression<Func<Attribute>>[] attributeFactoryExpressions) where T : class
        {
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

        private static readonly Func<ProxyGenerationOptions, IList<CustomAttributeInfo>, IList<CustomAttributeInfo>>? SwapoutDelegate = CreateSwapoutDelegate();

        private static Func<ProxyGenerationOptions, IList<CustomAttributeInfo>, IList<CustomAttributeInfo>>? CreateSwapoutDelegate()
        {
            FieldInfo? additionalAttributesFieldInfo = typeof(ProxyGenerationOptions).GetField("additionalAttributes", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalAttributesFieldInfo == null)
                return null;

            MethodInfo? interlockedExchangeMethodInfo = 
                typeof(Interlocked)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(methodInfo => methodInfo.IsGenericMethod && methodInfo.Name == nameof(Interlocked.Exchange))?
                .MakeGenericMethod(typeof(IList<CustomAttributeInfo>));
            if (interlockedExchangeMethodInfo == null)
                return null;

            var dynamicMethod = new DynamicMethod("SwapAdditionalAttributes", typeof(IList<CustomAttributeInfo>), new[] { typeof(ProxyGenerationOptions), typeof(IList<CustomAttributeInfo>) });
            var generator = dynamicMethod.GetILGenerator();

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldflda, additionalAttributesFieldInfo);
            generator.Emit(OpCodes.Ldarg_1);
            generator.EmitCall(OpCodes.Call, interlockedExchangeMethodInfo, null);
            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Func<ProxyGenerationOptions, IList<CustomAttributeInfo>, IList<CustomAttributeInfo>>>();
        }

    }

}