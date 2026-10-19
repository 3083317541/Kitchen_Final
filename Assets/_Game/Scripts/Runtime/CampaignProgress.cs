using UnityEngine;

namespace Kitchen.Runtime
{
    /// <summary>
    /// Which level the campaign is on.
    ///
    /// The main menu, the kitchen scene and the level clear panel run as separate scenes, so they need
    /// one shared number. A static holder is the smallest thing that survives a scene load without
    /// putting another persist-across-scenes object into the scene.
    ///
    /// Cleared-level rewards are recorded here for display, but they are not added on top of the next
    /// level: each level asset already describes the kitchen it should have, including anything the
    /// previous level promised. Adding both would double count, so the level asset stays the single
    /// source of truth for what a level contains.
    ///
    /// Nothing is written to disk: closing the game restarts the campaign, which is what an unsaved
    /// demo should do.
    /// </summary>
    public static class CampaignProgress
    {
        /// <summary>Level the kitchen scene should load, 1-based.</summary>
        public static int LevelNumber { get; private set; } = 1;

        /// <summary>Highest level reached, used by the menu to show progress.</summary>
        public static int HighestReached { get; private set; } = 1;

        /// <summary>Cookers promised by cleared levels. Recorded for display.</summary>
        public static System.Collections.Generic.List<string> UnlockedCookerIds { get; } =
            new System.Collections.Generic.List<string>();

        /// <summary>Restarts the campaign from level one.</summary>
        public static void Reset()
        {
            LevelNumber = 1;
            HighestReached = 1;
            UnlockedCookerIds.Clear();
        }

        /// <summary>Jumps to a level. Used by the menu.</summary>
        public static void JumpTo(int levelNumber)
        {
            LevelNumber = Mathf.Max(1, levelNumber);
            HighestReached = Mathf.Max(HighestReached, LevelNumber);
        }

        /// <summary>
        /// Records a cleared level's reward, advances the campaign and returns the level number now
        /// pending, or 0 when that was the last level.
        /// </summary>
        public static int ApplyClearReward(Data.LevelConfig cleared)
        {
            if (cleared == null)
            {
                return 0;
            }

            for (int i = 0; i < cleared.RewardCookerIds.Count; i++)
            {
                string id = cleared.RewardCookerIds[i];
                if (!string.IsNullOrEmpty(id) && !UnlockedCookerIds.Contains(id))
                {
                    UnlockedCookerIds.Add(id);
                }
            }

            int next = cleared.NextLevelNumber;
            if (next > 0)
            {
                LevelNumber = next;
                HighestReached = Mathf.Max(HighestReached, next);
            }

            return next;
        }

        /// <summary>Resources path of a level asset, e.g. "Kitchen/Level_02".</summary>
        public static string LevelResourcesPath(int levelNumber)
        {
            return $"Kitchen/Level_{Mathf.Max(1, levelNumber):00}";
        }
    }
}
