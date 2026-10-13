using System.IO;
using System.Text.Json;
using TaskTable.Infra.System;

namespace TaskTable.App.Dashboard;

/// <summary>停靠布局节点：叶子存模块 id；分组存排列方向与子节点（含权重比例）。</summary>
public sealed class DockNode
{
    public static readonly string[] KnownModules = { "pomodoro", "calendar", "tasks", "expenses", "periods" };

    public string? Module { get; set; }
    public string? Orientation { get; set; }   // "H" 横排 / "V" 竖排
    public List<DockNode>? Children { get; set; }
    public List<double>? Weights { get; set; }

    public static DockNode Leaf(string module) => new() { Module = module };

    public static DockNode Group(string orientation, params (DockNode Node, double Weight)[] items) => new()
    {
        Orientation = orientation,
        Children = items.Select(i => i.Node).ToList(),
        Weights = items.Select(i => i.Weight).ToList()
    };

    /// <summary>默认布局：左列（番茄钟 / 消费记账）· 中列日历 · 右列（当日任务 / 峰谷时段）。</summary>
    public static DockNode Default() => Group("H",
        (Group("V", (Leaf("pomodoro"), 1), (Leaf("expenses"), 1)), 0.95),
        (Leaf("calendar"), 2.0),
        (Group("V", (Leaf("tasks"), 1.0), (Leaf("periods"), 1.0)), 1.05));
}

/// <summary>布局树操作：移除（自动折叠空分组）、邻接插入、交换。</summary>
public static class DockTree
{
    /// <summary>移除模块并返回新树（分组只余 1 个子节点时自动折叠；整树为空返回 null）。</summary>
    public static DockNode? Remove(DockNode? root, string module)
    {
        if (root is null) return null;
        if (root.Module == module) return null;
        if (root.Children is null) return root;

        var children = new List<DockNode>();
        var weights = new List<double>();
        for (int i = 0; i < root.Children.Count; i++)
        {
            var kept = Remove(root.Children[i], module);
            if (kept is null) continue;
            children.Add(kept);
            weights.Add(root.Weights is not null && i < root.Weights.Count ? root.Weights[i] : 1);
        }
        if (children.Count == 0) return null;
        if (children.Count == 1) return children[0];
        root.Children = children;
        root.Weights = weights;
        return root;
    }

    /// <summary>把 module 插到 target 的指定侧（orientation: H/V；before: 是否在 target 之前）。</summary>
    public static DockNode InsertAround(DockNode root, string target, string module, string orientation, bool before)
    {
        if (root.Module == target)
        {
            var dragged = DockNode.Leaf(module);
            var anchor = DockNode.Leaf(target);
            return before
                ? DockNode.Group(orientation, (dragged, 1), (anchor, 1))
                : DockNode.Group(orientation, (anchor, 1), (dragged, 1));
        }
        if (root.Children is not null)
            for (int i = 0; i < root.Children.Count; i++)
                root.Children[i] = InsertAround(root.Children[i], target, module, orientation, before);
        return root;
    }

    /// <summary>交换两个模块所在的叶子。</summary>
    public static void Swap(DockNode root, string a, string b)
    {
        if (root.Module == a) root.Module = b;
        else if (root.Module == b) root.Module = a;
        else root.Children?.ForEach(c => Swap(c, a, b));
    }

    /// <summary>按顺序收集树上的模块 id。</summary>
    public static IEnumerable<string> Modules(DockNode? node)
    {
        if (node is null) yield break;
        if (node.Module is not null)
        {
            yield return node.Module;
            yield break;
        }
        if (node.Children is null) yield break;
        foreach (var child in node.Children)
            foreach (var id in Modules(child))
                yield return id;
    }
}

