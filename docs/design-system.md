# SyncForge Design System

Diese Dokumentation beschreibt das zentrale UI-Design-System des `SyncForge.Configurator`.

## Architektur

Die Styles sind zentral unter `Styles/` organisiert:

```text
Styles/
  SyncForgeTheme.axaml
  Tokens.axaml
  Controls/
    Button.axaml
    TextBox.axaml
    ComboBox.axaml
    ListBox.axaml
    ScrollBar.axaml
```

- `SyncForgeTheme.axaml`: zentrale Einbindung aller Theme- und Control-Styles.
- `Tokens.axaml`: Design-Tokens (Color + Brush Ressourcen).
- `Controls/*.axaml`: control-spezifische Styles und Templates.

## Design Tokens

### Semantische Farben

- `SfColor.Primary`
- `SfColor.Secondary`
- `SfColor.Accent`
- `SfColor.Danger`
- `SfColor.Success`
- `SfColor.Warning`

### Neutral Scale

- `SfColor.Neutral100`
- `SfColor.Neutral200`
- `SfColor.Neutral300`
- `SfColor.Neutral400`
- `SfColor.Neutral500`
- `SfColor.Neutral600`
- `SfColor.Neutral700`
- `SfColor.Neutral800`
- `SfColor.Neutral900`

### Rollen und Zustands-Tokens

- `SfColor.Surface`
- `SfColor.SurfaceAlt`
- `SfColor.Text`
- `SfColor.TextMuted`
- `SfColor.PrimaryHover`
- `SfColor.PrimaryPressed`

### Wichtige Brush-Ressourcen

- `SfBrush.Primary`, `SfBrush.PrimaryHover`, `SfBrush.PrimaryPressed`
- `SfBrush.Secondary`, `SfBrush.Accent`
- `SfBrush.Danger`, `SfBrush.Success`, `SfBrush.Warning`
- `SfBrush.Surface`, `SfBrush.SurfaceAlt`, `SfBrush.Background`
- `SfBrush.Text`, `SfBrush.TextMuted`
- `SfBrush.Border`
- `SfBrush.Selection`, `SfBrush.SelectionHover`, `SfBrush.SelectionForeground`

## Control-Klassen

### Buttons

Definiert in `Styles/Controls/Button.axaml`.

- `Button.primary`: Primaraction
- `Button.secondary`: neutrale Aktion
- `Button.danger`: destruktive Aktion

Alle Varianten enthalten konsistente Zustande: `Normal`, `:pointerover`, `:pressed`, `:disabled`.

### TextBox

Definiert in `Styles/Controls/TextBox.axaml`.

- Fokuszustand: `TextBox:focus`
- Disabled: `TextBox:disabled`
- Validation-ready Klassen:
  - `TextBox.invalid`
  - `TextBox.warning`
  - `TextBox.success`

### ListBox und ComboBox

Definiert in:

- `Styles/Controls/ListBox.axaml`
- `Styles/Controls/ComboBox.axaml`

Konsistente Selektions- und Hover-Farben via `SfBrush.Selection*`.

### ScrollBar

Definiert in `Styles/Controls/ScrollBar.axaml`.

Thumb hat konsistente Zustande fuer `Normal`, `:pointerover`, `:pressed`.

## View-Klassen

Definiert in `SyncForgeTheme.axaml` und in Views nutzbar:

- `Border.panel`
- `Border.panel-subtle`
- `Border.statusbar`
- `TextBlock.section-title`
- `TextBlock.muted`

## Naming-Konventionen

- Prefix `SfColor.*` fuer Color Tokens.
- Prefix `SfBrush.*` fuer Brush Tokens.
- Control-Klassen sind semantisch, nicht farbbasiert (`primary`, `secondary`, `danger`).
- View-Klassen beschreiben Rolle, nicht Darstellung (`panel`, `section-title`, `muted`).

## Usage Guidelines

- Keine Hex-Farben in Views verwenden.
- Keine lokalen `Window.Styles` in Views ohne zwingenden Grund.
- Neue Controls zuerst in `Styles/Controls/*.axaml` stylen, dann zentral in `SyncForgeTheme.axaml` einbinden.
- In Views nur Klassen und `DynamicResource` verwenden.

## Beispiel

```xml
<Button Classes="primary" Content="Run" />
<TextBlock Classes="section-title" Text="Run Summary" />
<Border Classes="panel-subtle">
  <TextBox Text="{Binding Value}" />
</Border>
```
