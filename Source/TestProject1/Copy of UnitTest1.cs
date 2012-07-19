using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImpromptuInterface;
using ImpromptuInterface.Dynamic;
using System.Runtime.Serialization;
using System.Dynamic;
using System.Linq.Expressions;
using Castle.DynamicProxy;
using System.Reflection;
using Nito.ConnectedProperties;
using Nito.ConnectedProperties.Implicit;

namespace TestProject1
{
    public interface IInstance
    {
        object CreateInstance();
    }

    public static class InstanceExtensions
    {
        private struct InstanceTag { }

        public static object GetInstance(this IInstance instance)
        {
            var property = instance.TryGetConnectedProperty<object, InstanceTag>();
            if (property != null)
                return property.GetOrCreate(instance.CreateInstance);
            return instance.CreateInstance();
        }

        public static T As<T>(this IInstance instance) where T : class
        {
            return instance.GetInstance() as T;
        }
    }

    public interface ITargetSelector
    {
        IEnumerable<Type> GetSupportedInterfaces();
        object GetTarget(Type interfaceType);
    }

    public sealed class DictionaryTargetSelectorBuilder : IEnumerable<KeyValuePair<Type, object>>
    {
        private Dictionary<Type, object> targets = new Dictionary<Type, object>();

        public void Add(Type type, object target, bool includeInheritedInterfaces = true)
        {
            targets.Add(type, target);
            if (includeInheritedInterfaces)
            {
                foreach (var inheritedInterface in type.GetInterfaces())
                    targets.Add(inheritedInterface, target);
            }
        }

        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
        {
            return targets.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<Type, object>>)this).GetEnumerator();
        }

        public ITargetSelector Create()
        {
            var ret = new DictionaryTargetSelector(this.targets);
            this.targets = null;
            return ret;
        }

        private sealed class DictionaryTargetSelector : ITargetSelector
        {
            private readonly IDictionary<Type, object> targets;

            public DictionaryTargetSelector(IDictionary<Type, object> targets)
            {
                this.targets = targets;
            }

            public IEnumerable<Type> GetSupportedInterfaces()
            {
                return targets.Select(x => x.Key);
            }

            public object GetTarget(Type interfaceType)
            {
                return targets[interfaceType];
            }
        }
    }

    public class RedirectingInterceptor : IInterceptor
    {
        private readonly ITargetSelector targetSelector;

        public RedirectingInterceptor(ITargetSelector targetSelector)
        {
            this.targetSelector = targetSelector;
        }

        public void Intercept(IInvocation invocation)
        {
            var changeInvocation = invocation as IChangeProxyTarget;
            if (changeInvocation != null)
                changeInvocation.ChangeInvocationTarget(targetSelector.GetTarget(invocation.Method.DeclaringType));
            invocation.Proceed();
        }
    }

    public static class Mixins
    {
        private static Lazy<ProxyGenerator> generator = new Lazy<ProxyGenerator>();

        public static ProxyGenerator Generator
        {
            get { return generator.Value; }
        }

        public static object GenerateProxy(ITargetSelector selector, ProxyGenerator generator = null)
        {
            // CreateInterfaceProxyWithTargetInterface only supports IChangeProxyTarget on the primary interface (DYNPROXY-169),
            //  so we actually create a series of proxies, each one redirecting a single interface.
            // Also, all target proxies have to claim to implement all the interfaces.
            // This is messy, but it works.
            generator = generator ?? Generator;
            var interceptor = new RedirectingInterceptor(selector);
            var interfaces = selector.GetSupportedInterfaces().ToArray();
            var options = new ProxyGenerationOptions();
            var proxy = generator.CreateInterfaceProxyWithoutTarget(interfaces[0], interfaces);
            for (int i = 0; i != interfaces.Length; ++i)
                proxy = generator.CreateInterfaceProxyWithTargetInterface(interfaces[i], interfaces, proxy, interceptor);
            return proxy;
        }
    }

    [TestClass]
    public class UnitTest1
    {
        public interface ICountable
        {
            int GetCount();
            int Count { get; set; }
        }

        public interface ILongCountable : ICountable
        {
            long LongCount { get; }
        }

        public interface IContainsCountable
        {
            ICountable Countable { get; }
        }

        public sealed class ClassA : ILongCountable
        {
            private int count;

            public int GetCount() { return count; }
            public int Count { get { return count; } set { count = value; } }
            public long LongCount { get { return count; } }
        }

        public sealed class ClassB : IContainsCountable, IInstance
        {
            public ClassB()
            {
                a = new ClassA();
            }

            public ClassA a { get; set; }

            public ICountable Countable
            {
                get { return a; }
            }

            public int OverrideCount()
            {
                return 17;
            }

            object IInstance.CreateInstance()
            {
                var overrideCountable = Builder.New().Object(GetCount: new Func<int>(OverrideCount));
                var targetSelector = new DictionaryTargetSelectorBuilder
                {
                    { typeof(ILongCountable), a, false },
                    { typeof(ICountable), Impromptu.ActLike<ICountable>(overrideCountable) },
                    { typeof(IContainsCountable), this }
                }.Create();
                return (IContainsCountable)Mixins.GenerateProxy(targetSelector);
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            var obj = new ClassB();
            var proxy = obj.GetInstance();
            var containsCountable = (IContainsCountable)proxy;
            var countable = (ICountable)proxy;
            var longCountable = (ILongCountable)proxy;
            containsCountable.Countable.Count = 13;
            Assert.AreEqual(13, obj.Countable.GetCount());
            Assert.AreEqual(17, countable.GetCount());
            Assert.AreEqual(17, longCountable.GetCount());
            Assert.AreEqual(13, longCountable.LongCount);
        }
    }
}
