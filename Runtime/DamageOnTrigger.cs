using UnityEngine;

namespace DamageSystem
{
    /// <summary>
    /// Uses the combat calculator to deal damage upon
    /// triggering a collider attached to this GameObject.
    /// </summary>
    public sealed class DamageOnTrigger : DamageHandler
    {
        public enum DamageTrigger
        {
            Disabled = 0,
            Enter = 1,
            Stay = 2,
            All = Enter | Stay,
        }

        public DamageTrigger Trigger = DamageTrigger.Enter;


        Vector3 StartPos;

        
        

        void OnEnable()
        {
            StartPos = transform.position;
        }

        #if TOOLBOX_2DCOLLIDER
        void OnTriggerEnter2D(Collider2D other)
        {
            if ((Trigger & DamageTrigger.Enter) != 0)
                Process(other.gameObject);
        }
        private void OnTriggerStay2D(Collider2D other)
        {
            if ((Trigger & DamageTrigger.Stay) != 0)
                Process(other.gameObject);
        }
        #else
        void OnTriggerEnter(Collider other)
        {
            if ((Trigger & DamageTrigger.Enter) != 0)
                Process(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if ((Trigger & DamageTrigger.Stay) != 0)
                Process(other.gameObject);
        }
        #endif

        public override void Process(GameObject other)
        {
            ProcessInternal(other, StartPos);
        }
    }
}
