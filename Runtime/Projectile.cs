using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;
using Sirenix.OdinInspector;
using Peg.Graphics;
using ToolFx;
using UnityEngine.Assertions;
using Peg.Messaging;
using Peg.Lazarus;
using Peg.AutonomousEntities;

namespace DamageSystem
{
    /// <summary>
    /// 
    /// </summary>
    [RequireComponent(typeof(EntityRoot))]
    [RequireComponent(typeof(LocalMessageDispatch))]
    public sealed class Projectile : MonoBehaviour, ILocalDispatchListener
    {
        /// <summary>
        /// Flags for serialized and inspectable data.
        /// </summary>
        [Flags]
        public enum ProjectileFlags
        {
            DisableOnCull           = 1 << 0,
            NoCollisionEvents       = 1 << 1,
            DisallowAttackingSelf   = 1 << 2,
            UseExternalCallback     = 1 << 3,
            SelfDestruct            = 1 << 4,
            AdjustableHeading       = 1 << 5,
            DestroyWithParent       = 1 << 6,
        }

        /// <summary>
        /// Flags for runtime generated data.
        /// </summary>
        [Flags]
        public enum ProjectileFlags2
        {
            TimeoutStarted = 1 << 0,
        }


        #region Public Inner Flags
        /// <summary>
        /// Flags that are serialized with the object.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private byte SerializedFlags = 112; //default value sets SelfDestruct, AdjustableHeading, and DestroyWithParent
        #endregion


        #region Hidden Inner Flags
        /// <summary>
        /// Flags that are used at runtime only.
        /// </summary>
        private byte RuntimeFlags;

        public bool TimeoutStarted
        {
            get { return (RuntimeFlags & (byte)ProjectileFlags2.TimeoutStarted) != 0; }
            set
            {
                if (value) RuntimeFlags |= (byte)ProjectileFlags2.TimeoutStarted;
                else RuntimeFlags &= ((byte)ProjectileFlags2.TimeoutStarted ^ 0xff);
            }
        }
        #endregion


        #region Projectile Properties
        [FoldoutGroup("Projectile Properties", -2)]
        [Tooltip("Max time the projectile is allowed to exist before being removed from the world.")]
        public float Lifetime = 3;
        [FoldoutGroup("Projectile Properties", -2)]
        [Tooltip("The number of times this projectile can collide with something before it is destroyed.")]
        public short Penetrations = 0;
        [ShowIf("IsPenetrating")]
        [Indent]
        [FoldoutGroup("Projectile Properties", -2)]
        [Tooltip("The layers that penetration can occur on. This should be a subset of the collisions this projectile can collide with set in the collision matrix.")]
        public LayerMask PenetrateLayers;
        [ShowIf("IsPenetrating")]
        [Indent]
        [FoldoutGroup("Projectile Properties", -2)]
        [Tooltip("These layers never increment penetration counters. They don't not cause projectiles to disappear when touched.")]
        public LayerMask PenetrateIgnoreLayers;

        /// <summary>
        /// Used by the inspector for property hiding.
        /// </summary>
        bool IsPenetrating {get { return Penetrations > 0; } }

        FlickerRenderers Flickerer;
        #endregion


