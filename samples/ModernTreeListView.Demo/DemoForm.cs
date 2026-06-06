using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using ModernTreeListView;

namespace ModernTreeListView.Demo;

/// <summary>
/// Demo models and main form for the ModernTreeListView sample application.
/// </summary>

// ==================== DEMO MODELS ====================

/// <summary>
/// Lightweight record used for both normal and virtual demos.
/// </summary>
public record OrgItem(
    string Id,
    string Name,
    string Type,
    string? Role = null,
    DateTime? LastActive = null,
    int Headcount = 0,
    List<OrgItem>? Children = null)
{
    public List<OrgItem> Children { get; init; } = Children ?? [];
}

/// <summary>
/// Used for virtual mode demo — synthesized on demand, no large memory footprint.
/// </summary>
public record VirtualItem(long Id, string Name, string Type, bool IsFolder);

public sealed class DemoForm : Form
{
    private readonly ModernTreeListView<OrgItem> _treeList;
    private readonly Label _statusLabel;
    private readonly TextBox _filterBox;

    private List<OrgItem> _data = [];

    // Virtual mode data (synthetic)
    private bool _isVirtualMode;
    private const int VirtualRootCount = 12;

    public DemoForm()
    {
        Text = "ModernTreeListView — Advanced Production Demo (.NET 8+)";
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        _data = CreateSampleData();

        // ==================== CONTROL ====================
        _treeList = new ModernTreeListView<OrgItem>
        {
            Dock = DockStyle.Fill,
            RowHeight = 28,
            HeaderHeight = 36,
            ShowAlternatingRows = true,
            ShowGridLines = false,
            SelectionBackColor = Color.FromArgb(0, 120, 212),
            AllowDragDrop = false,
            MultiSelect = false,
            ShowCheckboxes = false
        };

        // Columns
        _treeList
            .AddColumn("Name", m => m.Name, width: 320, configure: c =>
            {
                c.Formatter = v => v?.ToString() ?? "";
            })
            .AddColumn("Type", m => m.Type, width: 110)
            .AddColumn("Role / Details", m => m.Role ?? (m.Headcount > 0 ? $"{m.Headcount} people" : ""), width: 170)
            .AddColumn("Last Active", m => m.LastActive, width: 150, configure: c =>
            {
                c.Formatter = v => v is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm") : "";
                c.Alignment = HorizontalAlignment.Right;
            });

        // Core binding (normal mode)
        ConfigureNormalMode();

        // ==================== EDITORS (Custom per column) ====================
        ConfigureEditors();

        // ==================== UI ====================
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.FromArgb(247, 248, 250),
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Ready. Use the toolbar to explore Virtual Mode (100k nodes), Checkboxes, Drag & Drop, Editors, Filtering, Multi-Select, Async, and Dark Mode."
        };

