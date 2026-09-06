# Independent camera — v3 local preview and verification record

Updated: 2026-09-06. The independent component has been registered for the
current Windows user with explicit approval, and the white single-window UI
now uses this backend. Local `dist/无水印手机USB摄像头.exe` has been updated to
v3; the previous dist is preserved in `release-dist/v2.1.1-before-native-20260906`.
The public v2.1.1 release still uses OBS. No new stable release is claimed.

## Why the old v2.1.1 EXE needs OBS

The v2.1.1 EXE is a controller for scrcpy, an integrated preview and an OBS
virtual-camera session. Integrating the two visible windows did not implement
an independent camera device. Native OpenScreen expects a Windows camera
source; an Android USB/ADB stream is not itself such a camera.

## New pipeline

```text
Android Camera2 / H.264
    → USB / ADB (scrcpy 4.1 server only, pinned protocol)
    → Windows built-in H.264 decoder inside our process
    → per-user/session shared frames
    → Phone USB Camera DirectShow component
    → OpenScreen
```

The camera filter is adapted from the MIT-licensed UnityCapture filter, with
separate COM identities and a new IPC implementation. It does not require
Unity. The decoder uses Windows Media Foundation; the camera **output** uses
DirectShow, not the Windows 11 MFCreateVirtualCamera API. The source is aimed
at 64-bit desktop consumers, including OpenScreen; support for every Windows
Camera/UWP application is not claimed.

There is no OBS process, OBS driver dependency, window capture, scrcpy desktop
window, external FFmpeg process or upload. ADB remains a background USB
transport, and a temporary scrcpy server runs on Android. The program cannot
turn an arbitrary Android phone into a physical UVC USB device by renaming it.

## Verification completed

- C++ and C# builds succeed with Visual Studio Build Tools / Windows SDK.
- Unit checks: reference pixel colors, bottom-up row order, padded strides,
  non-SIMD tails, alpha channel, output bounds and invalid frame-size rejection.
- Xiaomi 24031PN0DC / Android 16, USB, rear camera 0.
- Final 1080p producer run: 438 decoded frames in 15.01 seconds, 438 changing
  frame fingerprints; steady-state around 30 fps. Mean decode/conversion/
  publishing call time: 14.50 ms.
- DirectShow consumer run: 347 frames in 12 seconds, 308 changing fingerprints,
  no non-increasing timestamps. Initial frames before phone startup are black.
- The consumer loaded our DLL directly into an isolated test graph. **No COM
  registration was needed for this test. This is not an OpenScreen recording
  test.** Tests saved only text statistics, not camera frames or videos.
- The test now checks changing frames in every measured second after warmup,
  not merely total frames or non-black output. A camera that freezes halfway
  through must fail even if it produced changing frames earlier.
- The actual per-user COM registration was subsequently tested with
  `FilterProbe.exe --registered 8 1920 1080`: 235 frames, 235 changing
  fingerprints, zero non-increasing timestamps. No OBS or scrcpy desktop
  process was running. This used the integrated GUI producer.
- Observed the OpenScreen recording UI selecting `Phone USB Camera` and
  showing a real phone preview. On 2026-09-06 the user took over the recording
  test and confirmed that recorded phone-camera content plays in OpenScreen.
  This is user-reported runtime acceptance, not an automated file/frame audit.
- C++/C# and UI layout checks pass. The newly added two-cycle integrated
  hardware regression stopped at the producer ownership guard because the
  user already had a live session; it did not interrupt that session. Its
  start/stop and transform assertions have not yet run to completion.

Text reports are generated under ignored `test-output/`:
`native-1080p-producer.txt` and `native-1080p-consumer.txt`.

## Installation and remaining validation

1. Registration and UI wiring are complete. `install-camera.ps1` installs
   only three owned HKCU 64-bit keys and preserves component files on
   unregistration. The release folder includes the script and license sources.
2. Rotation, mirror, preview and graceful asynchronous stop are implemented.
   The source no longer uses DWM/window capture for the v3 UI. Historical v2
   helper classes remain, but their OBS command-line entry points are disabled.
3. OpenScreen preview is observed and recording playback is user-confirmed.
   Extended minimize/restore, unplug/reconnect and repeated start/stop
   acceptance testing remains outstanding; do not conflate it with the
   registered consumer's continuous-frame test.
4. Optimize 4K decoding: initial scalar conversion ran 12–14 fps; SIMD and
   parallel conversion improved a subsequent run into the 20s, but stable
   4K30 has **not** passed. Most remaining measured time was in Windows software
   decoding. Do not advertise 4K30 on the basis of a 4K frame-size negotiation.
5. Address arbitrary camera color metadata (the prototype assumes BT.709
   limited range), camera switching and long-running latency/backpressure.
6. Repeat the opt-in hardware regression and long-running tests before
   publishing a new stable release. The local package includes the decoder,
   camera filter, registration script and third-party copyright notices.

## Build and test without installation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-native.ps1
```

This compiles the camera and decoder DLLs and the test executables, then runs
only synthetic unit tests. It does not touch phone hardware or register a camera.

For a short explicitly requested hardware test, first stop existing phone
camera sessions, then run the following in two terminals at roughly the same
time (replace the DLL argument with its actual absolute path):

```powershell
.\native-dist\DirectUsbProbe.exe .\dist\scrcpy 15 1920 1080 0
.\native-dist\FilterProbe.exe "C:\absolute\project\native-dist\PhoneUsbCameraFilter.dll" 12 1920 1080
```

Only one producer is allowed. The producer closes its own ADB shell and removes
only its own temporary forward and server file. It never force-stops another
phone camera app, never kills OBS and never modifies OBS scenes.

Primary references:

- [scrcpy v4.1 protocol and standalone server](https://github.com/Genymobile/scrcpy/blob/v4.1/doc/develop.md)
- [UnityCapture source and license](https://github.com/schellingb/UnityCapture/tree/3ed54c325e0ad71afcf4f246c07e5e17b3d7f2d2)
- [Windows H.264 decoder](https://learn.microsoft.com/en-us/windows/win32/medfound/h-264-video-decoder)
