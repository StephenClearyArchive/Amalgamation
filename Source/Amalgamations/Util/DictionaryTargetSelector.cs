using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amalgams.Util
{
    /// <summary>
    /// A target selector that uses a dictionary lookup.
    /// </summary>
    public sealed class DictionaryTargetSelector : ITargetSelector
    {
        /// <summary>
        /// The dictionary lookup.
        /// </summary>
        private readonly IDictionary<Type, object> targets;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryTargetSelector"/> class with the specified dictionary lookup.
        /// </summary>
        /// <param name="targets">The dictionary lookup. This dictionary instance is referenced, not copied.</param>
        public DictionaryTargetSelector(IDictionary<Type, object> targets)
        {
            this.targets = targets;
        }

        IEnumerable<Type> ITargetSelector.GetSupportedInterfaces()
        {
            return targets.Select(x => x.Key);
        }

        object ITargetSelector.GetTarget(Type interfaceType)
        {
            return targets[interfaceType];
        }
    }
}
