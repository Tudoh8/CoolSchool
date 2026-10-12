using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// "Ice cream shop". The child runs an ice cream stall on the playground. Five customers come one by one, each with a
    /// picture bubble above the head showing the order. He scoops balls out of the coloured tubs onto a cone, hands it over,
    /// and then counts the coins into the register. Counting (the scoops, then the coins) and colours are the lesson.
    /// He can never fail: a wrong colour bounces back with a hint, the right tub glows after the second mistake, and an idle
    /// child is helped after 12 and 24 seconds. Everything that was changed is put back in OnEnd, at any moment.
    /// This part has the flow; the touch input is in IceCreamShop.Touch.cs.
    /// </summary>
    public partial class IceCreamShop : Activity
    {
        [Header("Player and camera")]
        [SerializeField] PlayerController player;
        [SerializeField] LessonCameraRig cameraRig;
        [SerializeField] LessonUI lessonUI;
        [SerializeField] ImaginationEffect imagination;

        [Header("Points (the stall faces south; the shop camera looks south over the counter)")]
        [Tooltip("Where the player stands: behind the counter, at the left end as the camera sees it.")]
        [SerializeField] Transform shopkeeperSpot;
        [SerializeField] Transform shopCamera;
        [Tooltip("Where a customer stands in front of the counter, facing north.")]
        [SerializeField] Transform orderSpot;
        [Tooltip("Where customers come from, out of the shop camera's view.")]
        [SerializeField] Transform customerEntry;
        [Tooltip("Where customers walk off to, out of the shop camera's view.")]
        [SerializeField] Transform customerExit;
        [Tooltip("On the counter beside the cone holder: the coins appear here.")]
        [SerializeField] Transform coinSpot;

        [Header("Stall")]
        [SerializeField] FlavourTub[] tubs;
        [Tooltip("An empty cone, switched off. Each customer gets a copy of it in the holder.")]
        [SerializeField] IceCreamCone coneTemplate;
        [SerializeField] Transform coneHolder;
        [SerializeField] CashRegister register;
        [SerializeField] OrderBubble bubble;
        [SerializeField] Material goldMaterial;

        [Header("Customers, in visit order (the last one is the dragon)")]
        [SerializeField] ShopCustomer[] customers;
        [Tooltip("The big dragon stands this far back from the order spot, so its head does not go through the counter.")]
        [SerializeField] float dragonExtraDistance = 1.2f;

        [Header("Reward")]
        [SerializeField] int starsAtEnd = 5;

        const float MaxSpeechWaitSeconds = 10f;
        const float MaxWalkSeconds = 15f;
        const float StarsLandSeconds = 3f;
        const float RereadAfterSeconds = 12f;
        const float RereadRepeatSeconds = 18f;
        const float GlowAfterSeconds = 24f;
        const float LongPauseSeconds = 120f;
        const int MistakesToHelp = 2;
        const int MistakesFreeToLevelUp = 1;
        const int MistakesToLevelDown = 5;

        Coroutine flowRoutine;
        Transform runtimeRoot;
        bool started;
        bool quitting;
        bool hudHidden;
        bool savedJumpEnabled = true;

        // The customer being served right now.
        ShopCustomer current;
        ShopOrder order;
        ShopOrder previousOrder;
        IceCreamCone cone;
        IceCreamCone servedCone;
        bool[] slotFilled = new bool[0];
        readonly int[] remaining = new int[FlavourInfo.Count];
        int placed;
        int mistakes;
        int sessionMistakes;
        int coinsTaken;
        int coinsFlying;
        float idleSince;
        float nextRereadTime;
        bool idleGlow;
        float lastPayLineTime = float.NegativeInfinity;
        readonly List<ShopCoin> coins = new List<ShopCoin>();

        protected override void OnBegin()
        {
            if (!HasEverything())
            {
                Debug.LogWarning("CoolSchool: the ice cream shop is missing its points, tubs, cone or customers, so it can not start.");
                End();
                return;
            }

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            started = true;
            sessionMistakes = 0;
            lastPayLineTime = float.NegativeInfinity;
            previousOrder = null;

            ResetEverything();
            MovePlayerToShop();
            Sfx.PlayMusic("music.shop");
            if (imagination != null) imagination.Play(true);

            if (lessonUI != null)
            {
                lessonUI.SetGameplayHudVisible(false);
                hudHidden = true;
            }

            flowRoutine = StartCoroutine(ShopRoutine());
        }

        protected override void OnEnd()
        {
            StopAllCoroutines();
            flowRoutine = null;
            ResetEverything();

            if (hudHidden && lessonUI != null) lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;

            if (started)
            {
                started = false;
                // The player goes first, so the follow camera snaps to where he really is.
                ReturnPlayer();
                if (imagination != null) imagination.Play(false);
            }

            if (cameraRig != null) cameraRig.RestoreNow();
            if (player != null)
            {
                player.InputEnabled = true;
                player.JumpEnabled = savedJumpEnabled;
            }
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-game, put everything back first.
            if (IsRunning && !quitting) End();
        }

        bool HasEverything()
        {
            return shopkeeperSpot != null && shopCamera != null && orderSpot != null && customerEntry != null
                && customerExit != null && coinSpot != null && coneHolder != null && coneTemplate != null
                && tubs != null && tubs.Length > 0 && customers != null && customers.Length > 0 && bubble != null;
        }

        // ----- The flow of the game --------------------------------------------------------------

        IEnumerator ShopRoutine()
        {
            if (cameraRig != null) yield return cameraRig.EnterLesson(shopCamera);

            Say("shop.start");
            if (!ProgressStore.Current.shopHowtoHeard)
            {
                Say("shop.howto");
                ProgressStore.Current.shopHowtoHeard = true;
                ProgressStore.Save();
            }
            yield return WaitForSpeech();
            yield return new WaitForSeconds(0.3f);

            int level = Mathf.Clamp(ProgressStore.Current.shopLevel, 1, ShopOrder.MaxLevel);
            int last = LastCustomerIndex();
            for (int i = 0; i < customers.Length; i++)
            {
                if (customers[i] == null) continue;

                yield return ServeCustomer(customers[i], level, i == last);
            }

            SaveLevel(level);
            yield return OutroRoutine();

            if (cameraRig != null) yield return cameraRig.ExitLesson();
            flowRoutine = null;
            End();
        }

        int LastCustomerIndex()
        {
            for (int i = customers.Length - 1; i >= 0; i--)
                if (customers[i] != null) return i;
            return -1;
        }

        IEnumerator ServeCustomer(ShopCustomer who, int level, bool isLast)
        {
            current = who;
            bool dragon = who.Kind == CustomerKind.Dragon;
            order = ShopOrder.Create(level, previousOrder, dragon);
            StartOrderState();

            yield return ArriveRoutine(who, dragon);
            yield return OrderRoutine(who);
            yield return MakeConeRoutine();
            yield return ServeRoutine(who, dragon);
            yield return PayRoutine(who);

            // Off he goes; the next customer may already come in while this one is still walking away.
            who.Leave(customerExit.position);
            previousOrder = order;
            current = null;
            if (!isLast)
            {
                Say("shop.next");
                yield return WaitForSpeech();
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        // ---- 1. The customer walks in ----

        IEnumerator ArriveRoutine(ShopCustomer who, bool dragon)
        {
            if (dragon)
            {
                Say("shop.dragon_arrives");
                yield return WaitForSpeech();
            }

            Vector3 spot = OrderPosition(dragon);
            Vector3 facing = orderSpot.position + orderSpot.forward * 6f;
            who.Appear(customerEntry.position, spot);
            who.WalkTo(spot, facing);

            yield return null;
            float giveUp = Time.time + MaxWalkSeconds;
            while (who.IsWalking && Time.time < giveUp) yield return null;

            // Never wait for a customer who got lost: put him on his spot.
            if (who.IsWalking) who.Appear(spot, facing);
            Sfx.Play("shop.bell", spot);
        }

        Vector3 OrderPosition(bool dragon)
        {
            Vector3 spot = orderSpot.position;
            if (dragon) spot -= orderSpot.forward * dragonExtraDistance;
            return spot;
        }

        // ---- 2. He says what he wants, and the picture shows it ----

        IEnumerator OrderRoutine(ShopCustomer who)
        {
            who.Talk();
            Say("shop.hello." + KindName(who.Kind));
            bubble.Show(who.BubbleAnchor, order.Slots);
            yield return WaitForSpeech();
            yield return ReadOrderRoutine();
        }

        /// <summary>The narrator reads the order: one line for each colour.</summary>
        IEnumerator ReadOrderRoutine()
        {
            foreach (KeyValuePair<Flavour, int> group in order.Groups)
            {
                Say(OrderGroup(group));
                yield return WaitForSpeech();
            }
        }

        static string OrderGroup(KeyValuePair<Flavour, int> group)
        {
            return "shop.order." + group.Value + "." + FlavourInfo.VoiceName(group.Key);
        }

        // ---- 3. Making the cone ----

        IEnumerator MakeConeRoutine()
        {
            cone = SpawnCone();
            yield return new WaitForSeconds(0.45f);

            inputMode = InputMode.Scoops;
            ResetIdle();

            while (placed < order.Count)
            {
                UpdateScoopHints();
                yield return null;
            }

            inputMode = InputMode.Off;
            idleGlow = false;
            RefreshGlow();
            while (ballBusy) yield return null;
        }

        IceCreamCone SpawnCone()
        {
            IceCreamCone made = Instantiate(coneTemplate, coneHolder.position, coneHolder.rotation, Runtime);
            made.name = "IceCreamCone";
            made.gameObject.SetActive(true);
            made.PopIn();
            Sfx.Play("shop.cone_pop", coneHolder.position);
            BattleEffects.Sparkle(coneHolder.position + Vector3.up * 0.3f);
            return made;
        }

        /// <summary>After a pause the order is read again, and after a longer one the next tub that is needed starts to glow.</summary>
        void UpdateScoopHints()
        {
            // A child who is holding a scoop is not stuck.
            if (heldBall != null || ballBusy)
            {
                ResetIdle();
                return;
            }

            if (Time.time >= nextRereadTime)
            {
                nextRereadTime = Time.time + RereadRepeatSeconds;
                bubble.Pulse();
                if (current != null) current.Talk();
                Say("shop.hint");
                foreach (KeyValuePair<Flavour, int> group in order.Groups) Say(OrderGroup(group));
            }

            if (!idleGlow && Time.time - idleSince >= GlowAfterSeconds)
            {
                idleGlow = true;
                RefreshGlow();
            }
        }

        void ResetIdle()
        {
            idleSince = Time.time;
            nextRereadTime = Time.time + RereadAfterSeconds;
        }

        /// <summary>A scoop was put on the cone, or a coin was tapped: the child is doing fine.</summary>
        void NoteCorrectAction()
        {
            ResetIdle();
            if (idleGlow)
            {
                idleGlow = false;
                RefreshGlow();
            }
        }

        // The scoop flights run in their own coroutines, started by the touch input (IceCreamShop.Touch.cs).

        IEnumerator PlaceScoopRoutine(ScoopBall ball, bool tapped)
        {
            ballBusy = true;
            if (ball == null) { ballBusy = false; yield break; }
            if (cone == null)
            {
                yield return ReturnScoopRoutine(ball);
                yield break;
            }

            Vector3 target = cone.NextScoopPosition;
            if (remaining[(int)ball.Flavour] > 0)
            {
                yield return ShopMotion.Fly(ball.transform, target, tapped ? 0.45f : 0.22f, tapped ? 0.5f : 0.15f);
                Sfx.Play("shop.plop", target);
                cone.Seat(ball);

                remaining[(int)ball.Flavour]--;
                placed++;
                FillBubbleSlot(ball.Flavour);
                BattleEffects.Sparkle(target + Vector3.up * 0.1f);
                Say("number." + placed, true);
                NoteCorrectAction();
                RefreshGlow();
            }
            else
            {
                // Not this colour: it touches the cone, bounces off and flies home. No harm done.
                yield return ShopMotion.Fly(ball.transform, target + Vector3.up * 0.1f, tapped ? 0.4f : 0.2f, 0.2f);
                Sfx.Play("shop.bounce", target);
                mistakes++;
                sessionMistakes++;
                Say("shop.wrong_colour", true);
                RefreshGlow();

                yield return ShopMotion.Fly(ball.transform, ball.Tub.ScoopPoint.position, 0.55f, 0.7f);
                ball.Tub.Squash();
                Destroy(ball.gameObject);
            }
            ballBusy = false;
        }

        /// <summary>A scoop that was let go away from the cone flies back into its tub. That is not a mistake.</summary>
        IEnumerator ReturnScoopRoutine(ScoopBall ball)
        {
            ballBusy = true;
            if (ball != null)
            {
                yield return ShopMotion.Fly(ball.transform, ball.Tub.ScoopPoint.position, 0.35f, 0.3f);
                Sfx.Play("shop.plop", ball.Tub.ScoopPoint.position);
                ball.Tub.Squash();
                Destroy(ball.gameObject);
            }
            ballBusy = false;
        }

        void FillBubbleSlot(Flavour flavour)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (slotFilled[i] || order.Slots[i] != flavour) continue;

                slotFilled[i] = true;
                bubble.FillSlot(i);
                return;
            }
        }

        /// <summary>
        /// After the second mistake, or a long pause, the tub for the next scoop that is still needed glows and pulses.
        /// Otherwise no tub does.
        /// </summary>
        void RefreshGlow()
        {
            Flavour? needed = null;
            if ((mistakes >= MistakesToHelp || idleGlow) && order != null && placed < order.Count)
                needed = NextNeededFlavour();

            foreach (FlavourTub tub in tubs)
                if (tub != null) tub.SetGlow(needed.HasValue && tub.Flavour == needed.Value);
        }

        Flavour? NextNeededFlavour()
        {
            for (int i = 0; i < order.Count; i++)
                if (!slotFilled[i]) return order.Slots[i];
            return null;
        }

        // ---- 4. Counting the scoops and handing the cone over ----

        IEnumerator ServeRoutine(ShopCustomer who, bool dragon)
        {
            yield return new WaitForSeconds(0.5f);
            Say("shop.done");
            yield return WaitForSpeech();
            yield return new WaitForSeconds(0.4f);

            // Count along: each scoop hops while its number is said.
            for (int k = 0; k < order.Count; k++)
            {
                StartCoroutine(cone.HopScoop(k));
                Say("number." + (k + 1), true);
                BattleEffects.Sparkle(cone.ScoopPosition(k) + Vector3.up * 0.15f);
                yield return new WaitForSeconds(1f);
            }

            Say("shop.serve");
            yield return WaitForSpeech();
            PlayerCheer();

            // The cone flies into the customer's hand.
            yield return ShopMotion.Fly(cone.transform, who.HandPoint.position, 0.6f, 0.4f);
            servedCone = cone;
            cone = null;
            who.HoldCone(servedCone.transform);
            who.Cheer();
            Sfx.Play("shop.slurp", who.HandPoint.position);
            Say("shop.thanks." + KindName(who.Kind));
            bubble.Hide();

            if (dragon) yield return DragonSneezeRoutine(who);
            yield return WaitForSpeech();
        }

        /// <summary>After eating its cold ice cream the dragon sneezes a harmless burst of confetti.</summary>
        IEnumerator DragonSneezeRoutine(ShopCustomer who)
        {
            yield return new WaitForSeconds(1.2f);
            Sfx.Play("dragon.happy", who.HandPoint.position);
            for (int i = 0; i < 3; i++)
            {
                BattleEffects.Confetti(who.HandPoint.position + Vector3.up * 0.2f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        // ---- 5. Paying: one coin for each scoop ----

        IEnumerator PayRoutine(ShopCustomer who)
        {
            int count = order.Count;
            coinsTaken = 0;
            coinsFlying = 0;
            coins.Clear();

            yield return new WaitForSeconds(0.3f);
            who.PutCoin();
            if (Time.time - lastPayLineTime > LongPauseSeconds)
            {
                lastPayLineTime = Time.time;
                Say("shop.pay");
            }
            yield return new WaitForSeconds(0.3f);

            for (int i = 0; i < count; i++)
            {
                // Each coin hops over the counter from the customer to its place in the row.
                Vector3 position = CoinPosition(coinSpot, i, count);
                Vector3 start = CoinStart(who);
                Vector3 toCamera = Camera.main != null ? Camera.main.transform.position - position : Vector3.back;
                ShopCoin coin = ShopCoin.Create(goldMaterial, start, toCamera);
                coin.transform.SetParent(Runtime, true);
                coins.Add(coin);
                Sfx.Play("shop.coin_drop", start);
                StartCoroutine(HopCoinRoutine(coin, position));
                yield return new WaitForSeconds(0.4f);
            }
            yield return new WaitForSeconds(0.3f);

            inputMode = InputMode.Coins;
            ResetIdle();
            bool pulsing = false;

            while (coinsTaken < count)
            {
                if (Time.time >= nextRereadTime)
                {
                    // Waiting a long time: say what to do again and make the coins bounce.
                    nextRereadTime = Time.time + RereadRepeatSeconds;
                    lastPayLineTime = Time.time;
                    Say("shop.pay");
                    pulsing = true;
                    foreach (ShopCoin coin in coins) if (coin != null) coin.SetPulse(true);
                }
                yield return null;
            }
            if (pulsing) foreach (ShopCoin coin in coins) if (coin != null) coin.SetPulse(false);

            inputMode = InputMode.Off;
            while (coinsFlying > 0) yield return null;

            yield return TipRoutine(who, count);

            if (register != null) register.KaChing();
            Say("shop.paid");
            yield return WaitForSpeech();
        }

        /// <summary>The distance between two coins in the row on the counter.</summary>
        public const float CoinSpacing = 0.3f;

        /// <summary>
        /// Where a coin lies on the counter: the row is centred on the coin spot and runs to the camera's right.
        /// Shared with the editor's preview, so what is checked there is exactly what is played.
        /// </summary>
        public static Vector3 CoinPosition(Transform coinSpot, int index, int count)
        {
            float across = (index - (count - 1) * 0.5f) * CoinSpacing;
            return coinSpot.position + coinSpot.right * across + Vector3.up * (ShopCoin.Thickness * 0.5f + 0.03f);
        }

        /// <summary>Where a coin starts: held out in front of the customer, a little above the counter.</summary>
        Vector3 CoinStart(ShopCustomer who)
        {
            Vector3 over = who.transform.position + orderSpot.forward * 0.9f;
            return new Vector3(over.x, coinSpot.position.y + 0.3f, over.z);
        }

        IEnumerator HopCoinRoutine(ShopCoin coin, Vector3 target)
        {
            yield return ShopMotion.Fly(coin.transform, target, 0.5f, 0.35f);
            if (coin != null) BattleEffects.Sparkle(target + Vector3.up * 0.1f);
        }

        /// <summary>A coin was tapped: it flies into the register and the next number is said.</summary>
        void TakeCoin(ShopCoin coin)
        {
            coin.Taken = true;
            coinsTaken++;
            Sfx.Play("shop.coin_tap", coin.transform.position);
            Say("number." + coinsTaken, true);
            NoteCorrectAction();
            StartCoroutine(FlyCoinRoutine(coin));
        }

        IEnumerator FlyCoinRoutine(ShopCoin coin)
        {
            coinsFlying++;
            Vector3 target = register != null ? register.CoinTarget : coinSpot.position;
            yield return ShopMotion.Fly(coin.transform, target, 0.45f, 0.5f);

            if (register != null) register.Bump();
            if (coin != null) Destroy(coin.gameObject);
            coinsFlying--;
        }

        // ---- The end: the teacher is proud and the stars fly ----

        IEnumerator OutroRoutine()
        {
            Say("shop.end");
            PlayerCheer();
            yield return WaitForSpeech();

            Vector3 counter = coneHolder.position;
            RewardSystem.AddStars(starsAtEnd, counter, announce: false);
            BattleEffects.Confetti(counter + Vector3.up * 1f);
            yield return new WaitForSeconds(StarsLandSeconds);
        }

        // ----- Levels ----------------------------------------------------------------------------

        /// <summary>A session with almost no mistakes makes the orders bigger next time, many mistakes make them smaller.</summary>
        void SaveLevel(int level)
        {
            int next = level;
            if (sessionMistakes <= MistakesFreeToLevelUp) next = Mathf.Min(level + 1, ShopOrder.MaxLevel);
            else if (sessionMistakes >= MistakesToLevelDown) next = Mathf.Max(level - 1, 1);

            ProgressStore.Current.shopLevel = next;
            ProgressStore.Save();
        }

        // ----- State of the order ----------------------------------------------------------------

        void StartOrderState()
        {
            slotFilled = new bool[order.Count];
            for (int i = 0; i < remaining.Length; i++) remaining[i] = order.Totals[i];
            placed = 0;
            mistakes = 0;
            idleGlow = false;
            RefreshGlow();
        }

        // ----- The kid at the counter ------------------------------------------------------------

        void MovePlayerToShop()
        {
            if (player == null) return;

            BattleEffects.Sparkle(player.transform.position + Vector3.up * 0.8f);

            savedJumpEnabled = player.JumpEnabled;
            MovePlayer(shopkeeperSpot.position, shopkeeperSpot.rotation);
            player.InputEnabled = false;
            player.JumpEnabled = false;
        }

        /// <summary>
        /// The kid ends where he made the ice cream: behind the counter at the shopkeeper's spot, facing the playground. The zone
        /// is on the other side of the stall, so he does not start the shop again, and the counter's end is open to walk out.
        /// </summary>
        void ReturnPlayer()
        {
            if (player == null || shopkeeperSpot == null) return;

            MovePlayer(shopkeeperSpot.position, shopkeeperSpot.rotation);
        }

        void MovePlayer(Vector3 position, Quaternion rotation)
        {
            // Stand on the real ground under the point, in case it floats or sinks a little (not on a roof or a counter top).
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - position.y) < 0.5f && !hit.transform.IsChildOf(player.transform))
                position.y = hit.point.y;

            player.Teleport(position, rotation);
        }

        void PlayerCheer()
        {
            if (player == null) return;

            var kid = player.GetComponent<KidAnimator>();
            if (kid != null) kid.PlayCheer();
        }

        // ----- Putting things back ---------------------------------------------------------------

        /// <summary>Customers hidden, balls, cones and coins gone, glow off, bubble away. Safe to call at any time.</summary>
        void ResetEverything()
        {
            inputMode = InputMode.Off;
            heldBall = null;
            ballBusy = false;
            current = null;
            cone = null;
            order = null;
            placed = 0;
            mistakes = 0;
            coins.Clear();
            ResetTip();

            if (servedCone != null) Destroy(servedCone.gameObject);
            servedCone = null;

            if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
            runtimeRoot = null;

            if (customers != null)
                foreach (ShopCustomer customer in customers)
                    if (customer != null) customer.Hide();

            if (tubs != null)
                foreach (FlavourTub tub in tubs)
                    if (tub != null) tub.SetGlow(false);

            if (bubble != null) bubble.HideNow();
            if (register != null) register.ResetNow();
        }

        /// <summary>The holder of everything that is made while the shop runs.</summary>
        Transform Runtime
        {
            get
            {
                if (runtimeRoot == null) runtimeRoot = new GameObject("ShopRuntime").transform;
                return runtimeRoot;
            }
        }

        // ----- Voice -----------------------------------------------------------------------------

        static void Say(string group, bool interrupt = false)
        {
            VoicePlayer.Say(group, interrupt);
        }

        static string KindName(CustomerKind kind)
        {
            switch (kind)
            {
                case CustomerKind.Knight: return "knight";
                case CustomerKind.Alien: return "alien";
                case CustomerKind.Dragon: return "dragon";
                default: return "kid";
            }
        }

        /// <summary>Waits for whoever is talking to finish, but never for long.</summary>
        static IEnumerator WaitForSpeech()
        {
            // Gives a line that was just requested a frame to start playing.
            yield return null;

            float giveUp = Time.time + MaxSpeechWaitSeconds;
            VoicePlayer voice = VoicePlayer.Instance;
            while (voice != null && voice.IsSpeaking && Time.time < giveUp) yield return null;
        }
    }
}
