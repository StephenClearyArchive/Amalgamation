using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.DynamicProxy;
using System.Reflection;
using System.Linq.Expressions;

namespace AnonymousInterface.Util
{
    /// <summary>
    /// A builder object used to define the anonymous implmentation.
    /// </summary>
    /// <typeparam name="TInterface">The interface to implement.</typeparam>
    public sealed class Builder<TInterface> where TInterface : class
    {
        /// <summary>
        /// The anonymous method implementations.
        /// </summary>
        private readonly Dictionary<MethodInfo, Delegate> implementations;

        /// <summary>
        /// The optional default target object. If not <c>null</c>, implementations are forwarded to this object unless overridden by the anonymous implementation.
        /// </summary>
        private readonly TInterface defaultTarget;

        /// <summary>
        /// All the methods defined by the interface to implement (and its base interfaces).
        /// </summary>
        private static readonly MethodInfo[] interfaceMethods = typeof(TInterface).GetMethods();

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder&lt;TInterface&gt;"/> class.
        /// </summary>
        /// <param name="defaultTarget">The optional default target object. If defined, implementations are forwarded to this object unless overridden by the anonymous implementation. May be <c>null</c>.</param>
        public Builder(TInterface defaultTarget)
        {
            this.implementations = new Dictionary<MethodInfo, Delegate>();
            this.defaultTarget = defaultTarget;
        }

        /// <summary>
        /// Finds method references within an expression tree.
        /// </summary>
        private sealed class MethodFinder : ExpressionVisitor
        {
            /// <summary>
            /// The collection to hold the referenced methods.
            /// </summary>
            private readonly ICollection<MethodInfo> methods;

            /// <summary>
            /// <c>true</c> to capture the setter method on a property reference; <c>false</c> to capture the getter method on a property reference.
            /// </summary>
            private readonly bool returnSetter;
            
            /// <summary>
            /// Initializes a new instance of the <see cref="Builder&lt;TInterface&gt;.MethodFinder"/> class.
            /// </summary>
            /// <param name="methods">The collection to hold the referenced methods.</param>
            /// <param name="returnSetter"><c>true</c> to capture the setter method on a property reference; <c>false</c> to capture the getter method on a property reference.</param>
            public MethodFinder(ICollection<MethodInfo> methods, bool returnSetter)
            {
                this.methods = methods;
                this.returnSetter = returnSetter;
            }

            /// <summary>
            /// Adds a found method to the referenced methods collection, if the method is for the interface we're implementing.
            /// </summary>
            /// <param name="method">The method that was found.</param>
            private void Add(MethodInfo method)
            {
                if (interfaceMethods.Contains(method))
                    methods.Add(method);
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type == typeof(MethodInfo))
                    Add((MethodInfo)node.Value);
                return base.VisitConstant(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                Add(node.Method);
                return base.VisitMethodCall(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                var prop = node.Member as PropertyInfo;
                if (prop != null)
                {
                    if (this.returnSetter)
                        Add(prop.GetSetMethod());
                    else
                        Add(prop.GetGetMethod());
                }
                return base.VisitMember(node);
            }
        }

        /// <summary>
        /// Analyzes an expression tree and returns a collection of the methods referenced (on the interface we're implementing).
        /// </summary>
        /// <param name="expression">The expression tree.</param>
        /// <param name="returnSetter"><c>true</c> to capture the setter method on a property reference; <c>false</c> to capture the getter method on a property reference.</param>
        /// <returns>A collection of methods referenced by the expression tree.</returns>
        private static IList<MethodInfo> CalledMethods(Expression expression, bool returnSetter = false)
        {
            var ret = new List<MethodInfo>();
            new MethodFinder(ret, returnSetter).Visit(expression);
            return ret;
        }

        /// <summary>
        /// Determines whether an implementation is a valid implementation for an interface method.
        /// </summary>
        /// <param name="interfaceMethod">The method on the interface.</param>
        /// <param name="implementationMethod">The implementation to test.</param>
        /// <param name="implementationName">The name of the implementation method.</param>
        /// <returns><c>true</c> if the implementation is valid for the interface method; otherwise, <c>false</c>.</returns>
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

        /// <summary>
        /// Adds an anonymous interface method implementation. The implementation must already be known to be valid. Raises an exception if the interface method is not a member of the interface, or if an anonymous implementation already exists for that interface method.
        /// </summary>
        /// <param name="interfaceMethod">The interface method.</param>
        /// <param name="implementation">The implementation.</param>
        private void AddMatchedImplementation(MethodInfo interfaceMethod, Delegate implementation)
        {
            if (!interfaceMethods.Contains(interfaceMethod))
                throw new InvalidOperationException("Method is not a member of the interface.");
            if (implementations.ContainsKey(interfaceMethod))
                throw new InvalidOperationException("Interface already has an implementation for " + interfaceMethod.Name);
            implementations.Add(interfaceMethod, implementation);
        }

        /// <summary>
        /// Adds an anonymous interface method implementation. Avoid calling this method; prefer the generic <c>Method</c> overload instead.
        /// </summary>
        /// <param name="interfaceMethod">The interface method.</param>
        /// <param name="implementation">The implementation.</param>
        public Builder<TInterface> Method(MethodInfo interfaceMethod, Delegate implementation)
        {
            if (!Match(interfaceMethod, implementation.Method, interfaceMethod.Name))
                throw new InvalidOperationException("Delegate does not match interface method definition.");
            AddMatchedImplementation(interfaceMethod, implementation);
            return this;
        }

