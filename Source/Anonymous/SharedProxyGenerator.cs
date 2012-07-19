using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.DynamicProxy;
using System.Threading;

namespace AnonymousInterface
{
    /// <summary>
    /// Provides access to a single, global <see cref="ProxyGenerator"/> instance that is used by default from all AnonymousInterface APIs.
    /// </summary>
    public static class SharedProxyGenerator
    {
        /// <summary>
        /// The lock object.
        /// </summary>
        private static object gate = new object();

        /// <summary>
        /// The actual instance, or <c>null</c> if the instance has not yet been created.
        /// </summary>
        private static ProxyGenerator instance = null;

        /// <summary>
        /// Gets or sets the global <see cref="ProxyGenerator"/> instance, creating it if necessary.
        /// </summary>
        public static ProxyGenerator Instance
        {
            get
            {
                lock (gate)
                {
                    if (instance == null)
                        instance = new ProxyGenerator();
                    return instance;
                }
            }

            set
            {
                lock (gate)
                {
                    instance = value;
                }
            }
        }
    }
}
