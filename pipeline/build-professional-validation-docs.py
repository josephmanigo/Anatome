"""Export the shipped learning catalog as a professional review packet.

Run with the Codex bundled Python. Source content is never rewritten.
"""
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs' / 'professional-validation'
QA = ROOT / 'qa' / 'professional-validation'
CONTENT = ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning'
CATALOG = CONTENT / 'catalog.json'
ANATOMY = ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json'
catalog = json.loads(CATALOG.read_text(encoding='utf-8'))
anatomy = {x['id']: x for x in json.loads(ANATOMY.read_text(encoding='utf-8'))['records']}
guide = json.loads((CONTENT / 'how-to-play.json').read_text(encoding='utf-8'))
MODES = ['Anatomy Cases', 'Identify & Enumerate', 'Assemble Anatomy', 'Guided Dissection']
PREFIX = ['CASE', 'ID', 'ASM', 'DIS']
LEVELS = ['Easy', 'Medium', 'Hard']
ACTIONS = ['Select', 'Place', 'Expose', 'InspectFinding', 'EquipmentConnect',
           'ToolContact', 'ToolActivate', 'Trace', 'Reassess',
           'ProtectedStructureViolation', 'ClinicalDecision']
TOOLS = {'probe': 'Assessment pointer', 'anaesthetic': 'Local anaesthetic syringe',
         'guidewire': 'Guidewire', 'dilator': 'Short dilator', 'drain': 'Chest drain',
         'connector': 'Circuit connector', 'dressing': 'Fixation dressing',
         'aspirator': 'Aspiration catheter', 'medication': 'Prescribed medicine',
         'oxygen': 'Oxygen mask', 'valve': 'Valve delivery handle',
         'dissection-saw': 'Bone saw', 'dissection-scalpel': 'Scalpel'}
OUT.mkdir(parents=True, exist_ok=True)
QA.mkdir(parents=True, exist_ok=True)
doc = Document()
section = doc.sections[0]
section.page_width = Inches(8.27)
section.page_height = Inches(11.69)
section.top_margin = section.bottom_margin = Inches(.67)
section.left_margin = section.right_margin = Inches(.72)
section.header_distance = section.footer_distance = Inches(.27)
for s in ['Normal', 'Title', 'Subtitle', 'Heading 1', 'Heading 2', 'Heading 3']:
    style = doc.styles[s]
    style.font.name = 'Arial'
    style.font.color.rgb = RGBColor(0, 0, 0)
    style.paragraph_format.space_after = Pt(5)
doc.styles['Normal'].font.size = Pt(10)
doc.styles['Normal'].paragraph_format.line_spacing = 1.06
doc.styles['Title'].font.size = Pt(26)
doc.styles['Heading 1'].font.size = Pt(19)
doc.styles['Heading 2'].font.size = Pt(14)
doc.styles['Heading 3'].font.size = Pt(11)
for s in ['Heading 1', 'Heading 2', 'Heading 3']:
    doc.styles[s].paragraph_format.space_before = Pt(11)
    doc.styles[s].paragraph_format.keep_with_next = True
doc.styles['Heading 3'].paragraph_format.space_before = Pt(8)
for style in doc.styles:
    if style.element.pPr is not None:
        for border in list(style.element.pPr.findall(qn('w:pBdr'))):
            style.element.pPr.remove(border)
footer = section.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.RIGHT
footer.add_run('ANATOME professional review  |  ')
field = OxmlElement('w:fldSimple')
field.set(qn('w:instr'), 'PAGE')
footer._p.append(field)
for r in footer.runs:
    r.font.size = Pt(8)
doc.core_properties.title = 'ANATOME questions and procedures for professional validation'
doc.core_properties.subject = 'All four learning modes and every difficulty in curriculum 2026.10.2'
doc.core_properties.author = 'ANATOME project'
md = []
coverage = []
entry_count = 0
question_count = 0

