using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// The zoo park in free play: every kind he has found stands in its pen and wanders a little (an idle stroll of up to 0.8 m
    /// every few seconds), the other pens are empty with a black silhouette on the sign. It follows ProgressStore.Changed, so the
    /// pens are always as full as his saved progress. While the hide and seek game runs it does nothing at all (the game moves
    /// the animals itself), and when the game ends it puts the pens straight again.
    /// </summary>
    public class ZooParkDisplay : MonoBehaviour
    {
        [Tooltip("The 10 pens, in ZooAnimalKind.Order.")]
        [SerializeField] ZooPen[] pens;
        [Tooltip("The 10 animals, in ZooAnimalKind.Order (the same order as the pens).")]
        [SerializeField] ZooAnimal[] animals;
        [SerializeField] HideAndSeekGame game;

        const float WanderRadius = 0.8f;
        const float WanderSpeed = 0.5f;
        const float WanderArrive = 0.08f;
        const float WanderPauseMin = 2f;
        const float WanderPauseMax = 5f;

        // Per animal: whether it stands in its pen now, where it is strolling to, and when it strolls next.
        bool[] placed;
        bool[] strolling;
        Vector3[] strollTarget;
        float[] nextStroll;
        bool started;
        bool gameWasRunning;

        int Count => Mathf.Min(pens != null ? pens.Length : 0, animals != null ? animals.Length : 0);

        void Awake()
        {
            int count = Count;
            placed = new bool[count];
            strolling = new bool[count];
            strollTarget = new Vector3[count];
            nextStroll = new float[count];
        }

        void OnEnable()
        {
            ProgressStore.Changed += OnProgressChanged;
            if (started) Refresh();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
        }

        void Start()
        {
            started = true;
            Refresh();
        }

        void OnProgressChanged()
        {
            if (!GameRunning()) Refresh();
        }

        bool GameRunning()
        {
            return game != null && game.IsRunning;
        }

        /// <summary>Puts every found kind in its pen and hides the others; signs follow the progress.</summary>
        void Refresh()
        {
            int count = Count;
            if (placed == null || placed.Length != count) Awake();

            for (int i = 0; i < count; i++)
            {
                if (pens[i] != null) pens[i].RefreshSign();

                ZooAnimal animal = animals[i];
                if (animal == null || pens[i] == null) continue;

                if (ZooProgress.IsKnown(pens[i].KindId))
                {
                    // Already standing in the pen: leave it be (it may be strolling).
                    if (placed[i] && animal.IsVisible) continue;

                    pens[i].PlaceInstantly(animal);
                    placed[i] = true;
                    strolling[i] = false;
                    nextStroll[i] = Time.time + Random.Range(WanderPauseMin, WanderPauseMax);
                }
                else
                {
                    animal.Hide();
                    placed[i] = false;
                    strolling[i] = false;
                }
            }
        }

        void Update()
        {
            bool running = GameRunning();
            if (running)
            {
                // The game moves the animals now; start from scratch when it is over. The ones that were strolling stand still.
                if (!gameWasRunning)
                {
                    for (int i = 0; i < Count; i++)
                    {
                        if (strolling != null && i < strolling.Length) strolling[i] = false;
                        if (animals[i] != null) animals[i].SetMoving(false, false);
                    }
                }
                gameWasRunning = true;
                return;
            }
            if (gameWasRunning)
            {
                gameWasRunning = false;
                for (int i = 0; i < placed.Length; i++)
                {
                    placed[i] = false;
                    strolling[i] = false;
                }
                Refresh();
            }

            for (int i = 0; i < Count; i++)
                Stroll(i);
        }

        void Stroll(int i)
        {
            if (!placed[i]) return;

            ZooAnimal animal = animals[i];
            ZooPen pen = pens[i];
            if (animal == null || pen == null || pen.StandPoint == null) return;

            if (!strolling[i])
            {
                if (Time.time < nextStroll[i]) return;

                Vector2 offset = Random.insideUnitCircle * WanderRadius;
                strollTarget[i] = pen.StandPoint.position + new Vector3(offset.x, 0f, offset.y);
                strolling[i] = true;
            }

            Vector3 position = animal.transform.position;
            Vector3 flat = strollTarget[i] - position;
            flat.y = 0f;
            if (flat.magnitude <= WanderArrive)
            {
                animal.SetMoving(false, false);
                strolling[i] = false;
                nextStroll[i] = Time.time + Random.Range(WanderPauseMin, WanderPauseMax);
                return;
            }

            animal.WalkTo(strollTarget[i], WanderSpeed);
        }
    }
}
