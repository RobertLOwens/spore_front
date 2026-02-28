// ============================================================================
// FILE: Data/UserStats.cs
// PURPOSE: Data models for user lifetime statistics and game history entries.
//          Firestore-compatible with ToDictionary/FromDictionary serialization.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Sporefront.Data
{
    // ================================================================
    // User Stats
    // ================================================================

    [Serializable]
    public class UserStats
    {
        public int gamesPlayed;
        public int gamesWon;
        public int gamesLost;
        public double totalPlayTime;
        public int battlesWon;
        public int battlesLost;
        public int unitsKilled;
        public int unitsLost;
        public int buildingsBuilt;
        public int totalResourcesGathered;
        public int highestPopulation;
        public string lastUpdated;

        public UserStats()
        {
            lastUpdated = DateTime.UtcNow.ToString("o");
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "gamesPlayed", gamesPlayed },
                { "gamesWon", gamesWon },
                { "gamesLost", gamesLost },
                { "totalPlayTime", totalPlayTime },
                { "battlesWon", battlesWon },
                { "battlesLost", battlesLost },
                { "unitsKilled", unitsKilled },
                { "unitsLost", unitsLost },
                { "buildingsBuilt", buildingsBuilt },
                { "totalResourcesGathered", totalResourcesGathered },
                { "highestPopulation", highestPopulation },
                { "lastUpdated", lastUpdated }
            };
        }

        public static UserStats FromDictionary(Dictionary<string, object> data)
        {
            var stats = new UserStats();
            if (data == null) return stats;

            if (data.TryGetValue("gamesPlayed", out var gp)) stats.gamesPlayed = Convert.ToInt32(gp);
            if (data.TryGetValue("gamesWon", out var gw)) stats.gamesWon = Convert.ToInt32(gw);
            if (data.TryGetValue("gamesLost", out var gl)) stats.gamesLost = Convert.ToInt32(gl);
            if (data.TryGetValue("totalPlayTime", out var tpt)) stats.totalPlayTime = Convert.ToDouble(tpt);
            if (data.TryGetValue("battlesWon", out var bw)) stats.battlesWon = Convert.ToInt32(bw);
            if (data.TryGetValue("battlesLost", out var bl)) stats.battlesLost = Convert.ToInt32(bl);
            if (data.TryGetValue("unitsKilled", out var uk)) stats.unitsKilled = Convert.ToInt32(uk);
            if (data.TryGetValue("unitsLost", out var ul)) stats.unitsLost = Convert.ToInt32(ul);
            if (data.TryGetValue("buildingsBuilt", out var bb)) stats.buildingsBuilt = Convert.ToInt32(bb);
            if (data.TryGetValue("totalResourcesGathered", out var trg)) stats.totalResourcesGathered = Convert.ToInt32(trg);
            if (data.TryGetValue("highestPopulation", out var hp)) stats.highestPopulation = Convert.ToInt32(hp);
            if (data.TryGetValue("lastUpdated", out var lu)) stats.lastUpdated = lu?.ToString();

            return stats;
        }
    }

    // ================================================================
    // Game History Entry
    // ================================================================

    [Serializable]
    public class GameHistoryEntry
    {
        public string date;
        public bool isVictory;
        public string reason;
        public double duration;
        public int battlesWon;
        public int battlesLost;
        public int unitsKilled;
        public int unitsLost;
        public int buildingsBuilt;
        public int resourcesGathered;
        public int maxPopulation;

        public GameHistoryEntry()
        {
            date = DateTime.UtcNow.ToString("o");
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "date", date },
                { "isVictory", isVictory },
                { "reason", reason ?? "unknown" },
                { "duration", duration },
                { "battlesWon", battlesWon },
                { "battlesLost", battlesLost },
                { "unitsKilled", unitsKilled },
                { "unitsLost", unitsLost },
                { "buildingsBuilt", buildingsBuilt },
                { "resourcesGathered", resourcesGathered },
                { "maxPopulation", maxPopulation }
            };
        }

        public static GameHistoryEntry FromDictionary(Dictionary<string, object> data)
        {
            var entry = new GameHistoryEntry();
            if (data == null) return entry;

            if (data.TryGetValue("date", out var d)) entry.date = d?.ToString();
            if (data.TryGetValue("isVictory", out var iv)) entry.isVictory = Convert.ToBoolean(iv);
            if (data.TryGetValue("reason", out var r)) entry.reason = r?.ToString();
            if (data.TryGetValue("duration", out var dur)) entry.duration = Convert.ToDouble(dur);
            if (data.TryGetValue("battlesWon", out var bw)) entry.battlesWon = Convert.ToInt32(bw);
            if (data.TryGetValue("battlesLost", out var bl)) entry.battlesLost = Convert.ToInt32(bl);
            if (data.TryGetValue("unitsKilled", out var uk)) entry.unitsKilled = Convert.ToInt32(uk);
            if (data.TryGetValue("unitsLost", out var ul)) entry.unitsLost = Convert.ToInt32(ul);
            if (data.TryGetValue("buildingsBuilt", out var bb)) entry.buildingsBuilt = Convert.ToInt32(bb);
            if (data.TryGetValue("resourcesGathered", out var rg)) entry.resourcesGathered = Convert.ToInt32(rg);
            if (data.TryGetValue("maxPopulation", out var mp)) entry.maxPopulation = Convert.ToInt32(mp);

            return entry;
        }
    }
}
