# Pinta.Core → Pinta.Foundation: Unported Classes

This document lists every class/type in `Pinta.Core` that was **not** ported to `Pinta.Foundation`, along with the reason.

The guiding principle: `Pinta.Foundation` must not reference any graphical toolkit (GTK4, Cairo, Gdk, Gio, GLib, Pango, Adw, Mono.Addins) or global singletons (`PintaCore`). Only pure business logic, algorithms, and data types belong in `Pinta.Foundation`.

---

## Actions (all unported — UI layer)

| Class | File | Reason |
|-------|------|--------|
| `AddinActions` | Actions/AddinActions.cs | GTK/Gio action management — UI layer |
| `AdjustmentsActions` | Actions/AdjustmentsActions.cs | Action registration for UI menus |
| `AppActions` | Actions/AppActions.cs | GTK application actions — UI layer |
| `Command`, `ToggleCommand` | Actions/Command.cs | GLib/Gio command wrappers — UI layer |
| `EditActions` | Actions/EditActions.cs | Cairo/GTK/Gdk edit operations — UI layer |
| `EffectsActions` | Actions/EffectsActions.cs | Gio effects action registration — UI layer |
| `FileActions` | Actions/FileActions.cs | GTK/Gdk/Gio file operations — UI layer |
| `HelpActions` | Actions/HelpActions.cs | GTK/Gio help menu actions — UI layer |
| `ImageActions` | Actions/ImageActions.cs | Cairo/GTK/Gio image operations — UI layer |
| `LayerActions` | Actions/LayerActions.cs | Cairo/GTK/Gio layer operations — UI layer |
| `ViewActions` | Actions/ViewActions.cs | GLib/GTK/Gio view operations — UI layer |
| `WindowActions` | Actions/WindowActions.cs | GLib/GTK/Gio window operations — UI layer |

## Algorithms

| Class | File | Reason |
|-------|------|--------|
| `Utility` | Algorithms/Utility.cs | Uses `Cairo.ImageSurface`. Relevant pure-math portions were ported to `FoundationUtility`. |

## Classes

