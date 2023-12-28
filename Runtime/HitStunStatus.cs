using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using Peg.Graphics;
using Peg.Messaging;
using Peg.Lib;
using Peg.Game.ConsumableResource;
using Peg;
using Peg.AutonomousEntities;

namespace DamageSystem
{
    /// <summary>
    /// Attach to an entity that can be stunned by a 'HitStunApplier' or by damage in general.
    /// </summary>
    public class HitStunStatus : LocalListenerMonoBehaviour, IStunnable
    {
        public enum KnockbackModes
        {
            DirectionOfHit,
            DirectionOfFacing,
        }

        public HitStunStatus Stunnable { get => this;}
        public EntityRoot Root { get => gameObject.GetEntityRoot(); }

        //[Tooltip("An optional component that, when supplied, will let the flicker know which sprites to activate. First Sprite is considered 'Side', second is 'North', third is 'South'.")]
        //public SpriteDirectionThreshold SpriteDirections;

        [Tooltip("The transform that is used to determine the facing direction of this entity.")]
        public Transform Trans;
        [Tooltip("An optional charactercontroller attached to this entity that must be disabled in order to make Unity not shit the bed when applying forces to the rigidbody attached to the same object.")]
        public CharacterController Controller;
        [Tooltip("The rigidbody that hitback forces will be applied to.")]
        public Rigidbody Body;
        public Behaviour Input;
        public Behaviour Mover;

        [Space(12)]
        public float FlickerTime = 0.75f;
        [HideIf("FlickerOnce", true)]
        [HideIf("FlickerTime", 0.0f)]
        [Indent(1)]
        public float FlickerRate = 0.05f;
        [HideIf("FlickerTime", 0.0f)]
        [Indent(1)]
        public bool FlickerOnce;
        //public FlickerMode Mode;

        public float HitStunTime = 0;
        [Indent(1)]
        [HideIf("HitStunTime", 0.0f)]
        public KnockbackModes Mode;

        [Indent(1)]
        [HideIf("HitStunTime", 0.0f)]
        [Tooltip("This is a knockback force that is applied in 'aim-space' relative to this entity.")]
        public Vector3 KnockbackForce;

        [Indent(1)]
        [HideIf("HitStunTime", 0.0f)]
        [Tooltip("When DirectionOfFacing is used for the knockback mode, the directional vector must be calculated from the direction of the contact point. This vector is then used to scale that direction on each axis - this way you can exclude or amplify specfic axis if needed.")]
        public Vector3 DirectionScaling = new Vector3(1, 0, 1);

        [Tooltip("The time in seconds to play the hitstun animation if any. Leave 0 if no animation is to be played.")]
        public float AnimationTime = 0;
        [HideIf("AnimationTime", 0.0f)]
        [Indent(1)]
        public bool PlayAnimIfDead = false;
        [HideIf("AnimationTime", 0.0f)]
        [Indent(1)]
        public AnimatorEx Animator;
        [HideIf("AnimationTime", 0.0f)]
        [Indent(1)]
        public HashedString Animation;

        //public Color[] ColorCycle;
        [Space(12)]
        [FoldoutGroup("Events")]
        public UnityEvent OnFlickerStart;
        [FoldoutGroup("Events")]
        public UnityEvent OnFlickerEnd;
        [FoldoutGroup("Events")]
        public UnityEvent OnHitStunStart;
        [FoldoutGroup("Events")]
        public UnityEvent OnHitStunEnd;

        [Space(12)]
        public SpriteRenderer[] Sprites;
        public SpriteRenderer[] HitFlashes;

        Coroutine Flasher;
        bool OldGravity;
        //Color[] DefaultColors;

        public enum FlickerMode
        {
            Flicker,
            Color,
        }


        void Awake()
        {
            DispatchRoot.AddLocalListener<HealthLostEvent>(HandleDamaged);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            EndEffects();
            Flasher = null;
        }

        protected override void OnDestroy()
        {
            DispatchRoot.RemoveLocalListener<HealthLostEvent>(HandleDamaged);
            base.OnDestroy();
        }

        void HandleDamaged(HealthLostEvent msg)
        {
            if (msg.Health.IsDead) return;

            BeginDamageEffect(msg.Health);
        }

        public void EndEffects()
        {
            if (Sprites.Length > 0)
            {
                for (int i = 0; i < Sprites.Length; i++)
                    Sprites[i].enabled = true;
               
            }
            for (int i = 0; i < HitFlashes.Length; i++)
                HitFlashes[i].enabled = false;
        }
        
