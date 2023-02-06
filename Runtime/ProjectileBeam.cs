using UnityEngine;
using Toolbox;
using UnityEngine.Events;
using System;
using ToolFx;
using Sirenix.OdinInspector;
using Toolbox.Lazarus;

namespace DamageSystem
{
    /// <summary>
    /// 
    /// </summary>
    [RequireComponent(typeof(EntityRoot))]
    public sealed class ProjectileBeam : MonoBehaviour
    {
        public LineRenderer LineRend;
        [Tooltip("Optional transform that can be scaled, positioned, and oriented with the laser beam.")]
        public Transform Optional;
        [Indent]
        [ShowIf("IsOptionalValid")]
        public float HeightOffset = -10.25f;
        [Indent]
        [ShowIf("IsOptionalValid")]
        public float Width = 1;
        [Tooltip("Layers that impede motion of the laser beam.")]
        public LayerMask BlockingLayers;
        [Tooltip("Layers that are processed for damage.")]
        public LayerMask StrikeLayers;
        [Tooltip("The maximum number of objects to ray test against.")]
        public int MaxHits;
        [Tooltip("How quickly the end of the beam travels from the source when un-impededed.")]
        public float Speed;
        [Tooltip("The maximum length the beam can extend.")]
        public float MaxLength;
        [HideInInspector]
        public ITool Source;

        bool IsOptionalValid => Optional != null;

        [FoldoutGroup("Events")]
        public BeamStrikeEvent OnStrike;
        [FoldoutGroup("Events")]
        public UnityEvent OnBlocked;
        [FoldoutGroup("Events")]
        public UnityEvent OnKilledTarget;


        [Serializable]
        public class BeamStrikeEvent : UnityEvent<GameObject> { }

        float StartTime;
        float BeamLength;
        Transform SourceTrans;
        Transform MyTrans;
        DamageHandler Damage;
        Vector3[] Poses;
        

        private void Awake()
        {
            Damage = GetComponent<DamageHandler>();
            Poses = new Vector3[2];
        }

        void OnEnable()
        {
            if (Source != null)
            {
                var go = Source.gameObject;
                if (go != null)
                    SourceTrans = Source.gameObject.transform;
            }
            MyTrans = transform;
            BeamLength = 0;
            StartTime = Time.time;
            Killing = false;
        }

        /// <summary>
        /// Called by attacking code when this projectile has been fully initialized
        /// and is now on its way through the world.
        /// </summary>
        public void Fired()
        {
            
        }

        public void Stopped()
        {

        }

        /// <summary>
        /// Resets this projectile internal state.
        /// This is needed due to weapon's ability to override this value which then remains
        /// because projectiles are re-used from pools.
        /// </summary>
        public void ResetToDefault()
        {
            
        }

        bool Killing = false;

        /// <summary>
        /// Relenquished this beam object.
        /// </summary>
        public void KillBeam()
        {
            //so apparently I need to check if I am null now - horray for switching scene
            if (this == null || gameObject == null) return;
            Killing = true;
            //if(isActiveAndEnabled)
            //    Lazarus.RelenquishToPool(gameObject);
        }

        private void Update()
        {
            if (!Killing && (SourceTrans == null || !Source.gameObject.activeInHierarchy))
            {
                KillBeam();
                return;
            }


            if (Killing)
                ShrinkBeam();
            else GrowBeam();

            //update optional transform
            if (Optional != null && LineRend != null && LineRend.positionCount == 2)
                UpdateOptionalTransform(Optional, LineRend.GetPosition(0), LineRend.GetPosition(1));
        }

        void UpdateOptionalTransform(Transform trans, Vector3 p1, Vector3 p2)
        {
            var diff = (p2 - p1);
            var dist = diff.magnitude;

            trans.position = p2 - (diff * 0.5f) + (Vector3.up * HeightOffset);
            if(dist != 0) trans.rotation = Quaternion.LookRotation(diff.normalized);
            var localScale = trans.localScale;
            trans.localScale = new Vector3(Width, localScale.y, dist);
        }

