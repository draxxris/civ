# Data review notes

The generated `data-issues.json` contains 38 record-level diagnostics.

## Stale optional references

Four references have no matching record in the effective profile:

- Charles Martel's favorite civic names Nationhood.
- Genseric and Thrasamund name Vassalage as their favorite civic.
- Venezia names a nonexistent Roman civilization as its derivative civilization.

The importer emits null, as the native missing-info lookup does. It does not guess a
replacement. Required unresolved references fail import instead.

## Missing boolean define

`DONT_ESCORT_SUBDUED_LAND_ANIMAL` is absent from the imported defines. It appears in the
subdue outcomes of 34 units. The native boolean getter uses a define lookup for a
symbolic value and returns false for a missing define. The schema retains the policy key
and emits the resolved false value. Military rules must review this policy before they
implement captured-unit delivery.

## Native identities absent from XML

The documentary unit-AI table has 41 records. The native enum and registration contain
four additional roles:

- Diplomat.
- Hunter.
- Pillage counter.
- Subdued animal.

The military mod therefore contains 45 unit-AI definitions. These four added identities
are not additional XML records.

## Initialization and localization gaps

There are eight civic options. Of the 60 civilizations, 38 declare six initial civics,
and 22 declare none. The typed data retains explicit declarations without inventing
registration-dependent enum-zero selections. Government rules must define their
initialization policy for missing categories.

The generic source inventory has 171 unresolved English keys. The detailed localization
report identifies their uses. Typed text uses readable key words when no English entry
exists.

## Deferred mechanics

Religion now emits 13 playable definitions instead of deferred Foundation identities.
Foundation still reserves 36 corporation and ten victory identities. Their effects
remain unsupported. Their prerequisite evaluators fail closed until future rule mods
implement these mechanics.

The era filter removes every reference dependent, including text links and future
upgrades. It can remove an early building that refers to later replacement content. The
dropped-content report records each removal.