def para(text='', label=None, small=False, keep=False):
    p = doc.add_paragraph()
    if label:
        p.add_run(label + ' ').bold = True
    p.add_run(text)
    p.paragraph_format.keep_with_next = keep
    if small:
        for run in p.runs:
            run.font.size = Pt(8.5)
    md.append((('**' + label + '** ') if label else '') + text + '\n')
    return p

def heading(text, level=1, new=False):
    # Source titles remain verbatim in body text; headings use plain words.
    clean = re.sub(r'[^\w\s]', ' ', text.replace('&', 'and').replace("specimen's", 'specimen'))
    clean = re.sub(r'\s+', ' ', clean).strip()
    p = doc.add_heading(clean, level)
    if new:
        p.paragraph_format.page_break_before = True
    md.append('#' * (level + 1) + ' ' + clean + '\n')

def table(headers, rows, widths):
    t = doc.add_table(rows=1, cols=len(headers))
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    for col, width in zip(t.columns, widths):
        col.width = Inches(width)
    borders = OxmlElement('w:tblBorders')
    for side in ['top', 'left', 'bottom', 'right', 'insideH', 'insideV']:
        b = OxmlElement('w:' + side)
        for k, v in [('val', 'single'), ('sz', '4'), ('color', 'D9D9D9')]:
            b.set(qn('w:' + k), v)
        borders.append(b)
    t._tbl.tblPr.append(borders)
    for i, values in enumerate([headers] + list(rows)):
        row = t.rows[0] if i == 0 else t.add_row()
        trPr = row._tr.get_or_add_trPr()
        trPr.append(OxmlElement('w:cantSplit'))
        if i == 0:
            trPr.append(OxmlElement('w:tblHeader'))
        for j, (cell, value, width) in enumerate(zip(row.cells, values, widths)):
            cell.width = Inches(width)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            p = cell.paragraphs[0]
            p.paragraph_format.space_before = Pt(3)
            p.paragraph_format.space_after = Pt(3)
            p.paragraph_format.line_spacing = 1.03
            p.paragraph_format.keep_with_next = i == 0
            if j == 0 and width < 1.2:
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            run = p.add_run(str(value))
            run.font.size = Pt(9)
            run.bold = i == 0
            pr = cell._tc.get_or_add_tcPr()
            shade = OxmlElement('w:shd')
            shade.set(qn('w:fill'), 'E5EBF0' if i == 0 else ('F7F8FA' if i % 2 == 0 else 'FFFFFF'))
            pr.append(shade)
            margins = OxmlElement('w:tcMar')
            for side in ['top', 'bottom', 'left', 'right']:
                m = OxmlElement('w:' + side)
                m.set(qn('w:w'), '85')
                m.set(qn('w:type'), 'dxa')
                margins.append(m)
            pr.append(margins)
    md.append('| ' + ' | '.join(headers) + ' |')
    md.append('| ' + ' | '.join('---' for _ in headers) + ' |')
    for row in rows:
        md.append('| ' + ' | '.join(str(v).replace('\n', '<br>').replace('|', '\\|') for v in row) + ' |')
    md.append('')
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t

def named(identifier):
    if identifier in anatomy:
        return anatomy[identifier]['name'] + ' [' + identifier + ']'
    return identifier

def tool(identifier):
    return TOOLS.get(identifier, identifier[:1].upper() + identifier[1:]) if identifier else 'No instrument required'

def same_fields(a, b):
    fields = ['title', 'instruction', 'explanation', 'targetId', 'action', 'region',
              'toolId', 'equipmentState', 'essential', 'weight', 'choices', 'criticalOnWrong']
    return all(a.get(k) == b.get(k) for k in fields)

def review_lines():
    p = para('Accept / Revise / Remove / Not reviewed. Record item IDs, corrected wording, and supporting references.\nComments _______________________________________________________________________', label='Reviewer decision', small=True)
    p.paragraph_format.keep_together = True

