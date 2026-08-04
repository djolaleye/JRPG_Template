using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// The Pokémon-summary hexagon: an n-axis radar chart drawn procedurally, with no art at all.
    ///
    /// <para>It subclasses <see cref="Graphic"/> and builds its own mesh in <see cref="OnPopulateMesh"/>:
    /// concentric grid rings, spokes out to each axis, and a filled polygon through the values. Because
    /// everything is generated geometry tinted by <see cref="Graphic.color"/>, the chart re-themes by
    /// changing four colour fields — there is no sprite to re-author for a light or dark palette.</para>
    ///
    /// <para>Winding is uniform clockwise (uGUI's own convention, matching
    /// <c>VertexHelper.AddUIVertexQuad</c>), so nothing depends on the material leaving culling off.</para>
    ///
    /// <para>Supports 3..10 axes. Axis labels are pooled TMP children placed around the ring.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StatRadarView : Graphic
    {
        /// <summary>Fewest axes that still describe an area rather than a line.</summary>
        public const int MinAxes = 3;

        /// <summary>Practical upper bound — past this the labels collide and the shape stops reading.</summary>
        public const int MaxAxes = 10;

        [Header("Geometry")]
        [Tooltip("Fraction of the smaller rect dimension used by the outermost ring.")]
        [Range(0.1f, 1f)][SerializeField] private float radiusScale = 0.72f;

        [Tooltip("Degrees for the first axis. 90 puts it straight up.")]
        [SerializeField] private float startAngleDegrees = 90f;

        [Tooltip("Concentric grid rings drawn inside the outer ring.")]
        [Range(1, 8)][SerializeField] private int gridRings = 4;

        [Min(0f)][SerializeField] private float gridThickness = 1.5f;
        [Min(0f)][SerializeField] private float outlineThickness = 2.5f;

        [Tooltip("Draw a spoke from the centre out to each axis vertex.")]
        [SerializeField] private bool drawSpokes = true;

        [Header("Colours")]
        [Tooltip("The filled value polygon uses Graphic.color. These are the surrounding structure.")]
        [SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.22f);

        [SerializeField] private Color outlineColor = new(1f, 1f, 1f, 0.75f);

        [Tooltip("Outline traced around the value polygon itself. Alpha 0 disables it.")]
        [SerializeField] private Color valueOutlineColor = new(1f, 1f, 1f, 0.9f);

        [Header("Labels")]
        [Tooltip("Inactive TMP child cloned per axis. Null means the chart draws without labels.")]
        [SerializeField] private TMP_Text labelTemplate;

        [Tooltip("Parent for the pooled labels. Defaults to this RectTransform.")]
        [SerializeField] private RectTransform labelContainer;

        [Tooltip("Extra pixels between the outer ring and each label's centre.")]
        [SerializeField] private float labelPadding = 18f;

        private readonly List<float> _values = new();
        private readonly List<string> _labels = new();
        private readonly List<TMP_Text> _labelPool = new();

        private int _axisCount;
        private bool _warnedAxisCount;

        private static readonly UIVertex[] s_quad = new UIVertex[4];

        /// <summary>Number of axes actually being drawn (0 when the chart has no valid data).</summary>
        public int AxisCount => _axisCount;

        /// <summary>Vertices emitted by the most recent mesh build. Diagnostic — lets an editor test
        /// confirm the chart really generated geometry rather than silently drawing nothing.</summary>
        public int LastVertexCount { get; private set; }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>
        /// Set the axes and their 0..1 values. <paramref name="axisLabels"/> may be null (unlabelled
        /// chart) or shorter than the value list. Counts outside 3..10 are clamped, and a chart with
        /// fewer than 3 usable axes draws nothing rather than degenerating into a line.
        /// </summary>
        public void SetAxes(IReadOnlyList<string> axisLabels, IReadOnlyList<float> normalized01)
        {
            _values.Clear();
            _labels.Clear();

            int valueCount = normalized01?.Count ?? 0;
            int labelCount = axisLabels?.Count ?? 0;
            int count = Mathf.Max(valueCount, labelCount);

            if (count > MaxAxes)
            {
                WarnAxisCount($"StatRadarView was given {count} axes; drawing the first {MaxAxes}.");
                count = MaxAxes;
            }

            if (count < MinAxes)
            {
                if (count > 0)
                    WarnAxisCount($"StatRadarView needs at least {MinAxes} axes; got {count}. Drawing nothing.");

                _axisCount = 0;
                SyncLabels();
                SetVerticesDirty();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float v = i < valueCount ? normalized01[i] : 0f;
                _values.Add(float.IsNaN(v) ? 0f : Mathf.Clamp01(v));
                _labels.Add(i < labelCount ? axisLabels[i] ?? string.Empty : string.Empty);
            }

            _axisCount = count;
            SyncLabels();
            SetVerticesDirty();
        }

        /// <summary>Update the values without re-authoring the labels. Length must match the axis count.</summary>
        public void SetValues(IReadOnlyList<float> normalized01)
        {
            if (_axisCount == 0 || normalized01 == null) return;

            for (int i = 0; i < _axisCount; i++)
            {
                float v = i < normalized01.Count ? normalized01[i] : 0f;
                _values[i] = float.IsNaN(v) ? 0f : Mathf.Clamp01(v);
            }

            SetVerticesDirty();
        }

        /// <summary>Drop all data; the chart renders empty.</summary>
        public void ClearAxes()
        {
            _values.Clear();
            _labels.Clear();
            _axisCount = 0;
            SyncLabels();
            SetVerticesDirty();
        }

        /// <summary>The 0..1 value on <paramref name="index"/>, or 0 when out of range.</summary>
        public float ValueAt(int index) => index >= 0 && index < _values.Count ? _values[index] : 0f;

        // ---- mesh --------------------------------------------------------------------------------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVertexCount = 0;

            int n = _axisCount;
            if (n < MinAxes) return;

            Rect r = GetPixelAdjustedRect();
            Vector2 center = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f * Mathf.Clamp01(radiusScale);
            if (radius <= 0.5f) return;

            // --- grid rings (innermost first, outer ring gets the stronger colour) ---
            int rings = Mathf.Max(1, gridRings);
            for (int ring = 1; ring <= rings; ring++)
            {
                float rr = radius * ring / rings;
                bool outer = ring == rings;
                Color32 c = outer ? outlineColor : gridColor;
                float t = outer ? outlineThickness : gridThickness;
                if (t <= 0f) continue;

                for (int i = 0; i < n; i++)
                {
                    Vector2 a = center + AxisDir(i, n) * rr;
                    Vector2 b = center + AxisDir((i + 1) % n, n) * rr;
                    AddLine(vh, a, b, t, c);
                }
            }

            // --- spokes ---
            if (drawSpokes && gridThickness > 0f)
            {
                for (int i = 0; i < n; i++)
                    AddLine(vh, center, center + AxisDir(i, n) * radius, gridThickness, gridColor);
            }

            // --- filled value polygon, as a clockwise triangle fan from the centre ---
            int centerIndex = vh.currentVertCount;
            Color32 fill = color;
            vh.AddVert(center, fill, Vector2.zero);

            for (int i = 0; i < n; i++)
                vh.AddVert(center + AxisDir(i, n) * radius * _values[i], fill, Vector2.zero);

            for (int i = 0; i < n; i++)
            {
                int a = centerIndex + 1 + i;
                int b = centerIndex + 1 + (i + 1) % n;

                // AxisDir advances counter-clockwise, so (centre, b, a) is the clockwise order uGUI
                // treats as front-facing. Emitting (centre, a, b) would rely on Cull Off.
                vh.AddTriangle(centerIndex, b, a);
            }

            // --- outline of the value polygon ---
            if (valueOutlineColor.a > 0f && outlineThickness > 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = center + AxisDir(i, n) * radius * _values[i];
                    Vector2 b = center + AxisDir((i + 1) % n, n) * radius * _values[(i + 1) % n];
                    AddLine(vh, a, b, outlineThickness, valueOutlineColor);
                }
            }

            LastVertexCount = vh.currentVertCount;
        }

        private Vector2 AxisDir(int index, int count)
        {
            float deg = startAngleDegrees + 360f * index / count;
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>Thick line as one clockwise quad, matching VertexHelper.AddUIVertexQuad's ordering.</summary>
        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 c)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.0001f) return;

            Vector2 dir = delta.normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * Mathf.Max(0.1f, thickness) * 0.5f;

            s_quad[0] = MakeVert(a - normal, c);
            s_quad[1] = MakeVert(a + normal, c);
            s_quad[2] = MakeVert(b + normal, c);
            s_quad[3] = MakeVert(b - normal, c);

            vh.AddUIVertexQuad(s_quad);
        }

        private static UIVertex MakeVert(Vector2 pos, Color32 c)
        {
            var v = UIVertex.simpleVert;
            v.position = pos;
            v.color = c;
            v.uv0 = Vector2.zero;
            return v;
        }

        // ---- labels ------------------------------------------------------------------------------

        private void SyncLabels()
        {
            if (labelTemplate == null) return;

            var parent = labelContainer != null ? labelContainer : rectTransform;
            if (parent == null) return;

            if (labelTemplate.gameObject.activeSelf) labelTemplate.gameObject.SetActive(false);

            while (_labelPool.Count < _axisCount)
            {
                var clone = Instantiate(labelTemplate, parent);
                clone.name = $"AxisLabel_{_labelPool.Count}";
                clone.raycastTarget = false;
                clone.gameObject.SetActive(true);
                _labelPool.Add(clone);
            }

            for (int i = 0; i < _labelPool.Count; i++)
            {
                var label = _labelPool[i];
                if (label == null) continue;

                bool visible = i < _axisCount && !string.IsNullOrEmpty(_labels[i]);
                if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
                if (!visible) continue;

                label.text = _labels[i];
            }

            PositionLabels();
        }

        private void PositionLabels()
        {
            if (_axisCount < MinAxes || _labelPool.Count == 0) return;

            Rect r = rectTransform.rect;
            Vector2 center = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f * Mathf.Clamp01(radiusScale);

            for (int i = 0; i < _axisCount && i < _labelPool.Count; i++)
            {
                var label = _labelPool[i];
                if (label == null || !label.gameObject.activeSelf) continue;

                var rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = center + AxisDir(i, _axisCount) * (radius + labelPadding);
            }
        }

        private void WarnAxisCount(string message)
        {
            if (_warnedAxisCount) return;
            _warnedAxisCount = true;
            Debug.LogWarning($"[JRPG.Menu] {message}", this);
        }

        // ---- Graphic plumbing ---------------------------------------------------------------------

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            PositionLabels();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (labelTemplate != null && labelTemplate.gameObject.activeSelf)
                labelTemplate.gameObject.SetActive(false);
            PositionLabels();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            PositionLabels();
        }
#endif
    }
}
