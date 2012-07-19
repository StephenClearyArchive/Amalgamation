using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace AnonymousInterface.Util
{
    /// <summary>
    /// An object that selects method implementations.
    /// </summary>
    public interface IMethodSelector
    {
        /// <summary>
        /// Returns an implementation for the given method. May return <c>null</c> if there is no implementation.
        /// </summary>
        /// <param name="method">The method for which to return the implementation.</param>
        /// <returns>The implementation of the method, if any; otherwise, <c>null</c>.</returns>
        Delegate GetMethod(MethodInfo method);
    }
}
