using Colossal.Logging;
using Game;
using Game.Modding;
using HarmonyLib;
using System;
using Unity.Entities;

namespace NoRoadWear
{
    public class Mod : IMod
    {
        // Harmony ID.
        public const string HarmonyID = "rcav8tr." + ModAssemblyInfo.Name;

        // Create a new log just for this mod.
        // This mod will have its own log file in the game's Logs folder.
        public static readonly ILog log = LogManager.GetLogger(ModAssemblyInfo.Name)
            .SetShowsErrorsInUI(true)                       // Show message in UI for severity level Error and above.
            .SetShowsStackTraceAboveLevels(Level.Error);    // Include stack trace for severity level Error and above.

        /// <summary>
        /// One-time mod loading.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} Version {ModAssemblyInfo.Version}");

            try
            {
                // Create this mod's one system.
                // The system gets activated in the system's OnWorldReady.
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<RoadWearSystem>();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} complete.");
        }

        /// <summary>
        /// One-time mod disposing.
        /// </summary>
        public void OnDispose()
        {
            log.Info($"{nameof(Mod)}.{nameof(OnDispose)}");
            
            // Remove all Harmony patches for this mod.
            new Harmony(HarmonyID).UnpatchAll();
        }
    }
}
