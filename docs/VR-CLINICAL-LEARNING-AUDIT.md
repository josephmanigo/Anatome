# Advanced procedural anatomy and clinical reasoning audit

The requested audience is medical students. The user requested multiple ways to learn beyond text, surgery or case scenarios, cognitive skill development, gamification, accuracy validation, and an audit with fixes. This revision extends the existing four modes and six cases rather than adding duplicate menus or calling every treatment a surgery.

## Required behavior

- Identify & Enumerate requires selecting and marking actual 3D structures. Assemble Anatomy requires gripping, rotating and placing anatomy in its spatial relationship. Guided Dissection requires cutting and reflecting layers while preserving protected structures. Anatomy Cases requires spatial findings, procedural instrument work, mechanism reasoning and response prediction.
- All six cases retain their established fictional indication. PCI, TAVI, chest drainage and aspiration are interventional procedural anatomy scenarios. Asthma and pneumonia are treatment scenarios. No open cardiac operation or validated surgical competency is claimed.
- Each case and difficulty includes two physical reasoning decisions: select a mechanism cutaway, and predict the treated structural response. Pleural effusion additionally requires stopping aspiration and reassessing new chest discomfort. Three physical choices are available; students grab and release a model or action symbol into the patient-plan dock. A text button cannot submit an answer.
- Wrong choices produce specific rationales. Unsafe choices in the aspiration symptom branch prevent an assessment pass even after correction. Unheld tokens, out-of-dock releases, and tracking/focus interruptions cannot create answer evidence. Independent Hard and assessment steps offer alternative instruments.
- Essential decisions join the existing formative score, 80 percent threshold, all-essential-objective requirement, retries, mastery locks and one-time objective XP. Cognitive and procedural feedback appear in the debrief. Lesson/practice demonstrations cannot count as unassisted assessment mastery.
- Curriculum revision `2026.10.1` preserves older attempts without allowing earlier assessments to unlock this replacement content. The additive migrations preserve historical definitions and account isolation. Version 1.1.0 is the new application build.

## Cognitive and procedural learning goals

| Mode | Observable learner task | Skill rehearsed |
|---|---|---|
| Identify & Enumerate | Find and mark unique visible structures without credit for repeats | Anatomical recognition and retrieval |
| Assemble Anatomy | Rotate and place structures in their reference relationships | Spatial reconstruction |
| Guided Dissection | Plan a layer opening, cut/reflect it and preserve neighboring structures | Layer relationships and procedural planning |
| Anatomy Cases | Inspect findings, compare cutaway mechanisms, perform the ordered procedure and predict response; respond to an unsafe branch | Causal reasoning, procedure sequencing and safety judgment |

An instructor can compare an unassisted baseline with a later unassisted assessment using errors, protected contacts, mechanism/response choices and essential-objective completion. A transfer task should change the presentation and ask the student to justify anatomy and risk, rather than repeat memorized token positions. XP and an in-game pass are engagement/progression signals; improvement and clinical transfer have not been measured in this task.

## Accuracy review

The new mechanism and response choices are original short teaching summaries, reviewed on 4 October 2026 against primary sources:

- [NHLBI pleural compartments](https://www.nhlbi.nih.gov/health/pleural-disorders), [NHLBI asthma](https://www.nhlbi.nih.gov/health/asthma), [NHLBI pneumonia](https://www.nhlbi.nih.gov/health/pneumonia), and [NHLBI pneumonia treatment](https://www.nhlbi.nih.gov/health/pneumonia/treatment) support compartment-specific mechanisms and the distinction between supportive oxygen and infection treatment.
- [BTS pleural procedures statement](https://www.brit-thoracic.org.uk/document-library/clinical-statements/pleural-procedures/bts-clinical-statement-on-pleural-procedures/) supports ultrasound-guided fluid access, preservation of neighboring anatomy, controlled aspiration and a symptom-triggered stop. The [2026 published correction](https://doi.org/10.1136/thorax-2022-219371corr1) concerns comparative drain size for pleurodesis efficacy. This application does not teach a pleurodesis drain-size equivalence claim.
- [NHS PCI procedure](https://www.nhs.uk/tests-and-treatments/coronary-angioplasty/what-happens/) supports wire, balloon and retained stent relationships. [Guy's and St Thomas' TAVI procedure](https://www.guysandstthomas.nhs.uk/health-information/tavi-aortic-stenosis/tavi-procedure) supports placing a replacement inside the native valve after heart-team assessment.
- [University Hospitals Sussex spacer technique](https://www.uhsussex.nhs.uk/resources/inhaler-technique-metered-dose-inhalers-with-aerochamber-spacer/) supports the represented single-puff metered-dose device sequence. The model does not generalize this device sequence to every inhaler.
- [NICE pneumonia recommendations, NG250](https://www.nice.org.uk/guidance/NG250/chapter/recommendations) were rechecked for the represented intravenous-antibiotic review by 48 hours. The activity follows a fictional prescribed plan and does not ask the game to prescribe a drug or dose.

The 3D cutaways are original enlarged schematics; response states are illustrative and time-compressed. Their authored interaction tolerances are gameplay values, not patient measurements. Canonical anatomy and raw source assets remain separate. Existing detailed case-source and heart-dissection records continue to define reconstruction limits.

Source review and correct software scoring do not measure students' cognitive improvement or clinical transfer. The product provides formative rehearsal; it does not issue a clinical qualification. Educational benefit needs an actual learner evaluation with a defined pre/post or transfer task.

## Verification record

The current results, exact build/ZIP hashes, source parity, review findings and unresolved gates are recorded in `qa/clinical-learning/verification-report.json`. The XR run exercises queued controller events through actual XRI interactions. The development player separately runs all 72 scenario fixtures with its disclosed tracking bypass. Neither replaces physical Quest evidence.

The code review compares this task's working changes with `.tmp/clinical-learning-before`, captured before implementation. It excludes unrelated pre-existing dirty work. The user request above and PRODUCT.md are its behavior contract; DESIGN.md and docs/IMPLEMENTATION.md are its incumbent constraints.

The live project was paused and produced a reproduced DNS failure. It was resumed, and both October migrations were applied in one transaction. Current dashboard readback verified all 72 replacement rubrics, all three historical versions, 39 decisions, three critical decisions and preserved RLS/permissions. A stored-rubric fixture confirmed an unsafe choice remains failed after correction. Five public API probes passed after recovery. The live migration report and screenshot preserve the proof; learner authentication/synchronization still require current end-to-end acceptance.

Standards review found two P2 issues: choice labels did not follow accessibility size, and clinical steps lacked spatial guided help. Both were fixed and rechecked. Runtime checks then exposed a world-space font-unit error and the rendered review exposed partial label occlusion; the final size, height, forward label placement and separation from the specimen address those findings. The initially failed reports are retained. Avoidable per-frame allocations were removed. The separate spec review found no actionable implementation defect. These reviews exclude unrelated pre-existing work and do not certify patient treatment or measured learning outcomes.

Production acceptance requires the current APK's physical headset flow and sustained native performance, publisher release signing, and current connected learner persistence/synchronization. No software report or historical live report closes these gates.
