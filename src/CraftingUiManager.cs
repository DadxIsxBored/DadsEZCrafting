using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DadsEZCrafting
{
    internal sealed class CraftingUiManager : MonoBehaviour
    {
        private const float ToolbarHeight = 92f;
        private static readonly MethodInfo UpdateCraftingPanelMethod = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel", new[] { typeof(bool) });

        internal static CraftingUiManager Instance { get; private set; }

        private InventoryGui _gui;
        private RectTransform _viewport;
        private Vector2 _originalViewportOffsetMax;
        private RectTransform _toolbar;
        private TMP_InputField _search;
        private Button _categoryButton;
        private Button _tierButton;
        private Button _sortButton;
        private TMP_Text _categoryLabel;
        private TMP_Text _tierLabel;
        private TMP_Text _sortLabel;

        internal static void Attach(InventoryGui gui)
        {
            if (gui == null) return;
            CraftingUiManager manager = gui.GetComponent<CraftingUiManager>();
            if (manager == null) manager = gui.gameObject.AddComponent<CraftingUiManager>();
            manager.Initialize(gui);
        }

        internal static void RefreshRecipes()
        {
            if (Instance?._gui == null || UpdateCraftingPanelMethod == null) return;
            UpdateCraftingPanelMethod.Invoke(Instance._gui, new object[] { false });
        }

        private void Initialize(InventoryGui gui)
        {
            if (_gui != null) return;
            _gui = gui;
            Instance = this;
            BuildToolbar();
            RefreshLabels();
        }

        private void BuildToolbar()
        {
            RectTransform content = _gui.m_recipeListRoot;
            _viewport = content == null ? null : content.parent as RectTransform;
            if (_viewport == null || _viewport.parent == null)
            {
                DadsEZCraftingPlugin.ModLog.LogError("The native recipe-list viewport was not found; filtering remains active without the toolbar.");
                return;
            }

            _originalViewportOffsetMax = _viewport.offsetMax;
            _viewport.offsetMax = new Vector2(_originalViewportOffsetMax.x, _originalViewportOffsetMax.y - ToolbarHeight - 4f);

            GameObject toolbarObject = new GameObject("DadsEZCrafting_FilterToolbar", typeof(RectTransform));
            _toolbar = toolbarObject.GetComponent<RectTransform>();
            _toolbar.SetParent(_viewport.parent, false);
            _toolbar.anchorMin = new Vector2(_viewport.anchorMin.x, _viewport.anchorMax.y);
            _toolbar.anchorMax = new Vector2(_viewport.anchorMax.x, _viewport.anchorMax.y);
            _toolbar.pivot = new Vector2(0.5f, 1f);
            _toolbar.offsetMin = new Vector2(_viewport.offsetMin.x, _originalViewportOffsetMax.y - ToolbarHeight);
            _toolbar.offsetMax = new Vector2(_viewport.offsetMax.x, _originalViewportOffsetMax.y);
            _toolbar.SetAsLastSibling();

            _search = CreateSearchField(_toolbar);
            SetRow(_search.GetComponent<RectTransform>(), 0f, 1f, 0f, 28f);
            _search.onValueChanged.AddListener(value =>
            {
                RecipeFilterState.Search = value ?? string.Empty;
                RefreshRecipes();
            });

            _categoryButton = CreateNativeButton(_toolbar, "Category", out _categoryLabel);
            SetRow(_categoryButton.GetComponent<RectTransform>(), 0f, 0.5f, 32f, 28f, 0f, -2f);
            _categoryButton.onClick.AddListener(() =>
            {
                RecipeFilterState.NextCategory();
                RefreshLabels();
                RefreshRecipes();
            });

            _tierButton = CreateNativeButton(_toolbar, "Tier", out _tierLabel);
            SetRow(_tierButton.GetComponent<RectTransform>(), 0.5f, 1f, 32f, 28f, 2f, 0f);
            _tierButton.onClick.AddListener(() =>
            {
                RecipeFilterState.NextTier();
                RefreshLabels();
                RefreshRecipes();
            });

            _sortButton = CreateNativeButton(_toolbar, "Sort", out _sortLabel);
            SetRow(_sortButton.GetComponent<RectTransform>(), 0f, 1f, 64f, 28f);
            _sortButton.onClick.AddListener(() =>
            {
                RecipeFilterState.NextSort();
                RefreshLabels();
                RefreshRecipes();
            });
        }

        private TMP_InputField CreateSearchField(Transform parent)
        {
            GameObject root = new GameObject("DadsEZCrafting_Search", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            CopyNativeImage(_gui.m_craftButton.GetComponent<Image>(), background);
            background.color = new Color(0.30f, 0.25f, 0.21f, 0.98f);

            GameObject areaObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            areaObject.transform.SetParent(root.transform, false);
            RectTransform area = (RectTransform)areaObject.transform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(8f, 2f);
            area.offsetMax = new Vector2(-8f, -2f);

            TextMeshProUGUI placeholder = CreateInputText(area, "Placeholder", new Color(0.72f, 0.68f, 0.62f, 0.78f));
            placeholder.text = "Search recipes...";
            placeholder.fontStyle = FontStyles.Italic;
            TextMeshProUGUI text = CreateInputText(area, "Text", new Color(1f, 0.72f, 0.36f, 1f));

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 80;
            return input;
        }

        private TextMeshProUGUI CreateInputText(Transform parent, string name, Color color)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
            text.font = _gui.m_recipeName.font;
            text.fontSize = 15f;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateNativeButton(Transform parent, string name, out TMP_Text label)
        {
            GameObject root = Instantiate(_gui.m_craftButton.gameObject, parent, false);
            root.name = "DadsEZCrafting_" + name;
            root.SetActive(true);
            Button button = root.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = root.GetComponentInChildren<TMP_Text>(true);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4f, 1f);
            label.rectTransform.offsetMax = new Vector2(-4f, -1f);
            return button;
        }

        private void RefreshLabels()
        {
            if (_categoryLabel == null) return;
            _categoryLabel.text = "Type: " + RecipeFilterState.CategoryLabel();
            _tierLabel.text = "Tier: " + RecipeFilterState.TierLabel();
            _sortLabel.text = "Sort: " + RecipeFilterState.SortLabel();
            Color native = Color.white;
            Color active = new Color(1f, 0.72f, 0.36f, 1f);
            _categoryLabel.color = RecipeFilterState.Category == RecipeCategory.All ? native : active;
            _tierLabel.color = RecipeFilterState.Tier == ProgressionTier.All ? native : active;
            _sortLabel.color = RecipeFilterState.Sort == RecipeSort.Native ? native : active;
        }

        private static void CopyNativeImage(Image source, Image target)
        {
            if (source == null || target == null) return;
            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }

        private static void SetRow(RectTransform rect, float minX, float maxX, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(minX, 1f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(right, -top);
        }

        private void OnDestroy()
        {
            if (_viewport != null) _viewport.offsetMax = _originalViewportOffsetMax;
            if (Instance == this) Instance = null;
        }
    }
}
