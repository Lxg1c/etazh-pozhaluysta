---
name: unity-2d-art-workflow
description: Create and verify layered Aseprite sprites, sprite alignment, and menu animation in the Unity 2D game Etazh.
---

# Unity 2D Art Workflow

Read the project's [2D art notes](../../../docs/rules/2d-art.md) for source files and visual conventions. Use the editable Unity scene as the reference for positions and the in-game camera for visual verification.

## A character carrying a prop

1. Composite the prop and the hands with the character. Keep body, prop, and fingers on editable Aseprite layers so the palm or grip can overlap the prop in every pose.
2. Adapt the front idle and both walking directions. Keep the same frame bounds and foot pivot as the character being replaced so the sprite stays on the floor through the cycle. For the current passenger renderer, the strip has 4 front idle frames, 6 walking right, and 6 walking left. Use 24 × 24 cells, 32 pixels per unit and a pivot one pixel above bottom center when matching these sprites.
3. Import the frames with point filtering. Preview the actual sprite at game scale in the queue, in the cabin, and as it exits. For a new character variant, check the floor button portrait as well. An enlarged loop is useful for reviewing hand contact and the silhouette. The [courier and mover sources](../../../art/2d/shift-events/source) show this setup.

## Lift entrances across floors

1. Compare the live door opening, sill, and cabin artwork before copying another floor's spacing. Include the renderer's offset inside the cabin root when placing seated passengers.
2. Keep the character on the foreground aisle until they reach the opening. Draw the character in front of the jamb while passing along the aisle, then let the door cover them when they cross the sill. Use a single step inward towards their seat after lining up with the doorway.
3. Check both directions in Play Mode, including a stop that adds a person to a cabin with a rider already inside. A slowed capture makes a frame that crosses the yellow jamb or jumps sideways after the sill easy to spot.

## Fixtures repeated across floors

When the user adjusts a floor in the editor, read that live transform first. Compare the sprite centers for the whole fixture, including the wall glow and floor spill. Texture pivots can differ, so matching transform X alone can leave the illumination off center. Apply the position changes to the corresponding parts, then compare the centers after saving.

## Animation over an illustrated menu

1. Crop a fixed rectangle around the animated subject and keep the original crop as a layer in Aseprite. Match the opening frame to the original and keep the edges identical across frames so the overlay blends with the illustration.
2. Use the background's cover scale and alignment to position the overlay. Pause frame updates when the menu hides and restart the loop when it reopens.
3. When a moving part is baked into the illustration, put the moving pixels and the repaired area they expose on separate layers. For the cat's tail, check both ends of the swing for stray pixels from the face or rug and keep the root near its original position.
4. In Play Mode, check alignment at the usual and a wider aspect ratio, then start a shift and return to the menu to verify playback. See the [editable lobby cat](../../../art/2d/menu-cat/source/menu-cat.aseprite) for the implemented example.
