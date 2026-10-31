## What and why

<!-- What this changes for players, and the issue it belongs to (Fixes #N). -->

## Related pull requests

<!-- The SDK pull request this one needs, or the one that needs it. Leave
     config/release-toolchain.json as it is: the maintainer moves the SDK pin. -->

## Tested

<!-- See CONTRIBUTING.md → Testing. Test the exact branch of this pull request. -->

- PC (CPU, GPU, driver):
- Settings and window size or display:
- [ ] Unit tests for what I touched:
- [ ] Played the change (screenshots before / after for graphics, frame rates for performance)
- [ ] Renderer / SDK change: a run with `--vulkan_validation_enabled=true` logs no `Vulkan Error`

## Checklist

- [ ] Targets the next `release/<version>` branch if one exists, `main` otherwise
- [ ] Off by default if it changes what players see, unless agreed in the issue
- [ ] Guides and README updated for player-visible changes; debug options in `DEBUG_OPTIONS.md`
- [ ] New source files carry the copyright header; third-party code agreed and in `THIRD_PARTY_NOTICES.md`
