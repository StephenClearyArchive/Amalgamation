using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nito.ConnectedProperties;
using Nito.ConnectedProperties.Implicit;

namespace Amalgams
{
    /// <summary>
    /// A type that acts as an amalgamator. This interface is normally implemented explicitly.
    /// </summary>
    public interface IAmalgamator
    {
        /// <summary>
        /// Creates the amalgam. This method is not normally called directly (use <see cref="AmalgamatorExtensions.GetAmalgam"/> or <see cref="AmalgamatorExtensions.As{T}"/> instead). This method is normally implemented by calling <see cref="Amalgamation.Build"/>.
        /// </summary>
        /// <returns>The amalgam.</returns>
        object CreateAmalgam();
    }

    /// <summary>
    /// Provides extension methods for amalgamators.
    /// </summary>
    public static class AmalgamatorExtensions
    {
        private struct AmalgamationTag { }

        /// <summary>
        /// Retrieves the amalgam for an amalgamator. This method uses a cache to prevent multiple amalgam instances.
        /// </summary>
        /// <param name="amalgamator">The amalgamator.</param>
        /// <returns>The amalgam.</returns>
        public static object GetAmalgam(this IAmalgamator amalgamator)
        {
            var property = amalgamator.TryGetConnectedProperty<object, AmalgamationTag>();
            if (property != null)
                return property.GetOrCreate(amalgamator.CreateAmalgam);
            return amalgamator.CreateAmalgam();
        }

        /// <summary>
        /// Retrieves the amalgam for an amalgamator, as-casted to an instance type. This method calls <see cref="GetAmalgam"/>, which provides an amalgam cache.
        /// </summary>
        /// <typeparam name="T">The instance type to which to cast the amalgam.</typeparam>
        /// <param name="amalgamator">The amalgamator.</param>
        /// <returns>The amalgam.</returns>
        public static T As<T>(this IAmalgamator amalgamator) where T : class
        {
            return amalgamator.GetAmalgam() as T;
        }
    }
}
