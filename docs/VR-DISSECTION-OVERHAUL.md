# Dissection retest — 1.4.0

Curriculum revision: **2026.10.2**. Target: Quest 3 school testing. Physical headset acceptance remains pending.

## What changed

- Each topic has its own prepared anatomical stations. Chest wall covers bilateral superficial/muscular regions and both fourth/fifth ribs; pleura covers both pleural sacs and five lung lobes; airways opens the trachea and main bronchi; heart begins at the pericardium and examines the heart/coronaries; vessels opens the aortic/pulmonary outflow walls; mediastinum examines the esophageal wall and regional nerves/duct/vessels.
- Every difficulty covers the same full topic. Guidance decreases and required measured removal increases with difficulty. These are educational cut sections and prepared views, not clinical incision plans or competency assessments.
- Cutting retains the actual swept tissue volume as a small movable chunk. The remainder stays anchored during guided dissection. Both meshes are complementary; the runtime no longer decomposes a small incision into movable full-sized source shells.
- Grip/lift/release earns credit only for a chunk cut during the current objective. A tiny chip, stationary release, wrong-side chunk, whole remainder, or wrong instrument cannot complete it. Left and right objectives are separately essential.
- The saw equips automatically for bone, including each fourth rib. Saw/scalpel controls appear directly above the workspace instruction. Both tools use the first surface reached by the controller ray, up to three metres.
- Collected chunks are retired once. Undo cannot restore credited tissue. Completed regional cut strokes are retained in the checkpoint and replayed when its other side is resumed. Unfinished partial work restarts at the last completed objective.
- Repeated Restore presses wait for an in-flight cut or saved opening. The regression reproduced an interrupted reconstruction incorrectly pausing the activity; the guard keeps the original reconstruction active.
- Dissection demonstrations no longer create a full-sized moving copy of the target.
- The native build script now installs the current library into ThoraxLab, preserving the separate legacy asset-demo project.

The earlier 1.3.1 cleanup only retired completed layers. It did not prevent whole-shell movement after a small incision. This revision replaces that interaction, rather than relying on cleanup alone.

## Evidence and limits

Current checks and reports are under `qa/dissection-overhaul`. The content regression initially failed all thirteen coverage assertions. The native regression initially failed ten chunk-conservation/bounds assertions and passes with volume extraction. Client/server rubric regressions include one-sided chest work, insufficient removal and missing saw evidence. XR checks use actual simulated controller input, native cutting and XRI grabbing. The compiled player separately runs all 72 complete scenarios.

Local checks do not establish physical Quest tracking, cut latency, sustained frame rate, comfort or authenticated learner persistence. Record those against the exact APK hash in BUILD-INFO.json. Do not reuse a prior kit's acceptance result.

The content revision is additive. Previous assessment history stays available; mastery and unfinished attempts from the earlier curriculum do not unlock the changed rubric. The new definitions require the matching additive Supabase migration; retain the live readback report with the kit.

## Quest retest

1. Install the new APK as an update, then verify version 1.4.0. Confirm sign-in email remains readable/editable and Back preserves it.
2. Open each dissection topic on Easy. Confirm its starting tissue and complete target list differ. Check the same target list on Medium/Hard.
3. Cut a small section and move it with grip. Look for a matching hole and a single stationary remainder. Release without lifting once: that must not earn removal credit.
4. In chest wall, finish the left region only. Confirm the right region is still required. Pause/continue and confirm the completed opening remains.
5. At each fourth rib, verify BONE SAW appears automatically. Cut from a comfortable distance, release trigger, grip the detached section and lift/release it.
6. Recheck case-choice placement, tool placement, always-visible tray, assembly and immediate button response. Record any repeatable failure with the activity, difficulty, step, active tool and a short video.

Anatomical source records and authored reconstruction labels are unchanged. Topic framing references the existing [Texas Tech thorax dissection teaching resource](https://anatomy.ttuhscep.edu/schemes/thorax_wall.html); runtime chunk thickness and prepared stations remain explicit teaching approximations.
