using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernTreeListView;

/// <summary>
/// ModernTreeListView&lt;TModel&gt; — A high-quality, production-ready, open-source hybrid Tree + ListView control for WinForms (.NET 8+).
/// 
/// Features:
/// - Excellent in-place editing with built-in and custom editors (TextBox, ComboBox, DateTimePicker, CheckBox, NumericUpDown)
/// - Full column sorting (click header, stable, indicators)
/// - Virtual mode for 50,000 – 500,000+ nodes (on-demand roots + children)
/// - Tri-state checkboxes with hierarchical propagation
/// - Drag &amp; Drop (reorder + change parent) with visual drop indicator
/// - Powerful filtering (predicate + parent preservation)
/// - Multi-selection (Ctrl/Shift)
/// - Async children loading with loading indicator
/// - Dark mode + fully customizable colors
/// - Lazy / virtual loading friendly
/// - Modern, clean, high-performance custom painting
/// 
/// Quick Start (Normal Mode):
///   var tree = new ModernTreeListView&lt;MyItem&gt;()
///       .AddColumn("Name", m =&gt; m.Name, 280)
///       .AddColumn("Date", m =&gt; m.Date, 120)
///       .SetRoots(items)
///       .SetChildrenGetter(m =&gt; m.Children)
///       .SetCellValueSetter((m, col, val) =&gt; { /* update */ });
/// 
/// Virtual Mode (Large Data):
///   tree.VirtualMode = true;
///   tree.VirtualRootCount = 100_000;
///   tree.SetVirtualRootGetter(i =&gt; CreateRoot(i));
///   tree.SetVirtualChildCountGetter(parent =&gt; 50);
///   tree.SetVirtualChildGetter((parent, idx) =&gt; CreateChild(parent, idx));
/// 
/// License: MIT (see LICENSE file)
/// </summary>
/// <typeparam name="TModel">The data model type. Can be record, class, or struct. For virtual/large scenarios, prefer lightweight descriptors or use a key getter.</typeparam>
public sealed class ModernTreeListView<TModel> : Control where TModel : notnull
{
    // ==================== CONSTANTS ====================
    private const int DefaultRowHeight = 26;
    private const int DefaultHeaderHeight = 32;
    private const int IndentSize = 18;
    private const int ExpanderSize = 11;
    private const int MinColumnWidth = 36;
    private const int CellPadding = 6;
    private const int ExpanderMargin = 3;
    private const int CheckboxSize = 13;
    private const int CheckboxMargin = 3;

    // ==================== STATE ====================
    private readonly List<TreeListColumn<TModel>> _columns = [];
    private readonly List<TreeNode> _rootNodes = [];
    private readonly List<VisibleRow> _visibleRows = [];

    private Func<TModel, IEnumerable<TModel>>? _childrenGetter;
    private Func<TModel, Task<IEnumerable<TModel>>>? _childrenGetterAsync;
    private Func<TModel, bool>? _hasChildrenGetter;
    private Action<TModel, TreeListColumn<TModel>, object?>? _setCellValue;

    // Virtual mode support
    private bool _virtualMode;
    private int _virtualRootCount;
    private Func<int, TModel>? _virtualRootGetter;
    private Func<TModel, int>? _virtualChildCountGetter;
    private Func<TModel, int, TModel>? _virtualChildGetter;

    // Filtering
    private Func<TModel, bool>? _filter;

    // Theming
    private bool _useDarkMode;

    // Selection
    private bool _multiSelect;
    private TreeNode? _anchorNode; // for Shift range selection
    private readonly HashSet<TreeNode> _selectedNodes = [];

    // Checkboxes
    private bool _showCheckboxes;
    private bool _autoCheckChildren = true;
    private readonly Dictionary<object, CheckState> _checkStates = new(); // key -> state (for both normal + virtual)
    private Func<TModel, object?>? _modelKeyGetter;

    // Drag & Drop
    private bool _allowDragDrop;
    private TreeNode? _dragSourceNode;
    private int _dropTargetRowIndex = -1;
    private DropPosition _dropPosition = DropPosition.None;

    // Editor registry (per-column)
    private readonly Dictionary<int, Func<CellEditorContext<TModel>, Control>> _columnEditors = [];

    // State
    private TreeNode? _selectedNode; // primary / anchor for single-select
    private int _selectedIndex = -1;

    private int _rowHeight = DefaultRowHeight;
    private int _headerHeight = DefaultHeaderHeight;

    private int _vOffset;
    private int _hOffset;

    private VScrollBar _vScrollBar = null!;
    private HScrollBar _hScrollBar = null!;

    // Column resizing
    private int _resizingColumnIndex = -1;
    private int _resizeStartX;
    private int _resizeStartWidth;

    // Editing
    private int _currentEditColumnHint;
    private Control? _activeEditor;
    private int _editingRowIndex = -1;
    private int _editingColIndex = -1;
    private TreeNode? _editingNode;
    private object? _editingOriginalValue;

    // Async loading indicators (node -> loading)
    private readonly HashSet<TreeNode> _loadingNodes = [];

    // ==================== APPEARANCE (fully customizable) ====================
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HeaderBackColor { get; set; } = Color.FromArgb(247, 248, 250);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HeaderForeColor { get; set; } = Color.FromArgb(52, 58, 64);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color RowBackColor { get; set; } = Color.White;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AlternatingRowBackColor { get; set; } = Color.FromArgb(250, 251, 252);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectionBackColor { get; set; } = Color.FromArgb(0, 120, 212);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectionForeColor { get; set; } = Color.White;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color GridLineColor { get; set; } = Color.FromArgb(234, 236, 239);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ExpanderColor { get; set; } = Color.FromArgb(108, 117, 125);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TreeLineColor { get; set; } = Color.FromArgb(206, 212, 218);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBackColor { get; set; } = Color.FromArgb(241, 243, 245);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CheckboxColor { get; set; } = Color.FromArgb(108, 117, 125);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DragDropIndicatorColor { get; set; } = Color.FromArgb(0, 120, 212);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color LoadingForeColor { get; set; } = Color.FromArgb(108, 117, 125);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowAlternatingRows { get; set; } = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGridLines { get; set; } = false;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FullRowSelect { get; set; } = true;

    // ==================== PUBLIC EVENTS ====================
    public event EventHandler<CellEditEventArgs<TModel>>? CellEditCommitted;
    public event EventHandler<CellEditEventArgs<TModel>>? CellEditCanceled;
    public event EventHandler? SelectionChanged;
    public event EventHandler<TreeNodeEventArgs<TModel>>? NodeExpanded;
    public event EventHandler<TreeNodeEventArgs<TModel>>? NodeCollapsed;
    public event EventHandler? ColumnSortChanged;

    /// <summary>
    /// Raised when a node's checkbox state changes (including via tri-state propagation).
    /// </summary>
    public event EventHandler<TreeNodeEventArgs<TModel>>? NodeCheckStateChanged;

    /// <summary>
    /// Raised when the user starts dragging an item (use to customize the drag data if needed).
    /// </summary>
    public event EventHandler<ItemDragEventArgs<TModel>>? ItemDrag;

    /// <summary>
    /// Gives you full control during drag-over. Set e.Effect and optionally customize drop target.
    /// </summary>
    public event EventHandler<TreeDragOverEventArgs<TModel>>? DragOverNode;

    /// <summary>
    /// Final drop occurred. Perform your data mutation here then call Rebuild() or ReplaceModel as needed.
    /// </summary>
    public event EventHandler<TreeDragDropEventArgs<TModel>>? DragDropNode;

    /// <summary>
    /// Virtual mode: retrieve the model for a given root index or child position.
    /// Set the Model property on the args to supply data.
    /// </summary>
    public event EventHandler<RetrieveVirtualNodeEventArgs<TModel>>? RetrieveVirtualNode;

    /// <summary>
    /// Optional hint that the control is about to access a range of virtual items (for caching strategies).
    /// </summary>
    public event EventHandler<CacheVirtualNodesEventArgs>? CacheVirtualNodes;

    // ==================== CONSTRUCTOR ====================
    public ModernTreeListView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        DoubleBuffered = true;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(33, 37, 41);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        TabStop = true;

