using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.State;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Il2CppInterop.Runtime;

namespace SpecialMixingTerminal;

/// <summary>Recipe workspace on the game's existing world-space computer canvas.</summary>
internal sealed partial class TerminalScreen
{
    private sealed record Choice(string Id, string Name, string Detail = "", Sprite? Icon = null, string Unavailable = "", bool Selected = false);
    private static readonly Color HandlerColor = new(.25f, .58f, .87f);
    private static readonly Color ChemistColor = new(.95f, .74f, .25f);
    private readonly List<Action> _staffDiagnostics = new();
    private readonly LaunderingStation _station;
    private readonly LaunderingInterface _native;
    private readonly string _path;
    private readonly MelonLogger.Instance _log;
    private readonly RecipePlan _plan;
    private readonly TerminalConfigurable _owner;
    private readonly TextMeshProUGUI _font;
    private readonly Image _background;
    private readonly RectTransform _root;
    private readonly RectTransform _body;
    private readonly TextMeshProUGUI _status;
    private readonly Button _recipeTab;
    private readonly Button _teamTab;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private string _lastWiringError = "";
    private RectTransform? _liveFlow;
    private float _nextFlow;
    private string _flowSignature = "";
    private readonly Il2CppSystem.Action _onClose;
    private readonly GameInput.ExitDelegate _exit;
    private readonly float _unit;
    private RectTransform? _picker;
    private RectTransform? _dropdown;
    private bool _teamPage;
    private bool _dirty;
    private List<Choice>? _ingredientChoices;
    private TerminalConfigurable Owner => _owner;

    internal TerminalScreen(LaunderingStation station, string path, MelonLogger.Instance log, RecipePlan plan)
    {
        _station = station;
        _owner = station.GetComponent<TerminalConfigurable>();
        _native = station.Interface;
        _path = path;
        _log = log;
        _plan = plan;
        _plan.PropertyCode = station.ParentProperty?.PropertyCode ?? "";
        _font = _native.container.Find("OperationList/Title").GetComponent<TextMeshProUGUI>();
        _background = _native.container.Find("Background").GetComponent<Image>();
        _unit = Mathf.Max(_native.container.rect.width, 1f);

        // Keep the original hierarchy as our style library. The new workspace
        // shares its canvas, raycaster, camera target, and state machine.
        for (var i = 0; i < _native.container.childCount; i++)
            _native.container.GetChild(i).gameObject.SetActive(false);
        _root = Rect("Mixing workspace", _native.container);
        Place(_root, 0, 0, 1, 1);
        _root.localRotation = _background.transform.localRotation;
        Panel("Desktop", _root, 0, 0, 1, 1, _background.color);
        var titleBar = _native.container.Find("Topbar")?.GetComponent<Image>();
        Panel("Title bar", _root, 0, 0, 1, .085f, titleBar?.color ?? new Color(.12f, .18f, .23f));
        Label(_root, "SPECIAL MIXING TERMINAL", .025f, .013f, .6f, .055f, .025f, Color.white, FontStyles.Bold);
        Label(_root, station.ParentProperty?.PropertyName ?? "Property", .67f, .018f, .24f, .045f, .018f, Color.white, alignment: TextAlignmentOptions.Right);
        Button(_root, "×", .94f, .012f, .045f, .06f, Close);

        _recipeTab = Button(_root, "Recipe", .025f, .102f, .17f, .056f, () => { _teamPage = false; BuildPage(); });
        _teamTab = Button(_root, "Staff & routing", .205f, .102f, .28f, .056f, () => { _teamPage = true; BuildPage(); });
        _body = Rect("Workspace", _root);
        Place(_body, .025f, .18f, .95f, .71f);
        _status = Label(_root, "", .03f, .924f, .71f, .05f, .017f);
        _startButton = Button(_root, "Start", .72f, .102f, .12f, .056f, () =>
        {
            Save();
            if (_lastWiringError != "") return;
            var message = NativeJobs.Start(Owner); BuildPage(); _status.text = message;
        });
        _stopButton = Button(_root, "Stop", .855f, .102f, .12f, .056f, () =>
        {
            var error = NativeJobs.Release(Owner); BuildPage();
            _status.text = error == "" ? "Stopped · Employee jobs cleared; payment lockers kept." : error;
        });
        _onClose = (Il2CppSystem.Action)HandleNativeClose;
        var parentState = Object.FindObjectOfType<InGameStateMachine>()
            ?? throw new InvalidOperationException("The game's input state machine is unavailable.");
        _native.mainState.InitializeDefaultParent(parentState);
        _native.mainState.add_OnRemovedFromStack(_onClose);
        _exit = DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>(new Action<ExitAction>(action =>
        {
            if (!_native.IsOpen || action.Used) return;
            action.Use();
            Close();
        })) ?? throw new InvalidOperationException("Could not register terminal exit input.");
        GameInput.RegisterExitListener(_exit, 10000);
        BuildPage();
        _log.Msg($"Native computer workspace created for terminal {station.GUID}.");
    }

