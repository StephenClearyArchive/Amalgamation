using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImpromptuInterface;
using ImpromptuInterface.Dynamic;
using System.Runtime.Serialization;
using System.Dynamic;
using System.Linq.Expressions;
using Castle.DynamicProxy;
using System.Reflection;
using Nito.ConnectedProperties;
using Nito.ConnectedProperties.Implicit;
using AnonymousInterface;
using Amalgams;

// params
// default values
// ref, out
// different interfaces with same member
// generic methods

// Contract dll references, readme, branching - check all libraries!

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public interface ICountable
        {
            int GetCount();
            int Count { get; set; }
        }

        public interface ILongCountable : ICountable
        {
            long LongCount { get; }
        }

        public interface IContainsCountable
        {
            ICountable Countable { get; }
        }

        public sealed class ClassA : ILongCountable
        {
            private int count;

            public int GetCount() { return count; }
            public int Count { get { return count; } set { count = value; } }
            public long LongCount { get { return count; } }
        }

        public sealed class ClassB : IContainsCountable, IAmalgamator
        {
            public ClassB()
            {
                a = new ClassA();
            }

            public ClassA a { get; set; }

            public ICountable Countable
            {
                get { return a; }
            }

            public int OverrideCount()
            {
                return 17;
            }

            public interface ITest
            {
                int valg { get; }
                int vals { set; }
                int val { get; set; }
                event Action X;
                string this[int index] { get; set; }
                void A();
                void A(int arg);
                void A(out int arg);
                void A(int arg1, ref int arg2);
                int B();
            }

            // TODO: multiple interfaces with same methods.

            private delegate void TestDelegate1(out int arg);
            private delegate void TestDelegate2(int arg1, ref int arg);

            object IAmalgamator.CreateAmalgam()
            {
                Anonymous.Implement<ITest>()
                    .Method<Action>(x => x.A, () => { })
                    .Method<Action<int>>(x => x.A, _ => { })
                    .Method<TestDelegate1>(x => x.A, (out int x) => { x = 0; })
                    .Method<TestDelegate2>(x => x.A, (int x, ref int y) => { })
                    .Method<Func<int>>(x => x.B, () => 2)
                    .PropertyGet(x => x.valg, () => 17)
                    .PropertyGet(x => x.val, () => 13)
                    .PropertySet(x => x.val, value => { })
                    .PropertySet<int>("vals", value => { })
                    .EventSubscribe<Action>("X", value => { })
                    .EventUnsubscribe<Action>("X", value => { })
                    .IndexGet<Func<int, string>>(index => null)
                    .IndexSet<Action<int, string>>((index, value) => { })
                    .Create();
                return Amalgamation.Build()
                    .Forward<ILongCountable>(a, false)
                    .Forward<IContainsCountable>(this)
                    .Override<ICountable>(a, anon => anon
                        .PropertyGet(x => x.Count, OverrideCount)
                        .Method<Func<int>>(x => x.GetCount, OverrideCount))
                    .Create();
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            var obj = new ClassB();
            var proxy = obj.GetAmalgam();
            var containsCountable = (IContainsCountable)proxy;
            var countable = (ICountable)proxy;
            var longCountable = (ILongCountable)proxy;
            containsCountable.Countable.Count = 13;
            Assert.AreEqual(13, obj.Countable.Count);
            Assert.AreEqual(17, countable.Count);
            Assert.AreEqual(17, longCountable.Count);
            Assert.AreEqual(13, longCountable.LongCount);
        }
    }
}