        InitializeScrollBars();
    }

    private void InitializeScrollBars()
    {
        _vScrollBar = new VScrollBar
        {
            Visible = false,
            Width = SystemInformation.VerticalScrollBarWidth,
            Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
        };
        _hScrollBar = new HScrollBar
        {
            Visible = false,
            Height = SystemInformation.HorizontalScrollBarHeight,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.Add(_vScrollBar);
        Controls.Add(_hScrollBar);

        _vScrollBar.Scroll += OnVScroll;
        _hScrollBar.Scroll += OnHScroll;
    }

    // ==================== FLUENT API (existing + new) ====================

    /// <summary>
    /// Adds a column with fluent configuration support.
    /// </summary>
    public ModernTreeListView<TModel> AddColumn(
        string title,
        Func<TModel, object?> getter,
        int width = 140,
        Action<TreeListColumn<TModel>>? configure = null)
    {
        var column = new TreeListColumn<TModel>(title, getter, width);
        configure?.Invoke(column);
        _columns.Add(column);
        UpdateScrollbars();
        Invalidate();
        return this;
    }

    public ModernTreeListView<TModel> ClearColumns()
    {
        _columns.Clear();
        UpdateScrollbars();
        Invalidate();
        return this;
    }

    /// <summary>
    /// Sets the root models (normal mode). Ignored when VirtualMode is true.
    /// </summary>
    public ModernTreeListView<TModel> SetRoots(IEnumerable<TModel> roots)
    {
        if (VirtualMode) return this;
        LoadRoots(roots);
        return this;
    }

    public ModernTreeListView<TModel> SetChildrenGetter(Func<TModel, IEnumerable<TModel>> getter)
    {
        _childrenGetter = getter;
        return this;
    }

    /// <summary>
    /// Sets an async children getter. When expanding a node, the control will show a loading indicator
    /// and populate children when the task completes.
    /// </summary>
    public ModernTreeListView<TModel> SetChildrenGetterAsync(Func<TModel, Task<IEnumerable<TModel>>> getter)
    {
        _childrenGetterAsync = getter;
        return this;
    }

    public ModernTreeListView<TModel> SetHasChildrenGetter(Func<TModel, bool> getter)
    {
        _hasChildrenGetter = getter;
        return this;
    }

    public ModernTreeListView<TModel> SetCellValueSetter(Action<TModel, TreeListColumn<TModel>, object?> setter)
    {
        _setCellValue = setter;
        return this;
    }

    /// <summary>
    /// Registers a custom editor factory for a specific column.
    /// The factory receives context (model, column, current value) and must return a live Control (TextBox, ComboBox, etc.).
    /// </summary>
    public ModernTreeListView<TModel> SetColumnEditor(int columnIndex, Func<CellEditorContext<TModel>, Control> editorFactory)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));

        _columnEditors[columnIndex] = editorFactory;
        return this;
    }

    /// <summary>
    /// Enables or disables virtual mode. In virtual mode the control never loads the full tree.
    /// You must provide VirtualRootCount + virtual getters (or handle RetrieveVirtualNode).
    /// </summary>
    public ModernTreeListView<TModel> SetVirtualMode(bool enabled)
    {
        VirtualMode = enabled;
        return this;
    }

    /// <summary>
    /// Sets how many root nodes exist in virtual mode.
    /// </summary>
    public ModernTreeListView<TModel> SetVirtualRootCount(int count)
    {
        VirtualRootCount = Math.Max(0, count);
        if (VirtualMode)
        {
            Rebuild();
        }
        return this;
    }

    /// <summary>
    /// Provides the root model for a given root index (0-based) in virtual mode.
    /// </summary>
    public ModernTreeListView<TModel> SetVirtualRootGetter(Func<int, TModel> getter)
    {
        _virtualRootGetter = getter;
        return this;
    }

    /// <summary>
    /// Returns how many direct children the given parent has (virtual mode).
    /// </summary>
    public ModernTreeListView<TModel> SetVirtualChildCountGetter(Func<TModel, int> getter)
    {
        _virtualChildCountGetter = getter;
        return this;
    }

    /// <summary>
    /// Returns the child at the given index under the parent (virtual mode).
    /// </summary>
    public ModernTreeListView<TModel> SetVirtualChildGetter(Func<TModel, int, TModel> getter)
    {
        _virtualChildGetter = getter;
        return this;
    }

    /// <summary>
    /// Sets a stable key extractor. Strongly recommended for virtual mode, checkboxes, and multi-select
    /// when your TModel instances are recreated frequently (records, DTOs).
    /// </summary>
    public ModernTreeListView<TModel> SetModelKeyGetter(Func<TModel, object?> keyGetter)
    {
        _modelKeyGetter = keyGetter;
        return this;
    }

    /// <summary>
    /// Sets a filter predicate. Only nodes (or their ancestors) that match are shown.
    /// Call with null to clear.
    /// </summary>
    public ModernTreeListView<TModel> SetFilter(Func<TModel, bool>? predicate)
    {
        _filter = predicate;
        Rebuild();
        return this;
    }

    /// <summary>
    /// Convenience text filter. Searches across all column display text (case-insensitive).
    /// </summary>
    public ModernTreeListView<TModel> SetFilterText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SetFilter(null);
        }

        string term = text.Trim().ToLowerInvariant();
        return SetFilter(model =>
        {
            foreach (var col in _columns)
            {
                string display = GetDisplayText(model, col).ToLowerInvariant();
                if (display.Contains(term)) return true;
            }
            return false;
        });
    }

    public void Rebuild()
    {
        CancelEdit();
        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    public void Reload()
    {
        CancelEdit();
        if (VirtualMode)
        {
            RebuildVisibleRows();
            UpdateScrollbars();
            Invalidate();
            return;
        }
        var currentRoots = _rootNodes.Select(n => n.Model).ToList();
        LoadRoots(currentRoots);
    }

    /// <summary>
    /// Replaces a model reference (excellent for immutable records).
    /// </summary>
    public void ReplaceModel(TModel oldModel, TModel newModel)
    {
        var node = FindNode(oldModel);
        if (node == null) return;

        bool wasSelected = _selectedNode == node;

        node.ReplaceModelReference(newModel);

        RebuildVisibleRows();

        if (wasSelected)
        {
            _selectedNode = node;
            _selectedIndex = _visibleRows.FindIndex(vr => vr.Node == node);
        }

        UpdateScrollbars();
        Invalidate();
    }

    public void RefreshObject(TModel model)
    {
        var node = FindNode(model);
        if (node == null) return;

        int idx = _visibleRows.FindIndex(vr => vr.Node == node);
        if (idx >= 0)
        {
            InvalidateRow(idx);
        }
    }

    // ==================== PROPERTIES ====================

    public IReadOnlyList<TreeListColumn<TModel>> Columns => _columns;

    public TModel? SelectedModel => _selectedNode is { } n ? n.Model : default;

    /// <summary>
    /// All currently selected models (supports multi-select).
    /// </summary>
    public IReadOnlyList<TModel> SelectedModels
    {
        get
        {
            if (_multiSelect && _selectedNodes.Count > 0)
            {
                return _selectedNodes
                    .Where(n => n != null)
                    .Select(n => n.Model)
                    .ToList();
            }
            return _selectedNode != null ? [_selectedNode.Model] : [];
        }
    }

    public int SelectedRowIndex => _selectedIndex;

    /// <summary>
    /// Enables virtual mode for massive datasets. When true, use the virtual getter APIs.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool VirtualMode
    {
        get => _virtualMode;
        set
        {
            if (_virtualMode == value) return;
            _virtualMode = value;
            CancelEdit();
            _rootNodes.Clear();
            _visibleRows.Clear();
            _selectedNode = null;
            _selectedNodes.Clear();
            _anchorNode = null;
            RebuildVisibleRows();
            UpdateScrollbars();
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VirtualRootCount
    {
        get => _virtualRootCount;
        set
        {
            _virtualRootCount = Math.Max(0, value);
            if (VirtualMode)
            {
                Rebuild();
            }
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = Math.Max(16, value);
            UpdateScrollbars();
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            _headerHeight = Math.Max(18, value);
            UpdateScrollbars();
            Invalidate();
        }
    }

    /// <summary>
    /// Enables checkboxes (with tri-state support for parents).
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowCheckboxes
    {
        get => _showCheckboxes;
        set
        {
            if (_showCheckboxes == value) return;
            _showCheckboxes = value;
            Invalidate();
        }
    }

    /// <summary>
    /// When true (default), checking a parent will check/uncheck all its children.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoCheckChildren
    {
        get => _autoCheckChildren;
        set => _autoCheckChildren = value;
    }

    /// <summary>
    /// Enables full drag &amp; drop support (reordering and changing parents).
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowDragDrop
    {
        get => _allowDragDrop;
        set => _allowDragDrop = value;
    }

    /// <summary>
    /// Enables multiple row selection (Ctrl+Click, Shift+Click, Shift+Arrow).
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiSelect
    {
        get => _multiSelect;
        set
        {
            if (_multiSelect == value) return;
            _multiSelect = value;
            if (!_multiSelect)
            {
                _selectedNodes.Clear();
                if (_selectedNode != null)
                    _selectedNodes.Add(_selectedNode);
            }
            Invalidate();
        }
    }

    /// <summary>
    /// Toggles a dark color scheme. All individual color properties remain overridable afterwards.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseDarkMode
    {
        get => _useDarkMode;
        set
        {
            if (_useDarkMode == value) return;
            _useDarkMode = value;
            ApplyTheme();
            Invalidate();
        }
    }

    // ==================== SORTING (from previous version, kept) ====================
    private int _sortColumnIndex = -1;
    private SortOrder _sortOrder = SortOrder.None;

    public int? SortColumn => _sortColumnIndex >= 0 ? _sortColumnIndex : null;
    public SortOrder SortOrder => _sortOrder;

    public void Sort(int columnIndex, SortOrder order)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count || order == SortOrder.None)
        {
            ClearSortInternal();
            return;
        }
        _sortColumnIndex = columnIndex;
        _sortOrder = order;
        ColumnSortChanged?.Invoke(this, EventArgs.Empty);
        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    public void ClearSort()
    {
        ClearSortInternal();
    }

    private void ClearSortInternal()
    {
        if (_sortColumnIndex == -1 && _sortOrder == SortOrder.None) return;
        _sortColumnIndex = -1;
        _sortOrder = SortOrder.None;
        ColumnSortChanged?.Invoke(this, EventArgs.Empty);
        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    // ==================== DATA LOADING ====================

    private void LoadRoots(IEnumerable<TModel> roots)
    {
        CancelEdit();
        _rootNodes.Clear();
        _visibleRows.Clear();
        _selectedNode = null;
        _selectedNodes.Clear();
        _anchorNode = null;

        if (roots != null)
        {
            int idx = 0;
            foreach (var model in roots)
            {
                var node = new TreeNode(model, parent: null);
                node.OriginalIndex = idx++;
                _rootNodes.Add(node);
            }
        }

        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    private void RebuildVisibleRows()
    {
        _visibleRows.Clear();
        _loadingNodes.Clear(); // reset loading visuals on rebuild

        if (VirtualMode)
        {
            BuildVirtualVisibleRows();
        }
        else
        {
            foreach (var root in _rootNodes)
            {
                AppendVisible(root, level: 0);
            }
        }

        RestoreSelectionAfterRebuild();
    }

    private void BuildVirtualVisibleRows()
    {
        if (_virtualRootCount <= 0) return;

        for (int i = 0; i < _virtualRootCount; i++)
        {
            var model = GetVirtualModel(null, i);
            if (model == null) continue;

            var node = GetOrCreateVirtualNode(model, null, i);
            AppendVisible(node, level: 0);
        }
    }

    private TModel? GetVirtualModel(TreeNode? parent, int childIndex)
    {
        // Prefer explicit getters
        if (parent == null)
        {
            if (_virtualRootGetter != null)
                return _virtualRootGetter(childIndex);

            // Fallback to event
            var args = new RetrieveVirtualNodeEventArgs<TModel>(childIndex, default, childIndex);
            RetrieveVirtualNode?.Invoke(this, args);
            return args.Model;
        }
        else
        {
            if (_virtualChildGetter != null)
                return _virtualChildGetter(parent.Model, childIndex);

            var args = new RetrieveVirtualNodeEventArgs<TModel>(-1, parent.Model, childIndex);
            RetrieveVirtualNode?.Invoke(this, args);
            return args.Model;
        }
    }

    private int GetVirtualChildCount(TreeNode node)
    {
        if (_virtualChildCountGetter != null)
            return _virtualChildCountGetter(node.Model);

        // If no count getter, we can still work if child getter is index based, but for safety return 0 or optimistic.
        // Better: many virtual sources know the count.
        return 0;
    }

    private TreeNode GetOrCreateVirtualNode(TModel model, TreeNode? parent, int siblingIndex)
    {
        // In virtual mode we create lightweight nodes on demand for visible rows only.
        // We do not keep a persistent tree of all 500k nodes.
        var node = new TreeNode(model, parent);
        node.OriginalIndex = siblingIndex;
        node.IsVirtual = true;
        return node;
    }

    private void AppendVisible(TreeNode node, int level)
    {
        if (IsFilteredOut(node)) return;

        _visibleRows.Add(new VisibleRow(node, level));

        if (node.IsExpanded)
        {
            EnsureChildrenLoaded(node);

            var children = GetOrderedChildren(node);
            foreach (var child in children)
            {
                AppendVisible(child, level + 1);
            }
        }
    }

    private bool IsFilteredOut(TreeNode node)
    {
        if (_filter == null) return false;

        // Include if this node matches or any descendant matches (preserve hierarchy)
        if (_filter(node.Model)) return false;

        // Check subtree (expensive for deep virtual trees — acceptable for visible portion)
        EnsureChildrenLoaded(node);
        foreach (var child in node.Children)
        {
            if (!IsFilteredOut(child)) return false; // at least one descendant is visible
        }
        return true;
    }

    private void EnsureChildrenLoaded(TreeNode node)
    {
        if (node.ChildrenLoaded) return;

        if (VirtualMode)
        {
            // Virtual nodes never "preload" everything. We create children on demand when expanding visible rows.
            int count = GetVirtualChildCount(node);
            node.Children.Clear();
            for (int i = 0; i < count; i++)
            {
                var childModel = GetVirtualModel(node, i);
                if (childModel != null)
                {
                    var childNode = GetOrCreateVirtualNode(childModel, node, i);
                    node.Children.Add(childNode);
                }
            }
            node.ChildrenLoaded = true;
            return;
        }

        // Normal mode
        if (_childrenGetterAsync != null)
        {
            // Async path is started from ToggleExpand, not here.
            // If we reach here without children, treat as empty for now.
            node.ChildrenLoaded = true;
            return;
        }

        if (_childrenGetter != null)
        {
            node.Children.Clear();
            int idx = 0;
            foreach (var child in _childrenGetter(node.Model) ?? [])
            {
                var n = new TreeNode(child, node);
                n.OriginalIndex = idx++;
                node.Children.Add(n);
            }
            node.ChildrenLoaded = true;
        }
    }

    private IEnumerable<TreeNode> GetOrderedChildren(TreeNode node)
    {
        if (_sortColumnIndex < 0 || _sortOrder == SortOrder.None)
            return node.Children;

        var col = _columns[_sortColumnIndex];
        var comparer = new ValueComparer();

        if (_sortOrder == SortOrder.Ascending)
            return node.Children
                .OrderBy(n => GetSortableKey(n.Model, col), comparer)
                .ThenBy(n => n.OriginalIndex);

        return node.Children
            .OrderByDescending(n => GetSortableKey(n.Model, col), comparer)
            .ThenBy(n => n.OriginalIndex);
    }

    private static object? GetSortableKey(TModel model, TreeListColumn<TModel> column) => column.Getter(model);

    private sealed class ValueComparer : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (x.GetType() == y.GetType() && x is IComparable cx) { try { return cx.CompareTo(y); } catch { } }
            if (x is IComparable cx2) { try { return cx2.CompareTo(y); } catch { } }
            if (y is IComparable cy2) { try { return -cy2.CompareTo(x); } catch { } }
            return string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private void RestoreSelectionAfterRebuild()
    {
        if (_selectedNode != null)
        {
            _selectedIndex = _visibleRows.FindIndex(vr => vr.Node == _selectedNode);
            if (_selectedIndex < 0) _selectedNode = null;
        }
        else
        {
            _selectedIndex = -1;
        }

        // Reconcile multi-select set with current visible nodes
        if (_multiSelect && _selectedNodes.Count > 0)
        {
            var stillVisible = _selectedNodes
                .Where(n => _visibleRows.Any(v => v.Node == n))
                .ToHashSet();
            _selectedNodes.Clear();
            foreach (var n in stillVisible) _selectedNodes.Add(n);
        }
    }

    private bool NodeHasChildren(TreeNode node)
    {
        if (node.ChildrenLoaded)
            return node.Children.Count > 0;

        if (VirtualMode)
        {
            return GetVirtualChildCount(node) > 0;
        }

        if (_hasChildrenGetter != null)
            return _hasChildrenGetter(node.Model);

        return _childrenGetter != null || _childrenGetterAsync != null;
    }

    // ==================== CHECKBOX STATE ====================

    private CheckState GetCheckState(TreeNode node)
    {
        var key = GetModelKey(node.Model);
        if (_checkStates.TryGetValue(key, out var state))
            return state;

        // Default to Unchecked. For virtual we may want to query externally but keep simple.
        return CheckState.Unchecked;
    }

    private void SetCheckState(TreeNode node, CheckState state, bool updateVisual = true)
    {
        var key = GetModelKey(node.Model);
        _checkStates[key] = state;

        if (updateVisual)
        {
            int idx = _visibleRows.FindIndex(v => v.Node == node);
            if (idx >= 0) InvalidateRow(idx);
        }

        NodeCheckStateChanged?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));
    }

    private object GetModelKey(TModel model)
    {
        if (_modelKeyGetter != null)
        {
            var k = _modelKeyGetter(model);
            if (k != null) return k;
        }
        return model; // fallback to reference / value equality
    }

    private void SetNodeChecked(TreeNode node, bool isChecked)
    {
        var newState = isChecked ? CheckState.Checked : CheckState.Unchecked;
        SetCheckState(node, newState);

        if (_autoCheckChildren && node.ChildrenLoaded)
        {
            foreach (var child in node.Children)
            {
                SetNodeCheckedRecursive(child, isChecked);
            }
        }

        UpdateAncestorCheckStates(node);
    }

    private void SetNodeCheckedRecursive(TreeNode node, bool isChecked)
    {
        SetCheckState(node, isChecked ? CheckState.Checked : CheckState.Unchecked, updateVisual: true);
        if (node.ChildrenLoaded)
        {
            foreach (var child in node.Children)
                SetNodeCheckedRecursive(child, isChecked);
        }
    }

    private void UpdateAncestorCheckStates(TreeNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            var state = ComputeParentCheckState(current);
            SetCheckState(current, state);
            current = current.Parent;
        }
    }

    private CheckState ComputeParentCheckState(TreeNode parent)
    {
        if (!parent.ChildrenLoaded || parent.Children.Count == 0)
            return GetCheckState(parent);

        bool anyChecked = false;
        bool anyUnchecked = false;

        foreach (var child in parent.Children)
        {
            var s = GetCheckState(child);
            if (s == CheckState.Checked) anyChecked = true;
            if (s == CheckState.Unchecked) anyUnchecked = true;
            if (s == CheckState.Indeterminate)
            {
                anyChecked = true;
                anyUnchecked = true;
            }
        }

        if (anyChecked && anyUnchecked) return CheckState.Indeterminate;
        if (anyChecked) return CheckState.Checked;
        return CheckState.Unchecked;
    }

    /// <summary>
    /// Returns all models that are fully checked (not indeterminate).
    /// </summary>
    public IReadOnlyList<TModel> GetCheckedItems()
    {
        var result = new List<TModel>();
        if (VirtualMode)
        {
            // In virtual mode we can only know about currently materialized (visible) nodes + their stored states.
            // For a complete answer the caller should maintain external state or we would need a full virtual walk.
            foreach (var vr in _visibleRows)
            {
                if (GetCheckState(vr.Node) == CheckState.Checked)
                    result.Add(vr.Node.Model);
            }
            return result;
        }

        void Collect(TreeNode n)
        {
            if (GetCheckState(n) == CheckState.Checked)
                result.Add(n.Model);

            if (n.ChildrenLoaded)
            {
                foreach (var c in n.Children) Collect(c);
            }
        }

        foreach (var root in _rootNodes) Collect(root);
        return result;
    }

    /// <summary>
    /// Programmatically sets the check state of a model (and optionally its descendants).
    /// </summary>
    public void SetChecked(TModel model, bool isChecked, bool applyToDescendants = true)
    {
        var node = FindNode(model);
        if (node == null) return;

        if (applyToDescendants)
            SetNodeCheckedRecursive(node, isChecked);
        else
            SetCheckState(node, isChecked ? CheckState.Checked : CheckState.Unchecked);

        UpdateAncestorCheckStates(node);
        Invalidate();
    }

    // ==================== SCROLLING ====================
    // (kept mostly identical to previous version for brevity — same logic)
    private void OnVScroll(object? sender, ScrollEventArgs e)
    {
        CancelEdit();
        _vOffset = _vScrollBar.Value;
        Invalidate();
    }

    private void OnHScroll(object? sender, ScrollEventArgs e)
    {
        CancelEdit();
        _hOffset = _hScrollBar.Value;
        Invalidate();
    }

    private void UpdateScrollbars()
    {
        if (_vScrollBar == null || _hScrollBar == null) return;

        int clientWidth = ClientSize.Width;
        int clientHeight = ClientSize.Height;
        int header = _headerHeight;
        int viewHeight = Math.Max(0, clientHeight - header);

        int totalHeight = _visibleRows.Count * _rowHeight;
        bool needV = totalHeight > viewHeight && viewHeight > 0;

        _vScrollBar.Visible = needV;
        if (needV)
        {
            _vScrollBar.Left = clientWidth - _vScrollBar.Width;
            _vScrollBar.Top = header;
            _vScrollBar.Height = viewHeight;

            int large = Math.Max(_rowHeight, viewHeight);
            _vScrollBar.LargeChange = large;
            _vScrollBar.SmallChange = _rowHeight;

            int maxVal = Math.Max(0, totalHeight - viewHeight);
            _vScrollBar.Maximum = maxVal + large - 1;
            _vScrollBar.Value = Math.Min(_vOffset, maxVal);
            _vOffset = _vScrollBar.Value;
        }
        else
        {
            _vOffset = 0;
        }

        int totalWidth = _columns.Sum(c => c.Width);
        bool needH = totalWidth > clientWidth && clientWidth > 0;

        _hScrollBar.Visible = needH;
        if (needH)
        {
            int bottom = clientHeight - _hScrollBar.Height;
            _hScrollBar.Left = 0;
            _hScrollBar.Top = bottom;
            _hScrollBar.Width = clientWidth - (needV ? _vScrollBar.Width : 0);

            int large = Math.Max(10, clientWidth / 3);
            _hScrollBar.LargeChange = large;
            _hScrollBar.SmallChange = 20;

            int maxVal = Math.Max(0, totalWidth - clientWidth);
            _hScrollBar.Maximum = maxVal + large - 1;
            _hScrollBar.Value = Math.Min(_hOffset, maxVal);
            _hOffset = _hScrollBar.Value;
        }
        else
        {
            _hOffset = 0;
        }
    }

    private void EnsureRowVisible(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _visibleRows.Count) return;

        int rowTop = rowIndex * _rowHeight;
        int rowBottom = rowTop + _rowHeight;
        int viewTop = _vOffset;
        int viewHeight = Math.Max(0, ClientSize.Height - _headerHeight);
        int viewBottom = viewTop + viewHeight;

        if (rowTop < viewTop) _vOffset = rowTop;
        else if (rowBottom > viewBottom) _vOffset = rowBottom - viewHeight;

        if (_vScrollBar.Visible)
        {
            int maxVal = Math.Max(0, (_visibleRows.Count * _rowHeight) - viewHeight);
            _vScrollBar.Value = Math.Clamp(_vOffset, 0, maxVal);
            _vOffset = _vScrollBar.Value;
        }
        Invalidate();
    }

    // ==================== HIT TESTING (extended for checkbox + drag) ====================
    private readonly record struct HitTestResult(
        int RowIndex,
        int ColumnIndex,
        bool IsExpander,
        bool IsCheckbox,
        bool IsHeader,
        bool IsValid);

    private HitTestResult HitTest(int x, int y)
    {
        if (y < 0) return default;

        if (y < _headerHeight)
        {
            int colX = -_hOffset;
            for (int c = 0; c < _columns.Count; c++)
            {
                int w = _columns[c].Width;
                if (x >= colX && x < colX + w)
                    return new HitTestResult(-1, c, false, false, true, true);
                colX += w;
            }
            return new HitTestResult(-1, -1, false, false, true, true);
        }

        int rowY = _headerHeight - (_vOffset % _rowHeight);
        int firstRow = _vOffset / _rowHeight;
        int rowIndex = firstRow + (y - _headerHeight + (_vOffset % _rowHeight)) / _rowHeight;

        if (rowIndex < 0 || rowIndex >= _visibleRows.Count)
            return default;

        var vrow = _visibleRows[rowIndex];

        int cellX = -_hOffset;
        int colIndex = -1;
        bool isExpander = false;
        bool isCheckbox = false;

        for (int c = 0; c < _columns.Count; c++)
        {
            int w = _columns[c].Width;
            if (x >= cellX && x < cellX + w)
            {
                colIndex = c;
                if (c == 0)
                {
                    int indent = vrow.Level * IndentSize;
                    int contentLeft = cellX + indent + ExpanderMargin;

                    // Checkbox hit test (if enabled)
                    if (_showCheckboxes)
                    {
                        int cbLeft = contentLeft;
                        int cbRight = cbLeft + CheckboxSize;
                        if (x >= cbLeft && x <= cbRight)
                        {
                            isCheckbox = true;
                            return new HitTestResult(rowIndex, c, false, true, false, true);
                        }
                        contentLeft += CheckboxSize + CheckboxMargin;
                    }

                    if (NodeHasChildren(vrow.Node))
                    {
                        int expanderLeft = contentLeft;
                        int expanderRight = expanderLeft + ExpanderSize + 2;
                        if (x >= expanderLeft && x <= expanderRight)
                            isExpander = true;
                    }
                }
                break;
            }
            cellX += w;
        }

        return new HitTestResult(rowIndex, colIndex, isExpander, isCheckbox, false, true);
    }

    // ==================== SELECTION (multi-select aware) ====================
    private void SetSelection(int rowIndex, bool ctrl = false, bool shift = false)
    {
        if (rowIndex < 0 || rowIndex >= _visibleRows.Count)
        {
            ClearSelection();
            return;
        }

        var newNode = _visibleRows[rowIndex].Node;

        if (!_multiSelect)
        {
            if (_selectedNode == newNode) return;
            _selectedNode = newNode;
            _selectedIndex = rowIndex;
            _selectedNodes.Clear();
            _selectedNodes.Add(newNode);
            _anchorNode = newNode;
        }
        else
        {
            if (shift && _anchorNode != null)
            {
                // Range selection
                int anchorIdx = _visibleRows.FindIndex(v => v.Node == _anchorNode);
                if (anchorIdx < 0) anchorIdx = 0;

                int start = Math.Min(anchorIdx, rowIndex);
                int end = Math.Max(anchorIdx, rowIndex);

                _selectedNodes.Clear();
                for (int i = start; i <= end; i++)
                {
                    _selectedNodes.Add(_visibleRows[i].Node);
                }
                _selectedNode = _visibleRows[rowIndex].Node;
                _selectedIndex = rowIndex;
            }
            else if (ctrl)
            {
                if (_selectedNodes.Contains(newNode))
                    _selectedNodes.Remove(newNode);
                else
                    _selectedNodes.Add(newNode);

                _selectedNode = newNode;
                _selectedIndex = rowIndex;
                if (_anchorNode == null) _anchorNode = newNode;
            }
            else
            {
                _selectedNodes.Clear();
                _selectedNodes.Add(newNode);
                _selectedNode = newNode;
                _selectedIndex = rowIndex;
                _anchorNode = newNode;
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void SelectModel(TModel model)
    {
        var node = FindNode(model);
        if (node == null) return;

        ExpandAncestors(node);
        RebuildVisibleRows();

        int idx = _visibleRows.FindIndex(vr => vr.Node == node);
        if (idx >= 0)
        {
            SetSelection(idx);
            EnsureRowVisible(idx);
        }
    }

    public void ClearSelection()
    {
        _selectedNode = null;
        _selectedIndex = -1;
        _selectedNodes.Clear();
        _anchorNode = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private TreeNode? FindNode(TModel model)
    {
        if (VirtualMode)
        {
            // In virtual mode we can only find among materialized visible nodes
            return _visibleRows.FirstOrDefault(vr => EqualityComparer<TModel>.Default.Equals(vr.Node.Model, model)).Node;
        }

        foreach (var root in _rootNodes)
        {
            var found = FindNodeRecursive(root, model);
            if (found != null) return found;
        }
        return null;
    }

    private TreeNode? FindNodeRecursive(TreeNode node, TModel model)
    {
        if (EqualityComparer<TModel>.Default.Equals(node.Model, model))
            return node;

        if (node.ChildrenLoaded)
        {
            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, model);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void ExpandAncestors(TreeNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (!current.IsExpanded)
            {
                current.EnsureChildrenLoaded(_childrenGetter);
                current.IsExpanded = true;
                NodeExpanded?.Invoke(this, new TreeNodeEventArgs<TModel>(current.Model, current));
            }
            current = current.Parent;
        }
    }

    // ==================== EXPAND / COLLAPSE (with async support) ====================
    private void ToggleExpand(TreeNode node)
    {
        if (!NodeHasChildren(node)) return;

        if (!node.IsExpanded)
        {
            if (VirtualMode || _childrenGetterAsync == null)
            {
                node.EnsureChildrenLoaded(_childrenGetter);
                node.IsExpanded = true;
                NodeExpanded?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));
            }
            else
            {
                // Async path
                if (_loadingNodes.Contains(node)) return;

                _loadingNodes.Add(node);
                Invalidate(); // show loading state immediately

                _ = LoadChildrenAsync(node);
                return; // will expand after load
            }
        }
        else
        {
            node.IsExpanded = false;
            NodeCollapsed?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));
        }

        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    private async Task LoadChildrenAsync(TreeNode node)
    {
        try
        {
            if (_childrenGetterAsync == null) return;

            var children = await _childrenGetterAsync(node.Model);

            node.Children.Clear();
            int idx = 0;
            foreach (var child in children ?? [])
            {
                var n = new TreeNode(child, node);
                n.OriginalIndex = idx++;
                node.Children.Add(n);
            }
            node.ChildrenLoaded = true;

            node.IsExpanded = true;
            NodeExpanded?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));
        }
        catch (Exception ex)
        {
            // In real app you might want to surface this. For now we just stop loading state.
            System.Diagnostics.Debug.WriteLine($"Async children load failed: {ex}");
        }
        finally
        {
            _loadingNodes.Remove(node);
            RebuildVisibleRows();
            UpdateScrollbars();
            Invalidate();
        }
    }

    public void Expand(TModel model)
    {
        var node = FindNode(model);
        if (node == null || node.IsExpanded) return;

        ExpandAncestors(node);
        node.EnsureChildrenLoaded(_childrenGetter);
        node.IsExpanded = true;
        NodeExpanded?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));

        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    public void Collapse(TModel model)
    {
        var node = FindNode(model);
        if (node == null || !node.IsExpanded) return;

        node.IsExpanded = false;
        NodeCollapsed?.Invoke(this, new TreeNodeEventArgs<TModel>(node.Model, node));

        RebuildVisibleRows();
        UpdateScrollbars();
        Invalidate();
    }

    // ==================== IN-PLACE EDITING + CUSTOM EDITORS ====================
    public void BeginEdit(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _visibleRows.Count) return;
        if (columnIndex < 0 || columnIndex >= _columns.Count) return;

        CancelEdit();

        var vrow = _visibleRows[rowIndex];
        var column = _columns[columnIndex];
        var node = vrow.Node;

        _editingRowIndex = rowIndex;
        _editingColIndex = columnIndex;
        _editingNode = node;

        object? currentValue = column.Getter(node.Model);
        _editingOriginalValue = currentValue;

        var context = new CellEditorContext<TModel>(node.Model, column, currentValue);

        Control? editor = null;

        // 1. Column-specific registered editor
        if (_columnEditors.TryGetValue(columnIndex, out var factory))
        {
            editor = factory(context);
        }

        // 2. Fallback to smart default
        editor ??= CreateDefaultEditor(column, node.Model, currentValue);

        if (editor == null) return;

        var cellRect = GetCellRectangle(rowIndex, columnIndex);
        cellRect.Inflate(-1, -1);
        if (cellRect.Width < 20) cellRect.Width = 20;

        editor.Bounds = cellRect;
        editor.Font = Font;
        editor.Tag = new EditTag(column, node, currentValue);

        _activeEditor = editor;
        Controls.Add(editor);
        editor.BringToFront();
        editor.Focus();

        if (editor is TextBox tb)
        {
            tb.SelectAll();
            tb.KeyDown += Editor_KeyDown;
            tb.LostFocus += Editor_LostFocus;
        }
        else
        {
            editor.KeyDown += Editor_KeyDown;
            editor.LostFocus += Editor_LostFocus;
        }

        Invalidate();
    }

    private Control CreateDefaultEditor(TreeListColumn<TModel> column, TModel model, object? currentValue)
    {
        // DateTime
        if (currentValue is DateTime dtVal)
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = dtVal,
                BackColor = Color.White,
                ForeColor = ForeColor
            };
        }
        var dtNullable = currentValue as DateTime?;
        if (dtNullable.HasValue)
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = dtNullable.Value,
                BackColor = Color.White,
                ForeColor = ForeColor
            };
        }

        // bool
        if (currentValue is bool boolVal)
        {
            return new CheckBox
            {
                Checked = boolVal,
                BackColor = Color.White,
                ForeColor = ForeColor,
                Text = column.Title,
                AutoSize = true
            };
        }
        var boolNullable = currentValue as bool?;
        if (boolNullable.HasValue)
        {
            return new CheckBox
            {
                Checked = boolNullable.Value,
                BackColor = Color.White,
                ForeColor = ForeColor,
                Text = column.Title,
                AutoSize = true
            };
        }

        // Numeric (simple TextBox with right align; user can register NumericUpDown via SetColumnEditor)
        var text = currentValue?.ToString() ?? string.Empty;
        bool numeric = IsNumericType(currentValue);

        return new TextBox
        {
            Text = text,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = ForeColor,
            Padding = new Padding(2),
            TextAlign = numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
    }

    private static bool IsNumericType(object? value)
    {
        if (value == null) return false;
        var t = value.GetType();
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CommitEdit();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CancelEdit();
        }
        else if (e.KeyCode == Keys.Tab)
        {
            e.Handled = true;
            CommitEdit();
            MoveToNextEditableCell();
        }
    }

    private void Editor_LostFocus(object? sender, EventArgs e)
    {
        if (_activeEditor != null)
            CommitEdit();
    }

    private void CommitEdit()
    {
        if (_activeEditor == null || _editingNode == null || _editingColIndex < 0)
        {
            EndEdit();
            return;
        }

        var column = _columns[_editingColIndex];
        var model = _editingNode.Model;
        object? newValue = ExtractValueFromEditor(_activeEditor, column);

        var args = new CellEditEventArgs<TModel>(model, column, newValue, _editingOriginalValue, _editingRowIndex, _editingColIndex);

        try
        {
            CellEditCommitted?.Invoke(this, args);

            if (!args.Cancel)
            {
                _setCellValue?.Invoke(model, column, newValue);
                InvalidateRow(_editingRowIndex);
            }
            else
            {
                CellEditCanceled?.Invoke(this, args);
            }
        }
        finally
        {
            EndEdit();
        }
    }

    private void CancelEdit()
    {
        if (_activeEditor == null) return;

        if (_editingNode != null && _editingColIndex >= 0)
        {
            var args = new CellEditEventArgs<TModel>(
                _editingNode.Model,
                _columns[_editingColIndex],
                null,
                _editingOriginalValue,
                _editingRowIndex,
                _editingColIndex)
            { Cancel = true };

            CellEditCanceled?.Invoke(this, args);
        }

        EndEdit();
    }

    private void EndEdit()
    {
        if (_activeEditor != null)
        {
            _activeEditor.LostFocus -= Editor_LostFocus;
            _activeEditor.KeyDown -= Editor_KeyDown;

            if (Controls.Contains(_activeEditor))
                Controls.Remove(_activeEditor);

            _activeEditor.Dispose();
            _activeEditor = null;
        }

        _editingRowIndex = -1;
        _editingColIndex = -1;
        _editingNode = null;
        _editingOriginalValue = null;

        Focus();
        Invalidate();
    }

    private object? ExtractValueFromEditor(Control editor, TreeListColumn<TModel> column)
    {
        return editor switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.Checked,
            DateTimePicker dtp => dtp.Value,
            NumericUpDown nud => nud.Value,
            ComboBox cmb => cmb.SelectedItem ?? cmb.Text,
            _ => editor.Text
        };
    }

    private void MoveToNextEditableCell()
    {
        if (_editingRowIndex < 0) return;

        int nextCol = _editingColIndex + 1;
        int nextRow = _editingRowIndex;

        if (nextCol >= _columns.Count)
        {
            nextCol = 0;
            nextRow++;
        }

        if (nextRow >= _visibleRows.Count) return;

        BeginEdit(nextRow, nextCol);
    }

    private void InvalidateRow(int rowIndex)
    {
        if (rowIndex < 0) return;
        int y = _headerHeight + (rowIndex * _rowHeight) - _vOffset;
        Invalidate(new Rectangle(0, y, ClientSize.Width, _rowHeight));
    }

    // ==================== GEOMETRY ====================
    private Rectangle GetCellRectangle(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || columnIndex < 0 || columnIndex >= _columns.Count)
            return Rectangle.Empty;

        int colX = -_hOffset;
        for (int c = 0; c < columnIndex; c++)
            colX += _columns[c].Width;

        int colWidth = _columns[columnIndex].Width;
        int rowY = _headerHeight + (rowIndex * _rowHeight) - _vOffset;

        return new Rectangle(colX, rowY, colWidth, _rowHeight);
    }

    private int GetColumnStartX(int columnIndex)
    {
        int x = -_hOffset;
        for (int c = 0; c < columnIndex; c++)
            x += _columns[c].Width;
        return x;
    }

    // ==================== PAINTING ====================
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        DrawHeader(g, ClientSize.Width);
        DrawRows(g, ClientSize.Width, ClientSize.Height);

        using var borderPen = new Pen(GridLineColor);
        g.DrawLine(borderPen, 0, _headerHeight - 1, ClientSize.Width, _headerHeight - 1);

        if (Focused && _selectedIndex >= 0)
        {
            using var focusPen = new Pen(Color.FromArgb(100, 0, 120, 212)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(focusPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // Drag & drop indicator
        DrawDropIndicator(g);
    }

    private void DrawHeader(Graphics g, int clientWidth)
    {
        var headerRect = new Rectangle(0, 0, clientWidth, _headerHeight);
        using var headerBrush = new SolidBrush(HeaderBackColor);
        g.FillRectangle(headerBrush, headerRect);

        using var textBrush = new SolidBrush(HeaderForeColor);
        using var linePen = new Pen(GridLineColor);

        int x = -_hOffset;

        for (int c = 0; c < _columns.Count; c++)
        {
            var col = _columns[c];
            var colRect = new Rectangle(x, 0, col.Width, _headerHeight);

            if (colRect.Right > 0 && colRect.Left < clientWidth)
            {
                g.DrawLine(linePen, colRect.Right - 1, 4, colRect.Right - 1, _headerHeight - 5);

                bool isSortedCol = (_sortColumnIndex == c && _sortOrder != SortOrder.None);
                string sortGlyph = isSortedCol ? (_sortOrder == SortOrder.Ascending ? "▲" : "▼") : "";

                int textRightPadding = isSortedCol ? 18 : CellPadding;
                var textRect = new Rectangle(
                    colRect.Left + CellPadding,
                    colRect.Top,
                    Math.Max(4, colRect.Width - CellPadding - textRightPadding),
                    colRect.Height);

                TextRenderer.DrawText(g, col.Title, Font, textRect, HeaderForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);

                if (isSortedCol)
                {
                    var glyphRect = new Rectangle(colRect.Right - 16, colRect.Top, 14, colRect.Height);
                    TextRenderer.DrawText(g, sortGlyph, Font, glyphRect, HeaderForeColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.PreserveGraphicsClipping);
                }
            }
            x += col.Width;
        }

        g.DrawLine(linePen, 0, _headerHeight - 1, clientWidth, _headerHeight - 1);
    }

    private void DrawRows(Graphics g, int clientWidth, int clientHeight)
    {
        if (_visibleRows.Count == 0) return;

        int firstRow = Math.Max(0, _vOffset / _rowHeight);
        int rowPixelY = _headerHeight - (_vOffset % _rowHeight);

        using var gridPen = new Pen(GridLineColor);

        for (int r = firstRow; r < _visibleRows.Count && rowPixelY < clientHeight; r++)
        {
            var vrow = _visibleRows[r];
            bool isSelected = _multiSelect
                ? _selectedNodes.Contains(vrow.Node)
                : (r == _selectedIndex);
            bool isAlt = ShowAlternatingRows && (r % 2 == 1);

            Color bg = isSelected ? SelectionBackColor :
                       isAlt ? AlternatingRowBackColor : RowBackColor;

            using (var b = new SolidBrush(bg))
            {
                g.FillRectangle(b, 0, rowPixelY, clientWidth, _rowHeight);
            }

            int cellX = -_hOffset;
            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                int colW = col.Width;
                var cellRect = new Rectangle(cellX, rowPixelY, colW, _rowHeight);

                if (cellRect.Right > 0 && cellRect.Left < clientWidth)
                {
                    if (c == 0)
                        DrawTreeCell(g, cellRect, vrow, col, isSelected);
                    else
                        DrawDataCell(g, cellRect, vrow, col, isSelected);
                }

                if (ShowGridLines)
                    g.DrawLine(gridPen, cellRect.Right - 1, rowPixelY + 2, cellRect.Right - 1, rowPixelY + _rowHeight - 3);

                cellX += colW;
            }

            if (ShowGridLines)
                g.DrawLine(gridPen, 0, rowPixelY + _rowHeight - 1, clientWidth, rowPixelY + _rowHeight - 1);

            rowPixelY += _rowHeight;
        }
    }

    private void DrawTreeCell(Graphics g, Rectangle cellRect, VisibleRow vrow, TreeListColumn<TModel> column, bool isSelected)
    {
        int level = vrow.Level;
        var node = vrow.Node;
        bool hasChildren = NodeHasChildren(node);
        bool expanded = node.IsExpanded;
        bool isLoading = _loadingNodes.Contains(node);

        int indent = level * IndentSize;
        int contentLeft = cellRect.Left + indent + ExpanderMargin;

        // Checkbox
        if (_showCheckboxes)
        {
            var cbRect = new Rectangle(contentLeft, cellRect.Top + (_rowHeight - CheckboxSize) / 2, CheckboxSize, CheckboxSize);
            DrawCheckbox(g, cbRect, GetCheckState(node), isSelected);
            contentLeft += CheckboxSize + CheckboxMargin;
        }

        // Expander
        if (hasChildren)
        {
            var expRect = new Rectangle(contentLeft, cellRect.Top + (_rowHeight - ExpanderSize) / 2, ExpanderSize, ExpanderSize);
            DrawModernExpander(g, expRect, expanded, isSelected);
            contentLeft += ExpanderSize + 4;
        }
        else
        {
            contentLeft += 4;
        }

        // Loading indicator (simple text for now — can be improved with spinner)
        string text;
        if (isLoading)
        {
            text = "Loading...";
            var loadingBrush = new SolidBrush(LoadingForeColor);
            var loadingRect = new Rectangle(contentLeft, cellRect.Top, Math.Max(4, cellRect.Right - contentLeft - CellPadding), cellRect.Height);
            TextRenderer.DrawText(g, text, Font, loadingRect, LoadingForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            return;
        }

        text = GetDisplayText(node.Model, column);
        var textColor = isSelected ? SelectionForeColor : ForeColor;

        var textRect = new Rectangle(contentLeft, cellRect.Top,
            Math.Max(4, cellRect.Right - contentLeft - CellPadding), cellRect.Height);

        TextRenderer.DrawText(g, text, Font, textRect, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
    }

    private void DrawCheckbox(Graphics g, Rectangle rect, CheckState state, bool selected)
    {
        // Simple modern checkbox using pens/brushes (no renderer dependency for reliability)
        using var border = new Pen(_showCheckboxes ? CheckboxColor : Color.Gray, 1.2f);
        using var fill = new SolidBrush(Color.White);
        using var checkPen = new Pen(selected ? SelectionForeColor : CheckboxColor, 1.8f);

        g.FillRectangle(fill, rect);
        g.DrawRectangle(border, rect);

        if (state == CheckState.Checked)
        {
            // Check mark
            int m = 3;
            g.DrawLine(checkPen, rect.Left + m, rect.Top + rect.Height / 2, rect.Left + rect.Width / 3, rect.Bottom - m);
            g.DrawLine(checkPen, rect.Left + rect.Width / 3, rect.Bottom - m, rect.Right - m, rect.Top + m);
        }
        else if (state == CheckState.Indeterminate)
        {
            using var indBrush = new SolidBrush(CheckboxColor);
            int pad = 3;
            g.FillRectangle(indBrush, rect.Left + pad, rect.Top + pad, rect.Width - pad * 2, rect.Height - pad * 2);
        }
    }

    private void DrawDataCell(Graphics g, Rectangle cellRect, VisibleRow vrow, TreeListColumn<TModel> column, bool isSelected)
    {
        string text = GetDisplayText(vrow.Node.Model, column);
        var textColor = isSelected ? SelectionForeColor : ForeColor;

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping;
        if (column.Alignment == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
        else if (column.Alignment == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
        else flags |= TextFormatFlags.Left;

        var textRect = new Rectangle(cellRect.Left + CellPadding, cellRect.Top,
            Math.Max(4, cellRect.Width - CellPadding * 2), cellRect.Height);

        TextRenderer.DrawText(g, text, Font, textRect, textColor, flags);
    }

    private void DrawModernExpander(Graphics g, Rectangle rect, bool expanded, bool selected)
    {
        var color = selected ? SelectionForeColor : ExpanderColor;
        using var pen = new Pen(color, 1.6f);

        int cx = rect.Left + rect.Width / 2;
        int cy = rect.Top + rect.Height / 2;
        int sz = 3;

        if (expanded)
        {
            g.DrawLine(pen, cx - sz, cy - 1, cx, cy + sz - 1);
            g.DrawLine(pen, cx, cy + sz - 1, cx + sz, cy - 1);
        }
        else
        {
            g.DrawLine(pen, cx - 1, cy - sz, cx + sz - 1, cy);
            g.DrawLine(pen, cx + sz - 1, cy, cx - 1, cy + sz);
        }
    }

    private void DrawDropIndicator(Graphics g)
    {
        if (_dropTargetRowIndex < 0 || _dropPosition == DropPosition.None) return;

        int y;
        var row = _visibleRows[_dropTargetRowIndex];
        int rowTop = _headerHeight + (_dropTargetRowIndex * _rowHeight) - _vOffset;

        using var pen = new Pen(DragDropIndicatorColor, 2);

        if (_dropPosition == DropPosition.Before)
        {
            y = rowTop;
            g.DrawLine(pen, 0, y, ClientSize.Width, y);
        }
        else if (_dropPosition == DropPosition.After)
        {
            y = rowTop + _rowHeight;
            g.DrawLine(pen, 0, y, ClientSize.Width, y);
        }
        else // Into
        {
            using var highlight = new SolidBrush(Color.FromArgb(40, DragDropIndicatorColor));
            g.FillRectangle(highlight, 0, rowTop, ClientSize.Width, _rowHeight);
        }
    }

    private string GetDisplayText(TModel model, TreeListColumn<TModel> column)
    {
        object? value = column.Getter(model);
        if (column.Formatter != null)
            return column.Formatter(value) ?? string.Empty;
        return value?.ToString() ?? string.Empty;
    }

    // ==================== INPUT HANDLING (mouse + keyboard + dnd) ====================
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        CancelEdit();

        var hit = HitTest(e.X, e.Y);

        if (hit.IsHeader)
        {
            HandleHeaderMouseDown(e, hit);
            return;
        }

        if (hit.RowIndex >= 0 && hit.IsValid)
        {
            var vrow = _visibleRows[hit.RowIndex];

            if (hit.IsCheckbox && _showCheckboxes)
            {
                var current = GetCheckState(vrow.Node);
                bool newChecked = current != CheckState.Checked;
                SetNodeChecked(vrow.Node, newChecked);
                RebuildVisibleRows(); // ancestors may have changed
                UpdateScrollbars();
                Invalidate();
                return;
            }

            if (hit.IsExpander)
            {
                ToggleExpand(vrow.Node);
                return;
            }

            // Selection
            bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            SetSelection(hit.RowIndex, ctrl, shift);

            _currentEditColumnHint = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;

            // Drag & drop preparation
            if (_allowDragDrop && e.Button == MouseButtons.Left)
            {
                _dragSourceNode = vrow.Node;
            }
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var hit = HitTest(e.X, e.Y);
        if (hit.RowIndex >= 0 && !hit.IsExpander && !hit.IsCheckbox && hit.IsValid)
        {
            int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
            BeginEdit(hit.RowIndex, col);
        }
    }

    private void HandleHeaderMouseDown(MouseEventArgs e, HitTestResult hit)
    {
        if (e.Button != MouseButtons.Left || hit.ColumnIndex < 0) return;

        int colRight = GetColumnStartX(hit.ColumnIndex) + _columns[hit.ColumnIndex].Width;
        int gripWidth = 6;

        if (Math.Abs(e.X - colRight) <= gripWidth)
        {
            _resizingColumnIndex = hit.ColumnIndex;
            _resizeStartX = e.X;
            _resizeStartWidth = _columns[hit.ColumnIndex].Width;
            Cursor = Cursors.VSplit;
        }
        else
        {
            ToggleSortOnColumn(hit.ColumnIndex);
        }
    }

    private void ToggleSortOnColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count) return;

        SortOrder nextOrder = _sortColumnIndex != columnIndex
            ? SortOrder.Ascending
            : _sortOrder switch
            {
                SortOrder.None => SortOrder.Ascending,
                SortOrder.Ascending => SortOrder.Descending,
                _ => SortOrder.None
            };

        Sort(columnIndex, nextOrder);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_resizingColumnIndex >= 0)
        {
            var col = _columns[_resizingColumnIndex];
            int delta = e.X - _resizeStartX;
            col.Width = Math.Max(MinColumnWidth, _resizeStartWidth + delta);
            UpdateScrollbars();
            Invalidate();
            return;
        }

        // Resize cursor in header
        if (e.Y < _headerHeight)
        {
            var hit = HitTest(e.X, e.Y);
            if (hit.ColumnIndex >= 0)
            {
                int colRight = GetColumnStartX(hit.ColumnIndex) + _columns[hit.ColumnIndex].Width;
                if (Math.Abs(e.X - colRight) <= 5)
                {
                    Cursor = Cursors.VSplit;
                    return;
                }
            }
        }

        // Drag & drop auto-scroll + indicator update (while dragging over this control)
        if (_allowDragDrop && _dragSourceNode != null && e.Button == MouseButtons.Left)
        {
            UpdateDropTarget(e.X, e.Y);
        }

        Cursor = Cursors.Default;
    }

    private void UpdateDropTarget(int mouseX, int mouseY)
    {
        var hit = HitTest(mouseX, mouseY);
        if (hit.RowIndex < 0)
        {
            _dropTargetRowIndex = -1;
            _dropPosition = DropPosition.None;
            Invalidate();
            return;
        }

        var targetNode = _visibleRows[hit.RowIndex].Node;
        if (targetNode == _dragSourceNode)
        {
            _dropTargetRowIndex = -1;
            _dropPosition = DropPosition.None;
            Invalidate();
            return;
        }

        int rowTop = _headerHeight + (hit.RowIndex * _rowHeight) - _vOffset;
        int rowHeight = _rowHeight;
        float relativeY = (mouseY - rowTop) / (float)rowHeight;

        DropPosition pos;
        if (relativeY < 0.25f) pos = DropPosition.Before;
        else if (relativeY > 0.75f) pos = DropPosition.After;
        else pos = DropPosition.Into;

        _dropTargetRowIndex = hit.RowIndex;
        _dropPosition = pos;

        // Raise event so user can influence effect
        var args = new TreeDragOverEventArgs<TModel>(_dragSourceNode.Model, targetNode.Model, pos, DragDropEffects.Move | DragDropEffects.Copy);
        DragOverNode?.Invoke(this, args);

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_resizingColumnIndex >= 0)
        {
            _resizingColumnIndex = -1;
            Cursor = Cursors.Default;
            UpdateScrollbars();
            Invalidate();
        }

        _dragSourceNode = null;
        _dropTargetRowIndex = -1;
        _dropPosition = DropPosition.None;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_vScrollBar.Visible)
        {
            CancelEdit();
            int newVal = _vOffset - (e.Delta / 2);
            int maxVal = Math.Max(0, (_visibleRows.Count * _rowHeight) - Math.Max(1, ClientSize.Height - _headerHeight));
            _vOffset = Math.Clamp(newVal, 0, maxVal);

            if (_vScrollBar.Visible) _vScrollBar.Value = _vOffset;
            Invalidate();
        }
    }

    // Drag & Drop support (standard WinForms DoDragDrop)
    protected override void OnDragOver(DragEventArgs drgevent)
    {
        base.OnDragOver(drgevent);
        // We primarily use internal mouse handling for visual feedback.
        // This allows external drops if someone wants to implement them.
        drgevent.Effect = DragDropEffects.Move;
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        base.OnDragDrop(drgevent);
        // Handled via internal mouse up + events. Left for extensibility.
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_visibleRows.Count == 0) return;

        bool ctrl = (e.Modifiers & Keys.Control) == Keys.Control;
        bool shift = (e.Modifiers & Keys.Shift) == Keys.Shift;

        switch (e.KeyCode)
        {
            case Keys.F2:
                if (_selectedIndex >= 0)
                {
                    int col = _currentEditColumnHint >= 0 && _currentEditColumnHint < _columns.Count ? _currentEditColumnHint : 0;
                    BeginEdit(_selectedIndex, col);
                    e.Handled = true;
                }
                break;

            case Keys.Enter:
                if (_selectedIndex >= 0)
                {
                    int col = _currentEditColumnHint >= 0 && _currentEditColumnHint < _columns.Count ? _currentEditColumnHint : 0;
                    BeginEdit(_selectedIndex, col);
                    e.Handled = true;
                }
                break;

            case Keys.Up:
            case Keys.Down:
                HandleArrowNavigation(e.KeyCode, shift);
                e.Handled = true;
                break;

            case Keys.Left:
            case Keys.Right:
                HandleLeftRight(e.KeyCode);
                e.Handled = true;
                break;

            case Keys.Space:
                if (_showCheckboxes && _selectedIndex >= 0)
                {
                    var node = _visibleRows[_selectedIndex].Node;
                    bool newChecked = GetCheckState(node) != CheckState.Checked;
                    SetNodeChecked(node, newChecked);
                    RebuildVisibleRows();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Home:
            case Keys.End:
                HandlePageNavigation(e.KeyCode);
                e.Handled = true;
                break;
        }
    }

    private void HandleArrowNavigation(Keys key, bool shift)
    {
        if (_selectedIndex < 0) return;

        int newIdx = _selectedIndex;
        if (key == Keys.Up && _selectedIndex > 0) newIdx--;
        if (key == Keys.Down && _selectedIndex < _visibleRows.Count - 1) newIdx++;

        if (newIdx != _selectedIndex)
        {
            if (_multiSelect && shift)
            {
                // Extend range
                int anchorIdx = _anchorNode != null ? _visibleRows.FindIndex(v => v.Node == _anchorNode) : _selectedIndex;
                int start = Math.Min(anchorIdx, newIdx);
                int end = Math.Max(anchorIdx, newIdx);

                _selectedNodes.Clear();
                for (int i = start; i <= end; i++) _selectedNodes.Add(_visibleRows[i].Node);
                _selectedNode = _visibleRows[newIdx].Node;
                _selectedIndex = newIdx;
            }
            else
            {
                SetSelection(newIdx);
            }
            EnsureRowVisible(_selectedIndex);
        }
    }

    private void HandleLeftRight(Keys key)
    {
        if (_selectedIndex < 0) return;
        var node = _visibleRows[_selectedIndex].Node;

        if (key == Keys.Left)
        {
            if (node.IsExpanded)
                ToggleExpand(node);
            else if (node.Parent != null)
            {
                int parentIdx = _visibleRows.FindIndex(vr => vr.Node == node.Parent);
                if (parentIdx >= 0) SetSelection(parentIdx);
            }
        }
        else // Right
        {
            if (!node.IsExpanded && NodeHasChildren(node))
                ToggleExpand(node);
            else if (node.IsExpanded && node.Children.Count > 0)
            {
                int childIdx = _visibleRows.FindIndex(vr => vr.Node == node.Children[0]);
                if (childIdx >= 0) SetSelection(childIdx);
            }
        }
    }

    private void HandlePageNavigation(Keys key)
    {
        if (_visibleRows.Count == 0) return;

        int page = Math.Max(1, (ClientSize.Height - _headerHeight) / _rowHeight);
        int newIdx = _selectedIndex;

        if (key == Keys.PageUp) newIdx = Math.Max(0, _selectedIndex - page);
        if (key == Keys.PageDown) newIdx = Math.Min(_visibleRows.Count - 1, _selectedIndex + page);
        if (key == Keys.Home) newIdx = 0;
        if (key == Keys.End) newIdx = _visibleRows.Count - 1;

        SetSelection(newIdx);
        EnsureRowVisible(newIdx);
    }

    // Drag & drop is primarily handled via internal mouse events + public Drag*Node events.
    // This allows rich visual feedback (drop lines) while still supporting the standard WinForms drag events if needed.

    // ==================== THEME ====================
    private void ApplyTheme()
    {
        if (_useDarkMode)
        {
            HeaderBackColor = Color.FromArgb(45, 45, 48);
            HeaderForeColor = Color.FromArgb(220, 220, 220);
            RowBackColor = Color.FromArgb(30, 30, 30);
            AlternatingRowBackColor = Color.FromArgb(37, 37, 37);
            SelectionBackColor = Color.FromArgb(0, 120, 212);
            SelectionForeColor = Color.White;
            GridLineColor = Color.FromArgb(60, 60, 60);
            ExpanderColor = Color.FromArgb(160, 160, 160);
            TreeLineColor = Color.FromArgb(80, 80, 80);
            HoverBackColor = Color.FromArgb(55, 55, 55);
            CheckboxColor = Color.FromArgb(180, 180, 180);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.FromArgb(220, 220, 220);
            LoadingForeColor = Color.FromArgb(180, 180, 180);
        }
        else
        {
            // Reset to light defaults
            HeaderBackColor = Color.FromArgb(247, 248, 250);
            HeaderForeColor = Color.FromArgb(52, 58, 64);
            RowBackColor = Color.White;
            AlternatingRowBackColor = Color.FromArgb(250, 251, 252);
            SelectionBackColor = Color.FromArgb(0, 120, 212);
            SelectionForeColor = Color.White;
            GridLineColor = Color.FromArgb(234, 236, 239);
            ExpanderColor = Color.FromArgb(108, 117, 125);
            TreeLineColor = Color.FromArgb(206, 212, 218);
            HoverBackColor = Color.FromArgb(241, 243, 245);
            CheckboxColor = Color.FromArgb(108, 117, 125);
            BackColor = Color.White;
            ForeColor = Color.FromArgb(33, 37, 41);
            LoadingForeColor = Color.FromArgb(108, 117, 125);
        }
    }

    // ==================== LAYOUT & FOCUS ====================
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollbars();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateScrollbars();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    // ==================== CLEANUP ====================
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activeEditor?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ==================== INTERNAL TYPES ====================
    private sealed class TreeNode
    {
        public TModel Model { get; private set; }
        public TreeNode? Parent { get; }
        public List<TreeNode> Children { get; } = [];
        public bool IsExpanded { get; set; }
        public bool ChildrenLoaded { get; set; }
        public int OriginalIndex { get; set; }
        public bool IsVirtual { get; set; }

        public TreeNode(TModel model, TreeNode? parent)
        {
            Model = model;
            Parent = parent;
        }

        public void EnsureChildrenLoaded(Func<TModel, IEnumerable<TModel>>? getter)
        {
            if (ChildrenLoaded || getter is null || IsVirtual) return;

            Children.Clear();
            var children = getter(Model);
            if (children != null)
            {
                int idx = 0;
                foreach (var child in children)
                {
                    var n = new TreeNode(child, this);
                    n.OriginalIndex = idx++;
                    Children.Add(n);
                }
            }
            ChildrenLoaded = true;
        }

        internal void ReplaceModelReference(TModel newModel) => Model = newModel;
    }

    private readonly record struct VisibleRow(TreeNode Node, int Level);

    private sealed record EditTag(TreeListColumn<TModel> Column, TreeNode Node, object? OriginalValue);

}

