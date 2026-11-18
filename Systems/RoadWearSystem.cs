using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Entities;
using UnityEngine.Scripting;

namespace NoRoadWear
{
    /// <summary>
    /// System to prevent ongoing road wear and clear existing road wear.
    /// </summary>
    public partial class RoadWearSystem : GameSystemBase
    {
        // This system.
        private static RoadWearSystem _thisSystem;

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.log.Info($"{nameof(RoadWearSystem)}.{nameof(OnCreate)}");

            // Save the game's instance of this system.
            _thisSystem = this;

            // Disable OnUpdate.
            Enabled = false;
        }

        /// <summary>
        /// Called by the game when the world is ready.
        /// </summary>
        [Preserve]
        protected override void OnWorldReady()
        {
            try
            {
                base.OnWorldReady();
                Mod.log.Info($"{nameof(RoadWearSystem)}.{nameof(OnWorldReady)}");

                // This system is activated in OnWorldReady to ensure that all mods have been loaded
                // before determining how this system should be run.

                // Try to get Parks, Roads, Lane Wear (PRLW) mod LaneWearSystem.
                ComponentSystemBase prlwLaneWearSystem = GetPRLWLaneWearSystem();
                if (prlwLaneWearSystem != null)
                {
                    // PRLW mod is active.
                    // Create a postfix patch on PRLW mod RoadWearSystem.OnUpdate().
                    const string originalMethodName = "OnUpdate";
                    MethodInfo originalMethod = prlwLaneWearSystem.GetType().GetMethod(originalMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (originalMethod == null)
                    {
                        Mod.log.Error($"Unable to find patch original method {prlwLaneWearSystem.GetType()}.{originalMethodName}.");
                        return;
                    }
                    const string postfixMethodName = nameof(PRLWRoadWearSystemOnUpdatePostfix);
                    MethodInfo postfixMethod = typeof(RoadWearSystem).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                    if (postfixMethod == null)
                    {
                        Mod.log.Error($"Unable to find patch postfix method {nameof(RoadWearSystem)}.{postfixMethodName}.");
                        return;
                    }
                    new Harmony(Mod.HarmonyID).Patch(originalMethod, null, new HarmonyMethod(postfixMethod));
                }
                else
                {
                    // PRLW mod is not active.
                    // Activate this system to run at the end of the PrefabUpdate phase.
                    World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAfter<RoadWearSystem>(SystemUpdatePhase.PrefabUpdate);
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }
        
        /// <summary>
        /// Get the Parks, Roads, Lane Wear (PRLW) mod LaneWearSystem, if mod is active.
        /// </summary>
        private ComponentSystemBase GetPRLWLaneWearSystem()
        {
            // Find the PRLW mod.
            ExecutableAsset[] modAssets = ExecutableAsset.GetModAssets();
            foreach (ExecutableAsset executableAsset in modAssets)
            {
                // The name of the PRLW mod is "ParksRoads".
                Game.Modding.ModManager.ModInfo modInfo = new(executableAsset);

                if (modInfo.isValid && modInfo.asset != null && modInfo.asset.name == "ParksRoads")
                {
                    // Found the PRLW mod.
                    // Find LaneWearSystem in the PRLW mod.
                    // The namespace in the mod is "ParksRoads".
                    Type assemblyType = modInfo.asset.assembly.GetType("ParksRoads.LaneWearSystem");
                    if (assemblyType != null)
                    {
                        Mod.log.Info("Parks, Roads, Lane Wear mod found.");
                        return World.GetOrCreateSystemManaged(assemblyType);
                    }

                    // Warn for PRLW mod found but system not found.
                    Mod.log.Warn("Parks, Roads, Lane Wear mod found but unable to find LaneWearSystem in the mod.");
                    return null;
                }
            }

            // PRLW mod not found.
            // This is not an error, the PRLW mod is simply not active.
            Mod.log.Info("Parks, Roads, Lane Wear mod not found.");
            return null;
        }

        /// <summary>
        /// Called when a game, main menu, editor, etc. is about to be loaded.
        /// </summary>
        [Preserve]
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Disable OnUpdate.
            Enabled = false;
        }

        /// <summary>
        /// Called when a game is done being loaded.
        /// </summary>
        [Preserve]
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Initialize only for game mode.
            if (mode == GameMode.Game)
            {
                // Set all existing LaneCondition wear to zero.
                // Query in loop is adapted from Game.Simulation.NetDeteriorationSystem as of 1.6.4:
                //m_LaneQuery = GetEntityQuery(ComponentType.ReadWrite<LaneCondition>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
                foreach (RefRW<LaneCondition> laneConditionRef in SystemAPI
                    .Query<RefRW<LaneCondition>>()
                    .WithAll<UpdateFrame>()
                    .WithNone<Deleted, Temp>())
                {
                    laneConditionRef.ValueRW.m_Wear = 0f;
                }

                // Set all existing NetCondition wear to zero.
                // The game would do this eventually on its own due to lane condition wear being zero, but do it here so it is immediate.
                // Query in loop is adapted from Game.Simulation.NetDeteriorationSystem as of 1.6.4:
                //m_EdgeQuery = GetEntityQuery(ComponentType.ReadWrite<NetCondition>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
                foreach (RefRW<NetCondition> netConditionRef in SystemAPI
                    .Query<RefRW<NetCondition>>()
                    .WithAll<UpdateFrame>()
                    .WithNone<Deleted, Temp>())
                {
                    netConditionRef.ValueRW.m_Wear = 0f;
                }

                // There is no need to remove existing road MaintenanceRequest.
                // The game will eventually remove all of them on its own.

                // There is no need to remove existing requests from road MaintenanceConsumer.
                // The game will eventually remove all of them on its own.

                // The vanilla game does not track snow on roads and snow does not cause road wear.
                // So there is nothing special to do for snow.

                // Enable OnUpdate.
                Enabled = true;
            }
        }

        /// <summary>
        /// Postfix patch method for PRLW RoadWearSystem.OnUpdate().
        /// </summary>
        [Preserve]
        private static void PRLWRoadWearSystemOnUpdatePostfix()
        {
            // Call OnUpdate for the game's instance of this system.
            _thisSystem.OnUpdate();
        }

        /// <summary>
        /// Called by the game to update the system.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Must be in a game.
            if (GameManager.instance.gameMode == GameMode.Game)
            {
                // Update road deterioration prefabs to prevent ongoing road wear due to time and traffic.
                foreach (RefRW<LaneDeteriorationData> laneDeteriorationDataRef in SystemAPI.Query<RefRW<LaneDeteriorationData>>().WithAll<PrefabData>())
                {
                    ref LaneDeteriorationData laneDeteriorationData = ref laneDeteriorationDataRef.ValueRW;
                    laneDeteriorationData.m_TimeFactor    = 0f;
                    laneDeteriorationData.m_TrafficFactor = 0f;
                }
            }

            // Run only once.
            Enabled = false;
        }
    }
}
