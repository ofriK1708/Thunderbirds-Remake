using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    public class ObjectPoolTests
    {
        private GameObject _root;
        private SpriteRenderer _prefab;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PoolTestRoot");
            var prefabGo = new GameObject("Prefab");
            prefabGo.transform.SetParent(_root.transform);
            _prefab = prefabGo.AddComponent<SpriteRenderer>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private ObjectPool<SpriteRenderer> NewPool(int prewarm) =>
            new ObjectPool<SpriteRenderer>(_prefab, _root.transform, prewarm);

        [Test]
        public void Prewarm_CreatesInactiveInstances()
        {
            var pool = NewPool(5);

            Assert.AreEqual(5, pool.CountAll);
            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);
        }

        [Test]
        public void Get_ReturnsAnActiveInstance_WithoutCreatingOne()
        {
            var pool = NewPool(3);

            var item = pool.Get();

            Assert.IsNotNull(item);
            Assert.IsTrue(item.gameObject.activeSelf);
            Assert.AreEqual(3, pool.CountAll, "a warm pool must not instantiate");
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(2, pool.CountInactive);
        }

        [Test]
        public void Get_WhenEmpty_Grows()
        {
            var pool = NewPool(1);

            var a = pool.Get();
            var b = pool.Get();

            Assert.AreNotSame(a, b);
            Assert.AreEqual(2, pool.CountAll);
            Assert.IsTrue(b.gameObject.activeSelf);
        }

        [Test]
        public void Release_DeactivatesAndRecycles()
        {
            var pool = NewPool(1);
            var item = pool.Get();

            pool.Release(item);

            Assert.IsFalse(item.gameObject.activeSelf);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreSame(item, pool.Get(), "the released instance is reused");
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void FullRebuild_CreatesNothingNew()
        {
            var pool = NewPool(100);
            var built = new List<SpriteRenderer>();

            for (var round = 0; round < 3; round++)
            {
                foreach (var item in built) pool.Release(item);
                built.Clear();
                for (var i = 0; i < 100; i++) built.Add(pool.Get());
            }

            Assert.AreEqual(100, pool.CountAll);
        }

        [Test]
        public void Callbacks_RunOnGetAndRelease()
        {
            var log = new List<string>();
            var pool = new ObjectPool<SpriteRenderer>(_prefab, _root.transform, 1,
                onGet: r => log.Add($"get active={r.gameObject.activeSelf}"),
                onRelease: r => log.Add($"release active={r.gameObject.activeSelf}"));

            pool.Release(pool.Get());

            CollectionAssert.AreEqual(new[] { "get active=True", "release active=True" }, log);
        }

        [Test]
        public void DoubleRelease_NeverHandsTheSameInstanceOutTwice()
        {
            // Throwing or ignoring are both acceptable policies; a duplicate in the pool is not.
            var pool = NewPool(2);
            var item = pool.Get();
            pool.Release(item);
            try { pool.Release(item); } catch (System.InvalidOperationException) { }

            var first = pool.Get();
            var second = pool.Get();

            Assert.AreNotSame(first, second);
        }
    }
}
