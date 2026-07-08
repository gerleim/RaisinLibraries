using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using Raisin.WPF.Base.Models;

namespace Raisin.WPF.Base;

public class UndoCloseService<T> where T : class, IUndoRecord
{
    private const int MaxUndoDepth = 20;
    private readonly Stack<UndoCloseGroup<T>> _undoStack = new();
    private UndoCloseGroup<T>? _pendingGroup;

    public DockingManager? DockingManager { get; set; }

    public void CaptureFloatingState(ToolWindowViewModel vm, T record)
    {
        if (DockingManager is null) return;

        var layoutDoc = DockingManager.Layout.Descendents()
            .OfType<LayoutDocument>()
            .FirstOrDefault(d => d.Content == vm);
        if (layoutDoc is null) return;

        var floatingWindow = layoutDoc.FindParent<LayoutFloatingWindow>();
        if (floatingWindow is null) return;

        record.IsFloating = true;

        var fwc = DockingManager.FloatingWindows
            .FirstOrDefault(fw => fw.Model == floatingWindow);
        if (fwc is not null)
        {
            record.FloatingLeft = fwc.Left;
            record.FloatingTop = fwc.Top;
            record.FloatingWidth = fwc.Width;
            record.FloatingHeight = fwc.Height;
        }
    }

    public void RecordClose(T record, ToolWindowViewModel vm)
    {
        if (_pendingGroup is null)
        {
            _pendingGroup = new UndoCloseGroup<T>();
            _pendingGroup.Records.Add(record);

            if (record.IsFloating)
                CaptureFloatingLayout(vm);

            Application.Current?.Dispatcher.BeginInvoke(SealGroup, DispatcherPriority.Background);
        }
        else
        {
            _pendingGroup.Records.Add(record);
        }
    }

    public void RestoreFloatingGroup(UndoCloseGroup<T> group)
    {
        if (DockingManager is not { } dm) return;

        var docLookup = new Dictionary<string, LayoutDocument>();
        foreach (var rec in group.Records)
        {
            if (!rec.IsFloating || rec.ContentId is null) continue;
            var layoutDoc = dm.Layout.Descendents()
                .OfType<LayoutDocument>()
                .FirstOrDefault(d => d.Content is ToolWindowViewModel vm
                    && vm.ContentId == rec.ContentId);
            if (layoutDoc is not null)
                docLookup[rec.ContentId] = layoutDoc;
        }
        if (docLookup.Count > 0)
            RestoreFloatingLayout(dm, group, docLookup);
    }

    public UndoCloseGroup<T>? PopUndo()
    {
        SealGroup();
        return _undoStack.Count > 0 ? _undoStack.Pop() : null;
    }

    public bool CanUndo => _undoStack.Count > 0 || _pendingGroup is not null;

    #region Capture

    private void CaptureFloatingLayout(ToolWindowViewModel vm)
    {
        if (DockingManager is null || _pendingGroup is null) return;

        var layoutDoc = DockingManager.Layout.Descendents()
            .OfType<LayoutDocument>()
            .FirstOrDefault(d => d.Content == vm);
        if (layoutDoc is null) return;

        var floatingWindow = layoutDoc.FindParent<LayoutDocumentFloatingWindow>();
        if (floatingWindow?.RootPanel is null) return;

        _pendingGroup.FloatingLayout = CaptureTree(floatingWindow.RootPanel);

        var fwc = DockingManager.FloatingWindows
            .FirstOrDefault(fw => fw.Model == floatingWindow);
        if (fwc is not null)
        {
            _pendingGroup.FloatingLeft = fwc.Left;
            _pendingGroup.FloatingTop = fwc.Top;
            _pendingGroup.FloatingWidth = fwc.Width;
            _pendingGroup.FloatingHeight = fwc.Height;
            _pendingGroup.FloatingTitle = fwc.Title ?? "";
        }
    }

    private static FloatingPaneNode CaptureTree(ILayoutGroup group)
    {
        if (group is LayoutDocumentPane pane)
        {
            return new FloatingPaneNode
            {
                IsLeaf = true,
                ContentIds = pane.Children.OfType<LayoutDocument>()
                    .Where(d => d.Content is ToolWindowViewModel)
                    .Select(d => ((ToolWindowViewModel)d.Content).ContentId)
                    .ToList(),
                DockWidth = pane.DockWidth.Value,
                DockHeight = pane.DockHeight.Value,
                DockWidthIsStar = pane.DockWidth.IsStar,
                DockHeightIsStar = pane.DockHeight.IsStar,
            };
        }

        if (group is LayoutDocumentPaneGroup paneGroup)
        {
            return new FloatingPaneNode
            {
                IsLeaf = false,
                Orientation = (int)paneGroup.Orientation,
                DockWidth = paneGroup.DockWidth.Value,
                DockHeight = paneGroup.DockHeight.Value,
                DockWidthIsStar = paneGroup.DockWidth.IsStar,
                DockHeightIsStar = paneGroup.DockHeight.IsStar,
                Children = paneGroup.Children
                    .OfType<ILayoutGroup>()
                    .Select(CaptureTree)
                    .ToList(),
            };
        }

        return new FloatingPaneNode { IsLeaf = true, ContentIds = [] };
    }

