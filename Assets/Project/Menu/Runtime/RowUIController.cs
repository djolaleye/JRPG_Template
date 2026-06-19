using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    [DisallowMultipleComponent]
    public class RowUIController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text quantity;
        [SerializeField] private TMP_Text cost;
        [SerializeField] private Image icon;
        [SerializeField] private Image background;
        [SerializeField] private Selectable selectable;

        [Header("State colors")]
        [SerializeField] private Color normalColor = new(0.20f, 0.20f, 0.25f, 0.92f);
        [SerializeField] private Color selectedColor = new(0.95f, 0.85f, 0.30f, 1.0f);
        [SerializeField] private Color disabledColor = new(0.30f, 0.30f, 0.30f, 0.6f);
        [SerializeField] private Color invalidColor = new(0.60f, 0.20f, 0.20f, 0.9f);
        [SerializeField] private Color confirmedColor = new(0.30f, 0.85f, 0.30f, 1.0f);

        public RowModel Model { get; private set; }
        public Selectable Selectable => selectable;

        public void Bind(RowModel model)
        {
            Model = model;
            if (label != null) label.text = model.label ?? "";
            if (quantity != null) quantity.text = model.quantityText ?? "";
            if (cost != null) cost.text = model.costText ?? "";
            if (icon != null)
            {
                icon.sprite = model.icon;
                icon.enabled = model.icon != null;
            }
            SetVisualState(model.enabled ? RowState.Normal : RowState.Disabled);
            if (selectable != null) selectable.interactable = model.enabled;
        }

        public void SetVisualState(RowState state)
        {
            if (background == null) return;
            background.color = state switch
            {
                RowState.Selected => selectedColor,
                RowState.Disabled => disabledColor,
                RowState.Invalid => invalidColor,
                RowState.Confirmed => confirmedColor,
                _ => normalColor
            };
        }
    }
}
