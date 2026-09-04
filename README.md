# Phone USB Camera

把 Android 手机的高清摄像头通过 USB 变成 Windows 虚拟摄像头，不经过 DroidCam 视频链路，不添加水印。

```text
Android Camera2 → USB/ADB → scrcpy Camera Mode → OBS Virtual Camera → OpenScreen
```

所有视频数据只经过 USB 和本机，程序不上传摄像头画面。

## 功能

- 通过 USB/ADB 读取 Android Camera2 镜头；
- 扫描前摄、后摄和手机开放的视频尺寸；
- 提供 720p30、1080p30、2K30 和 4K30 档位；
- 高画质启动失败时可自动回退到 1080p30 H.264；
- 自动创建独立的 `PhoneUsbCamera` OBS 场景集；
- 确认 OBS Virtual Camera 真正启动后才打开 OpenScreen；
- 检测摄像头被抢占、USB 断连和 OBS 启动失败，避免误报成功。

## 系统要求

- Windows 10/11 64-bit；
- Android 12 或更高版本；
- 支持数据传输的 USB 线，手机已开启 USB 调试；
- [OBS Studio](https://obsproject.com/) 及 OBS Virtual Camera；
- [OpenScreen](https://github.com/getopenscreen/openscreen)。

## 直接使用

1. 从 GitHub Releases 下载 `phone-usb-camera-v2.0.0-win64.zip` 并完整解压；
2. 手机开启 USB 调试，连接电脑后在手机上允许调试授权；
3. 确认手机相机、视频通话和直播应用已关闭；
4. 双击 `无水印手机USB摄像头.exe`；
5. 点击「扫描手机镜头」，选择镜头与画质；
6. 点击「启动无水印虚拟摄像头」；
7. 在 OpenScreen 的摄像头列表选择 `OBS Virtual Camera`。

> `Phone USB Camera` scrcpy 预览窗口可以被其他窗口遮住，但不要最小化，否则 OBS 窗口捕获可能停止更新。

使用完可点击「停止本次会话」。正常启动完成后直接关闭控制程序，也会停止由该实例启动的 scrcpy 和 OBS；OpenScreen 保持打开。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本会：

1. 使用 Windows/.NET Framework 自带的 C# 编译器；
2. 在本地缺少运行库时下载官方 scrcpy 4.1 Windows 64-bit 压缩包；
3. 校验 scrcpy 压缩包的 SHA-256；
4. 在 `dist` 目录生成可执行程序和完整运行文件。

## 已验证环境

- Windows 11 25H2；
- Android 16，Xiaomi 24031PN0DC；
- 后摄 Camera ID 0；
- `3840×2160 @ 30fps`；
- scrcpy 4.1；
- OBS Studio 32.2.2；
- OpenScreen 1.10.0。

手机宣传的拍照像素不等于可用的虚拟摄像头分辨率。本工具使用的是 Camera2 实际开放的视频尺寸。

## 本机改动范围

- 不会卸载 DroidCam，不会清除手机应用数据；
- 启动时会临时结束手机的默认相机和 DroidCam 后台，释放镜头；
- 不会修改 OpenScreen 文件；
- 只会创建或更新名为 `PhoneUsbCamera` 的专用 OBS 场景集与配置；
- OBS 上次未正常关闭时，会将 `.sentinel` 异常标记移到 `%LOCALAPPDATA%\PhoneUsbCamera\obs-sentinel-backups` 保留，防止安全模式提示阻断自动启动。

## 许可证

本项目源码使用 [MIT License](LICENSE)。

scrcpy 使用 Apache License 2.0，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)以及发行包中的 `scrcpy/LICENSE.txt`。
