using CoolSchool.Audio;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Shop
{
    /// <summary>
    /// The touch (and mouse) part of the ice cream shop. Pressing a tub gives a scoop that follows the finger; letting go near
    /// the cone puts it on, letting go anywhere else sends it home. A quick tap on a tub puts the scoop on the cone by itself.
    /// Pressing a coin sends it into the register. Fingers are small and not precise, so everything is picked by the nearest
    /// thing on the screen and the drop zone is wide.
    /// </summary>
    public partial class IceCreamShop
    {
        enum InputMode { Off, Scoops, Coins }

        // A press and release faster than this, with little movement, is a tap.
        const float TapSeconds = 0.3f;
        const float TapMoveFraction = 0.04f;
        // How close to the cone (in screen heights) a scoop counts as dropped on it, and how close a finger must be to a tub or coin.
        const float DropRadiusFraction = 0.15f;
        const float PickRadiusFraction = 0.12f;
        const float TapRayRadius = 0.08f;
        // The scoop is shown a little above the finger, so the finger does not hide it, and floats this high above the counter.
        const float DragLiftFraction = 0.07f;
        const float DragHeight = 0.6f;
        const float DragReach = 3f;

        InputMode inputMode;
        ScoopBall heldBall;
        FlavourTub heldTub;
        bool ballBusy;
        float pressTime;
        Vector2 pressPosition;
        bool movedFar;

        void Update()
        {
            if (!IsRunning || inputMode == InputMode.Off) return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (inputMode == InputMode.Scoops) UpdateScoopInput(pointer);
            else UpdateCoinInput(pointer);
        }

        // ----- Scoops ----------------------------------------------------------------------------

        void UpdateScoopInput(Pointer pointer)
        {
            Vector2 position = pointer.position.ReadValue();

            if (heldBall == null)
            {
                // One scoop at a time: a new press is ignored while the last one is still flying.
                if (ballBusy || cone == null || !pointer.press.wasPressedThisFrame) return;

                FlavourTub tub = PickTub(position);
                if (tub != null) BeginScoop(tub, position);
                return;
            }

            if (Vector2.Distance(position, pressPosition) > Screen.height * TapMoveFraction) movedFar = true;
            DragScoop(position);

            if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed) EndScoop(position);
        }

        void BeginScoop(FlavourTub tub, Vector2 position)
        {
            heldTub = tub;
            heldBall = ScoopBall.Create(tub);
            heldBall.transform.SetParent(Runtime, true);
            pressTime = Time.time;
            pressPosition = position;
            movedFar = false;

            tub.Squash();
            Sfx.Play("shop.scoop", tub.ScoopPoint.position);
            cone.SetDropHint(true);
            ResetIdle();
        }

        /// <summary>The scoop floats on a plane just above the counter, under the point on the screen a little above the finger.</summary>
        void DragScoop(Vector2 position)
        {
            Camera view = Camera.main;
            if (view == null) return;

            Vector2 lifted = position + Vector2.up * (Screen.height * DragLiftFraction);
            Ray ray = view.ScreenPointToRay(lifted);
            float planeY = coneHolder.position.y + DragHeight;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (!plane.Raycast(ray, out float enter)) return;

            // Stays near the stall, whatever the finger does.
            Vector3 point = ray.GetPoint(enter);
            Vector3 offset = point - coneHolder.position;
            offset.y = 0f;
            point = coneHolder.position + Vector3.ClampMagnitude(offset, DragReach);
            point.y = planeY;
            heldBall.Follow(point);
        }

        void EndScoop(Vector2 position)
        {
            ScoopBall ball = heldBall;
            heldBall = null;
            heldTub = null;
            cone.SetDropHint(false);
            ResetIdle();

            bool tapped = Time.time - pressTime < TapSeconds && !movedFar;
            if (tapped || IsOverCone(ball))
                StartCoroutine(PlaceScoopRoutine(ball, tapped));
            else
                StartCoroutine(ReturnScoopRoutine(ball));
        }

        /// <summary>True when the scoop is close to where the next scoop goes, as seen on the screen.</summary>
        bool IsOverCone(ScoopBall ball)
        {
            Camera view = Camera.main;
            if (view == null || cone == null) return false;

            Vector3 scoop = view.WorldToScreenPoint(ball.transform.position);
            Vector3 target = view.WorldToScreenPoint(cone.NextScoopPosition);
            if (scoop.z <= 0f || target.z <= 0f) return false;

            return Vector2.Distance(scoop, target) <= Screen.height * DropRadiusFraction;
        }

        /// <summary>The tub under the finger, or the nearest one on the screen.</summary>
        FlavourTub PickTub(Vector2 position)
        {
            Camera view = Camera.main;
            if (view == null) return null;

            RaycastHit[] hits = Physics.SphereCastAll(view.ScreenPointToRay(position), TapRayRadius, 50f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                var tub = hit.collider.GetComponentInParent<FlavourTub>();
                if (tub != null) return tub;
            }

            float best = Screen.height * PickRadiusFraction;
            FlavourTub nearest = null;
            foreach (FlavourTub tub in tubs)
            {
                if (tub == null) continue;

                Vector3 point = view.WorldToScreenPoint(tub.ScoopPoint.position);
                float distance = Vector2.Distance(position, point);
                if (point.z <= 0f || distance >= best) continue;

                best = distance;
                nearest = tub;
            }
            return nearest;
        }

        // ----- Coins -----------------------------------------------------------------------------

        void UpdateCoinInput(Pointer pointer)
        {
            if (!pointer.press.wasPressedThisFrame) return;

            ShopCoin coin = PickCoin(pointer.position.ReadValue());
            if (coin != null) TakeCoin(coin);
        }

        /// <summary>The coin nearest to the finger on the screen, if it is close enough.</summary>
        ShopCoin PickCoin(Vector2 position)
        {
            Camera view = Camera.main;
            if (view == null) return null;

            float best = Screen.height * PickRadiusFraction;
            ShopCoin nearest = null;
            foreach (ShopCoin coin in coins)
            {
                if (coin == null || coin.Taken) continue;

                Vector3 point = view.WorldToScreenPoint(coin.transform.position);
                float distance = Vector2.Distance(position, point);
                if (point.z <= 0f || distance >= best) continue;

                best = distance;
                nearest = coin;
            }
            return nearest;
        }
    }
}
