using Il2CppScheduleOne;
using Il2CppScheduleOne.Product;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SpecialMixingTerminal;

internal sealed partial class TerminalScreen
{
    private string _librarySearch = "";
    private string _reportedAutomationError = "";

    private void TryStart()
    {
        Save();
        if (_lastWiringError != "") return;
        var chain = MixAutomation.Chain(Owner);
        if (chain.Error != "") { _status.text = chain.Error; return; }
        if (chain.Plans[0].AutoAcceptMixes)
        {
            var preview = MixAutomation.Analyze(Owner);
            if (preview.Error != "") { _status.text = preview.Error; return; }
            if (preview.HasUnknown && string.IsNullOrWhiteSpace(preview.Root.FinalProductName)) { ShowNaming(true); return; }
        }
        var message = NativeJobs.Start(Owner); BuildPage(); _status.text = message;
    }

    private void ShowBaseProduct()
    {
        var chain = MixAutomation.Chain(Owner);
        if (chain.Error != "") { _status.text = chain.Error; return; }
        if (chain.Plans[0] != _plan) { _status.text = "Choose the base product at the first terminal in this chain."; return; }
        if (chain.Plans.Any(p => p.NativeJobsOwned)) { _status.text = "Stop the chain before changing its base product."; return; }
        var choices = new List<Choice>();
        var products = ProductManager.DiscoveredProducts;
        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];
            if (product != null) choices.Add(new Choice(product.ID, product.Name, "Known product", product.Icon));
        }
        Pick("Base product", choices.OrderBy(c => c.Name).ToList(), id => { _plan.BaseProductId = id; Changed(); BuildPage(); }, true, "Auto · Read the product supply shelf");
    }

    private RectTransform Dialog(string title)
    {
        DismissPicker();
        _picker = Rect(title, _root); Place(_picker, 0, .085f, 1, .915f);
        Panel("Dialog background", _picker, 0, 0, 1, 1, _background.color);
        Label(_picker, title, .025f, .025f, .78f, .07f, .025f, style: FontStyles.Bold);
        Button(_picker, "Back", .85f, .025f, .125f, .055f, ClosePicker);
        return _picker;
    }

    private void ShowNaming(bool startAfter)
    {
        var chain = MixAutomation.Chain(Owner);
        if (chain.Error != "") { _status.text = chain.Error; return; }
        if (chain.Plans.Any(p => p.NativeJobsOwned)) { _status.text = "Stop every terminal in this chain before changing product names."; return; }
        var root = chain.Plans[0];
        var dialog = Dialog(startAfter ? "Name the unknown mix chain" : "Mix acceptance & naming");
        Label(dialog, "Known products keep their names. New intermediate results use the step number; the final result uses the name alone.", .025f, .13f, .95f, .13f, .019f);
        var name = root.FinalProductName;
        var nameField = Input(dialog, name, "Final product name", .025f, .30f, .95f, .09f, value => name = value.Trim());
        var first = root.FirstStageNumber;
        var last = root.FinalStageNumber;
        Label(dialog, "First step", .025f, .43f, .25f, .055f, .018f);
        var firstField = Input(dialog, first.ToString(), "1", .28f, .42f, .18f, .075f, value => { if (int.TryParse(value, out var n)) first = n; }, true);
        Label(dialog, "Final step (0 = auto)", .52f, .43f, .29f, .055f, .018f);
        var lastField = Input(dialog, last.ToString(), "0", .82f, .42f, .155f, .075f, value => { if (int.TryParse(value, out var n)) last = n; }, true);
        Label(dialog, $"{chain.Plans.Count} connected terminals · {chain.Plans.Sum(p => p.Steps.Count)} ingredients. Numbering includes known stages. Change First step only for a partial chain with an earlier prefix outside this setup.", .025f, .54f, .95f, .12f, .017f);
        var automatic = root.AutoAcceptMixes;
        var toggle = Button(dialog, automatic ? "Auto-accept: On" : "Auto-accept: Off", .025f, .71f, .32f, .075f, () => { });
        toggle.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        toggle.onClick.AddListener((UnityAction)(() => { automatic = !automatic; toggle.GetComponentInChildren<TextMeshProUGUI>().text = automatic ? "Auto-accept: On" : "Auto-accept: Off"; }));
        Button(dialog, startAfter ? "Apply & Start" : "Apply", .72f, .84f, .255f, .075f, () =>
        {
            name = nameField.text.Trim();
            if (!int.TryParse(firstField.text, out first) || first < 1 || first > 9999 || !int.TryParse(lastField.text, out last) || last < 0 || last > 9999 || last != 0 && last < first + chain.Plans.Sum(p => p.Steps.Count) - 1)
            { _status.text = "Use a valid first/final step range covering this chain."; return; }
            if (automatic && startAfter && !ProductManager.IsMixNameValid(name)) { _status.text = "Enter a final product name."; return; }
            foreach (var plan in chain.Plans) { plan.FinalProductName = name; plan.FirstStageNumber = first; plan.FinalStageNumber = last; plan.AutoAcceptMixes = automatic; }
            SavePlans(chain.Plans); ClosePicker(); Changed(); BuildPage();
            if (startAfter) TryStart(); else _status.text = "Naming saved for this chain. Known product names are preserved.";
        });
    }

    private void SavePlans(IReadOnlyList<RecipePlan> plans)
    {
        foreach (var owner in Mod.Terminals) if (plans.Contains(owner.Plan)) owner.Save();
    }

    private void ShowSavePreset()
    {
        var analysis = MixAutomation.Analyze(Owner);
        if (analysis.Error != "") { _status.text = analysis.Error; return; }
        var root = analysis.Root;
        var title = root.Name == "Special mix" ? root.FinalProductName : root.Name;
        var finalName = root.FinalProductName;
        var dialog = Dialog("Save a new recipe");
        Label(dialog, "This saves a new copy of the whole connected chain. Existing saved recipes and employee/equipment assignments stay unchanged.", .025f, .14f, .95f, .12f, .019f);
        Label(dialog, "Recipe title", .025f, .31f, .28f, .06f, .019f);
        var titleField = Input(dialog, title, "Saved recipe title", .32f, .30f, .655f, .085f, value => title = value);
        Label(dialog, "Product name", .025f, .45f, .28f, .06f, .019f);
        var productField = Input(dialog, finalName, "Use recipe title if unknown", .32f, .44f, .655f, .085f, value => finalName = value);
        Label(dialog, $"Makes: {analysis.FinalProduct}\n{string.Join(", ", analysis.Effects)}", .025f, .59f, .95f, .15f, .019f);
        Button(dialog, "Save new copy", .69f, .84f, .285f, .075f, () =>
        {
            title = titleField.text.Trim(); finalName = productField.text.Trim();
            if (title == "") { _status.text = "Enter a title for this saved recipe."; return; }
            if (finalName == "" && analysis.HasUnknown) finalName = title;
            var preset = RecipePreset.Capture(analysis.Plans, title);
            preset.FinalProductName = finalName; preset.Segments[0].BaseProductId = analysis.BaseProductId;
            var finalStage = root.FirstStageNumber + analysis.Plans.Sum(p => p.Steps.Count) - 1;
            preset.PreviewProduct = analysis.Stages.Last().KnownProductId != "" ? analysis.FinalProduct : MixChain.ProductName(finalName, finalStage, root.FinalStageNumber == 0 ? finalStage : root.FinalStageNumber);
            preset.PreviewEffects = analysis.Effects.ToList();
            var library = RecipeLibrary.Load(MixAutomation.LibraryPath(Owner)); library.Add(preset); library.Save(MixAutomation.LibraryPath(Owner));
            ClosePicker(); _libraryPage = true; _teamPage = false; _selectedPreset = preset.Id; BuildPage(); _status.text = "Saved a new recipe. Earlier saved versions are unchanged.";
        });
    }

    private void BuildLibrary()
    {
        RecipeLibrary library;
        try { library = RecipeLibrary.Load(MixAutomation.LibraryPath(Owner)); }
        catch (Exception error) { Label(_body, "Could not read the recipe library. The existing file has been preserved.", 0, 0, 1, .2f, .021f); _log.Error(error); return; }
        var selected = library.Recipes.FirstOrDefault(r => r.Id == _selectedPreset);
        if (selected != null) { BuildPresetDetail(library, selected); return; }
        Label(_body, $"SAVED MIXES · {library.Recipes.Count}", 0, 0, .70f, .075f, .022f, style: FontStyles.Bold);
        var list = Scroll(_body, "Recipe library", 0, .24f, 1, .76f);
        void Refresh(string search)
        {
            _librarySearch = search; Clear(list);
            var recipes = library.Recipes.Where(r => (r.Name + " " + r.PreviewProduct).Contains(search, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Name).ToList();
            if (recipes.Count == 0) { var empty = Row(list, "No recipes", .10f); Label(empty, "No saved mixes. Use Save as new on the Recipe tab.", .02f, .05f, .96f, .90f, .019f); }
            for (var i = 0; i < recipes.Count; i += 2)
            {
                var row = Row(list, "Saved recipe row", .085f);
                for (var column = 0; column < 2 && i + column < recipes.Count; column++)
                {
                    var recipe = recipes[i + column];
                    var button = Button(row, "", column * .515f, .04f, .485f, .92f, () => { _selectedPreset = recipe.Id; BuildPage(); });
                    Label(button.transform, recipe.Name, .035f, .04f, .93f, .43f, .019f, style: FontStyles.Bold);
                    Label(button.transform, $"{recipe.Segments.Sum(s => s.Steps.Count)} ingredients · {recipe.Segments.Count} terminals · {recipe.PreviewProduct}", .035f, .50f, .93f, .43f, .015f);
                }
            }
        }
        var search = Input(_body, _librarySearch, "Search saved recipes…", 0, .115f, 1, .085f, _ => { });
        search.onValueChanged.AddListener((UnityAction<string>)Refresh); Refresh(_librarySearch);
    }

    private void BuildPresetDetail(RecipeLibrary library, RecipePreset preset)
    {
        Label(_body, preset.Name, 0, 0, .76f, .075f, .024f, style: FontStyles.Bold);
        Button(_body, "Back", .83f, 0, .17f, .075f, () => { _selectedPreset = ""; BuildPage(); });
        Label(_body, $"MAKES · {preset.PreviewProduct}\n{string.Join(", ", preset.PreviewEffects)}", 0, .115f, 1, .15f, .019f);
        Button(_body, "Load into setup", 0, .30f, .30f, .075f, () =>
        {
            var chain = MixAutomation.Chain(Owner);
            if (chain.Error != "") { _status.text = chain.Error; return; }
            var error = preset.Restore(chain.Plans);
            if (error != "") { _status.text = error; return; }
            SavePlans(chain.Plans);
            foreach (var owner in Mod.Terminals.Where(o => chain.Plans.Contains(o.Plan)))
            {
                var wiring = MixerWiring.Apply(owner);
                if (wiring != "") error = wiring;
            }
            _libraryPage = false; _teamPage = false; Changed(); BuildPage();
            _status.text = error == "" ? "Recipe loaded. Edit and Save as new to keep another version, or press Start." : "Recipe loaded as a draft · " + error;
        });
        Button(_body, "Delete saved copy", .68f, .30f, .32f, .075f, () =>
        {
            library.Recipes.RemoveAll(r => r.Id == preset.Id); library.Save(MixAutomation.LibraryPath(Owner));
            _selectedPreset = ""; BuildPage(); _status.text = "Deleted only the saved copy. The current setup and known products are unchanged.";
        });
        var content = Scroll(_body, "Saved ingredient detail", 0, .43f, 1, .57f);
        var number = preset.FirstStageNumber;
        for (var s = 0; s < preset.Segments.Count; s++)
        {
            var heading = Row(content, "Terminal segment", .042f);
            Label(heading, $"Terminal {s + 1} · {preset.Segments[s].Steps.Count} ingredients", .01f, 0, .98f, 1, .017f, style: FontStyles.Bold);
            var steps = preset.Segments[s].Steps;
            for (var i = 0; i < steps.Count; i += 2)
            {
                var row = Row(content, "Saved ingredient row", .055f);
                for (var column = 0; column < 2 && i + column < steps.Count; column++, number++)
                {
                    var step = steps[i + column]; var x = column * .515f;
                    Icon(row, Registry.GetItem(step.IngredientId)?.Icon, x, .08f, .07f, .84f);
                    Label(row, $"{number}. {ItemName(step.IngredientId)}", x + .08f, .05f, .40f, .90f, .019f);
                }
            }
        }
    }
}
