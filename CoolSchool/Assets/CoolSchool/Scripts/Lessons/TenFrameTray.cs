using System.Collections;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// A flat, friendly tray with two rows of five shallow cells: a "ten-frame". Counting toys in a tray makes a big
    /// number easy to see: 14 reads as "a full ten and 4". The lesson shows one or two trays on the table, drops toys
    /// into the cells in a fixed order and hides the trays again. The geometry (a body and ten cell plates) is built
    /// by the lesson setup builder; the size of a cell can be tuned here without a rebuild.
    /// </summary>
    public class TenFrameTray : MonoBehaviour
    {
        [Tooltip("Width and depth of one cell in metres. The toys in the tray are scaled to fit it.")]
        [SerializeField] float cellSize = 0.2f;
        [Tooltip("Distance in metres between the two rows. A little more than a cell is wide, so the toys of the back row are not hidden by the front row when the camera looks down from the front.")]
        [SerializeField] float rowPitch = 0.3f;
        [Tooltip("The coloured base of the tray: a unit cube that is stretched to the tray's size.")]
        [SerializeField] Transform body;
        [Tooltip("The ten cell plates, top row first, left to right as the camera sees them.")]
        [SerializeField] Transform[] cells;

        public const int Columns = 5;
        public const int Rows = 2;
        public const int Capacity = Columns * Rows;

        const float Padding = 0.06f;
        const float Thickness = 0.03f;
        const float CellHeight = 0.008f;
        const float CellFill = 0.88f;
        const float ShowSeconds = 0.3f;

        Coroutine popIn;

        /// <summary>Width and depth of one cell in metres.</summary>
        public float CellSize => cellSize;

        /// <summary>The whole tray from left to right, in metres.</summary>
        public float Width => Columns * cellSize + 2f * Padding;

        /// <summary>The whole tray from back to front, in metres.</summary>
        public float Depth => (Rows - 1) * rowPitch + cellSize + 2f * Padding;

        /// <summary>Puts the tray on the table at this point, facing the same way as the toys, and pops it into view (or just shows it, without animate).</summary>
        public void Show(Vector3 center, Quaternion facing, bool animate = true)
        {
            transform.SetPositionAndRotation(center, facing);
            ApplyLayout();
            gameObject.SetActive(true);
            if (!animate) return;

            if (popIn != null) StopCoroutine(popIn);
            popIn = StartCoroutine(PopInRoutine());
        }

        /// <summary>Takes the tray off the table. Safe to call at any moment, also when it is not shown.</summary>
        public void Hide()
        {
            if (popIn != null) StopCoroutine(popIn);
            popIn = null;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The spot on the tray for the toy with this number (0 to 9): the top row first, left to right as the camera sees it.
        /// Table x is mirrored (it grows toward the left of the screen), so the first column has the biggest local x.
        /// </summary>
        public Vector3 SlotPosition(int index)
        {
            index = Mathf.Clamp(index, 0, Capacity - 1);
            return transform.position + transform.rotation * SlotLocal(index);
        }

        Vector3 SlotLocal(int index)
        {
            int row = index / Columns;
            int column = index % Columns;
            return new Vector3(((Columns - 1) * 0.5f - column) * cellSize, Thickness + CellHeight, (row - (Rows - 1) * 0.5f) * rowPitch);
        }

        /// <summary>Sizes the base and the cell plates from the cell size.</summary>
        void ApplyLayout()
        {
            if (body != null)
            {
                body.localPosition = new Vector3(0f, Thickness * 0.5f, 0f);
                body.localRotation = Quaternion.identity;
                body.localScale = new Vector3(Width, Thickness, Depth);
            }

            if (cells == null) return;

            for (int i = 0; i < cells.Length && i < Capacity; i++)
            {
                if (cells[i] == null) continue;

                Vector3 slot = SlotLocal(i);
                cells[i].localPosition = new Vector3(slot.x, Thickness + CellHeight * 0.5f, slot.z);
                cells[i].localRotation = Quaternion.identity;
                cells[i].localScale = new Vector3(cellSize * CellFill, CellHeight, Mathf.Max(cellSize, rowPitch) * CellFill);
            }
        }

        IEnumerator PopInRoutine()
        {
            transform.localScale = Vector3.zero;
            for (float elapsed = 0f; elapsed < ShowSeconds; elapsed += Time.deltaTime)
            {
                transform.localScale = Vector3.one * LessonMotion.OutBack(elapsed / ShowSeconds);
                yield return null;
            }

            transform.localScale = Vector3.one;
            popIn = null;
        }
    }
}
