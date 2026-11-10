# Case controls and browser return — Anatome 1.4.1

This update retains curriculum 2026.10.2 and its assessment definitions. It changes the interface, tool interaction and reference navigation. Previous results remain compatible; no new database migration is needed.

## What changed

- The BodyParts3D directory URL returned HTTP 404. The button now links to the publisher's database description page.
- Source details and the game's lesson summary can be read inside VR. Opening the publisher's website is a separate action. The Quest build uses Horizon's system browser launch, saves the current step and keeps a visible return action. When the OS returns focus, the activity returns to the same step. Pause and focus are tracked separately.
- New case lessons begin with the patient summary and controls. **Case and controls** remains available during a case, including an action to return loose tools to the tray.
- The current step shows pickup, working-end position, wrist alignment, route point and contact progress. Tools have readable names and a working-end marker. Guided routes have numbered points and an entry-direction guide.
- Grip and movement handle contact, connections and tracing. Trigger operates an active control. A completed stationary gesture cannot automatically complete the next step. Waiting for the inhalation phase does not record an incorrect action.
- **Show me this step** previews tool movement with a translucent, non-colliding model. It submits no assessment evidence. Choice feedback names the selected option and preserves its authored explanation.
- The pneumonia medicine uses a needle-free IV connector. Coronary access shows a radial compression band. Respiratory monitoring has a visible finger beside the monitor, matching the instruction's contact location.
- Withdrawal restores the last accepted working-end pose, including after returning loose tools or resuming a saved step. Pleural withdrawal keeps the catheter pointing inward while it moves out. Coronary withdrawal starts at the treated lesion and follows the full return route.

## Sources and limits

The repaired source is the [BodyParts3D database description](https://dbarchive.biosciencedbc.jp/en/bodyparts3d/desc.html). Browser handling follows Meta's [application lifecycle](https://developers.meta.com/vr/documentation/unity/unity-lifecycle/) and [system deep-link documentation](https://developers.meta.com/vr/documentation/unity/ps-system-deep-linking/).

The case audit checked the [NHS coronary angioplasty explanation](https://www.nhs.uk/tests-and-treatments/coronary-angioplasty/what-happens/), [BTS pleural procedure statement](https://www.brit-thoracic.org.uk/document-library/clinical-statements/pleural-procedures/bts-clinical-statement-on-pleural-procedures/), [NHS spacer instructions](https://www.elft.nhs.uk/sites/default/files/2022-01/howtouseaspacer-updatedmarch2017-UPDATED.pdf), and [Guy's and St Thomas' TAVI explanation](https://www.guysandstthomas.nhs.uk/health-information/tavi-aortic-stenosis/tavi-procedure). Trigger and grip remain VR controls, not instructions for operating real medical equipment.

Cases still use enlarged educational models, illustrative responses and fixed treatment plans. Pointer checks, routes, suturing and deployments represent tasks; the program does not reproduce tissue forces, every device mechanism, medication dosing or clinical competence. The controls page states these limits.

## Quest retest

1. Install 1.4.1 as an update. Open **Anatomy Cases**, select a case and **Learn step by step**. Read its summary, then select **Begin this case**.
2. Select the anatomy. Grip a mechanism choice, move it to the answer dock and release when prompted. Check the explanation after correct and incorrect answers.
3. Follow the tool name and live instruction. Contact and route steps require grip and movement; active operations require trigger. Check working ends, target direction and numbered routes at normal and maximum text size.
4. Open **Case and controls** and return. The same step must remain. Try **Show me this step** and confirm the demonstration does not award credit.
5. Open **References**, then **Read source details**. Return without leaving VR. Separately open the website, close the browser and return to Anatome. The same step must remain, with no held trigger/grip replay. If Horizon leaves its menu open, select Anatome and Resume.

Reports are in `qa/case-clarity`. No connected Quest or authenticated learner session has been tested for this version. Physical browser return, comfort, legibility, input timing and clinical teaching usability still require the school headset test.
