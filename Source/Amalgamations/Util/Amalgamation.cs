using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.DynamicProxy;
using AnonymousInterface;

namespace Amalgams.Util
{
    /// <summary>
    /// Provides utility methods.
    /// </summary>
    public static class Amalgamation
    {
        /// <summary>
        /// Creates a proxy that implements the specified target selector. The returned proxy implements all interfaces supported by the target selector by forwarding the interface methods to the targets selected by the target selector.
        /// </summary>
        /// <param name="selector">The target selector to implement.</param>
        /// <param name="generator">The generator to use. If <c>null</c>, then <c>AnonymousInterface.SharedProxyGenerator.Instance</c> is used. Defaults to <c>null</c>.</param>
        /// <returns>The proxy.</returns>
        public static object GenerateProxy(ITargetSelector selector, ProxyGenerator generator = null)
        {
            // CreateInterfaceProxyWithTargetInterface only supports IChangeProxyTarget on the primary interface (DYNPROXY-169),
            //  so we actually create a series of proxies, each one redirecting a single interface.
            // Also, all target proxies have to claim to implement all the interfaces (they don't have to *actually* implement them).
            // This is messy, but it works.
            generator = generator ?? SharedProxyGenerator.Instance;
            var interceptor = new RedirectingInterceptor(selector);
            var interfaces = selector.GetSupportedInterfaces().ToArray();
            var options = new ProxyGenerationOptions();
            var proxy = generator.CreateInterfaceProxyWithoutTarget(interfaces[0], interfaces);
            for (int i = 0; i != interfaces.Length; ++i)
                proxy = generator.CreateInterfaceProxyWithTargetInterface(interfaces[i], interfaces, proxy, interceptor);
            return proxy;
        }
    }
}