variants = [v for u in catalog['units'] for v in u['variants']]
objectives = [o for v in variants for o in v['objectives']]
assert len(variants) == 72 and len(objectives) == 645
assert all(o['essential'] for o in objectives)
assert len({o['id'] for o in objectives}) == len(objectives)
for u in catalog['units']:
    for v in u['variants']:
        assert v['protectedIds'] == u['protectedIds']
        d, m = v['difficulty'], v['mode']
        for o in v['objectives']:
            a = o['action']
            if m == 0:
                expected = (.09, 180, 0, 0, 2) if a == 10 else (
                    [.075, .05, .03][d], [55, 35, 22][d],
                    [.6, .9, 1.2][d] if a >= 4 else 0, .95 if a == 7 else 0,
                    2 if a >= 4 else 1)
            else:
                expected = ([.12, .065, .04][d] if m == 2 else [.075, .04, .02][d],
                            [55, 35, 20][d] if m == 2 else [35, 20, 10][d],
                            0, [.003, .005, .008][d] if a == 2 else 0, 1)
            actual = tuple(o[k] for k in ['maxDistance', 'maxAngle', 'minContactSeconds', 'minPathFraction', 'weight'])
            assert actual == expected, (o['id'], 'threshold table requires update')

doc.add_paragraph('ANATOME questions and procedures for professional validation', 'Title')
md.append('# ANATOME questions and procedures for professional validation\n')
para('Professional content review packet', label='Purpose')
para('Curriculum ' + catalog['version'] + '  |  Prepared 27 October 2026  |  Review status Pending')
para('Please review the anatomical accuracy, educational suitability, wording, answer keys, procedure sequences, and safety limitations of all four ANATOME learning modes. Record corrections against the item IDs in this packet before approving content for the intended student audience.')
para('The packet covers the current project content: 12 units, 72 difficulty variants, and 645 scored objectives. It contains 247 distinct task entries. The 13 distinct case decision questions each appear at three difficulties, giving 39 decision objectives. Repeated content is presented once with its exact difficulty coverage.')
para('These are virtual learning activities for medical students. Case indications are predetermined and responses are illustrative. Approval of teaching content is separate from clinical competence, educational effectiveness, and headset usability validation. No professional approval has been recorded in this packet.')
table(['Mode', 'Scope', 'Variants', 'Objectives'], [
    [MODES[m], '6 cases' if m == 0 else '6 anatomy units',
     sum(v['mode'] == m for v in variants),
     sum(len(v['objectives']) for v in variants if v['mode'] == m)] for m in range(4)
], [1.65, 2.1, .9, 1.55])
para('Professional name __________________________  Specialty __________________________')
para('Institution ________________________________  Review date ________________________')
para('Learner group _____________________________  Build tested _________________________')
heading('How to use this packet', 2)
para('1. Review the common procedure and scoring rules. Then review each mode within your area of expertise.')
para('2. Read the verbatim learner prompts and expected responses. Use the difficulty labels and thresholds to judge progression.')
para('3. Record corrections using the compact item ID, such as CASE1 02. Complete the review form and state any conditions for use.')
para('Use Word navigation headings to jump to a mode or unit. The final sections contain all ten Guide pages, the source register, the 72-variant coverage register, and the professional review form.', small=True)