    internal void Open()
    {
        if (_native.IsOpen) return;
        BuildPage();
        _native.Open();
        ResetAllScrolls();
        _log.Msg($"Opened mixing workspace with native camera transition at {_station.ParentProperty?.PropertyName}.");
    }

    internal void Tick()
    {
        if (!_native.IsOpen) return;
        if (_teamPage && _liveFlow != null && Time.realtimeSinceStartup >= _nextFlow)
        {
            _nextFlow = Time.realtimeSinceStartup + .5f;
            BuildLiveFlow(_liveFlow);
            foreach (var refresh in _staffDiagnostics) refresh();
        }
    }

    internal void Dispose()
    {
        GameInput.DeregisterExitListener(_exit);
        if (_native != null && _native.mainState != null) _native.mainState.remove_OnRemovedFromStack(_onClose);
    }

    private void Close()
    {
        if (_dropdown != null) { DismissDropdown(); return; }
        if (_picker != null) { ClosePicker(); return; }
        _native.mainState.PopFromDefaultParent();
    }

    private void HandleNativeClose()
    {
        DismissDropdown();
        if (_picker != null) { Object.Destroy(_picker.gameObject); _picker = null; }
        if (!_plan.NativeJobsOwned) Save();
        _native.OnClose();
        Mod.ScreenClosed(this);
    }

    private void ClosePicker()
    {
        DismissPicker();
    }

    private void DismissPicker()
    {
        if (_picker != null) { Object.Destroy(_picker.gameObject); _picker = null; }
    }

    private void Save()
    {
        try
        {
            _plan.Save(_path);
            _dirty = false;
            _lastWiringError = MixerWiring.Apply(Owner);
            _status.text = _lastWiringError == "" ? "Saved · Mixer filters, threshold 20 and destinations applied." : "Draft saved · " + _lastWiringError;
        }
        catch (Exception e)
        {
            _lastWiringError = "Could not save or apply recipe.";
            _status.text = "Could not save recipe. See MelonLoader log.";
            _log.Error("Could not save terminal configuration: " + e);
        }
    }

    private void Changed()
    {
        _dirty = true;
        _status.text = "Unsaved changes · Closing the computer also saves your recipe.";
    }

    private void BuildPage()
    {
        DismissDropdown();
        _liveFlow = null; _flowSignature = "";
        _staffDiagnostics.Clear();
        Clear(_body);
        _recipeTab.interactable = _teamPage;
        _teamTab.interactable = !_teamPage;
        if (_teamPage) BuildTeam(); else BuildRecipe();
        _startButton.interactable = !_plan.NativeJobsOwned;
        _stopButton.interactable = _plan.NativeJobsOwned;
        ResetAllScrolls();
        if (!_dirty) _status.text = _plan.NativeJobsOwned ? NativeJobs.Status(Owner) : "Ready checks do not start employees. Press Start to apply jobs.";
    }

