using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace AnonymousInterface.Util
{
    /// <summary>
    /// A method selector that uses a dictionary lookup.
    /// </summary>
    public sealed class DictionaryMethodSelector : IMethodSelector
    {
        /// <summary>
        /// The dictionary lookup.
        /// </summary>
        private readonly IDictionary<MethodInfo, Delegate> implementations;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryMethodSelector"/> class with the specified dictionary lookup.
        /// </summary>
        /// <param name="implementations">The dictionary lookup. This dictionary instance is referenced, not copied.</param>
        public DictionaryMethodSelector(IDictionary<MethodInfo, Delegate> implementations)
        {
            this.implementations = implementations;
        }

        Delegate IMethodSelector.GetMethod(MethodInfo name)
        {
            Delegate ret;
            if (implementations.TryGetValue(name, out ret))
                return ret;
            return null;
        }
    };
}
