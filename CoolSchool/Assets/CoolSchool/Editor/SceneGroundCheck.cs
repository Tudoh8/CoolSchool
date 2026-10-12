using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Checks that the objects of the built world really touch the ground. For every environment object that is meant to stand
    /// on the ground (its bounds bottom is within 30 cm of it) it compares the bounds bottom with the ground directly under it,
    /// found by raycasting down onto the ground colliders. Anything more than 2 cm off is reported, worst first.
    /// Menu: CoolSchool / Check Scene Grounding. Batch:
    /// Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.SceneGroundCheck.CheckFromCommandLine (opens the saved scene).
    /// Log lines start with "SceneGround:".
    /// </summary>
    public static class SceneGroundCheck
    {
        const string LogPrefix = "SceneGround: ";
        const string ScenePath = "Assets/CoolSchool/Scenes/Playground.unity";
        const float Tolerance = 0.02f;
        /// <summary>Objects whose bottom is further than this from the ground are meant to be up in the air (windows, things on tables).</summary>
        const float Reach = 0.30f;
        const int ListLength = 25;

        /// <summary>Names of the groups whose colliders count as ground.</summary>
        static readonly string[] GroundGroups = { "Ground", "Floor" };

        [MenuItem("CoolSchool/Check Scene Grounding")]
        static void CheckFromMenu()
        {
            Run();
        }

        /// <summary>Opens the saved playground scene and checks it. Exits 0.</summary>
        public static void CheckFromCommandLine()
        {
            int code = 1;
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Run();
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        sealed class Finding
        {
            public string Path;
            public float Delta;
            public float Bottom;
            public float Ground;
        }

        /// <summary>Checks the open scene, logs the report, and returns the number of floating objects.</summary>
        internal static int Run()
        {
            Physics.SyncTransforms();
            HashSet<Collider> ground = CollectGround(out int groundCount);
            if (ground.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "no ground colliders found (nothing under a group named Ground or Floor).");
                return 0;
            }

            var seen = new HashSet<GameObject>();
            var floating = new List<Finding>();
            var sinking = new List<Finding>();
            int checkedCount = 0;
            int skippedAir = 0;
            int noGround = 0;

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!(renderer is MeshRenderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (IsGroundPart(renderer.transform)) continue;
                if (renderer.GetComponentInParent<Animator>() != null) continue; // characters
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                GameObject item = ObjectOf(renderer.gameObject);
                if (!seen.Add(item)) continue;

                if (!TryGetBounds(item, out Bounds bounds)) continue;
                if (bounds.size.y < 0.02f) continue; // flat decals and rings drawn on the ground

                if (!TryGroundHeight(ground, bounds, out float groundY))
                {
                    noGround++;
                    continue;
                }

                float delta = bounds.min.y - groundY;
                if (delta > Reach || delta < -Reach)
                {
                    skippedAir++;
                    continue;
                }

                checkedCount++;
                var finding = new Finding { Path = PathOf(item.transform), Delta = delta, Bottom = bounds.min.y, Ground = groundY };
                if (delta > Tolerance + 0.001f) floating.Add(finding);
                else if (delta < -Tolerance - 0.001f) sinking.Add(finding);
            }

            var report = new StringBuilder();
            report.AppendLine($"{LogPrefix}{checkedCount} objects stand on the ground ({groundCount} ground colliders); "
                + $"{floating.Count} float more than {Tolerance * 100f:0} cm above it, {sinking.Count} sink more than {Tolerance * 100f:0} cm into it; "
                + $"{skippedAir} are higher or lower by design (more than {Reach * 100f:0} cm), {noGround} have no ground under them.");

            AppendList(report, "FLOATING (worst first)", floating.OrderByDescending(f => f.Delta));
            AppendList(report, "SINKING (worst first)", sinking.OrderBy(f => f.Delta));
            AppendGroundHeights(report, ground);
            AppendPlayer(report, ground);

            Debug.Log(report.ToString());
            return floating.Count;
        }

        // ----- Finding things -------------------------------------------------------------------

        static HashSet<Collider> CollectGround(out int count)
        {
            var set = new HashSet<Collider>();
            foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.isTrigger || !collider.gameObject.activeInHierarchy || !collider.enabled) continue;
                if (IsGroundPart(collider.transform)) set.Add(collider);
            }
            count = set.Count;
            return set;
        }

        static bool IsGroundPart(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
                foreach (string group in GroundGroups)
                    if (p.name == group) return true;
            return false;
        }

        /// <summary>The thing a person would call one object: its prefab instance, or the renderer's own object.</summary>
        static GameObject ObjectOf(GameObject renderer)
        {
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(renderer);
            return root != null ? root : renderer;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null && parts.Count < 4; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static bool TryGetBounds(GameObject item, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(false))
            {
                if (!(renderer is MeshRenderer) || !renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// The ground height under an object: lumpy ground has tufts, so a dense set of rays is cast over the footprint (widened to
        /// at least a metre, as a thin post only touches a few) and the height most of them agree on is taken.
        /// </summary>
        static bool TryGroundHeight(HashSet<Collider> ground, Bounds bounds, out float groundY)
        {
            groundY = 0f;
            const int steps = 6;
            const float minHalf = 0.5f;
            float halfX = Mathf.Max(bounds.extents.x, minHalf);
            float halfZ = Mathf.Max(bounds.extents.z, minHalf);
            float startY = bounds.min.y + Reach;

            var bins = new Dictionary<int, List<float>>();
            for (int ix = 0; ix < steps; ix++)
            {
                for (int iz = 0; iz < steps; iz++)
                {
                    float x = bounds.center.x + Mathf.Lerp(-halfX, halfX, (ix + 0.5f) / steps);
                    float z = bounds.center.z + Mathf.Lerp(-halfZ, halfZ, (iz + 0.5f) / steps);
                    if (!TryGroundAt(ground, x, z, startY, out float y)) continue;

                    int bin = Mathf.RoundToInt(y / 0.01f);
                    if (!bins.TryGetValue(bin, out List<float> list)) bins[bin] = list = new List<float>();
                    list.Add(y);
                }
            }
            if (bins.Count == 0) return false;

            // The fullest bin and its neighbours; ties go to the higher one.
            int bestBin = 0;
            int bestCount = -1;
            foreach (KeyValuePair<int, List<float>> pair in bins)
            {
                int count = pair.Value.Count + (bins.TryGetValue(pair.Key - 1, out var below) ? below.Count : 0)
                    + (bins.TryGetValue(pair.Key + 1, out var above) ? above.Count : 0);
                if (count > bestCount || (count == bestCount && pair.Key > bestBin))
                {
                    bestCount = count;
                    bestBin = pair.Key;
                }
            }

            var chosen = new List<float>();
            for (int b = bestBin - 1; b <= bestBin + 1; b++)
                if (bins.TryGetValue(b, out List<float> list)) chosen.AddRange(list);
            groundY = chosen.Average();
            return true;
        }

        static bool TryGroundAt(HashSet<Collider> ground, float x, float z, float startY, out float y)
        {
            y = 0f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, startY, z), Vector3.down, startY + 50f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (!ground.Contains(hit.collider)) continue;
                if (hit.point.y > best)
                {
                    best = hit.point.y;
                    found = true;
                }
            }
            y = best;
            return found;
        }

        // ----- Report ---------------------------------------------------------------------------

        static void AppendList(StringBuilder report, string title, IEnumerable<Finding> findings)
        {
            List<Finding> list = findings.ToList();
            report.AppendLine($"{LogPrefix}--- {title}: {list.Count} ---");
            foreach (Finding f in list.Take(ListLength))
                report.AppendLine($"{LogPrefix}{f.Delta * 100f:+0.0;-0.0} cm  {f.Path}  (bottom y {f.Bottom:0.000}, ground y {f.Ground:0.000})");
            if (list.Count > ListLength) report.AppendLine($"{LogPrefix}... and {list.Count - ListLength} more.");
        }

        /// <summary>The ground itself: its height at a few spots, so a tile that sits lower than 0 shows up.</summary>
        static void AppendGroundHeights(StringBuilder report, HashSet<Collider> ground)
        {
            report.AppendLine($"{LogPrefix}--- ground height at sample spots (the walking surface should be 0.000) ---");
            var spots = new[]
            {
                new Vector2(0f, -16.9f), new Vector2(0f, 4f), new Vector2(3f, 3f), new Vector2(-12f, 0f), new Vector2(12f, 0f),
                new Vector2(0f, 12f), new Vector2(0f, 30f), new Vector2(25f, 5f), new Vector2(-25f, 5f),
            };
            foreach (Vector2 spot in spots)
            {
                if (TryGroundAt(ground, spot.x, spot.y, 5f, out float y))
                    report.AppendLine($"{LogPrefix}ground at ({spot.x:0.0}, {spot.y:0.0}): y {y:0.000}");
            }
        }

        static void AppendPlayer(StringBuilder report, HashSet<Collider> ground)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            Vector3 feet = player.transform.position;
            bool hasGround = TryGroundAt(ground, feet.x, feet.z, feet.y + 1f, out float groundY);
            report.AppendLine($"{LogPrefix}player feet y {feet.y:0.000}, ground under the player {(hasGround ? groundY.ToString("0.000") : "none")}.");

            if (NavMesh.SamplePosition(new Vector3(feet.x, groundY, feet.z), out NavMeshHit hit, 1f, NavMesh.AllAreas))
                report.AppendLine($"{LogPrefix}NavMesh height under the player y {hit.position.y:0.000}.");
        }
    }
}
