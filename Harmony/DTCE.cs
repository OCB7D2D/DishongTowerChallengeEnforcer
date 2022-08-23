using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class DishongTowerChallengeEnforcer : IModApi
{

    static readonly int Ground = 41;
    static readonly Vector2i TowerX = new Vector2i(11, 47);
    static readonly Vector2i TowerY = new Vector2i(11, 47);

    static readonly int HurtPerIv = 3;
    static readonly float Interval = 0.25f;
    
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
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


            // Wait for update interval
            Waited += Time.deltaTime;
            if (Waited < Interval) return;
            Waited -= Interval;

            // Get age of player for grace period
            ulong age = __instance.world.worldTime
                - __instance.WorldTimeBorn;

            // Calculate timed radiation level
            float radiation = age / 150f;
            radiation = Mathf.Max(0f, radiation);
            radiation = Mathf.Min(900f, radiation);

            // Get Block position of player
            Vector3i pos = __instance.GetBlockPosition();

            // Allow to move inside tower and above ground
            if (IsInSafeArea(pos) || pos.y > Ground ||
                // Allow to be outside when in/on a vehicles
                __instance.AttachedToEntity != null) radiation = 0;

            // Update the radiation level
            AllowRadiationCVar = true;
            __instance.Buffs.SetCustomVar(
                "_biomeradiation",
                radiation == 0 ? 0 : 255f);
            AllowRadiationCVar = false;

            // Otherwise hurt the player constantly until he is back in or dies
            // Add a Grace Period for when you are just starting out
            __instance.DamageEntity(DamageSource.radiation,
                (int)(Mathf.Min(HurtPerIv, radiation - 1)),
                false, 1f);
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
    [HarmonyPatch(typeof(GameManager))]
    [HarmonyPatch("SetBlocksRPC")]
    public class SetBlocksRPC
    {
        static void Prefix(ref List<BlockChangeInfo> _changes)
        {
            if (InPrefabCopy) return;
            _changes.RemoveAll(change =>
            {
                if (!change.bChangeBlockValue) return false;
                if (change.blockValue.isair) return false;
                if (IsInSafeArea(change.pos)) return false;
                return change.pos.y < Ground + 1;
            });
        }
    }

    // Mark Ground as "Trader Protected"
    [HarmonyPatch(typeof(World))]
    [HarmonyPatch("IsWithinTraderArea")]
    public class IsWithinTraderArea
    {
        static bool Prefix(
            Vector3i _worldBlockPos,
            ref bool __result)
        {
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