| Class | File | Reason |
|-------|------|--------|
| `AsyncEffectRenderer` | Classes/AsyncEffectRenderer.cs | Depends on `Cairo.ImageSurface` for rendering surfaces. Would need rearchitecting with `PixelBuffer`. |
| `BasePaintBrush` | Classes/BasePaintBrush.cs | Depends on Cairo context and Gdk for brush rendering — UI/rendering layer |
| `BaseTool` | Classes/BaseTool.cs | Core tool abstraction depending on GTK widgets and Gdk events — UI layer |
| `BrushStrokeArgs` | Classes/BrushStrokeArgs.cs | Depends on `Cairo.ImageSurface` — rendering layer |
| `ColorBgra.Conversion` | Classes/Color/ColorBgra.Conversion.cs | Contains conversions to/from `Cairo.Color` — toolkit-specific |
| `Document` | Classes/Document.cs | Core document class depending on Cairo surfaces and Gio — UI/document management layer |
| `DocumentHistory` | Classes/DocumentHistory.cs | Document-level undo/redo management. While no direct GUI deps, it's tightly coupled to the `Document` class which uses Cairo. |
| `DocumentLayers` | Classes/DocumentLayers.cs | Layer management depending on `Cairo.ImageSurface` — rendering layer |
| `DocumentSelection` | Classes/DocumentSelection.cs | Selection paths using `Cairo.Path` — rendering layer |
| `DocumentWorkspace` | Classes/DocumentWorkspace.cs | GLib/GTK workspace management — UI layer |
| `HsvColor` *(Conversion parts)* | Classes/HsvColor.cs | The `Cairo.Color` conversion was removed; core HSV logic was ported. |
| `IExtension` | Classes/IExtension.cs | Depends on `Mono.Addins` and `PintaCore` — plugin infrastructure |
| `IToolHandle` | Classes/IToolHandle.cs | GTK-specific tool handle interface — UI layer |
| `Layer` | Classes/Layer.cs | Depends on `Cairo.ImageSurface` for layer pixel data — rendering layer |
| `LayerProperties` | Classes/LayerProperties.cs | Has `SetProperties(Layer)` method depending on `Layer` which uses Cairo |
| `Palette` | Classes/Palette.cs | Uses `Cairo.Color` for color representation — toolkit-specific |
| `ReEditableLayer` | Classes/Re-editable/ReEditableLayer.cs | Tightly coupled to `Layer` and `Document` — rendering layer |
| `TextEngine` | Classes/Re-editable/Text/TextEngine.cs | Uses Cairo context and GLib/Gdk for text input — UI/rendering layer |
| `TextLayout` | Classes/Re-editable/Text/TextLayout.cs | Uses `Pango.Layout` for text rendering — UI/rendering layer |
| `SelectionModeHandler` | Classes/SelectionModeHandler.cs | GTK widget for selection mode — UI layer |
| `SurfaceDiff` | Classes/SurfaceDiff.cs | Operates on `Cairo.ImageSurface` — rendering layer |
| `Translations` | Classes/Translations.cs | GLib internationalization — UI/localization layer |
| `UserLayer` | Classes/UserLayer.cs | Extends `Layer` with Cairo surfaces — rendering layer |
| `RenderHandle`, `CompletionInfo` | Classes/RenderHandle.cs | Internal rendering coordination — could be ported in future |
| `ToolOption`, `IntegerOption` | Classes/ToolOption/*.cs | Tool options metadata — no GUI deps but tightly coupled to tool system |

## Effects

| Class | File | Reason |
|-------|------|--------|
| `BaseEffect`, `EffectData` | Effects/BaseEffect.cs | Base effect class using `Cairo.ImageSurface` and `Mono.Addins` — rendering/plugin layer |

## Enumerations

| Class | File | Reason |
|-------|------|--------|
| `CursorShape` | Enumerations/CursorShape.cs | Cursor shapes for UI tools — UI layer |
| `ResamplingMode` *(extension methods)* | Enumerations/ResamplingMode.cs | Extension method `ToInterpolationMode()` depends on Cairo. The enum itself was ported. |

## EventArgs

| Class | File | Reason |
|-------|------|--------|
| `BrushEventArgs` | EventArgs/BrushEventArgs.cs | No GUI deps but tightly coupled to brush tool system |
| `CanvasInvalidatedEventArgs` | EventArgs/CanvasInvalidatedEventArgs.cs | Canvas invalidation for UI repainting |
| `DocumentEventArgs` | EventArgs/DocumentEventArgs.cs | References `Document` which uses Cairo |
| `DocumentSaveEventArgs` | EventArgs/DocumentSaveEventArgs.cs | References document save operations |
| `HistoryItemAddedEventArgs` | EventArgs/HistoryItemAddedEventArgs.cs | References `BaseHistoryItem` — undo/redo UI |
| `IndexEventArgs` | EventArgs/IndexEventArgs.cs | Simple index event — no GUI deps but only used by UI classes |
| `ModifyCompressionEventArgs` | EventArgs/ModifyCompressionEventArgs.cs | GTK dialog interaction — UI layer |
| `TextChangedEventArgs` | EventArgs/TextChangedEventArgs.cs | Text change events for text tool — could be ported later |
| `ToolEventArgs` | EventArgs/ToolEventArgs.cs | Tool event — coupled to tool system |
| `ToolKeyEventArgs` | EventArgs/ToolKeyEventArgs.cs | Uses `Gdk.Key` — UI input layer |
| `ToolMouseEventArgs` | EventArgs/ToolMouseEventArgs.cs | Uses `Gdk` mouse state — UI input layer |

## Extensions (all Cairo/GTK/GLib wrappers — UI layer)

| Class | File | Reason |
|-------|------|--------|
| `AddinUtilities` | Extensions/AddinUtilities.cs | `Mono.Addins` utilities — plugin infrastructure |
| `CairoExtensions.*` (10 files) | Extensions/Cairo/*.cs | All Cairo-specific extensions — rendering toolkit layer |
| `GLibExtensions.Timer` | Extensions/GLib/GLibExtensions.Timer.cs | GLib timer wrapper — UI layer |
| `GdkExtensions` | Extensions/GdkExtensions.cs | Gdk utilities — UI layer |
| `GdkKey` | Extensions/GdkKey.cs | Gdk keyboard abstraction — UI input layer |
| `GdkPixbufExtensions` | Extensions/GdkPixbufExtensions.cs | GdkPixbuf operations — UI/image layer |
| `GioExtensions` | Extensions/GioExtensions.cs | GLib/Gio file operations — UI layer |
| `GioStream` | Extensions/GioStream.cs | GLib/Gio stream wrapper — UI layer |
| `GrapheneExtensions` | Extensions/GrapheneExtensions.cs | Graphene math extensions — no GUI deps but only used by UI code |
| `GskExtensions` | Extensions/GskExtensions.cs | GSK (GTK Scene Kit) extensions — UI rendering layer |
| `BoxStyle` | Extensions/Gtk/BoxStyle.cs | GTK widget styling — UI layer |
| `GtkExtensions.*` (7 files) | Extensions/Gtk/*.cs | All GTK-specific extensions — UI layer |
| `IntlExtensions` | Extensions/IntlExtensions.cs | GLib internationalization — UI/localization layer |
| `NativeImportResolver` | Extensions/NativeImportResolver.cs | No GUI deps but manages native library loading for GTK/Cairo |
| `OtherExtensions` *(partial)* | Extensions/OtherExtensions.cs | `LaunchUri` (uses GTK), `ShowUnsupportedFormatDialog` (uses GTK), `CreatePolygonSet` (uses CairoExtensions) were not ported. `ToRows` and `RandomColorBgra` were ported. |
| `PaletteHelper` | Extensions/PaletteHelper.cs | Uses Cairo/Gio for palette file operations — UI layer |
| `PangoExtensions` | Extensions/PangoExtensions.cs | `PangoRectangle` struct — no GUI deps but only used by text rendering |
| `ToolBarComboBox` | Extensions/ToolBarComboBox.cs | GTK toolbar widget — UI layer |
| `ToolBoxButton` | Extensions/ToolBoxButton.cs | GTK toolbox widget — UI layer |

## HistoryItems (all unported — document/UI layer)

| Class | File | Reason |
|-------|------|--------|
| `BaseHistoryItem` | HistoryItems/BaseHistoryItem.cs | No direct GUI deps but is the base for all undo/redo items tied to Document |
| `AddLayerHistoryItem` | HistoryItems/AddLayerHistoryItem.cs | Uses `PintaCore` singleton — UI layer |
| `CompoundHistoryItem` | HistoryItems/CompoundHistoryItem.cs | Uses Cairo/PintaCore — UI layer |
| `DeleteLayerHistoryItem` | HistoryItems/DeleteLayerHistoryItem.cs | Uses `PintaCore` singleton — UI layer |
| `FinishPixelsHistoryItem` | HistoryItems/FinishPixelsHistoryItem.cs | Uses Cairo/PintaCore — UI layer |
| `InvertHistoryItem` | HistoryItems/InvertHistoryItem.cs | Uses `PintaCore` singleton — UI layer |
| `MovePixelsHistoryItem` | HistoryItems/MovePixelsHistoryItem.cs | Uses Cairo/PintaCore — UI layer |
| `PasteHistoryItem` | HistoryItems/PasteHistoryItem.cs | Uses Cairo/PintaCore — UI layer |
| `ResizeHistoryItem` | HistoryItems/ResizeHistoryItem.cs | No direct GUI deps but only used by Document system |
| `SelectionHistoryItem` | HistoryItems/SelectionHistoryItem.cs | No direct GUI deps but only used by Document/Selection system |
| `SimpleHistoryItem` | HistoryItems/SimpleHistoryItem.cs | Uses Cairo/PintaCore — UI layer |
| `SwapLayersHistoryItem` | HistoryItems/SwapLayersHistoryItem.cs | Uses `PintaCore` singleton — UI layer |
| `TextHistoryItem` | HistoryItems/TextHistoryItem.cs | Uses Cairo — rendering layer |
| `UpdateLayerPropertiesHistoryItem` | HistoryItems/UpdateLayerPropertiesHistoryItem.cs | Uses `PintaCore` singleton — UI layer |

## ImageFormats (all unported — UI/IO layer)

| Class | File | Reason |
|-------|------|--------|
| `FormatDescriptor` | ImageFormats/FormatDescriptor.cs | Uses GTK file filter — UI layer |
| `GdkPixbufFormat` | ImageFormats/GdkPixbufFormat.cs | Uses Cairo/GTK/Gdk/Gio — UI/rendering layer |
| `IImageExporter` | ImageFormats/IImageExporter.cs | Uses GTK/Gio/Mono.Addins — UI layer |
| `IImageImporter` | ImageFormats/IImageImporter.cs | Uses Gio/Mono.Addins — UI layer |
| `JpegFormat` | ImageFormats/JpegFormat.cs | Uses GTK/Gdk/Gio/PintaCore — UI layer |
| `NetpbmPortablePixmap` | ImageFormats/NetpbmPortablePixmap.cs | Uses Cairo/GTK/Gio/PintaCore — UI layer |
| `OraFormat` | ImageFormats/OraFormat.cs | Uses Cairo/GTK/Gdk/Gio/PintaCore — UI layer |
| `TgaExporter` | ImageFormats/TgaExporter.cs | Uses Cairo/GTK/Gio — rendering/UI layer |

## Managers (all unported — application service layer)

| Class | File | Reason |
|-------|------|--------|
| `ActionManager` | Managers/ActionManager.cs | Adw/GTK action management — UI layer |
| `CanvasGridManager` | Managers/CanvasGridManager.cs | No GUI deps but manages canvas grid state for UI |
| `ChromeManager` | Managers/ChromeManager.cs | GTK/Gdk/Gio/Mono.Addins — core UI management |
| `EffectsManager` | Managers/EffectsManager.cs | No GUI deps but manages effect registration |
| `ImageConverterManager` | Managers/ImageConverterManager.cs | Uses Gdk — image conversion layer |
| `LivePreviewManager` | Managers/LivePreviewManager.cs | Cairo/GLib live preview — UI rendering layer |
| `PaintBrushManager` | Managers/PaintBrushManager.cs | No GUI deps but manages brush registration |
| `PaletteFormatManager` | Managers/PaletteFormatManager.cs | No GUI deps but manages palette format registration |
| `PaletteManager` | Managers/PaletteManager.cs | Uses Cairo/Gio — UI palette management |
| `RecentFileManager` | Managers/RecentFileManager.cs | Uses GTK/Gio — UI recent files |
| `ResourceManager` | Managers/ResourceManager.cs | Uses Gdk — UI resource management |
| `ServiceManager` | Managers/ServiceManager.cs | Application service locator |
| `SettingsManager` | Managers/SettingsManager.cs | No GUI deps but manages application settings |
| `SystemManager` | Managers/SystemManager.cs | Uses `Mono.Addins` — platform management |
| `ToolManager` | Managers/ToolManager.cs | Uses GTK/Gdk — tool management |
| `WorkspaceManager` | Managers/WorkspaceManager.cs | Uses Cairo/GTK/Gio/PintaCore — workspace management |

## PaletteFormats

| Class | File | Reason |
|-------|------|--------|
| `PaintShopProPalette` | PaletteFormats/PaintShopProPalette.cs | Uses Cairo/Gio/PintaCore. Could be ported to stream-based API in future. |
| `PaletteDescriptor` | PaletteFormats/PaletteDescriptor.cs | Uses GTK for file filters — UI layer |

## Other

| Class | File | Reason |
|-------|------|--------|
| `PintaCore` | PintaCore.cs | Global application singleton — UI orchestration layer |
| `SettingNames` | SettingNames.cs | References `BaseTool` — coupled to UI tool system |
| `ToolBarDropDownButton`, `ToolBarItem` | Widgets/ToolBarDropDownButton.cs | GTK toolbar widgets — UI layer |
| `FriendAssemblies` | FriendAssemblies.cs | Assembly-level `InternalsVisibleTo` — infrastructure, not portable logic |
| `ErrorDialogResponse` | Messages/ErrorDialogResponse.cs | No GUI deps but simple enum only used by UI error dialogs |

---

## Summary

**Total files in Pinta.Core:** ~170 `.cs` files

**Ported to Pinta.Foundation:** ~60 types across ~55 files, including:
- All color types (ColorBgra, HsvColor, IColor interfaces)
- All geometry types (PointI/D/F, Size, RectangleI/D, Angle, Matrix3x2D, Fraction, etc.)
- All pure algorithms (Mathematics, PerlinNoise, ColorDifference, Histogram, Julia, Mandelbrot, Sampling, SpatialPartition)
- All blend operations (16 blend modes with SIMD vectorization)
- Pixel operations (PixelOp, BinaryPixelOp, UnaryPixelOp, all UnaryPixelOps)
- Gradient rendering (GradientRenderer, all GradientRenderers)
- Palette formats (stream-based IPaletteLoader/Saver, GimpPalette, PaintDotNetPalette)
- Utility classes (BitMask, SplineInterpolator, ObservableObject, ColorGradient, etc.)
- All applicable enumerations
- New `PixelBuffer` type replacing Cairo.ImageSurface for pixel storage

**Not ported:** ~115 files — all are either:
1. **GUI/Toolkit dependent** (GTK4, Cairo, Gdk, Gio, GLib, Pango, Adw, Mono.Addins)
2. **Tightly coupled to GUI-dependent types** (e.g., Document depends on Cairo surfaces)
3. **Application infrastructure** (PintaCore singleton, managers, actions)
4. **UI widgets** (toolbars, tool handles, selection mode handlers)

These unported classes belong in the UI/application layers that sit on top of Pinta.Foundation.
