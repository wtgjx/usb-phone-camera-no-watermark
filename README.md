# U镜 · UCam

把 Android 手机通过 USB 变成无水印 Windows 摄像头。中文名称为 U镜，英文为 UCam；无需安装或启动 OBS。

应用已更名，Windows 摄像头设备名暂保留 **Phone USB Camera**，以兼容现有 OpenScreen 设置。设备标识、注册和视频传输协议均未改变。

> 当前版本为 v3.0.1 Windows 安装版。旧 v2.1.1 发行包仍使用 OBS，不要混用。验证范围见 [技术与验证记录](NATIVE_CAMERA_PROTOTYPE.md)。

## 普通用户一键安装

[![下载 U镜 Windows 安装版](https://img.shields.io/badge/Windows-下载_U镜安装版-ff643d?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/wtgjx/usb-phone-camera-no-watermark/releases/latest/download/UCam-Setup-win64.exe)

点击上面的按钮下载 `UCam-Setup-win64.exe`，双击即可安装。无需下载源码，也不需要使用 PowerShell。安装程序会：

- 将 U镜及运行组件安装到当前用户的应用目录；
- 自动注册 `Phone USB Camera`，无需手动运行 PowerShell；
- 在桌面和开始菜单创建“U镜”入口；
- 提供标准卸载，并且不修改 OBS 或其他摄像头。

安装完成后，连接 Android 手机、开启 USB 调试并在手机上允许这台电脑，然后打开桌面的“U镜”。首次公开下载的未签名安装包可能触发 Windows SmartScreen 提示；正式公开发行应增加可信代码签名。

## 使用

1. 用支持数据传输的 USB 线连接 Android 手机，开启 USB 调试并在手机上允许授权。
2. 安装版会自动注册摄像头；仅便携开发包需要运行 `install-camera.ps1 -Mode Install`。
3. 打开桌面的“U镜”，选择镜头，建议先用 **1080p**，点击「启动摄像头」。
4. 在 OpenScreen 开启摄像头，选择 **Phone USB Camera**，再正常录制。
5. 使用结束点击「停止输出」或关闭本程序。OpenScreen 不会被关闭。

本次开发使用的电脑已经完成注册。`dist` 是便携开发输出，不能只复制 EXE；面向普通用户请发布完整 Windows 安装包。

如果 OpenScreen 已经打开但列表没有新摄像头，保存当前项目后重新打开 OpenScreen。不要选择旧的 `OBS Virtual Camera`。

## 界面与画质

- 浅色单窗口，参考 OpenScreen 的紧凑顶栏、大预览和右侧设置面板，使用维护者提供的橙色品牌图标。
- 预览下方为画面控制；运行日志在左侧展开，不挤压右侧启停操作。默认和最小窗口布局均检查无侧栏滚动。
- 程序、窗口标题、任务栏图标与 EXE 图标统一使用 U镜 / UCam 品牌。完整设计说明见 [UI_DESIGN.md](UI_DESIGN.md)。
- 左转/右转 90°、水平镜像会作用于输出；「适应/填满」只改变本程序的预览布局。
- 预览不再依赖另一个 scrcpy 窗口。虚拟摄像头直接接收视频帧，最小化不会主动停止视频接收。
- 默认 1080p30；720p 可降低负载。
- **2K/4K 是实验档，帧率未保证。** 当前软件解码没有通过稳定 4K30 验证。手机的拍照像素也不等于 Camera2 开放的视频分辨率。
- 切换镜头或画质前请停止本次会话。开始录制后建议保持分辨率和方向不变。

## 工作方式

```text
Android Camera2 / H.264 → USB / ADB → Windows 内置解码器
    → 独立 Phone USB Camera 组件 → OpenScreen
```

视频只在手机和本机处理，不上传。手机运行临时 scrcpy 4.1 服务端；电脑在本程序内解码，不运行 OBS 或 scrcpy 桌面预览窗口。扫描镜头时会短暂调用 scrcpy 的命令行查询功能。

这是一台软件虚拟摄像头，不会把手机固件改成实体 UVC 摄像头。目前面向 Windows 64 位 DirectShow 桌面应用，不承诺兼容所有 UWP/Windows 相机应用。

## 要求与本机改动

- Windows 10/11 64 位、.NET Framework 4.8、系统 H.264 解码组件。
- Android 12 或更高版本，支持 Camera2 的手机和 USB 数据线。
- 不要求 OBS、Unity 或 DroidCam。
- 注册仅写入当前 Windows 用户的 3 个项目专用注册表项，组件复制到 `%LOCALAPPDATA%\PhoneUsbCamera\VirtualCamera`。
- 不修改 OpenScreen 文件、OBS 配置或其他摄像头注册，不强制结束手机上的其他相机应用。
- 停止时只清理本次创建的 ADB 转发、临时服务端和本程序持有的画面。

卸载此独立摄像头的注册：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-camera.ps1 -Mode Uninstall
```

卸载脚本保留组件文件，便于恢复，不卸载其他摄像头。

## 构建与测试

源码构建需要 Visual Studio 2022 C++ Build Tools、Windows SDK、Windows 自带的 .NET Framework C# 编译器。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

构建会先编译原生组件并运行合成像素测试，再构建界面。缺少 scrcpy 时下载固定版本的官方发行包并核对 SHA-256。构建和布局测试均不会注册摄像头或开启手机镜头。

安装包使用 Inno Setup 6 构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

构建、测试和发布脚本只供维护者使用。普通用户运行安装包和 U镜本体时不需要执行 PowerShell。推送 `vMAJOR.MINOR.PATCH` 标签后，GitHub Actions 会构建安装包、SHA-256 校验文件并添加到对应 Release。

可选真机回归测试（会短暂使用手机；先停止现有摄像头会话）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test-capture.ps1
```

真机脚本检查两次启停、预览旋转、关闭预览后的输出、通过实际注册读取的连续帧。不保存照片或视频，不打开 OBS/OpenScreen。它不代替 OpenScreen 的真实录制测试。

## 当前验证状态

- 原生像素测试、C++/C# 构建和三种窗口布局检查通过。
- Xiaomi 24031PN0DC / Android 16，1080p 已实测连续输出。
- 已注册组件的读取测试：8 秒 235 帧，235 次画面指纹变化，没有非递增时间戳；测试时无 OBS 或 scrcpy 桌面进程。
- 已观察到 OpenScreen 选中 Phone USB Camera 并显示手机预览。
- 用户于 2026-09-06 手动确认：录下的手机画面可以在 OpenScreen 中观看。这是用户的真机验收反馈；未额外解析该录制文件。
- Windows 安装包已完成编译和静态校验；全新 Windows 用户环境的安装、升级和卸载闭环仍待扩大测试。
- 新增两次启停自动回归尚未完整执行：检测到已有独立会话后安全退出，未抢占用户镜头。长时间运行、拔插恢复及 4K30 仍需进一步测试。

v2 的历史辅助类目前保留在源码中，但 v3 主界面和诊断使用独立后端；旧 `--prepare-obs` / `--start-session` 入口已禁用，不会从这些入口启动 OBS。

## 开源许可

项目代码为 [MIT](LICENSE)。独立滤镜改编自 MIT 授权的 UnityCapture，保留作者与 DirectShow 基类版权声明；scrcpy 使用 Apache License 2.0。完整声明随程序包提供，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
