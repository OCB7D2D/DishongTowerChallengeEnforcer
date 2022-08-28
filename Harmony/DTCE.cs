using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class DishongTowerChallengeEnforcer : IModApi
{

    static readonly int HurtPerIv = 3;
    static readonly float Interval = 0.25f;

    static int Ground = 41;
    static Vector2i TowerX = new Vector2i(11, 47);
    static Vector2i TowerY = new Vector2i(11, 47);

    static float SpawnRot = 180f;
    static Vector3 SpawnPos = new Vector3(29.5f, 41.5f, 51.5f);

    static bool reportedWinOnce = false;

    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        ModEvents.GameStartDone.RegisterHandler(StartDone);
    }

    static bool Enabled = false;

    private static void SetupDishongTowerDefaults()
    {
        Ground = 41;
        SpawnRot = 180f;
        TowerX = new Vector2i(11, 47);
        TowerY = new Vector2i(11, 47);
        SpawnPos = new Vector3(29.5f, 41.5f, 51.5f);
    }

    private static void SetupDishongTowerConfig()
    {
        reportedWinOnce = false;
        SetupDishongTowerDefaults();
        // Try to load config from config block
        // Config Method inspired by sphereII's SCore
        if (Block.GetBlockValue("ConfigDishongTowerChallenge") is BlockValue cfgbv)
        {
            if (cfgbv.Block.Properties is DynamicProperties props)
            {
                props.ParseInt("TowerGround", ref Ground);
                props.ParseInt("TowerPosMinX", ref TowerX.x);
                props.ParseInt("TowerPosMaxX", ref TowerX.y);
                props.ParseInt("TowerPosMinY", ref TowerY.x);
                props.ParseInt("TowerPosMaxY", ref TowerY.y);
                props.ParseFloat("TowerSpawnX", ref SpawnPos.x);
                props.ParseFloat("TowerSpawnY", ref SpawnPos.y);
                props.ParseFloat("TowerSpawnZ", ref SpawnPos.z);
                props.ParseFloat("TowerSpawnRot", ref SpawnRot);
            }
        }
        // Report configuration for now
        Log.Out("TowerGround {0}", Ground);
        Log.Out("TowerPosX {0}", TowerX);
        Log.Out("TowerPosY {0}", TowerY);
        Log.Out("TowerSpawnPos {0}", SpawnPos);
        Log.Out("TowerSpawnRot {0}", SpawnRot);
    }

    private void StartDone()
    {
        var world = GamePrefs.GetString(EnumGamePrefs.GameWorld);
        Enabled = world == "DishongTowerChallenge";
        if (Enabled) SetupDishongTowerConfig();
    }

    // Hook for clients of dedicated servers
    [HarmonyPatch(typeof(GameManager))]
    [HarmonyPatch("WorldInfo")]
    public class GameManager_WorldInfo
    {
        static void Prefix(string _levelName)
        {
            Enabled = _levelName == "DishongTowerChallenge";
            if (Enabled) SetupDishongTowerConfig();
        }
    }

    // Spawn on porch when died without bedroll
    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("GetSpawnPoint")]
    public class EntityPlayerLocal_GetSpawnPoint
    {
        static bool Prefix(
            EntityPlayerLocal __instance,
            ref SpawnPosition __result)
        {
            if (!Enabled) return true;
            if (__instance.Spawned) return true;
            if (__instance.SpawnPoints.Count != 0) return true;
            __result = new SpawnPosition(SpawnPos, SpawnRot);
            return false;
        }
    }

    // Spawn on the porch when entering game/server
    [HarmonyPatch(typeof(PlayerMoveController))]
    [HarmonyPatch("updateRespawn")]
    public class PlayerMoveController_updateRespawn
    {
        static void Prefix(
            EntityPlayerLocal ___entityPlayerLocal,
            float ___respawnTime,
            RespawnType ___respawnReason)
        {
            if (!Enabled) return;
            if (___entityPlayerLocal.Spawned) return;
            if (___respawnTime > 0.0) return;
            switch (___respawnReason)
            {
                case RespawnType.NewGame:
                case RespawnType.EnterMultiplayer:
                    ___entityPlayerLocal.position.x = SpawnPos.x;
                    ___entityPlayerLocal.position.y = SpawnPos.y;
                    ___entityPlayerLocal.position.z = SpawnPos.z;
                    ___entityPlayerLocal.rotation.y = SpawnRot;
                    break;
            }
        }
    }

    static float Waited = 0;
    static bool InPrefabCopy = false;
    static bool AllowRadiationCVar = false;

    static bool IsInsideTower(Vector3i pos)
    {
        return pos.x >= TowerX.x && pos.x <= TowerX.y
            && pos.z >= TowerY.x && pos.z <= TowerY.y;
    }

    static bool IsInSafeArea(Vector3i pos)
    {
        return IsInsideTower(pos) ||
            // Allow users to be within trader area without being hurt
            GameManager.Instance.World.GetTraderAreaAt(pos) != null;
    }

    // Skip regular radiation cvar set
    [HarmonyPatch(typeof(EntityBuffs))]
    [HarmonyPatch("SetCustomVar")]
    public class EntityBuffs_SetCustomVar
    {
        static bool Prefix(
            ref string _name)
        {
            if (!Enabled) return true;
            if (AllowRadiationCVar) return true;
            return _name != "_biomeradiation";
        }
    }

    // Check player position to limit position
    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("Update")]
    public class EntityPlayerLocal_Update
    {
        static void Postfix(EntityPlayerLocal __instance)
        {
            if (!Enabled) return;

            // Wait for update interval
            Waited += Time.deltaTime;
            if (Waited < Interval) return;
            Waited -= Interval;

            bool radiation = true;

            // Get age of player for grace period
            // ulong age = __instance.world.worldTime
            //     - __instance.WorldTimeBorn;

            // Calculate timed radiation level
            // float radiation = age / 150f;
            // radiation = Mathf.Max(0f, radiation);
            // radiation = Mathf.Min(900f, radiation);

            // Get Block position of player
            Vector3i pos = __instance.GetBlockPosition();

            // Check against static tower position
            bool inTower = IsInsideTower(pos);
            // Allow users to be within trader area without being hurt
            bool atTrader = GameManager.Instance.World.GetTraderAreaAt(pos) != null;

            // Call to check if we have reached win condition
            if (atTrader) CheckWinCondition(__instance, __instance.AttachedToEntity);

            // Allow to move inside tower/trader and above ground
            if (inTower || atTrader || pos.y > Ground ||
                // Allow to be outside when in/on a vehicles
                __instance.AttachedToEntity != null) radiation = false;

            // Update the radiation level
            AllowRadiationCVar = true;
            __instance.Buffs.SetCustomVar(
                "_biomeradiation",
                radiation ? 255f : 0f);
            AllowRadiationCVar = false;

            // Otherwise hurt the player constantly until he is back in or dies
            // Add a Grace Period for when you are just starting out
            __instance.DamageEntity(DamageSource.radiation,
                radiation ? HurtPerIv : 0,
                false, 1f);
        }
    }


    static readonly MethodInfo FnGetWheelsOnGround = AccessTools
        .Method(typeof(EntityVehicle), "GetWheelsOnGround");
    
    // Note: this function is only called if at a trader protected area
    private static void CheckWinCondition(EntityPlayerLocal player, Entity vehicle)
    {
        if (reportedWinOnce) return;
        // Note: seems `onGround` doesn't return what it's supposed to do
        if (vehicle is EntityVGyroCopter gyro /*  && gyro.onGround */)
        {
            if ((int)FnGetWheelsOnGround.Invoke(gyro, null) < 3) return;
            Log.Warning("Congratulations to complete the dishong tower challenge");
            Log.Warning("  Your time is {0}", player.totalTimePlayed);
            foreach (var mod in ModManager.GetLoadedMods())
            {
                Log.Out("  - {0} ({1})",
                    mod.ModInfo.Name.Value,
                    mod.ModInfo.Version.Value);
            }
            Log.Warning("You died {0} time(s) to beat this challenge", player.Died);
            Log.Warning("Please be honest and don't cheat with your results :)");
            Log.Warning("Feel proud that you have survived the dishong tower challenge");
            // LocalPlayerUI.primaryUI.windowManager.OpenIfNotOpen(GUIWindowConsole.ID, false);
            player.PlayerUI.windowManager.OpenIfNotOpen("DishongTowerChallange", true);
            reportedWinOnce = true;
        }
    }

    // Ensure block are not built outside/ground
    [HarmonyPatch(typeof(World))]
    [HarmonyPatch("CanPlaceBlockAt")]
    public class CanPlaceBlockAt
    {
        static void Postfix(
            Vector3i blockPos,
            ref bool __result)
        {
            if (!Enabled) return;
            // Used to determine exact tower positions
            // Log.Out("Check CanPlaceAt {0}", blockPos);
            if (InPrefabCopy) return;
            if (__result == false) return;
            // Only allow if two above ground
            __result = blockPos.y > Ground + 1
                || IsInSafeArea(blockPos);
        }
    }

    // Enforce build rules (if CanPlace does not work)
    // [HarmonyPatch(typeof(GameManager))]
    // [HarmonyPatch("SetBlocksRPC")]
    // public class SetBlocksRPC
    // {
    //     static void Prefix(ref List<BlockChangeInfo> _changes)
    //     {
    //         if (!Enabled) return;
    //         if (InPrefabCopy) return;
    //         _changes.RemoveAll(change =>
    //         {
    //             if (!change.bChangeBlockValue) return false;
    //             if (change.blockValue.isair) return false;
    //             if (IsInSafeArea(change.pos)) return false;
    //             return change.pos.y < Ground;
    //         });
    //     }
    // }

    // Mark Ground as "Trader Protected"
    [HarmonyPatch(typeof(World))]
    [HarmonyPatch("IsWithinTraderArea")]
    public class IsWithinTraderArea
    {
        static bool Prefix(
            Vector3i _worldBlockPos,
            ref bool __result)
        {
            if (!Enabled) return true;
            if (_worldBlockPos.y >= Ground) return true;
            __result = true;
            return false;
        }
    }
    
    // Needed to allow prefabs to add any blocks
    [HarmonyPatch(typeof(Prefab))]
    [HarmonyPatch("CopyBlocksIntoChunkNoEntities")]
    public class CopyBlocksIntoChunkNoEntities
    {
        static void Prefix()
        {
            InPrefabCopy = true;
        }
        static void Postfix()
        {
            InPrefabCopy = false;
        }
    }

}
