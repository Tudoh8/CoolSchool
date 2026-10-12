using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>The four ways the baby alien can be stuck.</summary>
    public enum RescueKind
    {
        Tree,
        Rocket,
        Bubble,
        Slime
    }

    /// <summary>
    /// One place where the baby alien can be rescued: a tree, the rocket's nose, a floating bubble over the open-field
    /// trampoline, or a slime puddle. It only holds references; the alien landing picks a site and runs the rescue.
    /// </summary>
    public class AlienRescueSite : MonoBehaviour
    {
        [SerializeField] RescueKind kind;
        [Tooltip("Where the baby sits (tree crown, rocket nose). For a bubble: where the bubble's middle floats (optional).")]
        [SerializeField] Transform perch;
        [Tooltip("The trampoline under the perch. Not used for the slime rescue.")]
        [SerializeField] JumpPad pad;
        [Tooltip("The bubble (Bubble sites only).")]
        [SerializeField] FloatingBubble bubble;
        [Tooltip("The slime puddle and blobs (Slime sites only).")]
        [SerializeField] SlimeTrap slime;

        const float BubbleHeightAbovePad = 4.5f;

        public RescueKind Kind => kind;
        public Transform Perch => perch;
        public JumpPad Pad => pad;
        public FloatingBubble Bubble => bubble;
        public SlimeTrap Slime => slime;

        /// <summary>True when everything this kind of rescue needs is there. Otherwise the alien landing picks another site.</summary>
        public bool IsUsable
        {
            get
            {
                switch (kind)
                {
                    case RescueKind.Tree:
                    case RescueKind.Rocket:
                        return perch != null && pad != null;
                    case RescueKind.Bubble:
                        return bubble != null && pad != null;
                    case RescueKind.Slime:
                        return slime != null;
                    default:
                        return false;
                }
            }
        }

        /// <summary>Where the middle of the bubble floats: the perch if there is one, otherwise 4.5 m above the trampoline.</summary>
        public Vector3 BubbleCentre()
        {
            if (perch != null) return perch.position;
            return (pad != null ? pad.Position : transform.position) + Vector3.up * BubbleHeightAbovePad;
        }

        /// <summary>Everything about this site back to how it is outside the game: no highlight, nothing shown.</summary>
        public void ResetSite()
        {
            if (pad != null)
            {
                pad.ResetPad();
                // The rocket's trampoline only exists during the rocket rescue; the others stay (they are always bouncy).
                if (kind == RescueKind.Rocket) pad.gameObject.SetActive(false);
            }

            if (bubble != null) bubble.ResetBubble();
            if (slime != null) slime.ResetTrap();
        }
    }
}
