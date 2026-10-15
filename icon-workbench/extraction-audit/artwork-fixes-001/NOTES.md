# Extraction audit: artwork fixes 001

Source: `revisions/artwork-fixes-001/corrections-20261015-131723-001.png`

Parameters: 6 rows, 6 columns, 164px square source crops, 4px trimmed from
each side, then rescaled to 64×64. The output contains all 36 grid cells,
including the five unused blank cells.

The splitter estimated the same center as the equal-cell grid for all 36 cells.
Every mask balance score was zero. The debug mask shows that the framed blue
interiors were not useful as connected outer-edge background samples. Automatic
centering therefore did not provide cell-specific corrections for this sheet.
The centers stayed at the nominal equal-cell positions; inspect the crop-box
image and use manual offsets for any needed adjustment.

The previous crops used source bounds 170–172px per side, with variation from
integer grid division. The new source crops are consistently 164×164px. After
trimming, they are 156×156px before resize. Both sets produce 64×64 outputs.
`comparison.json` lists the bounds per cell. `_debug_crop_boxes.png` shows crop
and trim bounds. `_debug_output_contact.png` shows the extracted outputs.