        Vector3 StaticEjectPos;
        Vector3 EjectEnd;
        Vector3 EjectForward;
        Vector3 EjectStart;
        /// <summary>
        /// 
        /// </summary>
        void ShrinkBeam()
        {
            var start = EjectStart;
            var dir = EjectForward;
            Vector3 end = EjectEnd;

            float beamLen = (end - start).magnitude;


            bool allowGrowth = true;
            LayerMask masks = BlockingLayers | StrikeLayers;
            //Sadly, we can't use the non-alloc version because we need to sort and it will
            //invalidate the buffer due to excess values in the array.
            var hits = Physics.RaycastAll(start, dir, beamLen, masks, QueryTriggerInteraction.Collide);
            int count = hits.Length;
            if (count > 0)
            {
                BubbleSortRaycastHits(hits);

                for (int i = 0; i < count; i++)
                {
                    if (MaskContains(BlockingLayers, hits[i].collider.gameObject))
                    {
                        //blocked
                        end = hits[i].point;
                        allowGrowth = false;
                        OnBlocked.Invoke();
                        break;
                    }
                    else if (MaskContains(StrikeLayers, hits[i].collider.gameObject))
                    {
                        //struck a target - keep growing
                        allowGrowth = true;
                        OnStrike.Invoke(hits[i].collider.gameObject);
                    }

                }
            }

            //if the start point catched up to the end point, destroy this beam
            start = start + (dir * Speed * Time.deltaTime);
            if(Vector3.Distance(EjectStart, end) < 1)
            {
                if(isActiveAndEnabled)
                    Lazarus.Instance.RelenquishToPool(gameObject);
                return;
            }

            //continue to push the end point
            if (allowGrowth)
            {
                //hit nothing - grow the beam
                end = end + (dir * Speed * Time.deltaTime);
            }

            EjectStart = start;
            EjectEnd = end;
            MyTrans.position = end;
            Poses[0] = start;
            Poses[1] = end;
            LineRend.SetPositions(Poses);
        }

        /// <summary>
        /// 
        /// </summary>
        void GrowBeam()
        {
            var start = SourceTrans.position;
            var dir = SourceTrans.forward;
            Vector3 end = start;
            //set the beam's forward to match the source, just in case other script atached 
            //to this projectile rely on this object facing the direction of the beam
            transform.forward = dir;

            bool allowGrowth = true;
            LayerMask masks = BlockingLayers | StrikeLayers;
            //Sadly, we can't use the non-alloc version because we need to sort and it will
            //invalidate the buffer due to excess values in the array.
            var hits = Physics.RaycastAll(start, dir, BeamLength, masks, QueryTriggerInteraction.Collide);
            int count = hits.Length;
            if (count > 0)
            {
                BubbleSortRaycastHits(hits);

                for (int i = 0; i < count; i++)
                {
                    if (MaskContains(BlockingLayers, hits[i].collider.gameObject))
                    {
                        //blocked
                        end = hits[i].point;
                        BeamLength = (end - start).magnitude;
                        allowGrowth = false;
                        OnBlocked.Invoke();
                        break;
                    }
                    else if (MaskContains(StrikeLayers, hits[i].collider.gameObject))
                    {
                        //struck a target - keep growing
                        allowGrowth = true;
                        OnStrike.Invoke(hits[i].collider.gameObject);
                    }

                }
            }

            if (allowGrowth)
            {
                //hit nothing - grow the beam
                end = start + (dir * BeamLength);
                BeamLength += Speed * Time.deltaTime;
                if (BeamLength > MaxLength)
                    BeamLength = MaxLength;
            }


            MyTrans.position = end;
            Poses[0] = start;
            Poses[1] = end;
            LineRend.SetPositions(Poses);

            StaticEjectPos = start;
            EjectStart = start;
            EjectForward = dir;
            EjectEnd = end;
        }

        /// <summary>
        /// Checks to see if a layermask contains the layer of a given GameObject.
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="go"></param>
        /// <returns></returns>
        public static bool MaskContains(LayerMask mask, GameObject go)
        {
            return ((1 << go.layer) & mask.value) != 0;
        }

        /// <summary>
        /// Simple method for sorting raycast hits.
        /// </summary>
        /// <param name="arr"></param>
        public static void BubbleSortRaycastHits(RaycastHit[] arr)
        {
            RaycastHit temp;

            for (int write = 0; write < arr.Length; write++)
            {
                for (int sort = 0; sort < arr.Length - 1; sort++)
                {
                    if (arr[sort].distance > arr[sort + 1].distance)
                    {
                        temp = arr[sort + 1];
                        arr[sort + 1] = arr[sort];
                        arr[sort] = temp;
                    }
                }
            }
        }
    }
}