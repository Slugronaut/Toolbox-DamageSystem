using UnityEngine;
using Peg;
using Peg.Game;
using System;
using Random = UnityEngine.Random;
using Peg.AutoCreate;

namespace DamageSystem
{
    /// <summary>
    /// Core combat system for DGSP.
    /// </summary>
    [AutoCreate(CreationActions.DeserializeSingletonData)]
    public class CombatCalculator
    {
        public static int PhysicalAttack = HashedString.StringToHash("Physical Attack");
        public static int MagicAttack = HashedString.StringToHash("Magic Attack");
        public static float MaxStat = 20;

        public bool FriendlyFire = false;
        public bool EveryoneIsGod = false;
        public bool ReportDamage = false;
        public bool ShowFloatingNumbers = true;
        public float AttackScale = 1.0f;
        public float DefenseScale = 1.0f;
        public float MinDamage = 1;
        public float MinHeal = 1;
        [Tooltip("The fudge factor to use when re-checking the distance for an attack.")]
        public float RecheckFudge = 0.2f;
        private static readonly HashedString hashedString = new HashedString("Root Stats");
        public HashedString BaseStatsHash = hashedString;
        public HashedString DerivedStatsHash = new("Derived Stats");

        public static CombatCalculator Instance { get; private set; }
        
        //public static DealDamageCmd DealDamage = new DealDamageCmd(null, null, 0, 0, null);
        public static ChangeHealthCmd HealthChangeCmd = new(null, 0);
        
        public static bool DisplayTextScroll
        {
            get { return Instance.ShowFloatingNumbers; }
        }

        /// <summary>
        /// 
        /// </summary>
        void AutoAwake()
        {
            Instance = this;
        }

        /// <summary>
        /// Used to directly apply damage to an entity.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="target"></param>
        /// <param name="damageMin"></param>
        /// <param name="damageMax"></param>
        /// <param name="damageScale"></param>
        public static void ProcessDirectDamage(EntityRoot agent, EntityRoot target, float damageMin, float damageMax, HashedString[] damageTypes, float damageScale = 1.0f, bool honorInvincibilityFrames = true, Behaviour damageObject = null)
        {
            if (target == null) return;
            IStats targetStats = null;// target.FindComponentInEntity<IStats>(true);

            //now that we finally have confirmed our target, we can make sure we don't attack if they are dead
            Health targetHealth = target.FindComponentInEntity<Health>(true);
            if (targetHealth == null || targetHealth.IsDead)
                return;

            if(honorInvincibilityFrames && !targetHealth.isActiveAndEnabled)
                return;

            float fDamage = damageScale * (float)((Instance.EveryoneIsGod) ?
                0.0f : CalculateDamage(agent, target, null, targetStats, damageMin, damageMax, damageTypes));

            int finalDamage = Mathf.CeilToInt(fDamage);
            GlobalMessagePump.Instance.ForwardDispatch(target.gameObject, HealthChangeCmd.ChangeValues((agent == null) ? null : agent.gameObject, finalDamage, honorInvincibilityFrames));
            if (Instance.ReportDamage) Debug.Log(((agent == null) ? "Unknown agent" : agent.Name) + " did " + HealthChangeCmd.Change + " direct damage to " + target.Name);

        }

        /// <summary>
        /// Instantly kills and entity.
        /// </summary>
        public static void ForceKill(EntityRoot agent, EntityRoot target, bool honorInvincibilityFrames = false)
        {
            if (target == null) return;
            //IStats targetStats = null;//target.FindComponentInEntity<IStats>(true);

            //now that we finally have confirmed our target, we can make sure we don't attack if they are dead
            Health targetHealth = target.FindComponentInEntity<Health>(true);
            if (targetHealth == null || targetHealth.IsDead)
                return;

            if (honorInvincibilityFrames && !targetHealth.isActiveAndEnabled)
                return;

            GlobalMessagePump.Instance.ForwardDispatch(target.gameObject, KillEntityForcedCmd.Shared.Change((agent == null) ? null : agent.gameObject, target.gameObject));
            if (Instance.ReportDamage) Debug.Log(((agent == null) ? "Unknown agent" : agent.Name) + " did " + HealthChangeCmd.Change + " direct damage to " + target.Name);

        }