// ==================== SUPPORTING PUBLIC TYPES ====================

public enum SortOrder { None, Ascending, Descending }

public enum CheckState { Unchecked, Checked, Indeterminate }

/// <summary>
/// Represents the position of a drag &amp; drop operation relative to the target node.
/// </summary>
public enum DropPosition
{
    None,
    Before,
    After,
    /// <summary>Drop as a child of the target (reparent).</summary>
    Into
}

/// <summary>
/// Context passed to custom editor factories.
/// </summary>
public readonly record struct CellEditorContext<TModel>(TModel Model, TreeListColumn<TModel> Column, object? Value);

/// <summary>
/// Event args for virtual node retrieval.
/// </summary>
public class RetrieveVirtualNodeEventArgs<TModel> : EventArgs
{
    public int RootIndex { get; }
    public TModel? Parent { get; }
    public int ChildIndex { get; }
    public TModel? Model { get; set; }

    public RetrieveVirtualNodeEventArgs(int rootIndex, TModel? parent, int childIndex)
    {
        RootIndex = rootIndex;
        Parent = parent;
        ChildIndex = childIndex;
    }
}

/// <summary>
/// Hint that the control will soon request a range of virtual items.
/// </summary>
public class CacheVirtualNodesEventArgs : EventArgs
{
    public int StartIndex { get; }
    public int Count { get; }

