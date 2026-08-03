using System;
using UnityEngine;

namespace Fields.Save
{
    /// <summary>
    /// Persisted personal-best records. Stored in records.json — separate from the
    /// save file so records survive New Game and save deletion (spec §8.1).
    /// </summary>
    [Serializable]
    public class RecordsData
    {
        // Fastest time-in-parcel for each parcel (seconds). -1 = not yet achieved.
        public float[] fastestParcelSeconds = { -1f, -1f, -1f, -1f };

        // Largest area cut across a single play session (m²).
        public double largestAreaCutM2;

        // Most bales sold in a single delivery trip.
        public int mostBalesDelivered;

        // Longest unbroken cutting streak (seconds; gap > 3 s breaks it).
        public float longestCuttingStreakSeconds;

        // Total time from first cell cut to 4th parcel completion. -1 = not yet achieved.
        public float fullGameCompletionSeconds = -1f;
    }
}
