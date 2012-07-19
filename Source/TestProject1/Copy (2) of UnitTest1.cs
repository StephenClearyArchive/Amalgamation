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

        public void Add<TInterface>(TInterface target, bool includeInheritedInterfaces = true)
        {
            Add(typeof(TInterface), target, includeInheritedInterfaces);
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

            // Get-Property
            // AddGet, AddSet
            public interface ITest
            {
                int valg { get; }
                int vals { set; }
                int val { get; set; }
                event Action X;
                int this[int index] { get; set; }
            }

            object IInstance.CreateInstance()
            {
                var targetSelector = new DictionaryTargetSelectorBuilder
                {
                    { typeof(ILongCountable), a, false },
                    { typeof(ICountable), AnonymousInterface.Implement<ICountable>(new { get_Count = new Func<int>(OverrideCount) })
                        .Add(x => x.GetCount(), new Func<int>(OverrideCount))
                        //.Add(x => x.GetType(), null)
                        .Create() },
                    { typeof(IContainsCountable), this }
                }.Create();
                AnonymousInterface.Implement<ITest>()
                    .Add(x => x.valg, new Func<int>(() => 17))
                    .Add(x => x[0], new Func<int, int>(y => y));
                return (IContainsCountable)Mixins.GenerateProxy(targetSelector);
            }
        }

        // Anonymous interface implementations MADE EZ!
        public static class AnonymousInterface
        {
            // Implementations are matched based on name, parameters, *and* return type.

            public sealed class Builder<TInterface> where TInterface : class
            {
                private readonly Dictionary<MethodInfo, Delegate> implementations;
                private readonly TInterface defaultTarget;
                private readonly MethodInfo[] interfaceMethods;

                public Builder(TInterface defaultTarget)
                {
                    this.implementations = new Dictionary<MethodInfo, Delegate>();
                    this.defaultTarget = defaultTarget;
                    this.interfaceMethods = typeof(TInterface).GetMethods();
                }

                private sealed class MethodFinder : ExpressionVisitor
                {
                    private readonly ICollection<MethodInfo> methods;

                    public MethodFinder(ICollection<MethodInfo> methods)
                    {
                        this.methods = methods;
                    }

                    protected override Expression VisitMethodCall(MethodCallExpression node)
                    {
                        this.methods.Add(node.Method);
                        return base.VisitMethodCall(node);
                    }

                    protected override Expression VisitMember(MemberExpression node)
                    {
                        var prop = node.Member as PropertyInfo;
                        if (prop != null)
                            this.methods.Add(prop.GetGetMethod());
                        return base.VisitMember(node);
                    }
                }

                private static IList<MethodInfo> CalledMethods(Expression expression)
                {
                    var ret = new List<MethodInfo>();
                    new MethodFinder(ret).Visit(expression);
                    return ret;
                }

                private static bool Match(MethodInfo interfaceMethod, MethodInfo implementationMethod, string implementationName)
                {
                    if (ReferenceEquals(interfaceMethod, implementationMethod))
                        return true;
                    if (interfaceMethod.Name != implementationName)
                        return false;
                    if (interfaceMethod.ReturnType != implementationMethod.ReturnType)
                        return false;
                    var interfaceMethodParameters = interfaceMethod.GetParameters();
                    var implementationMethodParameters = implementationMethod.GetParameters();
                    if (interfaceMethodParameters.Length != implementationMethodParameters.Length)
                        return false;
                    for (int i = 0; i != interfaceMethodParameters.Length; ++i)
                    {
                        if (interfaceMethodParameters[i].ParameterType != implementationMethodParameters[i].ParameterType)
                            return false;
                        if ((interfaceMethodParameters[i].Attributes & (ParameterAttributes.In | ParameterAttributes.Out)) !=
                            (implementationMethodParameters[i].Attributes & (ParameterAttributes.In | ParameterAttributes.Out)))
                            return false;
                    }

                    return true;
                }

                private void AddMatchedImplementation(MethodInfo interfaceMethod, Delegate implementation)
                {
                    // TODO: ensure interface method is for our interface
                    if (implementations.ContainsKey(interfaceMethod))
                        throw new InvalidOperationException("Interface already has an implementation for " + interfaceMethod.Name);
                    implementations.Add(interfaceMethod, implementation);
                }

                public Builder<TInterface> AddImplementation(MethodInfo interfaceMethod, Delegate implementation)
                {
                    if (!Match(interfaceMethod, implementation.Method, interfaceMethod.Name))
                        throw new InvalidOperationException("Delegate does not match interface method definition.");
                    AddMatchedImplementation(interfaceMethod, implementation);
                    return this;
                }

                public Builder<TInterface> AddImplementation(string interfaceMethodName, Delegate implementation)
                {
                    var method = interfaceMethods.Where(x => Match(x, implementation.Method, interfaceMethodName)).ToArray();
                    if (method.Length == 0)
                        throw new InvalidOperationException("Could not match \"" + interfaceMethodName + "\" to an interface method.");
                    if (method.Length > 1)
                        throw new InvalidOperationException("\"" + interfaceMethodName + "\" matched multiple interface methods.");
                    AddMatchedImplementation(method[0], implementation);
                    return this;
                }

                public Builder<TInterface> Add(Expression<Func<TInterface, object>> interfaceMethodSelector, Delegate implementation)
                {
                    var methodCall = CalledMethods(interfaceMethodSelector);
                    if (methodCall.Count != 1)
                        throw new InvalidOperationException("Could not determine interface method.");
                    return AddImplementation(methodCall[0], implementation);
                }

                public Builder<TInterface> Add(Expression<Action<TInterface>> interfaceMethodSelector, Delegate implementation)
                {
                    var methodCall = CalledMethods(interfaceMethodSelector);
                    if (methodCall.Count != 1)
                        throw new InvalidOperationException("Could not determine interface method.");
                    return AddImplementation(methodCall[0], implementation);
                }

                public Builder<TInterface> AddAnonymousImplementations(object definition)
                {
                    foreach (var implementationProperty in definition.GetType().GetProperties())
                    {
                        var implementation = implementationProperty.GetValue(definition, null) as Delegate;
                        if (implementation == null)
                            throw new InvalidOperationException("Property \"" + implementationProperty.Name + "\" is not a delegate.");
                        AddImplementation(implementationProperty.Name, implementation);
                    }

                    return this;
                }

                public TInterface Create(ProxyGenerator generator = null)
                {
                    generator = generator ?? Mixins.Generator;
                    if (defaultTarget == null)
                        return generator.CreateInterfaceProxyWithoutTarget<TInterface>(new MethodInterceptor(new ObjectMethodSelector(implementations)));
                    return generator.CreateInterfaceProxyWithTarget<TInterface>(defaultTarget, new MethodInterceptor(new ObjectMethodSelector(implementations)));
                }
            }

            public static Builder<TInterface> Implement<TInterface>(TInterface defaultTarget, object definition) where TInterface : class
            {
                return new Builder<TInterface>(defaultTarget).AddAnonymousImplementations(definition);
            }

            public static Builder<TInterface> Implement<TInterface>(object definition) where TInterface : class
            {
                return new Builder<TInterface>(null).AddAnonymousImplementations(definition);
            }

            public static Builder<TInterface> Implement<TInterface>() where TInterface : class
            {
                return new Builder<TInterface>(null);
            }
        }

        public sealed class ObjectMethodSelector : IMethodSelector
        {
            private readonly Dictionary<MethodInfo, Delegate> implementations;

            public ObjectMethodSelector(Dictionary<MethodInfo, Delegate> implementations)
            {
                this.implementations = implementations;
            }

            public Delegate GetMethod(MethodInfo name)
            {
                Delegate ret;
                if (implementations.TryGetValue(name, out ret))
                    return ret;
                return null;
            }
        };

        public interface IMethodSelector
        {
            Delegate GetMethod(MethodInfo name);
        }

        public class MethodInterceptor : IInterceptor
        {
            private readonly IMethodSelector methodSelector;

            public MethodInterceptor(IMethodSelector methodSelector)
            {
                this.methodSelector = methodSelector;
            }

            public void Intercept(IInvocation invocation)
            {
                var func = methodSelector.GetMethod(invocation.Method);
                if (func != null)
                    invocation.ReturnValue = func.DynamicInvoke(invocation.Arguments);
                else
                    invocation.Proceed();
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
            Assert.AreEqual(13, obj.Countable.Count);
            Assert.AreEqual(17, countable.Count);
            Assert.AreEqual(17, longCountable.Count);
            Assert.AreEqual(13, longCountable.LongCount);
        }
    }
}
