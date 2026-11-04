---
name: computer-control
description: "Safe, accurate use of BURAK BIM computer control (mouse/keyboard) when no API reaches a command - dialogs, Bluebeam/PlanSwift canvases, license screens, third-party add-ins."
metadata:
  discipline: "All"
  product: "BURAK BIM 1.3"
---
# Computer control - only with the user's permission

Use computer control only after every API route has failed (see bim-base §6).

1. **Explain first** in the chat: what you will click and type, and why no API can do it.
2. `computer_request_control reason="..." minutes=10`. The user clicks **Yes** on their screen. If they decline, stop and give manual steps.
3. `computer_screenshot` (grid on), or `capture app=<app> grid=true`. Read **screen** coordinates from the grid labels or from the caption mapping. Never estimate positions from a downscaled image without the mapping.
4. Act one step at a time:
   - `computer_focus app=<app>`
   - `computer_click x y`
   - `computer_type text="..." clear=true`
   - `computer_key keys="enter"`
   - `computer_drag from=[..] to=[..]`

   Every action returns a new screenshot. **Check it before the next step.** If something unexpected appears (an error, the wrong dialog, a prompt about licences, payments or deleting files), stop and ask the user.
5. Prefer keyboard shortcuts and typed values over pixel-precise dragging.
6. `computer_release_control` as soon as you are done. The user can always stop control with Ctrl+Alt+Q.

**Never:**
- type passwords;
- accept licence agreements or purchases;
- change system settings;
- delete files without an explicit OK.
