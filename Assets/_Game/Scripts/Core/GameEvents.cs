using System;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Lightweight static event bus for gameplay → statistics wiring.
    /// Gameplay systems fire into here; StatisticsTracker subscribes.
    /// All events carry playerId so co-op attribution works from day one.
    /// In single-player playerId is always 0.
    /// </summary>
    public static class GameEvents
    {
        // parcelId, playerId, cellAreaM2
        public static event Action<int, int, float> OnGridCellCut;

        // parcelId, worldPos
        public static event Action<int, Vector3> OnHayPileSpawned;

        // parcelId, playerId (fired when hay is consumed into a bale)
        public static event Action<int, int> OnHayConsumed;

        // parcelId, playerId, isRound
        public static event Action<int, int, bool> OnBaleCreated;

        // parcelId, playerId, value
        public static event Action<int, int, int> OnBaleSold;

        // playerId, toolIndex
        public static event Action<int, int> OnToolPurchased;

        // playerId, toolIndex, newLevel
        public static event Action<int, int, int> OnToolUpgraded;

        // parcelId, cost
        public static event Action<int, int> OnParcelUnlocked;

        // parcelId, playerId
        public static event Action<int, int> OnParcelEntered;
        public static event Action<int, int> OnParcelExited;

        // parcelId, timeSpentInParcelSeconds
        public static event Action<int, float> OnParcelCompleted;

        // totalPlaytimeSeconds (all 4 parcels done)
        public static event Action<float> OnFullGameCompleted;

        // ------------------------------------------------------------------ //
        // Fire helpers — single null-check per call site
        // ------------------------------------------------------------------ //

        public static void FireGridCellCut(int parcelId, int playerId, float cellAreaM2)
            => OnGridCellCut?.Invoke(parcelId, playerId, cellAreaM2);

        public static void FireHayPileSpawned(int parcelId, Vector3 pos)
            => OnHayPileSpawned?.Invoke(parcelId, pos);

        public static void FireHayConsumed(int parcelId, int playerId)
            => OnHayConsumed?.Invoke(parcelId, playerId);

        public static void FireBaleCreated(int parcelId, int playerId, bool isRound)
            => OnBaleCreated?.Invoke(parcelId, playerId, isRound);

        public static void FireBaleSold(int parcelId, int playerId, int value)
            => OnBaleSold?.Invoke(parcelId, playerId, value);

        public static void FireToolPurchased(int playerId, int toolIndex)
            => OnToolPurchased?.Invoke(playerId, toolIndex);

        public static void FireToolUpgraded(int playerId, int toolIndex, int newLevel)
            => OnToolUpgraded?.Invoke(playerId, toolIndex, newLevel);

        public static void FireParcelUnlocked(int parcelId, int cost)
            => OnParcelUnlocked?.Invoke(parcelId, cost);

        public static void FireParcelEntered(int parcelId, int playerId)
            => OnParcelEntered?.Invoke(parcelId, playerId);

        public static void FireParcelExited(int parcelId, int playerId)
            => OnParcelExited?.Invoke(parcelId, playerId);

        public static void FireParcelCompleted(int parcelId, float timeSpentSeconds)
            => OnParcelCompleted?.Invoke(parcelId, timeSpentSeconds);

        public static void FireFullGameCompleted(float totalPlaytimeSeconds)
            => OnFullGameCompleted?.Invoke(totalPlaytimeSeconds);
    }
}