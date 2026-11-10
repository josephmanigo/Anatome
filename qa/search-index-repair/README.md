# Unity review startup exception

The review launcher and XR verification entered Play immediately during Editor
startup. On Unity 6000.3.19f1 with `UserSettings/Search.index` absent and startup
indexing enabled, Unity's delayed Search initialization then ran during the Play
transition. Its default-index setup returns early in that state, while database
enumeration subsequently indexes the empty list. This produced the user's exact
`ArgumentOutOfRangeException` stack through `SearchDatabase.EnumerateAll`,
`GetDefaultSearchDatabase` and `SearchInit.IndexationOnStartup`.

The installed `UnityEditor.QuickSearchModule.dll` was inspected using the bundled
Mono.Cecil library to confirm its Play-transition guard. The corresponding
[Unity 6000.3 SearchDatabase source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Modules/QuickSearch/Editor/Indexing/SearchDatabase.cs#L390)
and [startup callback](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Modules/QuickSearch/Editor/SearchInit.cs#L99)
explain the observed sequence.

## Fix

`LearningEditorPlayMode.Request()` defers automatic Play through two Editor
callback dispatches. Unity's initial callback batch therefore runs while the
Editor is still in Edit mode. The helper waits for compilation/import activity,
deduplicates requests, preserves a request across assembly reloads, and supports
cancellation when a verification times out. Both `OpenVRReview` and the XR
verification use this helper.

Search was left enabled. No Library/cache purge, Unity installation patch,
anatomy asset edit or runtime/player change was used to repair this exception.
The existing Editor recovery snapshot is retained under `before/scene-recovery`.

The verification error ledger now begins before the Play request, survives the
Play domain reload and is merged into the final pass/fail result. Earlier
runtime-only logging could miss Editor startup exceptions.

## Reproduction and verification

Run outside the Editor, with graphics and without `-quit`:

```powershell
pipeline/run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.LearningVRSimulatorVerification.Run -Target Win64 -Log search-index-check -Graphics -NoQuit
```

| Run | Initial default index | Result |
|---|---|---|
| Original launcher, strengthened error capture | Missing | 61 gameplay checks completed, but the overall report correctly failed on the exact startup exception. |
| Deferred launcher, first cold start | Missing | Unity created the index before Play; 61 checks passed with an empty error list. |
| Final patch, subsequent start | Present | 61 checks passed with an empty error list. |

Evidence:

- `before/original-editor.log`: user's original review startup stack.
- `before/reproduced-xr-report.json` and `../search-index-red.log`: failing reproduction.
- `cold-start-passed.json` and `../search-index-green.log`: missing-to-created index transition and passing run.
- `final-xr-passed.json` and `../search-index-final.log`: final verified code.
- `../search-index-user-review.log`: reopened interactive review.

This regression concerns Editor startup ordering. Player performance, clinical
content, and physical headset acceptance are separate from this result.