heading('Common procedure and assessment rules', new=True)
heading('Entry and phases', 2)
para('Complete the VR control orientation: select the skin, grip it, lift it by at least 0.04 m in the virtual scene, and release. This unlocks Easy activities. Reading the Guide does not complete orientation. Choose a mode, unit, difficulty, and phase: Learn step by step, Practice, or Assessment.')
para('Lesson introduces the activity and demonstrations. Practice allows rehearsal and answer-revealing help. Assessment requires independent interaction; answer-revealing demonstrations are unavailable. The general Guide explains controls and pauses specimen interaction while open. There is no compulsory countdown and retries are unlimited.')
para('Pass the current curriculum Easy assessment to unlock Medium in the same mode and unit. Pass Medium to unlock Hard. Lesson or Practice completion does not satisfy these assessment locks.')
heading('How the game accepts an answer', 2)
para('The accepted response is a verified interaction with the required anatomy, tool, or decision model. Selecting a text answer or pressing Finish does not generate objective credit. Each action must match its target, action type, required instrument, equipment state, and applicable numeric thresholds. Required earlier objectives must already be complete.')
para('Anatomy Cases and Guided Dissection use the listed sequence at every difficulty. Identify and Enumerate is ordered in Easy; Medium and Hard allow eligible selections in any order. Assemble Anatomy currently offers and accepts the current piece in sequence at every difficulty, even though its Medium and Hard catalog rubrics are marked unordered. Review this distinction when judging task design.')
para('All 645 objectives are essential. An accepted duplicate observation of an already completed objective adds no credit and no ordinary mistake. Other submitted actions that do not match the current or eligible objective can add a mistake. Some incomplete gestures are filtered before submission, so a physical misalignment does not always produce a scoring event.')
heading('Score and pass conditions', 2)
para('Score = max(0, 100 × completed objective weight / total objective weight − min(50, 5 × mistakes)).')
para('A pass requires a score of at least 80, every essential objective complete, no critical failure, and no assisted flag. Weights are 1 for anatomy objectives and case finding selections; case procedure and decision objectives have weight 2. With every objective complete, four ordinary mistakes yield 80; five yield 75. A high score cannot override a critical failure.')
para('A verified protected-structure violation causes a critical failure. Choosing Continue draining or Dismiss the symptom at the pleural-effusion safety decision also causes a critical failure when submitted as a valid dock interaction. Correcting the answer later does not remove that failure from the attempt.')
para('Only completed, passed, unassisted assessments can unlock harder content and award new objective mastery rewards. The software rule is a proposed formative standard for professional review; it is not a clinically validated pass mark.')
heading('Review questions about assessment design', 2)
para('Are all essential objectives appropriate? Is the 80-point pass rule defensible for this learner group? Should an error be recoverable or a critical failure? Can the physical interaction demonstrate the intended knowledge without relying on remembered positions?')
review_lines()

heading('Mode procedures and difficulty settings', new=True)
mode_procedures = [
    'Inspect the finding on the specimen. Grip the mechanism model and release it into the patient-plan dock. Complete the authored instrument steps in order. Place the predicted response model in the dock, reassess the represented anatomy, and finish. In pleural effusion, complete the symptom-triggered safety decision before continuing. Hard and Assessment offer alternative instruments; successful choices must still satisfy the same objective.',
    'Read the requested anatomical name. Inspect the visible specimen, point at the correct structure, and press and release the trigger. Each accepted unique structure gains a marker. Easy contains the first three entries, Medium the first five, and Hard all eight in each unit. The required structures and their exact prompts are listed in the mode section.',
    'Grip the offered piece, move it into its anatomical relationship, rotate it, and release. Easy, Lesson, and Practice show a placement outline; Medium and Hard Assessments omit it. Accepted pieces return to the reference pose. Easy contains the first three entries, Medium the first five, and Hard all eight. Both position and rotation must meet the difficulty threshold.',
    'Work through every prepared station in the listed order. For a removal step, point at the named tissue, hold trigger and sweep a short cut, then release. Grip the detached chunk, lift it at least 0.06 m clear in the virtual scene, and release. Repeat until the removal meter is full. Whole layers stay anchored. For a preservation step, select and examine the named anatomy without cutting it. All difficulties cover the same stations.'
]
for m, text in enumerate(mode_procedures):
    heading(MODES[m], 2)
    para(text)