        #region Projectile Flags
        /// <summary>
        /// TODO: These seven values should be converted to proerties that use the 'InnerFlags' field and a bitmask!
        /// </summary>
        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("Does this projectile deactivate once it is no longer visible.")]
        [ShowInInspector]
        public bool DisableOnCull
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.DisableOnCull) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.DisableOnCull;
                else SerializedFlags &= ((byte)ProjectileFlags.DisableOnCull ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("If set, this projectile won't perform any work when a collision or trigger is detected. Useful for dummy projectiles like shields and special-effects.")]
        [ShowInInspector]
        public bool NoCollisionEvents
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.NoCollisionEvents) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.NoCollisionEvents;
                else SerializedFlags &= ((byte)ProjectileFlags.NoCollisionEvents ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("Can be set to avoid some kinds of bugs.")]
        [ShowInInspector]
        public bool DisallowAttackingSelf
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.DisallowAttackingSelf) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.DisallowAttackingSelf;
                else SerializedFlags &= ((byte)ProjectileFlags.DisallowAttackingSelf ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("Can be used by the combat calculator to signal that it shouldn't register a callback to the projectile. Useful when the projectile should process its collision independantly.")]
        [ShowInInspector]
        public bool UseExternalCallback
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.UseExternalCallback) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.UseExternalCallback;
                else SerializedFlags &= ((byte)ProjectileFlags.UseExternalCallback ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("Should this porjectile self-destruct on collision? Set this to false if you need it to stick around for a while - just be sure you handle the relenquishing elsewhere.")]
        [ShowInInspector]
        public bool SelfDestruct
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.SelfDestruct) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.SelfDestruct;
                else SerializedFlags &= ((byte)ProjectileFlags.SelfDestruct ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("Can this projectile have its heading altered?")]
        [ShowInInspector]
        public bool AdjustableHeading
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.AdjustableHeading) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.AdjustableHeading;
                else SerializedFlags &= ((byte)ProjectileFlags.AdjustableHeading ^ 0xff);
            }
        }

        [FoldoutGroup("Projectile Flags", -1)]
        [PropertyTooltip("If the parent is disabled, this projectile self-destructs.")]
        [ShowInInspector]
        public bool DestroyWithParent
        {
            get { return (SerializedFlags & (byte)ProjectileFlags.DestroyWithParent) != 0; }
            set
            {
                if (value) SerializedFlags |= (byte)ProjectileFlags.DestroyWithParent;
                else SerializedFlags &= ((byte)ProjectileFlags.DestroyWithParent ^ 0xff);
            }
        }
        #endregion

        [FoldoutGroup("Events")]
        public UnityEvent OnFired;
        [FoldoutGroup("Events", 1)]
        public UnityEvent OnTerminated;
        [FoldoutGroup("Events", 1)]
        public ProjectileCollideEvent OnCollided;
        [FoldoutGroup("Events", 1)]
        public UnityEvent OnKilledTarget;


        public DamageOnTrigger DamageTrigger
        {
            get;
            private set;
        }

        public Rigidbody Body { get; private set; }
        public EntityRoot Root { get; private set; }
        public ITool Source;

        #if TOOLBOX_2DCOLLIDER
        [Serializable]
        public class ProjectileCollideEvent : UnityEvent<Projectile, ITool, Collider2D> { }
        #else
        [Serializable]
        public class ProjectileCollideEvent : UnityEvent<Projectile, ITool, Collider> { }
        #endif

        /// <summary>
        /// The enitity that generated this projectile.
        /// </summary>
        //[HideInInspector]
        //public UnityEngine.Object Source;

        TrailRenderer[] Trails;
        ParticleSystem[] Particles;
        
        short DefaultPenetrations;
        int PenCount = 0;

#if TOOLBOX_2DCOLLIDER
        public delegate void CollideCallback(Projectile projectile,  ITool emitter, Collider2D target);
#else
        public delegate void CollideCallback(Projectile projectile,  ITool emitter, Collider target);