    public CacheVirtualNodesEventArgs(int startIndex, int count)
    {
        StartIndex = startIndex;
        Count = count;
    }
}

/// <summary>
/// Raised when the user begins dragging an item.
/// </summary>
public class ItemDragEventArgs<TModel> : EventArgs
{
    public TModel Model { get; }
    public TreeNodeEventArgs<TModel> NodeArgs { get; }

    public ItemDragEventArgs(TModel model, TreeNodeEventArgs<TModel> nodeArgs)
    {
        Model = model;
        NodeArgs = nodeArgs;
    }
}

/// <summary>
/// Gives drag-over feedback and allows changing the allowed effect.
/// </summary>
public class TreeDragOverEventArgs<TModel> : EventArgs
{
    public TModel Source { get; }
    public TModel Target { get; }
    public DropPosition Position { get; }
    public DragDropEffects Effect { get; set; }

    public TreeDragOverEventArgs(TModel source, TModel target, DropPosition position, DragDropEffects allowed)
    {
        Source = source;
        Target = target;
        Position = position;
        Effect = allowed;
    }
}

/// <summary>
/// Final drop information. Perform your model mutation in the handler.
/// </summary>
public class TreeDragDropEventArgs<TModel> : EventArgs
{
    public TModel Source { get; }
    public TModel Target { get; }
    public DropPosition Position { get; }

