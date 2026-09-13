using System;
using System.Collections.Generic;
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
        private const int MaximumCraftQuantity = 9999;
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
        private GameObject _categoryMenu;
        private GameObject _tierMenu;
        private RectTransform _craftControls;
        private TMP_Text _quantityLabel;
        private Button _minusButton;
        private Button _plusButton;
        private Button _maxButton;
        private Recipe _lastSelectedRecipe;
        private ItemDrop.ItemData _lastUpgradeItem;
        private int _craftQuantity = 1;

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

        internal static void PrepareCrafting(InventoryGui gui)
        {
            if (Instance == null || gui != Instance._gui) return;
            bool canMultiCraft = Instance._craftQuantity > 1 && gui.m_selectedRecipe.ItemData == null;
            gui.m_multiCraftAmount = canMultiCraft ? Instance._craftQuantity : 1;
            gui.m_touchMultiCrafting = canMultiCraft;
        }

        private void Initialize(InventoryGui gui)
        {
            if (_gui != null) return;
            _gui = gui;
            Instance = this;
            BuildToolbar();
            BuildCraftControls();
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
                ToggleMenu(_categoryMenu, _tierMenu);
            });

            _tierButton = CreateNativeButton(_toolbar, "Tier", out _tierLabel);
            SetRow(_tierButton.GetComponent<RectTransform>(), 0.5f, 1f, 32f, 28f, 2f, 0f);
            _tierButton.onClick.AddListener(() =>
            {
                ToggleMenu(_tierMenu, _categoryMenu);
            });

            _sortButton = CreateNativeButton(_toolbar, "Sort", out _sortLabel);
            SetRow(_sortButton.GetComponent<RectTransform>(), 0f, 1f, 64f, 28f);
            _sortButton.onClick.AddListener(() =>
            {
                RecipeFilterState.NextSort();
                RefreshLabels();
                RefreshRecipes();
            });

            _categoryMenu = CreateDropdownMenu(
                _categoryButton,
                "CategoryMenu",
                Array.ConvertAll(RecipeFilterState.Categories, value => new DropdownChoice
                {
                    Label = CategoryLabel(value),
                    Select = () =>
                    {
                        RecipeFilterState.SetCategory(value);
                        CloseMenus();
                        RefreshLabels();
                        RefreshRecipes();
                    }
                }));

            _tierMenu = CreateDropdownMenu(
                _tierButton,
                "TierMenu",
                Array.ConvertAll(RecipeFilterState.Tiers, value => new DropdownChoice
                {
                    Label = TierLabel(value),
                    Select = () =>
                    {
                        RecipeFilterState.SetTier(value);
                        CloseMenus();
                        RefreshLabels();
                        RefreshRecipes();
                    }
                }));
        }

        private void BuildCraftControls()
        {
            RectTransform craftButtonRect = _gui.m_craftButton == null ? null : _gui.m_craftButton.GetComponent<RectTransform>();
            if (craftButtonRect == null || craftButtonRect.parent == null) return;

            GameObject controlsObject = new GameObject("DadsEZCrafting_CraftControls", typeof(RectTransform));
            _craftControls = controlsObject.GetComponent<RectTransform>();
            _craftControls.SetParent(craftButtonRect.parent, false);
            _craftControls.SetSiblingIndex(craftButtonRect.GetSiblingIndex());
            CopyRect(craftButtonRect, _craftControls);

            craftButtonRect.SetParent(_craftControls, false);
            SetSlice(craftButtonRect, 0.48f, 1f, 2f, 0f);

            RectTransform selector = new GameObject("QuantitySelector", typeof(RectTransform)).GetComponent<RectTransform>();
            selector.SetParent(_craftControls, false);
            SetSlice(selector, 0f, 0.46f, 0f, -2f);

            _minusButton = CreateNativeButton(selector, "QuantityMinus", out TMP_Text minusLabel);
            minusLabel.text = "-";
            SetSlice(_minusButton.GetComponent<RectTransform>(), 0f, 0.18f, 0f, -1f);
            _minusButton.onClick.AddListener(() => ChangeQuantity(-ModifierStep()));

            Button countButton = CreateNativeButton(selector, "Quantity", out _quantityLabel);
            countButton.enabled = false;
            SetSlice(countButton.GetComponent<RectTransform>(), 0.18f, 0.43f, 1f, -1f);

            _plusButton = CreateNativeButton(selector, "QuantityPlus", out TMP_Text plusLabel);
            plusLabel.text = "+";
            SetSlice(_plusButton.GetComponent<RectTransform>(), 0.43f, 0.61f, 1f, -1f);
            _plusButton.onClick.AddListener(() => ChangeQuantity(ModifierStep()));

            _maxButton = CreateNativeButton(selector, "QuantityMax", out TMP_Text maxLabel);
            maxLabel.text = "Max";
            SetSlice(_maxButton.GetComponent<RectTransform>(), 0.61f, 1f, 1f, 0f);
            _maxButton.onClick.AddListener(SetMaximumQuantity);

            _lastSelectedRecipe = _gui.m_selectedRecipe.Recipe;
            _lastUpgradeItem = _gui.m_selectedRecipe.ItemData;
            RefreshQuantityControls();
        }

        private GameObject CreateDropdownMenu(Button owner, string name, IReadOnlyList<DropdownChoice> choices)
        {
            const float rowHeight = 27f;
            GameObject root = new GameObject("DadsEZCrafting_" + name, typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(owner.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -rowHeight * choices.Count);
            rect.offsetMax = Vector2.zero;
            Image background = root.GetComponent<Image>();
            CopyNativeImage(_gui.m_craftButton.GetComponent<Image>(), background);
            background.color = new Color(0.16f, 0.13f, 0.11f, 1f);

            for (int index = 0; index < choices.Count; index++)
            {
                DropdownChoice choice = choices[index];
                Button option = CreateNativeButton(rect, name + "_" + index, out TMP_Text label);
                label.text = choice.Label;
                RectTransform optionRect = option.GetComponent<RectTransform>();
                optionRect.anchorMin = new Vector2(0f, 1f);
                optionRect.anchorMax = new Vector2(1f, 1f);
                optionRect.pivot = new Vector2(0.5f, 1f);
                optionRect.offsetMin = new Vector2(2f, -rowHeight * (index + 1) + 1f);
                optionRect.offsetMax = new Vector2(-2f, -rowHeight * index - 1f);
                option.onClick.AddListener(() => choice.Select());
            }

            root.SetActive(false);
            return root;
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
            _categoryLabel.text = "Type: " + RecipeFilterState.CategoryLabel() + "  v";
            _tierLabel.text = "Tier: " + RecipeFilterState.TierLabel() + "  v";
            _sortLabel.text = "Sort: " + RecipeFilterState.SortLabel();
            Color native = Color.white;
            Color active = new Color(1f, 0.72f, 0.36f, 1f);
            _categoryLabel.color = RecipeFilterState.Category == RecipeCategory.All ? native : active;
            _tierLabel.color = RecipeFilterState.Tier == ProgressionTier.All ? native : active;
            _sortLabel.color = RecipeFilterState.Sort == RecipeSort.Native ? native : active;
        }

        private void Update()
        {
            if (_gui == null) return;
            Recipe selectedRecipe = _gui.m_selectedRecipe.Recipe;
            ItemDrop.ItemData selectedUpgrade = _gui.m_selectedRecipe.ItemData;
            if (selectedRecipe != _lastSelectedRecipe || selectedUpgrade != _lastUpgradeItem)
            {
                _lastSelectedRecipe = selectedRecipe;
                _lastUpgradeItem = selectedUpgrade;
                _craftQuantity = 1;
                RefreshQuantityControls();
            }

            PrepareCrafting(_gui);

            if (Input.GetKeyDown(KeyCode.Escape)) CloseMenus();
            if (Input.GetMouseButtonDown(0) && !PointerInside(_categoryButton) && !PointerInside(_tierButton) &&
                !PointerInside(_categoryMenu == null ? null : _categoryMenu.transform) &&
                !PointerInside(_tierMenu == null ? null : _tierMenu.transform))
            {
                CloseMenus();
            }
        }

        private void ChangeQuantity(int delta)
        {
            if (!CanSelectQuantity()) return;
            _craftQuantity = Mathf.Clamp(_craftQuantity + delta, 1, MaximumCraftQuantity);
            RefreshQuantityControls();
            RefreshRecipes();
        }

        private void SetMaximumQuantity()
        {
            if (!CanSelectQuantity()) return;
            Player player = Player.m_localPlayer;
            Recipe recipe = _gui.m_selectedRecipe.Recipe;
            int quality = 1;
            int low = 0;
            int high = 1;
            while (high < MaximumCraftQuantity && player.HaveRequirements(recipe, false, quality, high))
            {
                low = high;
                high = Math.Min(MaximumCraftQuantity, high * 2);
                if (high == low) break;
            }
            while (low + 1 < high)
            {
                int middle = low + (high - low) / 2;
                if (player.HaveRequirements(recipe, false, quality, middle)) low = middle;
                else high = middle;
            }
            if (high == MaximumCraftQuantity && player.HaveRequirements(recipe, false, quality, high)) low = high;
            _craftQuantity = Mathf.Max(1, low);
            RefreshQuantityControls();
            RefreshRecipes();
        }

        private bool CanSelectQuantity()
        {
            return _gui != null && Player.m_localPlayer != null && _gui.m_selectedRecipe.Recipe != null && _gui.m_selectedRecipe.ItemData == null;
        }

        private void RefreshQuantityControls()
        {
            if (_quantityLabel == null) return;
            bool available = CanSelectQuantity();
            _quantityLabel.text = _craftQuantity.ToString();
            _minusButton.interactable = available && _craftQuantity > 1;
            _plusButton.interactable = available && _craftQuantity < MaximumCraftQuantity;
            _maxButton.interactable = available;
        }

        private static int ModifierStep()
        {
            return Input.GetKey(KeyCode.LeftControl) ? 10 : 1;
        }

        private void ToggleMenu(GameObject menu, GameObject other)
        {
            if (menu == null) return;
            if (other != null) other.SetActive(false);
            bool show = !menu.activeSelf;
            menu.SetActive(show);
            if (show) menu.transform.parent.SetAsLastSibling();
        }

        private void CloseMenus()
        {
            if (_categoryMenu != null) _categoryMenu.SetActive(false);
            if (_tierMenu != null) _tierMenu.SetActive(false);
        }

        private static bool PointerInside(Component component)
        {
            if (component == null || !component.gameObject.activeInHierarchy) return false;
            RectTransform rect = component.GetComponent<RectTransform>();
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition);
        }

        private static string CategoryLabel(RecipeCategory category)
        {
            return category == RecipeCategory.Ammunition ? "Ammo / Throwables" : category.ToString();
        }

        private static string TierLabel(ProgressionTier tier)
        {
            if (tier == ProgressionTier.All) return "All Tiers";
            if (tier == ProgressionTier.BlackForest) return "Black Forest";
            if (tier == ProgressionTier.DeepNorth) return "Deep North";
            return tier.ToString();
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

        private static void SetSlice(RectTransform rect, float minX, float maxX, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(right, 0f);
            rect.localScale = Vector3.one;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private sealed class DropdownChoice
        {
            internal string Label;
            internal Action Select;
        }

        private void OnDestroy()
        {
            if (_viewport != null) _viewport.offsetMax = _originalViewportOffsetMax;
            if (Instance == this) Instance = null;
        }
    }
}
