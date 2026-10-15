# Spawn and extraction fix verification

This repository is a code extract. The fixes were reviewed against the full
Unity 6000.3.12f1 source project. **12 focused tests passed**, including two tests
that entered real Play Mode with isolated test objects for bounded spawn
retry/fallback/wave completion/failure and player death during extraction.

Hidden searches no longer return invalid fallback points. General fallback also
respects minimum distance, including NavMesh-adjusted positions. After the finite
hidden retry budget, a valid visible point may be used. If every point is invalid,
`WaveFailed` stops the wave and fails the run rather than faking a clear or waiting
for unspawned enemies. The cursor scan visits all candidates.

Extraction succeeds only after the real sequence completes with valid required
references and a surviving player. Missing configuration, false results, or ordinary
exceptions produce one Failed outcome. Lifecycle cancellation cannot replace an
existing terminal result. Rotor/audio state is cleaned up even on early return.

A separate audit of the full project's GameScene **failed to load cleanly because
external prefabs were absent**, including the rescue helicopter. Serialized
helicopter/landing/departure references existed, but the helicopter prefab was a
missing-asset placeholder. Restore the original asset packs/GUIDs and verify the
authored scene, camera/NavMesh, normal cinematics, reference removals, and cancellation
at each stage before release. The passing isolated tests do not claim a complete
playthrough of that scene. No full Unity build of this extract is claimed.
