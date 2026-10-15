using System.Collections.Generic;
using Kitchen.Cookers;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.UI;
using Kitchen.Work;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Builds the whole Kitchen scene hierarchy.
    ///
    /// The scene is generated rather than hand authored so that every reference is resolved in code:
    /// no serialized field is left empty, and re-running the generator produces a clean, identical
    /// scene. Layout numbers in <see cref="KitchenUIBuilder"/> are plain constants so the UI can be
    /// adjusted in one place.
    /// </summary>
    public static class KitchenSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Kitchen.unity";

        /// <summary>
        /// Camera tilt below the horizon. This keeps a clear top down reading while still showing the
        /// vertical faces of the cookers, chefs and floor, which is what makes the view read as 3D
        /// instead of as a flat plan.
        /// </summary>
        public const float DefaultCameraPitch = 55f;

        /// <summary>Vertical field of view of the perspective camera.</summary>
        public const float DefaultCameraFieldOfView = 50f;

        /// <summary>
        /// Extra room around the map so it never touches the edge of the screen.
        ///
        /// The framing solves for the exact distance that fits the map, and this factor is applied on
        /// top. At 1.26 the outermost map corner sits at roughly 0.8 of the half frame instead of on
        /// the edge, which leaves a comfortable border of floor and background around the kitchen.
        /// </summary>
        public const float CameraMargin = 1.26f;

        /// <summary>
        /// The window shape the scene is framed for.
        ///
        /// The camera's own aspect ratio is forced to this value, so the projection saved in the scene
        /// is deterministic and the map provably fits. Unity fits the resulting view into whichever
        /// window the player actually has, which is far more robust than baking a distance that
        /// depends on the editor's window at generation time. 4:3 is the narrowest supported shape, so
        /// any wider window sees the map with margin to spare.
        /// </summary>
        public const float CameraFramingAspect = 4f / 3f;

        /// <summary>
        /// Minimum viewport coordinate a map corner may reach. A corner at 0 or 1 is exactly on the
        /// edge of the screen, so this is the border the framing must preserve.
        /// </summary>
        public const float CameraViewportMargin = 0.04f;

        // ------------------------------------------------------------------ entry point

        /// <summary>
        /// Creates the full Kitchen hierarchy in the active scene and returns the runtime component.
        /// </summary>
        /// <param name="level">Level the runtime loads.</param>
        /// <param name="strategies">Work strategies stored on the Systems object.</param>
        /// <param name="font">Font asset shared by every label.</param>
        public static GameRuntime Build(LevelConfig level, IList<CookerWorkStrategy> strategies, TMP_FontAsset font)
        {
            Camera camera = CreateCamera();
            CreateDirectionalLight();

            GameRuntime runtime = CreateGameRuntime(level, camera);
            GameObject systems = CreateSystems(strategies);
            GameObject world = CreateWorld();
            GameObject canvas = KitchenUIBuilder.BuildCanvas(font);

            WireRuntime(runtime, systems, world, camera);
            KitchenUIBuilder.WireUI(runtime.GetComponent<UIManager>(), canvas, font);

            // Author a visible preview of the level so the Scene view shows the kitchen instead of
            // empty roots. The runtime removes these objects and builds its own.
            KitchenScenePreview.Build(
                level,
                world.transform.Find("GridRoot"),
                world.transform.Find("CookerRoot"),
                world.transform.Find("StaffRoot"));

            // The camera is framed with the level's real dimensions, so the scene is correct the
            // moment it is opened and no first frame guess is involved.
            FrameCamera(camera, level, DefaultCameraPitch);
            return runtime;
        }

        // ------------------------------------------------------------------ camera

        /// <summary>
        /// Places a perspective camera on a raised three quarter top down view and sizes its field of
        /// view so the whole map is inside the frustum, with margin, on any aspect ratio.
        /// </summary>
        public static void FrameCamera(Camera camera, LevelConfig level, float pitchDegrees)
        {
            if (camera == null || level == null)
            {
                Debug.LogWarning(
                    "[Kitchen] The camera could not be framed because " +
                    (camera == null ? "the camera" : "the level") + " is missing, so the view is not guaranteed " +
                    "to show the whole map.");
                return;
            }

            // The projected aspect is pinned so the framing baked into the scene does not depend on
            // the size of the editor window at generation time.
            camera.aspect = CameraFramingAspect;

            CameraFraming framing = ComputeFraming(level, pitchDegrees, CameraFramingAspect);
            ApplyFraming(camera, framing);
        }

        /// <summary>
        /// Projects the map corners through the camera and reports the ones outside the viewport.
        /// Kept for interactive diagnosis; the acceptance suite uses the arithmetic overload so it
        /// does not need a graphics device.
        /// </summary>
        public static List<string> FindCornersOutsideView(Camera camera, LevelConfig level, float margin = 0.02f)
        {
            List<string> failures = new List<string>();

            if (camera == null || level == null)
            {
                failures.Add("camera or level is null");
                return failures;
            }

            CameraFraming framing = new CameraFraming
            {
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
                FieldOfView = camera.orthographic ? 60f : camera.fieldOfView,
                Aspect = camera.aspect
            };

            if (camera.orthographic)
            {
                failures.Add("the camera is orthographic; the demo expects a perspective view");
                return failures;
            }

            List<CornerProjection> projections = ProjectMapCorners(framing, level, margin);
            for (int i = 0; i < projections.Count; i++)
            {
                if (!projections[i].InsideViewport(margin))
                {
                    failures.Add($"corner {projections[i].World} projects to " +
                        $"({projections[i].Viewport.x:0.000}, {projections[i].Viewport.y:0.000})");
                }
            }

            return failures;
        }

        private static Camera CreateCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = DefaultCameraFieldOfView;
            camera.transform.position = new Vector3(0f, 10f, -12f);
            camera.transform.rotation = Quaternion.Euler(DefaultCameraPitch, 0f, 0f);

            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateDirectionalLight()
        {
            GameObject key = new GameObject("Directional Light");
            Light keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.96f, 0.89f, 1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.75f;
            key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            key.transform.position = new Vector3(0f, 9f, 0f);

            // A low fill light keeps the shadowed sides of the cookers readable instead of black.
            GameObject fill = new GameObject("Fill Light");
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.62f, 0.72f, 0.88f, 1f);
            fillLight.intensity = 0.35f;
            fillLight.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        }

        /// <summary>Result of projecting a map corner.</summary>
        public struct CornerProjection
        {
            public Vector3 World;
            public Vector3 Viewport;
            public bool InFrontOfCamera;

            public bool InsideViewport(float margin)
            {
                return InFrontOfCamera
                    && Viewport.x >= margin && Viewport.x <= 1f - margin
                    && Viewport.y >= margin && Viewport.y <= 1f - margin;
            }
        }

        /// <summary>
        /// The raised three quarter top down camera the demo is designed around:
        /// position and rotation for the map, plus the two projection settings.
        /// </summary>
        public struct CameraFraming
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
            public float Aspect;

            /// <summary>Distance from the map centre along the view axis.</summary>
            public float Distance;

            public Vector3 Forward => Rotation * Vector3.forward;

            public Vector3 Right => Rotation * Vector3.right;

            public Vector3 Up => Rotation * Vector3.up;
        }

        /// <summary>
        /// Computes the framing for a map. The distance is solved exactly so that every corner of the
        /// map's bounding box lands inside the view frustum, which works at any aspect ratio and any
        /// pitch. It is pure arithmetic, so the same values can be checked without a Camera instance
        /// and therefore without a graphics device.
        /// </summary>
        public static CameraFraming ComputeFraming(LevelConfig level, float pitchDegrees, float aspect)
        {
            CameraFraming framing = new CameraFraming
            {
                FieldOfView = DefaultCameraFieldOfView,
                Aspect = aspect > 0.01f ? aspect : 16f / 9f
            };

            if (level == null)
            {
                return framing;
            }

            float pitch = Mathf.Clamp(pitchDegrees, 25f, 89f);

            Vector3 center = new Vector3(level.MapCenter.x, level.MapCenter.y, level.MapCenter.z);
            float halfWidth = level.Width * level.CellSize * 0.5f;
            float halfHeight = level.Height * level.CellSize * 0.5f;

            // Height of the tallest thing that can stand on the floor. Cookers and chefs are under two
            // units, so a little headroom keeps nothing clipped.
            const float contentHeight = 2.5f;

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            float tanVertical = Mathf.Tan(DefaultCameraFieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * aspect;

            // Exact fit: the camera sits on the +forward axis and the map is fixed, so for a point p
            // the distance along the view axis is e = D + dot(center - p, forward) and the viewport
            // offset is u = dot(p - center, right) / (e * tanHorizontal), v likewise. Requiring
            // |u| <= 1 and |v| <= 1 turns into D >= A / t - B, evaluated for every corner of the map's
            // bounding box. Taking the maximum gives the smallest distance that shows all of it, which
            // is a much tighter fit than a bounding sphere and works at any aspect ratio or pitch.
            float minimumDistance = 0f;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = new Vector3(
                            center.x + x * halfWidth,
                            center.y + y * contentHeight,
                            center.z + z * halfHeight);

                        Vector3 offset = corner - center;
                        float b = Vector3.Dot(offset, forward);
                        float cRight = Vector3.Dot(offset, right);
                        float cUp = Vector3.Dot(offset, up);

                        minimumDistance = Mathf.Max(minimumDistance, Mathf.Abs(cRight) / tanHorizontal - b);
                        minimumDistance = Mathf.Max(minimumDistance, Mathf.Abs(cUp) / tanVertical - b);
                    }
                }
            }

            float distance = minimumDistance * CameraMargin;

            framing.Rotation = rotation;
            framing.Position = center - forward * distance;
            framing.Distance = distance;
            return framing;
        }

        /// <summary>Places a camera using a precomputed framing.</summary>
        public static void ApplyFraming(Camera camera, CameraFraming framing)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographic = false;
            camera.fieldOfView = framing.FieldOfView;
            camera.aspect = framing.Aspect;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = framing.Distance * 2f + 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.062f, 0.078f, 1f);
            camera.transform.rotation = framing.Rotation;
            camera.transform.position = framing.Position;
        }

        /// <summary>
        /// Projects the four map corners with a framing. Works without a Camera instance, so the
        /// "is the whole map visible" question can be answered analytically.
        /// </summary>
        public static List<CornerProjection> ProjectMapCorners(CameraFraming framing, LevelConfig level, float margin, float aspectOverride = 0f)
        {
            List<CornerProjection> projections = new List<CornerProjection>(4);

            if (level == null)
            {
                return projections;
            }

            float aspect = aspectOverride > 0.01f ? aspectOverride : framing.Aspect;
            float halfWidth = level.Width * level.CellSize * 0.5f;
            float halfHeight = level.Height * level.CellSize * 0.5f;
            const float contentHeight = 2.5f;
            float verticalHalfAngle = framing.FieldOfView * 0.5f * Mathf.Deg2Rad;

            Vector3 right = framing.Right;
            Vector3 up = framing.Up;
            Vector3 forward = framing.Forward;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = new Vector3(
                            level.MapCenter.x + x * halfWidth,
                            level.MapCenter.y + y * contentHeight,
                            level.MapCenter.z + z * halfHeight);

                        Vector3 relative = corner - framing.Position;
                        float forwardDistance = Vector3.Dot(relative, forward);

                        CornerProjection projection = new CornerProjection
                        {
                            World = corner,
                            InFrontOfCamera = forwardDistance > 0.01f
                        };

                        if (projection.InFrontOfCamera)
                        {
                            float halfHeightAtCorner = forwardDistance * Mathf.Tan(verticalHalfAngle);
                            float halfWidthAtCorner = halfHeightAtCorner * aspect;

                            float u = Vector3.Dot(relative, right) / halfWidthAtCorner;
                            float v = Vector3.Dot(relative, up) / halfHeightAtCorner;

                            // Viewport space: 0 at the left/bottom edge, 1 at the right/top edge.
                            projection.Viewport = new Vector3(0.5f + u * 0.5f, 0.5f + v * 0.5f, forwardDistance);
                        }

                        projections.Add(projection);
                    }
                }
            }

            return projections;
        }

        /// <summary>
        /// Finds the Main Camera inside a loaded scene, so checks can be run against the camera that
        /// was actually written to disk instead of a stand-in created by the test.
        /// </summary>
        public static Camera FindSceneMainCamera(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cameras.Length; c++)
                {
                    if (cameras[c] != null && cameras[c].CompareTag("MainCamera"))
                    {
                        return cameras[c];
                    }
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ Game_Runtime

        private static GameRuntime CreateGameRuntime(LevelConfig level, Camera camera)
        {
            GameObject go = new GameObject("Game_Runtime");
            GameRuntime runtime = go.AddComponent<GameRuntime>();
            go.AddComponent<Kitchen.Input.InputManager>();
            go.AddComponent<UIManager>();
            go.AddComponent<Kitchen.Level.LevelManager>();

            SerializedObject so = new SerializedObject(runtime);
            so.FindProperty("levelConfig").objectReferenceValue = level;
            so.FindProperty("loadFromResourcesWhenEmpty").boolValue = true;
            so.FindProperty("resourcesLevelPath").stringValue = "Kitchen/Level_01";
            so.FindProperty("initialTimeScale").floatValue = 1f;
            so.FindProperty("staffMoveSpeed").floatValue = 6f;
            so.FindProperty("plateReturnMinSeconds").floatValue = GameContentFactory.DefaultPlateReturnMinSeconds;
            so.FindProperty("plateReturnMaxSeconds").floatValue = GameContentFactory.DefaultPlateReturnMaxSeconds;
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();

            Kitchen.Level.LevelManager levelManager = go.GetComponent<Kitchen.Level.LevelManager>();
            SerializedObject levelSo = new SerializedObject(levelManager);
            levelSo.FindProperty("activeLevel").objectReferenceValue = level;
            SerializedProperty levels = levelSo.FindProperty("availableLevels");
            levels.arraySize = 1;
            levels.GetArrayElementAtIndex(0).objectReferenceValue = level;
            levelSo.FindProperty("runtime").objectReferenceValue = runtime;
            levelSo.ApplyModifiedPropertiesWithoutUndo();

            return runtime;
        }

        private static GameObject CreateSystems(IList<CookerWorkStrategy> strategies)
        {
            GameObject systems = new GameObject("Systems");
            systems.AddComponent<Kitchen.Grid.GridManager>();
            systems.AddComponent<CookerManager>();
            systems.AddComponent<Kitchen.Staff.StaffManager>();
            systems.AddComponent<WorkManager>();
            systems.AddComponent<Kitchen.Inventory.InventoryManager>();
            systems.AddComponent<Kitchen.Recipes.RecipeManager>();
            systems.AddComponent<Kitchen.Orders.OrderManager>();
            systems.AddComponent<Kitchen.Orders.PlateReturnManager>();

            WorkStrategyHolder holder = systems.AddComponent<WorkStrategyHolder>();
            AssignWorkStrategies(holder, strategies);

            return systems;
        }

        /// <summary>Stores the work strategy assets on the holder so WorkManager can find them.</summary>
        public static void AssignWorkStrategies(WorkStrategyHolder holder, IList<CookerWorkStrategy> strategies)
        {
            if (holder == null || strategies == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(holder);
            SerializedProperty array = so.FindProperty("strategies");
            array.arraySize = strategies.Count;
            for (int i = 0; i < strategies.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = strategies[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateWorld()
        {
            GameObject world = new GameObject("World");
            CreateChild(world.transform, "Floor");
            CreateChild(world.transform, "GridRoot");
            CreateChild(world.transform, "CookerRoot");
            CreateChild(world.transform, "StaffRoot");
            CreateChild(world.transform, "IngredientRoot");
            return world;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void WireRuntime(GameRuntime runtime, GameObject systems, GameObject world, Camera camera)
        {
            SerializedObject so = new SerializedObject(runtime);
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.FindProperty("inputManager").objectReferenceValue = runtime.GetComponent<Kitchen.Input.InputManager>();
            so.FindProperty("uiManager").objectReferenceValue = runtime.GetComponent<UIManager>();
            so.FindProperty("floorRoot").objectReferenceValue = world.transform.Find("Floor");
            so.FindProperty("gridRoot").objectReferenceValue = world.transform.Find("GridRoot");
            so.FindProperty("cookerRoot").objectReferenceValue = world.transform.Find("CookerRoot");
            so.FindProperty("staffRoot").objectReferenceValue = world.transform.Find("StaffRoot");
            so.FindProperty("ingredientRoot").objectReferenceValue = world.transform.Find("IngredientRoot");
            so.FindProperty("gridManager").objectReferenceValue = systems.GetComponent<Kitchen.Grid.GridManager>();
            so.FindProperty("cookerManager").objectReferenceValue = systems.GetComponent<CookerManager>();
            so.FindProperty("staffManager").objectReferenceValue = systems.GetComponent<Kitchen.Staff.StaffManager>();
            so.FindProperty("workManager").objectReferenceValue = systems.GetComponent<WorkManager>();
            so.FindProperty("inventoryManager").objectReferenceValue = systems.GetComponent<Kitchen.Inventory.InventoryManager>();
            so.FindProperty("recipeManager").objectReferenceValue = systems.GetComponent<Kitchen.Recipes.RecipeManager>();
            so.FindProperty("orderManager").objectReferenceValue = systems.GetComponent<Kitchen.Orders.OrderManager>();
            so.FindProperty("plateReturnManager").objectReferenceValue = systems.GetComponent<Kitchen.Orders.PlateReturnManager>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
