r"""Exports UMBRA Pixel (the game's own bitmap font) to BMFont (text .fnt + .png) for Godot.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\export_umbra_pixel.py

Glyphs come from the UMBRA repository itself (assets/fonts/umbra_pixel.txt through game/ui/pixel_font.bitmap, which
also composes the accented letters), so every pixel matches the Python game. One texel per font pixel; the game scales
by integers (FontFile.FixedSizeScaleMode = IntegerOnly), so 11 px = 1x, 22 px = 2x, 33 px = 3x.
Metrics follow pixel_font: 11 rows, 1 px tracking, space = 3 px.
"""
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
UMBRA = HERE.parent / 'umbra-ashes-below'
os.environ.setdefault('SDL_VIDEODRIVER', 'dummy')
sys.path.insert(0, str(UMBRA))

import pygame                                      # noqa: E402
from game.ui import pixel_font as pf               # noqa: E402

ACCENTED = 'áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇñÑ'


def main():
    out = HERE / 'game' / 'assets' / 'fonts'
    out.mkdir(parents=True, exist_ok=True)
    chars = sorted({c for c in pf.glyph_table()} | {c for c in ACCENTED if pf.bitmap(c)})
    glyphs = [(c, pf.bitmap(c)) for c in chars if pf.bitmap(c)]
    pad, cols = 1, 16
    cell_w = max(len(rows[0]) for _, rows in glyphs) + pad
    cell_h = pf.ROWS + pad
    rows_n = (len(glyphs) + cols - 1) // cols
    atlas = pygame.Surface((cols * cell_w, rows_n * cell_h), pygame.SRCALPHA)
    lines = [f'info face="UMBRA Pixel" size={pf.ROWS} bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=1,1 outline=0',
             f'common lineHeight={pf.ROWS + 1} base={pf.ROWS - 2} scaleW={atlas.get_width()} scaleH={atlas.get_height()} pages=1 packed=0',
             'page id=0 file="umbra_pixel.png"', f'chars count={len(glyphs) + 1}',
             f'char id=32 x=0 y=0 width=0 height=0 xoffset=0 yoffset=0 xadvance={pf.SPACE + pf.TRACKING} page=0 chnl=15']
    for i, (char, bitmap) in enumerate(glyphs):
        gx, gy = (i % cols) * cell_w, (i // cols) * cell_h
        width = len(bitmap[0])
        for y, row in enumerate(bitmap):
            for x, value in enumerate(row):
                if value == '#': atlas.set_at((gx + x, gy + y), (255, 255, 255, 255))
        lines.append(f'char id={ord(char)} x={gx} y={gy} width={width} height={pf.ROWS} xoffset=0 yoffset=0 '
                     f'xadvance={width + pf.TRACKING} page=0 chnl=15')
    pygame.image.save(atlas, str(out / 'umbra_pixel.png'))
    (out / 'umbra_pixel.fnt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'UMBRA Pixel: {len(glyphs)} glifos -> {out}')


if __name__ == '__main__':
    main()