        /// <summary>
        /// Adds an anonymous interface method implementation. Avoid calling this method; prefer the generic <c>Method</c> overload instead.
        /// </summary>
        /// <param name="interfaceMethodName">The name of the interface method.</param>
        /// <param name="implementation">The implementation.</param>
        public Builder<TInterface> Method(string interfaceMethodName, Delegate implementation)
        {
            var method = interfaceMethods.Where(x => Match(x, implementation.Method, interfaceMethodName)).ToArray();
            if (method.Length == 0)
                throw new InvalidOperationException("Could not match \"" + interfaceMethodName + "\" to an interface method.");
            if (method.Length > 1)
                throw new InvalidOperationException("\"" + interfaceMethodName + "\" matched multiple interface methods.");
            AddMatchedImplementation(method[0], implementation);
            return this;
        }

        /// <summary>
        /// Adds a property getter implementation.
        /// </summary>
        /// <typeparam name="T">The type of the property. This does not have to be specified explicitly.</typeparam>
        /// <param name="interfacePropertySelector">An expression selecting the property from the interface. This may be a lambda expression.</param>
        /// <param name="implementation">The implementation of the property getter.</param>
        public Builder<TInterface> PropertyGet<T>(Expression<Func<TInterface, T>> interfacePropertySelector, Func<T> implementation)
        {
            var methodCall = CalledMethods(interfacePropertySelector);
            if (methodCall.Count != 1)
                throw new InvalidOperationException("Could not determine interface property getter.");
            return Method(methodCall[0], implementation);
        }

        /// <summary>
        /// Adds a property setter implementation (for properties that also have getters).
        /// </summary>
        /// <typeparam name="T">The type of the property. This does not have to be specified explicitly.</typeparam>
        /// <param name="interfacePropertySelector">An expression selecting the property from the interface. This may be a lambda expression.</param>
        /// <param name="implementation">The implementation of the property setter.</param>
        public Builder<TInterface> PropertySet<T>(Expression<Func<TInterface, T>> interfacePropertySelector, Action<T> implementation)
        {
            var methodCall = CalledMethods(interfacePropertySelector, true);
            if (methodCall.Count != 1)
                throw new InvalidOperationException("Could not determine interface property setter.");
            return Method(methodCall[0], implementation);
        }

        /// <summary>
        /// Adds a property setter implementation (for properties that do not have getters). The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="T">The type of the property. This should be specified explicitly.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="implementation">The implementation of the property setter.</param>
        public Builder<TInterface> PropertySet<T>(string propertyName, Action<T> implementation)
        {
            return Method("set_" + propertyName, implementation);
        }

        /// <summary>
        /// Adds an index getter implementation. The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="TMethod">The type of the index getter method. This should be specified explicitly.</typeparam>
        /// <param name="implementation">The implementation of the index getter.</param>
        public Builder<TInterface> IndexGet<TMethod>(TMethod implementation)
        {
            if (!(implementation is Delegate))
                throw new InvalidOperationException("Implementation is not a delegate.");
            return Method("get_Item", implementation as Delegate);
        }

        /// <summary>
        /// Adds an index setter implementation. The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="TMethod">The type of the index setter method. This should be specified explicitly.</typeparam>
        /// <param name="implementation">The implementation of the index setter.</param>
        public Builder<TInterface> IndexSet<TMethod>(TMethod implementation)
        {
            if (!(implementation is Delegate))
                throw new InvalidOperationException("Implementation is not a delegate.");
            return Method("set_Item", implementation as Delegate);
        }

        /// <summary>
        /// Adds an event subscription implementation. The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event. This should be specified explicitly.</typeparam>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="implementation">The implementation of the event subscription.</param>
        public Builder<TInterface> EventSubscribe<TEvent>(string eventName, Action<TEvent> implementation)
        {
            return Method("add_" + eventName, implementation);
        }

        /// <summary>
        /// Adds an event unsubscription implementation. The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event. This should be specified explicitly.</typeparam>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="implementation">The implementation of the event unsubscription.</param>
        public Builder<TInterface> EventUnsubscribe<TEvent>(string eventName, Action<TEvent> implementation)
        {
            return Method("remove_" + eventName, implementation);
        }

        /// <summary>
        /// Adds a method implementation. The type argument should be specified explicitly.
        /// </summary>
        /// <typeparam name="TMethod">The type of the method. This should be specified explicitly.</typeparam>
        /// <param name="interfaceMethodSelector">An expression selecting the method from the interface. This may be a lambda expression.</param>
        /// <param name="implementation">The implementation of the method.</param>
        public Builder<TInterface> Method<TMethod>(Expression<Func<TInterface, TMethod>> interfaceMethodSelector, TMethod implementation)
        {
            var methodCall = CalledMethods(interfaceMethodSelector);
            if (methodCall.Count != 1)
                throw new InvalidOperationException("Could not determine interface method.");
            if (!(implementation is Delegate))
                throw new InvalidOperationException("Implementation is not a delegate.");
            return Method(methodCall[0], implementation as Delegate);
        }

        /// <summary>
        /// Creates the anonymous instance.
        /// </summary>
        /// <param name="generator">The generator to use. If <c>null</c>, then <see cref="SharedProxyGenerator.Instance"/> is used. Defaults to <c>null</c>.</param>
        /// <returns>The anonymous instance.</returns>
        public TInterface Create(ProxyGenerator generator = null)
        {
            generator = generator ?? SharedProxyGenerator.Instance;
            if (defaultTarget == null)
                return generator.CreateInterfaceProxyWithoutTarget<TInterface>(new MethodInterceptor(new DictionaryMethodSelector(implementations)));
            return generator.CreateInterfaceProxyWithTarget<TInterface>(defaultTarget, new MethodInterceptor(new DictionaryMethodSelector(implementations)));
        }
    }
}
