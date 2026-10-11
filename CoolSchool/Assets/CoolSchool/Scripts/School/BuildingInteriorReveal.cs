using System.Collections.Generic;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoolSchool.School
{
    /// <summary>
    /// Keeps the fixed-angle follow camera from looking at a roof or through a wall.
    /// While the player is inside the building (or just outside its door), the roof is hidden; the wall
    /// pieces between a camera outside the building and the room are hidden too, but only while the
    /// camera really is outside, so the lesson camera inside still sees all four walls.
    /// The follow camera sits south of the player and looks north. The school lies north of the playground with
    /// its door in its south wall, so the camera-side wall is also the door wall; the approach area is the strip
    /// south of the door.
    /// All box values are in this object's local space, so the building may be turned (the school is): the
    /// approach direction is local, pointing from the room out through the door.
    /// </summary>
    public class BuildingInteriorReveal : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2;

        [Tooltip("Roof and ceiling pieces: hidden while the player is inside or at the door.")]
        [SerializeField] Renderer[] hideWhenInside;
        [Tooltip("Wall piece roots between the follow camera and the room: hidden while the player is inside and the camera is outside.")]
        [SerializeField] GameObject[] hideWhenCameraOutside;
        [SerializeField] Vector3 interiorCenter = new Vector3(0f, 1.5f, 0f);
        [SerializeField] Vector3 interiorSize = new Vector3(10f, 3f, 8f);
        [Tooltip("How far past the box, toward the door, the player already counts as arriving.")]
        [SerializeField] float approachDistance = 3f;
        [Tooltip("Local direction from the room out through the door: the side the approach area is on.")]
        [SerializeField] Vector3 approachDirection = Vector3.forward;
        [Tooltip("Off hides the pieces completely (brighter room). ShadowsOnly keeps their shadows.")]
        [SerializeField] ShadowCastingMode hiddenShadowMode = ShadowCastingMode.Off;

        PlayerController player;
        Camera viewCamera;

        ShadowCastingMode[] roofModes;
        Renderer[] wallRenderers;
        ShadowCastingMode[] wallModes;
        Transform[] wallTransforms;
        int[] wallLayers;

        bool roofHidden;
        bool wallsHidden;

        void Awake()
        {
            hideWhenInside = hideWhenInside ?? new Renderer[0];
            hideWhenCameraOutside = hideWhenCameraOutside ?? new GameObject[0];

            roofModes = new ShadowCastingMode[hideWhenInside.Length];
            for (int i = 0; i < hideWhenInside.Length; i++)
                if (hideWhenInside[i] != null) roofModes[i] = hideWhenInside[i].shadowCastingMode;

            var renderers = new List<Renderer>();
            var transforms = new List<Transform>();
            foreach (GameObject wall in hideWhenCameraOutside)
            {
                if (wall == null) continue;
                renderers.AddRange(wall.GetComponentsInChildren<Renderer>(true));
                transforms.AddRange(wall.GetComponentsInChildren<Transform>(true));
            }

            wallRenderers = renderers.ToArray();
            wallModes = new ShadowCastingMode[wallRenderers.Length];
            for (int i = 0; i < wallRenderers.Length; i++) wallModes[i] = wallRenderers[i].shadowCastingMode;

            wallTransforms = transforms.ToArray();
            wallLayers = new int[wallTransforms.Length];
            for (int i = 0; i < wallTransforms.Length; i++) wallLayers[i] = wallTransforms[i].gameObject.layer;
        }

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            if (viewCamera == null) viewCamera = Camera.main;

            Vector3 playerLocal = transform.InverseTransformPoint(player.transform.position);
            bool playerNear = ApproachBounds().Contains(OnBoxHeight(playerLocal));

            bool cameraInside = false;
            if (viewCamera != null)
            {
                Vector3 cameraLocal = transform.InverseTransformPoint(viewCamera.transform.position);
                cameraInside = new Bounds(interiorCenter, interiorSize).Contains(cameraLocal);
            }

            SetRoofHidden(playerNear);
            SetWallsHidden(playerNear && !cameraInside);
        }

        void OnDisable()
        {
            SetRoofHidden(false);
            SetWallsHidden(false);
        }

        /// <summary>The interior box, stretched toward the door by approachDistance.</summary>
        Bounds ApproachBounds()
        {
            Vector3 direction = approachDirection.normalized;
            Vector3 center = interiorCenter + direction * (approachDistance * 0.5f);
            Vector3 size = interiorSize + new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)) * approachDistance;
            return new Bounds(center, size);
        }

        /// <summary>Only the floor plan matters for the player, not how high the box is.</summary>
        Vector3 OnBoxHeight(Vector3 local)
        {
            return new Vector3(local.x, interiorCenter.y, local.z);
        }

        void SetRoofHidden(bool hidden)
        {
            if (hidden == roofHidden) return;
            roofHidden = hidden;

            for (int i = 0; i < hideWhenInside.Length; i++)
            {
                Renderer piece = hideWhenInside[i];
                if (piece == null) continue;
                ApplyVisibility(piece, hidden, roofModes[i]);
            }
        }

        void SetWallsHidden(bool hidden)
        {
            if (hidden == wallsHidden) return;
            wallsHidden = hidden;

            for (int i = 0; i < wallRenderers.Length; i++)
            {
                if (wallRenderers[i] != null) ApplyVisibility(wallRenderers[i], hidden, wallModes[i]);
            }

            // Hidden walls still stop the kid, but the camera's obstacle probe must look through them.
            for (int i = 0; i < wallTransforms.Length; i++)
            {
                if (wallTransforms[i] != null)
                    wallTransforms[i].gameObject.layer = hidden ? IgnoreRaycastLayer : wallLayers[i];
            }
        }

        void ApplyVisibility(Renderer piece, bool hidden, ShadowCastingMode originalMode)
        {
            if (!hidden)
            {
                piece.enabled = true;
                piece.shadowCastingMode = originalMode;
            }
            else if (hiddenShadowMode == ShadowCastingMode.Off)
            {
                piece.enabled = false;
            }
            else
            {
                piece.shadowCastingMode = hiddenShadowMode;
            }
        }
    }
}
