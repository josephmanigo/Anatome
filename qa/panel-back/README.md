# VR panel Back control

ANATOME 1.6.4 passed all 69 focused XR Device Simulator checks in Unity 6000.3.19f1 on 21 November 2026. The run included 33 Back-specific checks, recorded zero errors and used the normal tracking gate. [Simulator report](../release-1.6.4/panel-xr.json)

| Control | Text setting | Ray target in canvas units | Width of 700-unit panel |
| --- | --- | --- | --- |
| Page or keyboard Back | 100% | 115 × 58 | 16.4% |
| Page or keyboard Back | 150%, high contrast | 157 × 72 | 22.4% |
| Guide Close | 150%, high contrast | 168 × 72 | 24.0% |
| Confirmation Cancel | 150%, high contrast | 189 × 72 | 27.0% |

The full button face accepts a ray hit, including the space around the arrow and text. At the shipped world-space scale, the Back target measures approximately 98 × 49 mm at 100% and 133 × 61 mm at 150%. Gripping Back leaves the panel in place. A controller trigger press navigates once, even if held over the next page.

- **Reading and navigation.** Back stayed fixed while the introductory page scrolled to its last line and returned to sign-in. On the activity chooser, two separate presses returned from the chest-wall unit to its unit list and then to the activity list. [Normal view](page-normal.png) · [150% high contrast](page-large-high-contrast.png)
- **Keyboard.** Scrolling the keys left Back, the field heading and the entered email visible. Both Back and the controller Menu button dismissed the keyboard while preserving the draft. [Before](../release-1.6.4/panel-keyboard-before.png) · [Normal view](keyboard-normal.png) · [150% high contrast](keyboard-large-high-contrast.png)
- **Overlays.** Closing the guide restored the same activity-list scroll position. Cancel dismissed a confirmation without accepting its action. Separate controller Menu presses opened and closed the pause menu.

The four captures show the running Unity world-space canvas. Physical Quest controller aiming and hand-pinch comfort still require an on-headset check.
