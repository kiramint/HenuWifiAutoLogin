# Henu wifi auto login

Auto connect the fxxking expensive and slow henu-student Wi-Fi.

You can deploy this program into OpenWRT or any other always running devices, this program can help you keep your network connecting.
It will automatically test network connectivity and re-auth when network connect breaks

## Usage

### On Windows

* Install  ASP .Net Core 8.0 Runtime from https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0

* Download this program from github release https://github.com/kiramint/HenuWifiAutoLogin/releases
* Run the program once `./HenuWifiAutoLogin.exe`
* Edit the configure file `config.json`
* Test network connectivity check component `./HenuWifiAutoLogin.exe -t`
* Test auth request `./HenuWifiAutoLogin.exe -s`
* Run the program, and enjoy. `./HenuWifiAutoLogin.exe`

### On Linux

* Install  ASP .Net Core 8.0 Runtime from https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0

  or install .Net Core using apt :  `sudo apt install dotnet-runtime-8.0`

* Download this program from github release https://github.com/kiramint/HenuWifiAutoLogin/releases

* Run the program once `dotnet HenuWifiAutoLogin.dll`
* Edit the configure file `config.json`
* Test network connectivity check component `dotnet HenuWifiAutoLogin.dll -t`
* Test auth request `dotnet HenuWifiAutoLogin.dll -s`
* Run the program, and enjoy. `dotnet HenuWifiAutoLogin.dll`
* To run in the background, use `screen dotnet HenuWifiAutoLogin.dll` then press `CTRL-A` and `CTRL-D`

### Reference

#### Terminal Option

```shell
Description:
  Henu wifi auto login. Power by the amazing dotNet Core 8.0

Usage:
  HenuWifiAutoLogin [options]

Options:
  -t              Test network connection once
  -s              Send auth data to henu wifi server
  -f <f>          Config file for this program. Default option: "./config.json"
  --version       Show version information
  -?, -h, --help  Show help and usage information
```

**Chinese version**

```shell
描述:
  河大Wifi自动登录. 由 dotNet Core 8.0 支持

用法:
  HenuWifiAutoLogin [选项]

选项:
  -t              测试一次网络连通性
  -s              发送一次认证到服务器
  -f <f>          指定配置文件，默认值: "./config.json"
  --version       显示软件版本
  -?, -h, --help  显示帮助与使用方法
```

#### Config file options

```json
{
  "Username": "Your henu account id",         /* henu-student 账号 */
  "Password": "Your henu account password",   /* henu-student 密码 */
  "Isp": "henuyd or henult or henudx",        /* henuyd：移动， henult：联通， henudx：电信 */
  "PingHostOrIp": "119.29.29.29",             /* 网络连通信测试Ping的IP地址或网址 */
  "PingTimeout": 1000,                        /* Ping 超时 */
  "PingDelayMs": 30000,                       /* Ping 检测间隔，不建议太短 */
  "UseFileLog": true,                         /* 启用文件日志 */
  "UseSyslogServer": false,                   /* 使用Syslog服务器记录日志 */
  "SyslogHost": "0.0.0.0",                    /* Syslog服务器地址 */
  "SyslogPort": 514                           /* Syslog服务器端口*/
}
```



## Build

On Linux :

```shell
git clone https://github.com/kiramint/HenuWifiAutoLogin.git
cd HenuWifiAutoLogin
dotnet build
cd bin/Debug/net8.0/
./HenuWifiAutoLogin
```

On Windows, you can use dotNet AOT, modify file `HenuWifiAutoLogin.csproj` set `<PublishAot>true</PublishAot>`

Then `dotnet build` or use the following command to publish the program

```shell
 dotnet publish -c Release
```

Note: Linux AOT is unavailable now because those warnings: IL2104, IL3000, IL3002

