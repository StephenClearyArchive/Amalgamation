using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.DynamicProxy;

namespace AnonymousInterface.Util
{
    /// <summary>
    /// A method call interceptor which forwards calls to the implementation provided by an <see cref="IMethodSelector"/>. If no implementation is provided, this interceptor allows the call to proceed to the next interceptor.
    /// </summary>
    public sealed class MethodInterceptor : IInterceptor
    {
        /// <summary>
        /// The method selector which provides method implementations.
        /// </summary>
        private readonly IMethodSelector methodSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodInterceptor"/> class with the specified method selector.
        /// </summary>
        /// <param name="methodSelector">The method selector which provides method implementations.</param>
        public MethodInterceptor(IMethodSelector methodSelector)
        {
            this.methodSelector = methodSelector;
        }

        void IInterceptor.Intercept(IInvocation invocation)
        {
            var func = methodSelector.GetMethod(invocation.Method);
            if (func != null)
                invocation.ReturnValue = func.DynamicInvoke(invocation.Arguments);
            else
                invocation.Proceed();
        }
    }
}
