using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.UI.Management;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace SpecialMixingTerminal;

internal static class TerminalClipboard
{
    private static TerminalConfigurable? _panelOwner;

    internal static void BeforeDestroyPanel()
    {
        if (_panelOwner != null && !_panelOwner.IsDestroyed) _panelOwner.ResetFieldCallbacks();
        _panelOwner = null;
    }
    internal static bool BeforeGetTypeName(EConfigurableType __0, ref string __result)
    {
        if (__0 != TerminalConfigurable.TerminalType) return true;
        __result = "Mixing Terminal"; return false;
    }

    internal static bool BeforeReplicateField(EntityConfiguration __instance)
        => __instance.Configurable?.TryCast<TerminalConfigurable>() == null;

    internal static bool BeforeInitializePanel(ManagementInterface __instance)
    {
        if (__instance.Configurables == null || __instance.Configurables.Count != 1) return true;
        var owner = __instance.Configurables[0].TryCast<TerminalConfigurable>();
        if (owner == null) return true;
        BuildPanel(__instance, owner);
        return false;
    }

    private static void BuildPanel(ManagementInterface ui, TerminalConfigurable owner)
    {
        ui.DestroyConfigPanel();
        owner.ResetFieldCallbacks();
        owner.RefreshFields();
        _panelOwner = owner;
        var template = ui.GetConfigPanelPrefab(EConfigurableType.Chemist).Cast<ChemistConfigPanel>();
        var panel = Object.Instantiate(template, ui.PanelContainer, false);
        ui.loadedPanel = panel;
        var root = panel.GetComponent<RectTransform>();
        Place(root, 0, 0, 1, 1);
        for (var i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);

        // Reuse native rows, icons, remove buttons, counts, edit controls and selectors.
        var content = root;
        Single(template.BedUI, content, owner.ProductSupply, "Product supply", .005f, .10f);
        Single(template.BedUI, content, owner.OutputShelf, "Output", .12f, .10f);
        Multiple(template.StationsUI, content, owner.Mixers, "Mixers", .265f, .315f);
        Multiple(template.StationsUI, content, owner.IngredientSupplies, "Ingredient shelves", .64f, .315f);
        if (owner.Plan.NativeJobsOwned)
            foreach (var control in content.GetComponentsInChildren<Selectable>(true)) control.interactable = false;
        panel.gameObject.SetActive(true);
        panel.ConfigureScreen(ui.UIScreen);
    }

    private static void Single(ObjectFieldUI template, Transform parent, ObjectField field, string label, float y, float h)
    {
        var ui = Object.Instantiate(template, parent, false);
        ui.name = label;
        Place(ui.GetComponent<RectTransform>(), .015f, y, .97f, h);
        ui.FieldLabel.text = label;
        Place(ui.FieldLabel.GetComponent<RectTransform>(), 0, 0, .41f, 1);
        // Selection children use the selection bar's coordinate space, not the
        // whole field. Laying each out against the whole row squeezed it twice.
        Place(ui.SelectionLabel.transform.parent.GetComponent<RectTransform>(), .43f, 0, .57f, 1);
        Place(ui.SelectionLabel.GetComponent<RectTransform>(), .16f, 0, .69f, 1);
        Place(ui.IconImg.GetComponent<RectTransform>(), .015f, .18f, .12f, .64f);
        Place(ui.NoneSelected.GetComponent<RectTransform>(), .04f, 0, .79f, 1);
        Place(ui.MultipleSelected.GetComponent<RectTransform>(), .04f, 0, .79f, 1);
        FitText(ui.gameObject, 20, 28);
        ui.InstructionText = label == "Output" ? "Select a storage shelf, Packaging Station Mk I / Mk II, or Brick Press on this property." : "Select a storage shelf on this property.";
        ui.ExtendedInstructionText = ui.InstructionText;
        var fields = new Il2CppSystem.Collections.Generic.List<ObjectField>(); fields.Add(field);
        ui.Bind(fields);
        // The native NoneSelected object is a crossed-out graphic, not a text
        // label. Stretching it to the selection bar made the giant black X.
        foreach (var graphic in ui.NoneSelected.GetComponentsInChildren<Image>(true)) graphic.enabled = false;
        foreach (var graphic in ui.MultipleSelected.GetComponentsInChildren<Image>(true)) graphic.enabled = false;
        // The native refresh positions the clear graphic against its original
        // locker row. Keep it in this row's coordinates after every refresh.
        void RefreshClear(BuildableItem ignored)
        {
            ui.ClearButton.SetParent(ui.transform, false);
            Place(ui.ClearButton, .94f, .24f, .055f, .52f);
            foreach (var graphic in ui.ClearButton.GetComponentsInChildren<Image>(true)) graphic.preserveAspect = true;
            ui.ClearButton.gameObject.SetActive(field.SelectedObject != null);
        }
        field.onObjectChanged.AddListener((UnityAction<BuildableItem>)RefreshClear);
        RefreshClear(null!);
        ui.gameObject.SetActive(true);
    }

