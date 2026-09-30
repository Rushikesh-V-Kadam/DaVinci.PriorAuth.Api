"""Builds DaVinci_PriorAuth_Implementation_Guide.pdf from the project source (python3 tools/build_guide.py)."""
import json, os, textwrap
from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak, Table,
                                TableStyle, Preformatted, KeepTogether, CondPageBreak)
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.graphics.shapes import Drawing, Line, Rect, String, Polygon
from reportlab.lib.enums import TA_CENTER

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "src", "DaVinci.PriorAuth.Api")
OUT = os.path.join(ROOT, "DaVinci_PriorAuth_Implementation_Guide.pdf")

NAVY = colors.HexColor("#1F3A5F"); TEAL = colors.HexColor("#2E7D8C"); LIGHT = colors.HexColor("#EEF3F7")
CODEBG = colors.HexColor("#F6F7F9"); AMBER = colors.HexColor("#FFF6E0"); GREEN = colors.HexColor("#E8F5EC")
GREY = colors.HexColor("#555555")

ss = getSampleStyleSheet()
H1 = ParagraphStyle("H1", parent=ss["Heading1"], textColor=NAVY, fontSize=17, leading=21, spaceBefore=4, spaceAfter=8)
H2 = ParagraphStyle("H2", parent=ss["Heading2"], textColor=TEAL, fontSize=12.5, leading=16, spaceBefore=10, spaceAfter=4)
H3 = ParagraphStyle("H3", parent=ss["Heading3"], textColor=NAVY, fontSize=10.5, leading=13, spaceBefore=8, spaceAfter=3)
B = ParagraphStyle("B", parent=ss["BodyText"], fontSize=9.2, leading=13, spaceAfter=4)
BL = ParagraphStyle("BL", parent=B, leftIndent=12, bulletIndent=2, spaceAfter=2)
SM = ParagraphStyle("SM", parent=B, fontSize=8, leading=10.2, spaceAfter=0)
SMB = ParagraphStyle("SMB", parent=SM, fontName="Helvetica-Bold")
NOTE = ParagraphStyle("NOTE", parent=B, backColor=AMBER, borderPadding=5, borderColor=colors.HexColor("#E8C36A"),
                      borderWidth=0.5, spaceBefore=4, spaceAfter=8)
TIP = ParagraphStyle("TIP", parent=NOTE, backColor=GREEN, borderColor=colors.HexColor("#8CC9A0"))
CODE = ParagraphStyle("CODE", fontName="Courier", fontSize=6.6, leading=8.1, backColor=CODEBG,
                      borderColor=colors.HexColor("#DDDDDD"), borderWidth=0.4, borderPadding=4,
                      leftIndent=3, rightIndent=3, spaceBefore=3, spaceAfter=7)
FILEHDR = ParagraphStyle("FILEHDR", parent=SM, fontName="Courier-Bold", textColor=colors.white, backColor=NAVY,
                         borderPadding=(3, 4, 3, 4), spaceBefore=6, spaceAfter=0, fontSize=7.8)
TITLE = ParagraphStyle("T", parent=ss["Title"], textColor=NAVY, fontSize=23, leading=29)
SUB = ParagraphStyle("S", parent=B, fontSize=12, leading=16, textColor=GREY)
TOC1 = ParagraphStyle("TOC1", fontSize=9.5, leading=13, leftIndent=6, fontName="Helvetica-Bold")
TOC2 = ParagraphStyle("TOC2", fontSize=8.6, leading=11, leftIndent=20)

story = []
WRAP = 118


class Doc(BaseDocTemplate):
    def __init__(self, path):
        super().__init__(path, pagesize=A4, leftMargin=17 * mm, rightMargin=17 * mm, topMargin=16 * mm, bottomMargin=15 * mm,
                         title="Da Vinci Prior Authorization API (.NET) - Implementation Guide",
                         author="Prepared for Rushikesh Kadam", subject="CRD 2.0.1, DTR 2.0.1, PAS 2.0.1, CDex 2.0.0")
        frame = Frame(self.leftMargin, self.bottomMargin, self.width, self.height, id="f")
        self.addPageTemplates([PageTemplate(id="p", frames=[frame], onPage=self.decorate)])

    def decorate(self, c, d):
        if d.page == 1:
            return
        c.saveState()
        c.setFont("Helvetica", 7.3); c.setFillColor(colors.grey)
        c.drawString(17 * mm, 9 * mm, "Da Vinci Prior Authorization API (.NET 8): CRD 2.0.1 | DTR 2.0.1 | PAS 2.0.1 | CDex 2.0.0")
        c.drawRightString(A4[0] - 17 * mm, 9 * mm, f"Page {d.page}")
        c.setStrokeColor(TEAL); c.setLineWidth(0.6); c.line(17 * mm, A4[1] - 11 * mm, A4[0] - 17 * mm, A4[1] - 11 * mm)
        c.restoreState()

    def afterFlowable(self, f):
        if isinstance(f, Paragraph) and f.style.name in ("H1", "H2"):
            level = 0 if f.style.name == "H1" else 1
            text = f.getPlainText()
            key = f"k{id(f)}"
            self.canv.bookmarkPage(key)
            self.canv.addOutlineEntry(text, key, level=level)
            self.notify("TOCEntry", (level, text, self.page, key))


def h1(t): story.append(CondPageBreak(60 * mm)); story.append(Paragraph(t, H1))
def h2(t): story.append(CondPageBreak(35 * mm)); story.append(Paragraph(t, H2))
def h3(t): story.append(Paragraph(t, H3))
def p(t): story.append(Paragraph(t, B))
def note(t): story.append(Paragraph(t, NOTE))
def tip(t): story.append(Paragraph(t, TIP))
def bullets(items):
    for i in items:
        story.append(Paragraph(i, BL, bulletText="•"))
    story.append(Spacer(1, 3))
def steps(items):
    for n, i in enumerate(items, 1):
        story.append(Paragraph(i, BL, bulletText=f"{n}."))
    story.append(Spacer(1, 3))


def wrap_code(text):
    out = []
    for line in text.replace("\t", "    ").rstrip("\n").split("\n"):
        if len(line) <= WRAP:
            out.append(line); continue
        indent = len(line) - len(line.lstrip())
        first, rest = line[:WRAP], line[WRAP:]
        out.append(first)
        while rest:
            chunk = rest[:WRAP - indent - 6]
            out.append(" " * indent + "    » " + chunk)
            rest = rest[len(chunk):]
    return "\n".join(out)


def code(text):
    story.append(Preformatted(wrap_code(text), CODE))


def src(rel, title=None):
    path = os.path.join(SRC, rel) if not rel.startswith("/") else rel
    with open(path, encoding="utf-8") as f:
        body = f.read()
    shown = title or os.path.relpath(path, ROOT)
    story.append(Paragraph(shown, FILEHDR))
    # split long files into chunks so pages break cleanly
    lines = wrap_code(body).split("\n")
    for i in range(0, len(lines), 95):
        story.append(Preformatted("\n".join(lines[i:i + 95]), CODE))


