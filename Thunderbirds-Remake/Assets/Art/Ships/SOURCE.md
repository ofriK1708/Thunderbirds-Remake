# Ship sprites

Made by the team on 2026-10-02 with Google Gemini (image generation), then cut into frames and given a
transparent background with a script.

- Style reference only: *Void - Fleet Pack 2 (Nairan)* by Baldur, distributed by Foozle (CC0). No sprite from
  that pack is included here.
- `*_Side` is the side profile facing right, `*_Turn` the three-quarter view, `*_Front` the front view: hull only.
- `*Flame` is the matching thruster-flame layer (same width as its hull frame, cut from the flame tops down), animated in code.
- `*_Portrait` is the complete side view, used as the still picture in the HUD.
  Facing left is the mirror image, done in code (`ShipView`).
- The raw generated sheets are in `docs/generated_images/`.
