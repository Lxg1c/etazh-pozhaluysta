# Unity desktop build and release checks

## Build from one source revision

Use the pinned Unity editor version from `ProjectSettings/ProjectVersion.txt`. If the live workspace is open in Unity, build from a separate snapshot of the intended Git commit. Select the same game scene and version for both platforms. For Etazh the entry scene is `Assets/_Project/Prototype/Scenes/ElevatorGame.unity`.

On Windows, add `Mac Build Support (Mono)` for that exact editor version. Use the Mono backend for the macOS cross-build. A Universal macOS build includes x86_64 and arm64. Consult the [Unity macOS guide](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/apple-mac/macos-delivery/macos-building) for requirements that vary by editor version.

## Package and check the player

- Windows ZIP contains the executable, its complete data folder, and the Unity/Mono runtime files. Launch the built player and take a route that demonstrates the start menu, input, a delivery, and a score change.
- macOS ZIP has the `.app` at its root. For a cross-build prepared on Windows, write Unix file metadata in the ZIP and retain execute permissions on `Contents/MacOS/<CFBundleExecutable>`. Read the executable name from `Info.plist`. For a Universal package, inspect the Mach-O header for both x86_64 and arm64, then read the execute bits back from the ZIP. Record runtime launch on a Mac separately from these archive checks.
- Keep build logs, local provenance, debug-only output, and Unity Pipeline editor-bridge assemblies outside the distribution archive. Compare the version and target with the build report before upload.

## Publish the verified archives

Use a GitHub release draft to upload both ZIPs before publishing. Match the tag to the built revision, check asset state, size, and SHA-256 against local files, then verify both download links as an unauthenticated viewer for a public release. Describe the tested platforms and macOS signing or runtime status in the release notes.
