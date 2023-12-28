using System;
using UnityEngine.Events;
using Peg.Messaging;
using Peg.MessageDispatcher;

namespace DamageSystem
{
    /// <summary>
    /// Listens for the ProjectileHitEvent and invokes any registered methods.
    /// </summary>
    public class ProjectileHitEventHandler : LocalListenerMonoBehaviour
    {
        [Serializable]
        public class HitEvent : UnityEvent<float> { }

        public AbstractMessageReciever.ListenerType Scope = AbstractMessageReciever.ListenerType.Local;
        public HitEvent OnHit;

        private void Awake()
        {
            if((Scope & AbstractMessageReciever.ListenerType.Local) != 0)
                DispatchRoot.AddLocalListener<ProjectileHitEvent>(HandleHit);
            if((Scope & AbstractMessageReciever.ListenerType.Global) != 0)
                GlobalMessagePump.Instance.AddListener<ProjectileHitEvent>(HandleHit);
        }

        protected override void OnDestroy()
        {
            if ((Scope & AbstractMessageReciever.ListenerType.Local) != 0)
                DispatchRoot.RemoveLocalListener<ProjectileHitEvent>(HandleHit);
            if ((Scope & AbstractMessageReciever.ListenerType.Global) != 0)
                GlobalMessagePump.Instance.RemoveListener<ProjectileHitEvent>(HandleHit);
        }

        void HandleHit(ProjectileHitEvent msg)
        {
            OnHit.Invoke(msg.Scale);
        }

    }
}
