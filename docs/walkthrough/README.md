# Walkthrough PDF

`docs/QPAC.Sculptor-Walkthrough.pdf` is generated from this folder:

| File | Contains |
|---|---|
| `content.py` | All text, parameter tables and code examples |
| `build.py` | Runs every example, renders the pictures, lays out the PDF |
| `harness/` | Console app compiled against `src/QPAC.Sculptor` that executes the examples |

```
pip install reportlab numpy pillow matplotlib
python docs/walkthrough/build.py
```

The build compiles and runs every `model`, `profile` and `code` example and stops if any fails,
so the PDF can't show broken code. Pictures come from the same geometry the STEP writer exports.
WPF examples (`kind: "wpf"`) are not run by the build. Check them by hand against
QPAC.Sculptor.Wpf when that API changes.

To add a function: append an item to the right `section(...)` in `content.py`, rebuild, and look
at the page. Bump `VERSION` when the library version changes.
