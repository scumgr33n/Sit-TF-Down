using HarmonyLib;
using MelonLoader;
using UnityEngine;
using SteamShelf.Placeables;
using System.Reflection;

[assembly: MelonInfo(typeof(SitTFDown.Core), "Sit TF Down", "1.0.0", "scumgr33n", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]

namespace SitTFDown
{
    public class Core : MelonMod
    {
        public static Core Instance;
        private FirstPersonController player;
        private Rigidbody playerRb;
        private bool isSeated = false;
        private float scanTimer = 0f;
        private Collider currentChairCollider;
        private int framesUntilRelease = -1;
        private bool loggedRbSettings = false;
        private static readonly FieldInfo MoveInputField =
    typeof(FirstPersonController).GetField("playerInputContext", BindingFlags.NonPublic | BindingFlags.Instance);


        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("Initialized Sit TF Down");
        }

        public override void OnUpdate()
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= 2f)
            {
                scanTimer = 0f;
                TagChairs();
            }

            if (isSeated && Input.GetKeyDown(KeyCode.LeftShift))
            {
                StandUp();
        
            }

            if (framesUntilRelease > 0)
            {
                framesUntilRelease--;
                if (framesUntilRelease == 0)
                {
                    playerRb.isKinematic = false;
                    LoggerInstance.Msg("Kinematic released.");
                }
            }
        }
        private static readonly string[] ChairKeywords = { "chair", "couch", "sofa", "recliner", "stool", "bench" };

        private bool IsSeatingFurniture(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string lower = id.ToLower();
            foreach (string keyword in ChairKeywords)
            {
                if (lower.Contains(keyword)) return true;
            }
            return false;
        }

        private void TagChairs()
        {
            PlacementTag[] allTags = UnityEngine.Object.FindObjectsOfType<PlacementTag>();
            foreach (PlacementTag tag in allTags)
            {
                string id = tag.PlaceableData?.ID ?? "";
                bool isChair = IsSeatingFurniture(id);

                if (isChair && tag.GetComponent<SittableChair>() == null)
                {
                    tag.gameObject.AddComponent<SittableChair>();
                    LoggerInstance.Msg($"Tagged chair: {id}");
                }
            }
        }
        private void DiagnoseZoomSettings()
        {
            if (player == null)
                player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();

            if (player == null)
            {
                LoggerInstance.Msg("No FirstPersonController found.");
                return;
            }

            LoggerInstance.Msg($"[ZoomDiag] enableZoom={player.enableZoom}, holdToZoom={player.holdToZoom}, zoomKey={player.zoomKey}, fov={player.fov}, zoomFOV={player.zoomFOV}, zoomStepTime={player.zoomStepTime}, current playerCamera.fieldOfView={player.playerCamera.fieldOfView}");
        }

        public void SitDown(Transform chair)
        {
            if (isSeated) return;

            if (player == null)
                player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();

            if (player == null)
            {
                LoggerInstance.Warning("Could not find FirstPersonController.");
                return;
            }

            playerRb = player.GetComponent<Rigidbody>();
            currentChairCollider = chair.GetComponent<Collider>();

            Vector3 seatOffset = chair.position + chair.up * 0.5f + chair.forward * 0.2f;
            player.transform.position = seatOffset;
            player.transform.rotation = Quaternion.LookRotation(-chair.forward, Vector3.up);

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.isKinematic = true;
            }

            player.playerCanMove = false;

            isSeated = true;
            LoggerInstance.Msg("Sat down.");
        }

        public void StandUp()
        {
            if (!isSeated) return;

            if (player != null && playerRb != null)
            {
                if (!loggedRbSettings)
                {
                    loggedRbSettings = true;
                    LoggerInstance.Msg($"[RB Settings] mass={playerRb.mass}, drag={playerRb.linearDamping}, angularDrag={playerRb.angularDamping}, constraints={playerRb.constraints}, collisionDetectionMode={playerRb.collisionDetectionMode}, interpolation={playerRb.interpolation}");
                }

                Vector3 clearSpot = FindClearStandPosition(player.transform);
                playerRb.position = clearSpot;
                Physics.SyncTransforms();
            }

            if (player != null)
                player.playerCanMove = true;

            isSeated = false;
            framesUntilRelease = 3;
            LoggerInstance.Msg("Stood up (position set, kinematic release pending).");
        }

        private Vector3 FindClearStandPosition(Transform playerTransform)
        {
            Collider realPlayerCollider = player.GetComponent<Collider>();
            float checkRadius = (realPlayerCollider != null ? realPlayerCollider.bounds.extents.x : 0.4f) + 0.05f;
            float stepDistance = checkRadius * 2.5f;

            LoggerInstance.Msg($"[StandCheck] Using checkRadius={checkRadius}, stepDistance={stepDistance}");

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * playerTransform.forward;
                Vector3 candidate = playerTransform.position + dir * stepDistance;

                Collider[] overlaps = Physics.OverlapSphere(candidate, checkRadius);
                bool blocked = false;
                foreach (Collider col in overlaps)
                {
                    if (col == currentChairCollider) continue;
                    if (col == realPlayerCollider) continue;
                    if (col.isTrigger) continue;
                    blocked = true;
                    break;
                }

                if (!blocked)
                {
                    LoggerInstance.Msg($"Standing spot chosen: angle {angle}, clear.");
                    return candidate;
                }
            }

            LoggerInstance.Msg("No clear direction found in 8 directions — staying in place.");
            return playerTransform.position;
        }
    }
}