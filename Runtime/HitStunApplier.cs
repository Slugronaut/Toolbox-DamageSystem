using Sirenix.OdinInspector;
using Toolbox;
using Toolbox.Game;
using Toolbox.Graphics;
using ToolFx;
using UnityEngine;

namespace DamageSystem
{
    /// <summary>
    /// Attached to a projectile and used as a way to apply a hit-stun to living entity that is struck.
    /// </summary>
    [RequireComponent(typeof(Projectile))]
    public class HitStunApplier : MonoBehaviour
    {
        public bool OverrideKnockbackForce;
        [ShowIf("OverrideKnockbackForce")]
        [Indent(1)]
        public Vector3 KnockbackForce;

        public bool OverrideStunTime;
        [ShowIf("OverrideStunTime")]
        [Indent(1)]
        public float StunTime;

        public bool OverrideAnimation;
        [ShowIf("OverrideAnimation")]
        [Tooltip("The time in seconds to play the hitstun animation if any. Leave 0 if no animation is to be played.")]
        public float AnimationTime = 0;
        [HideIf("AnimationTime", 0.0f)]
        [Indent(1)]
        public HashedString Animation;


        public void Awake()
        {
            GetComponent<Projectile>().OnCollided.AddListener(Handle);
        }

        public void OnDestroy()
        {
            GetComponent<Projectile>().OnCollided.RemoveListener(Handle);
        }

        public void Handle(Projectile proj, ITool tool, Collider col)
        {
            var stunner = col.GetComponent<IStunnable>();
            if (stunner == null) return;

            var root = stunner.Root;

            var hp = Health.IsKillable(root);
            if(hp != null)
            {
                var hs = root.FindComponentInEntity<HitStunStatus>(true);
                if (hs != null)
                {
                    Vector3 oldForce = hs.KnockbackForce;
                    float oldStunTime = hs.HitStunTime;
                    float oldAnimTime = hs.AnimationTime;
                    string oldAnim = hs.Animation.Value;

                    if (OverrideKnockbackForce)
                        hs.KnockbackForce = KnockbackForce;
                    if (OverrideStunTime)
                        hs.HitStunTime = StunTime;
                    if (OverrideAnimation && AnimationTime > 0)
                    {
                        hs.AnimationTime = AnimationTime;
                        hs.Animation.Value = Animation.Value;
                    }

                    hs.BeginDamageEffect(hp, col.ClosestPointOnBounds(proj.transform.position));
                    

                    hs.KnockbackForce = oldForce;
                    hs.HitStunTime = oldStunTime;
                }

            }
        }


    }
}
