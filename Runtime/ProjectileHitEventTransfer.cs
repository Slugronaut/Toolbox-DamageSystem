using Peg;
using Peg.Messaging;
using UnityEngine;


namespace DamageSystem
{
    /// <summary>
    /// Attached to projectiles in order to transfer the HealthChanged event back to the owner of the weapon that produced this projectile.
    /// Note that this can only be attached to projectiles. Objects that simply use damage triggers don't have a link back to any attacker source
    /// since they themselves are the source.
    /// </summary>
    [RequireComponent(typeof(Projectile))]
    public class ProjectileHitEventTransfer : LocalListenerMonoBehaviour
    {
        [Tooltip("The amount of meter built by a single strike of this projectile (not incuding per-character scaling factors).")]
        public float Strength = 1;
        [Tooltip("Even if the target absorbed the damage, do we build meter? Must be set at start.")]
        public bool AbsorptionBuildsMeter = true;

        Projectile Proj;
        

        void Awake()
        {
            DispatchRoot.AddLocalListener<CausedHealthChangedEvent>(HandleHit);
            if(AbsorptionBuildsMeter)
                DispatchRoot.AddLocalListener<HealthDamageAbsorbedByOtherEvent>(HandleAbsorb);
            Proj = GetComponent<Projectile>();

            if (TryGetComponent<DamageHandler>(out var damHandler))
                damHandler.OnKilledTarget.AddListener(HandleKilledTarget);
        }

        protected override void OnDestroy()
        {
            DispatchRoot.RemoveLocalListener<CausedHealthChangedEvent>(HandleHit);
                if(AbsorptionBuildsMeter)
            DispatchRoot.AddLocalListener<HealthDamageAbsorbedByOtherEvent>(HandleAbsorb);

            if (TryGetComponent<DamageHandler>(out var damHandler))
                damHandler.OnKilledTarget.RemoveListener(HandleKilledTarget);

            base.OnDestroy();
        }

        void HandleKilledTarget(EntityRoot agent, EntityRoot target)
        {
            var proj = agent.FindComponentInEntity<Projectile>(true);
            if (proj == null) return;

            GlobalMessagePump.Instance.ForwardDispatch(target.gameObject, ProjectileHitMeEvent.Shared.Change(proj));
        }
        
        void HandleHit(CausedHealthChangedEvent msg)
        {
            //Fuckin' yikes! This might need to be profiled for performance!
            //All these null-ref checks are super expensive!
            //SuperMeterBuildEvent.Shared.Strength = Strength;
            ProjectileHitEvent.Shared.Scale = Strength;
            if(Proj != null && Proj.Source != null && Proj.Source.Owner != null)
                GlobalMessagePump.Instance.PostMessage(ProjectileHitEvent.Shared); 
        }

        void HandleAbsorb(HealthDamageAbsorbedByOtherEvent msg)
        {
            //Fuckin' yikes! This might need to be profiled for performance!
            //All these null-ref checks are super expensive!
            //SuperMeterBuildEvent.Shared.Strength = Strength;
            ProjectileHitEvent.Shared.Scale = Strength;
            if (Proj != null && Proj.Source != null && Proj.Source.Owner != null)
                GlobalMessagePump.Instance.PostMessage(ProjectileHitEvent.Shared);
        }
    }


    public class ProjectileHitEvent : IMessage
    {
        public float Scale = 1;
        public static ProjectileHitEvent Shared = new ProjectileHitEvent();
    }


    public class ProjectileHitMeEvent : TargetMessage<Projectile, ProjectileHitMeEvent>
    {
        public static ProjectileHitMeEvent Shared = new ProjectileHitMeEvent();
    }
}
