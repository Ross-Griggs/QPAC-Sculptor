"""Builds docs/QPAC.Sculptor-Walkthrough.pdf.

    pip install reportlab numpy pillow matplotlib
    python docs/walkthrough/build.py

1. Generates harness/Snippets.g.cs from the examples in content.py.
2. Runs the harness (compiled against src/QPAC.Sculptor), which executes every example and dumps
   its geometry. Any failing example stops the build, so the PDF only contains working code.
3. Renders each model with a small z-buffer renderer and each profile with matplotlib.
4. Lays out the PDF with reportlab.
"""
import json
import os
import re
import subprocess
import textwrap
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import content  # noqa: E402

OUT = os.path.join(HERE, "out")
IMG = os.path.join(OUT, "img")
PDF = os.path.join(HERE, "..", "QPAC.Sculptor-Walkthrough.pdf")


# ---------------------------------------------------------------- 1. generate + run examples
def items():
    for s in content.SECTIONS:
        for it in s["items"]:
            yield it


def generate():
    lines = ["using QPAC.Sculptor;", "", "namespace Walkthrough;", "", "public static class Snippets", "{"]
    n = 0
    for it in items():
        if it["kind"] not in ("model", "profile", "code"):
            continue
        n += 1
        ret = {"model": "return file;", "profile": "return profile;", "code": "return null;"}[it["kind"]]
        lines.append(f"    public static object Ex{n:03d}_{it['id']}()")
        lines.append("    {")
        lines += ["        " + l for l in it["code"].split("\n")]
        lines.append("        " + ret)
        lines.append("    }")
        lines.append("")
    lines.append("}")
    with open(os.path.join(HERE, "harness", "Snippets.g.cs"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


def run_harness():
    os.makedirs(OUT, exist_ok=True)
    r = subprocess.run(["dotnet", "run", "--project", os.path.join(HERE, "harness"), "--", OUT],
                       capture_output=True, text=True)
    print(r.stdout[-4000:])
    if r.returncode != 0:
        print(r.stderr[-4000:])
        sys.exit("An example failed; fix content.py before building the PDF.")


# ---------------------------------------------------------------- 2. rendering
LIGHT = np.array([-0.35, -0.55, 0.76])
LIGHT /= np.linalg.norm(LIGHT)


def view_matrix(elev, azim):
    # Rows: screen right, screen up, toward the viewer (larger = closer). Same angles as matplotlib.
    a, e = np.radians(azim), np.radians(elev)
    d = np.array([np.cos(e) * np.cos(a), np.cos(e) * np.sin(a), np.sin(e)])
    right = np.array([-np.sin(a), np.cos(a), 0.0])
    up = np.cross(d, right)
    return np.array([right, up, d])


def render_model(data, path, elev=24, azim=-55, size=(1500, 960)):
    tris = []
    cols = []
    for s in data["solids"]:
        t = np.array(s["tris"], dtype=float).reshape(-1, 3, 3)
        tris.append(t)
        cols.append(np.repeat(np.array([s["color"]], dtype=float) / 255.0, len(t), axis=0))
    if not tris:
        return False
    T = np.concatenate(tris)
    C = np.concatenate(cols)
    E = np.array(data["edges"], dtype=float).reshape(-1, 2, 3) if data["edges"] else np.zeros((0, 2, 3))

    nrm = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    ln = np.linalg.norm(nrm, axis=1, keepdims=True)
    nrm = nrm / np.where(ln == 0, 1, ln)
    diff = np.clip(nrm @ LIGHT, 0, 1)
    shade = (0.42 + 0.58 * diff)[:, None]
    Cs = np.clip(C * shade + 0.10 * diff[:, None] ** 8, 0, 1)

    M = view_matrix(elev, azim)
    P = T @ M.T
    PE = E @ M.T
    lo = P.reshape(-1, 3).min(axis=0)
    hi = P.reshape(-1, 3).max(axis=0)
    W, H = size
    margin = 0.06
    scale = min(W * (1 - 2 * margin) / max(hi[0] - lo[0], 1e-9), H * (1 - 2 * margin) / max(hi[1] - lo[1], 1e-9))
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2

    def screen(p):
        return np.stack([(p[..., 0] - cx) * scale + W / 2, H / 2 - (p[..., 1] - cy) * scale, p[..., 2]], axis=-1)

    S = screen(P)
    SE = screen(PE)
    zbuf = np.full((H, W), -np.inf)
    img = np.ones((H, W, 3))

    for k in range(len(S)):
        a, b, c = S[k]
        x0 = int(max(0, np.floor(min(a[0], b[0], c[0]))))
        x1 = int(min(W - 1, np.ceil(max(a[0], b[0], c[0]))))
        y0 = int(max(0, np.floor(min(a[1], b[1], c[1]))))
        y1 = int(min(H - 1, np.ceil(max(a[1], b[1], c[1]))))
        if x1 < x0 or y1 < y0:
            continue
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        if not inside.any():
            continue
        z = w0 * a[2] + w1 * b[2] + w2 * c[2]
        sub = zbuf[y0:y1 + 1, x0:x1 + 1]
        upd = inside & (z > sub)
        sub[upd] = z[upd]
        img[y0:y1 + 1, x0:x1 + 1][upd] = Cs[k]

    # Outlines: sample each sharp edge and draw where it isn't hidden.
    bias = 0.004 * np.linalg.norm(hi - lo)
    ink = np.array([0.13, 0.15, 0.17])
    for p, q in SE:
        n = int(max(abs(q[0] - p[0]), abs(q[1] - p[1])) * 1.5) + 2
        t = np.linspace(0, 1, n)[:, None]
        pts = p + (q - p) * t
        xi = np.clip(pts[:, 0].astype(int), 0, W - 1)
        yi = np.clip(pts[:, 1].astype(int), 0, H - 1)
        vis = pts[:, 2] >= zbuf[yi, xi] - bias
        for dx, dy in ((0, 0), (1, 0), (0, 1)):
            img[np.clip(yi[vis] + dy, 0, H - 1), np.clip(xi[vis] + dx, 0, W - 1)] = ink

    im = Image.fromarray((img * 255).astype(np.uint8))
    im = im.resize((W // 2, H // 2), Image.LANCZOS)
    bg = Image.new("RGB", im.size, (255, 255, 255))
    from PIL import ImageChops
    box = ImageChops.difference(im, bg).getbbox()
    if box:
        pad = 14
        im = im.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(im.width, box[2] + pad), min(im.height, box[3] + pad)))
    im.save(path)
    return True


def render_profile(data, path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    loop = np.array(data["loop"])
    closed = np.vstack([loop, loop[:1]])
    fig, ax = plt.subplots(figsize=(5.2, 3.2), dpi=160)
    ax.fill(closed[:, 0], closed[:, 1], color="#dfe7ee")
    ax.plot(closed[:, 0], closed[:, 1], color="#1f4e79", lw=1.4)
    ax.plot(loop[:, 0], loop[:, 1], "o", color="#1f4e79", ms=2.2)
    ax.plot([0], [0], "+", color="#c0392b", ms=9, mew=1.4)
    ax.set_aspect("equal")
    ax.grid(True, color="#e6e6e6", lw=0.6)
    ax.set_xlabel("U")
    ax.set_ylabel("V")
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)
    return True


def render_all():
    os.makedirs(IMG, exist_ok=True)
    n = 0
    for it in items():
        if it["kind"] not in ("model", "profile", "code"):
            continue
        n += 1
        js = os.path.join(OUT, f"{it['id']}.json")
        it["output"] = open(os.path.join(OUT, f"{it['id']}.txt"), encoding="utf-8").read().strip()
        if not os.path.exists(js):
            continue
        data = json.load(open(js, encoding="utf-8"))
        png = os.path.join(IMG, f"{it['id']}.png")
        ok = render_profile(data, png) if "loop" in data else render_model(data, png, **it.get("view", {}))
        if ok:
            it["image"] = png
        print("rendered", it["id"])


# ---------------------------------------------------------------- 3. PDF
def build_pdf():
    from reportlab.lib import colors
    from reportlab.lib.enums import TA_LEFT
    from reportlab.lib.pagesizes import letter
    from reportlab.lib.styles import ParagraphStyle
    from reportlab.lib.units import inch
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.platypus import (BaseDocTemplate, CondPageBreak, Frame, Image as RLImage, KeepTogether,
                                    NextPageTemplate, PageBreak, PageTemplate, Paragraph, Spacer, Table,
                                    TableStyle, XPreformatted)
    from reportlab.platypus.tableofcontents import TableOfContents

    fonts = r"C:\Windows\Fonts"
    pdfmetrics.registerFont(TTFont("Body", os.path.join(fonts, "segoeui.ttf")))
    pdfmetrics.registerFont(TTFont("Body-Bold", os.path.join(fonts, "segoeuib.ttf")))
    pdfmetrics.registerFont(TTFont("Body-Italic", os.path.join(fonts, "segoeuii.ttf")))
    pdfmetrics.registerFont(TTFont("Mono", os.path.join(fonts, "consola.ttf")))
    pdfmetrics.registerFont(TTFont("Mono-Bold", os.path.join(fonts, "consolab.ttf")))
    from reportlab.pdfbase.pdfmetrics import registerFontFamily
    registerFontFamily("Body", normal="Body", bold="Body-Bold", italic="Body-Italic", boldItalic="Body-Bold")
    registerFontFamily("Mono", normal="Mono", bold="Mono-Bold", italic="Mono", boldItalic="Mono-Bold")

    navy = colors.HexColor("#1f3a5f")
    accent = colors.HexColor("#2f6db3")
    grey = colors.HexColor("#5b6670")
    codebg = colors.HexColor("#f4f6f8")
    rule = colors.HexColor("#d5dbe1")

    st = {
        "title": ParagraphStyle("title", fontName="Body-Bold", fontSize=30, leading=36, textColor=navy),
        "subtitle": ParagraphStyle("subtitle", fontName="Body", fontSize=14, leading=20, textColor=grey),
        "part": ParagraphStyle("part", fontName="Body-Bold", fontSize=20, leading=26, textColor=navy, spaceAfter=8),
        "item": ParagraphStyle("item", fontName="Body-Bold", fontSize=13.5, leading=18, textColor=accent,
                               spaceBefore=14, spaceAfter=4),
        "body": ParagraphStyle("body", fontName="Body", fontSize=9.6, leading=13.6, spaceAfter=5, alignment=TA_LEFT),
        "label": ParagraphStyle("label", fontName="Body-Bold", fontSize=8.2, leading=11, textColor=grey,
                                spaceBefore=5, spaceAfter=2),
        "note": ParagraphStyle("note", fontName="Body", fontSize=8.9, leading=12.4, leftIndent=12, bulletIndent=2,
                               spaceAfter=2.5),
        "cell": ParagraphStyle("cell", fontName="Body", fontSize=8.5, leading=11.2),
        "cellmono": ParagraphStyle("cellmono", fontName="Mono", fontSize=8.1, leading=11),
        "toc1": ParagraphStyle("toc1", fontName="Body-Bold", fontSize=10.5, leading=15, leftIndent=0),
        "toc2": ParagraphStyle("toc2", fontName="Body", fontSize=9, leading=12, leftIndent=14),
    }

    PAGE_W, PAGE_H = letter
    MARGIN = 0.65 * inch
    TEXT_W = PAGE_W - 2 * MARGIN

    def esc(s):
        return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    KEYWORDS = r"\b(var|new|for|foreach|in|int|double|string|return|using|try|catch|if|true|false|null|out)\b"

    def highlight(code):
        out = []
        for line in code.split("\n"):
            m = re.search(r"//.*$", line)
            body, comment = (line[:m.start()], line[m.start():]) if m and line[:m.start()].count('"') % 2 == 0 else (line, "")
            parts = re.split(r'(\$?"(?:[^"\\]|\\.)*")', body)
            html = ""
            for i, ptxt in enumerate(parts):
                if i % 2 == 1:
                    html += f'<font color="#a3501c">{esc(ptxt)}</font>'
                else:
                    html += re.sub(KEYWORDS, r'<font color="#1d4fb8">\1</font>', esc(ptxt))
            if comment:
                html += f'<font color="#3d7a3d">{esc(comment)}</font>'
            out.append(html)
        return "\n".join(out)

    def code_block(code, shell=False):
        longest = max(len(l) for l in code.split("\n"))
        size = min(8.2, (TEXT_W - 16) / (0.55 * max(longest, 1)))
        style = ParagraphStyle("code", fontName="Mono", fontSize=size, leading=size * 1.32)
        text = esc(code) if shell else highlight(code)
        t = Table([[XPreformatted(text, style)]], colWidths=[TEXT_W])
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), codebg), ("BOX", (0, 0), (-1, -1), 0.5, rule),
            ("LEFTPADDING", (0, 0), (-1, -1), 7), ("RIGHTPADDING", (0, 0), (-1, -1), 7),
            ("TOPPADDING", (0, 0), (-1, -1), 5), ("BOTTOMPADDING", (0, 0), (-1, -1), 5)]))
        return t

    def sig_block(sig):
        longest = max(len(l) for l in sig.split("\n"))
        size = min(8.4, (TEXT_W - 16) / (0.55 * max(longest, 1)))
        style = ParagraphStyle("sig", fontName="Mono-Bold", fontSize=size, leading=size * 1.35, textColor=navy)
        t = Table([[XPreformatted(esc(sig), style)]], colWidths=[TEXT_W])
        t.setStyle(TableStyle([("LINEBEFORE", (0, 0), (0, -1), 2.2, accent),
                               ("LEFTPADDING", (0, 0), (-1, -1), 8), ("TOPPADDING", (0, 0), (-1, -1), 2),
                               ("BOTTOMPADDING", (0, 0), (-1, -1), 2)]))
        return t

    def two_col(rows, left_mono=True, widths=(0.3, 0.7)):
        data = [[Paragraph(esc(a), st["cellmono"] if left_mono else st["cell"]), Paragraph(esc(b), st["cell"])] for a, b in rows]
        t = Table(data, colWidths=[TEXT_W * widths[0], TEXT_W * widths[1]])
        t.setStyle(TableStyle([
            ("VALIGN", (0, 0), (-1, -1), "TOP"), ("LINEBELOW", (0, 0), (-1, -1), 0.4, rule),
            ("LEFTPADDING", (0, 0), (-1, -1), 4), ("RIGHTPADDING", (0, 0), (-1, -1), 4),
            ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3)]))
        return t

    class Doc(BaseDocTemplate):
        def afterFlowable(self, f):
            if isinstance(f, Paragraph) and getattr(f, "toc_level", None) is not None:
                key = f"k{id(f)}"
                self.canv.bookmarkPage(key)
                self.canv.addOutlineEntry(f.getPlainText(), key, level=f.toc_level, closed=f.toc_level > 0)
                self.notify("TOCEntry", (f.toc_level, f.getPlainText(), self.page, key))

    def footer(canv, doc):
        canv.saveState()
        canv.setFont("Body", 7.8)
        canv.setFillColor(grey)
        canv.drawString(MARGIN, 0.42 * inch, f"QPAC.Sculptor {content.VERSION} - Building STEP files from scratch")
        canv.drawRightString(PAGE_W - MARGIN, 0.42 * inch, str(doc.page))
        canv.setStrokeColor(rule)
        canv.line(MARGIN, 0.58 * inch, PAGE_W - MARGIN, 0.58 * inch)
        canv.restoreState()

    def cover(canv, doc):
        canv.saveState()
        canv.setFillColor(navy)
        canv.rect(0, PAGE_H - 0.5 * inch, PAGE_W, 0.5 * inch, stroke=0, fill=1)
        canv.restoreState()

    doc = Doc(PDF, pagesize=letter, leftMargin=MARGIN, rightMargin=MARGIN, topMargin=MARGIN, bottomMargin=0.8 * inch,
              title="QPAC.Sculptor - Building STEP files from scratch", author="Q-PAC",
              subject=f"Function-by-function walkthrough, QPAC.Sculptor {content.VERSION}")
    frame = Frame(MARGIN, 0.8 * inch, TEXT_W, PAGE_H - MARGIN - 0.8 * inch, id="f")
    doc.addPageTemplates([PageTemplate("cover", [frame], onPage=cover), PageTemplate("body", [frame], onPage=footer)])

    story = []
    story += [Spacer(1, 1.6 * inch), Paragraph("QPAC.Sculptor", st["title"]), Spacer(1, 6),
              Paragraph("Building STEP files from scratch", ParagraphStyle("t2", parent=st["title"], fontSize=22, leading=28, textColor=accent)),
              Spacer(1, 14),
              Paragraph(f"A function-by-function walkthrough for version {content.VERSION}, with a tested example and a "
                        "rendered result for every function.", st["subtitle"]),
              Spacer(1, 0.4 * inch)]
    if os.path.exists(os.path.join(IMG, "full.png")):
        story.append(RLImage(os.path.join(IMG, "full.png"), width=TEXT_W, height=TEXT_W * 0.64))
    story += [NextPageTemplate("body"), PageBreak()]

    story.append(Paragraph("About this walkthrough", st["part"]))
    for p in content.INTRO:
        story.append(Paragraph(esc(p), st["body"]))
    story.append(Spacer(1, 8))
    story.append(Paragraph("How a model is put together", st["label"]))
    story.append(code_block("StepFile                      the document: one .step file (Units, Author, Description)\n"
                            " +- parts       List<StepPart>    components in the CAD tree, placed where drawn\n"
                            " |    +- shapes  List<StepShape>  the solids: boxes, extrusions, sweeps ...\n"
                            " +- instances   List<StepInstance> extra placed copies of a part", shell=True))
    story.append(Paragraph(
        "Each shape is turned into a closed solid made of flat polygon faces (a Polyhedron). The STEP writer and the "
        "WPF preview both draw from that one solid. In the pictures in this document, solids are drawn in their "
        "STEP colours, and grey stands for shapes with no colour. Dark lines mark edges where faces meet at more than "
        "30 degrees.", st["body"]))
    story.append(Spacer(1, 10))
    story.append(Paragraph("Contents", st["label"]))
    toc = TableOfContents()
    toc.levelStyles = [st["toc1"], st["toc2"]]
    story.append(toc)
    story.append(PageBreak())

    def heading(text, style, level):
        p = Paragraph(esc(text), style)
        p.toc_level = level
        return p

    for sec in content.SECTIONS:
        story.append(heading(sec["title"], st["part"], 0))
        for p in sec["intro"]:
            story.append(Paragraph(esc(p), st["body"]))
        for it in sec["items"]:
            head = [heading(it["title"], st["item"], 1)]
            if it.get("what"):
                head.append(Paragraph(esc(it["what"]), st["body"]))
            story.append(CondPageBreak(2.6 * inch))
            story.append(KeepTogether(head))
            if it.get("signature"):
                story.append(sig_block(it["signature"]))
                story.append(Spacer(1, 4))
            if it.get("params"):
                story.append(Paragraph("PARAMETERS", st["label"]))
                story.append(two_col(it["params"]))
            if it.get("shell"):
                story.append(Paragraph("COMMANDS", st["label"]))
                story.append(code_block(it["shell"], shell=True))
            if it.get("code"):
                story.append(Paragraph("EXAMPLE" if it["kind"] != "wpf" else "EXAMPLE (WPF)", st["label"]))
                story.append(code_block(it["code"]))
            if it.get("output"):
                wrapped = "\n".join(w for line in it["output"].split("\n") for w in (textwrap.wrap(line, 104) or [""]))
                story.append(KeepTogether([Paragraph("OUTPUT", st["label"]), code_block(wrapped, shell=True)]))
            if it.get("image"):
                im = Image.open(it["image"])
                w = TEXT_W * (1.0 if it["id"] == "full" else 0.72 if it["kind"] == "model" else 0.55)
                h = w * im.height / im.width
                max_h = 3.3 * inch if it["id"] != "full" else 5.2 * inch
                if h > max_h:
                    w, h = w * max_h / h, max_h
                story.append(KeepTogether([Paragraph("RESULT", st["label"]), RLImage(it["image"], width=w, height=h)]))
            if it.get("table"):
                story.append(Spacer(1, 4))
                story.append(two_col(it["table"], left_mono=False, widths=(0.34, 0.66)))
            for n in it.get("notes", []):
                story.append(Paragraph(esc(n), st["note"], bulletText="\u2022"))
        story.append(PageBreak())

    story.append(heading("Troubleshooting", st["part"], 0))
    story.append(two_col(content.TROUBLESHOOTING, left_mono=False, widths=(0.4, 0.6)))
    story.append(PageBreak())

    story.append(heading("Review findings and suggestions", st["part"], 0))
    story.append(Paragraph(esc(content.SUGGESTIONS_INTRO), st["body"]))
    for i, (title, text) in enumerate(content.SUGGESTIONS, 1):
        story.append(KeepTogether([Paragraph(f"{i}. {esc(title)}", ParagraphStyle("sg", parent=st["item"], fontSize=11, spaceBefore=8)),
                                   Paragraph(esc(text), st["body"])]))

    doc.multiBuild(story)
    print("wrote", os.path.abspath(PDF))


if __name__ == "__main__":
    generate()
    if "--no-run" not in sys.argv:
        run_harness()
    render_all()
    build_pdf()