para('Dissection automatically selects the bone saw for bone and the scalpel for other tissue. Named left or right regions use the specimen’s side; a chunk crossing the midline does not complete that regional requirement. Prepared openings and the removed-volume fractions are software teaching mechanisms, not clinical incision dimensions. Non-assessment hints show where to cut or lift but do not perform or score the action.')
heading('Gameplay thresholds', 2, new=True)
table(['Rule', 'Easy', 'Medium', 'Hard'], [
    ['Assembly maximum position error', '0.12 m', '0.065 m', '0.04 m'],
    ['Assembly maximum angular error', '55 degrees', '35 degrees', '20 degrees'],
    ['Anatomy Select and Expose rubric distance', '0.075 m', '0.04 m', '0.02 m'],
    ['Anatomy Select and Expose rubric angle', '35 degrees', '20 degrees', '10 degrees'],
    ['Dissection minimum removed fraction', '0.003 (0.3%)', '0.005 (0.5%)', '0.008 (0.8%)'],
    ['Case non-decision maximum distance', '0.075 m', '0.05 m', '0.03 m'],
    ['Case non-decision maximum angle', '55 degrees', '35 degrees', '22 degrees'],
    ['Case instrument contact minimum', '0.6 s', '0.9 s', '1.2 s'],
    ['Case Trace minimum path fraction', '0.95', '0.95', '0.95'],
    ['Case decision dock distance and angle', '0.09 m / 180 degrees', '0.09 m / 180 degrees', '0.09 m / 180 degrees']
], [2.65, 1.15, 1.15, 1.25])
para('These are virtual interaction tolerances. Selection and chunk-release events currently submit zero distance and angle; their rubric values therefore do not measure selection precision. Expose uses the removed-volume fraction, while case Trace uses route completion. Case finding and decision steps require no contact hold; other case instrument objectives use the contact minimum above. Clinical decisions require no route fraction.', small=True)
heading('Free dissection sandbox', 2)
para('Free dissection sandbox is part of Guided Dissection, not a fifth game mode. It has no authored questions, scored objectives, or difficulty variants. The normal assessment engine is absent, so sandbox work does not award assessment mastery.')
para('Open Guided Dissection, then Free dissection sandbox. Explore the intact specimen, select a tissue and cutting tool, and make temporary cuts. Use Undo for eligible uncollected cuts or Restore specimen to reset the model. The workspace states: “Dissection sandbox · cuts are temporary. Use Undo or Restore specimen.”')
para('Review whether the distinction between free exploration and a clinical procedure is clear, and whether the prepared specimen and protected anatomy are suitable for teaching.')
review_lines()

heading('Anatomy unit teaching text', new=True)
para('The following summaries and lessons are verbatim catalog text shared by Identify and Enumerate, Assemble Anatomy, and Guided Dissection. Protected structures apply to the corresponding unit in each mode.')
anatomy_unit_index = 0
for u in catalog['units']:
    if u['isCase']:
        continue
    heading(u['title'], 2, new=anatomy_unit_index == 3)
    anatomy_unit_index += 1
    para(u['summary'], label='Summary')
    para(u['lesson'], label='Lesson')
    para('; '.join(named(x) for x in u['protectedIds']), label='Preserve', small=True)
    para(', '.join(u['sourceIds']), label='Reference IDs', small=True)