/// <summary>浮窗状态：模块 id 与窗口位置 / 大小 / 置顶。</summary>
public sealed class FloatState
{
    public string? Module { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Topmost { get; set; }
}

/// <summary>布局持久化：%AppData%\TaskTable\layout.json（停靠树 + 模块最大化 + 浮窗 + 已关闭模块）。</summary>
public static class DockLayoutStore
{
    private static string FilePath => Path.Combine(AppPaths.Root, "layout.json");

    private sealed class Payload
    {
        public DockNode? Layout { get; set; }
        public string? Maximized { get; set; }
        public List<FloatState>? Floating { get; set; }
        public List<string>? Closed { get; set; }
    }

    public static (DockNode? Layout, string? Maximized, List<FloatState> Floating, List<string> Closed) Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var payload = JsonSerializer.Deserialize<Payload>(File.ReadAllText(FilePath));
                var floating = payload?.Floating?
                    .Where(f => f.Module is not null && DockNode.KnownModules.Contains(f.Module))
                    .ToList() ?? new List<FloatState>();
                foreach (var f in floating)
                {
                    f.Left = Safe(f.Left);
                    f.Top = Safe(f.Top);
                    f.Width = Safe(f.Width);
                    f.Height = Safe(f.Height);
                }
                var closed = payload?.Closed?
                    .Where(m => DockNode.KnownModules.Contains(m))
                    .Distinct()
                    .ToList() ?? new List<string>();
                if (IsValid(payload?.Layout, floating, closed))
                {
                    if (payload!.Layout is not null) Normalize(payload.Layout);
                    var docked = CollectModules(payload.Layout);
                    var maximized = payload.Maximized is not null && docked.Contains(payload.Maximized)
                        ? payload.Maximized : null;
                    return (payload.Layout, maximized, floating, closed);
                }
                AppLog.Warn("布局文件无效，改用默认布局");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"布局读取失败，改用默认布局: {ex.Message}");
        }
        return (DockNode.Default(), null, new List<FloatState>(), new List<string>());
    }

    public static void Save(DockNode? layout, string? maximized, IReadOnlyList<FloatState> floating, IReadOnlyList<string> closed)
    {
        try
        {
            AppPaths.EnsureAll();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new Payload
                {
                    Layout = layout,
                    Maximized = maximized,
                    Floating = floating.ToList(),
                    Closed = closed.ToList()
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLog.Error("布局保存失败", ex);
        }
    }

    private static double Safe(double v) => double.IsFinite(v) ? v : 0;

    /// <summary>补全缺省字段（方向、权重数量），保证构建时安全。</summary>
    private static void Normalize(DockNode node)
    {
        if (node.Module is not null)
        {
            node.Children = null;
            node.Weights = null;
            node.Orientation = null;
            return;
        }
        node.Orientation = node.Orientation == "V" ? "V" : "H";
        int n = node.Children!.Count;
        if (node.Weights is null || node.Weights.Count != n)
            node.Weights = Enumerable.Repeat(1.0, n).ToList();
        foreach (var child in node.Children) Normalize(child);
    }

    /// <summary>收集树上的模块 id（结构不合法时返回 false）。</summary>
    private static bool Collect(DockNode? node, List<string> found)
    {
        if (node is null) return true;
        if (node.Module is not null) { found.Add(node.Module); return true; }
        return node.Children is { Count: >= 2 } && node.Children.All(c => Collect(c, found));
    }

    private static List<string> CollectModules(DockNode? node)
    {
        var found = new List<string>();
        Collect(node, found);
        return found;
    }

    /// <summary>校验：停靠树 + 浮窗 + 已关闭合计无重复、全部为已知模块；
    /// 允许缺失（缺失的模块视为新版本新增，由 DockHost 自动补上）。</summary>
    private static bool IsValid(DockNode? node, List<FloatState> floating, List<string> closed)
    {
        var tree = new List<string>();
        if (!Collect(node, tree)) return false;
        var all = tree.Concat(floating.Select(f => f.Module!)).Concat(closed).ToList();
        return all.All(m => DockNode.KnownModules.Contains(m))
            && all.Distinct().Count() == all.Count;
    }
}