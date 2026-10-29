using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Fix: ItemRoulette's "PauseGameWhileOpen" option ("pause single player while the roulette window is open")
    /// did nothing. The setting is created and shown in Risk of Options, but no code anywhere in ItemRoulette 1.4.4
    /// ever reads it.
    ///
    /// This implements it. While ItemRoulette's reel (spinning / result / Void decision), the casino or the admin menu is
    /// open, in a single-player run, and the option is on, <c>Time.timeScale</c> is set to 0; it is put back to
    /// whatever it was when the window closes. ItemRoulette's UI animates on unscaled time, so the reel itself keeps
    /// moving while the game world is frozen. Multiplayer is never paused, matching the option's description.
    ///
    /// The real pause menu is respected: nothing is changed while it is open, and the game is never un-paused from
    /// underneath it.
    /// </summary>
    internal static class PauseWhileOpenPatch
    {
        private static bool _resolved;
        private static bool _unsupported;

        private static PropertyInfo _pauseOption;      // ModConfig.PauseGameWhileOpen
        private static PropertyInfo _reelInstance;     // RouletteUI.Instance
        private static FieldInfo _reelState;           // RouletteUI._state
        private static PropertyInfo _casinoIsOpen;     // CasinoRouletteUI.IsOpen (static)
        private static PropertyInfo _adminInstance;    // AdminMenuUI.Instance
        private static PropertyInfo _adminIsOpen;      // AdminMenuUI.IsOpen

        private static bool _pausedByUs;
        private static float _savedTimeScale = 1f;

        /// <summary>Called every frame from the plugin.</summary>
        public static void Tick()
        {
            if (_unsupported)
            {
                return;
            }

            if (!_resolved && !Resolve())
            {
                return;
            }

            var want = ShouldPause();
            if (want && !_pausedByUs)
            {
                // Never take over from the real pause menu or any other mod that already froze time.
                if (PauseManager.isPaused || Time.timeScale == 0f)
                {
                    return;
                }

                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pausedByUs = true;
            }
            else if (!want && _pausedByUs)
            {
                // If the pause menu is open, wait: restoring now would un-pause the game behind it.
                if (PauseManager.isPaused)
                {
                    return;
                }

                Restore();
            }
        }

        /// <summary>Never leave the game frozen (plugin unload, game quit).</summary>
        public static void Release()
        {
            if (_pausedByUs)
            {
                Restore();
            }
        }

        private static void Restore()
        {
            if (Time.timeScale == 0f)
            {
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }

            _pausedByUs = false;
        }

        private static bool ShouldPause()
        {
            // No run (menu, scene change) or a multiplayer session: never frozen.
            if (!PluginConfig.PauseWhileOpen.Value || !Run.instance || !RoR2Application.isInSinglePlayer || !OptionEnabled())
            {
                return false;
            }

            try
            {
                var reel = _reelInstance.GetValue(null);
                if (reel != null)
                {
                    // BannerState: Hidden, Warning, Spinning, Finished, VoidDecision. Warning is a short error toast, not a window.
                    var state = _reelState.GetValue(reel)?.ToString();
                    if (state == "Spinning" || state == "Finished" || state == "VoidDecision")
                    {
                        return true;
                    }
                }

                if (_casinoIsOpen.GetValue(null) is bool casinoOpen && casinoOpen)
                {
                    return true;
                }

                var admin = _adminInstance.GetValue(null);
                return admin != null && _adminIsOpen.GetValue(admin) is bool adminOpen && adminOpen;
            }
            catch (Exception e)
            {
                _unsupported = true;
                Plugin.Log.LogWarning($"Pause-while-open check failed and was disabled: {e.Message}");
                return false;
            }
        }

        private static bool OptionEnabled()
        {
            return _pauseOption.GetValue(null) is ConfigEntryBase entry && entry.BoxedValue is bool enabled && enabled;
        }

        private static bool Resolve()
        {
            var modConfig = ItemRouletteBridge.FindType("ItemRoulette.Configuration.ModConfig");
            var reel = ItemRouletteBridge.FindType("ItemRoulette.UI.RouletteUI");
            var casino = ItemRouletteBridge.FindType("ItemRoulette.UI.CasinoRouletteUI");
            var admin = ItemRouletteBridge.FindType("ItemRoulette.UI.AdminMenuUI");
            if (modConfig == null || reel == null || casino == null || admin == null)
            {
                _unsupported = true;
                return false;
            }

            _pauseOption = AccessTools.Property(modConfig, "PauseGameWhileOpen");
            _reelInstance = AccessTools.Property(reel, "Instance");
            _reelState = AccessTools.Field(reel, "_state");
            _casinoIsOpen = AccessTools.Property(casino, "IsOpen");
            _adminInstance = AccessTools.Property(admin, "Instance");
            _adminIsOpen = AccessTools.Property(admin, "IsOpen");

            if (_pauseOption == null || _reelInstance == null || _reelState == null || _casinoIsOpen == null ||
                _adminInstance == null || _adminIsOpen == null)
            {
                _unsupported = true;
                Plugin.Log.LogWarning("ItemRoulette members needed for the pause-while-open fix were not found; fix disabled.");
                return false;
            }

            _resolved = true;
            return true;
        }
    }
}
