Run `mise run test` by default. It excludes E2E and extractor tests. Use
`mise run test:e2e` to run E2E tests and `mise run test:extractor` to run extractor
tests.

`mise run test` also excludes world generation tests (`Category=WorldGen`). Run
`mise run test:worldgen` when the work set touches world generation: `src/Mods/World/`,
map generator registrations, `MapSpec`/`HexGrid` layout, terrain or relief generation,
river and cliff generation. `mise run test:nobuild` reruns the default tests on the
current build.

Always prefer background mode for sub-agents.

Product rule: never write "AND", "A New Dawn", "Rise of Mankind" or "RoM" in shipped
code, ids, text or docs.
