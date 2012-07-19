using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amalgams
{
    /// <summary>
    /// Provides utility methods for amalgamation.
    /// </summary>
    public static class Amalgamation
    {
        /// <summary>
        /// Builds an amalgam. This method is normally called to implement <see cref="IAmalgamator.CreateAmalgam"/>.
        /// </summary>
        /// <returns>An amalgam builder.</returns>
        public static Util.AmalgamBuilder Build()
        {
            return new Util.AmalgamBuilder();
        }

        /// <summary>
        /// As-casts an amalgamator or amalgam to a given instance. The amalgam is created (and cached) if necessary.
        /// </summary>
        /// <typeparam name="T">The instance type to which to cast the amalgam.</typeparam>
        /// <param name="instance">The amalgamator or amalgam.</param>
        /// <returns>The amalgam.</returns>
        public static T As<T>(object instance) where T : class
        {
            var amalgamator = instance as IAmalgamator;
            if (amalgamator != null)
                return amalgamator.As<T>();
            return instance as T;
        }
    }
}
