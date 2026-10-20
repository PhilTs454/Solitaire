using Solitaire.Models;

namespace Solitaire.Services
{
    /// <summary>
    /// Victory win-sequence SFX gate. Off until one-shots are authored and mixed
    /// (glyph flight was stacking StreakPip / Place / Streak at once).
    /// </summary>
    internal static class VictorySequenceAudio
    {
        internal const bool Enabled = false;

        internal static void Play(IAudioService audio, string key, float volume)
        {
            if (!Enabled || audio == null || string.IsNullOrEmpty(key))
                return;

            audio.PlaySfx(key, volume);
        }
    }
}