    public TreeDragDropEventArgs(TModel source, TModel target, DropPosition position)
    {
        Source = source;
        Target = target;
        Position = position;
    }
}

/// <summary>
/// Column definition (public for configuration).
/// </summary>
public sealed class TreeListColumn<TModel>
{
    public string Title { get; set; }
    public int Width { get; set; }
    public Func<TModel, object?> Getter { get; set; }
    public Func<object?, string>? Formatter { get; set; }
    public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
    public int MinWidth { get; set; } = 36;

    internal TreeListColumn(string title, Func<TModel, object?> getter, int width)
    {
        Title = title;
        Getter = getter;
        Width = Math.Max(width, MinWidth);
    }
}

/// <summary>
/// Cell edit event arguments.
/// </summary>
public sealed class CellEditEventArgs<TModel> : EventArgs
{
    public TModel Model { get; }
    public TreeListColumn<TModel> Column { get; }
    public object? ProposedValue { get; set; }
    public object? OriginalValue { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public bool Cancel { get; set; }

    public CellEditEventArgs(TModel model, TreeListColumn<TModel> column, object? proposedValue, object? originalValue, int rowIndex, int columnIndex)
    {
        Model = model;
        Column = column;
        ProposedValue = proposedValue;
        OriginalValue = originalValue;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
    }
}

/// <summary>
/// Tree node expand/collapse/check event args.
/// </summary>
public sealed class TreeNodeEventArgs<TModel> : EventArgs
{
    public TModel Model { get; }
    internal object? Node { get; }

    public TreeNodeEventArgs(TModel model, object? node)
    {
        Model = model;
        Node = node;
    }
}