def table(rows, widths, header=True, font=SM, zebra=True):
    data = []
    for r_i, r in enumerate(rows):
        style = SMB if (header and r_i == 0) else font
        data.append([Paragraph(str(c), style) for c in r])
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0)
    st = [("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#BBBBBB")), ("VALIGN", (0, 0), (-1, -1), "TOP"),
          ("LEFTPADDING", (0, 0), (-1, -1), 3.5), ("RIGHTPADDING", (0, 0), (-1, -1), 3.5),
          ("TOPPADDING", (0, 0), (-1, -1), 2.2), ("BOTTOMPADDING", (0, 0), (-1, -1), 2.2)]
    if header:
        st += [("BACKGROUND", (0, 0), (-1, 0), NAVY)]
        data[0] = [Paragraph(f"<font color='white'><b>{c}</b></font>", SM) for c in rows[0]]
        t = Table(data, colWidths=widths, repeatRows=1)
        if zebra:
            st += [("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, LIGHT])]
    t.setStyle(TableStyle(st))
    story.append(t); story.append(Spacer(1, 6))


# ------------------------------------------------------------------ diagrams
def sequence(lanes, msgs, width=172 * mm, title=None):
    from reportlab.pdfbase.pdfmetrics import stringWidth
    lane_x = {name: 12 + i * (width - 24) / (len(lanes) - 1) for i, name in enumerate(lanes)}
    row = 17
    height = 36 + row * len(msgs) + 10
    d = Drawing(width, height)
    top = height - 8
    for name, x in lane_x.items():
        d.add(Rect(x - 36, top - 20, 72, 20, fillColor=NAVY, strokeColor=NAVY, rx=3, ry=3))
        d.add(String(x, top - 13.5, name, fontName="Helvetica-Bold", fontSize=7, fillColor=colors.white, textAnchor="middle"))
        d.add(Line(x, top - 20, x, 6, strokeColor=colors.HexColor("#9AA9B8"), strokeDashArray=[2, 2], strokeWidth=0.6))
    y = top - 34
    for m in msgs:
        a, b, label = m[0], m[1], m[2]
        kind = m[3] if len(m) > 3 else "call"
        x1, x2 = lane_x[a], lane_x[b]
        col = {"call": NAVY, "reply": TEAL, "note": colors.HexColor("#B26B00")}[kind]
        if a == b:
            d.add(Rect(x1 - 2, y - 5, 110 if x1 < width - 120 else -110, 11, fillColor=AMBER, strokeColor=colors.HexColor("#E8C36A"), strokeWidth=0.4))
            anchor = "start" if x1 < width - 120 else "end"
            d.add(String(x1 + (3 if anchor == "start" else -3), y - 2, label, fontName="Helvetica", fontSize=6.3, fillColor=colors.black, textAnchor=anchor))
        else:
            d.add(Line(x1, y, x2, y, strokeColor=col, strokeWidth=0.9, strokeDashArray=[3, 2] if kind == "reply" else None))
            s = 1 if x2 > x1 else -1
            d.add(Polygon([x2, y, x2 - 5 * s, y + 2.4, x2 - 5 * s, y - 2.4], fillColor=col, strokeColor=col))
            tw = stringWidth(label, "Helvetica", 6.1)
            if min(x1, x2) + 4 + tw > width - 2:
                d.add(String(max(x1, x2) - 4, y + 2.4, label, fontName="Helvetica", fontSize=6.1, fillColor=col, textAnchor="end"))
            else:
                d.add(String(min(x1, x2) + 4, y + 2.4, label, fontName="Helvetica", fontSize=6.1, fillColor=col, textAnchor="start"))
        y -= row
    if title:
        story.append(Paragraph(f"<i>{title}</i>", SM))
    story.append(d); story.append(Spacer(1, 6))


def decision_tree():
    import math
    w, h = 174 * mm, 218
    d = Drawing(w, h)

    def box(x, y, bw, bh, text, fill=LIGHT, bold=False, size=6.6):
        d.add(Rect(x, y, bw, bh, fillColor=fill, strokeColor=NAVY, strokeWidth=0.6, rx=3, ry=3))
        lines = text.split("\n")
        lh = size + 1.8
        top = y + bh / 2 + (len(lines) - 1) * lh / 2 - size / 3
        for i, t in enumerate(lines):
            d.add(String(x + bw / 2, top - i * lh, t, fontName="Helvetica-Bold" if bold else "Helvetica",
                         fontSize=size, textAnchor="middle"))

    def arrow(x1, y1, x2, y2):
        d.add(Line(x1, y1, x2, y2, strokeColor=NAVY, strokeWidth=0.7))
        ang = math.atan2(y2 - y1, x2 - x1)
        d.add(Polygon([x2, y2, x2 - 5 * math.cos(ang) + 2.5 * math.sin(ang), y2 - 5 * math.sin(ang) - 2.5 * math.cos(ang),
                       x2 - 5 * math.cos(ang) - 2.5 * math.sin(ang), y2 - 5 * math.sin(ang) + 2.5 * math.cos(ang)],
                      fillColor=NAVY, strokeColor=NAVY))

    cx = w / 2
    box(cx - 170, h - 30, 340, 26, "CRD hook call\n(order-sign, order-select, order-dispatch, appointment-book, encounter-start/discharge)", bold=True)
    arrow(cx, h - 30, cx, h - 42)
    box(cx - 90, h - 66, 180, 24, "Read ext-coverage-information on the order\ncovered | pa-needed | doc-needed | doc-purpose")
    labels = ["not-covered", "conditional", "no-auth or\nsatisfied", "doc-needed\n(any value)", "auth-needed", "performpa"]
    results = ["Show the card.\nNo DTR, no PAS.\nChange the order\nor accept the cost.",
               "Add the missing\ninfo (code, location,\nperformer, date)\nand re-run CRD.",
               "Nothing to submit.\nsatisfied-pa-id is\nthe auth on file.",
               "Launch DTR from\nthe smart card,\n$questionnaire-package,\nsave the QR.",
               "Submit PAS\nClaim/$submit\n(include the QR if\ndoc-purpose = withpa).",
               "The performing\nprovider submits\nPAS (after\norder-dispatch)."]
    col = w / 6
    by = h - 112
    for i, (lab, res) in enumerate(zip(labels, results)):
        x = i * col + 3
        bw = col - 6
        arrow(cx, h - 66, x + bw / 2, by + 22)
        box(x, by, bw, 22, lab, fill=colors.HexColor("#DDE7F0"), bold=True)
        arrow(x + bw / 2, by, x + bw / 2, by - 10)
        box(x, by - 58, bw, 48, res, fill=colors.white, size=6.3)
    box(4, 2, w - 8, 36,
        "PAS response per line:  A1 / A2 / A6 approved  |  A3 denied (specific reason required)  |  NA not required  |  C cancelled  |  CT contact payer\n"
        "A4 pended + PAS Task (CDex): the provider runs DTR (attachment-request-questionnaire) or sends LOINC documents (attachment-request-code)\n"
        "with $submit-attachment; the payer re-adjudicates, sends a Subscription notification, and the provider calls Claim/$inquire",
        fill=AMBER, size=6.4)
    story.append(d); story.append(Spacer(1, 4))


# =================================================================================================== CONTENT
# ---------------- cover
story.append(Spacer(1, 38 * mm))
story.append(Paragraph("Da Vinci Prior Authorization API (.NET 8)", TITLE))
story.append(Spacer(1, 4))
story.append(Paragraph("A step-by-step guide to building the payer side of CRD, DTR, PAS and CDex: every scenario, model, "
                       "constant, service, controller and sample message", SUB))
story.append(Spacer(1, 14))
table([["Item", "Value"],
       ["Prepared for", "Rushikesh Kadam"],
       ["Date", "25 September 2026"],
       ["Implementation guides", "CRD 2.0.1 &middot; DTR 2.0.1 &middot; PAS 2.0.1 &middot; CDex 2.0.0 (the versions named in CMS-0057-F), FHIR R4 4.0.1, CDS Hooks 2.0"],
       ["Platform", "ASP.NET Core 8 Web API, C# 12, System.Text.Json. No NuGet packages are needed to build or run"],
       ["What you get", "This guide + <b>DaVinciPriorAuth_Source.zip</b> (the complete project, samples, test script). "
                        "Every source listing in this PDF is printed from that project"],
       ["Scope", "Payer side: CRD service, DTR questionnaire service, PAS endpoint, CDex attachment endpoint, subscriptions, "
                 "UM reviewer worklist. The EHR and the DTR SMART app are the provider side and are described, not built"]],
      [38 * mm, 138 * mm])
note("<b>Build status.</b> The code in this guide was reviewed line by line but has <b>not been compiled here</b>: the "
     "environment used to write it could not download the .NET SDK. Run <font face='Courier'>dotnet build</font> first; "
     "if anything fails, the fix is usually a one-line typo. Then run <font face='Courier'>tools/run-scenarios.sh</font>, "
     "which checks all 45 scenario checks in section 12 against the running API.")

story.append(PageBreak())
story.append(Paragraph("Contents", H1))
toc = TableOfContents(); toc.levelStyles = [TOC1, TOC2]
story.append(toc)
story.append(PageBreak())

# ---------------- 1 overview
h1("1. How the four guides fit together")
p("Prior authorization under Da Vinci is a chain of four FHIR exchanges. Each guide answers one question. "
  "The payer builds the four services in this guide; the provider's EHR and a DTR app call them.")
table([["Guide", "Question it answers", "Mechanism", "Caller -> Payer endpoint"],
       ["<b>CRD</b> 2.0.1", "Is this covered? Is prior auth (PA) needed? Is documentation needed?",
        "CDS Hooks. The answer is the <b>ext-coverage-information</b> extension, returned as a systemAction 'update' on the order, plus cards",
        "EHR -> POST /cds-services/crd-{hook}"],
       ["<b>DTR</b> 2.0.1", "What documentation does the payer need, and can the chart pre-fill it?",
        "Questionnaire + CQL Library + ValueSets from <b>$questionnaire-package</b>; the DTR app runs CQL and saves a QuestionnaireResponse (QR)",
        "DTR app -> POST /fhir/Questionnaire/$questionnaire-package (and $next-question)"],
       ["<b>PAS</b> 2.0.1", "Submit the PA request and get a decision",
        "<b>Claim/$submit</b> with a PAS Request Bundle, answered by a PAS Response Bundle with a ClaimResponse. "
        "<b>Claim/$inquire</b> and Subscriptions cover pended requests",
        "EHR -> POST /fhir/Claim/$submit"],
       ["<b>CDex</b> 2.0.0", "The payer needs more information to decide",
        "A PAS Task (attachment-request-code or -questionnaire) plus <b>$submit-attachment</b> for the answer",
        "EHR -> POST /fhir/$submit-attachment"]],
      [18 * mm, 42 * mm, 72 * mm, 44 * mm])

h2("1.1 End-to-end sequence")
sequence(["EHR", "DTR app", "Payer CRD", "Payer DTR", "Payer PAS", "Payer CDex/UM"], [
    ("EHR", "Payer CRD", "1  POST /cds-services/crd-order-sign (draftOrders + prefetch Coverage)"),
    ("Payer CRD", "EHR", "systemAction update: order + ext-coverage-information; cards", "reply"),
    ("EHR", "EHR", "EHR stores coverage-information on the order", "note"),
    ("EHR", "DTR app", "2  doc-needed: user clicks card link (type smart) -> SMART launch"),
    ("DTR app", "Payer DTR", "POST Questionnaire/$questionnaire-package (coverage, order, context)"),
    ("Payer DTR", "DTR app", "Parameters: PackageBundle (Questionnaire + Library + ValueSet)", "reply"),
    ("DTR app", "DTR app", "run CQL against EHR data, clinician completes", "note"),
    ("DTR app", "EHR", "save QuestionnaireResponse (status completed)"),
    ("EHR", "Payer PAS", "3  pa-needed = auth-needed: POST Claim/$submit (Claim + QR)"),
    ("Payer PAS", "EHR", "PAS Response Bundle: ClaimResponse A1/A2/A3/NA or A4 pended", "reply"),
    ("Payer PAS", "Payer CDex/UM", "4  A4 + info missing: create PAS Task (tracking id)"),
    ("EHR", "Payer CDex/UM", "GET Task (poll) or receive Task (push)"),
    ("EHR", "Payer CDex/UM", "POST $submit-attachment (TrackingId, documents / QR)"),
    ("Payer CDex/UM", "Payer PAS", "re-adjudicate pended lines"),
    ("Payer PAS", "EHR", "5  Subscription notification (rest-hook)", "reply"),
    ("EHR", "Payer PAS", "POST Claim/$inquire -> final ClaimResponse"),
], title="Figure 1: Full flow. The numbers match sections 6 (CRD), 7 (DTR), 8 (PAS), 9 (CDex).")

h2("1.2 When to trigger DTR, PAS and CDex")
p("These are the decision rules, and the ones implementers most often get wrong. They come straight from the CRD 2.0.1 "
  "coverage-information codes and the PAS 2.0.1 additional-information rules.")
decision_tree()
table([["Signal (from CRD coverage-information)", "What the EHR does next", "Calls"],
       ["<b>covered = not-covered</b>", "Show the card; the clinician changes the order or the patient accepts the cost", "none"],
       ["<b>covered or pa-needed = conditional</b> (+ info-needed / detail-code)", "Add the missing data (specific code, location, performer, date) and the hook runs again", "CRD again"],
       ["<b>pa-needed = no-auth</b>", "Nothing to submit. If doc-needed is present, still do DTR (for the claim or the order)", "DTR only if doc-needed"],
       ["<b>pa-needed = satisfied</b>", "An authorization is already on file (satisfied-pa-id). Put it on the claim", "none"],
       ["<b>pa-needed = auth-needed</b>", "Submit PAS. If doc-needed is present with doc-purpose = withpa, run DTR <b>first</b> and include the QR in the PAS bundle", "DTR (if doc) -> PAS"],
       ["<b>pa-needed = performpa</b>", "The performing provider (e.g. imaging centre) submits PAS, typically after order-dispatch", "PAS by performer"],
       ["<b>doc-needed = clinical | admin | both</b>", "Launch DTR from the 'smart' link on the card. clinical: clinician completes it; admin: back-office staff can", "DTR"],
       ["<b>doc-purpose</b>", "withpa: send the QR with PAS. withclaim: send it with the claim. withorder: send it with the order. retain-doc: keep it in the EHR", "-"],
       ["<b>PAS reviewAction A4</b> + PAS Task", "Task code attachment-request-questionnaire: run DTR with the Task as context. attachment-request-code: send the LOINC documents. Both are answered with $submit-attachment", "DTR and/or CDex"],
       ["<b>PAS A4</b> without a Task", "Clinical review in progress. Wait for the Subscription notification, then call $inquire", "$inquire"]],
      [50 * mm, 98 * mm, 28 * mm])

# ---------------- 2 scenario matrix
h1("2. Scenario catalogue")
p("Each row is a real scenario implemented by one row of the payer's coverage-rule table (section 5.2) and exercised by a "
  "sample file in <font face='Courier'>samples/</font>. The same table drives CRD, DTR and PAS, so answers never disagree.")
table([["#", "Order (code)", "CRD coverage-information", "Cards", "DTR", "PAS result", "CDex"],
       ["S1", "Office visit (CPT 99213)", "covered, no-auth", "none", "no", "NA (not required)", "no"],
       ["S2", "CPAP (HCPCS E0601)", "covered, auth-needed, doc clinical, withpa, questionnaire cpap", "PA card + DTR launch card", "yes", "A1 if AHI >= 15 and face-to-face; else A4 for clinical review", "only if QR missing"],
       ["S3", "MRI lumbar (CPT 72148)", "covered, auth-needed, no doc, allowed-quantity 1", "PA card", "no", "A1; A2 if quantity > 1", "no"],
       ["S4", "Oxygen concentrator (E1390)", "covered, no-auth, doc clinical, <b>withclaim</b>", "DTR launch card", "yes (adaptive)", "not submitted", "no"],
       ["S5", "Crutches (E0114)", "covered, no-auth, doc <b>admin</b>, withorder", "DTR launch card", "yes", "not submitted", "no"],
       ["S6", "Dermabrasion (CPT 15780)", "<b>not-covered</b> + reason", "warning card", "no", "A3 if submitted anyway", "no"],
       ["S7", "Unlisted procedure (CPT 64999)", "<b>conditional</b>, pa conditional, detail detail-code", "info card", "no", "CT (contact payer)", "no"],
       ["S8a", "Knee replacement (CPT 27447), no location", "covered, pa <b>conditional</b>, info-needed <b>location</b>", "info card", "no", "-", "no"],
       ["S8b", "27447 at POS 21 (inpatient)", "auth-needed, doc <b>both</b>, withpa", "PA + DTR cards", "yes", "A4 until the imaging report (LOINC 18748-4) arrives, then A1", "<b>yes</b>"],
       ["S8c", "27447 at POS 22 (outpatient)", "covered, no-auth", "none", "no", "NA", "no"],
       ["S9", "Physical therapy (CPT 97110), gold-card NPI", "no-auth, reason <b>gold-card</b>", "info card", "no", "NA", "no"],
       ["S10", "MRI brain (CPT 70553)", "auth-needed as <b>performpa</b>", "info card", "no", "submitted by the performer", "no"],
       ["S11", "Any order, no active Coverage", "(no systemAction)", "warning card", "no", "-", "no"],
       ["S12", "Same order after approval", "pa <b>satisfied</b> + satisfied-pa-id", "info card", "no", "not needed", "no"],
       ["P-dup", "Same PAS Bundle.identifier sent twice", "-", "-", "-", "stored response returned (idempotent)", "-"],
       ["P-upd", "Update / cancel a line", "-", "-", "-", "C for the cancelled line, others re-decided", "-"],
       ["P-err", "Invalid bundle (wrong type/use/missing)", "-", "-", "-", "HTTP 400 + OperationOutcome", "-"]],
      [9 * mm, 30 * mm, 42 * mm, 20 * mm, 13 * mm, 44 * mm, 18 * mm])

h2("2.1 X12 codes used in PAS responses")
table([["X12 306 code", "Meaning", "When this API returns it"],
       ["A1", "Certified in total", "All criteria met, quantity within limit"],
       ["A2", "Certified - partial", "Approved with a lower quantity (itemAuthorizedDetail carries the approved quantity)"],
       ["A3", "Not certified", "Not a covered benefit, member not eligible, or reviewer denial (a reason is required)"],
       ["A4", "Pended", "Information missing (Task created) or clinical criteria not met (reviewer decides)"],
       ["A6", "Modified", "Reviewer approves with changes"],
       ["C", "Cancelled", "Line cancelled in an update (certificationType 3 / infoCancelled)"],
       ["CT", "Contact payer", "Code too generic (unlisted procedure)"],
       ["NA", "No action required", "PA not required for this line (plan default, no-auth, outside PA place of service, gold card)"]],
      [22 * mm, 38 * mm, 116 * mm])
note("Denial reason codes (X12 886) are a licensed X12 list, so this project stores the code in "
     "<font face='Courier'>PriorAuthItemRecord.ReasonCode</font> and always sends human-readable text. Load the real 886 codes "
     "from your X12 licence into your UM system. CMS-0057-F requires a <b>specific</b> reason for every denial.")

# ---------------- 3 setup
h1("3. Step 1: Create the project")
steps([
    "Install the <b>.NET 8 SDK</b> (<font face='Courier'>dotnet --list-sdks</font> should show 8.0.x). Visual Studio 2022 17.8+, Rider or VS Code all work.",
    "Create the solution and project:",
])
code("""mkdir DaVinciPriorAuth && cd DaVinciPriorAuth
dotnet new sln -n DaVinciPriorAuth
dotnet new webapi -n DaVinci.PriorAuth.Api -o src/DaVinci.PriorAuth.Api --use-controllers --no-openapi
dotnet sln add src/DaVinci.PriorAuth.Api
# remove the template's WeatherForecast files
rm src/DaVinci.PriorAuth.Api/WeatherForecast.cs src/DaVinci.PriorAuth.Api/Controllers/WeatherForecastController.cs""")
steps(["Create the folders below, then add each file from sections 4-10 (or unzip DaVinciPriorAuth_Source.zip over the project).",
       "Build and run: <font face='Courier'>dotnet run --project src/DaVinci.PriorAuth.Api</font>, which listens on http://localhost:5080."])
code("""DaVinciPriorAuth/
|-- src/DaVinci.PriorAuth.Api/
|   |-- DaVinci.PriorAuth.Api.csproj
|   |-- Program.cs, appsettings.json
|   |-- Constants/        CommonConstants.cs, CrdConstants.cs, DtrPasCdexConstants.cs
|   |-- Models/Fhir/      Datatypes.cs, Resources.cs, ClaimResources.cs, QuestionnaireResources.cs, FhirJson.cs
|   |-- Models/CdsHooks/  CdsHooksModels.cs
|   |-- Models/Domain/    DomainModels.cs
|   |-- Data/             Repositories.cs, SeedData.cs
|   |-- Infrastructure/   PayerOptions.cs, FhirHttp.cs
|   |-- Services/Common/  BundleResolver.cs, EhrFhirClient.cs
|   |-- Services/Crd/     OrderExtractor.cs, CoverageRulesEngine.cs, CoverageInformationBuilder.cs, CrdService.cs
|   |-- Services/Dtr/     QuestionnairePackageService.cs, AdaptiveQuestionnaireService.cs
|   |-- Services/Pas/     PasRequestValidator.cs, PasAdjudicator.cs, PasResponseBuilder.cs, PasService.cs, SubscriptionService.cs
|   |-- Services/Cdex/    AdditionalInfoRequestBuilder.cs, AttachmentService.cs
|   |-- Controllers/      CdsServicesController.cs, DtrController.cs, PasController.cs, CdexController.cs,
|   |                     MetadataAndReviewControllers.cs
|   `-- Resources/        Questionnaires/*.json, Cql/*.cql (+ *.elm.json), Adaptive/*.template.json
|-- samples/              one JSON request per scenario
`-- tools/                run-scenarios.sh, make_samples.py, build_guide.py""")
src("DaVinci.PriorAuth.Api.csproj")
h2("3.1 Design choices you should know")
bullets([
    "<b>No FHIR SDK dependency.</b> FHIR JSON is read and written with small C# models and System.Text.Json (section 5). "
    "This keeps every field visible and the project buildable offline. You can switch to the Firely SDK "
    "(Hl7.Fhir.R4) later; the service layer would barely change.",
    "<b>One rule table drives everything.</b> <font face='Courier'>CoverageRule</font> rows decide the CRD answer, the DTR "
    "questionnaire and the PAS adjudication, so the payer never tells the provider two different things.",
    "<b>Orders are echoed as raw JSON.</b> CRD must return the EHR's own order with the coverage-information extension added. "
    "The order is kept as a JsonObject so no field the EHR sent is lost.",
    "<b>In-memory repositories</b> behind interfaces, so the API runs on first launch. Section 13 shows the EF Core tables.",
])

# ---------------- 4 constants
h1("4. Step 2: Constants (every URL, code and parameter name)")
p("Every canonical URL, extension URL, code system, code value and operation parameter name used by the four guides lives "
  "in these three files. Where two IG artifacts disagree, the comment says which one this project follows and why.")
table([["Value", "Where it is used", "Checked against"],
       ["ext-coverage-information + 16 sub-extensions", "CRD systemAction", "CRD 2.0.1 StructureDefinition-ext-coverage-information"],
       ["covered / pa-needed / doc-needed / doc-purpose / info-needed codes", "CRD", "CRD 2.0.1 CodeSystem temp"],
       ["davinci-crd.configuration-options / davinci-crd.configuration", "CRD discovery / hook request", "CRD 2.0.1 deviations page"],
       ["$questionnaire-package parameters", "DTR", "DTR 2.0.1 OperationDefinition (in: coverage, order, referenced, questionnaire, context, changedsince)"],
       ["Output parameter <b>PackageBundle</b>", "DTR", "The DTR 2.0.1 output-parameters profile and its example use PackageBundle, "
        "but the OperationDefinition says 'return'. The name is configurable (Payer:PackageOutputParameterName)"],
       ["Claim-submit / Claim-inquiry (resource -> return)", "PAS", "PAS 2.0.1 OperationDefinitions"],
       ["PAS extension URLs (reviewAction, itemPreAuthPeriod ...)", "PAS", "PAS 2.0.1 artifacts + ClaimResponse profile"],
       ["PAS Task codes (PASTempCodes: attachment-request-code, payer-url, attachments-needed, priorAuthorization)", "PAS/CDex", "PAS 2.0.1 Task example"],
       ["$submit-attachment parameters", "CDex", "CDex 2.0.0 OperationDefinition"],
       ["cdex-temp codes, tracking-id", "CDex", "CDex 2.0.0 Task profile and examples"]],
      [62 * mm, 30 * mm, 84 * mm])
src("Constants/CommonConstants.cs")
src("Constants/CrdConstants.cs")
src("Constants/DtrPasCdexConstants.cs")

# ---------------- 5 models
h1("5. Step 3: Models")
h2("5.1 FHIR R4 models")
p("These are plain classes whose property names serialize to FHIR JSON names (camelCase). Choice elements (value[x]) are "
  "separate properties such as <font face='Courier'>ValueCodeableConcept</font>, exactly as FHIR JSON writes them. "
  "<font face='Courier'>FhirJson</font> handles the three FHIR JSON rules that need code: <i>resourceType</i> picks the class, "
  "empty arrays are never written, and unknown resource types are kept whole (<font face='Courier'>GenericResource</font>).")
src("Models/Fhir/Datatypes.cs")
src("Models/Fhir/Resources.cs")
src("Models/Fhir/ClaimResources.cs")
src("Models/Fhir/QuestionnaireResources.cs")
src("Models/Fhir/FhirJson.cs")
h2("5.2 CDS Hooks models (CRD wire format)")
src("Models/CdsHooks/CdsHooksModels.cs")
h2("5.3 Domain models: the coverage rule table and prior-auth records")
p("<font face='Courier'>CoverageRule</font> is the heart of the system: one row per plan + billing code. "
  "<font face='Courier'>PriorAuthRecord</font> is one PAS submission with its per-line decisions.")
src("Models/Domain/DomainModels.cs")

# ---------------- infrastructure + data
h1("6. Step 4: Configuration, data and seed content")
h2("6.1 Options, HTTP helpers and authorization")
src("Infrastructure/PayerOptions.cs")
src("Infrastructure/FhirHttp.cs")
h2("6.2 Repositories")
src("Data/Repositories.cs")
h2("6.3 Seed data: the rule table, questionnaires and CQL")
p("Each <font face='Courier'>CoverageRule</font> row below produces one scenario from section 2. To add a service, add a row. "
  "Questionnaires, ValueSets and CQL are loaded from <font face='Courier'>Resources/</font> at startup.")
src("Data/SeedData.cs")
h3("Questionnaire: CPAP (standard DTR questionnaire with CQL pre-population)")
src("Resources/Questionnaires/cpap-e0601.json")
h3("Questionnaire: total knee arthroplasty (clinical + administrative groups)")
src("Resources/Questionnaires/knee-arthroplasty-27447.json")
h3("Questionnaire: crutches (administrative only, uses an answerValueSet)")
src("Resources/Questionnaires/crutches-e0114.json")
src("Resources/Questionnaires/valueset-dme-delivery-method.json")
h3("Adaptive questionnaire: home oxygen (public shell + payer-internal template)")
src("Resources/Questionnaires/home-o2-adaptive.json")
src("Resources/Adaptive/home-o2-adaptive.template.json")
h3("CQL libraries")
src("Resources/Cql/CpapPrepopulation.cql")
src("Resources/Cql/KneePrepopulation.cql")
src("Resources/Cql/HomeO2Prepopulation.cql")
note("<b>DTR requires ELM next to the CQL.</b> DTR 2.0.1 says each Library SHALL contain both the CQL "
     "(text/cql) and the compiled ELM (application/elm+json), and all libraries a Questionnaire needs, including "
     "<b>FHIRHelpers 4.0.1</b>, SHALL be in the package. Before go-live:<br/>"
     "1. Compile each .cql with the CQL-to-ELM translator (cqframework <i>cql-to-elm-cli</i>, or the VS Code CQL extension) and "
     "save the output as <font face='Courier'>&lt;Name&gt;.elm.json</font> next to the .cql; SeedData picks it up automatically.<br/>"
     "2. Add FHIRHelpers.cql / FHIRHelpers.elm.json (version 4.0.1, from the cqframework repository) to Resources/Cql. "
     "Until you do, $questionnaire-package returns a warning in its outcome.<br/>"
     "3. Replace the placeholder AHI code (example.org code system) with the LOINC code your provider EHRs actually record.")

# ---------------- CRD
h1("7. Step 5: CRD (Coverage Requirements Discovery)")
h2("7.1 What CRD must do")
bullets([
    "<b>Discovery</b> (GET /cds-services): list one service per supported hook, with prefetch templates and the "
    "<font face='Courier'>davinci-crd.configuration-options</font> extension (coverage-info, claim, max-cards).",
    "<b>Hook call</b> (POST /cds-services/{id}): read the orders from context (draftOrders, appointments, order, encounter), "
    "read Coverage from prefetch (or query the EHR with fhirServer + fhirAuthorization if prefetch is missing).",
    "<b>Answer within 5 seconds</b> (CRD target, 90% of calls; 10 s for some primary hooks). The EHR client timeout is set to 4 s.",
    "<b>Primary hooks</b> (appointment-book, order-sign, order-dispatch) SHALL return Coverage Information: a "
    "<font face='Courier'>systemAction</font> of type <b>update</b> whose resource is the order with ext-coverage-information added.",
    "Cards are optional guidance: PA required, performer must request, satisfied, gold card, not covered, more information needed, "
    "and the <b>Launch SMART app</b> card (link type 'smart') that opens DTR when documentation is needed.",
    "Honour <font face='Courier'>extension.davinci-crd.configuration</font> from the EHR (e.g. coverage-info: false, max-cards).",
])
h2("7.2 Coverage-information extension, field by field")
table([["Sub-extension", "Card.", "Type", "How this API fills it"],
       ["coverage", "1..1", "Reference", "Coverage/{id} from prefetch"],
       ["covered", "1..1", "code", "covered | not-covered | conditional"],
       ["pa-needed", "0..1", "code", "no-auth | auth-needed | satisfied | performpa | conditional"],
       ["doc-needed", "0..1", "code", "clinical | admin | both | conditional (omitted when no documentation is needed)"],
       ["doc-purpose", "0..*", "code", "withpa | withclaim | withorder | retain-doc"],
       ["info-needed", "0..*", "code", "performer | location | timeframe | contract-window"],
       ["billingCode", "0..*", "Coding", "the order code that matched the rule"],
       ["reason", "0..*", "CodeableConcept", "not-covered reason text; CRD temp#gold-card"],
       ["detail", "0..*", "complex", "category (CRD temp detail codes) + value[x]: detail-code, allowed-quantity ..."],
       ["questionnaire", "0..*", "canonical", "DTR questionnaire(s) to complete"],
       ["date", "1..1", "date", "date of the decision"],
       ["coverage-assertion-id", "1..1", "string", "payer id for this answer. DTR 'context' and later PAS can reference it"],
       ["satisfied-pa-id", "0..1", "string", "existing authorization number when pa-needed = satisfied"]],
      [34 * mm, 12 * mm, 24 * mm, 106 * mm])
h2("7.3 Code")
src("Services/Crd/OrderExtractor.cs")
src("Services/Crd/CoverageRulesEngine.cs")
src("Services/Crd/CoverageInformationBuilder.cs")
src("Services/Crd/CrdService.cs")
src("Controllers/CdsServicesController.cs")
h2("7.4 Example: S2 CPAP order-sign")
p("Request (abridged; the full file is <font face='Courier'>samples/crd-02-order-sign-pa-and-clinical-doc.json</font>):")
with open(os.path.join(ROOT, "samples", "crd-02-order-sign-pa-and-clinical-doc.json")) as f:
    req = json.load(f)
req["prefetch"] = {"patient": "{ Patient pat-1 }", "coverage": "{ Bundle with Coverage cov-1, subscriberId MBR-1001, plan GOLD-PPO }",
                   "practitioner": "{ Bundle with Practitioner prac-1, NPI 1111111112 }"}
code(json.dumps(req, indent=2))
p("Response (abridged). The systemAction echoes the DeviceRequest with the extension added; two cards are returned:")
code("""{
  "cards": [
    { "summary": "Prior authorization required: CPAP device", "indicator": "info",
      "source": { "label": "Example Health Plan",
                  "topic": { "system": "http://hl7.org/fhir/us/davinci-crd/CodeSystem/temp", "code": "coverage-info" } },
      "links": [ { "label": "Coverage policy", "url": "https://example.org/policies/cpap", "type": "absolute" } ] },
    { "summary": "Documentation required (clinical): CPAP device", "indicator": "info",
      "links": [ { "label": "Launch documentation (DTR)", "url": "https://dtr.example.org/launch", "type": "smart",
                   "appContext": "{\\"coverage-assertion-id\\":\\"9f2c...\\",\\"questionnaire\\":[\\"http://example.org/fhir/Questionnaire/cpap-e0601\\"],
                                  \\"order\\":\\"DeviceRequest/dr-e0601\\",\\"coverage\\":\\"Coverage/cov-1\\"}" } ] } ],
  "systemActions": [ {
    "type": "update", "description": "Add coverage information to DeviceRequest/dr-e0601",
    "resource": { "resourceType": "DeviceRequest", "id": "dr-e0601", "...": "(original fields unchanged)",
      "extension": [ { "url": "http://hl7.org/fhir/us/davinci-crd/StructureDefinition/ext-coverage-information",
        "extension": [
          { "url": "coverage", "valueReference": { "reference": "Coverage/cov-1" } },
          { "url": "covered", "valueCode": "covered" },
          { "url": "pa-needed", "valueCode": "auth-needed" },
          { "url": "doc-needed", "valueCode": "clinical" },
          { "url": "doc-purpose", "valueCode": "withpa" },
          { "url": "billingCode", "valueCoding": { "system": "https://bluebutton.cms.gov/resources/codesystem/hcpcs", "code": "E0601" } },
          { "url": "detail", "extension": [
              { "url": "category", "valueCodeableConcept": { "coding": [ { "system": "http://hl7.org/fhir/us/davinci-crd/CodeSystem/temp",
                                                                              "code": "allowed-quantity" } ] } },
              { "url": "value", "valueQuantity": { "value": 1 } } ] },
          { "url": "questionnaire", "valueCanonical": "http://example.org/fhir/Questionnaire/cpap-e0601" },
          { "url": "date", "valueDate": "2026-09-25" },
          { "url": "coverage-assertion-id", "valueString": "9f2c..." } ] } ] } } ]
}""")
note("<b>DTR launch context.</b> In DTR 2.0.1 the EHR launches the DTR app with the SMART EHR launch and passes "
     "<b>fhirContext</b> (the Coverage plus the order, or an in-progress QuestionnaireResponse, or a CDex Task). The card's "
     "<font face='Courier'>appContext</font> string is payer-defined; this API puts the coverage-assertion-id and the "
     "questionnaire canonicals in it so the DTR app can pass them straight to $questionnaire-package.")

# ---------------- DTR
h1("8. Step 6: DTR (Documentation Templates and Rules)")
h2("8.1 How the DTR app gets the questionnaire")
steps([
    "The EHR launches the DTR app (SMART EHR launch, scopes such as <font face='Courier'>launch openid fhirUser patient/*.rs</font>) "
    "with fhirContext = Coverage + order. The app can also be EHR-native.",
    "The DTR app calls <b>POST [payer]/fhir/Questionnaire/$questionnaire-package</b> with a Parameters resource holding coverage, "
    "the order(s), and, when it has them, the questionnaire canonical(s) from coverage-information and "
    "<b>context</b> = coverage-assertion-id (or the CDex tracking id).",
    "This API picks the questionnaires in this order: explicit canonicals, then context (CRD assertion or PAS tracking id), "
    "then evaluating the orders against the rule table.",
    "For each questionnaire it returns one <b>PackageBundle</b> (profile DTR-QPackageBundle): the Questionnaire, every Library "
    "in its cqf-library chain (CQL + ELM, following depends-on to FHIRHelpers) and every ValueSet used by answerValueSet.",
    "The app runs the CQL (initialExpression, candidateExpression, calculatedExpression) against the EHR's FHIR API and marks "
    "pre-filled answers with the <b>information-origin</b> extension (source = auto). Answers the user types or changes get "
    "source = manual / override plus an author.",
    "Adaptive forms: the returned Questionnaire has the questionnaireAdaptive extension and no items; the app calls "
    "<b>$next-question</b> repeatedly, sending the QR with the Questionnaire contained, until status = completed.",
    "The completed QR is saved in the EHR (POST/PUT QuestionnaireResponse). For doc-purpose = withpa it goes into the PAS bundle; "
    "for a CDex request it is sent with $submit-attachment.",
])
h2("8.2 Code")
src("Services/Dtr/QuestionnairePackageService.cs")
src("Services/Dtr/AdaptiveQuestionnaireService.cs")
src("Controllers/DtrController.cs")
h2("8.3 Example: $questionnaire-package request and response shape")
with open(os.path.join(ROOT, "samples", "dtr-01-package-by-questionnaire.json")) as f:
    d1 = json.load(f)
d1["parameter"][0]["resource"] = "{ Coverage cov-1 }"
code(json.dumps(d1, indent=2))
code("""{ "resourceType": "Parameters",
  "meta": { "profile": [ "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-qpackage-output-parameters" ] },
  "parameter": [
    { "name": "PackageBundle",
      "resource": { "resourceType": "Bundle", "type": "collection",
        "meta": { "profile": [ "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/DTR-QPackageBundle" ] },
        "entry": [
          { "fullUrl": "http://localhost:5080/fhir/Questionnaire/cpap-e0601", "resource": { "resourceType": "Questionnaire", "...": "" } },
          { "fullUrl": "http://localhost:5080/fhir/Library/CpapPrepopulation",
            "resource": { "resourceType": "Library", "url": "http://example.org/fhir/Library/CpapPrepopulation", "version": "1.0.0",
                          "content": [ { "contentType": "text/cql", "data": "<base64>" },
                                       { "contentType": "application/elm+json", "data": "<base64>" } ] } } ] } },
    { "name": "outcome", "resource": { "resourceType": "OperationOutcome", "issue": [ { "severity": "warning", "code": "not-found",
        "diagnostics": "Library http://hl7.org/fhir/Library/FHIRHelpers|4.0.1 ... is not loaded" } ] } } ] }""")

# ---------------- PAS
h1("9. Step 7: PAS (Prior Authorization Support)")
h2("9.1 The PAS Request Bundle")
table([["Element", "Rule (PAS 2.0.1)", "Checked in"],
       ["Bundle.type", "collection", "PasRequestValidator"],
       ["Bundle.identifier, Bundle.timestamp", "required; identifier is used for idempotency", "PasRequestValidator / PasService"],
       ["entry[0]", "SHALL be the Claim; no entry.request", "PasRequestValidator"],
       ["Claim.status / use", "active / preauthorization", "PasRequestValidator"],
       ["Claim.identifier, type, patient, created, insurer, provider, priority", "required", "PasRequestValidator"],
       ["Claim.insurance", "1..*, focal = true, coverage in bundle", "PasRequestValidator"],
       ["Claim.item", "1..*: sequence, category (X12 1365), productOrService; extensions certificationType (X12 1322), serviceItemRequestType (X12 1525), requestedService, itemTraceNumber", "PasRequestValidator / PasAdjudicator"],
       ["Claim.supportingInfo", "category PASTempCodes#additionalInformation -> the DTR QuestionnaireResponse / DocumentReference", "PasAdjudicator"],
       ["Update", "new Bundle.identifier, same Claim.identifier (or Claim.related); items with certificationType 3 + modifierextension-infoCancelled are cancelled; infoChanged = added | changed", "PasService / PasAdjudicator"]],
      [44 * mm, 96 * mm, 36 * mm])
h2("9.2 HTTP behaviour")
table([["Situation", "HTTP", "Body"],
       ["Processed (any decision, including A3 and A4)", "200", "PAS Response Bundle, ClaimResponse first"],
       ["Same Bundle.identifier again", "200", "the stored response (nothing re-processed)"],
       ["Technical error (structure, missing required element)", "400", "OperationOutcome with FHIRPath expressions"],
       ["Server failure", "500", "OperationOutcome (fatal)"],
       ["Decision not possible within 15 s", "200", "ClaimResponse outcome = queued, lines A4; final result via Subscription + $inquire"]],
      [80 * mm, 14 * mm, 82 * mm])
p("PAS 2.0.1 expects the whole round trip within 15 seconds. The decision windows in CMS-0057-F (72 hours expedited, "
  "7 calendar days standard) are stored as <font face='Courier'>DueAt</font> on each pended request; "
  "Claim.priority = stat is treated as expedited.")
h2("9.3 Code")
src("Services/Pas/PasRequestValidator.cs")
src("Services/Pas/PasAdjudicator.cs")
src("Services/Pas/PasResponseBuilder.cs")
src("Services/Pas/PasService.cs")
src("Services/Pas/SubscriptionService.cs")
src("Controllers/PasController.cs")
h2("9.4 Example: approved CPAP response (P1)")
code("""{ "resourceType": "Bundle", "type": "collection",
  "meta": { "profile": [ "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/profile-pas-response-bundle" ] },
  "entry": [ { "fullUrl": "urn:uuid:3b1d...", "resource": {
      "resourceType": "ClaimResponse",
      "meta": { "profile": [ "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/profile-claimresponse" ] },
      "status": "active", "use": "preauthorization", "outcome": "complete",
      "type": { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/claim-type", "code": "professional" } ] },
      "patient": { "reference": "urn:uuid:7bb0..." }, "insurer": { "reference": "urn:uuid:..." }, "requestor": { "reference": "urn:uuid:..." },
      "request": { "reference": "urn:uuid:<claim fullUrl>" },
      "disposition": "1 certified in total", "preAuthRef": "PA20260925123456",
      "preAuthPeriod": { "start": "2026-10-01", "end": "2027-11-01" },
      "item": [ { "itemSequence": 1,
        "extension": [
          { "url": ".../extension-itemTraceNumber", "valueIdentifier": { "system": "http://example.org/provider/trace", "value": "PA-CPAP-001-1" } },
          { "url": "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/extension-reviewAction", "extension": [
              { "url": "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/extension-reviewActionCode",
                "valueCodeableConcept": { "coding": [ { "system": "https://codesystem.x12.org/005010/306", "code": "A1",
                                                        "display": "Certified in total" } ] } },
              { "url": "number", "valueString": "PA20260925123456" } ] },
          { "url": ".../extension-itemPreAuthIssueDate", "valueDate": "2026-09-25" },
          { "url": ".../extension-itemPreAuthPeriod", "valuePeriod": { "start": "2026-10-01", "end": "2027-11-01" } } ],
        "adjudication": [ { "category": { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/adjudication",
                                                         "code": "submitted" } ] }, "value": 1 } ],
        "noteNumber": [ 1 ] } ],
      "processNote": [ { "number": 1, "type": "display", "text": "Line 1: Approved." } ] } },
    { "...": "Patient, insurer Organization and requestor Organization copied from the request" } ] }""")
h2("9.5 Subscriptions for pended results")
p("The provider subscribes once per requesting organization (R4 Subscriptions Backport, as PAS requires). When a pended request "
  "changes, this API POSTs a <i>history</i> Bundle whose first entry is the SubscriptionStatus Parameters; with id-only "
  "payload the provider then calls $inquire.")
with open(os.path.join(ROOT, "samples", "pas-13-subscription.json")) as f:
    code(f.read())

# ---------------- CDex
h1("10. Step 8: CDex (additional information for pended requests)")
h2("10.1 Flow")
sequence(["EHR", "DTR app", "Payer PAS", "Payer CDex"], [
    ("EHR", "Payer PAS", "Claim/$submit (knee, POS 21, QR, no imaging report)"),
    ("Payer PAS", "EHR", "ClaimResponse A4 + CommunicationRequest (LOINC 18748-4)", "reply"),
    ("Payer PAS", "Payer CDex", "create PAS Task: attachment-request-code, tracking id, payer-url"),
    ("EHR", "Payer CDex", "GET /fhir/Task?owner={NPI}&status=requested (or Task pushed to EHR)"),
    ("EHR", "Payer CDex", "PUT Task status = in-progress (optional)"),
    ("EHR", "Payer CDex", "POST $submit-attachment (TrackingId, AttachTo=preauthorization, DocumentReference)"),
    ("Payer CDex", "Payer PAS", "re-adjudicate pended line -> A1, Task completed"),
    ("Payer PAS", "EHR", "Subscription notification", "reply"),
    ("EHR", "Payer PAS", "Claim/$inquire -> A1"),
    ("EHR", "DTR app", "questionnaire variant: Task code attachment-request-questionnaire -> launch DTR with Task"),
    ("DTR app", "Payer CDex", "$submit-attachment with the completed QuestionnaireResponse"),
], title="Figure 2: Pended request answered through CDex (solicited attachments).")
table([["Parameter", "Card.", "This API"],
       ["TrackingId", "1..1 Identifier", "the payer tracking id from Task.identifier (solicited), or the provider's Claim.identifier (unsolicited)"],
       ["AttachTo", "1..1 code", "preauthorization -> linked to the PAS request and re-adjudicated; claim -> stored"],
       ["PayerId", "0..1 Identifier", "accepted"],
       ["OrganizationId / ProviderId", "0..1 each, at least one", "validated"],
       ["MemberId", "1..1 Identifier", "must match the member on the request, else 422"],
       ["ServiceDate", "0..1 dateTime", "accepted"],
       ["Attachment", "1..*: LineItem 0..*, Code 0..1 (LOINC), Content 1..1 Resource", "LOINC from Code or DocumentReference.type; QR / DTR QR Bundle recognised"],
       ["Final", "0..1 boolean (default true)", "re-adjudicates only when true"]],
      [40 * mm, 46 * mm, 90 * mm])
h2("10.2 Code")
src("Services/Cdex/AdditionalInfoRequestBuilder.cs")
src("Services/Cdex/AttachmentService.cs")
src("Controllers/CdexController.cs")
h2("10.3 Example: the Task created for S8b (abridged)")
code("""{ "resourceType": "Task", "meta": { "profile": [ "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/profile-task" ] },
  "identifier": [ { "type": { "coding": [ { "system": "http://hl7.org/fhir/us/davinci-cdex/CodeSystem/cdex-temp", "code": "tracking-id" } ] },
                    "system": "http://example.org/cdex/tracking-ids", "value": "TRK-5c1f0e2d9a7b41" } ],
  "status": "requested", "intent": "order",
  "code": { "coding": [ { "system": "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASTempCodes", "code": "attachment-request-code" } ] },
  "for": { "type": "Patient", "identifier": { "system": "http://example.org/member-ids", "value": "MBR-1001" } },
  "requester": { "type": "Organization", "identifier": { "system": "http://example.org/payer-ids", "value": "PAYER001" } },
  "owner": { "identifier": { "system": "http://hl7.org/fhir/sid/us-npi", "value": "1999999992" } },
  "reasonCode": { "coding": [ { "system": "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASTempCodes", "code": "priorAuthorization" } ] },
  "reasonReference": { "type": "Claim", "identifier": { "system": "http://example.org/provider/claim-ids", "value": "PA-KNEE-006" } },
  "restriction": { "period": { "end": "2026-10-09" } },
  "input": [
    { "type": { "coding": [ { "system": ".../PASTempCodes", "code": "payer-url" } ] }, "valueUrl": "http://localhost:5080/fhir/$submit-attachment" },
    { "extension": [ { "url": "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/extension-paLineNumber", "valueInteger": 1 } ],
      "type": { "coding": [ { "system": ".../PASTempCodes", "code": "attachments-needed" } ] },
      "valueCodeableConcept": { "coding": [ { "system": "http://loinc.org", "code": "18748-4" } ] } },
    { "type": { "coding": [ { "system": ".../cdex-temp", "code": "purpose-of-use" } ] },
      "valueCodeableConcept": { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/v3-ActReason", "code": "COVAUTH" } ] } } ] }""")

# ---------------- Program + run
h1("11. Step 9: Wire it up (Program.cs, appsettings.json, metadata, reviewer)")
src("Program.cs")
src("appsettings.json")
src("Services/Common/BundleResolver.cs")
src("Services/Common/EhrFhirClient.cs")
src("Controllers/MetadataAndReviewControllers.cs")
h2("11.1 Endpoint catalogue")
table([["Method", "Path", "Guide", "Purpose"],
       ["GET", "/cds-services", "CRD", "Discovery"],
       ["POST", "/cds-services/crd-{hook}", "CRD", "order-sign, order-select, order-dispatch, appointment-book, encounter-start, encounter-discharge"],
       ["POST", "/fhir/Questionnaire/$questionnaire-package", "DTR", "Questionnaire package"],
       ["POST", "/fhir/Questionnaire/$next-question", "DTR/SDC", "Adaptive forms"],
       ["GET", "/fhir/Questionnaire/{id}, ?url=, /fhir/Library/{id}, /fhir/ValueSet/{id}", "DTR", "Read / search"],
       ["POST", "/fhir/Claim/$submit", "PAS", "Submit, update or cancel a PA request"],
       ["POST", "/fhir/Claim/$inquire", "PAS", "Inquiry (query by example)"],
       ["POST/GET", "/fhir/Subscription, /fhir/Subscription/{id}", "PAS", "Pended-result notifications"],
       ["GET", "/fhir/ClaimResponse/{id}", "PAS", "Read the latest ClaimResponse (full-resource notifications)"],
       ["POST", "/fhir/$submit-attachment", "CDex", "Receive attachments / QRs"],
       ["GET/PUT", "/fhir/Task, /fhir/Task/{id}", "CDex", "Additional-information requests"],
       ["GET", "/fhir/metadata, /.well-known/smart-configuration", "all", "CapabilityStatement, SMART discovery"],
       ["GET/POST", "/um/prior-auth, /um/prior-auth/{id}/items/{seq}/decision", "internal", "Reviewer worklist and decisions (not an IG API)"]],
      [16 * mm, 72 * mm, 16 * mm, 72 * mm])

h1("12. Step 10: Run and verify every scenario")
steps(["<font face='Courier'>dotnet run --project src/DaVinci.PriorAuth.Api</font>",
       "In a second terminal (Git Bash or WSL on Windows): <font face='Courier'>./tools/run-scenarios.sh</font>",
       "Every line should print PASS. The script chains the flows the same way an EHR would: it reads the coverage-assertion-id "
       "from CRD, finds the CDex Task's tracking id, submits attachments and then calls $inquire."])
src(os.path.join(ROOT, "tools", "run-scenarios.sh"), "tools/run-scenarios.sh")
tip("To call the endpoints by hand, use the files in <font face='Courier'>samples/</font>, for example "
    "<font face='Courier'>curl -X POST http://localhost:5080/fhir/Claim/$submit -H \"Content-Type: application/fhir+json\" "
    "--data-binary @samples/pas-01-submit-cpap-approved.json</font>. To regenerate the samples after changing codes, run "
    "<font face='Courier'>python3 tools/make_samples.py</font>.")

# ---------------- security + production
h1("13. Step 11: Security and production hardening")
h2("13.1 Security (required before any real data)")
table([["Channel", "Mechanism", "What to configure"],
       ["EHR -> CRD (CDS Hooks)", "The EHR signs a JWT for every call (iss, aud = service URL, exp, jti; kid/jku) that the CRD service verifies against the EHR's JWKS",
        "JwtBearer with the EHR's JWKS as signing keys; policy crd.hooks = authenticated caller"],
       ["CRD -> EHR FHIR (prefetch fallback)", "fhirAuthorization.access_token from the hook request", "already used by EhrFhirClient"],
       ["EHR -> PAS / CDex", "SMART Backend Services: client_credentials + private_key_jwt, scopes system/Claim.c, system/DocumentReference.c, system/Task.rs",
        "Add Microsoft.AspNetCore.Authentication.JwtBearer, uncomment the block in Program.cs, set Payer:RequireAuthentication = true"],
       ["DTR app -> payer DTR", "SMART App Launch token issued by the payer (or the EHR, per trust agreement), scope user/Questionnaire.r", "CORS is open for the DTR app; restrict origins in production"],
       ["Payer -> provider notifications", "Subscription.channel.header (e.g. Authorization: Bearer ...) supplied by the provider", "sent by SubscriptionService"]],
      [36 * mm, 80 * mm, 60 * mm])
h2("13.2 Moving to a database (EF Core)")
table([["Repository", "Table", "Key columns"],
       ["ICoverageRuleRepository", "CoverageRule", "RuleId PK, PlanId, CodeSystem, Code (unique index PlanId+CodeSystem+Code), rule JSON columns"],
       ["ICoverageAssertionRepository", "CoverageAssertion", "CoverageAssertionId PK, PatientMemberId, OrderReference, DecisionJson, CreatedAt"],
       ["IPriorAuthRepository", "PriorAuthRequest + PriorAuthItem", "Id PK, BundleIdentifierKey (unique), ClaimIdentifierKey, TrackingId (unique), Status, DueAt, PreAuthRef, RequestBundleJson, LatestResponseJson"],
       ["IPriorAuthRepository (attachments)", "PriorAuthAttachment", "Id, RequestId FK, LoincCode, ContentType, BlobUri (store the payload in blob storage)"],
       ["IFhirResourceStore", "FhirResource", "ResourceType + Id PK, VersionId, LastUpdated, Url (for canonicals), Json"],
       ["ISubscriptionRepository", "Subscription", "Id PK, OrgIdentifierFilter, Endpoint, PayloadContent, Status, EventCount"]],
      [44 * mm, 38 * mm, 94 * mm])
h2("13.3 Checklist before go-live")
bullets([
    "<b>Profile validation</b>: run the HL7 FHIR validator (or the Firely validator) with the packages hl7.fhir.us.davinci-crd#2.0.1, "
    "-dtr#2.0.1, -pas#2.0.1, -cdex#2.0.0 and hl7.fhir.us.core against every sample, and in CI.",
    "<b>Conformance testing</b>: run the Inferno Da Vinci CRD, DTR and PAS test kits against the deployed API.",
    "<b>X12 278</b>: HIPAA still names the X12 278. Either map PAS Bundles to 278 through an intermediary or confirm your use of the "
    "CMS enforcement discretion for FHIR-only prior authorization.",
    "<b>Terminology</b>: confirm the code systems your EHR partners use (for example, HCPCS may arrive with a different system URI; "
    "InMemoryCoverageRuleRepository already accepts both). Load the real X12 886 reason codes.",
    "<b>CQL</b>: compile the libraries to ELM, add FHIRHelpers, and test pre-population with a real DTR client (the HL7 Da Vinci DTR "
    "reference app or an EHR-native DTR).",
    "<b>Metrics</b>: CMS-0057-F requires public reporting of approval/denial rates and decision times. Every record has "
    "SubmittedAt, DueAt and per-line decisions; add a DecidedAt timestamp to report turnaround.",
    "<b>Audit</b>: log every PHI access (who, what, when) as FHIR AuditEvent or to your SIEM. Do not log request bodies.",
    "<b>Performance</b>: CRD within 5 s (90% of calls) and PAS within 15 s end to end. Keep rule lookups in memory or cache them.",
])

# ---------------- verification appendix
h1("Appendix A: Specification alignment and items to confirm")
p("Every value was checked against the published CMS-0057-F versions (CRD 2.0.1, DTR 2.0.1, PAS 2.0.1, CDex 2.0.0) on "
  "hl7.org. A few things could not be confirmed from the published pages, or the IG itself is inconsistent about them. "
  "Confirm these with your trading partners:")
table([["Item", "What this project does", "Why it needs confirming"],
       ["DTR output parameter name", "PackageBundle (configurable)", "The DTR 2.0.1 OperationDefinition says 'return', but its output-parameters profile and example use 'PackageBundle'"],
       ["PAS SubscriptionTopic canonical", "http://hl7.org/fhir/us/davinci-pas/SubscriptionTopic/PASSubscriptionTopic, filter orgIdentifier", "The topic's JSON artifact could not be retrieved; the filter name comes from the topic's narrative"],
       ["PAS Task questionnaires-needed value", "Identifier (value = canonical) plus a CDex-style input sdc temp#questionnaire (valueCanonical)", "The PAS 2.0.1 Task profile types this input as an Identifier; DTR needs the canonical, so both are sent"],
       ["Claim update linkage", "Same Claim.identifier (as in the PAS update example) or Claim.related", "The published update example reuses the identifier and has no Claim.related"],
       ["ClaimResponse.outcome for pended", "queued", "The IG does not fix this value; 'queued' is the FHIR code for 'not yet complete'"],
       ["HCPCS code system URI", "https://bluebutton.cms.gov/resources/codesystem/hcpcs (the OID form is also accepted)", "EHRs vary"],
       ["CQL expression language", "text/cql, expression \"Library\".Define", "DTR 2.0.1 requires CQL and the \"LibraryName\".statement form; some clients expect text/cql-identifier"],
       ["X12 886 denial reasons", "free text + optional code", "X12 licensed code list"]],
      [36 * mm, 66 * mm, 74 * mm])
h2("References")
bullets([
    "CRD 2.0.1: https://hl7.org/fhir/us/davinci-crd/STU2/",
    "DTR 2.0.1: https://hl7.org/fhir/us/davinci-dtr/STU2/",
    "PAS 2.0.1: https://hl7.org/fhir/us/davinci-pas/STU2/",
    "CDex 2.0.0: https://hl7.org/fhir/us/davinci-cdex/STU2/",
    "CDS Hooks 2.0: https://cds-hooks.hl7.org/",
    "SMART App Launch 2.x / Backend Services: https://hl7.org/fhir/smart-app-launch/",
    "Subscriptions R5 Backport: https://hl7.org/fhir/uv/subscriptions-backport/",
    "SDC ($next-question, initialExpression): https://hl7.org/fhir/uv/sdc/",
    "CMS Interoperability and Prior Authorization Final Rule (CMS-0057-F): https://www.cms.gov/",
])

doc = Doc(OUT)
doc.multiBuild(story)
print("written", OUT)
