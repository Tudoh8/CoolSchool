using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Characters;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.Events;

namespace CoolSchool.Tag
{
    /// <summary>
    /// Runs a game of tag. Whoever is "it" chases; touching someone passes "it" on.
    /// Roles switch between the player and the computer kids.
    /// The game is an activity: it starts when the player walks into the tag zone,
    /// lasts one round, then everyone cheers and it is back to free play.
    /// </summary>
    public class TagGame : Activity
    {
        [SerializeField] bool playerStartsAsIt = true;
        [Tooltip("Seconds a computer kid stands still (counting) after being tagged by the player.")]
        [SerializeField] float kidCountSeconds = 2.5f;
        [Tooltip("Seconds the player stands still after being tagged. Keep short so it never feels unfair.")]
        [SerializeField] float playerFreezeSeconds = 1f;
        [Tooltip("Seconds before the tagger can be tagged back.")]
        [SerializeField] float tagBackImmunity = 3f;
        [Tooltip("How long one round of tag lasts.")]
        [SerializeField] float roundSeconds = 40f;
        [Tooltip("Seconds everyone cheers at the end of the round before free play starts again.")]
        [SerializeField] float cheerSeconds = 2f;

        [Header("Marker above whoever is 'it'")]
        [SerializeField] Transform itMarker;
        [SerializeField] float markerBobHeight = 0.15f;
        [SerializeField] float markerSpinSpeed = 90f;

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent onPlayerTaggedSomeone;
        public UnityEvent onPlayerBecameIt;
        public UnityEvent onRoundFinished;

        /// <summary>(tagger, tagged)</summary>
        public event Action<TagParticipant, TagParticipant> Tagged;

        public TagParticipant It { get; private set; }
        public TagParticipant PlayerParticipant { get; private set; }
        public IReadOnlyList<TagParticipant> Participants => participants;

        readonly List<TagParticipant> participants = new List<TagParticipant>();
        PlayerController playerController;
        Coroutine roundRoutine;

        protected override void OnBegin()
        {
            CollectParticipants();
            if (participants.Count < 2)
            {
                Debug.LogWarning("TagGame needs at least two TagParticipants.");
                End();
                return;
            }

            if (playerController != null) playerController.InputEnabled = true;

            TagParticipant first = playerStartsAsIt && PlayerParticipant != null
                ? PlayerParticipant
                : participants[UnityEngine.Random.Range(0, participants.Count)];
            first.BecomeIt(0f);
            It = first;

            if (itMarker != null) itMarker.gameObject.SetActive(true);
            roundRoutine = StartCoroutine(RoundRoutine());
        }

        protected override void OnEnd()
        {
            if (roundRoutine != null)
            {
                StopCoroutine(roundRoutine);
                roundRoutine = null;
            }
            ClearIt();
        }

        void OnDisable()
        {
            if (IsRunning) End();
        }

        void Update()
        {
            if (!IsRunning || It == null) return;

            if (playerController != null)
                playerController.InputEnabled = !PlayerParticipant.IsFrozen;

            if (!It.IsFrozen)
            {
                foreach (TagParticipant other in participants)
                {
                    if (other == null || other == It || !other.CanBeTagged) continue;
                    if (FlatDistance(It.transform.position, other.transform.position) <= It.TagReach)
                    {
                        PassOn(It, other);
                        break;
                    }
                }
            }

            UpdateMarker();
        }

        /// <summary>Collects the players once; kids do not come and go during the day.</summary>
        void CollectParticipants()
        {
            participants.RemoveAll(p => p == null);
            if (participants.Count > 0) return;

            participants.AddRange(FindObjectsByType<TagParticipant>(FindObjectsSortMode.None));
            PlayerParticipant = participants.Find(p => p.IsPlayer);
            if (PlayerParticipant != null)
                playerController = PlayerParticipant.GetComponent<PlayerController>();
        }

        IEnumerator RoundRoutine()
        {
            yield return new WaitForSeconds(roundSeconds);

            // Time is up: nobody is "it" any more and everyone cheers.
            ClearIt();
            onRoundFinished?.Invoke();
            foreach (TagParticipant p in participants)
            {
                if (p == null) continue;
                var kid = p.GetComponent<KidAnimator>();
                if (kid != null) kid.PlayCheer();
            }

            yield return new WaitForSeconds(cheerSeconds);

            roundRoutine = null;
            End();
        }

        void ClearIt()
        {
            if (It != null) It.StopBeingIt(0f);
            It = null;

            if (itMarker != null) itMarker.gameObject.SetActive(false);
            if (playerController != null) playerController.InputEnabled = true;
        }

        void PassOn(TagParticipant tagger, TagParticipant tagged)
        {
            tagger.StopBeingIt(tagBackImmunity);
            tagged.BecomeIt(tagged.IsPlayer ? playerFreezeSeconds : kidCountSeconds);
            It = tagged;

            if (tagger.IsPlayer)
            {
                tagger.GetComponent<KidAnimator>()?.PlayCheer();
                onPlayerTaggedSomeone?.Invoke();
            }
            else if (tagged.IsPlayer)
            {
                onPlayerBecameIt?.Invoke();
            }

            Tagged?.Invoke(tagger, tagged);
        }

        void UpdateMarker()
        {
            if (itMarker == null) return;

            float bob = Mathf.Sin(Time.time * 3f) * markerBobHeight;
            itMarker.position = It.transform.position + Vector3.up * (It.Height + 0.5f + bob);
            itMarker.Rotate(Vector3.up, markerSpinSpeed * Time.deltaTime, Space.World);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