    #endregion

    #region Restore

    private static void RestoreFloatingLayout(DockingManager dm, UndoCloseGroup<T> group,
        Dictionary<string, LayoutDocument> docLookup)
    {
        if (TryRestoreToSamePaneInFloatingWindow(dm, group, docLookup))
            return;

        GatherFloatingWindowSiblings(dm, group, docLookup);

        if (group.FloatingLayout is { Children.Count: > 0 })
            RestoreWithTree(dm, group, group.FloatingLayout, docLookup);
        else
            RestoreFlat(dm, group, docLookup);
    }

    private static bool TryRestoreToSamePaneInFloatingWindow(DockingManager dm, UndoCloseGroup<T> group,
        Dictionary<string, LayoutDocument> docLookup)
    {
        if (group.FloatingLayout is null) return false;

        var siblingIds = GetSiblingIds(group);
        if (siblingIds.Count == 0) return false;

        bool anySharedPane = false;
        foreach (var rec in group.Records)
        {
            if (rec.ContentId is null) continue;
            var leaf = FindLeafContaining(group.FloatingLayout, rec.ContentId);
            if (leaf?.ContentIds?.Any(id => siblingIds.Contains(id)) == true)
            {
                anySharedPane = true;
                break;
            }
        }
        if (!anySharedPane) return false;

        var targetPane = dm.Layout.Descendents()
            .OfType<LayoutDocument>()
            .Where(d => d.Content is ToolWindowViewModel vm && siblingIds.Contains(vm.ContentId))
            .Select(d => (doc: d, fw: d.FindParent<LayoutFloatingWindow>()))
            .Where(x => x.fw is not null)
            .Select(x => x.doc.Parent as LayoutDocumentPane)
            .FirstOrDefault(p => p is not null);

        if (targetPane is null) return false;

        foreach (var layoutDoc in docLookup.Values)
        {
            (layoutDoc.Parent as ILayoutContainer)?.RemoveChild(layoutDoc);
            targetPane.Children.Add(layoutDoc);
        }

        return true;
    }

    private static void GatherFloatingWindowSiblings(DockingManager dm, UndoCloseGroup<T> group,
        Dictionary<string, LayoutDocument> docLookup)
    {
        if (group.FloatingLayout is null) return;

        var siblingIds = GetSiblingIds(group);
        if (siblingIds.Count == 0) return;

        var siblingDocs = dm.Layout.Descendents()
            .OfType<LayoutDocument>()
            .Where(d => d.Content is ToolWindowViewModel vm && siblingIds.Contains(vm.ContentId))
            .Where(d => d.FindParent<LayoutFloatingWindow>() is not null)
            .ToList();

        foreach (var doc in siblingDocs)
        {
            var contentId = ((ToolWindowViewModel)doc.Content).ContentId;
            (doc.Parent as ILayoutContainer)?.RemoveChild(doc);
            docLookup.TryAdd(contentId, doc);
        }

        foreach (var emptyFw in dm.Layout.FloatingWindows.Where(fw => !fw.IsValid).ToList())
            dm.Layout.FloatingWindows.Remove(emptyFw);
    }

    private static HashSet<string> GetSiblingIds(UndoCloseGroup<T> group)
    {
        var siblingIds = new HashSet<string>();
        CollectContentIds(group.FloatingLayout!, siblingIds);
        foreach (var rec in group.Records)
        {
            if (rec.ContentId is not null)
                siblingIds.Remove(rec.ContentId);
        }
        return siblingIds;
    }

    private static FloatingPaneNode? FindLeafContaining(FloatingPaneNode node, string contentId)
    {
        if (node.IsLeaf && node.ContentIds?.Contains(contentId) == true)
            return node;
        if (node.Children is not null)
            foreach (var child in node.Children)
                if (FindLeafContaining(child, contentId) is { } found)
                    return found;
        return null;
    }

