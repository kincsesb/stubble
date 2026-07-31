using Fields.Feel;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fields.Editor
{
    /// <summary>
    /// Editor utility: wires Feel components onto the Player in the active scene.
    /// Run via Fields/Setup Feel or called from Unity MCP after compile.
    /// </summary>
    public static class FeelSetupHelper
    {
        [MenuItem("Fields/Setup Feel on Player")]
        public static void Setup()
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null) { Debug.LogError("[FeelSetup] Player not found"); return; }

            var cam = playerGO.GetComponentInChildren<Camera>();
            if (cam == null) { Debug.LogError("[FeelSetup] Camera not found"); return; }
            var cameraRoot = cam.transform.parent;

            // ---- Springs on CameraRoot ----
            var springRot = cameraRoot.GetComponent<MMSpringRotation>()
                         ?? cameraRoot.gameObject.AddComponent<MMSpringRotation>();
            springRot.Space = MMSpringRotation.Spaces.Local;
            springRot.SpringVector3.SpringX.Damping  = 0.55f; springRot.SpringVector3.SpringX.Frequency = 9f;
            springRot.SpringVector3.SpringY.Damping  = 0.55f; springRot.SpringVector3.SpringY.Frequency = 9f;
            springRot.SpringVector3.SpringZ.Damping  = 0.55f; springRot.SpringVector3.SpringZ.Frequency = 9f;
            EditorUtility.SetDirty(cameraRoot.gameObject);

            var springFov = cam.GetComponent<MMSpringCameraFieldOfView>()
                          ?? cam.gameObject.AddComponent<MMSpringCameraFieldOfView>();
            springFov.FloatSpring.Damping   = 0.6f; springFov.FloatSpring.Frequency = 8f;
            EditorUtility.SetDirty(cam.gameObject);

            var springPos = cameraRoot.GetComponent<MMSpringPosition>()
                          ?? cameraRoot.gameObject.AddComponent<MMSpringPosition>();
            springPos.SpringVector3.SpringX.Damping  = 0.5f; springPos.SpringVector3.SpringX.Frequency = 12f;
            springPos.SpringVector3.SpringY.Damping  = 0.5f; springPos.SpringVector3.SpringY.Frequency = 12f;
            springPos.SpringVector3.SpringZ.Damping  = 0.5f; springPos.SpringVector3.SpringZ.Frequency = 12f;
            EditorUtility.SetDirty(cameraRoot.gameObject);

            // ---- SwingFeelController on Player ----
            var feelCtrl = playerGO.GetComponent<SwingFeelController>()
                        ?? playerGO.AddComponent<SwingFeelController>();
            feelCtrl.firstCutIntensityBonus = 1.5f;

            // ---- _Feel parent ----
            var feelRootT = playerGO.transform.Find("_Feel");
            var feelRoot  = feelRootT != null ? feelRootT.gameObject : new GameObject("_Feel");
            if (feelRootT == null)
            {
                feelRoot.transform.SetParent(playerGO.transform);
                feelRoot.transform.localPosition = Vector3.zero;
            }

            feelCtrl.feedbackFullHit  = GetOrCreatePlayer("Feel_FullHit",  feelRoot.transform);
            feelCtrl.feedbackPartial  = GetOrCreatePlayer("Feel_Partial",  feelRoot.transform);
            feelCtrl.feedbackWhiff    = GetOrCreatePlayer("Feel_Whiff",    feelRoot.transform);
            feelCtrl.feedbackObstacle = GetOrCreatePlayer("Feel_Obstacle", feelRoot.transform);

            // Full Hit: rotation spring + FOV pulse
            SetupFullHitFeedbacks(feelCtrl.feedbackFullHit, cameraRoot, cam);
            // Partial: rotation spring only
            SetupPartialFeedbacks(feelCtrl.feedbackPartial, cameraRoot);
            // Whiff: subtle spring
            SetupWhiffFeedbacks(feelCtrl.feedbackWhiff, cameraRoot);
            // Obstacle: reversed strong kick + position shake
            SetupObstacleFeedbacks(feelCtrl.feedbackObstacle, cameraRoot);

            EditorUtility.SetDirty(feelCtrl);

            PrefabUtility.SaveAsPrefabAsset(playerGO, "Assets/_Game/Prefabs/Network/Player.prefab");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[FeelSetup] Done — MMSpringRotation + FOV + Position + 4 MMF_Players wired");
        }

        // ------------------------------------------------------------------ //

        static MMF_Player GetOrCreatePlayer(string name, Transform parent)
        {
            var t = parent.Find(name);
            var go = t != null ? t.gameObject : new GameObject(name);
            if (t == null) { go.transform.SetParent(parent); go.transform.localPosition = Vector3.zero; }
            return go.GetComponent<MMF_Player>() ?? go.AddComponent<MMF_Player>();
        }

        static void SetupFullHitFeedbacks(MMF_Player player, Transform camRoot, Camera cam)
        {
            player.FeedbacksList?.Clear();

            // Spring rotation kick: -2° pitch (kick up), spring-damped back
            var rotFb = (MMF_RotationSpring)player.AddFeedback(typeof(MMF_RotationSpring));
            rotFb.AnimateRotationTarget = camRoot;
            rotFb.Mode = MMF_RotationSpring.Modes.Bump;
            rotFb.BumpRotationMin = new Vector3(-2000f, 0f, 0f);
            rotFb.BumpRotationMax = new Vector3(-2000f, 0f, 0f);
            rotFb.DampingX = 0.55f; rotFb.FrequencyX = 9f;
            rotFb.DeclaredDuration = 0.3f;
            rotFb.RotationSpace = Space.Self;
            rotFb.Label = "Rotation Kick";

            // FOV compress pulse — needs MMCameraFieldOfViewShaker on the camera
            var fovFb = (MMF_CameraFieldOfView)player.AddFeedback(typeof(MMF_CameraFieldOfView));
            fovFb.RemapFieldOfViewZero = 60f;
            fovFb.RemapFieldOfViewOne  = 58f;
            fovFb.Duration = 0.12f;
            fovFb.Label = "FOV Pulse";

            EditorUtility.SetDirty(player);
        }

        static void SetupPartialFeedbacks(MMF_Player player, Transform camRoot)
        {
            player.FeedbacksList?.Clear();

            var rotFb = (MMF_RotationSpring)player.AddFeedback(typeof(MMF_RotationSpring));
            rotFb.AnimateRotationTarget = camRoot;
            rotFb.Mode = MMF_RotationSpring.Modes.Bump;
            rotFb.BumpRotationMin = new Vector3(-1200f, 0f, 0f);
            rotFb.BumpRotationMax = new Vector3(-1200f, 0f, 0f);
            rotFb.DampingX = 0.55f; rotFb.FrequencyX = 9f;
            rotFb.DeclaredDuration = 0.25f;
            rotFb.RotationSpace = Space.Self;
            rotFb.Label = "Rotation Kick (Partial)";

            EditorUtility.SetDirty(player);
        }

        static void SetupWhiffFeedbacks(MMF_Player player, Transform camRoot)
        {
            player.FeedbacksList?.Clear();

            var rotFb = (MMF_RotationSpring)player.AddFeedback(typeof(MMF_RotationSpring));
            rotFb.AnimateRotationTarget = camRoot;
            rotFb.Mode = MMF_RotationSpring.Modes.Bump;
            rotFb.BumpRotationMin = new Vector3(-600f, 0f, 0f);
            rotFb.BumpRotationMax = new Vector3(-600f, 0f, 0f);
            rotFb.DampingX = 0.65f; rotFb.FrequencyX = 9f;
            rotFb.DeclaredDuration = 0.2f;
            rotFb.RotationSpace = Space.Self;
            rotFb.Label = "Rotation Kick (Whiff)";

            EditorUtility.SetDirty(player);
        }

        static void SetupObstacleFeedbacks(MMF_Player player, Transform camRoot)
        {
            player.FeedbacksList?.Clear();

            // Reversed kick (forward) + strong shake
            var rotFb = (MMF_RotationSpring)player.AddFeedback(typeof(MMF_RotationSpring));
            rotFb.AnimateRotationTarget = camRoot;
            rotFb.Mode = MMF_RotationSpring.Modes.Bump;
            rotFb.BumpRotationMin = new Vector3(3000f, 0f, 0f);
            rotFb.BumpRotationMax = new Vector3(3000f, 0f, 0f);
            rotFb.DampingX = 0.4f; rotFb.FrequencyX = 7f;
            rotFb.DeclaredDuration = 0.4f;
            rotFb.RotationSpace = Space.Self;
            rotFb.Label = "Rotation Kick (Obstacle, reversed)";

            var posFb = (MMF_PositionShake)player.AddFeedback(typeof(MMF_PositionShake));
            posFb.Duration = 0.3f;
            posFb.ShakeRange = 0.05f;
            posFb.Label = "Position Shake (Obstacle)";

            EditorUtility.SetDirty(player);
        }
    }
}
