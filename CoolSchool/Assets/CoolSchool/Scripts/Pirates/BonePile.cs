using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// What a skeleton does when the sword bumps it: it falls apart into a heap of cartoon bones and a skull, they rattle on
    /// the ground, then they fly back and the skeleton is whole again. Never scary: it ends in a sparkle.
    /// The pieces are simple tossed shapes without colliders (they move by themselves), so they can never get in anyone's way.
    /// </summary>
    public class BonePile : MonoBehaviour
    {
        [Tooltip("Off-white URP/Lit.")]
        [SerializeField] Material boneMaterial;
        [SerializeField] int boneCount = 8;
        [SerializeField] float apartSeconds = 1.4f;
        [SerializeField] float rebuildSeconds = 0.7f;

        const float BoneLength = 0.35f;
        const float SkullSize = 0.25f;
        const float Gravity = -14f;

        sealed class Piece
        {
            public Transform Root;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Radius;
            public bool Resting;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly List<SkinnedMeshRenderer> hidden = new List<SkinnedMeshRenderer>();
        Transform poolRoot;
        Coroutine routine;
        Animator animator;
        WeaponHolder weapons;

        /// <summary>True while it is lying in pieces or flying back together.</summary>
        public bool IsApart { get; private set; }

        void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            weapons = GetComponent<WeaponHolder>();
        }

        void OnDisable()
        {
            // Switched off in the middle of it (the game ended): put the skeleton back together without a fuss.
            routine = null;
            RestoreVisuals();
        }

        void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot.gameObject);
        }

        /// <summary>Falls apart, rattles, and flies back together. rebuilt is called when the skeleton is whole again.</summary>
        public void FallApart(Action rebuilt)
        {
            if (routine != null) StopCoroutine(routine);
            RestoreVisuals();
            routine = StartCoroutine(FallApartRoutine(rebuilt));
        }

        /// <summary>Whole again straight away (reset between rounds).</summary>
        public void ResetPile()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            RestoreVisuals();
        }

        // ----- The routine -------------------------------------------------------------------------

        IEnumerator FallApartRoutine(Action rebuilt)
        {
            IsApart = true;
            EnsurePieces();

            Vector3 origin = HipsPosition();
            float floor = transform.position.y;
            HideBody();

            Sfx.Play("pirates.bones_rattle", origin);
            BattleEffects.Confetti(origin + Vector3.up * 0.3f);

            foreach (Piece piece in pieces)
            {
                piece.Root.gameObject.SetActive(true);
                piece.Root.localScale = Vector3.one;
                piece.Root.position = origin + UnityEngine.Random.insideUnitSphere * 0.15f;
                piece.Root.rotation = UnityEngine.Random.rotation;

                Vector2 sideways = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(1.2f, 2.6f);
                piece.Velocity = new Vector3(sideways.x, UnityEngine.Random.Range(3f, 5.5f), sideways.y);
                piece.Spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(180f, 520f);
                piece.Resting = false;
            }

            // Tossed about, bouncing a little on the floor.
            for (float elapsed = 0f; elapsed < apartSeconds; elapsed += Time.deltaTime)
            {
                foreach (Piece piece in pieces) Toss(piece, floor);
                yield return null;
            }

            // Back together: every piece flies to its place on the body.
            Sfx.Play("pirates.bones_rebuild", origin);
            var starts = new Vector3[pieces.Count];
            var startRotations = new Quaternion[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                starts[i] = pieces[i].Root.position;
                startRotations[i] = pieces[i].Root.rotation;
            }

            float duration = Mathf.Max(0.1f, rebuildSeconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                for (int i = 0; i < pieces.Count; i++)
                {
                    Piece piece = pieces[i];
                    piece.Root.position = Vector3.Lerp(starts[i], BodyPoint(i), k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.4f);
                    piece.Root.rotation = Quaternion.Slerp(startRotations[i], transform.rotation, k);
                    piece.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
                }
                yield return null;
            }

            RestoreVisuals();
            BattleEffects.Sparkle(origin + Vector3.up * 0.3f);
            BattleEffects.Sparkle(transform.position + Vector3.up * 1.2f);
            routine = null;
            rebuilt?.Invoke();
        }

        /// <summary>One step of a piece flying through the air and bouncing, with a little spin.</summary>
        void Toss(Piece piece, float floor)
        {
            if (piece.Resting) return;

            float dt = Time.deltaTime;
            piece.Velocity.y += Gravity * dt;
            Vector3 position = piece.Root.position + piece.Velocity * dt;

            float ground = floor + piece.Radius;
            if (position.y <= ground && piece.Velocity.y < 0f)
            {
                position.y = ground;
                piece.Velocity.y = -piece.Velocity.y * 0.35f;
                piece.Velocity.x *= 0.6f;
                piece.Velocity.z *= 0.6f;
                piece.Spin *= 0.6f;

                if (piece.Velocity.y < 0.6f)
                {
                    piece.Velocity = Vector3.zero;
                    piece.Resting = true;
                }
            }

            piece.Root.position = position;
            piece.Root.Rotate(piece.Spin * dt, Space.World);
        }

        // ----- The body ----------------------------------------------------------------------------

        void HideBody()
        {
            hidden.Clear();
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled) continue;

                skin.enabled = false;
                hidden.Add(skin);
            }
            if (weapons != null) weapons.ShowWeapons(false);
        }

        /// <summary>Shows the body and the sword again and puts the pieces away.</summary>
        void RestoreVisuals()
        {
            IsApart = false;
            foreach (SkinnedMeshRenderer skin in hidden)
                if (skin != null) skin.enabled = true;
            hidden.Clear();

            if (weapons != null && pieces.Count > 0) weapons.ShowWeapons(true);
            foreach (Piece piece in pieces)
                if (piece.Root != null) piece.Root.gameObject.SetActive(false);
        }

        Vector3 HipsPosition()
        {
            Transform hips = Bone(HumanBodyBones.Hips);
            return hips != null ? hips.position : transform.position + Vector3.up * 0.9f;
        }

        /// <summary>Where piece i goes when it flies back: the head for the skull, other joints for the bones.</summary>
        Vector3 BodyPoint(int index)
        {
            // The last piece is the skull.
            if (index >= pieces.Count - 1)
            {
                Transform head = Bone(HumanBodyBones.Head);
                return head != null ? head.position : transform.position + Vector3.up * 1.4f;
            }

            HumanBodyBones[] joints =
            {
                HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.Spine, HumanBodyBones.Chest,
            };
            Transform bone = Bone(joints[index % joints.Length]);
            return bone != null ? bone.position : transform.position + Vector3.up * (0.4f + 0.1f * index);
        }

        Transform Bone(HumanBodyBones bone)
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

        // ----- The pieces (made once) --------------------------------------------------------------

        void EnsurePieces()
        {
            if (pieces.Count > 0) return;

            poolRoot = new GameObject(name + " Bones").transform;
            for (int i = 0; i < Mathf.Max(1, boneCount); i++)
                pieces.Add(MakePiece(MakeBone(), BoneLength * 0.5f));
            // The skull is always the last piece.
            pieces.Add(MakePiece(MakeSkull(), SkullSize * 0.5f));
        }

        Piece MakePiece(GameObject root, float radius)
        {
            root.transform.SetParent(poolRoot, false);
            root.SetActive(false);
            return new Piece { Root = root.transform, Radius = radius };
        }

        /// <summary>A thin capsule with a small ball at each end (the classic cartoon bone).</summary>
        GameObject MakeBone()
        {
            var root = new GameObject("Bone");
            Part(root.transform, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.05f, (BoneLength - 0.08f) * 0.5f, 0.05f));
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 offset = Vector3.up * (end * (BoneLength * 0.5f - 0.04f));
                Part(root.transform, PrimitiveType.Sphere, offset + Vector3.right * 0.03f, Vector3.one * 0.075f);
                Part(root.transform, PrimitiveType.Sphere, offset - Vector3.right * 0.03f, Vector3.one * 0.075f);
            }
            return root;
        }

        GameObject MakeSkull()
        {
            var root = new GameObject("Skull");
            Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * SkullSize);
            // Two dark eyes so it is clearly a friendly skull.
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0.05f, 0.02f, 0.1f), Vector3.one * 0.06f, true);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(-0.05f, 0.02f, 0.1f), Vector3.one * 0.06f, true);
            return root;
        }

        void Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, bool dark = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var meshRenderer = part.GetComponent<MeshRenderer>();
            if (boneMaterial != null) meshRenderer.sharedMaterial = boneMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (dark)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.12f, 1f));
                block.SetColor("_Color", new Color(0.1f, 0.1f, 0.12f, 1f));
                meshRenderer.SetPropertyBlock(block);
            }
        }
    }
}
