using Fusion;

namespace Singleton
{
    public abstract class NetworkedSingleton<T> : NetworkBehaviour where T : NetworkedSingleton<T>
    {
        public static T Instance { get; protected set; }

        protected virtual void Awake()
        {
            if (!Instance)
            {
                Instance = (T)this;
                OnSetInstance();
                return;
            }
        
            if (Instance != this) Destroy(gameObject);
        }

        protected virtual void OnSetInstance(){}
    }
}
