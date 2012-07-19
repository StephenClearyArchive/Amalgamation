using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnonymousInterface
{
    /// <summary>
    /// Provides the entrypoint for creating an anonymous interface implementation.
    /// </summary>
    public static class Anonymous
    {
        /// <summary>
        /// Starts implementing an interface, optionally forwarding implementations to a default target object.
        /// </summary>
        /// <typeparam name="TInterface">The interface to implement.</typeparam>
        /// <param name="defaultTarget">The optional default target object. If defined, implementations are forwarded to this object unless overridden by the anonymous implementation. Defaults to <c>null</c>.</param>
        /// <returns>A builder object used to define the anonymous implmentation.</returns>
        public static Util.Builder<TInterface> Implement<TInterface>(TInterface defaultTarget = null) where TInterface : class
        {
            return new Util.Builder<TInterface>(defaultTarget);
        }
    }
}