    private void BuildRecipe()
    {
        if (_plan.NativeJobsOwned)
        {
            Label(_body, "NATIVE EMPLOYEE JOBS", 0, .02f, 1, .09f, .023f, style: FontStyles.Bold);
            Label(_body, NativeJobs.Status(Owner), 0, .16f, 1, .2f, .021f);
            Label(_body, "Release this terminal's jobs on the Staff & routing tab before editing the recipe or clipboard connections. Payment lockers are preserved.", 0, .40f, 1, .25f, .020f);
            return;
        }
        Input(_body, _plan.Name, "Recipe name", 0, 0, .65f, .10f,
            value => { _plan.Name = string.IsNullOrWhiteSpace(value) ? "Special mix" : value.Trim(); Changed(); });
        Label(_body, $"{_plan.AssignedMixerIds.Count} mixers assigned", .69f, .01f, .31f, .08f, .019f, style: FontStyles.Bold);
        var mapping = "Four distinct mixers · Threshold 20 · Each ingredient uses its own mixer.";
        Label(_body, mapping, 0, .12f, 1, .065f, .017f);

        Label(_body, $"MIXING ORDER · {_plan.Steps.Count} ingredients", 0, .21f, .70f, .06f, .020f, style: FontStyles.Bold);
        var addIngredient = Button(_body, "+ Add ingredient", .73f, .205f, .27f, .065f,
            () => Pick("Add an ingredient", Ingredients(), id => { _plan.Steps.Add(new IngredientStep { IngredientId = id }); Changed(); BuildPage(); }));
        addIngredient.interactable = _plan.Steps.Count < 4;
        var content = Scroll(_body, "Recipe steps", 0, .29f, 1, .71f);
        if (_plan.Steps.Count == 0)
        {
            var empty = Row(content, "Empty recipe", .16f);
            Label(empty, "Build your mix one ingredient at a time.", .025f, .1f, .94f, .35f, .020f, style: FontStyles.Bold);
            Label(empty, "Assign equipment with clipboard 9, then choose supplies and mixer order here.", .025f, .48f, .94f, .36f, .017f);
        }
        for (var i = 0; i < _plan.Steps.Count; i++) BuildStep(content, i);
    }

    private void BuildStep(RectTransform content, int index)
    {
        var step = _plan.Steps[index];
        var row = Row(content, "Recipe ingredient", .068f);
        var background = Panel("Row background", row, 0, .025f, 1, .95f, new Color(.90f, .51f, 0f));
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.raycastTarget = false;
        var up = RecipeButton(row, "↑", .005f, .12f, .043f, .76f, () => MoveStep(index, -1));
        up.interactable = index > 0;
        var down = RecipeButton(row, "↓", .052f, .12f, .043f, .76f, () => MoveStep(index, 1));
        down.interactable = index + 1 < _plan.Steps.Count;
        Icon(row, Registry.GetItem(step.IngredientId)?.Icon, .11f, .12f, .055f, .76f);
        var ingredient = RecipeButton(row, ItemName(step.IngredientId), .175f, .10f, .32f, .80f,
            () => Pick("Replace ingredient", Ingredients(), id => { step.IngredientId = id; Changed(); RebuildKeepingRecipePosition(); }));
        ingredient.GetComponentInChildren<TextMeshProUGUI>(true).alignment = TextAlignmentOptions.Left;
        var supplies = new List<Choice>();
        if (_plan.IngredientShelfIds.Count == 1) supplies.Add(new Choice("", "Auto · Only assigned shelf"));
        supplies.AddRange(Supplies().Select((choice, i) => choice with { Name = (i + 1) + " · " + choice.Name }));
        InlineDropdown(row, EquipmentName(_plan.ResolveSupplyId(index), "Supply"), .51f, .19f, supplies,
            id => { step.SupplyShelfId = id; Changed(); RebuildKeepingRecipePosition(); });
        var mixers = new List<Choice> { new("", "Auto · Clipboard order") };
        mixers.AddRange(Mixers().Select((choice, i) => choice with { Name = (i + 1) + " · " + choice.Name }));
        InlineDropdown(row, MixerLabel(_plan.ResolveMixerId(index)), .71f, .19f, mixers,
            id => { step.MixerId = id; Changed(); RebuildKeepingRecipePosition(); });
        Label(row, "20", .908f, .12f, .042f, .76f, .018f, alignment: TextAlignmentOptions.Center);
        RecipeButton(row, "×", .958f, .12f, .037f, .76f, () => { _plan.Steps.RemoveAt(index); Changed(); RebuildKeepingRecipePosition(); });
    }

