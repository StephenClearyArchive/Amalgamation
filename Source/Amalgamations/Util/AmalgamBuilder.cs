using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnonymousInterface;

namespace Amalgams.Util
{
    /// <summary>
    /// A builder object used to define the amalgam.
    /// </summary>
    public sealed class AmalgamBuilder
    {
        /// <summary>
        /// The interface implementations.
        /// </summary>
        private readonly Dictionary<Type, object> targets = new Dictionary<Type, object>();

        /// <summary>
        /// Adds an interface implementation. Throws an exception if the type is not an interface or if the interface is already implemented.
        /// </summary>
        /// <param name="type">The interface type.</param>
        /// <param name="target">The interface implementation.</param>
        private void Add(Type type, object target)
        {
            if (!type.IsInterface)
                throw new InvalidOperationException("Type " + type.Name + " is not an interface.");
            if (targets.ContainsKey(type))
                throw new InvalidOperationException("Amalgamation already has an implementation for interface " + type.Name + ".");
            targets.Add(type, target);
        }

        /// <summary>
        /// Adds an interface implementation, optionally including inherited interfaces. Throws an exception if the type is not an interface or if any of the interfaces are already implemented.
        /// </summary>
        /// <param name="type">The interface type.</param>
        /// <param name="target">The interface implementation.</param>
        /// <param name="includeInheritedInterfaces"><c>true</c> to include inherited interfaces; <c>false</c> to only include the specified interface type.</param>
        private void Add(Type type, object target, bool includeInheritedInterfaces)
        {
            Add(type, target);
            if (!includeInheritedInterfaces)
                return;
            foreach (var inheritedInterface in type.GetInterfaces())
                Add(inheritedInterface, target);
        }

        /// <summary>
        /// Forwards an interface (and optionally its inherited interfaces) to a specific target implementation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <param name="target">The target implementation.</param>
        /// <param name="includeInheritedInterfaces"><c>true</c> to forward inherited interfaces; <c>false</c> to only forward the specified interface type.</param>
        public AmalgamBuilder Forward<TInterface>(TInterface target, bool includeInheritedInterfaces = true) where TInterface : class
        {
            Add(typeof(TInterface), target, includeInheritedInterfaces);
            return this;
        }

        /// <summary>
        /// Implements an interface (and optionally its inherited interfaces) anonymously.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <param name="anonymousDefinition">The anonymous implementation definition delegate, which receives and returns an anonymous implementation builder. Do not call <c>Create</c> on the anonymous implementation builder.</param>
        /// <param name="includeInheritedInterfaces"><c>true</c> to implement inherited interfaces; <c>false</c> to only implement the specified interface type.</param>
        public AmalgamBuilder Implement<TInterface>(Func<AnonymousInterface.Util.Builder<TInterface>, AnonymousInterface.Util.Builder<TInterface>> anonymousDefinition, bool includeInheritedInterfaces = true) where TInterface : class
        {
            var builder = Anonymous.Implement<TInterface>();
            builder = anonymousDefinition(builder);
            Add(typeof(TInterface), builder.Create(), includeInheritedInterfaces);
            return this;
        }

        /// <summary>
        /// Forwards an interface (and optionally ints inherited interfaces) to a target implementation, overriding some members with anonymous implementations.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <param name="defaultTarget">The target implementation, which receives any interface calls not overriden by the anonymous implementation.</param>
        /// <param name="anonymousDefinition">The anonymous implementation definition delegate, which receives and returns an anonymous implementation builder. Do not call <c>Create</c> on the anonymous implementation builder.</param>
        /// <param name="includeInheritedInterfaces"><c>true</c> to forward/implement inherited interfaces; <c>false</c> to only forward/implement the specified interface type.</param>
        public AmalgamBuilder Override<TInterface>(TInterface defaultTarget, Func<AnonymousInterface.Util.Builder<TInterface>, AnonymousInterface.Util.Builder<TInterface>> anonymousDefinition, bool includeInheritedInterfaces = true) where TInterface : class
        {
            var builder = Anonymous.Implement<TInterface>(defaultTarget);
            builder = anonymousDefinition(builder);
            Add(typeof(TInterface), builder.Create(), includeInheritedInterfaces);
            return this;
        }

        /// <summary>
        /// Creates the amalgam.
        /// </summary>
        /// <returns>The amalgam.</returns>
        public object Create()
        {
            return Amalgamation.GenerateProxy(new DictionaryTargetSelector(targets));
        }

        /// <summary>
        /// Creates the amalgam, casting it to an interface type. Throws an exception if the amalgam does not implement the specified interface.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <returns>The amalgam.</returns>
        public TInterface Create<TInterface>() where TInterface : class
        {
            return (TInterface)Create();
        }
    }
}
