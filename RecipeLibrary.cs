using System.Text.Json;

namespace SpecialMixingTerminal;

// Portable recipe data contains no employee IDs, equipment GUIDs or job leases.
public sealed class PresetStep
{
    public string IngredientId { get; set; } = "";
    public int MixerSlot { get; set; }
    public int SupplySlot { get; set; }
}

public sealed class PresetSegment
{
    public string BaseProductId { get; set; } = "";
    public List<PresetStep> Steps { get; set; } = [];
}

public sealed class RecipePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string FinalProductName { get; set; } = "";
    public string PreviewProduct { get; set; } = "";
    public List<string> PreviewEffects { get; set; } = [];
    public bool AutoAcceptMixes { get; set; }
    public int FirstStageNumber { get; set; } = 1;
    public int FinalStageNumber { get; set; }
    public List<PresetSegment> Segments { get; set; } = [];

    public static RecipePreset Capture(IReadOnlyList<RecipePlan> plans, string name)
    {
        if (plans.Count == 0 || string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Name the recipe and configure its chain first.");
        if (plans.Any(p => p.ValidateMixerLayout() != "" || p.Steps.Any(s => string.IsNullOrEmpty(s.IngredientId)))) throw new InvalidOperationException("Complete the recipe's mixer and ingredient choices first.");
        if (plans.Any(p => Enumerable.Range(0, p.Steps.Count).Any(i => string.IsNullOrEmpty(p.ResolveSupplyId(i))))) throw new InvalidOperationException("Choose each ingredient's supply shelf before saving a recipe.");
        var root = plans[0];
        return new RecipePreset {
            Name = name.Trim(), FinalProductName = root.FinalProductName,
            AutoAcceptMixes = root.AutoAcceptMixes, FirstStageNumber = root.FirstStageNumber, FinalStageNumber = root.FinalStageNumber,
            Segments = plans.Select(plan => new PresetSegment {
                BaseProductId = plan.BaseProductId,
                Steps = plan.Steps.Select((step, i) => new PresetStep {
                    IngredientId = step.IngredientId, MixerSlot = plan.AssignedMixerIds.IndexOf(plan.ResolveMixerId(i)),
                    SupplySlot = plan.IngredientShelfIds.IndexOf(plan.ResolveSupplyId(i))
                }).ToList()
            }).ToList()
        };
    }

    public string Restore(IReadOnlyList<RecipePlan> targets)
    {
        if (Segments.Count == 0) return "This saved recipe has no terminal segments.";
        if (targets.Count != Segments.Count) return $"Connect {Segments.Count} terminals for this recipe; this setup has {targets.Count}.";
        if (targets.Any(p => p.NativeJobsOwned)) return "Stop every terminal in this chain before loading a recipe.";
        // Prepare every segment before mutating any plan.
        var prepared = new List<List<IngredientStep>>();
        for (var s = 0; s < Segments.Count; s++)
        {
            var source = Segments[s]; var target = targets[s];
            if (target.AssignedMixerIds.Any(string.IsNullOrWhiteSpace) || target.AssignedMixerIds.Distinct().Count() != target.AssignedMixerIds.Count) return $"Assign distinct mixers to terminal {s + 1}.";
            if (source.Steps.Count < 1 || source.Steps.Count > RecipePlan.EquipmentLimit || target.AssignedMixerIds.Count < source.Steps.Count) return $"Terminal {s + 1} needs {source.Steps.Count} assigned mixers.";
            if (target.IngredientShelfIds.Count == 0) return $"Assign ingredient supplies to terminal {s + 1}.";
            var steps = new List<IngredientStep>(); var used = new HashSet<int>();
            foreach (var step in source.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.IngredientId)) return "A saved ingredient is missing.";
                var mixer = step.MixerSlot;
                if (mixer < 0 || mixer >= target.AssignedMixerIds.Count || used.Contains(mixer)) mixer = Enumerable.Range(0, target.AssignedMixerIds.Count).First(i => !used.Contains(i));
                used.Add(mixer);
                var shelf = step.SupplySlot;
                if (target.IngredientShelfIds.Count == 1) shelf = 0;
                if (shelf < 0 || shelf >= target.IngredientShelfIds.Count) return $"Choose the supply shelves for terminal {s + 1} before loading this recipe.";
                steps.Add(new IngredientStep { IngredientId = step.IngredientId, MixerId = target.AssignedMixerIds[mixer], SupplyShelfId = target.IngredientShelfIds[shelf], Threshold = 20 });
            }
            prepared.Add(steps);
        }
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            target.Name = Name; target.Steps = prepared[i]; target.BaseProductId = Segments[i].BaseProductId;
            target.AutoAcceptMixes = AutoAcceptMixes; target.FinalProductName = FinalProductName;
            target.FirstStageNumber = FirstStageNumber; target.FinalStageNumber = FinalStageNumber;
        }
        return "";
    }
}

