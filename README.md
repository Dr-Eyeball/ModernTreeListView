# ModernTreeListView

[![NuGet](https://img.shields.io/nuget/v/ModernTreeListView.svg)](https://www.nuget.org/packages/ModernTreeListView)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-blueviolet)](https://dotnet.microsoft.com/)

A modern, high-performance, production-ready **TreeListView / TreeGrid** control for Windows Forms (.NET 8 and later).

Designed to be genuinely useful and competitive with commercial controls while remaining lightweight, fully open-source (MIT), and easy to consume via NuGet.

## Key Features

- **Virtual Mode** — Handle 50,000 – 500,000+ nodes efficiently with on-demand data loading.
- **Custom Cell Editors** — Built-in support for TextBox, ComboBox, DateTimePicker, CheckBox, NumericUpDown. Register per-column or per-type custom editors easily.
- **Tri-State Hierarchical Checkboxes** — Full support with indeterminate state, automatic propagation, and `GetCheckedItems()` / `SetChecked()`.
- **Drag & Drop** — Move/reorder nodes or change parents with a visual drop indicator (line or highlight).
- **Advanced Filtering** — Predicate-based filtering that intelligently preserves parent nodes of matching children.
- **Multi-Selection** — Ctrl/Shift selection with `SelectedModels` collection.
- **Async Children Loading** — `SetChildrenGetterAsync` with built-in loading indicators.
- **Column Sorting** — Click headers to sort (ascending/descending/none) with stable ordering and ▲/▼ indicators.
- **Theming & Appearance** — Built-in dark mode (`UseDarkMode`) plus full color customization.
- **Immutable-Friendly** — Excellent support for C# records via `ReplaceModel()` and `RefreshObject()`.
- **Fluent API** — Clean, chainable configuration: `AddColumn(...).SetChildrenGetter(...).SetCellValueSetter(...)`.
- **High-Performance Rendering** — Custom-painted, double-buffered, low-allocation visible row management.
- **Rich In-Place Editing** — Keyboard-friendly (F2, Enter, Tab, arrows), type-aware default editors, validation support.

## Installation

```bash
dotnet add package ModernTreeListView
```

Or via Package Manager Console:

```powershell
Install-Package ModernTreeListView
```

## Quick Start

```csharp
using ModernTreeListView;

var treeList = new ModernTreeListView<MyDataItem>
{
    Dock = DockStyle.Fill,
    RowHeight = 28,
    ShowAlternatingRows = true
};

// Fluent column definition
treeList
    .AddColumn("Name", x => x.Name, width: 280)
    .AddColumn("Date", x => x.Created, width: 120, c =>
    {
        c.Formatter = v => v is DateTime dt ? dt.ToShortDateString() : "";
        c.Alignment = HorizontalAlignment.Right;
    })
    .AddColumn("Status", x => x.Status, width: 100);

// Data binding
treeList
    .SetRoots(rootItems)
    .SetChildrenGetter(item => item.Children)
    .SetCellValueSetter((item, column, value) =>
    {
        // Handle edits (works seamlessly with immutable records)
        if (column.Title == "Name" && value is string newName)
        {
            // Create updated record and replace in the control
            var updated = item with { Name = newName };
            // ... update your source data ...
            treeList.ReplaceModel(item, updated);
        }
    });

// Enable advanced features
treeList.ShowCheckboxes = true;
treeList.AllowDragDrop = true;
treeList.MultiSelect = true;
treeList.UseDarkMode = true;
```

## Advanced Usage

- **Virtual Mode**: See `SetVirtualMode()`, `SetVirtualRootGetter()`, `SetVirtualChildGetter()`, etc.
- **Custom Editors**: Use `SetColumnEditor(columnIndex, factory)` to provide ComboBox, DateTimePicker, or any custom Control.
- **Checkboxes**: `GetCheckedItems()`, `SetChecked()`, `NodeCheckStateChanged` event with full tri-state support.
- **Drag & Drop**: Handle `ItemDrag`, `DragOverNode`, and `DragDropNode` events.
- **Filtering**: `SetFilter(predicate)` or the convenience `SetFilterText(text)`.
- **Async Loading**: `SetChildrenGetterAsync(async item => await GetChildrenAsync(item))`.

Full demonstrations of all features are available in the `samples/ModernTreeListView.Demo` project.

## Project Structure

This repository uses a professional, NuGet-ready layout:

```
ModernTreeListView/
├── src/
│   └── ModernTreeListView/                 # The NuGet library (multi-target net8/net9)
├── samples/
│   └── ModernTreeListView.Demo/            # WinForms sample application
├── .editorconfig
├── .gitignore
├── LICENSE
├── README.md
└── ModernTreeListView.sln
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes (keep the fluent API and performance focus)
4. Ensure `dotnet build` succeeds cleanly
5. Open a Pull Request

Bug reports and feature requests are welcome via GitHub Issues.

## License

Licensed under the [MIT License](LICENSE).

## Acknowledgments

Built to provide a high-quality, open-source alternative for complex hierarchical data scenarios in WinForms applications. Feedback and contributions from the community help make it better.