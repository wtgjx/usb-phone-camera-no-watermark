# U镜 v3.0.1

这是 U镜首个不依赖 OBS 的 Windows 安装版。

## 主要变化

- Android 手机通过 USB / ADB 直接输出为 `Phone USB Camera`；
- 使用独立 DirectShow 摄像头组件，无需安装或启动 OBS；
- 新增 U镜 / UCam 品牌、浅色单窗口和内置实时预览；
- 安装程序自动注册摄像头，并创建桌面与开始菜单入口；
- 普通用户无需下载源码或手动执行 PowerShell；
- 安装、升级和卸载只处理 U镜自己的文件与注册项。

## 使用要求

- Windows 10/11 64 位；
- Android 12 或更高版本；
- 支持数据传输的 USB 线；
- 手机开启 USB 调试，并确认电脑的 RSA 授权。

## 已知限制

- 当前安装包未签名，首次下载可能出现 Windows SmartScreen 提示；
- 目前面向 64 位 DirectShow 摄像头消费者；
- 1080p30 为推荐档位，2K/4K 仍属于实验档；
- 全新 Windows 用户环境的安装、升级和卸载闭环仍待扩大测试。
