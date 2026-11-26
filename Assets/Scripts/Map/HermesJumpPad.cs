using UnityEngine;
using System;
using System.Collections.Generic;

public class HermesJumpPad : MonoBehaviour
    {
        public MapGenerator owner;
        public LayerMask affectedLayers = ~0;
        [Tooltip("World-space launch velocity (m/s). Recomputed on every Generate; re-generate after moving the map.")]
        public Vector3 launchVelocity = new Vector3(0f, 16f, 0f);
        [Tooltip("Gravity your hero controller uses (positive number). Used by the fallback driver and gizmo.")]
        public float gravityMagnitude = 9.81f;
        public float cooldownPerActor = 0.5f;
        [Tooltip("Multiplier passed to IMovementController.ApplyImpulseForce. Use 1 if your controller treats the " +
                 "impulse as a velocity change (m/s); raise/lower it if it divides by mass or scales by time.")]
        public float movementImpulseScale = 1f;
        public Vector3 targetWorldPoint;
        public float estimatedFlightTime;
        public GameObjectEvent onLaunched = new GameObjectEvent();
        public float triggerRadius = 1f;

        /// <summary>Static hook for global systems (audio, stats, VFX).</summary>
        public static event Action<HermesJumpPad, GameObject> AnyPadLaunched;

        readonly Dictionary<GameObject, float> _lastLaunch = new Dictionary<GameObject, float>();

       private void Update()
        {
            Collider[] cols = AbilityPhysics.OverlapSphere(transform.position, triggerRadius);

            foreach (var col in cols)
            {
                if (Time.time - _lastLaunch.GetValueOrDefault(col.gameObject, 0f) < cooldownPerActor) continue;
                _lastLaunch[col.gameObject] = Time.time;
                Launch(col.gameObject);
            }
        }

        public void Launch(GameObject actor)
        {
            if (actor == null) return;

            var movement = actor.GetComponentInParent<IMovementController>();
            if (movement != null)
            {
                movement.HaltVelocity();
                Vector3 impactDirection = (targetWorldPoint - actor.transform.position).normalized;
                movement.ApplyImpulseForce(launchVelocity + impactDirection);
            }
            
            onLaunched?.Invoke(actor);
            if (owner != null) owner.NotifyJumpPadLaunched(actor);
            AnyPadLaunched?.Invoke(this, actor);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 g = Vector3.down * gravityMagnitude;
            Vector3 p0 = transform.position + Vector3.up * 0.1f;
            Vector3 prev = p0;
            for (int i = 1; i <= 40; i++)
            {
                float t = i * 0.1f;
                Vector3 p = p0 + launchVelocity * t + 0.5f * g * t * t;
                Gizmos.DrawLine(prev, p);
                prev = p;
                if (p.y < p0.y - 4f) break;
            }
            if (targetWorldPoint != Vector3.zero) Gizmos.DrawWireSphere(targetWorldPoint, 0.4f);
        }

        private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
    }
