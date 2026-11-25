# AGENTS.md: Plugin.Maui.Sheet

Guidance for AI coding agents that write .NET MAUI app code with **Plugin.Maui.Sheet**, followed by notes for agents working on this repository. Human-oriented docs: [README.md](README.md). This file also ships in the NuGet package (`AGENTS.md` at the package root).

## What it is

Native bottom sheets for .NET MAUI 11: `UISheetPresentationController` on iOS/Mac Catalyst, Material `BottomSheetDialog` on Android, a centred WinUI `Popup` card on Windows. The header (title, subtitle, close/back, actions, overflow menu) is native on every platform. Sheet content is ordinary MAUI **Views** (not Pages), and a sheet can hold a stack of views (in-sheet navigation).

Namespace: `Plugin.Maui.Sheet`. XAML: `xmlns:sheet="clr-namespace:Plugin.Maui.Sheet;assembly=Plugin.Maui.Sheet"`.

## Setup (required)

```csharp
builder.UseMauiApp<App>().UseNativeSheet(); // registers the handler and INativeSheetService
```

Without `UseNativeSheet()` a `NativeSheet` has no handler and never shows, and `INativeSheetService` cannot be resolved. Supported: iOS 15+, Mac Catalyst 17+, Android API 24+, Windows 10 1809+.

## Choose the API

| Need | Use |
|---|---|
| A sheet that belongs to one page, bound to page state | `<sheet:NativeSheet IsOpen="{Binding …}">` in the page's XAML |
| Open a sheet from a view model and await its close or a result | `INativeSheetService.ShowSheetAsync<TView>()` / `ShowSheetAsync<TView, TResult>()` |
| Open a sheet and keep the `NativeSheet` (navigate in it, subscribe to events) | `await INativeSheetService.OpenAsync(rootView, configure)` |
| Several screens inside one sheet | `sheet.PushAsync(view)` / `sheet.PopAsync()` |

### XAML sheet

```xml
<sheet:NativeSheet IsOpen="{Binding IsEditing}"
                   Detents="FitContent, Large"
                   Title="New entry"
                   PrimaryActionText="Save"
                   PrimaryActionCommand="{Binding SaveCommand}"
                   IsPrimaryActionEnabled="{Binding CanSave}"
                   SecondaryActionText="Cancel">
    <VerticalStackLayout Padding="20">
        <Entry Text="{Binding Name}" />
    </VerticalStackLayout>
</sheet:NativeSheet>
```

- Place it anywhere in the page layout; it takes no space. `IsOpen` is TwoWay and becomes `false` when the user dismisses the sheet.
- Content inherits the page's `BindingContext` unless it sets its own.
- An action without a command closes the sheet. With a command, the command decides (set `IsOpen = false` to close).
- `ContentTemplate` instead of `Content` builds the view on every open and releases it on close.

### Service (view models)

```csharp
public class OrdersViewModel(INativeSheetService sheets)
{
    public async Task PickAsync()
    {
        // TView comes from DI, or is created with constructor injection when not registered.
        string? fruit = await sheets.ShowSheetAsync<FruitPickerView, string>(s =>
        {
            s.Title = "Pick a fruit";
            s.Detents = [SheetDetent.FitContent, SheetDetent.Large];
            s.ShowNativeCloseButton = true;
        });
    }
}

// In the sheet's own view model: complete the awaiting call with a result.
await sheets.CloseSheetAsync("Apple");
```

- `ShowSheetAsync` completes when the sheet is dismissed; the result is `default` unless `CloseSheetAsync(result)` closed it.
- `OpenAsync(root, configure)` completes once the sheet is presented and returns the `NativeSheet`.
- `CloseSheetAsync()` closes the most recently opened sheet of the service.
- Sheets from the service are disconnected after they close; open a new one instead of reusing it.

## In-sheet navigation

```csharp
// Inside a view that is in the sheet's stack, Parent is the NativeSheet.
await ((NativeSheet)Parent).PushAsync(new CategoryPickerView());

// Back one level from code (plays the back transition, does NOT raise Popped).
await ((NativeSheet)Parent).PopAsync();
```

- The views must be `View`s (`ContentView`, layouts…), never `Page`s. There is no Shell routing, URI navigation or navigation parameters: pass data through constructors, properties or callbacks.
- **Do not** use `Navigation.PushAsync`/`Shell.GoToAsync` to navigate inside a sheet: those navigate the page underneath it.
- `NavigationStack` lists the views, root first. The root is `Content` (or the templated content).
- `PushAsync` throws `InvalidOperationException` if the sheet has no root view yet or the view is already in the stack. `PopAsync(count)` never pops the root; `count < 1` throws.
- `Popped` is raised only when the **user** goes back (back button, iOS edge swipe, Android system back, Esc on Windows), with `e.View` the removed view. Code that pops should do its own follow-up.
- Pushed views become logical children of the sheet: styles, `StaticResource`, `DynamicResource` and `AppThemeBinding` resolve, and they inherit the sheet's `BindingContext` unless you set their own.
- Popped views, and every view when the sheet closes, get their handlers disconnected. Don't keep using their platform views.
- Call navigation APIs from the UI thread. Without a handler (unit tests) `PushAsync`/`PopAsync` update `NavigationStack` and complete immediately.

### Header per view (attached properties)

