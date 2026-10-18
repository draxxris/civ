# Artwork reviewer

This directory contains the review server, browser UI, crop script, tests, and fresh review decisions.
Run commands from the repository root.

## Prepare crops

```sh
python3 icon-workbench/reviewer/prepare_review.py
```

The script discovers completed 8×8 batches under `icon-workbench/batches/`.
It crops each editable square with a 1-pixel inset on every side, then resizes the 88×88 result to 64×64.
This removes edge bleed. It excludes gutters and overwrites review crops, but not source sheets or decisions.

## Start the server

```sh
python3 icon-workbench/reviewer/review_server.py --port 40559
```

Open `http://127.0.0.1:40559/`.
The server listens on all interfaces.
Use a trusted network because reachable clients can submit decisions.

The page compares originals with fresh candidates.
It supports acceptances, rejection comments, filtering, and hidden approved icons.
Every rejection requires a comment.
Each submission requires a decision for every mapped icon in that batch.
Submissions retain decisions for other batches.

The server saves decisions to `reviewer/review-selections-8x8.json`.

The server shows base artwork, not the separate badge composites.
No command installs production assets.

## Offline checks

```sh
python3 icon-workbench/reviewer/test_review_server.py
node --check icon-workbench/reviewer/review.js
```

Tests cover generation arguments, partial batches, exact crops, HTTP review, saved decisions, and invalid inputs.
They make no paid requests.
