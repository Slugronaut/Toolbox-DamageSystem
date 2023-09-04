using UnityEngine;
using Peg;
using System;

namespace DamageSystem
{
    /// <summary>
    /// Attach to an entity to mark them with weaknesses and immunitys to weapons
    /// that have any of the named damage types.
    /// </summary>
    public class WeaknessAndImmunity : MonoBehaviour
    {
        [Serializable]
        public class Pair
        {
            public HashedString DamageTypeName;
            public float Multiplier;
        }

        public Pair[] Weakness;
        public Pair[] Immunities;
    }
}