```xml
<ContentView sheet:NativeSheet.Title="Category"
             sheet:NativeSheet.Subtitle="Pick one">
    <sheet:NativeSheet.ToolbarItems>
        <ToolbarItem Text="Save" Command="{Binding SaveCommand}" AutomationId="Save">
            <ToolbarItem.IconImageSource>
                <FontImageSource Glyph="✓" />
            </ToolbarItem.IconImageSource>
        </ToolbarItem>
        <ToolbarItem Text="Delete" Order="Secondary" Command="{Binding DeleteCommand}" />
    </sheet:NativeSheet.ToolbarItems>
    <!-- content -->
</ContentView>
```

- C#: `NativeSheet.SetTitle(view, "…")`, `NativeSheet.SetSubtitle(view, "…")`, `NativeSheet.GetToolbarItems(view).Add(item)`.
- The root view falls back to the sheet's own `Title`/`Subtitle`. `PrimaryActionText`/`SecondaryActionText` and the close button (`ShowNativeCloseButton`) only show at the root; pushed views get a native back button.
- `ToolbarItem` `Order="Primary"`/`Default` become trailing header actions (icon if `IconImageSource` is set, else text); `Order="Secondary"` go into the native overflow menu. Android moves primary items that don't fit into the overflow menu.
- Overflow menu items show their `IconImageSource` on iOS, Mac Catalyst and Windows; Android overflow menus are text only (Material). `ShowOverflowIcons="False"` on the sheet turns the icons off everywhere.
- `Text`, `IconImageSource`, `IsEnabled` and `Command.CanExecute(CommandParameter)` update live. Icons take the header colour unless a `FontImageSource` sets `Color`. `AutomationId` becomes the accessibility identifier on iOS, Mac Catalyst and Windows; on Android the item's `Text` is the accessibility label (content description) and `AutomationId` is not used.
- Header items inherit the view's `BindingContext`.

## Properties and events

| Member | Notes |
|---|---|
| `Detents` | `Medium`, `Large`, `FitContent`, `Fraction:0.3`, `Height:250` (XAML, comma separated). C#: `[SheetDetent.Medium, SheetDetent.Fraction(0.3)]`. Default `Medium, Large`. An empty list is ignored. |
| `SelectedDetent` | TwoWay; set it to animate to a detent. `null` = smallest. |
| `IsDismissible` | `false`: swipe down, tap outside and back at the root do nothing; only the close button or `IsOpen = false` close the sheet. |
| `PassThroughInteraction` | The page below stays interactive at every detent except `Large`. |
| `ShowGrabber`, `IsDraggable` (Android only), `CornerRadius` (-1 = OS default), `BackgroundColor`, `ShowOverflowIcons` (default `true`) | Appearance. |
| `Opened`, `Dismissed`, `DetentChanged` | Lifecycle. `IsOpen` is already `false` in `Dismissed`. |
| `DismissAttempted` | `CancelEventArgs`; `Cancel` starts as `!IsDismissible`. Set `Cancel = true` to keep a dismissible sheet open. |
| `InvalidateDetents()` | Re-measure `FitContent` after size changes deep inside the content (changes of the root view are picked up automatically). |

## Platform behaviour to rely on

- Alerts (`DisplayAlertAsync`, `DisplayActionSheetAsync`), pickers and modal pages (`PushModalAsync`, CommunityToolkit.Maui popups) opened while a sheet is up appear above it. iOS: the plugin presents them from the sheet. Windows: the sheet hides while a modal page opened over it is up.
- Android `Detents="Large"` alone opens full height; the window resizes for the keyboard. At most three distinct detent heights are used on Android.
- iOS 15 knows only medium/large; other detents fall back to the nearer one.
- Windows has no dragging or grabber; detents only choose the card height. Clicking the backdrop or Esc tries to dismiss (Esc goes back first on pushed views).

## Common mistakes

- Missing `UseNativeSheet()`: the sheet never shows and the service is not registered.
- Passing a `Page` as sheet content or to `PushAsync`: not supported, use a `ContentView`.
- Expecting `Popped` after `PopAsync`: it is user-only.
- Relying on `DismissAttempted` to close a non-dismissible sheet: set `IsOpen = false` instead.
- Reusing a sheet returned by `OpenAsync` after it was dismissed.

## Working on this repository

- Library: `src/Plugin.Maui.Sheet`. `Controls/NativeSheet*.cs` is the platform-independent model (properties, navigation stack, header per level); `Handlers/NativeSheetHandler.{android,macios,windows}.cs` are the native implementations, `Handlers/NativeSheetHandler.cs` the shared mapper. Keep the plugin lean: views only, no pages, Shell, DI containers or view-model frameworks.
- Tests: `dotnet test tests/Plugin.Maui.Sheet.Tests` (net11.0, no device needed; they cover the model without handlers).
- Build: `dotnet build src/Plugin.Maui.Sheet/Plugin.Maui.Sheet.csproj`. iOS/Mac Catalyst targets need macOS, Windows needs Windows.
- Sample: `samples/Plugin.Maui.Sheet.Sample`. Its `SAMPLE_SHEET` launch flag (Android intent extra, environment variable elsewhere) opens a given sheet; the [screenshot workflow](.github/workflows/screenshots.yml) uses it to capture `screenshots/` on all platforms.
- Releases: push a `vX.Y.Z` tag; `release-nuget.yml` builds that version (taken from the tag) and publishes it with NuGet Trusted Publishing. Keep `<Version>` in the library's csproj in step for local builds.
- Update README.md and this file when the public API changes.
