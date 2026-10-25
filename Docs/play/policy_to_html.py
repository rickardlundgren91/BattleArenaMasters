#!/usr/bin/env python3
"""Turns Docs/play/privacy-policy.md into the HTML between the POLICY markers of the website's privacy.html.

    python3 Docs/play/policy_to_html.py [path/to/privacy.html]   (default /mnt/project-files/bam-site/privacy.html)

Handles only what the policy uses: # and ## headings, paragraphs, "- " lists, **bold**, _italic_, links and emails.
Refuses to run while {{DEVELOPER_NAME}} is still in the text.
"""
import html
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "privacy-policy.md")
DST = sys.argv[1] if len(sys.argv) > 1 else "/mnt/project-files/bam-site/privacy.html"


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


def main():
    md = open(SRC, encoding="utf-8").read()
    if "{{DEVELOPER_NAME}}" in md:
        sys.exit("privacy-policy.md still has {{DEVELOPER_NAME}}; fill in the developer name first")
    page = open(DST, encoding="utf-8").read()
    start, end = "<!-- POLICY_START -->", "<!-- POLICY_END -->"
    i, j = page.index(start) + len(start), page.index(end)
    page = page[:i] + "\n" + convert(md) + "\n" + page[j:]
    open(DST, "w", encoding="utf-8").write(page)
    print("wrote policy into", DST)


if __name__ == "__main__":
    main()