for mode in range(4):
    heading(MODES[mode], new=True)
    para(mode_procedures[mode])
    if mode == 0:
        para('The case questions are physical model choices. “Expected” identifies the current software answer key for review; it does not record professional endorsement. Choices are listed in catalog order; their positions in the running activity can vary.')
    else:
        para('These modes use performance prompts rather than a separate written-question bank. Every prompt and expected target is included below. Shared teaching text appears in Anatomy unit teaching text.')
    relevant = [u for u in catalog['units'] if any(v['mode'] == mode for v in u['variants'])]
    for unit_index, u in enumerate(relevant, 1):
        vs = sorted([v for v in u['variants'] if v['mode'] == mode], key=lambda v: v['difficulty'])
        base = vs[-1]
        heading(f'{PREFIX[mode]}{unit_index} {u["title"]}', 2, new=unit_index > 1)
        para(' / '.join(v['id'] for v in vs), label='Scenario IDs', small=True)
        if u['isCase']:
            para(u['pathway'], label='Treatment pathway')
            para(u['summary'], label='Scenario')
            para(u['lesson'], label='Lesson')
            para('; '.join(named(x) for x in u['protectedIds']), label='Preserve', small=True)
            para(', '.join(u['sourceIds']), label='Reference IDs', small=True)
        else:
            para('; '.join(named(x) for x in u['protectedIds']), label='Preserve', small=True)
        # Keep every distinct briefing and debrief without repeating identical text.
        for field_name, label in [('briefing', 'Briefing'), ('debrief', 'Debrief')]:
            texts = {}
            for v in vs:
                texts.setdefault(v[field_name], []).append(LEVELS[v['difficulty']])
            for text, levels in texts.items():
                para(text, label=label + ' for ' + ', '.join(levels), small=True)
        rows = []
        for pos, o in enumerate(base['objectives'], 1):
            entry_count += 1
            item_id = f'{PREFIX[mode]}{unit_index} {pos:02d}'
            present = []
            for v in vs:
                if pos > len(v['objectives']):
                    continue
                other = v['objectives'][pos - 1]
                assert same_fields(o, other), (item_id, v['id'], 'unhandled content difference')
                present.append(LEVELS[v['difficulty']])
                coverage.append({'reviewItem': item_id, 'scenarioId': v['id'], 'objectiveId': other['id'],
                                 'ordinal': pos, 'difficulty': LEVELS[v['difficulty']], 'objective': other})
            expected_order = mode in [0, 3]
            for v in vs:
                if pos > len(v['objectives']):
                    continue
                prerequisites = v['objectives'][pos - 1]['prerequisites']
                expected = [v['objectives'][pos - 2]['id']] if pos > 1 and (expected_order or v['difficulty'] == 0) else []
                assert prerequisites == expected, (v['id'], pos, 'unhandled prerequisite')
            if mode in [1, 2]:
                rows.append([item_id + '\n' + ', '.join(present), o['title'] + '\n' + o['instruction'],
                             o['targetId'] + '\n' + o['explanation']])
                continue
            block_start = len(doc.paragraphs)
            heading(item_id + ' ' + o['title'], 3)
            # The exact source title also appears in body text to retain punctuation.
            if o['title'] not in doc.paragraphs[-1].text:
                para(o['title'], label='Task')
            para(o['instruction'], label='Learner prompt')
            if o['action'] == 10:
                question_count += 1
                assert len([c for c in o['choices'] if c['id'] == o['toolId']]) == 1
                for i, choice in enumerate(o['choices']):
                    status = 'Expected' if choice['id'] == o['toolId'] else 'Alternative'
                    para(choice['label'] + '. ' + choice['rationale'], label=f'{chr(65+i)} {status}')
                if o.get('criticalOnWrong'):
                    para('Either alternative, when validly submitted to the dock, creates a critical failure that remains after correction.', label='Safety rule')
                para(o['explanation'], label='Success feedback')
                para('Grip and release the expected model in ' + o['targetId'] + '; required state ' + o['equipmentState'] + '.', label='Acceptance', small=True)
            else:
                details = named(o['targetId']) + '; action ' + ACTIONS[o['action']] + '; ' + tool(o.get('toolId', ''))
                if o.get('equipmentState'):
                    details += '; required state ' + o['equipmentState']
                if o.get('region'):
                    details += '; region ' + o['region']
                para(details + '.', label='Expected interaction', small=True)
                para(o['explanation'], label='Success feedback', small=True)
            para('All three difficulties. Essential. Weight ' + str(o['weight']) + '. ' +
                 ('First step; no prerequisite.' if pos == 1 else f'Complete {PREFIX[mode]}{unit_index} {pos-1:02d} first.'), small=True)
            for p in doc.paragraphs[block_start:-1]:
                p.paragraph_format.keep_with_next = True
        if rows:
            table(['Item and levels', 'Exact task and learner prompt', 'Target ID and success feedback'], rows, [1.05, 3.9, 1.25])
            para('Every listed task is essential with weight 1. Identification accepts a verified selection of the target. Assembly accepts release of the current whole structure within the position and angle tolerances. Repeating a target does not add credit.', small=True)
        # Unit breaks leave space for annotations; use the final form for decisions.

