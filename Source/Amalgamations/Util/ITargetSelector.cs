using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amalgams.Util
{
    /// <summary>
    /// An object that selects interface implementations.
    /// </summary>
    public interface ITargetSelector
    {
        /// <summary>
        /// Enumerates all interfaces supported by this selector.
        /// </summary>
        IEnumerable<Type> GetSupportedInterfaces();

        /// <summary>
        /// Gets an implementation for a specific interface. Must not return <c>null</c>.
        /// </summary>
        /// <param name="interfaceType">The interface type. This must be a type enumerated by <see cref="GetSupportedInterfaces"/>.</param>
        /// <returns>The interface implementation. Will not be <c>null</c>.</returns>
        object GetTarget(Type interfaceType);
    }
}