    private void RebuildKeepingRecipePosition()
    {
        var current = _body.GetComponentsInChildren<ScrollRect>().FirstOrDefault(scroll => scroll.name == "Recipe steps");
        var position = current?.verticalNormalizedPosition ?? 1f;
        BuildPage();
        var next = _body.GetComponentsInChildren<ScrollRect>().FirstOrDefault(scroll => scroll.name == "Recipe steps");
        if (next != null) { next.StopMovement(); next.verticalNormalizedPosition = position; }
    }

    private void InlineDropdown(Transform parent, string caption, float x, float width, List<Choice> choices, Action<string> selected)
    {
        Button? button = null;
        button = RecipeButton(parent, caption, x, .12f, width, .76f,
            () => ShowDropdown(button!.GetComponent<RectTransform>(), choices, selected));
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        Place(label.GetComponent<RectTransform>(), .025f, .06f, .82f, .88f);
        // Draw the chevron from UI geometry; the native font lacks the old glyph.
        ChevronStroke(button.transform, .875f, -45, label.color);
        ChevronStroke(button.transform, .91f, 45, label.color);
    }

    private Button RecipeButton(Transform parent, string text, float x, float y, float w, float h, Action action)
    {
        var button = Button(parent, text, x, y, w, h, action);
        // The laundering button's irregular artwork stretches across narrow
        // inline controls. A plain rectangle keeps row edges and gaps aligned.
        button.image.sprite = null;
        button.image.type = Image.Type.Simple;
        var colors = button.colors;
        colors.normalColor = new Color(.90f, .51f, 0f);
        colors.highlightedColor = new Color(.98f, .61f, .08f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.76f, .40f, 0f);
        button.colors = colors;
        button.image.color = colors.normalColor;
        return button;
    }

    private void ChevronStroke(Transform parent, float x, float rotation, Color color)
    {
        var rect = Rect("Dropdown chevron", parent);
        Place(rect, x, .47f, .05f, .045f);
        rect.localRotation = Quaternion.Euler(0, 0, rotation);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
    }

    private void DismissDropdown()
    {
        if (_dropdown == null) return;
        _dropdown.gameObject.SetActive(false);
        Object.Destroy(_dropdown.gameObject); _dropdown = null;
    }

