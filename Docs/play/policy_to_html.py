#!/usr/bin/env python3
"""Turns Docs/play/privacy-policy.md into the HTML of the website's privacy.html.

    python3 Docs/play/policy_to_html.py [path/to/privacy.html]

Default target: the public site repo's public/privacy.html (the page Cloudflare serves), else the old bam-site copy.
The page is updated in one of two ways:
- With <!-- POLICY_START --> and <!-- POLICY_END --> markers: everything between them is replaced by the whole policy.
- Without markers (the public site's page has none; full review 2026-11-09 nr 92): the policy block inside <main>...</main>
  is replaced. The page keeps its own <h1>; its <p class="meta">Last updated ...</p> line takes the date from the policy's
  "_Last updated: ..._" line, and the policy's own title and date lines are left out. Without an <h1> and a meta line the
  block starts at the first policy heading.

Handles only what the policy uses: # and ## headings, paragraphs, "- " lists, **bold**, _italic_, links and emails.
Refuses to run while {{DEVELOPER_NAME}} is still in the text.
"""
import html
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "privacy-policy.md")
TARGETS = ["/home/user/battlearenamasters/public/privacy.html", "/mnt/project-files/bam-site/privacy.html"]
DST = sys.argv[1] if len(sys.argv) > 1 else next((p for p in TARGETS if os.path.exists(p)), TARGETS[0])
MARK_START, MARK_END = "<!-- POLICY_START -->", "<!-- POLICY_END -->"


def inline(s):
    s = html.escape(s, quote=False)
    s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"(?<![\w/])_(.+?)_(?!\w)", r"<em>\1</em>", s)
    s = re.sub(r"(https?://[^\s<,)]+[^\s<,).])", r'<a href="\1">\1</a>', s)
    s = re.sub(r"(?<![\w/>])(www\.[^\s<,)]+[^\s<,).])", r'<a href="https://\1">\1</a>', s)
    s = re.sub(r"(?<![\w.:/>])([\w.+-]+@[\w-]+\.[\w.]+[a-z])", r'<a href="mailto:\1">\1</a>', s)
    return s


def convert(md):
    out, para, items = [], [], []

    def flush():
        if para:
            out.append("<p>" + inline(" ".join(para)) + "</p>")
            para.clear()
        if items:
            out.append("<ul>\n" + "\n".join("  <li>" + inline(i) + "</li>" for i in items) + "\n</ul>")
            items.clear()

    for raw in md.splitlines():
        line = raw.rstrip()
        if not line.strip():
            flush()
        elif line.startswith("## "):
            flush(); out.append("<h2>" + inline(line[3:]) + "</h2>")
        elif line.startswith("# "):
            flush(); out.append("<h1>" + inline(line[2:]) + "</h1>")
        elif line.startswith("- "):
            if para: flush()
            items.append(line[2:])
        elif line.startswith("  ") and items:
            items[-1] += " " + line.strip()
        else:
            if items: flush()
            para.append(line.strip())
    flush()
    return "\n".join(out)


def split_head(md):
    """The policy's title line and its "_Last updated: ..._" line (both None when missing), and the text without them."""
    title, dated, body = None, None, []
    for line in md.splitlines():
        if title is None and line.startswith("# "):
            title = line[2:].strip()
        elif dated is None and re.fullmatch(r"_Last updated:.*_", line.strip()):
            dated = line.strip().strip("_")
        else:
            body.append(line)
    return title, dated, "\n".join(body)


def splice(page, md):
    """The page with the policy put in: between the markers when the page has them, else inside <main>."""
    if MARK_START in page and MARK_END in page:
        i, j = page.index(MARK_START) + len(MARK_START), page.index(MARK_END)
        return page[:i] + "\n" + convert(md) + "\n" + page[j:]
    m = re.search(r"<main\b[^>]*>", page)
    if not m:
        sys.exit(DST + " has neither the POLICY markers nor a <main> element; nothing written")
    end = page.index("</main>", m.end())
    inner = page[m.end():end]
    _, dated, body = split_head(md)
    start = 0
    h1 = re.search(r"</h1>\s*", inner)
    if h1:
        start = h1.end()
    meta = re.compile(r'<p class="meta">(.*?)</p>\s*').search(inner, start)
    if meta:
        if dated:
            inner = inner[:meta.start(1)] + inline(dated) + inner[meta.end(1):]
            meta = re.compile(r'<p class="meta">(.*?)</p>\s*').search(inner, start)
        start = meta.end()
    elif not h1:
        first = inner.find("<h2>")
        start = first if first >= 0 else 0
    return page[:m.end()] + inner[:start] + convert(body) + "\n" + page[end:]


def main():
    md = open(SRC, encoding="utf-8").read()
    if "{{DEVELOPER_NAME}}" in md:
        sys.exit("privacy-policy.md still has {{DEVELOPER_NAME}}; fill in the developer name first")
    page = open(DST, encoding="utf-8").read()
    page = splice(page, md)
    open(DST, "w", encoding="utf-8").write(page)
    print("wrote policy into", DST)


if __name__ == "__main__":
    main()
