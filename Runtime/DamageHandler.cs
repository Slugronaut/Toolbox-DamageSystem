using Toolbox;
using UnityEngine;

using Sirenix.OdinInspector;
using UnityEngine.Events;
using Toolbox.Game;

namespace DamageSystem
{
    /// <summary>
    /// Provides a simple public method for dealing damage to a GameObject.
    /// The GameObject provided must be part of an AEH that has
    /// a Health component attached to it.
    /// </summary>
    public class DamageHandler : MonoBehaviour
    {
        public enum Flags
        {
            HonorInvincibiity   = 1 << 0,
            ConfirmLos          = 1 << 1,
        }

        [Tooltip("The period in seconds between processing damage when colliders stay within the los ray.")]
        public float Freq = 0;
        public float MinDamage = 1;
        public float MaxDamage = 1;
        public HashedString[] DamageTypes;

        #region Byte-flag Properties
        [HideInInspector]
        [SerializeField]
        byte SerializedFlags = 1;


        [PropertyTooltip("If an entity's Health component is disabled, will this object not damage them?")]
        [ShowInInspector]
        public bool HonorInvincibility
        {
            get { return (SerializedFlags & (byte)Flags.HonorInvincibiity) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)Flags.HonorInvincibiity;
                else SerializedFlags &= ((byte)Flags.HonorInvincibiity ^ 0xff);
            }
        }

        #if UNITY_EDITOR
        [Tooltip("Should we skip applying damage upon colliding with a valid target? WARNING: This is for edit-time debugging only and will be stripped in builds!")]
        [SerializeField]
        #pragma warning disable IDE0044 // Add readonly modifier
        [Sirenix.OdinInspector.SuffixLabel(" (Editor Only")]
        public bool SkipApplyDamage;
        #pragma warning restore IDE0044 // Add readonly modifier
        #endif

        [PropertyOrder(10)]
        [PropertyTooltip("If set, a raycast willbe performed from the position this object was first activated to the position it collided with something. If no other objects interrupted the line, the damage will be applied.")]
        [ShowInInspector]
        public bool ConfirmLos
        {
            get { return (SerializedFlags & (byte)Flags.ConfirmLos) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)Flags.ConfirmLos;
                else SerializedFlags &= ((byte)Flags.ConfirmLos ^ 0xff);
            }
        }
        #endregion

        [PropertyOrder(11)]
        [ShowIf("ConfirmLos")]
        [Indent(1)]
        public LayerMask LosMask;

        [PropertyOrder(12)]
        [Tooltip("An offset applied to the target when doing LoS check.")]
        [ShowIf("ConfirmLos")]
        [Indent]
        public Vector3 TargetOffset = Vector3.up * 0.5f;

        [Tooltip("Invoked when a collision with a valid, damageable objects is detected.")]
        public UnityEvent OnHit;
        [Tooltip("Invoked when a collision with a non-damageable object is detected.")]
        public UnityEvent OnInvalid;
        [Tooltip("Invoked when this trigger causes an entity to die.")]
        public AgentTargetEntityEvent OnKilledTarget;


        protected float LastTime;
        protected EntityRoot Root;
        protected Transform Trans;
        
        
        protected virtual void Awake()
        {
            Root = gameObject.GetEntityRoot();
            Trans = transform;
        }

        public virtual void Process(GameObject other)
        {
            ProcessInternal(other, Trans.position);
        }

        protected void ProcessInternal(GameObject other, Vector3 startPos)
        {
            if (ConfirmLos)
            {
                var dir = (TargetOffset + other.transform.position) - startPos;
                if (Physics.Raycast(startPos, dir, out RaycastHit hit, dir.magnitude, LosMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.gameObject != other)
                        return;
                }

            }

            DemandEntityRoot.Shared.Reset();
            GlobalMessagePump.Instance.ForwardDispatch(other, DemandEntityRoot.Shared);
            var target = DemandEntityRoot.Shared.Desired;
            if (target == null)
            {
                OnInvalid.Invoke();
                return;
            }

            if (Freq > 0)
            {
                float t = Time.time;
                if (t - LastTime < Freq) return;
                else LastTime = t;
            }

            #if UNITY_EDITOR
            //NOTE: This logic here is inverted because Unity is being a fuckshit and is getting things backwards
            //UPDATE: Aaaaaand now Unity is back to having a brain, or something. I dunno. Fuck it. Iguess it works again.
            //So I should porbaly remove these two lines but I don't fucking feel like it now okay. Fuck.
            if (!SkipApplyDamage)
            {
                var hp = target.FindComponentInEntity<Health>(true);
                bool notDead = !hp.IsDead;
                CombatCalculator.ProcessDirectDamage(Root, target, MinDamage, MaxDamage, DamageTypes, 1.0f, HonorInvincibility, this);
                if (notDead && hp.IsDead)
                    OnKilledTarget.Invoke(Root, target);
                OnHit.Invoke();
            }
            #else
            CombatCalculator.ProcessDirectDamage(Root, target, MinDamage, MaxDamage, DamageTypes, 1.0f, HonorInvincibility);
            OnHit.Invoke();
            #endif

        }
    }
}
