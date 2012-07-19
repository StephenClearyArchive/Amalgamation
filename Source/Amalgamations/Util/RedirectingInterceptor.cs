using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.DynamicProxy;

namespace Amalgams.Util
{
    /// <summary>
    /// A method call interceptor which forwards calls to the implementation provided by an <see cref="ITargetSelector"/>. This interceptor must be installed only for interfaces implemented by the target selector.
    /// </summary>
    public sealed class RedirectingInterceptor : IInterceptor
    {
        /// <summary>
        /// The target selector which provides the interface implementation.
        /// </summary>
        private readonly ITargetSelector targetSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedirectingInterceptor"/> class with the specified target selector.
        /// </summary>
        /// <param name="targetSelector">The target selector which provides the interface implementation.</param>
        public RedirectingInterceptor(ITargetSelector targetSelector)
        {
            this.targetSelector = targetSelector;
        }

        void IInterceptor.Intercept(IInvocation invocation)
        {
            // IChangeProxyTarget is only implemented for the "primary interface" on a given proxy (DYNPROXY-169), so we need to Proceed the other calls.
            var changeInvocation = invocation as IChangeProxyTarget;
            if (changeInvocation != null)
                changeInvocation.ChangeInvocationTarget(targetSelector.GetTarget(invocation.Method.DeclaringType));
            invocation.Proceed();
        }
    }
}
