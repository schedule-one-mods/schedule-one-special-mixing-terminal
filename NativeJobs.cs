using Il2CppScheduleOne;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using MelonLoader;
using UnityEngine;
using NativeItems = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.EntityFramework.BuildableItem>;
using NativeRoutes = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Management.AdvancedTransitRoute>;
using Object = UnityEngine.Object;

namespace SpecialMixingTerminal;

// The terminal writes the same route/station fields as the employee clipboard.
// Native employee AI performs every pickup, insertion, mix and output transfer.
internal static class NativeJobs
{
    internal static bool IsRunning(TerminalConfigurable owner) => owner.Plan.NativeJobsOwned;
    internal static string Status(TerminalConfigurable owner) => owner.Plan.NativeJobsOwned ? "Running · Native handler routes and chemist stations assigned" : "Stopped";
    private static readonly List<Employee> _employees = new();
    private static readonly Dictionary<string, Employee> _employeeById = new();
    private static float _employeesAt;
    private static int _employeeCount = -1;
    internal static IReadOnlyList<Employee> Employees()
    {
        if (!NetworkSingleton<EmployeeManager>.InstanceExists) return Array.Empty<Employee>();
        var source = NetworkSingleton<EmployeeManager>.Instance.AllEmployees;
        if (source == null) return Array.Empty<Employee>();
        if (Time.realtimeSinceStartup >= _employeesAt || source.Count != _employeeCount)
        {
            _employeesAt = Time.realtimeSinceStartup + 1f;
            _employeeCount = source.Count;
            _employees.Clear(); _employeeById.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                var employee = source[i];
                if (employee == null) continue;
                _employees.Add(employee); _employeeById[employee.GUID.ToString()] = employee;
            }
        }
        return _employees;
    }
    internal static Employee? Employee(string id)
    {
        Employees();
        return _employeeById.TryGetValue(id, out var employee) && employee != null ? employee : null;
    }
    private static EntityConfiguration? Configuration(Employee? employee) => employee?.TryCast<IConfigurable>()?.Configuration;

    internal static string Ready(TerminalConfigurable owner, EEmployeeType role)
    {
        var error = MixerWiring.Validate(owner);
        if (error != "") return error;
        var ids = role == EEmployeeType.Handler ? owner.Plan.AssignedHandlerIds : owner.Plan.AssignedChemistIds;
        var employee = ids.Count == 1 ? Employee(ids[0]) : null;
        if (employee == null) return "Select a " + role.ToString().ToLowerInvariant();
        error = Unavailable(owner, employee, role);
        if (error != "") return error;
        var config = Configuration(employee)!;
        if (role == EEmployeeType.Handler)
            return RequiredRoutes(owner.Plan).Count <= config.Cast<PackagerConfiguration>().Routes.MaxRoutes ? "" : "Not enough handler routes";
        var chemist = config.Cast<ChemistConfiguration>();
        if (chemist.Stations.MaxItems < owner.Plan.AssignedMixerIds.Count) return "Not enough chemist station slots";
        foreach (var id in owner.Plan.AssignedMixerIds)
        {
            var assigned = owner.FindItem(id)!.Cast<MixingStation>().stationConfiguration.AssignedChemist.SelectedNPC;
            if (assigned != null && assigned.GUID != employee.GUID) return "A mixer belongs to another chemist";
        }
        return "";
    }

    internal static (int Used, int Limit) HandlerRoutes(Employee employee)
    {
        var config = Configuration(employee)?.TryCast<PackagerConfiguration>();
        return config == null ? (0, 0) : (config.Routes.Routes.Count, config.Routes.MaxRoutes);
    }

    internal static string Unavailable(TerminalConfigurable owner, Employee employee, EEmployeeType type)
    {
        if (employee.AssignedProperty != owner.ParentProperty || employee.EmployeeType != type) return "Different property or role";
        foreach (var other in Mod.Terminals)
            if (other != owner && (other.Plan.AssignedHandlerIds.Contains(employee.GUID.ToString()) || other.Plan.AssignedChemistIds.Contains(employee.GUID.ToString())))
                return "Reserved by another mixing terminal";
        var config = Configuration(employee);
        var handler = config?.TryCast<PackagerConfiguration>();
        var chemist = config?.TryCast<ChemistConfiguration>();
        if (handler != null)
        {
            if (handler.Stations.SelectedObjects.Count != 0) return "Clear this handler's assigned stations first";
            if (handler.Routes.Routes.Count != 0 && !OwnsRoutes(owner, employee.GUID.ToString(), handler)) return "Clear this handler's routes first";
            if (handler.Home.SelectedObject == null) return "Assign a payment locker first";
            return "";
        }
        if (chemist != null)
        {
            if (chemist.Stations.SelectedObjects.Count != 0 && !OwnsStations(owner, employee.GUID.ToString(), chemist)) return "Clear this chemist's stations first";
            if (chemist.Home.SelectedObject == null) return "Assign a payment locker first";
            return "";
        }
        return "Unsupported employee configuration";
    }

    private static string Pair(string source, string destination) => source + "/" + destination;

    private static bool OwnsRoutes(TerminalConfigurable owner, string employee, PackagerConfiguration config)
    {
        if (!owner.Plan.NativeJobsOwned) return false;
        var expected = owner.Plan.NativeRoutes.Where(r => r.HandlerId == employee).Select(r => Pair(r.SourceId, r.DestinationId)).OrderBy(x => x).ToArray();
        var actual = new List<string>();
        for (var i = 0; i < config.Routes.Routes.Count; i++)
        {
            var route = config.Routes.Routes[i];
            actual.Add(Pair(route.Source?.TryCast<BuildableItem>()?.GUID.ToString() ?? "", route.Destination?.TryCast<BuildableItem>()?.GUID.ToString() ?? ""));
        }
        return expected.SequenceEqual(actual.OrderBy(x => x));
    }

    private static bool OwnsStations(TerminalConfigurable owner, string employee, ChemistConfiguration config)
    {
        if (!owner.Plan.NativeJobsOwned || !owner.Plan.NativeChemistStations.TryGetValue(employee, out var expected)) return false;
        var actual = new List<string>();
        for (var i = 0; i < config.Stations.SelectedObjects.Count; i++) actual.Add(config.Stations.SelectedObjects[i].GUID.ToString());
        return expected.OrderBy(x => x).SequenceEqual(actual.OrderBy(x => x));
    }

    internal static List<(string Source, string Destination)> RequiredRoutes(RecipePlan plan)
    {
        var routes = new List<(string, string)>();
        if (plan.Steps.Count == 0) return routes;
        routes.Add((plan.InputShelfId, plan.ResolveMixerId(0)));
        for (var i = 0; i < plan.Steps.Count; i++) routes.Add((plan.ResolveSupplyId(i), plan.ResolveMixerId(i)));
        return routes.Distinct().ToList();
    }

    internal static string Start(TerminalConfigurable owner)
    {
        if (owner.Plan.NativeJobsOwned) return "Already running.";
        var plan = owner.Plan;
        var wiringError = MixerWiring.Validate(owner);
        if (wiringError != "") return wiringError;
        if (plan.AssignedHandlerIds.Count != 1 || plan.AssignedChemistIds.Count != 1) return "Assign one handler and one chemist.";
        var handlers = new List<(Employee Employee, PackagerConfiguration Config)>();
        var chemists = new List<(Employee Employee, ChemistConfiguration Config)>();
        foreach (var (ids, type) in new[] { (plan.AssignedHandlerIds, EEmployeeType.Handler), (plan.AssignedChemistIds, EEmployeeType.Chemist) })
        foreach (var id in ids.Distinct())
        {
            var employee = Employee(id);
            if (employee == null) return "An assigned employee is unavailable.";
            var reason = Unavailable(owner, employee, type);
            if (reason != "") return employee.FullName + ": " + reason;
            if (type == EEmployeeType.Handler) handlers.Add((employee, Configuration(employee)!.Cast<PackagerConfiguration>()));
            else chemists.Add((employee, Configuration(employee)!.Cast<ChemistConfiguration>()));
        }
        var pairs = RequiredRoutes(plan);
        var mixerIds = plan.AssignedMixerIds.ToList();
        if (handlers.Sum(h => h.Config.Routes.MaxRoutes) < pairs.Count) return "This recipe exceeds the handler's native route limit.";
        if (chemists.Sum(c => c.Config.Stations.MaxItems) < mixerIds.Count) return "The chemist does not have four available station slots.";
        foreach (var id in mixerIds)
        {
            var station = owner.FindItem(id)!.Cast<MixingStation>();
            var assigned = station.stationConfiguration.AssignedChemist.SelectedNPC;
            if (assigned != null && !plan.NativeChemistStations.Values.Any(ids => ids.Contains(id))) return "A mixer already belongs to another chemist. Clear that assignment first.";
            if (!plan.NativeJobsOwned && (station.CurrentMixOperation != null || station.ProductSlot.Quantity > 0 || station.MixerSlot.Quantity > 0 || station.OutputSlot.Quantity > 0)) return "Empty the assigned mixers before starting a new terminal job.";
        }
        // Validate everything before changing native employee fields. Home/locker
        // fields are never reset or written by this mod.
        wiringError = MixerWiring.Apply(owner);
        if (wiringError != "") return wiringError;
        var bindings = new List<NativeRouteBinding>();
        var pairIndex = 0;
        foreach (var handler in handlers)
        {
            var routes = new NativeRoutes();
            for (var slot = 0; slot < handler.Config.Routes.MaxRoutes && pairIndex < pairs.Count; slot++, pairIndex++)
            {
                var pair = pairs[pairIndex];
                var route = new AdvancedTransitRoute(owner.FindItem(pair.Source)!.Cast<ITransitEntity>(), owner.FindItem(pair.Destination)!.Cast<ITransitEntity>());
                route.Filter.SetMode(ManagementItemFilter.EMode.Blacklist);
                route.Filter.Items.Clear(); // Native destination slot filters select the ingredient.
                routes.Add(route);
                bindings.Add(new NativeRouteBinding { HandlerId = handler.Employee.GUID.ToString(), SourceId = pair.Source, DestinationId = pair.Destination });
            }
            handler.Config.Routes.SetList(routes, true);
        }
        var stationIndex = 0;
        var stationBindings = new Dictionary<string, List<string>>();
        foreach (var chemist in chemists)
        {
            var items = new NativeItems(); var ids = new List<string>();
            for (var slot = 0; slot < chemist.Config.Stations.MaxItems && stationIndex < mixerIds.Count; slot++, stationIndex++)
            { var id = mixerIds[stationIndex]; ids.Add(id); items.Add(owner.FindItem(id)!); }
            chemist.Config.Stations.SetList(items, true);
            stationBindings[chemist.Employee.GUID.ToString()] = ids;
        }
        plan.NativeRoutes = bindings; plan.NativeChemistStations = stationBindings; plan.NativeJobsOwned = true;
        owner.Save();
        MelonLogger.Msg($"[Mixing terminal {owner.Station.GUID}] START: {bindings.Count} native routes, {mixerIds.Count} stations. Jobs remain assigned until explicit Stop.");
        return "Started · Native employees keep their payment lockers.";
    }

    internal static string Release(TerminalConfigurable owner)
    {
        var plan = owner.Plan;
        if (!plan.NativeJobsOwned) return "";
        // A player may have edited the employee through their own clipboard.
        // Never clear a field that no longer exactly matches our leased jobs.
        foreach (var id in plan.NativeRoutes.Select(r => r.HandlerId).Distinct())
        {
            var employee = Employee(id); var config = employee == null ? null : Configuration(employee)?.TryCast<PackagerConfiguration>();
            if (config != null && config.Routes.Routes.Count > 0 && !OwnsRoutes(owner, id, config)) return "Handler routes changed outside the terminal. Clear those routes manually before releasing this job.";
        }
        foreach (var pair in plan.NativeChemistStations)
        {
            var employee = Employee(pair.Key); var config = employee == null ? null : Configuration(employee)?.TryCast<ChemistConfiguration>();
            if (config != null && config.Stations.SelectedObjects.Count > 0 && !OwnsStations(owner, pair.Key, config)) return "Chemist stations changed outside the terminal. Clear those assignments manually before releasing this job.";
        }
        foreach (var id in plan.NativeRoutes.Select(r => r.HandlerId).Distinct())
        {
            var employee = Employee(id); var config = employee == null ? null : Configuration(employee)?.TryCast<PackagerConfiguration>();
            config?.Routes.SetList(new NativeRoutes(), true);
        }
        foreach (var pair in plan.NativeChemistStations)
        {
            var employee = Employee(pair.Key); var config = employee == null ? null : Configuration(employee)?.TryCast<ChemistConfiguration>();
            config?.Stations.SetList(new NativeItems(), true);
        }
        plan.NativeJobsOwned = false; plan.NativeRoutes.Clear(); plan.NativeChemistStations.Clear(); owner.Save();
        MelonLogger.Msg($"[Mixing terminal {owner.Station.GUID}] STOP: explicit release cleared owned employee jobs.");
        return "";
    }

    // All production is performed by the native AI using persisted fields.
    internal static void Tick() { }
    internal static void BeforeSave() { }
    internal static void AfterSave() { }
    internal static void Clear()
    {
        _employees.Clear(); _employeeById.Clear(); _employeeCount = -1; _employeesAt = 0;
    }
}