    private void ShowDropdown(RectTransform anchor, List<Choice> choices, Action<string> selected)
    {
        DismissDropdown();
        _dropdown = Rect("Inline assignment dropdown", _root);
        Place(_dropdown, 0, 0, 1, 1);
        var blocker = Button(_dropdown, "", 0, 0, 1, 1, DismissDropdown);
        blocker.transition = Selectable.Transition.None; blocker.image.color = Color.clear;
        var local = _root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMin, 0)));
        var menuWidth = .34f;
        var menuHeight = Mathf.Min(.46f, Mathf.Max(1, choices.Count) * .060f + .015f);
        var menuX = Mathf.Clamp((local.x - _root.rect.xMin) / _root.rect.width, .01f, .99f - menuWidth);
        var menuY = 1 - (local.y - _root.rect.yMin) / _root.rect.height;
        if (menuY + menuHeight > .99f) menuY -= menuHeight + anchor.rect.height / _root.rect.height;
        menuY = Mathf.Clamp(menuY, .085f, .99f - menuHeight);
        var menu = Rect("Assigned equipment", _dropdown);
        Place(menu, menuX, menuY, menuWidth, menuHeight);
        Panel("Menu background", menu, 0, 0, 1, 1, _background.color);
        var content = Scroll(menu, "Assigned options", .015f, .015f, .97f, .97f);
        if (choices.Count == 0)
        {
            var row = Row(content, "No assigned equipment", .04f);
            Label(row, "Assign shelves with clipboard 9.", .02f, .05f, .96f, .90f, .017f);
        }
        foreach (var choice in choices)
        {
            var row = Row(content, choice.Name, .039f);
            Button(row, choice.Name, 0, .035f, 1, .93f, () => { DismissDropdown(); selected(choice.Id); });
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void MoveStep(int index, int direction)
    {
        var other = index + direction;
        if (other < 0 || other >= _plan.Steps.Count) return;
        (_plan.Steps[index], _plan.Steps[other]) = (_plan.Steps[other], _plan.Steps[index]);
        Changed(); RebuildKeepingRecipePosition();
    }

    private void BuildTeam()
    {
        _liveFlow = Rect("Actual equipment links", _body);
        Place(_liveFlow, 0, 0, .755f, 1);
        var staff = Rect("Terminal staff", _body); Place(staff, .78f, 0, .22f, 1);
        StaffSlot(staff, "Handler", _plan.AssignedHandlerIds, EEmployeeType.Handler, .015f);
        StaffSlot(staff, "Chemist", _plan.AssignedChemistIds, EEmployeeType.Chemist, .515f);
        BuildLiveFlow(_liveFlow);
    }

    private void StaffSlot(RectTransform parent, string title, List<string> ids, EEmployeeType role, float y)
    {
        var panel = Rect(title, parent); Place(panel, 0, y, 1, .46f);
        Label(panel, title, 0, 0, 1, .12f, .020f, style: FontStyles.Bold, alignment: TextAlignmentOptions.Center);
        var employee = ids.Count == 0 ? null : NativeJobs.Employee(ids[0]);
        var height = .52f * (_unit * .95f * .22f) / (_unit * .71f * .46f * .67f);
        height = Mathf.Min(height, .55f);
        var button = Button(panel, employee == null ? "+" : "", .24f, .16f, .52f, height, () => Pick(title, StaffChoices(role), id =>
        {
            var chosen = NativeJobs.Employee(id);
            var reason = chosen == null ? "Employee unavailable" : NativeJobs.Unavailable(Owner, chosen, role);
            if (reason != "") { _status.text = reason; return; }
            ids.Clear(); ids.Add(id); Changed(); BuildPage();
        }));
        button.interactable = !_plan.NativeJobsOwned;
        Tint(button, role == EEmployeeType.Handler ? HandlerColor : ChemistColor);
        if (employee == null) return;
        Icon(button.transform, employee.MugshotSprite, .04f, .04f, .92f, .92f);
        Label(panel, employee.FullName, 0, .76f, 1, .14f, .017f, alignment: TextAlignmentOptions.Center);
        var remove = Button(panel, "×", .79f, .15f, .19f, .17f, () => { ids.Clear(); Changed(); BuildPage(); });
        remove.interactable = !_plan.NativeJobsOwned;
        if (role == EEmployeeType.Handler)
        {
            var count = Label(panel, "", .73f, .36f, .27f, .18f, .014f, HandlerColor, alignment: TextAlignmentOptions.Center);
            var detail = Label(panel, "", 0, .91f, 1, .09f, .013f, alignment: TextAlignmentOptions.Center);
            void Refresh()
            {
                var routes = NativeJobs.HandlerRoutes(employee);
                count.text = $"{routes.Used}/{routes.Limit}\nroutes";
                var ready = NativeJobs.Ready(Owner, role);
                detail.text = _plan.NativeJobsOwned ? "Native routes assigned" : ready == "" ? $"{NativeJobs.RequiredRoutes(_plan).Count} routes ready" : ready;
                detail.color = ready == "" ? HandlerColor : LinkBad;
            }
            _staffDiagnostics.Add(Refresh); Refresh();
        }
    }

    private List<Choice> StaffChoices(EEmployeeType role)
        => NativeJobs.Employees()
            .Where(employee => employee.AssignedProperty == _station.ParentProperty && employee.EmployeeType == role)
            .OrderBy(employee => employee.FullName)
            .Select(employee => new Choice(employee.GUID.ToString(), employee.FullName, role.ToString(), employee.MugshotSprite,
                _plan.AssignedHandlerIds.Contains(employee.GUID.ToString()) || _plan.AssignedChemistIds.Contains(employee.GUID.ToString())
                    ? "Assigned to this terminal" : NativeJobs.Unavailable(Owner, employee, role),
                _plan.AssignedHandlerIds.Contains(employee.GUID.ToString()) || _plan.AssignedChemistIds.Contains(employee.GUID.ToString())))
            .ToList();

    private List<Choice> Supplies() => _plan.IngredientShelfIds.Select(id => new Choice(id, EquipmentName(id))).ToList();

    private string MixerLabel(string id)
    {
        if (string.IsNullOrEmpty(id)) return "No mixer assigned";
        var index = _plan.AssignedMixerIds.IndexOf(id);
        return index < 0 ? "Mixer unavailable" : EquipmentName(id, "Mixer unavailable");
    }

    private List<Choice> Ingredients()
    {
        if (_ingredientChoices != null) return _ingredientChoices;
        var filter = new ItemFilter_MixingIngredient();
        _ingredientChoices = Items().Where(item =>
        {
            try { return filter.DoesItemMatchFilter(item.GetDefaultInstance(1)); }
            catch { return false; }
        }).Select(item => new Choice(item.ID, item.Name, item.ID, item.Icon)).OrderBy(c => c.Name).ToList();
        return _ingredientChoices;
    }

    private static IEnumerable<ItemDefinition> Items()
    {
        var items = Singleton<Registry>.Instance.GetAllItems();
        for (var i = 0; i < items.Count; i++) if (items[i] != null) yield return items[i];
    }

    private IEnumerable<BuildableItem> Equipment()
    {
        var items = _station.ParentProperty?.BuildableItems;
        if (items == null) yield break;
        for (var i = 0; i < items.Count; i++)
            if (items[i] != null && !items[i].IsDestroyed) yield return items[i];
    }

    private List<Choice> Mixers()
    {
        var choices = new List<Choice>();
        for (var i = 0; i < _plan.AssignedMixerIds.Count; i++)
        {
            var id = _plan.AssignedMixerIds[i];
            var item = Owner.FindItem(id);
            if (item != null) choices.Add(new Choice(id, EquipmentName(id)));
        }
        return choices;
    }

    private string EquipmentName(string id, string empty = "Choose…")
    {
        if (string.IsNullOrEmpty(id)) return empty;
        var item = Owner.FindItem(id);
        if (item == null) return "Equipment unavailable";
        var assignedName = item.TryCast<Il2CppScheduleOne.Management.IConfigurable>()?.Configuration?.Name?.Value;
        return string.IsNullOrWhiteSpace(assignedName) ? item.GetManagementName() : assignedName;
    }

    private string EmployeeName(string id, string empty)
        => string.IsNullOrEmpty(id) ? empty : NativeJobs.Employee(id)?.FullName ?? "Employee unavailable";

    private static string ItemName(string id, string empty = "Choose ingredient…")
        => string.IsNullOrEmpty(id) ? empty : Registry.GetItem(id)?.Name ?? "Item unavailable";

    private void Pick(string title, List<Choice> choices, Action<string> selected, bool allowClear = false, string clearLabel = "Auto · Follow assigned mixer order")
    {
        DismissPicker();
        _picker = Rect("Selection dialog", _root);
        Place(_picker, 0, .085f, 1, .915f);
        Panel("Dialog backdrop", _picker, 0, 0, 1, 1, _background.color);
        Label(_picker, title, .025f, .015f, .78f, .055f, .024f, style: FontStyles.Bold);
        Button(_picker, "Back", .85f, .015f, .125f, .055f, ClosePicker);
        var resultLabel = Label(_picker, "", .025f, .165f, .95f, .035f, .016f);
        var list = Scroll(_picker, "Choices", .025f, .21f, .95f, .775f);
        void Choose(string id) { ClosePicker(); selected(id); }
        void Refresh(string search)
        {
            Clear(list);
            var filtered = choices.Where(c => string.IsNullOrWhiteSpace(search) || (c.Name + " " + c.Detail + " " + c.Unavailable).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            var available = filtered.Count(c => c.Unavailable == "");
            resultLabel.text = filtered.Count == 0 ? (choices.Count == 0 ? "No employees or equipment of this type at this property." : "No matches. Try another search.") : $"{filtered.Count} shown · {available} available";
            if (allowClear)
            {
                var empty = Row(list, "Automatic assignment", .055f);
                Button(empty, clearLabel, .005f, .04f, .99f, .92f, () => Choose(""));
            }
            var hasReasons = filtered.Any(c => c.Unavailable != "" || c.Detail is "Handler" or "Chemist");
            for (var i = 0; i < filtered.Count; i += 2)
            {
                var row = Row(list, "Choices " + i, hasReasons ? .088f : .068f);
                for (var column = 0; column < 2 && i + column < filtered.Count; column++)
                {
                    var choice = filtered[i + column];
                    var button = Button(row, "", column * .515f, .04f, .485f, .92f, () => Choose(choice.Id));
                    button.interactable = choice.Unavailable == "";
                    var stateColor = choice.Selected ? new Color(.36f, .75f, 1f)
                        : choice.Unavailable == "" ? LinkGood
                        : choice.Unavailable.Contains("locker", StringComparison.OrdinalIgnoreCase) ? ChemistColor : new Color(.92f, .49f, .42f);
                    if (hasReasons) Tint(button, choice.Selected ? new Color(.14f, .36f, .53f) : new Color(.20f, .27f, .33f));
                    var iconWidth = choice.Icon == null ? 0 : .17f;
                    if (choice.Icon != null) Icon(button.transform, choice.Icon, .025f, .12f, .14f, .76f);
                    Label(button.transform, choice.Name, .035f + iconWidth, hasReasons ? .04f : .08f, .93f - iconWidth, hasReasons ? .43f : .84f, .019f,
                        color: choice.Selected ? stateColor : null, style: FontStyles.Bold);
                    if (hasReasons)
                        Label(button.transform, choice.Selected ? "Assigned to this terminal" : choice.Unavailable == "" ? "Available" : choice.Unavailable, .035f + iconWidth, .48f, .93f - iconWidth, .49f, .015f, stateColor);
                }
            }
            ResetAllScrolls();
        }
        var searchField = Input(_picker, "", "Search by name…", .025f, .085f, .95f, .065f, _ => { });
        searchField.onValueChanged.AddListener((UnityAction<string>)Refresh);
        Refresh("");
    }

    private void Icon(Transform parent, Sprite? sprite, float x, float y, float w, float h)
    {
        if (sprite == null) return;
        var icon = Rect("Icon", parent); Place(icon, x, y, w, h);
        var image = icon.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
    }

    private static void Tint(Button button, Color color)
    {
        // Avoid the native disabled brown tint obscuring selected/blocked states.
        button.image.color = color;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(.8f, .8f, .8f); colors.disabledColor = Color.white;
        button.colors = colors;
    }

    private void ResetAllScrolls()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var scroll in _root.GetComponentsInChildren<ScrollRect>())
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }
    }

    private RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(x, 1 - y - height);
        rect.anchorMax = new Vector2(x + width, 1 - y);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        var p = rect.localPosition; p.z = 0; rect.localPosition = p;
    }

    private Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(name, parent); Place(rect, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = _background.sprite; image.type = _background.type;
        image.color = color;
        return image;
    }

    private TextMeshProUGUI Label(Transform parent, string text, float x, float y, float w, float h, float size,
        Color? color = null, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rect = Rect("Label", parent); Place(rect, x, y, w, h);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font.font; label.fontSharedMaterial = _font.fontSharedMaterial;
        label.fontSize = _unit * size; label.fontStyle = style;
        label.enableAutoSizing = true; label.fontSizeMin = _unit * size * .65f; label.fontSizeMax = _unit * size;
        label.color = color ?? _font.color; label.alignment = alignment;
        label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false; label.text = text;
        return label;
    }

    private Button Button(Transform parent, string text, float x, float y, float w, float h, Action action)
    {
        var button = Object.Instantiate(_native.launderButton, parent, false);
        button.name = text;
        Place(button.GetComponent<RectTransform>(), x, y, w, h);
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener((UnityAction)(() =>
        {
            try { action(); }
            catch (Exception e) { _status.text = "Could not apply this change. See MelonLoader log."; _log.Error(e); }
        }));
        button.interactable = true;
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        Place(label.GetComponent<RectTransform>(), .025f, .06f, .95f, .88f);
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.text = text; label.fontSize = _unit * .018f;
        label.enableAutoSizing = true; label.fontSizeMin = _unit * .011f; label.fontSizeMax = _unit * .018f;
        label.overflowMode = TextOverflowModes.Ellipsis; label.enableWordWrapping = false;
        button.gameObject.SetActive(true);
        return button;
    }

    private TMP_InputField Input(Transform parent, string value, string placeholder, float x, float y, float w, float h, Action<string> changed, bool numeric = false)
    {
        var field = Object.Instantiate(_native.amountInputField, parent, false);
        field.name = placeholder;
        Place(field.GetComponent<RectTransform>(), x, y, w, h);
        field.onValueChanged = new TMP_InputField.OnChangeEvent();
        field.onEndEdit = new TMP_InputField.SubmitEvent();
        field.onSubmit = new TMP_InputField.SubmitEvent();
        field.contentType = numeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
        field.interactable = true;
        field.characterLimit = numeric ? 2 : 80;
        foreach (var text in field.GetComponentsInChildren<TMP_Text>(true))
            if (text != field.textComponent && (field.placeholder == null || text != field.placeholder.TryCast<TMP_Text>()))
                text.gameObject.SetActive(false);
        Place(field.textViewport, .025f, .02f, .95f, .96f);
        Place(field.textComponent.GetComponent<RectTransform>(), 0, 0, 1, 1);
        field.textComponent.margin = Vector4.zero;
        field.textComponent.fontSize = _unit * .020f;
        field.textComponent.alignment = numeric ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        if (field.placeholder != null)
        {
            var label = field.placeholder.TryCast<TMP_Text>();
            if (label != null)
            {
                Place(label.GetComponent<RectTransform>(), 0, 0, 1, 1);
                label.text = placeholder; label.fontSize = _unit * .019f;
                label.margin = Vector4.zero;
            }
        }
        field.SetTextWithoutNotify(value);
        field.onEndEdit.AddListener((UnityAction<string>)(text =>
        {
            try { changed(text); } catch (Exception e) { _log.Error(e); }
        }));
        field.gameObject.SetActive(true);
        return field;
    }

    private RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h)
    {
        var scroll = Object.Instantiate(_native.scrollRect, parent, false);
        scroll.name = name;
        Place(scroll.GetComponent<RectTransform>(), x, y, w, h);
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(false);
        Place(scroll.viewport, 0, 0, .965f, 1);
        var content = scroll.content;
        Clear(content);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(2, 2, 2, 2); layout.spacing = _unit * .003f;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        scroll.gameObject.SetActive(true);
        return content;
    }

    private RectTransform Row(RectTransform parent, string name, float height)
    {
        var rect = Rect(name, parent);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = _unit * height; layout.minHeight = _unit * height;
        return rect;
    }

    private static void Clear(RectTransform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }
}
