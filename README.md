# ModernTreeListView

[![NuGet](https://img.shields.io/nuget/v/ModernTreeListView.svg)](https://www.nuget.org/packages/ModernTreeListView)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-blueviolet)](https://dotnet.microsoft.com/)

A modern, lightweight **TreeListView / TreeGrid** control for Windows Forms (.NET 8+) — a tree and a multi-column list view in a single, custom-painted control.

Zero dependencies, a single source file, a fluent API, and first-class support for immutable C# records.

## Features

- **Hybrid tree + columns** — hierarchical rows with any number of typed, formatted columns.
- **Lazy loading** — children are queried on demand via `SetChildrenGetter`; combine with `SetHasChildrenGetter` to browse very large data sets (100,000+ nodes) without materializing them up front.
- **Rich in-place editing** — type-aware default editors (TextBox, DateTimePicker for `DateTime`, CheckBox for `bool`), per-column custom editors, edit validation, and full keyboard flow (F2/Enter, Tab/Shift+Tab, Esc).
- **Immutable-friendly** — designed for records: commit edits with `model with { … }` + `ReplaceModel()`, which preserves expansion, children, and selection.
- **Column sorting** — click a header to cycle ascending → descending → none, with stable per-sibling-group ordering and ▲/▼ indicators; also available programmatically via `Sort()` / `ClearSort()`.
- **Filtering** — predicate-based `SetFilter()` keeps ancestors of matches visible and auto-expands them.
- **Multi-selection** — Ctrl/Shift click, Shift+arrows, Ctrl+A, exposed through `SelectedModels`.
- **Checkboxes** — `ShowCheckBoxes` with mouse + Space-key toggling, `SetChecked()`, `CheckedModels`, and a `CheckedChanged` event.
- **Per-node icons** — supply 16×16 images with `SetIconGetter`.
- **Theming** — `TreeListTheme.Light` / `TreeListTheme.Dark` presets via `ApplyTheme()`, plus every color individually settable.
- **Polished UX** — tree connector lines, hover highlighting, alternating row shading, optional grid lines, tooltips for truncated cells, type-ahead search, column resize with double-click auto-fit, `AutoFillLastColumn`.
- **Fast** — custom-painted and double-buffered; only visible rows are realized and drawn.

## Requirements

- Windows
- .NET 8.0 or .NET 9.0 (`net8.0-windows` / `net9.0-windows`) with Windows Forms

## Installation

```bash
dotnet add package ModernTreeListView
```

Or via the Package Manager Console:

```powershell
Install-Package ModernTreeListView
```

## Quick Start

```csharp
using ModernTreeListView;

public record OrgItem(
    string Name,
    string? Role = null,
    int Headcount = 0,
    DateTime? LastActive = null,
    List<OrgItem>? Children = null)
{
    public List<OrgItem> Children { get; init; } = Children ?? [];
}

var tree = new ModernTreeListView<OrgItem>
{
    Dock = DockStyle.Fill,
    RowHeight = 28,
    ShowTreeLines = true,
    MultiSelect = true,
    AutoFillLastColumn = true
};

// Columns (fluent, with formatters and alignment)
tree
    .AddColumn("Name", m => m.Name, width: 280)
    .AddColumn("Role", m => m.Role, width: 180)
    .AddColumn("Headcount", m => m.Headcount, width: 100, c =>
    {
        c.Alignment = HorizontalAlignment.Right;
        c.Formatter = v => v is int i ? i.ToString("N0") : "";
    })
    .AddColumn("Last Active", m => m.LastActive, width: 140, c =>
    {
        c.Formatter = v => v is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm") : "";
    });

// Data binding — children are loaded lazily, on first expand
tree
    .SetRoots(rootItems)
    .SetChildrenGetter(m => m.Children)
    .SetHasChildrenGetter(m => m.Children.Count > 0)
    .SetCellValueSetter((model, column, value) =>
    {
        // Immutable record pattern: build the updated instance and swap it in.
        if (column.Title == "Name" && value is string name && name.Length > 0)
        {
            var updated = model with { Name = name };
            // ...update your own source collection too, then:
            tree.ReplaceModel(model, updated); // keeps expansion + selection
        }
    });
```

## Going Further

### Custom cell editors

Each column can supply its own editor and value extractor:

```csharp
tree.AddColumn("Type", m => m.Type, width: 110, c =>
{
    c.EditorFactory = (model, value) =>
    {
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmb.Items.AddRange(["Department", "Team", "Person"]);
        cmb.SelectedItem = value as string;
        return cmb;
    };
    c.EditorValueExtractor = editor => ((ComboBox)editor).SelectedItem;
});
```

Validate (or veto) edits before they are applied:

```csharp
tree.CellEditCommitted += (_, e) =>
{
    if (e.Column.Title == "Name" && string.IsNullOrWhiteSpace(e.ProposedValue?.ToString()))
        e.Cancel = true;
};
```

### Filtering and sorting

```csharp
tree.SetFilter(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
tree.ClearFilter();

tree.Sort(columnIndex: 2, SortOrder.Descending);
tree.ClearSort();
```

### Theming

```csharp
tree.ApplyTheme(TreeListTheme.Dark);   // or TreeListTheme.Light

// Fine-tune any color afterwards:
tree.SelectionBackColor = Color.FromArgb(0, 120, 212);
```

### Large data sets

For data too big to materialize, generate children on demand:

```csharp
tree
    .SetChildrenGetter(parent => QueryChildren(parent))      // called on first expand
    .SetHasChildrenGetter(parent => parent.MayHaveChildren); // avoids enumerating just to draw expanders
```

### Events

| Event | Raised when |
| --- | --- |
| `SelectionChanged` | The selection or focused row changes |
| `NodeExpanded` / `NodeCollapsed` | A node is expanded or collapsed |
| `CellEditCommitted` / `CellEditCanceled` | An in-place edit is committed (cancellable) or canceled |
| `ColumnSortChanged` | The sort column or direction changes |
| `CheckedChanged` | A node's checked state changes |

### Keyboard reference

| Key | Action |
| --- | --- |
| ↑ ↓ PgUp PgDn Home End | Navigate (hold Shift to extend the selection) |
| ← / → | Collapse / expand, or move to parent / first child |
| `+` / `-` / `*` | Expand / collapse node, expand whole subtree |
| F2 or Enter | Edit the current cell |
| Tab / Shift+Tab | Commit and edit the next / previous editable cell |
| Esc | Cancel the edit |
| Space | Toggle checkboxes for all selected rows |
| Ctrl+A | Select all rows |
| Any letter/digit | Type-ahead search in the first column |

## Demo Application

A complete sample exercising every feature (lazy 105,000-node data set, custom editors, themes, filtering, checkboxes, icons, and more) lives in [`samples/ModernTreeListView.Demo`](samples/ModernTreeListView.Demo):

```bash
dotnet run --project samples/ModernTreeListView.Demo
```

## Project Structure

```
ModernTreeListView/
├── src/
│   └── ModernTreeListView/          # The library (multi-targets net8.0-windows / net9.0-windows)
├── samples/
│   └── ModernTreeListView.Demo/     # WinForms demo application
├── .github/workflows/               # CI: NuGet publishing on version tags
├── Directory.Build.props
├── ModernTreeListView.slnx
├── LICENSE
└── README.md
```

## Contributing

Contributions are welcome!

1. Fork the repository and create a feature branch.
2. Make your changes — keep the fluent API style, zero-dependency policy, and rendering performance in mind.
3. Ensure `dotnet build` succeeds without warnings and the demo app runs.
4. Open a pull request describing the motivation and the change.

Bug reports and feature requests are welcome via [GitHub Issues](https://github.com/kushanp/ModernTreeListView/issues).

## Releasing (maintainers)

Pushing a tag matching `v*.*.*` (e.g. `v1.2.3`) triggers the `publish-nuget.yml` workflow, which builds in Release mode, packs the `.nupkg` and `.snupkg`, and publishes to [NuGet.org](https://www.nuget.org/packages/ModernTreeListView). The workflow authenticates with the `NUGET_API_KEY` repository secret.

```bash
git tag v1.2.3
git push origin main --tags
```

## License

Licensed under the [MIT License](LICENSE).