public sealed class RecipeLibrary
{
    public int Version { get; set; } = 1;
    public List<RecipePreset> Recipes { get; set; } = [];
    public static RecipeLibrary Load(string path)
    {
        if (!File.Exists(path)) return new();
        var library = JsonSerializer.Deserialize<RecipeLibrary>(File.ReadAllText(path)) ?? throw new InvalidDataException("Recipe library is empty or invalid.");
        if (library.Version != 1 || library.Recipes == null) throw new InvalidDataException("Unsupported recipe library format; existing file was preserved.");
        if (library.Recipes.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Name) || r.Segments == null || r.Segments.Count == 0 || r.PreviewEffects == null || r.Segments.Any(s => s == null || s.Steps == null || s.Steps.Count < 1 || s.Steps.Count > RecipePlan.EquipmentLimit || s.Steps.Any(p => p == null || string.IsNullOrWhiteSpace(p.IngredientId)))) || library.Recipes.Select(r => r.Id).Distinct().Count() != library.Recipes.Count)
            throw new InvalidDataException("Recipe library contains invalid entries; existing file was preserved.");
        return library;
    }
    public void Add(RecipePreset recipe)
    {
        if (Recipes.Any(r => r.Id == recipe.Id)) throw new InvalidOperationException("Saving edits creates a new recipe; an existing recipe cannot be overwritten.");
        // Store a deep copy so subsequent workspace edits cannot mutate the library.
        Recipes.Add(JsonSerializer.Deserialize<RecipePreset>(JsonSerializer.Serialize(recipe))!);
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}

public static class MixChain
{
    public static (List<RecipePlan> Plans, string Error) Resolve(IEnumerable<RecipePlan> available, RecipePlan selected)
    {
        var plans = available.Where(p => p.PropertyCode == selected.PropertyCode).Distinct().ToList();
        if (!plans.Contains(selected)) plans.Add(selected);
        var seen = new HashSet<RecipePlan>(); var root = selected;
        while (true)
        {
            if (!seen.Add(root)) return ([], "The terminal chain contains a cycle.");
            var previous = plans.Where(p => p != root && root.InputShelfId != "" && p.OutputShelfId == root.InputShelfId).ToList();
            if (previous.Count > 1) return ([], "Several terminals feed this handoff shelf; use a unique chain for automatic naming.");
            if (previous.Count == 0) break;
            root = previous[0];
        }
        seen.Clear(); var chain = new List<RecipePlan>();
        while (true)
        {
            if (!seen.Add(root)) return ([], "The terminal chain contains a cycle.");
            if (root.InputShelfId != "" && plans.Count(p => p != root && p.OutputShelfId == root.InputShelfId) > 1)
                return ([], "Several terminals feed this handoff shelf; use a unique chain for automatic naming.");
            chain.Add(root);
            var next = plans.Where(p => p != root && root.OutputShelfId != "" && p.InputShelfId == root.OutputShelfId).ToList();
            if (next.Count > 1) return ([], "The output feeds several terminals; automatic naming needs one downstream chain.");
            if (next.Count == 0) break;
            root = next[0];
        }
        return (chain, "");
    }
    public static string ProductName(string finalName, int stage, int finalStage)
        => stage == finalStage ? finalName.Trim() : $"{stage} {finalName.Trim()}";
}
