using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class DishongTowerChallengeEnforcer : IModApi
{

    static int Ground = 61;
    static int HurtPerIv = 3;
    static float Interval = 0.25f;
    
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    static float Waited = 0;
    static bool InPrefabCopy = false;

    static bool IsInsideTower(Vector3i pos)
    {
        return pos.x >= -139 && pos.x <= -103
            && pos.z >= -380 && pos.z <= -344;
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

            // Give 90 seconds to enter tower
            if (age < 90.0) return;

            // Get Block position of player
            Vector3i pos = __instance.GetBlockPosition();

            // Allow to move inside tower
            if (IsInsideTower(pos)) return;

            // Allow to move above ground level
            if (pos.y > Ground) return;

            // Otherwise hurt the player constantly until he is back in or dies
            // Add a Grace Period for when you are just starting out
            __instance.DamageEntity(DamageSource.radiation,
                age < 150.0 ? 1 : HurtPerIv, false, 1f);
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
            if (InPrefabCopy) return;
            if (__result == false) return;
            // Only allow if two above ground
            __result = blockPos.y > Ground + 1
                || IsInsideTower(blockPos);
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
                if (IsInsideTower(change.pos)) return false;
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
