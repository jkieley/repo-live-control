using System;

namespace RepoLiveControl.Commands
{
    /// <summary>Shared selection and viewport rules for the virtualized completion list.</summary>
    public static class CompletionListNavigation
    {
        public static int MoveSelection(int selected, int direction, int count)
        {
            if (count <= 0) return 0;
            selected = Math.Max(0, Math.Min(count - 1, selected));
            return (int)(((long)selected + direction % count + count) % count);
        }

        public static float ClampScroll(float scroll, int count, float rowHeight, float viewportHeight)
        {
            ValidateDimensions(rowHeight, viewportHeight);
            float maximum = Math.Max(0f, Math.Max(0, count) * rowHeight - viewportHeight);
            return float.IsNaN(scroll) ? 0f : Math.Max(0f, Math.Min(maximum, scroll));
        }

        public static float RevealSelection(float scroll, int selected, int count, float rowHeight, float viewportHeight)
        {
            scroll = ClampScroll(scroll, count, rowHeight, viewportHeight);
            if (count <= 0) return scroll;
            selected = Math.Max(0, Math.Min(count - 1, selected));
            float top = selected * rowHeight;
            float bottom = top + rowHeight;
            if (top < scroll) scroll = top;
            else if (bottom > scroll + viewportHeight) scroll = bottom - viewportHeight;
            return ClampScroll(scroll, count, rowHeight, viewportHeight);
        }

        public static int FirstVisible(float scroll, int count, float rowHeight, float viewportHeight)
        {
            scroll = ClampScroll(scroll, count, rowHeight, viewportHeight);
            return count <= 0 ? 0 : Math.Min(count - 1, (int)Math.Floor(scroll / rowHeight));
        }

        public static int EndVisible(float scroll, int count, float rowHeight, float viewportHeight)
        {
            scroll = ClampScroll(scroll, count, rowHeight, viewportHeight);
            return Math.Max(0, Math.Min(count, (int)Math.Ceiling((scroll + viewportHeight) / rowHeight)));
        }

        private static void ValidateDimensions(float rowHeight, float viewportHeight)
        {
            if (float.IsNaN(rowHeight) || float.IsInfinity(rowHeight) || rowHeight <= 0f)
                throw new ArgumentOutOfRangeException("rowHeight");
            if (float.IsNaN(viewportHeight) || float.IsInfinity(viewportHeight) || viewportHeight <= 0f)
                throw new ArgumentOutOfRangeException("viewportHeight");
        }
    }
}
