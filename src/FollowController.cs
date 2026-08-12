using System;
using UnityEngine;
using UnityEngine.AI;

namespace ErenshorDeepSims
{
    // Local-player escort MVP. It deliberately does not alter Sim AI or COOP state: only the
    // current player's movement is steered while an explicit /dsfollow target is active.
    internal static class FollowController
    {
        private static readonly NavMeshPath Path = new NavMeshPath();
        private static SimPlayer _target;
        private static string _targetName;
        private static float _nextPathTime;
        private static Vector3 _waypoint;
        private static bool _hasWaypoint;
        private static bool _waitingAtTarget;
        private static Vector3 _lastProgressPosition;
        private static float _lastProgressTime;
        private static int _stallCount;
        private static bool _active;
        private static bool _standaloneChecked;
        private static bool _standalonePresent;
        private const float StopDistance = 3.0f;
        private const float ResumeDistance = 4.5f;

        internal static bool Active
        {
            get
            {
                if (_active && !IsTargetValid()) Stop();
                return _active;
            }
        }
        internal static string TargetName { get { return _targetName; } }

        internal static bool Start(SimPlayer target)
        {
            if (target == null || target.gameObject == null) return false;
            _target = target;
            _targetName = target.gameObject.name;
            _nextPathTime = 0f;
            _hasWaypoint = false;
            _waitingAtTarget = false;
            _lastProgressPosition = Vector3.zero;
            _lastProgressTime = 0f;
            _stallCount = 0;
            _active = true;
            return true;
        }

        internal static void Stop()
        {
            _active = false;
            _target = null;
            _targetName = null;
            _hasWaypoint = false;
            _waitingAtTarget = false;
            _lastProgressPosition = Vector3.zero;
            _lastProgressTime = 0f;
            _stallCount = 0;
        }

        internal static bool TryDrive(PlayerControl player)
        {
            if (!_active) return false;
            if (!IsTargetValid() || player == null || player.Myself == null || !player.Myself.Alive)
            {
                Stop();
                return false;
            }

            // Any deliberate movement or mouse interaction cancels escort mode immediately.
            if (Input.GetKey(InputManager.Forward) || Input.GetKey(InputManager.Backward) ||
                Input.GetKey(InputManager.Left) || Input.GetKey(InputManager.Right) ||
                Input.GetKey(InputManager.StrafeL) || Input.GetKey(InputManager.StrafeR) ||
                Input.GetKey(InputManager.Jump))
            {
                Stop();
                return false;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null || !player.CanMove) return true;

            Vector3 from = player.transform.position;
            Vector3 to = _target.transform.position;
            if (_lastProgressTime <= 0f)
            {
                _lastProgressPosition = from;
                _lastProgressTime = Time.time;
            }
            else if (HorizontalDistance(from, _lastProgressPosition) > 0.2f)
            {
                _lastProgressPosition = from;
                _lastProgressTime = Time.time;
                _stallCount = 0;
            }
            else if (Time.time - _lastProgressTime > 1.25f)
            {
                _lastProgressTime = Time.time;
                _hasWaypoint = false;
                _nextPathTime = 0f;
                _stallCount++;
                if (_stallCount >= 4)
                {
                    Stop();
                    return false;
                }
            }
            Vector3 flat = to - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (_waitingAtTarget && distance < ResumeDistance)
            {
                SetMoving(player, false);
                controller.SimpleMove(Vector3.zero);
                return true;
            }
            if (distance <= StopDistance)
            {
                _waitingAtTarget = true;
                SetMoving(player, false);
                controller.SimpleMove(Vector3.zero);
                return true;
            }
            _waitingAtTarget = false;

            if (Time.time >= _nextPathTime || !_hasWaypoint || HorizontalDistance(from, _waypoint) < 0.6f)
            {
                _nextPathTime = Time.time + 0.25f;
                NavMeshHit fromHit = new NavMeshHit();
                NavMeshHit toHit = new NavMeshHit();
                bool sampled = NavMesh.SamplePosition(from, out fromHit, 2.5f, NavMesh.AllAreas) &&
                                NavMesh.SamplePosition(to, out toHit, 4f, NavMesh.AllAreas);
                if (sampled && NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, Path) &&
                    Path.status != NavMeshPathStatus.PathInvalid && Path.corners != null && Path.corners.Length > 1)
                {
                    _waypoint = Path.corners[1];
                    _hasWaypoint = true;
                }
                else
                {
                    _hasWaypoint = false;
                }
            }
            if (!_hasWaypoint) return true;
            Vector3 destination = _waypoint;

            Vector3 direction = destination - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return true;
            direction.Normalize();
            float speed = player.Myself.MyStats == null ? 3.5f : player.Myself.MyStats.actualRunSpeed;
            if (speed < 1f) speed = 3.5f;

            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
            controller.SimpleMove(direction * speed);
            SetMoving(player, true);
            try { player.UpdateAnimRun(); } catch { }
            return true;
        }

        private static bool IsTargetValid()
        {
            return _target != null && _target.gameObject != null && _target.gameObject.activeInHierarchy &&
                   _target.MyStats != null && _target.MyStats.Myself != null && _target.MyStats.Myself.Alive &&
                   !CoopCompatibility.IsRemoteCoopHuman(_target);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        internal static bool StandaloneFollowLoaded()
        {
            if (_standaloneChecked) return _standalonePresent;
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType("ErenshorFollow.ErenshorFollowPlugin", false) == null) continue;
                _standalonePresent = true;
                break;
            }
            _standaloneChecked = true;
            return _standalonePresent;
        }

        private static void SetMoving(PlayerControl player, bool moving)
        {
            try
            {
                System.Reflection.FieldInfo movingField = player.GetType().GetField("moving", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (movingField != null) movingField.SetValue(player, moving);
            }
            catch { }
            try
            {
                Animator animator = player.Myself == null ? null : player.Myself.GetMyAnim();
                if (animator != null) animator.SetBool("Walking", moving);
            }
            catch { }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(PlayerControl), "LandMovement")]
    internal static class DeepSimsFollowMovementPatch
    {
        [HarmonyLib.HarmonyPrefix]
        private static bool Prefix(PlayerControl __instance)
        {
            try
            {
                // The standalone mod owns movement when installed; do not run two LandMovement prefixes.
                if (FollowController.StandaloneFollowLoaded()) return true;
                if (!FollowController.Active) return true;
                return !FollowController.TryDrive(__instance);
            }
            catch
            {
                FollowController.Stop();
                return true;
            }
        }
    }
}
