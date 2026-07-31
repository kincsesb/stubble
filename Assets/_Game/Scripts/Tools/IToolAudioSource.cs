using Fields.Core;

namespace Fields.Tools
{
    /// <summary>
    /// Audio hook interface for tool sounds. Implemented by the audio system in P3-01.
    /// Placeholder implementation allows full gameplay without audio assets.
    /// </summary>
    public interface IToolAudioSource
    {
        void PlaySwingResult(SwingResult result);
        void PlayObstacleStrike();
        void PlayEngineStart();
        void PlayEngineStop();
        void PlayEngineLoop(float rpm); // continuous, call every frame
    }

    /// <summary>Placeholder that does nothing — replaced by real audio in P3.</summary>
    public class PlaceholderToolAudio : UnityEngine.MonoBehaviour, IToolAudioSource
    {
        public void PlaySwingResult(SwingResult result) { }
        public void PlayObstacleStrike() { }
        public void PlayEngineStart() { }
        public void PlayEngineStop() { }
        public void PlayEngineLoop(float rpm) { }
    }
}
