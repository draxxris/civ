# Grid extraction helper

`split_icons_base.py` is the complete splitter source checked at
`/home/gordon/workspace/felix/tools/split_icons.py`. The vendored copy is
byte-for-byte identical. It preserves the original centering, background mask,
edge trimming, and square rescaling implementation for this workbench.

`extract_icons.py` exposes `extract_sheet(...)` and writes square 64×64 PNGs,
a `geometry.json` record, and two debug images. The output folder is not cleared
before extraction. The crop-box image shows the source crop in white and the
post-trim region in orange. Center crosses use magenta.

## Python API

Run the example from `icon-workbench/`:

```python
from extraction.extract_icons import extract_sheet

extract_sheet(
    "revisions/artwork-fixes-001/corrections-20261015-131723-001.png",
    "extraction-audit/artwork-fixes-001",
    rows=6,
    cols=6,
    crop_size=164,
    cut_edges=4,
    manual_offsets={(0, 2): (1, -1)},
)
```

Rows and columns in `manual_offsets` are zero-based. Positive `dx` moves right;
positive `dy` moves up, matching the source splitter. Set `search_radius=0`
to skip automatic mask optimization and start from equal-cell centers. A
nonzero value uses the vendored outer-edge mask search.

## CLI

```sh
python icon-workbench/extraction/extract_icons.py \
  icon-workbench/revisions/artwork-fixes-001/corrections-20261015-131723-001.png \
  icon-workbench/extraction-audit/artwork-fixes-001 \
  --rows 6 --cols 6 --crop-size 164 --cut-edges 4
```

Optional offsets come from JSON, keyed by zero-based `row,col`:

```json
{"0,2": [1, -1]}
```

## Centering limits

The source splitter estimates background color at the outer image edge and
flood-fills connected pixels from there. Silver cell frames can enclose their
blue backgrounds. Those enclosed areas may not enter the mask, which can make
the automatic balance score uninformative or misleading. The mask and crop-box
images are evidence to inspect, not proof of correct centering. Review output
geometry, use manual offsets where needed, or set `search_radius=0` to use the
equal-cell centers. This helper does not claim automatic cell detection is
perfect.