heading('Complete shared Guide text', new=True)
para('These ten pages are the in-game control and learning guide. Their text is reproduced verbatim. They are instructions, not scored questions.')
for page in guide['pages']:
    heading(page['title'], 2)
    para(page['id'], small=True)
    for block in page['text'].split('\n\n'):
        para(block)

heading('Source register and review priorities', new=True)
para('Reference titles, URLs, and recorded access dates below are retained from the current content catalog. They identify the references used by the project; the recorded date is not a new literature review date. Reviewers should confirm current guidance, local teaching standards, and whether each cited source supports the exact claim. The catalog describes the text as original educational summaries and reports no reproduced figures or guideline tables.')
for source in catalog['sources']:
    block_start = len(doc.paragraphs)
    heading(source['id'] + ' ' + source['title'], 3)
    para(source['url'], small=True)
    para('Catalog access date ' + source['accessed'] + '. ' + source['usage'], small=True)
    for p in doc.paragraphs[block_start:-1]:
        p.paragraph_format.keep_with_next = True
heading('Questions requiring professional judgment', 2)
for text in [
    'Are the represented thoracic anatomy, laterality, reconstructed tissues, enlarged cutaways, and reference spaces anatomically appropriate? Review the 3D specimen as well as the text.',
    'Are the six predetermined treatment indications, instrument sequences, protected relationships, response predictions, and omitted clinical details appropriate for these students? Identify any wording that could be mistaken for complete patient-care instruction.',
    'Does each decision have a defensible answer key and plausible alternatives? Are the mechanism explanations, symptom-stop branch, and feedback educationally accurate?',
    'Do simulated cutting fractions, preparation windows, placement guides, and instrument actions teach the intended concepts without implying a validated surgical technique?',
    'Are the assessment threshold, penalties, essential objectives, difficulty progression, and assistance rules appropriate? Specify what additional learner or headset evaluation is needed.'
]:
    para(text)

heading('Coverage register')
para('Each row identifies one complete catalog variant. The item ranges map to the numbered entries in this packet. All entries in a range are essential. The accompanying machine-readable coverage file retains the exact original objective IDs and every threshold for audit and regeneration.')
register = []
for mode in range(4):
    relevant = [u for u in catalog['units'] if any(v['mode'] == mode for v in u['variants'])]
    for i, u in enumerate(relevant, 1):
        for v in sorted([v for v in u['variants'] if v['mode'] == mode], key=lambda v: v['difficulty']):
            register.append([v['id'], LEVELS[v['difficulty']], str(len(v['objectives'])),
                             f'{PREFIX[mode]}{i} 01 to {len(v["objectives"]):02d}'])