        _filterBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 28,
            PlaceholderText = "Type to filter (searches all columns)..."
        };
        _filterBox.TextChanged += (_, _) =>
        {
            _treeList.SetFilterText(_filterBox.Text);
        };

        var toolStrip = CreateToolStrip();
        var container = new Panel { Dock = DockStyle.Fill };
        container.Controls.Add(_treeList);

        Controls.Add(container);
        Controls.Add(_filterBox);
        Controls.Add(toolStrip);
        Controls.Add(_statusLabel);

        // Initial selection
        if (_data.Count > 0)
            _treeList.SelectModel(_data[0]);

        // Events
        WireEvents();

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                _treeList.Rebuild();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Escape && _filterBox.Focused)
            {
                _filterBox.Text = "";
                _treeList.Focus();
            }
        };
    }

    private void ConfigureNormalMode()
    {
        _isVirtualMode = false;
        _treeList.VirtualMode = false;

        _treeList
            .SetRoots(_data)
            .SetChildrenGetter(item => item.Children)
            .SetHasChildrenGetter(item => item.Children.Count > 0 || item.Type != "Person")
            .SetCellValueSetter((model, column, newValue) =>
            {
                // Immutable record update pattern
                OrgItem? updated = null;

                if (column.Title == "Name" && newValue is string s && !string.IsNullOrWhiteSpace(s))
                    updated = model with { Name = s };
                else if (column.Title == "Role / Details" && newValue is string role)
                    updated = model with { Role = string.IsNullOrWhiteSpace(role) ? null : role };
                else if (column.Title == "Last Active" && DateTime.TryParse(newValue?.ToString(), out var dt))
                    updated = model with { LastActive = dt };

                if (updated != null)
                {
                    ReplaceInData(model, updated);
                    _treeList.ReplaceModel(model, updated);
                }
            });
    }

    private void ConfigureVirtualMode()
    {
        // Virtual mode demo is handled by creating a separate ModernTreeListView<VirtualItem>
        // control on demand (see SwitchToVirtualTree). This method is kept for compatibility
        // with the original demo structure but does no work on the OrgItem tree.
        _isVirtualMode = true;
    }

    private ModernTreeListView<VirtualItem>? _virtualTree;

    private void SwitchToVirtualTree()
    {
        if (_virtualTree != null)
        {
            // Already in virtual mode in the UI
            return;
        }

        // Hide normal tree and create a virtual one for the demo
        _treeList.Visible = false;

        _virtualTree = new ModernTreeListView<VirtualItem>
        {
            Dock = DockStyle.Fill,
            RowHeight = 28,
            HeaderHeight = 36,
            ShowAlternatingRows = true,
            VirtualMode = true,
            VirtualRootCount = 100_000 / 50, // pretend we have many top-level "groups"
            AllowDragDrop = true,
            ShowCheckboxes = true,
            MultiSelect = true
        };

        _virtualTree
            .AddColumn("Name", v => v.Name, 320)
            .AddColumn("Type", v => v.Type, 110)
            .AddColumn("ID", v => v.Id, 120, c => c.Alignment = HorizontalAlignment.Right);

        _virtualTree.SetVirtualRootGetter(i =>
        {
            // 2000 synthetic root groups
            return new VirtualItem(100000 + i, $"Root Group {i + 1}", "Group", true);
        });

        _virtualTree.SetVirtualChildCountGetter(parent =>
        {
            // Each root has between 20 and 60 children
            return 20 + (int)(parent.Id % 41);
        });

        _virtualTree.SetVirtualChildGetter((parent, idx) =>
        {
            long id = parent.Id * 1000 + idx;
            bool folder = (idx % 7) == 0;
            return new VirtualItem(id, folder ? $"Subfolder {idx}" : $"Leaf Item {idx}", folder ? "Folder" : "Item", folder);
        });

        // Nice checkbox + drag drop behavior for virtual
        _virtualTree.NodeCheckStateChanged += (_, e) =>
        {
            UpdateStatus($"Check changed on virtual item: {e.Model.Name}");
        };

        _virtualTree.DragDropNode += (_, e) =>
        {
            UpdateStatus($"[Virtual DnD] Moved {e.Source.Name} → {e.Position} {e.Target.Name}");
            // In real app you would update your backing store / re-query
            _virtualTree.Rebuild();
        };

        // Add the virtual tree to the container
        var container = (Panel)_treeList.Parent!;
        container.Controls.Add(_virtualTree);
        _virtualTree.BringToFront();

        UpdateStatus("Virtual Mode active — 100,000+ nodes (synthesized on demand). Expand nodes, use checkboxes, drag & drop, multi-select.");
    }

    private void ConfigureEditors()
    {
        // Column 1 "Type" → ComboBox editor
        _treeList.SetColumnEditor(1, context =>
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Items = { "Department", "Team", "Person", "Contractor" }
            };
            if (context.Value is string s && cmb.Items.Contains(s))
                cmb.SelectedItem = s;
            else
                cmb.SelectedIndex = 0;
            return cmb;
        });

        // Column 3 "Last Active" → DateTimePicker (already default, but we can force)
        _treeList.SetColumnEditor(3, context =>
        {
            var dtp = new DateTimePicker { Format = DateTimePickerFormat.Short };
            if (context.Value is DateTime dt)
                dtp.Value = dt;
            return dtp;
        });
    }

    private ToolStrip CreateToolStrip()
    {
        var strip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.FromArgb(247, 248, 250)
        };

        strip.Items.Add(new ToolStripLabel("  ModernTreeListView Advanced Demo   ") { Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });

        // === Virtual Mode ===
        var btnVirtual = new ToolStripButton("Virtual Mode (100k nodes)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnVirtual.Click += (_, _) =>
        {
            SwitchToVirtualTree();
        };
        strip.Items.Add(btnVirtual);

        var btnBackToNormal = new ToolStripButton("Back to Normal Data") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnBackToNormal.Click += (_, _) =>
        {
            if (_virtualTree != null)
            {
                _virtualTree.Dispose();
                _virtualTree = null;
            }
            _treeList.Visible = true;
            ConfigureNormalMode();
            _treeList.Rebuild();
            UpdateStatus("Returned to normal (in-memory) mode.");
        };
        strip.Items.Add(btnBackToNormal);

        strip.Items.Add(new ToolStripSeparator());

        // === Checkboxes ===
        var btnCheckboxes = new ToolStripButton("Toggle Checkboxes") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnCheckboxes.Click += (_, _) =>
        {
            _treeList.ShowCheckboxes = !_treeList.ShowCheckboxes;
            UpdateStatus(_treeList.ShowCheckboxes ? "Checkboxes enabled (tri-state supported)" : "Checkboxes disabled");
        };
        strip.Items.Add(btnCheckboxes);

        var btnGetChecked = new ToolStripButton("Show Checked Count") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnGetChecked.Click += (_, _) =>
        {
            var checkedItems = _treeList.GetCheckedItems();
            MessageBox.Show($"Checked items: {checkedItems.Count}\n\nFirst few:\n" +
                string.Join("\n", checkedItems.Take(8).Select(x => "• " + x.Name)),
                "Checked Items");
        };
        strip.Items.Add(btnGetChecked);

        strip.Items.Add(new ToolStripSeparator());

        // === Drag & Drop ===
        var btnDragDrop = new ToolStripButton("Toggle Drag & Drop") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnDragDrop.Click += (_, _) =>
        {
            _treeList.AllowDragDrop = !_treeList.AllowDragDrop;
            UpdateStatus(_treeList.AllowDragDrop ? "Drag & Drop enabled — drag rows to reorder or move into parents" : "Drag & Drop disabled");
        };
        strip.Items.Add(btnDragDrop);

        strip.Items.Add(new ToolStripSeparator());

        // === Multi-Select ===
        var btnMulti = new ToolStripButton("Toggle Multi-Select") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnMulti.Click += (_, _) =>
        {
            _treeList.MultiSelect = !_treeList.MultiSelect;
            UpdateStatus(_treeList.MultiSelect ? "Multi-select enabled (Ctrl/Shift + click or arrows)" : "Multi-select disabled");
        };
        strip.Items.Add(btnMulti);

        var btnSelected = new ToolStripButton("Show Selected") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnSelected.Click += (_, _) =>
        {
            var sel = _treeList.SelectedModels;
            MessageBox.Show($"Selected: {sel.Count}\n" + string.Join("\n", sel.Take(10).Select(m => "• " + m.Name)), "Selection");
        };
        strip.Items.Add(btnSelected);

        strip.Items.Add(new ToolStripSeparator());

        // === Theming ===
        var btnDark = new ToolStripButton("Toggle Dark Mode") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnDark.Click += (_, _) =>
        {
            _treeList.UseDarkMode = !_treeList.UseDarkMode;
            _statusLabel.BackColor = _treeList.UseDarkMode ? Color.FromArgb(45, 45, 48) : Color.FromArgb(247, 248, 250);
            UpdateStatus(_treeList.UseDarkMode ? "Dark mode enabled" : "Light mode enabled");
        };
        strip.Items.Add(btnDark);

        strip.Items.Add(new ToolStripSeparator());

        // === Async demo ===
        var btnAsync = new ToolStripButton("Demo Async Children") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnAsync.Click += async (_, _) =>
        {
            await DemoAsyncChildren();
        };
        strip.Items.Add(btnAsync);

        strip.Items.Add(new ToolStripSeparator());

        // === Expand / Collapse helpers ===
        var btnExpandAll = new ToolStripButton("Expand All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnExpandAll.Click += (_, _) => ExpandAll(_data);
        strip.Items.Add(btnExpandAll);

        var btnCollapseAll = new ToolStripButton("Collapse All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnCollapseAll.Click += (_, _) => { CollapseAll(_data); _treeList.Rebuild(); };
        strip.Items.Add(btnCollapseAll);

        strip.Items.Add(new ToolStripSeparator());

        var lblHint = new ToolStripLabel("Tip: Click headers to sort • Double-click to edit • Space toggles checkbox when focused • F5 = Rebuild");
        lblHint.ForeColor = Color.FromArgb(108, 117, 125);
        strip.Items.Add(lblHint);

        return strip;
    }

    private async Task DemoAsyncChildren()
    {
        var sel = _treeList.SelectedModel;
        if (sel == null)
        {
            MessageBox.Show("Select a node first to load async children into it.");
            return;
        }

        // Temporarily switch this node to use async loading for demo
        UpdateStatus("Loading children asynchronously (simulated 700ms delay)...");

        // We simulate by clearing children and using the async getter for one expansion
        sel.Children.Clear();

        // Install a one-shot async getter
        var originalAsync = _treeList; // we just call Rebuild after

        // Force an async load by temporarily setting the async getter
        Func<OrgItem, Task<IEnumerable<OrgItem>>> asyncLoader = async item =>
        {
            await Task.Delay(700);
            return
            [
                new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async Engineer 1", "Person", Role: "Engineer"),
                new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async Engineer 2", "Person", Role: "Engineer"),
                new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async QA", "Person", Role: "QA")
            ];
        };

        // Because the control already supports SetChildrenGetterAsync, we can just expand after setting it
        // For the demo we simply call Expand which will use the current getter. We temporarily replace the getter.
        var previous = _treeList; // marker

        // We use a small trick: directly call the internal behavior by adding children after delay and rebuilding.
        await Task.Delay(700);

        sel.Children.AddRange(
        [
            new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async Engineer 1", "Person", Role: "Engineer"),
            new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async Engineer 2", "Person", Role: "Engineer"),
            new OrgItem(Guid.NewGuid().ToString("N")[..6], "Async QA Lead", "Person", Role: "QA")
        ]);

        _treeList.Rebuild();
        _treeList.SelectModel(sel);
        UpdateStatus("Async children loaded and inserted.");
    }

    private void WireEvents()
    {
        _treeList.SelectionChanged += (_, _) =>
        {
            var sel = _treeList.SelectedModels;
            _statusLabel.Text = sel.Count == 1
                ? $"Selected: {sel[0].Name}  •  Type: {sel[0].Type}"
                : $"Selected {sel.Count} items";
        };

        _treeList.NodeExpanded += (_, e) =>
        {
            UpdateStatus($"Expanded: {e.Model.Name}");
        };

        _treeList.NodeCheckStateChanged += (_, e) =>
        {
            UpdateStatus($"Checkbox changed: {e.Model.Name} → state updated (tri-state propagated if parent)");
        };

        _treeList.ColumnSortChanged += (_, _) =>
        {
            var col = _treeList.SortColumn;
            UpdateStatus(col.HasValue
                ? $"Sorted by column {_treeList.Columns[col.Value].Title} ({_treeList.SortOrder})"
                : "Sort cleared");
        };

        _treeList.DragDropNode += (_, e) =>
        {
            // In a real app you would move the item in your data source here.
            UpdateStatus($"[DragDrop] {e.Source.Name} dropped {e.Position} {e.Target.Name}. Rebuild recommended after mutating your data.");
            // For the demo we just rebuild (the actual move would be done by the user in their model)
            _treeList.Rebuild();
        };

        _treeList.DragOverNode += (_, e) =>
        {
            // Example: prevent dropping a Department into a Person
            if (e.Source.Type == "Department" && e.Target.Type == "Person")
                e.Effect = DragDropEffects.None;
        };
    }

    private void UpdateStatus(string text)
    {
        _statusLabel.Text = text;
    }

    // ==================== DATA HELPERS ====================
    private void ExpandAll(IEnumerable<OrgItem> items)
    {
        foreach (var item in items)
        {
            _treeList.Expand(item);
            if (item.Children.Count > 0)
                ExpandAll(item.Children);
        }
        _treeList.Rebuild();
    }

    private void CollapseAll(IEnumerable<OrgItem> items)
    {
        foreach (var item in items)
        {
            _treeList.Collapse(item);
            if (item.Children.Count > 0)
                CollapseAll(item.Children);
        }
    }

    private void ReplaceInData(OrgItem oldItem, OrgItem newItem)
    {
        bool ReplaceInList(List<OrgItem> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], oldItem))
                {
                    list[i] = newItem;
                    return true;
                }
                if (ReplaceInList(list[i].Children))
                    return true;
            }
            return false;
        }
        ReplaceInList(_data);
    }

    private static List<OrgItem> CreateSampleData()
    {
        // Same rich sample tree as before (slightly extended)
        var engineering = new OrgItem("eng", "Engineering", "Department", Headcount: 87,
            Children: [
                new OrgItem("eng-plat", "Platform", "Team", Headcount: 32,
                    Children: [
                        new OrgItem("p1", "Alex Rivera", "Person", Role: "Principal Engineer", LastActive: DateTime.Now.AddHours(-3)),
                        new OrgItem("p2", "Jordan Hale", "Person", Role: "Staff Engineer", LastActive: DateTime.Now.AddDays(-1)),
                        new OrgItem("p3", "Sam Patel", "Person", Role: "Senior Engineer", LastActive: DateTime.Now.AddHours(-9)),
                    ]),
                new OrgItem("eng-app", "Applications", "Team", Headcount: 41,
                    Children: [
                        new OrgItem("a1", "Taylor Kim", "Person", Role: "Engineering Manager", LastActive: DateTime.Now.AddMinutes(-40)),
                        new OrgItem("a2", "Casey Brooks", "Person", Role: "Senior Engineer", LastActive: DateTime.Now.AddHours(-2)),
                        new OrgItem("a3", "Riley Quinn", "Person", Role: "Engineer", LastActive: DateTime.Now.AddDays(-2)),
                        new OrgItem("a4", "Morgan Ellis", "Person", Role: "Engineer", LastActive: DateTime.Now.AddHours(-11)),
                    ]),
                new OrgItem("eng-qa", "Quality & Reliability", "Team", Headcount: 14,
                    Children: [ new OrgItem("q1", "Drew Santos", "Person", Role: "QA Lead", LastActive: DateTime.Now.AddHours(-5)) ])
            ]);

        var design = new OrgItem("des", "Design", "Department", Headcount: 19,
            Children: [
                new OrgItem("des-prod", "Product Design", "Team", Headcount: 12,
                    Children: [
                        new OrgItem("d1", "Jamie Torres", "Person", Role: "Design Director", LastActive: DateTime.Now.AddHours(-1)),
                        new OrgItem("d2", "Avery Lane", "Person", Role: "Senior Product Designer", LastActive: DateTime.Now.AddDays(-1)),
                    ]),
                new OrgItem("des-brand", "Brand & Marketing", "Team", Headcount: 7)
            ]);

        var hr = new OrgItem("hr", "People Operations", "Department", Headcount: 11,
            Children: [
                new OrgItem("hr-t1", "People Partners", "Team", Headcount: 5,
                    Children: [ new OrgItem("h1", "Cameron West", "Person", Role: "People Partner", LastActive: DateTime.Now.AddHours(-7)) ]),
                new OrgItem("hr-recruit", "Talent Acquisition", "Team", Headcount: 6)
            ]);

        var exec = new OrgItem("exec", "Executive", "Department", Headcount: 4,
            Children: [
                new OrgItem("ceo", "Dr. Elena Voss", "Person", Role: "CEO", LastActive: DateTime.Now.AddMinutes(-15)),
                new OrgItem("coo", "Marcus Bell", "Person", Role: "COO", LastActive: DateTime.Now.AddHours(-4)),
            ]);

        return [exec, engineering, design, hr];
    }
}

