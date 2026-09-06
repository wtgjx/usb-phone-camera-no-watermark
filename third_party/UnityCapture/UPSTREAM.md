# Upstream provenance

Source: https://github.com/schellingb/UnityCapture

Pinned commit: `3ed54c325e0ad71afcf4f246c07e5e17b3d7f2d2`.

The filter is licensed by its upstream author under MIT. The complete license
and author notices remain at the top of `UnityCaptureFilter.cpp`. The embedded
DirectShow base classes in `streams.h` / `streams.cpp` preserve their Microsoft
copyright notices. These upstream files are not represented as original work.

Project-specific changes to the filter:

- A distinct Phone USB Camera display name and distinct 32/64-bit COM IDs;
- Replacement of upstream IPC with the project's own bounded shared-frame
  transport in `native/PhoneFrameMemory.h` (no dependency on Unity runtime);
- Black output after the sender stops, with no logos or on-video text;
- Graph-clock timestamps and resetting timing when capture restarts;
- Basic format validation and compiler-warning fixes.

The upstream `shared.inl` and `.vcxproj` are retained for provenance/reference,
but are not used by `build-native.ps1`. The build does not invoke any upstream
installation scripts or register the component.

No Unity engine, OBS component or OBS process is used by this prototype.
