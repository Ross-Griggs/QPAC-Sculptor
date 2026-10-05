# QPAC.Sculptor

A lightweight .NET library for building 3D solid geometry in code and exporting it
to **STEP** (ISO 10303-21) CAD files.

- Compose a model from `StepPart`s built out of primitive shapes.
- Apply rigid transforms — rotate any shape or whole part about an arbitrary axis with `RotateAbout`.
- Write a `.step` file that any CAD package can open.

Targets `net7.0` and `netstandard2.0`.

## Install

```
dotnet add package QPAC.Sculptor
```

## Quick start

```csharp
using QPAC.Sculptor;

var file = new StepFile();
var part = file.AddPart(new StepPart("Widget"));

// add shapes to the part, transform them as needed …
part.RotateAbout((0, 0, 0), (0, 0, 1), 90);

Step.WriteStep("widget.step", file, "Widget");
```

## Related

- [`QPAC.Sculptor.Wpf`](https://www.nuget.org/packages/QPAC.Sculptor.Wpf) — render
  `QPAC.Sculptor` geometry as a HelixToolkit `Visual3D` in WPF apps.

## License

MIT
