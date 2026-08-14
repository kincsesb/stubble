using System.Collections;
using UnityEngine;
using Fields.Core;
using Fields.Tools;
using Fields.UI;

namespace Fields.World
{
    /// <summary>
    /// Place on any world object (grindstone, barn wall hook, etc.).
    /// When the player presses E with a worn LongScythe equipped, sharpens it over sharpenDuration seconds.
    /// Implements IHintProvider so the HUD shows wear percentage instead of the generic "interact" text.
    /// </summary>
    public class WhetstoneInteractable : MonoBehaviour, IInteractable, IHintProvider
    {
        [Header("Sharpening")]
        [SerializeField] float sharpenDuration = 2.5f;

        [Header("Audio")]
        [SerializeField] AudioClip sfxGrinding;
        [SerializeField][Range(0f, 1f)] float sfxVolume = 0.45f;

        bool _isBusy;

        // ------------------------------------------------------------------ //
        // IHintProvider — context-sensitive hint shown in HUD
        // ------------------------------------------------------------------ //

        public string GetHint(PlayerController player)
        {
            var scythe = GetEquippedScythe(player);
            if (scythe == null)          return "[E]  Fenőkő  (kasza szükséges)";
            if (scythe.WearNormalized < 0.05f) return "[E]  Kasza éles";
            return $"[E]  Kasza élezése  —  {Mathf.RoundToInt(scythe.WearNormalized * 100)}% kopott";
        }

        // ------------------------------------------------------------------ //
        // IInteractable
        // ------------------------------------------------------------------ //

        public void Interact(PlayerController player)
        {
            if (_isBusy) return;

            var scythe = GetEquippedScythe(player);
            if (scythe == null)
            {
                HUDController.Instance?.ShowToolTip("Csak kaszával élezhetod.", 2f);
                return;
            }
            if (scythe.WearNormalized < 0.05f)
            {
                HUDController.Instance?.ShowToolTip("A kasza még éles.", 1.5f);
                return;
            }
            StartCoroutine(SharpenRoutine(scythe));
        }

        // ------------------------------------------------------------------ //

        LongScythe GetEquippedScythe(PlayerController player)
        {
            var holder = player.GetComponentInChildren<ToolHolder>();
            return holder?.ActiveTool as LongScythe;
        }

        IEnumerator SharpenRoutine(LongScythe scythe)
        {
            _isBusy = true;

            AudioSource audio = null;
            if (sfxGrinding != null)
            {
                audio = gameObject.AddComponent<AudioSource>();
                audio.clip         = sfxGrinding;
                audio.loop         = true;
                audio.volume       = sfxVolume;
                audio.spatialBlend = 1f;
                audio.Play();
            }

            float elapsed   = 0f;
            float startWear = scythe.WearNormalized;

            while (elapsed < sharpenDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / sharpenDuration);
                scythe.SetWear(Mathf.Lerp(startWear, 0f, t));
                HUDController.Instance?.ShowToolTip($"Élezés...  {Mathf.RoundToInt(t * 100)}%", 0.12f);
                yield return null;
            }

            scythe.SetWear(0f);

            if (audio != null) Destroy(audio);
            HUDController.Instance?.ShowToolTip("Kasza megélesítve!", 2.5f);
            _isBusy = false;
        }
    }
}
