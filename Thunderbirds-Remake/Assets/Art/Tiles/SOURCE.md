# Tile sprites

Made by the team on 2026-10-02 with Google Gemini (image generation), then cut with a script: the frame of
each generated tile is cropped away so neighbouring cells join into one surface.

- `Wall_1` .. `Wall_4`: sandstone wall variants. Each wall cell picks one from its position (`LevelView.WallVariant`).
- `BlockCell`: the stone texture of one block cell, converted to light grey. The game tints it with the stone colour and draws the colour class on the edge.
- `Background`: dark brickwork, tiled behind the level.
- Style reference only: *Void - Fleet Pack 2 (Nairan)* by Baldur, distributed by Foozle (CC0).