table(['Scenario ID', 'Difficulty', 'Count', 'Review item range'], register, [2.7, .85, .55, 2.1])
heading('Project snapshot', 2)
para('Content version ' + catalog['version'] + '. Snapshot date 27 October 2026. This identifies the current working-tree curriculum, not the contents of an independently inspected APK.')
source_files = [CATALOG, ANATOMY, CONTENT / 'how-to-play.json',
                ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Learning/Core/AssessmentEngine.cs',
                ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Learning/LabRuntime.cs',
                ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Learning/ProcedureWorkbench.cs',
                ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Learning/GuidedInteractionCoach.Dissection.cs']
hashes = {str(p.relative_to(ROOT)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_files}
para(str(CATALOG.relative_to(ROOT)).replace('\\', '/'), label='Catalog file', small=True)
para(hashes[str(CATALOG.relative_to(ROOT)).replace('\\', '/')], label='Catalog SHA256', small=True)
para('Exact source IDs, paths, hashes, and coverage are retained in ANATOME-CONTENT-COVERAGE.json beside this document. No game content is modified by this export.', small=True)

heading('Professional validation form', new=True)
form_start = len(doc.paragraphs)
para('Reviewer name __________________________________  Specialty _____________________')
para('Institution ______________________________________  Date ________________________')
para('Curriculum reviewed 2026.10.2   Build observed _____________________________________')
para('Evidence reviewed: Written packet / 3D specimen / Live activity / Other ______________')
para('Review scope: All modes / Anatomy Cases / Identify and Enumerate / Assemble Anatomy / Guided Dissection')
heading('Content ratings', 2)
para('Rate each criterion: 1 = major correction needed; 2 = revision needed; 3 = acceptable with minor changes; 4 = acceptable; NR = not reviewed. These are review prompts, not a validated measurement instrument.')
table(['Criterion', 'Rating', 'Comment or item IDs'], [
    ['Anatomical accuracy and terminology', '', ''],
    ['Question and instruction clarity', '', ''],
    ['Answer keys and feedback', '', ''],
    ['Procedure order and safety', '', ''],
    ['Appropriateness for intended learners', '', ''],
    ['Difficulty progression and scoring', '', ''],
    ['3D representations and simulation limitations', '', ''],
    ['Reference support and currency', '', '']
], [3.0, .65, 2.55])
heading('Required corrections', 2)
para('Copy this section for additional comments. Retain the item ID so a correction can be traced to the game.')
para('Item or scenario ID ____________________________________________________________')
para('Issue and reason ______________________________________________________________')
para('________________________________________________________________________________')
para('Proposed correction ___________________________________________________________')
para('________________________________________________________________________________')
para('Supporting reference or guideline ______________________________________________')
para('Priority: Safety critical / Required before student use / Minor editorial')
heading('Overall decision', 2)
para('Accept for the stated teaching use / Accept after specified revisions / Re-review required / Not acceptable for the stated use / Unable to judge')
para('Conditions or excluded content __________________________________________________')
para('________________________________________________________________________________')
para('Reviewer signature ____________________________  Date ___________________________')
para('Review of content does not certify clinical competence or establish educational effectiveness. Record any separate requirements for learner studies, hardware usability, and implementation verification.')
for p in doc.paragraphs[form_start:]:
    p.paragraph_format.space_after = Pt(4)

# A completeness check fails on omitted content, inconsistent grouping, or lost options.
assert entry_count == 247 and question_count == 13
assert Counter(c['objectiveId'] for c in coverage) == Counter(o['id'] for o in objectives)
assert sum(c['objective']['action'] == 10 for c in coverage) == 39
assert sum(c['objective'].get('criticalOnWrong', False) for c in coverage) == 3
assert {s for u in catalog['units'] for s in u['sourceIds']} <= {s['id'] for s in catalog['sources']}
body = '\n'.join(md)
for o in objectives:
    for key in ['title', 'instruction', 'explanation']:
        assert o[key] in body, (o['id'], key)
    for choice in o.get('choices', []):
        assert choice['label'] in body and choice['rationale'] in body
for u in catalog['units']:
    assert u['lesson'] in body and u['summary'] in body
    for v in u['variants']:
        assert v['briefing'] in body and v['debrief'] in body
for page in guide['pages']:
    assert all(block in body for block in page['text'].split('\n\n'))
docx_path = OUT / 'ANATOME-Professional-Validation.docx'
doc.save(docx_path)
(OUT / 'ANATOME-Professional-Validation.md').write_text(body, encoding='utf-8')
(OUT / 'ANATOME-CONTENT-COVERAGE.json').write_text(json.dumps({
    'version': catalog['version'], 'snapshotDate': '2026-10-27',
    'counts': {'units': 12, 'modes': 4, 'variants': 72, 'objectives': 645,
               'reviewEntries': entry_count, 'distinctDecisionQuestions': question_count,
               'decisionObjectives': 39, 'guidePages': len(guide['pages'])},
    'sourceHashes': hashes, 'coverage': coverage
}, indent=2) + '\n', encoding='utf-8')
report = {'checksPassed': ['all 645 objectives mapped exactly once', 'all 72 variants present',
                         'all task titles prompts and feedback preserved',
                         'all decision labels answer keys and rationales preserved',
                         'all lessons briefings debriefs and guide text preserved',
                         'difficulty grouping and prerequisite chains checked'],
          'document': str(docx_path), 'reviewEntries': entry_count, 'distinctDecisionQuestions': question_count}
(QA / 'content-verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report))
