# Third-party notices

## Independent camera (v3 local preview)

The v3 native camera component adapts the MIT-licensed
[UnityCapture filter](https://github.com/schellingb/UnityCapture), pinned to
`3ed54c325e0ad71afcf4f246c07e5e17b3d7f2d2`.
The full filter license and author notices are retained in
`third_party/UnityCapture/UnityCaptureFilter.cpp`; embedded Microsoft
DirectShow base-class copyright notices are preserved. See
`third_party/UnityCapture/UPSTREAM.md` for local modifications. This component
does not require Unity or OBS and is not included in the existing v2.1.1 release.
The v3 local program folder includes these notice-bearing sources under
`third_party/UnityCapture/`. No new public stable release has been published.

## scrcpy

This project downloads and redistributes the official Windows build of
[Genymobile/scrcpy](https://github.com/Genymobile/scrcpy), version 4.1.

scrcpy is licensed under the Apache License 2.0. Its original `LICENSE.txt`
is preserved inside the `scrcpy` directory of packaged releases.

The expected SHA-256 of the official `scrcpy-win64-v4.1.zip` archive is:

```text
5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db
```

## Inno Setup Simplified Chinese translation

The Windows installer uses `ChineseSimplified.isl` from
`kira-96/Inno-Setup-Chinese-Simplified-Translation`, pinned to commit
`1ff90acc4ed4aee82b1cda43253243deee3daed4`. The translation is MIT licensed;
its license is retained at `installer/ChineseSimplified.LICENSE` and is included
with the installed license files.