        /// <summary>
        /// Calculate healing output between two entities.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="target"></param>
        /// <param name="agentStats"></param>
        /// <param name="targetStats"></param>
        /// <param name="minHeal"></param>
        /// <param name="maxHeal"></param>
        /// <param name="damageTypes"></param>
        public static float CalculateHealing(EntityRoot agent, EntityRoot target, IStats agentStats, IStats targetStats, float minHeal, float maxHeal, HashedString[] healTypes)
        {
            return Random.Range(minHeal, maxHeal);
        }

        /// <summary>
        /// Calculates damage output between two entities.
        /// </summary>
        /// <param name="agentStats"></param>
        /// <param name="targetStats"></param>
        /// <param name="agentWeapon"></param>
        /// <returns></returns>
        public static float CalculateDamage(EntityRoot agent, EntityRoot target, IStats agentStats, IStats targetStats, float minDamage, float maxDamage, HashedString[] damageTypes)
        {
            float wepDamage = 0;

            if (agentStats == null || targetStats == null)
                wepDamage = UnityEngine.Random.Range(minDamage, maxDamage);
            else
            {
                //float agentBasePow;

                //float agentPow;
                //float targetDef;
                /*
                //IMPORTANT: We only consider this magical if it is the FIRST damage type!!
                if (damageTypes != null && damageTypes.Length > 0 && damageTypes[0].Hash == MagicAttack)
                {
                    //this is a magical attack
                    //agentBasePow = agentStats.GetLocalEffectAt(MPowerStat, 0);
                    agentPow = agentStats.QueryStat(DerivedStats.MagicPower) * Instance.AttackScale;
                    targetDef = targetStats.QueryStat(DerivedStats.MagicDefense) * Instance.DefenseScale;
                }
                else
                {
                    //all others are physical
                    //agentBasePow = agentStats.GetLocalEffectAt(PowerStat, 0);
                    agentPow = agentStats.QueryStat(DerivedStats.Power) * Instance.AttackScale;
                    targetDef = targetStats.QueryStat(DerivedStats.Defense) * Instance.DefenseScale;
                }
                */

                float damageMul = 1;
                /*
                if (agentPow > targetDef)
                    damageMul += (agentPow - targetDef) / MaxStat;
                else if (targetDef > agentPow)
                    damageMul -= (targetDef - agentPow) / MaxStat;
                */
                wepDamage = UnityEngine.Random.Range(minDamage, maxDamage);
                wepDamage *= damageMul;
            }


            //CRIT CHANCE - small percentage chance based on power/magic power & agility - reduced by defense/magic defense & durability
            /*
            //crit (kinda broken, needs work)
            float pen = (agentPow - targetDef) / (MaxStat * 2);
            if (pen > Random.Range(0, 1))
                wepDamage *= 2;
            }
            */

            //process weakness and resistences
            var wai = target.FindComponentInEntity<WeaknessAndImmunity>(true);
            if(wai != null)
            {
                for(int i = 0; i < wai.Weakness.Length; i++)
                {
                    var hash = wai.Weakness[i].DamageTypeName.Hash;
                    int index = Array.IndexOf(damageTypes, hash);
                    if (index < 0 || hash != damageTypes[index].Hash) continue;
                    else wepDamage *= wai.Weakness[i].Multiplier;
                }

                for (int i = 0; i < wai.Immunities.Length; i++)
                {
                    var hash = wai.Immunities[i].DamageTypeName.Hash;
                    int index = Array.IndexOf(damageTypes, hash);
                    if (index < 0 || hash != damageTypes[index].Hash) continue;
                    else wepDamage /= wai.Immunities[i].Multiplier;
                }
            }

            //clamp minimum healing
            if (minDamage < 0 && wepDamage > -Instance.MinHeal)
                wepDamage = -Instance.MinHeal;
            //clamp minimum damage
            else if (minDamage >= 0 && wepDamage < Instance.MinDamage)
                wepDamage = Instance.MinDamage;

            return wepDamage;
        }
        
    }
    
}