    private static void Multiple(ObjectListFieldUI template, Transform parent, ObjectListField field, string label, float y, float h)
    {
        var ui = Object.Instantiate(template, parent, false);
        ui.name = label;
        Place(ui.GetComponent<RectTransform>(), .015f, y, .97f, h);
        ui.FieldText = label;
        ui.FieldLabel.raycastTarget = false;
        ui.InstructionText = label == "Mixers" ? "Select one to four mixers in stage order, one per ingredient." : "Select up to four ingredient storage shelves.";
        ui.ExtendedInstructionText = ui.InstructionText;
        var entries = new Il2CppReferenceArray<RectTransform>(RecipePlan.EquipmentLimit);
        var contents = ui.Entries[0].parent.GetComponent<RectTransform>();
        var layout = contents.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        Place(contents, 0, .26f, 1, .74f);
        for (var i = RecipePlan.EquipmentLimit; i < ui.Entries.Length; i++) ui.Entries[i].gameObject.SetActive(false);
        for (var i = 0; i < RecipePlan.EquipmentLimit; i++)
        {
            entries[i] = i < ui.Entries.Length ? ui.Entries[i] : Object.Instantiate(ui.Entries[0], ui.Entries[0].parent, false);
            entries[i].name = label + " " + (i + 1);
            // Four full-width native rows, in clipboard selection order.
            Place(entries[i], 0, i * .25f, 1, .225f);
            Child(entries[i], "Title", .095f, 0, .80f, 1);
            Child(entries[i], "Icon", .015f, .12f, .065f, .76f);
            Child(entries[i], "Remove", .93f, .16f, .055f, .68f);
            Child(entries[i], "None", .04f, 0, .82f, 1);
            Child(entries[i], "Mixed", .04f, 0, .82f, 1);
            entries[i].gameObject.SetActive(true);
        }
        ui.Entries = entries;
        Place(ui.FieldLabel.GetComponent<RectTransform>(), 0, 0, .76f, .20f);
        var hint = ui.transform.Find("Hint")?.GetComponent<RectTransform>();
        if (hint != null) Place(hint, .79f, .02f, .06f, .16f);
        Place(ui.Button.GetComponent<RectTransform>(), 0, 0, 1, .20f);
        Child(ui.Button.transform, "Edit", .92f, .10f, .065f, .80f);
        var fields = new Il2CppSystem.Collections.Generic.List<ObjectListField>(); fields.Add(field);
        ui.Bind(fields);
        FitText(ui.gameObject, 19, 26);
        ui.gameObject.SetActive(true);
    }

    private static void Child(Transform parent, string name, float x, float y, float w, float h)
    {
        var child = parent.Find(name)?.GetComponent<RectTransform>();
        if (child != null) Place(child, x, y, w, h);
    }

    private static void FitText(GameObject root, float min, float max)
    {
        foreach (var text in root.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true))
        { text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max; text.fontSize = max; text.enableWordWrapping = false; text.overflowMode = Il2CppTMPro.TextOverflowModes.Ellipsis; }
    }

    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = new Vector2(x, 1 - y - h); rect.anchorMax = new Vector2(x + w, 1 - y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
}
