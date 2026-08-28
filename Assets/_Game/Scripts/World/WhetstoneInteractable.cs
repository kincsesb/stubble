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
            if (scythe == null)               return Loc("whetstone.hint.noscythe");
            if (scythe.WearNormalized < 0.05f) return Loc("whetstone.hint.sharp");
            return Loc("whetstone.hint.worn", Mathf.RoundToInt(scythe.WearNormalized * 100));
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
                HUDController.Instance?.ShowToolTip(Loc("whetstone.tooltip.noscythe"), 2f);
                return;
            }
            if (scythe.WearNormalized < 0.05f)
            {
                HUDController.Instance?.ShowToolTip(Loc("whetstone.tooltip.sharp"), 1.5f);
                return;
            }
            StartCoroutine(SharpenRoutine(scythe));
        }

        // ------------------------------------------------------------------ //

        static string Loc(string key, object arg = null)
        {
            var loc = Fields.Core.LocalizationManager.Instance;
            string raw = loc != null ? loc.Get(key) : key;
            return arg != null ? raw.Replace("{0}", arg.ToString()) : raw;
        }

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
                HUDController.Instance?.ShowToolTip(Loc("scythe.tooltip.sharpening", Mathf.RoundToInt(t * 100)), 0.12f);
                yield return null;
            }

            scythe.SetWear(0f);

            if (audio != null) Destroy(audio);
            HUDController.Instance?.ShowToolTip(Loc("scythe.tooltip.done"), 2.5f);
            _isBusy = false;
        }
    }
}