#endif
        LinkedList<CollideCallback> StrikeCallbacks = new LinkedList<CollideCallback>();
        LinkedList<CollideCallback> DespawnCallbacks = new LinkedList<CollideCallback>();

        LocalMessageDispatch _DispatchRoot;
        public LocalMessageDispatch DispatchRoot
        {
            get
            {
                if (_DispatchRoot == null)
                {
                    _DispatchRoot = gameObject.FindComponentInEntity<LocalMessageDispatch>();
                    if (_DispatchRoot == null) throw new UnityException("The component '" + this.GetType().Name + "' attached to '" + gameObject.name + "' requires there to be a LocalMessageDispatch attached to its autonomous entity hierarchy.");
                }
                return _DispatchRoot;
            }
        }



        void Awake()
        {
            if(Root == null) Root = gameObject.GetEntityRoot();
            if(Body == null) Body = GetComponent<Rigidbody>();
            DispatchRoot.AddLocalListener<DemandProjectileComponent>(GetMe);
            Trails = GetComponentsInChildren<TrailRenderer>();
            Particles = GetComponentsInChildren<ParticleSystem>();
            DefaultPenetrations = Penetrations;
            Flickerer = GetComponent<FlickerRenderers>();
            DamageTrigger = GetComponent<DamageOnTrigger>();
            if (Flickerer != null)
                Flickerer.OnFlickerEnd += HandleFlickerEnd;
        }

        void OnDestroy()
        {
            _DispatchRoot = null;
            if (Flickerer != null)
            {
                Flickerer.OnFlickerEnd -= HandleFlickerEnd;
                Flickerer = null;
            }
            DispatchRoot.RemoveLocalListener<DemandProjectileComponent>(GetMe);
        }

        void OnEnable()
        {
            if (Trails.Length > 0)
            {
                //we'll have to delay the clearning of the trails due to race conditions internal to Unity
                for (int i = 0; i < Trails.Length; i++)
                    Trails[i].emitting = false;
                Invoke("DelayedTrailsStart", 0.01f);
            }
            for (int i = 0; i < Particles.Length; i++)
                Particles[i].Clear(true);

            PenCount = 0;
            Invoke("LifeTimeout", Lifetime);
            TimeoutStarted = false;
        }

        void DelayedTrailsStart()
        {
            //Physics.SyncTransforms(); //need this to make trails clear properly
            for (int i = 0; i < Trails.Length; i++)
            {
                Trails[i].Clear();
                Trails[i].emitting = true;
            }
        }

        void OnDisable()
        {
            StrikeCallbacks.Clear();
            DespawnCallbacks.Clear();
            CancelInvoke();
        }

        #if TOOLBOX_2DCOLLIDER
        void OnTriggerEnter2D(Collider2D col)
        {
            #if UNITY_EDITOR
            Assert.IsNotNull(col, "The collider is null in OnTriggerEnter2D?");
            #endif
            if (!NoCollisionEvents)
                ProcessCollision(col, transform.position);
        }
        #else
        /*
        void OnCollisionEnter(Collision col)
        {
            if (!NoCollisionEvents)
                ProcessCollision(col.collider, col.contacts[0].point);
        }
        */

        void OnTriggerEnter(Collider col)
        {
            #if UNITY_EDITOR
            Assert.IsNotNull(col, "The collider is null in OnTriggerEnter?");
            #endif
            if(!NoCollisionEvents)
                ProcessCollision(col, transform.position);
        }
        #endif

        #if TOOLBOX_2DCOLLIDER
        void ProcessCollision(Collider2D col, Vector3 contact)
        #else
        void ProcessCollision(Collider col, Vector3 contact)
        #endif
        {
            if (UseExternalCallback)
                return;

            var cc = StrikeCallbacks.First;
            while(cc != null)
            {
                cc.Value(this, Source, col);
                cc = cc.Next;
            }
            //foreach (var call in StrikeCallbacks)
            //    call(this, Source, col);

            OnCollided.Invoke(this, Source, col);
            OnTerminated.Invoke();

            int layer = col.gameObject.layer;
            if ((PenetrateLayers.value & (1 << layer)) > 0)
            {
                PenCount++;
                if (PenCount > Penetrations)
                   Lazarus.Instance.RelenquishToPool(gameObject);
            }
            else if ((PenetrateIgnoreLayers.value & (1 << layer)) > 0)
                return;
            else
            {
                foreach (var call in DespawnCallbacks)
                    call(this, Source, col);
                Lazarus.Instance.RelenquishToPool(gameObject);
            }
        }

        /// <summary>
        /// Adds a listener callback that is invoked the next time this projectile collides with something.
        /// This callback will be removed automatically after this projectile has been disabled.
        /// Use this to dynamically add one-shot callbacks that would be too expensive to add using
        /// traditional delegate combining or unity events.
        /// </summary>
        public void AddStrikeCallback(CollideCallback callback)
        {
            //Debug.Log("Strike Callback added");
            StrikeCallbacks.AddLast(callback);
        }

        /// <summary>
        /// Adds a listener callback that is invoked the next time this projectile collides with something that causes
        /// this projectile to despawn. Note that the dofference between this and a Strike Callback is that this isn't
        /// called when penetrating multiple objects.
        /// This callback will be removed automatically after this projectile has been disabled.
        /// Use this to dynamically add one-shot callbacks that would be too expensive to add using
        /// traditional delegate combining or unity events.
        /// </summary>
        public void AddDespawnCallback(CollideCallback callback)
        {
            //Debug.Log("Despawn Callback added");
            StrikeCallbacks.AddLast(callback);
        }

        /// <summary>
        /// Called by attacking code when this projectile has been fully initialized
        /// and is now on its way through the world.
        /// </summary>
        public void Fired()
        {
            OnFired.Invoke();
        }

        void GetMe(DemandProjectileComponent msg)
        {
            msg.Respond(this);
        }

        /// <summary>
        /// Resets this projectile internal state.
        /// This is needed due to weapon's ability to override this value which then remains
        /// because projectiles are re-used from pools.
        /// </summary>
        public void ResetToDefault()
        {
            Penetrations = DefaultPenetrations;
        }

        /// <summary>
        /// Used to force this projectile to self-destruct and return to its spawning pool.
        /// </summary>
        public void LifeTimeout()
        {
            //so apparently I need to check if I am null now - horray for switching scene
            if (this == null || gameObject == null) return;

            if (!TimeoutStarted && isActiveAndEnabled)
            {
                TimeoutStarted = true;
                if (Flickerer == null) Lazarus.Instance.RelenquishToPool(gameObject);
                else
                {
                    //Flickerer.FlickerTime = 2;
                    Flickerer.PerformOp();
                }
            }
        }

        /// <summary>
        /// Immediately returns this projectile to its pool.
        /// </summary>
        public void KillProjectile()
        {
            if (isActiveAndEnabled)
            {
                OnTerminated.Invoke();
                Lazarus.Instance.RelenquishToPool(gameObject);
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        void HandleFlickerEnd()
        {
            Lazarus.Instance.RelenquishToPool(gameObject);
        }
        
    }

    /// <summary>
    /// 
    /// </summary>
    public class DemandProjectileComponent : SimpleDemand<Projectile>
    {
        public DemandProjectileComponent() { }
    }
}