        /// <summary>
        /// Starts the flicker effect without applying knockback.
        /// </summary>
        public void BeginFlickerEffect(Health health)
        {
            if (Flasher == null && isActiveAndEnabled)
                Flasher = StartCoroutine(StunEffect(health, false, false, Vector3.zero));
        }

        /// <summary>
        /// Starts the flash coroutine for damage effects including the knockback.
        /// </summary>
        public void BeginDamageEffect(Health health)
        {
            //TODO: Apply physical force and disable player controls for a split-second
            if (Flasher == null && isActiveAndEnabled)
                Flasher = StartCoroutine(StunEffect(health, true, true, Vector3.zero));
        }

        /// <summary>
        /// Starts the flash coroutine for damage effects including the knockback.
        /// </summary>
        public void BeginDamageEffect(Health health, Vector3 hitPoint)
        {
            //TODO: Apply physical force and disable player controls for a split-second
            if (Flasher == null && isActiveAndEnabled)
                Flasher = StartCoroutine(StunEffect(health, true, true, hitPoint));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="health"></param>
        /// <param name="knockBack"></param>
        /// <returns></returns>
        IEnumerator StunEffect(Health health, bool knockBack, bool animate, Vector3 hitPoint)
        {
            var waitTime = CoroutineWaitFactory.RequestWait(FlickerRate);
            var start = Time.timeSinceLevelLoadAsDouble;

            if(knockBack && HitStunTime > 0)
            {
                OldGravity = Body.useGravity;
                OnHitStunStart.Invoke();
                if(Input != null)
                    Input.enabled = false;
                if(KnockbackForce.sqrMagnitude > 0)
                {
                    var trans = Trans;
                    var pos = trans.position;
                    Vector3 vel;
                    if (Mode == KnockbackModes.DirectionOfFacing)
                    {
                        var offset = MathUtils.ForwardSpaceOffset(pos, trans.forward, KnockbackForce);
                        vel = pos - offset;
                    }
                    else
                    {
                        var offset = MathUtils.ForwardSpaceOffset(pos, Vector3.Scale((hitPoint - pos).normalized, DirectionScaling), KnockbackForce);
                        vel = pos - offset;
                    }
                    if(Mover != null)
                        Mover.enabled = false;
                    if (Controller != null)
                        Controller.enabled = false;
                    Body.isKinematic = false;
                    Body.useGravity = true;
                    Body.AddForce(vel, ForceMode.VelocityChange);
                }

                Invoke("ResetInput", HitStunTime);
            }
            
            OnFlickerStart.Invoke();

            if (animate && AnimationTime > 0)
            {
                if ((PlayAnimIfDead || !health.IsDead) && Animator != null)
                    Animator.ExecuteAnimation(Animation.Hash, 0, AnimationTime, true, AnimatorEx.PlayMode.Interrupt);
            }



            if (FlickerOnce)
            {
                for(int x = 0; x < 2; x++)
                {
                    if (health.IsDead) break;
                    if (Sprites.Length > 0)
                    {
                        for (int i = 0; i < Sprites.Length; i++)
                            Sprites[i].enabled = !Sprites[i].enabled;
                    }
                    for (int i = 0; i < HitFlashes.Length; i++)
                        HitFlashes[i].enabled = !HitFlashes[i].enabled;

                    yield return waitTime;
                }
            }
            else
            { 
                while (Time.timeSinceLevelLoadAsDouble - start < FlickerTime)
                {
                    if (health.IsDead) break;
                    if (Sprites.Length > 0)
                    {
                        for (int i = 0; i < Sprites.Length; i++)
                            Sprites[i].enabled = !Sprites[i].enabled;

                    }
                    for (int i = 0; i < HitFlashes.Length; i++)
                        HitFlashes[i].enabled = !HitFlashes[i].enabled;

                    yield return waitTime;
                }
            }
            
            OnFlickerEnd.Invoke();
            EndEffects();
            Flasher = null;
        }
        

        void ResetInput()
        {
            Body.isKinematic = true;
            Body.useGravity = OldGravity;
            if(Mover != null)
                Mover.enabled = true;
            if (Controller != null)
                Controller.enabled = true;
            if (Input != null)
                Input.enabled = true;
            OnHitStunEnd.Invoke();
        }
    }


    /// <summary>
    /// An interface that can be used to expose a HitStunStatus object anywhere on an Enitty hierarchy.
    /// </summary>
    public interface IStunnable
    {
        HitStunStatus Stunnable { get; }
        EntityRoot Root { get; }
    }
}
