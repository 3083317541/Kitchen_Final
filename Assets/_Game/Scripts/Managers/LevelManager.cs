using Kitchen.Data;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Level
{
    /// <summary>
    /// Holds the level configuration the kitchen should be built from and offers a restart.
    ///
    /// It is a thin holder, not a second game entry point: GameRuntime still owns the flow. The
    /// component exists so a level can be swapped from the inspector without editing GameRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelManager : MonoBehaviour
    {
        [Header("Levels")]
        [Tooltip("Level used at start. Mirrors the value on GameRuntime when both are set.")]
        [SerializeField] private LevelConfig activeLevel;

        [Tooltip("Levels that can be selected at runtime by index.")]
        [SerializeField] private LevelConfig[] availableLevels = new LevelConfig[0];

        [Header("Runtime")]
        [SerializeField] private GameRuntime runtime;

        /// <summary>Level currently in use.</summary>
        public LevelConfig ActiveLevel => activeLevel;

        /// <summary>All selectable levels.</summary>
        public LevelConfig[] AvailableLevels => availableLevels;

        /// <summary>Number of selectable levels.</summary>
        public int LevelCount => availableLevels != null ? availableLevels.Length : 0;

        /// <summary>Injects the runtime so the level can be applied without a scene search.</summary>
        public void Initialize(GameRuntime gameRuntime)
        {
            runtime = gameRuntime;
        }

        /// <summary>Returns a level by index, or null when the index is out of range.</summary>
        public LevelConfig GetLevel(int index)
        {
            if (availableLevels == null || index < 0 || index >= availableLevels.Length)
            {
                return null;
            }

            return availableLevels[index];
        }

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<GameRuntime>();
            }
        }

        /// <summary>True when a level has been assigned.</summary>
        public bool HasLevel => activeLevel != null;
    }
}
