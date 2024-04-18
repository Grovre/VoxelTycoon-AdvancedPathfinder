using System;
using JetBrains.Annotations;
using VoxelTycoon;

namespace XMNUtils
{
    public class SimpleManager<T> where T: SimpleManager<T>, new()
    {
        private static T _current;

        [CanBeNull]
        public static T Current => _current;

        [NotNull]
        public static T SafeCurrent
        {
            get
            {
                if (_current == null)
                    throw new InvalidOperationException("Simple manager not initialized");
                return _current;
            }
        }

        public static void Initialize()
        {
            if (_current != null)
                throw new InvalidOperationException("Already initialized");
            _current = new T();
        }
        
        protected UpdateBehaviour Behaviour { get; private set; }
     
        protected SimpleManager()
        {
            Behaviour = UpdateBehaviour.Create(typeof(T).Name);
            Behaviour.OnDestroyAction = delegate
            {
                OnDeinitialize();
                _current = null;
            };
            OnInitialize();
        }
        
        protected virtual void OnDeinitialize()
        {
        }
     
        protected virtual void OnInitialize()
        {
        }
        
    }
}