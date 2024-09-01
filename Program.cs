using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace HenuWifiAutoLogin
{
    class ConfigData
    {
        public string Username { get; set; } = "NULL"; // henu id account
        public string Password { get; set; } = "NULL"; // henu id password
        public string Isp { get; set; } = "NULL"; // "henuyd" && "henult" && "henudx" && "henulocal"
        public string HenuWifiGateway { get; set; } = "10.16.0.1"; // Henu Jingming Dongyuan Gateway
        public string PingHostOrIp { get; set; } = "119.29.29.29"; // Network check url or address
        public int PingTimeout { get; set; } = 1000; // default: 1000
        public int PingDelayMs { get; set; } = 1000 * 30; // default: 1000*10
        public bool UseFileLog { get; set; } = true; // use log4net to log into file
        public bool UseSyslogServer { get; set; }
        public string SyslogHost { get; set; } = "0.0.0.0";
        public int SyslogPort { get; set; } = 514;
    }

    [JsonSerializable(typeof(ConfigData))]
    partial class ConfigDataSource : JsonSerializerContext
    {
        // Auto generated class
    }

    static class HenuWifi
    {
        public static async Task Main(string[] args)
        {
            // Ilog
            var iLog = LogManager.GetLogger("Main");

            // Args
            var testConnOption = new Option<bool>(
                name: "-t",
                description: "Test network connection once."
            );
            var testAuthOption = new Option<bool>(
                name: "-s",
                description: "Send auth data to henu wifi server"
            );
            var configFileOption = new Option<FileInfo?>(
                name: "-f",
                description: "Config file for this program. Default option: \"./config.json\""
            );
            var rootCommand = new RootCommand("Henu wifi auto login. Power by the amazing dotNet Core 8.0");
            rootCommand.AddOption(testConnOption);
            rootCommand.AddOption(testAuthOption);
            rootCommand.AddOption(configFileOption);

            rootCommand.SetHandler(context =>
            {
                var testConnInfo = context.ParseResult.GetValueForOption(testConnOption);
                var testAuthInfo = context.ParseResult.GetValueForOption(testAuthOption);
                var configInfo = context.ParseResult.GetValueForOption(configFileOption) ?? new FileInfo("config.json");

                var cancelTokonSource = new CancellationTokenSource();

                try
                {
                    // Read config
                    if (!File.Exists(configInfo.Name))
                    {
                        var configFileCreate = new FileStream(configInfo.Name, FileMode.Create, FileAccess.Write);
                        var configDataCreate = new ConfigData()
                        {
                            Username = "Your henu account id",
                            Password = "Your henu account password",
                            Isp = "henuyd or henult or henudx",
                            HenuWifiGateway = "10.16.0.1",
                            PingHostOrIp = "119.29.29.29",
                            PingTimeout = 1000,
                            PingDelayMs = 1000 * 30,
                            UseFileLog = true,
                            UseSyslogServer = false,
                            SyslogHost = "0.0.0.0",
                            SyslogPort = 514
                        };

                        var converted =
                            JsonSerializer.Serialize(configDataCreate, ConfigDataSource.Default.ConfigData);
                        var configWriter = new StreamWriter(configFileCreate);
                        configWriter.Write(converted);
                        configWriter.Close();
                        configFileCreate.Close();
                        Console.WriteLine("Config file generated, fill the config and run this program again.");
                        return;
                    }

                    var configFile = new FileStream(configInfo.Name, FileMode.Open, FileAccess.Read);
                    var configReader = new StreamReader(configFile);
                    var jsonData = configReader.ReadToEnd();
                    var configData =
                        JsonSerializer.Deserialize<ConfigData>(jsonData, ConfigDataSource.Default.ConfigData);

                    Debug.Assert(configData != null, "Config data parse error, config can't be null.");

                    // log4net
                    if (!File.Exists("log4net.config"))
                    {
                        var assembly = Assembly.GetExecutingAssembly();
                        var l4NConfStream = new FileStream("log4net.config", FileMode.Create, FileAccess.Write);
                        var l4NConfWriter = new StreamWriter(l4NConfStream);
                        var confFileStream = assembly.GetManifestResourceStream("HenuWifiAutoLogin.log4net.config");
                        if (confFileStream == null)
                        {
                            Console.WriteLine("Program assembly resource not found. Maybe compile error?");
                            Environment.Exit(-1);
                        }
                        var confFileText = new StreamReader(confFileStream).ReadToEnd();
                        l4NConfWriter.Write(confFileText);
                        l4NConfWriter.Close();
                        l4NConfStream.Close();
                        XmlConfigurator.Configure(new FileInfo("log4net.config"));
                    }

                    var hierarchy = (Hierarchy)LogManager.GetRepository();
                    var iAppender = hierarchy.Root.GetAppender("FileAppender");
                    if (iAppender == null)
                    {
                        Console.WriteLine("Log4Net component error, quit...");
                        Environment.Exit(-1);
                    }

                    var fileAppender = (RollingFileAppender)iAppender;

                    if (configData.UseFileLog)
                    {
                        fileAppender.Threshold = log4net.Core.Level.Warn;
                    }
                    else
                    {
                        fileAppender.Threshold = log4net.Core.Level.Off;
                        Console.WriteLine("Log4Net file appender off");
                    }

                    if (configData.UseSyslogServer)
                    {
                        var syslog = new RemoteSyslogAppender
                        {
                            RemoteAddress = IPAddress.Parse(configData.SyslogHost),
                            RemotePort = configData.SyslogPort,
                            Facility = RemoteSyslogAppender.SyslogFacility.User,
                            Layout = new PatternLayout
                            {
                                ConversionPattern = "%date %-5level %logger - %message%newline"
                            }
                        };
                        syslog.AddFilter(new LevelMatchFilter
                        {
                            LevelToMatch = log4net.Core.Level.Warn,
                            AcceptOnMatch = true
                        });
                        syslog.AddFilter(new DenyAllFilter());
                        syslog.ActivateOptions();
                        hierarchy.Root.AddAppender(syslog);
                    }

                    hierarchy.Configured = true;


                    // Action
                    if (!testAuthInfo && !testConnInfo)
                    {
                        Task.Run(async () => await CheckConnection(configData, cancelTokonSource.Token),
                            cancelTokonSource.Token).Wait(60 * 1000, cancelTokonSource.Token);
                    }
                    else if (testAuthInfo)
                    {
                        Task.Run(async () =>
                        {
                            var result = await SendRequest(configData);
                            Console.WriteLine(result);
                        }, cancelTokonSource.Token).Wait(60 * 1000, cancelTokonSource.Token);
                    }
                    else if (testConnInfo)
                    {
                        Task.Run(async () => await CheckConnection(configData, cancelTokonSource.Token, true),
                            cancelTokonSource.Token).Wait(60 * 1000, cancelTokonSource.Token);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    iLog.Warn("Task cancel by cancellation token, message: " + ex.Message);
                    Environment.Exit(0);
                }
                catch (OperationCanceledException ex)
                {
                    iLog.Warn("Operation cancel by cancellation token, message: " + ex.Message);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    iLog.Warn("Exception happend: " + ex.Message);
                    Environment.Exit(-1);
                }
            });
            await rootCommand.InvokeAsync(args);
        }

        private static IPAddress GetHenuWifiIpv4(ConfigData configData)
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var gateway = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address.ToString() == configData.HenuWifiGateway);
                if (gateway != null)
                {
                    var ip = ni.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ip != null)
                    {
                        return ip.Address;
                    }
                }
            }
            throw new Exception("Henu wifi connection not found!");
        }


        // Check network connection
        private static async Task CheckConnection(ConfigData configData, CancellationToken cancellationToken,
            bool checkOnly = false)
        {
            // ILog
            var iLog = LogManager.GetLogger("CheckConnection");

            // Test
            if (checkOnly)
            {
                try
                {
                    var ping = new Ping();
                    var pingReply = ping.Send(hostNameOrAddress: configData.PingHostOrIp,
                        timeout: configData.PingTimeout);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        Console.WriteLine("Network still available");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Network unreachable, try reconnect ...");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentException)
                    {
                        iLog.Error("Invalid PingHostOrIp setting in config file. message: " + ex.Message);
                        Environment.Exit(-1);
                    }

                    if (ex is PingException)
                    {
                        iLog.Error("Failed to ping because ICMP, message: " + ex.Message);
                        Environment.Exit(-1);
                    }

                    iLog.Error("Ping exception caught, message: " + ex.Message);
                    Environment.Exit(-1);
                }
            }

            // Run
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var ping = new Ping();
                    var pingReply = ping.Send(hostNameOrAddress: configData.PingHostOrIp,
                        timeout: configData.PingTimeout);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        iLog.Info("Network still available");
                        await Task.Delay(configData.PingDelayMs, cancellationToken);
                    }
                    else
                    {
                        iLog.Warn("Network unreachable, try reconnect ...");
                        var result = await SendRequest(configData);
                        iLog.Warn("Auth result: " + result);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentException)
                    {
                        iLog.Error("Invalid PingHostOrIp setting in config file. message: " + ex.Message);
                        Environment.Exit(-1);
                    }

                    if (ex is PingException)
                    {
                        iLog.Error("Failed to ping because ICMP, message: " + ex.Message);
                        Environment.Exit(-1);
                    }

                    iLog.Error("Ping exception caught, message: " + ex.Message);
                    Environment.Exit(-1);
                }
            }
        }

        // Send auth request
        private static async Task<string> SendRequest(ConfigData configData)
        {
            // ILog
            var iLog = LogManager.GetLogger("SendRequest");

            // Get Ip

            IPAddress henuIp;
            try
            {
                henuIp = GetHenuWifiIpv4(configData);
            }
            catch (Exception ex)
            {
                iLog.Error("Exception happend: " + ex.Message);
                return "Exception caught";
            }


            var client = new HttpClient();
            // Headers
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/signed-exchange", 0.7));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN", 0.9));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            client.DefaultRequestHeaders.Add("DNT", "1");
            client.DefaultRequestHeaders.Add("Origin", "http://172.29.35.25:9999");
            client.DefaultRequestHeaders.Add("Referer",
                "http://172.29.35.25:9999/portalReceiveAction.do?wlanuserip=10.36.219.90&wlanacname=HD-SuShe-ME60");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0");
            // Cookie
            client.DefaultRequestHeaders.Add("Cookie",
                "userName=" + configData.Username + "; " + configData.Username + "=" + configData.Password +
                "; useridtemp=" + configData.Username + "@" + configData.Isp);
            // Content type
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            // Auth form content
            var content = new FormUrlEncodedContent(new[]
            {
                // General config data
                new KeyValuePair<string, string>("wlanuserip", henuIp.ToString()),     //Todo: auth need ip
                new KeyValuePair<string, string>("wlanacname", "HD-SuShe-ME60"),
                new KeyValuePair<string, string>("chal_id", ""),
                new KeyValuePair<string, string>("chal_vector", ""),
                new KeyValuePair<string, string>("auth_type", "PAP"),
                new KeyValuePair<string, string>("seq_id", ""),
                new KeyValuePair<string, string>("req_id", ""),
                new KeyValuePair<string, string>("wlanacIp", "172.22.254.253"),
                new KeyValuePair<string, string>("ssid", ""),
                new KeyValuePair<string, string>("vlan", ""),
                new KeyValuePair<string, string>("mac", ""),
                new KeyValuePair<string, string>("message", ""),
                new KeyValuePair<string, string>("bank_acct", ""),
                new KeyValuePair<string, string>("isCookies", ""),
                new KeyValuePair<string, string>("version", "0"),
                new KeyValuePair<string, string>("authkey", "88----89"),
                new KeyValuePair<string, string>("url", ""),
                new KeyValuePair<string, string>("usertime", "0"),
                new KeyValuePair<string, string>("listpasscode", "0"),
                new KeyValuePair<string, string>("listgetpass", "0"),
                new KeyValuePair<string, string>("getpasstype", "0"),
                new KeyValuePair<string, string>("randstr", "9015"),
                new KeyValuePair<string, string>("domain", ""),
                new KeyValuePair<string, string>("isRadiusProxy", "true"),
                new KeyValuePair<string, string>("usertype", "0"),
                new KeyValuePair<string, string>("isHaveNotice", "0"),
                new KeyValuePair<string, string>("times", "12"),
                new KeyValuePair<string, string>("weizhi", "0"),
                new KeyValuePair<string, string>("smsid", ""),
                new KeyValuePair<string, string>("freeuser", ""),
                new KeyValuePair<string, string>("freepasswd", ""),
                new KeyValuePair<string, string>("listwxauth", "0"),
                new KeyValuePair<string, string>("templatetype", "1"),
                new KeyValuePair<string, string>("tname", "henandaxue_pc_portal_V2.1"),
                new KeyValuePair<string, string>("logintype", "0"),
                new KeyValuePair<string, string>("act", ""),
                new KeyValuePair<string, string>("is189", "false"),
                new KeyValuePair<string, string>("terminalType", ""),
                new KeyValuePair<string, string>("checkterminal", "true"),
                new KeyValuePair<string, string>("portalpageid", "161"),
                new KeyValuePair<string, string>("listfreeauth", "0"),
                new KeyValuePair<string, string>("viewlogin", "1"),
                new KeyValuePair<string, string>("userid", configData.Username + "@" + configData.Isp),
                new KeyValuePair<string, string>("authGroupId", ""),
                new KeyValuePair<string, string>("smsoperatorsflat", ""),

                // User config data
                new KeyValuePair<string, string>("useridtemp", configData.Username + "@" + configData.Isp),
                new KeyValuePair<string, string>("passwd", configData.Password),
                new KeyValuePair<string, string>("operator", "@" + configData.Isp)
            });

            try
            {
                // Request
                var response = await client.PostAsync("http://172.29.35.25:9999/portalAuthAction.do", content);
                // Return result
                response.EnsureSuccessStatusCode();
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                iLog.Error("Http request faild. Probably not connet to henu-student. Message: " + ex.Message);
                return "Exception caught";
            }
            catch (Exception ex)
            {
                iLog.Error("Exception happend: " + ex.Message);
                return "Exception caught";
            }
        }
    }
}