    private static void CollectContentIds(FloatingPaneNode node, HashSet<string> ids)
    {
        if (node.IsLeaf && node.ContentIds is not null)
        {
            foreach (var id in node.ContentIds)
                ids.Add(id);
        }
        if (node.Children is not null)
        {
            foreach (var child in node.Children)
                CollectContentIds(child, ids);
        }
    }

    private static void RestoreWithTree(DockingManager dm, UndoCloseGroup<T> group,
        FloatingPaneNode layout, Dictionary<string, LayoutDocument> docLookup)
    {
        foreach (var layoutDoc in docLookup.Values)
            (layoutDoc.Parent as ILayoutContainer)?.RemoveChild(layoutDoc);

        var rootPanel = BuildPaneGroup(layout, docLookup, isRoot: true);

        var fw = new LayoutDocumentFloatingWindow
        {
            RootPanel = rootPanel,
            Title = group.FloatingTitle,
        };

        rootPanel.FloatingLeft = group.FloatingLeft;
        rootPanel.FloatingTop = group.FloatingTop;
        rootPanel.FloatingWidth = group.FloatingWidth;
        rootPanel.FloatingHeight = group.FloatingHeight;

        dm.Layout.FloatingWindows.Add(fw);
        dm.CreateMissingFloatingWindowControls();

        if (!string.IsNullOrEmpty(group.FloatingTitle))
        {
            var fwc = dm.FloatingWindows.FirstOrDefault(f => f.Model == fw);
            if (fwc is not null)
                fwc.Title = group.FloatingTitle;
        }
    }

    private static LayoutDocumentPaneGroup BuildPaneGroup(
        FloatingPaneNode node, Dictionary<string, LayoutDocument> docLookup, bool isRoot = false)
    {
        var paneGroup = new LayoutDocumentPaneGroup
        {
            Orientation = (Orientation)node.Orientation,
            DockWidth = ToGridLength(node.DockWidth, node.DockWidthIsStar),
            DockHeight = ToGridLength(node.DockHeight, node.DockHeightIsStar),
        };

        if (node.Children is not null)
        {
            foreach (var child in node.Children)
            {
                if (child.IsLeaf)
                {
                    var pane = new LayoutDocumentPane
                    {
                        DockWidth = ToGridLength(child.DockWidth, child.DockWidthIsStar),
                        DockHeight = ToGridLength(child.DockHeight, child.DockHeightIsStar),
                    };
                    if (child.ContentIds is not null)
                    {
                        foreach (var id in child.ContentIds)
                        {
                            if (docLookup.Remove(id, out var doc))
                                pane.Children.Add(doc);
                        }
                    }
                    paneGroup.Children.Add(pane);
                }
                else
                {
                    paneGroup.Children.Add(BuildPaneGroup(child, docLookup));
                }
            }
        }

        if (isRoot && docLookup.Count > 0)
        {
            var spill = new LayoutDocumentPane();
            foreach (var doc in docLookup.Values)
                spill.Children.Add(doc);
            docLookup.Clear();
            paneGroup.Children.Add(spill);
        }

        return paneGroup;
    }

    private static void RestoreFlat(DockingManager dm, UndoCloseGroup<T> group,
        Dictionary<string, LayoutDocument> docLookup)
    {
        LayoutDocumentPane? floatingPane = null;

        foreach (var rec in group.Records)
        {
            if (!rec.IsFloating) continue;
            if (rec.ContentId is null) continue;
            if (!docLookup.TryGetValue(rec.ContentId, out var layoutDoc)) continue;

            if (floatingPane is null)
            {
                layoutDoc.FloatingLeft = rec.FloatingLeft;
                layoutDoc.FloatingTop = rec.FloatingTop;
                layoutDoc.FloatingWidth = rec.FloatingWidth;
                layoutDoc.FloatingHeight = rec.FloatingHeight;
                var fwc = dm.CreateFloatingWindow(layoutDoc, false);
                if (fwc is not null)
                    fwc.Show();
                floatingPane = layoutDoc.Parent as LayoutDocumentPane;
            }
            else
            {
                (layoutDoc.Parent as ILayoutContainer)?.RemoveChild(layoutDoc);
                floatingPane.Children.Add(layoutDoc);
            }
        }
    }

    private static GridLength ToGridLength(double value, bool isStar) =>
        isStar ? new(value, GridUnitType.Star) : new(value, GridUnitType.Pixel);

    #endregion

    #region Undo stack

    private void SealGroup()
    {
        if (_pendingGroup is null) return;
        _undoStack.Push(_pendingGroup);
        _pendingGroup = null;
        while (_undoStack.Count > MaxUndoDepth)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
                _undoStack.Push(items[i]);
            break;
        }
    }

    #endregion
}
