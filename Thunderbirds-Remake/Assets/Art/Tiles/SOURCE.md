# Tile sprites

Made by the team on 2026-10-02 with Google Gemini (image generation), then cut with a script: the frame of
each generated tile is cropped away so neighbouring cells join into one surface.

- `Wall_1` .. `Wall_4`: sandstone wall variants. Each wall cell picks one from its position (`LevelView.WallVariant`).
- `BlockCell`: the fill of one block cell, converted to near-white grey so the game can tint it teal / yellow / red.
- `Background`: dark brickwork, tiled behind the level.
- Style reference only: *Void - Fleet Pack 2 (Nairan)* by Baldur, distributed by Foozle (CC0).
