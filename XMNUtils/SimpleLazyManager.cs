using JetBrains.Annotations;
using VoxelTycoon;

namespace XMNUtils
{
    public class SimpleLazyManager<T> where T: SimpleLazyManager<T>, new()
    {
        private static T _current;

        [NotNull]
        public static T Current => _current ??= new T();
        [CanBeNull]
        public static T CurrentWithoutInit => _current;

        protected UpdateBehaviour Behaviour { get; private set; }
     
        protected SimpleLazyManager()
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