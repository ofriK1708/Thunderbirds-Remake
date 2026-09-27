using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Generic pool of prefab instances (GDD §7 course feature 1; Lecture 6 "Object Pool").
    /// Instances are created up front (pre-warm) and deactivated; <see cref="Get"/> hands one out,
    /// <see cref="Release"/> takes it back. The pool grows when empty, so a bigger level still works,
    /// but a correct pre-warm count means no Instantiate during play.
    /// Open-closed: callers customise get/release through the optional callbacks, not by editing the pool.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;

        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _active;

        /// <summary>Instances currently handed out.</summary>
        public int CountActive => _active.Count;

        /// <summary>Instances waiting in the pool.</summary>
        public int CountInactive => _inactive.Count;

        /// <summary>Every instance this pool ever created. Stops growing once pre-warm is big enough.</summary>
        public int CountAll { get; private set; }

        /// <param name="prefab">What to instantiate.</param>
        /// <param name="parent">Keeps the Hierarchy tidy; every instance lives under it.</param>
        /// <param name="prewarm">How many to create now, deactivated.</param>
        /// <param name="onGet">Runs on an instance as it is handed out (after it is activated).</param>
        /// <param name="onRelease">Runs on an instance as it comes back (before it is deactivated).</param>
        public ObjectPool(T prefab, Transform parent, int prewarm, Action<T> onGet = null, Action<T> onRelease = null)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _onGet = onGet;
            _onRelease = onRelease;
            _inactive = new Stack<T>(prewarm);
            _active = new HashSet<T>();

            for (var i = 0; i < prewarm; i++)
                _inactive.Push(Create());
        }

        /// <summary>
        /// Hands out an active instance: a pooled one if any are waiting, otherwise a new one.
        /// </summary>
        public T Get()
        {
            var item = _inactive.Count > 0 ? _inactive.Pop() : Create();
            _active.Add(item);
            item.gameObject.SetActive(true);
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>
        /// Takes an instance back: it is deactivated and waits for the next <see cref="Get"/>.
        /// </summary>
        public void Release(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            // Remove() is false for anything not currently active: a foreign instance this pool
            // never handed out, or one that was already released - one check covers both.
            if (!_active.Remove(item))
                throw new InvalidOperationException(
                    "Item is not an active instance of this pool (foreign object or already released).");

            _onRelease?.Invoke(item);
            item.gameObject.SetActive(false);
            _inactive.Push(item);
        }

        /// <summary>A new deactivated instance under the parent. Only called at pre-warm or when the pool is empty.</summary>
        private T Create()
        {
            var item = Object.Instantiate(_prefab, _parent);
            item.gameObject.SetActive(false);
            CountAll++;
            return item;
        }
    }
}
