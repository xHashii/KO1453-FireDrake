# KO tooling references and 1453 development direction

Reviewed 25 October 2026. Index:
https://github.com/xmkg/kol-projects-index
(commit 883b30b51aa144cde42990156d9e2a1af85cf1f6).

The index describes the author's 2012–2019 work and explicitly says these tools
are no longer maintained. It is a tool catalogue, not a complete game server.
We have inspected documentation and selected source, not built/run the tools.

## Useful candidates

| Project | Potential use here | Gate before adoption |
|---|---|---|
| [SMD generator](https://github.com/mustafakemalgilor/kol-smd-generator) | GTD height layout, OPD collision parsing, tile/object-event generation | Highest-priority map-format reference; inspect output format and preserve authored warp/event data. Not a client stair-repair promise. |
| [PENTAGRAM table editor](https://github.com/mustafakemalgilor/ko-table-editor) | Typed table serialization and inspect/export tooling | Match the actual 1453 schema and encryption; preserve original files. |
| [Skill importer](https://github.com/mustafakemalgilor/kol-skill-table-importer) | Compare client skill definitions with source MAGIC families | Audit only first; do not import higher-era skills or overwrite live SQL. |
| [TBL shell](https://github.com/mustafakemalgilor/kol-tbl-shell) | Offline SQLite inventory/diff of client tables | Its MSSQL migration path is not directly our MariaDB pipeline. |
| [Item converter](https://github.com/mustafakemalgilor/ko-item-tbl-to-sql) | Client/server item-catalog comparisons | Inspect implementation/version/license before reuse; not yet source-reviewed. |
| [Upgrade importer](https://github.com/mustafakemalgilor/kol-upgrade-table-importer) | Future upgrade-content auditing | Not implemented gameplay; not yet source-reviewed. |
| [Packet logger](https://github.com/mustafakemalgilor/ko-packet-logger) | Understand native packet capture techniques | README describes trampoline hooks. Prefer existing server diagnostics; no injection by default, no credential-bearing raw logs. |

Executable editors/launcher/admin tools are lower priority: they do not fill the
missing gameplay/effect/party infrastructure. Do not run old binaries or overwrite
our admin/authentication/item safety code based on a catalogue description.

## SMD generator source findings

Pinned 804fe4c352491052a40e22dd1e199e14142d9d1b. C++ exporter (not C#).
README and reviewed C++ headers state MIT/SPDX MIT. Other linked tool READMEs
also state MIT; GitHub's license API returns null for several. Preserve notices
and inspect actual files before copying; none were vendored into product code.

- GameTerrain.cpp reads a name/header, grid dimension, then float height plus
  four skipped bytes per GTD sample. It has old/new header paths.
- ObjectPostData.cpp reads a name/header before collision/shape data.
- GameserverMap.cpp generates movement tiles and object events.
- Its SaveGameserverMapData writes a custom header before terrain/collision.
  Do not assume that output is the legacy SMD our reader expects or simply rename
  it into production. Authored warps/regeneration need explicit retention.
- Some source paths are unsafe as-is: ObjectPostData's old-mode path reads from
  a null FILE pointer before fopen; reads/count allocations are not comprehensively
  bounded. SMDFile.cpp height interpolation also contains suspicious unparenthesized
  index expressions. These are reasons to port verified concepts with tests,
  not import the tool wholesale.

Our small accompanying 1453 runtime hardening rejects non-finite terrain samples
at map load, including unused samples, with synthetic acceptance/refusal tests.
This is validation only, NOT collision implementation or a stair fix.

## Product direction and cleanup

1453 is the only active wire target again. Experimental protocol switches,
handshake helpers/tests, inspection uploads and experiment documents were removed
from the current branch tree. Git history/LFS objects are not purged; that would
require a separately approved history rewrite. Working runtime data/map and quests
remain unchanged. Local deployed INIs and live SQL cannot be changed by this patch:
remove obsolete PROTOCOL_VERSION settings, set LoginServer ADVERTISE_VERSION=1453,
and restore any temporary live map reference separately if still active.

Keep the chosen client-side Moradon visual assets on the owner's test copy. Their
stair issue remains open; visual loading is not full collision certification.
Do not reintroduce another executable/protocol migration to investigate it.

Continue the 1453 roadmap: selected combat/effects foundations, then friendly
healing with authoritative recipient checks/atomic persistence and two-client
acceptance, then party membership/rewards. Existing self-healing, Sprint,
warehouse/serial/CAS protections and intro suppression remain intact. No new
party/friendly-healing implementation is claimed in this cleanup increment.
