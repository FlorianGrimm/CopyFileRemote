// rgdevl-flori-CopyFileRemote
public class Program {
    public static async Task<int> Main(string[] args) {
        var appSettings = GetAppSettings(args);
        if (appSettings.ServiceType == ServiceType.Unknown) {
            System.Console.Error.WriteLine("ServiceType is not configured and cannot be determined.");
            return -1;
        }
        // if (appSettings.HostType == HostType.Unknown) {
        //     System.Console.Error.WriteLine("HostType is not configured and cannot be determined.");
        //     return -1;
        // }
        if (string.IsNullOrEmpty(appSettings.ServiceBusConnectionString)
            && string.IsNullOrEmpty(appSettings.ServiceBusNamespace)
        ) {
            System.Console.Error.WriteLine("ServiceBusConnectionString nor ServiceBusNamespace is not configured.");
            return -1;
        }
        var (r, transportService) = await SetupPipeline(appSettings);
        if (r != 0 || transportService is null) {
            return r;
        }
        try {
            if (!string.IsNullOrEmpty(appSettings.InputPath)) {
                return await LocalReadFromFS(appSettings, transportService);
            }
            if (!string.IsNullOrEmpty(appSettings.OutputPath)) {
                return await LocalWriteToFS(appSettings, transportService);
            }
            System.Console.Error.WriteLine("No input or output path specified.");
            return -1;
        } finally {
            await transportService.DisposeAsync();
        }
    }

    private static AppSettings GetAppSettings(string[] args) {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        configurationBuilder.AddEnvironmentVariables();
        configurationBuilder.AddCommandLine(args);
        configurationBuilder.AddUserSecrets(typeof(Program).Assembly);
        configurationBuilder.AddUserSecrets("copyfileremote", reloadOnChange: false);

        var configuration = configurationBuilder.Build();
        var appSettings = new AppSettings();
        configuration.Bind(appSettings);
        if (!string.IsNullOrEmpty(appSettings.Json)) {
            configurationBuilder.AddJsonFile(appSettings.Json, optional: false, reloadOnChange: false);
            configuration = configurationBuilder.Build();
            configuration.Bind(appSettings);
        }

        var computerName = System.Environment.GetEnvironmentVariable("COMPUTERNAME");
        if (string.Equals(appSettings.HostMe, computerName, StringComparison.OrdinalIgnoreCase)) {
            appSettings.HostType = HostType.HostA;
        }
        if (string.Equals(appSettings.HostOther, computerName, StringComparison.OrdinalIgnoreCase)) {
            appSettings.HostType = HostType.HostB;
        }

        return appSettings;
    }

    private static ServiceBusAdministrationClient? createServiceBusAdministrationClient(AppSettings appSettings) {
        if (!string.IsNullOrEmpty(appSettings.ServiceBusNamespace)) {
            var tokenCredential = GetTokenCredential(appSettings);
            return new ServiceBusAdministrationClient(appSettings.ServiceBusNamespace, tokenCredential);
        } else if (!string.IsNullOrEmpty(appSettings.ServiceBusConnectionString)) {
            return new ServiceBusAdministrationClient(appSettings.ServiceBusConnectionString!);
        } else {
            return null;
        }
    }

    private static async Task<(int result, ITransportService? transportService)> SetupPipeline(AppSettings appSettings) {
        System.Console.Out.WriteLine("Creating the Service Bus Administration Client object");
        var adminClient = createServiceBusAdministrationClient(appSettings);
        if (adminClient is null) {
            System.Console.Error.WriteLine("ServiceBusNamespace or ServiceBusConnectionString is not configured.");
            return (-1, null);
        }
        if (appSettings.ServiceType == ServiceType.ServiceBusQueue) {
            var transportService = (ServiceBusQueueTransportService?)CreateTransportService(appSettings);
            if (transportService is null) {
                System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is not supported.");
                return (-1, null);
            }
            await transportService.Setup(adminClient);
            return (0, transportService);
        }
        if (appSettings.ServiceType == ServiceType.ServiceBusTopic) {
            // not ready yet
            var topicName = appSettings.GetServiceBusTopic();
            System.Console.Out.WriteLine($"Creating the topic {topicName}");
            var responceGetTopic = await adminClient.GetTopicAsync(topicName);
            if (responceGetTopic is null) {
                var responceCreateTopic = await adminClient.CreateTopicAsync(topicName);
                if (responceCreateTopic is null) {
                    System.Console.Error.WriteLine($"Failed to create topic {topicName}");
                    return (-1, null);
                }
                var topic = responceCreateTopic.Value;
                topic.DefaultMessageTimeToLive = TimeSpan.FromHours(1);
                await adminClient.UpdateTopicAsync(topic);
            }
            //return (0,transportService);
            return (-1, null);
        }
        return (-1, null);
    }

    private static TokenCredential GetTokenCredential(AppSettings appSettings) {
        TokenCredential tokenCredential;
        if (appSettings.ClientSecretOptions is ClientSecretOptions clientSecretOptions
            && !string.IsNullOrEmpty(clientSecretOptions.TenantId)
            && !string.IsNullOrEmpty(clientSecretOptions.ClientId)
            && !string.IsNullOrEmpty(clientSecretOptions.ClientSecret)
        ) {
            tokenCredential = new Azure.Identity.ClientSecretCredential(clientSecretOptions.TenantId!, clientSecretOptions.ClientId!, clientSecretOptions.ClientSecret!);
        } else {
            var defaultAzureCredentialOptions = appSettings.DefaultAzureCredentialOptions ?? new() { ExcludeInteractiveBrowserCredential = false };
            tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
        }

        return tokenCredential;
    }

    private static async Task<int> LocalReadFromFS(AppSettings appSettings, ITransportService transportService) {
        var inputPath = appSettings.InputPath;
        if (string.IsNullOrEmpty(inputPath)) {
            return -1;
        }
        if (!inputPath.EndsWith(System.IO.Path.DirectorySeparatorChar)) {
            inputPath = inputPath + System.IO.Path.DirectorySeparatorChar;
        }
        var lstFile = ReadDirectory(appSettings, inputPath);
        if (lstFile is null) {
            return -1;
        }

        // ITransportService? transportService = CreateTransportService(appSettings);
        if (transportService is null) {
            System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is not supported.");
            return -1;
        }

        var taskReadListFileHash = ReadListFileHash(lstFile, inputPath);

        await transportService.OpenForLocalReadFromFS();
        await transportService.Ping();
        try {
            var listFileHashSender = await taskReadListFileHash;

            // var json = System.Text.Json.JsonSerializer.Serialize(listFileHashSender);

            await transportService.Send(new Message("ListFileHash", "application/json", System.BinaryData.FromObjectAsJson(listFileHashSender)));

            while (true) {
                GetFileContent? getFileContent = null;
                while (true) {
                    var msg = await transportService.Receive();
                    if (msg is null) {
                        await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.NextInt64() % 500));
                        continue;
                    }
                    if (msg.Subject == "GetFileContent") {
                        getFileContent = msg.Body.ToObjectFromJson<GetFileContent>();
                        break;
                    }
                    if (msg.Subject == "Goodbye") {
                        return 0;
                    }
                }
                var hsRelativeNameSender = new HashSet<string>(listFileHashSender.LstFileHash.Select(x => x.RelativeName), StringComparer.OrdinalIgnoreCase);

                if (getFileContent is null) {
                    return -1;
                }

                long size = 0;
                PutFileContent putFileContent = new(new());
                Queue<string> queueRelativeName = new();
                foreach (var relativeName in getFileContent.LstRelativeName) {
                    queueRelativeName.Enqueue(relativeName);
                }

                while (queueRelativeName.TryDequeue(out var relativeName)) {
                    if (!hsRelativeNameSender.Contains(relativeName)) {
                        continue;
                    }
                    System.Console.Out.WriteLine(relativeName);
                    var content = await System.IO.File.ReadAllBytesAsync(inputPath + relativeName);
                    var fileContent = new FileContent(relativeName, Convert.ToBase64String(content));
                    putFileContent.LstFileContent.Add(fileContent);
                    size += content.Length;
                    if (size > 10 * 1024) {
                        await transportService.Send(new Message("PutFileContent", "application/json", System.BinaryData.FromObjectAsJson(putFileContent)));
                        putFileContent = new(new());
                        size = 0;
                        await Task.Delay(TimeSpan.FromMilliseconds(10));
                        while (true) {
                            var msg = await transportService.Receive();
                            if (msg is null) {
                                await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.NextInt64() % 500));
                                continue;
                            }
                            if (msg.Subject == "GetFileContent") {
                                var getFileContentNext = msg.Body.ToObjectFromJson<GetFileContent>();
                                foreach (var relativeNameNext in getFileContentNext.LstRelativeName) {
                                    queueRelativeName.Enqueue(relativeNameNext);
                                }
                                break;
                            }
                            if (msg.Subject == "Goodbye") {
                                return 0;
                            }
                            if (msg.Subject == "Continue") {
                                break;
                            }
                        }
                    }
                }
                if (putFileContent.LstFileContent.Count > 0) {
                    await transportService.Send(new Message("PutFileContent", "application/json", System.BinaryData.FromObjectAsJson(putFileContent)));
                }
                await transportService.Send(new Message("More?", "application/json", System.BinaryData.FromString("More?")));
            }
        } finally {
            await transportService.Send(new Message("Goodbye", "application/json", System.BinaryData.FromString("Goodbye")));
            while ((await transportService.Receive(TimeSpan.FromSeconds(2))) is not null) { }
        }
        return 0;
    }

    private static async Task<int> LocalWriteToFS(AppSettings appSettings, ITransportService transportService) {
        var outputPath = appSettings.OutputPath;
        if (string.IsNullOrEmpty(outputPath)) {
            return -1;
        }
        if (!outputPath.EndsWith(System.IO.Path.DirectorySeparatorChar)) {
            outputPath = outputPath + System.IO.Path.DirectorySeparatorChar;
        }
        var lstFile = ReadDirectory(appSettings, outputPath);
        if (lstFile is null) {
            return -1;
        }

        //ITransportService? transportService = CreateTransportService(appSettings);
        if (transportService is null) {
            System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is not supported.");
            return -1;
        }

        var taskReadListFileHash = ReadListFileHash(lstFile, outputPath);

        await transportService.OpenForLocalWriteToFS();

        await transportService.WaitForPing();

        try {
            ListFileHash? listFileHashSender = null;
            while (true) {
                var msg = await transportService.Receive();
                if (msg is null) {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.NextInt64() % 500));
                    continue;
                }
                if (msg.Subject == "ListFileHash") {
                    listFileHashSender = msg.Body.ToObjectFromJson<ListFileHash>();
                    break;
                }
            }
            if (listFileHashSender is null) {
                System.Console.Error.WriteLine($"ListFileHash is not received.");
                return -1;
            }

            var listFileHashReceiver = await taskReadListFileHash;
            var dictFileHashReceiver = listFileHashReceiver.LstFileHash.ToDictionary(x => x.RelativeName, x => x);

            var getFileContent = new GetFileContent(new());
            foreach (var fileHash in listFileHashSender.LstFileHash) {
                if (dictFileHashReceiver.TryGetValue(fileHash.RelativeName, out var fileHashReceiver)) {
                    if (fileHashReceiver.Hash == fileHash.Hash) {
                        continue;
                    }
                }
                getFileContent.LstRelativeName.Add(fileHash.RelativeName);
            }
            await transportService.Send(new Message("GetFileContent", "application/json", System.BinaryData.FromObjectAsJson(getFileContent)));

            while (true) {
                var msg = await transportService.Receive();
                if (msg is null) {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.NextInt64() % 500));
                    continue;
                }
                if (msg.Subject == "PutFileContent") {
                    var putFileContent = msg.Body.ToObjectFromJson<PutFileContent>();
                    foreach (var fileContent in putFileContent.LstFileContent) {
                        var relativeName = fileContent.RelativeName;
                        System.Console.Out.WriteLine(relativeName);
                        var file = new FileInfo(outputPath + relativeName);
                        if (!file.Directory!.Exists) {
                            file.Directory.Create();
                        }
                        if (string.IsNullOrEmpty(fileContent.ContentBase64)) {
                            System.IO.File.WriteAllText(file.FullName, "");
                        } else {
                            await System.IO.File.WriteAllBytesAsync(file.FullName, Convert.FromBase64String(fileContent.ContentBase64));
                        }
                    }
                    await transportService.Send(new Message("Continue", "application/json", System.BinaryData.FromString("Continue")));
                    continue;
                }

                if (msg.Subject == "Goodbye") {
                    break;
                }
                

                if (msg.Subject == "More?") {
                    break;
                }
            }
        } finally {
            await transportService.Send(new Message("Goodbye", "application/json", System.BinaryData.FromString("Goodbye")));
            while ((await transportService.Receive(TimeSpan.FromSeconds(2))) is not null) { }
        }

        return 0;
    }
    private static List<FileInfo>? ReadDirectory(AppSettings appSettings, string? path) {
        if (string.IsNullOrEmpty(path)) {
            System.Console.Error.WriteLine($"Path is not configured.");
            return null;
        }
        var diRoot = new DirectoryInfo(path!);
        if (!diRoot.Exists) {
            System.Console.Error.WriteLine($"Path '{path}' does not exist.");
            return null;
        }

        List<DirectoryInfo> lstDirectoryFiltered;
        if (appSettings.RecurseSubdirectories) {
            var lstDirectoryAll = new[] { diRoot }
                .Concat(
                    diRoot.EnumerateDirectories("*", new EnumerationOptions() {
                        RecurseSubdirectories = true,
                        MatchCasing = MatchCasing.CaseInsensitive,
                        MatchType = MatchType.Simple,
                        AttributesToSkip = FileAttributes.Hidden,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false,
                    }))
    ;
            var hsExcludeDirectory = new HashSet<string>(appSettings.ExcludeDirectory, StringComparer.OrdinalIgnoreCase);
            var regexExcludeDirectoryNames = appSettings.ExcludeDirectory.Select(ed => "(" + Regex.Escape(ed) + ")");
            var regexExclude = new Regex(
                "[/\\\\]" +
                string.Join("|", regexExcludeDirectoryNames) +
                "[/\\\\]"
                );
            lstDirectoryFiltered = lstDirectoryAll.Where(d => !hsExcludeDirectory.Contains(d.Name) && !regexExclude.IsMatch(d.FullName)).ToList();
        } else {
            lstDirectoryFiltered = new List<DirectoryInfo>() { diRoot };
        }

        //appSettings.ExcludeDirectory.Select(ed=>$"([/\\]{Regex.Escape(ed)}[/\\])")

        //appSettings.ExcludeDirectory.Select(ed=>System.IO.Path.PathSeparator+ed+System.IO.Path.PathSeparator);
        /*
        foreach (var directory in lstDirectoryAll)
        {
            System.Console.WriteLine(directory.FullName);
        }
        */
        foreach (var directory in lstDirectoryFiltered) {
            System.Console.WriteLine(directory.FullName);
        }

        var hsIncludeExtensions = new HashSet<string>(appSettings.IncludeExtensions, StringComparer.OrdinalIgnoreCase);

        var lstFileAll = lstDirectoryFiltered.SelectMany(
            di => di.EnumerateFiles("*", new EnumerationOptions() {
                RecurseSubdirectories = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                MatchType = MatchType.Simple,
                AttributesToSkip = FileAttributes.Hidden,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
            })
            ).ToList();
        var lstFileExtensionExcluded = lstFileAll.Where(fi => !DoesExtensionMatch(fi, hsIncludeExtensions)).Select(fi => fi.Extension).Distinct().ToList();
        if (lstFileExtensionExcluded.Count > 0) {
            System.Console.WriteLine("Excluded file extensions:");
            foreach (var fileExtension in lstFileExtensionExcluded) {
                System.Console.WriteLine("\"" + fileExtension + "\",");
            }
        }

        var lstFile = lstFileAll
            .Where(fi => DoesExtensionMatch(fi, hsIncludeExtensions))
            .ToList();
        return lstFile;
    }

    private static async Task<ListFileHash> ReadListFileHash(List<FileInfo> lstFile, string inputPath) {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var listFileHash = new ListFileHash(new List<FileHash>());
        foreach (var file in lstFile) {
            var content = await System.IO.File.ReadAllBytesAsync(file.FullName);
            var relativeName = file.FullName.Substring(inputPath.Length);
            var hash = Convert.ToBase64String(sha1.ComputeHash(content));
            listFileHash.LstFileHash.Add(new FileHash(relativeName, hash));
        }
        return listFileHash;
    }

    private static ITransportService? CreateTransportService(AppSettings appSettings) {
        if (appSettings.ServiceType == ServiceType.ServiceBusQueue) {
            if (!string.IsNullOrEmpty(appSettings.ServiceBusNamespace)) {
                var tokenCredential = GetTokenCredential(appSettings);
                return new ServiceBusQueueTransportService(
                    appSettings.GetServiceBusQueueMeOther(),
                    appSettings.GetServiceBusQueueOtherMe(),
                    appSettings.ServiceBusNamespace,
                    tokenCredential,
                    appSettings.ServiceBusClientOptions
                    );
            } else if (!string.IsNullOrEmpty(appSettings.ServiceBusConnectionString)) {
                return new ServiceBusQueueTransportService(
                    appSettings.GetServiceBusQueueMeOther(),
                    appSettings.GetServiceBusQueueOtherMe(),
                    appSettings.ServiceBusConnectionString,
                    appSettings.ServiceBusClientOptions
                    );
            } else {
                System.Console.Error.WriteLine($"ServiceBusNamespace or ServiceBusConnectionString must be specified.");
                return null;
            }
        } else if (appSettings.ServiceType == ServiceType.ServiceBusTopic) {
            System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is NYI.");
            //     transportService = new ServiceBusTopicTransportService(appSettings.ServiceBusTopicOptions!);
            return null;
        } else {
            System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is not supported.");
            return null;
        }
    }

    private static bool DoesExtensionMatch(FileInfo fi, HashSet<string> hsIncludeExtensions) {
        return fi.Extension.Length == 0 || hsIncludeExtensions.Contains(fi.Extension);
    }


}

public record ListFileHash(
    List<FileHash> LstFileHash
);
public record FileHash(string RelativeName, string Hash);

public record GetFileContent(
    List<string> LstRelativeName
);
public record PutFileContent(
    List<FileContent> LstFileContent
);
public record FileContent(string RelativeName, string ContentBase64);

public enum HostType {
    Unknown,
    HostA,
    HostB,
}

public enum ServiceType {
    Unknown,
    ServiceBusQueue,
    ServiceBusTopic,
}

public class AppSettings {
    public ServiceType ServiceType { get; set; } = ServiceType.Unknown;
    public HostType HostType { get; set; } = HostType.Unknown;
    public string? HostMe { get; set; }
    public string? HostOther { get; set; }
    public string? Json { get; set; }
    public string? InputPath { get; set; }
    public string[] IncludeExtensions { get; set; } = new[] { ".sln", ".cs", ".csproj", ".ts", ".tsx", ".txt" };
    public string[] ExcludeDirectory { get; set; } = new[] { "node_modules", "bower_components", "obj", "bin", "packages" };
    public string? OutputPath { get; set; }
    public bool RecurseSubdirectories { get; set; } = true;
    public string? ServiceBusNamespace { get; set; }
    public string? ServiceBusConnectionString { get; set; }
    public string? ServiceBusQueueMeOther { get; set; }
    public string? ServiceBusQueueOtherMe { get; set; }
    public string GetServiceBusQueueMeOther() {
        return ServiceBusQueueMeOther ?? $"Queue-{HostMe}-{HostOther}";
    }
    public string GetServiceBusQueueOtherMe() {
        return ServiceBusQueueMeOther ?? $"Queue-{HostOther}-{HostMe}";
    }

    public string? ServiceBusTopic { get; set; }
    public string GetServiceBusTopic() {
        return ServiceBusTopic ?? $"{HostMe}-{HostOther}";
    }

    public ServiceBusClientOptions? ServiceBusClientOptions { get; set; }
    public DefaultAzureCredentialOptions? DefaultAzureCredentialOptions { get; set; }
    public ClientSecretOptions? ClientSecretOptions { get; set; }

    public AppSettings() {
        this.ServiceBusClientOptions = new ServiceBusClientOptions {
            TransportType = ServiceBusTransportType.AmqpWebSockets
        };
        this.ClientSecretOptions = new ClientSecretOptions();
        this.DefaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
    }
}

public class ClientSecretOptions {
    /// <summary>
    /// Gets the Azure Active Directory tenant (directory) Id of the service principal
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets the client (application) ID of the service principal
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Gets the client secret that was generated for the App Registration used to authenticate the client.
    /// </summary>
    public string? ClientSecret { get; set; }
}
public record Message(
    string Subject,
    string ContentType,
    System.BinaryData Body
);
public interface ITransportService : IAsyncDisposable {
    Task OpenForLocalReadFromFS();
    Task OpenForLocalWriteToFS();

    Task Ping();
    Task WaitForPing();

    Task Send(Message message);
    Task<Message?> Receive(TimeSpan? timeout = default);

    // Task<int> Setup(ServiceBusAdministrationClient adminClient);
}
public class ServiceBusQueueTransportService : ITransportService, IAsyncDisposable {
    private readonly string _QueueNameMeOther;
    private readonly string _QueueNameOtherMe;
    private readonly string? _ServiceBusConnectionString;
    private readonly string? _ServiceBusNamespace;
    private readonly TokenCredential? _TokenCredential;
    private readonly ServiceBusClientOptions? _ServiceBusClientOptions;
    private ServiceBusClient? _Client;
    private string _SenderQueueName = "";
    private ServiceBusSender? _Sender;
    private string _ReceiverQueueName = "";
    private ServiceBusReceiver? _Receiver;

    public ServiceBusQueueTransportService(string queueNameMeOther, string queueNameOtherMe, string serviceBusConnectionString, ServiceBusClientOptions? serviceBusClientOptions = null) {
        if (string.IsNullOrEmpty(queueNameMeOther)) { throw new ArgumentNullException(nameof(queueNameMeOther)); }
        if (string.IsNullOrEmpty(queueNameOtherMe)) { throw new ArgumentNullException(nameof(queueNameOtherMe)); }
        if (string.IsNullOrEmpty(serviceBusConnectionString)) { throw new ArgumentNullException(nameof(serviceBusConnectionString)); }
        this._QueueNameMeOther = queueNameMeOther;
        this._QueueNameOtherMe = queueNameOtherMe;
        this._ServiceBusConnectionString = serviceBusConnectionString;
        this._ServiceBusClientOptions = serviceBusClientOptions;
        this._ServiceBusNamespace = null;
        this._TokenCredential = null;

    }
    public ServiceBusQueueTransportService(string queueNameMeOther, string queueNameOtherMe, string serviceBusNamespace, TokenCredential tokenCredential, ServiceBusClientOptions? serviceBusClientOptions = null) {
        if (string.IsNullOrEmpty(queueNameMeOther)) { throw new ArgumentNullException(nameof(queueNameMeOther)); }
        if (string.IsNullOrEmpty(queueNameOtherMe)) { throw new ArgumentNullException(nameof(queueNameOtherMe)); }
        if (string.IsNullOrEmpty(serviceBusNamespace)) { throw new ArgumentNullException(nameof(serviceBusNamespace)); }

        this._QueueNameMeOther = queueNameMeOther;
        this._QueueNameOtherMe = queueNameOtherMe;
        this._ServiceBusNamespace = serviceBusNamespace;
        this._TokenCredential = tokenCredential;
        this._ServiceBusClientOptions = serviceBusClientOptions;
        this._ServiceBusConnectionString = null;
    }
    public Task OpenForLocalReadFromFS() {
        // var credential = new DefaultAzureCredential();
        // var client = new ServiceBusClient("Endpoint=sb://my-service-bus-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=secret", credential);

        if (!string.IsNullOrEmpty(this._ServiceBusNamespace)) {
            this._Client = new ServiceBusClient(this._ServiceBusNamespace, this._TokenCredential, this._ServiceBusClientOptions);
        } else if (!string.IsNullOrEmpty(this._ServiceBusConnectionString)) {
            this._Client = new ServiceBusClient(this._ServiceBusConnectionString, this._ServiceBusClientOptions);
        } else {
            throw new Exception("No connection string or namespace provided");
        }
        this._SenderQueueName = this._QueueNameMeOther;
        this._Sender = _Client.CreateSender(this._QueueNameMeOther);
        this._ReceiverQueueName = this._QueueNameOtherMe;
        this._Receiver = _Client.CreateReceiver(this._QueueNameOtherMe);
        return Task.CompletedTask;
    }
    public Task OpenForLocalWriteToFS() {
        if (!string.IsNullOrEmpty(this._ServiceBusNamespace)) {
            this._Client = new ServiceBusClient(this._ServiceBusNamespace, this._TokenCredential, this._ServiceBusClientOptions);
        } else if (!string.IsNullOrEmpty(this._ServiceBusConnectionString)) {
            this._Client = new ServiceBusClient(this._ServiceBusConnectionString, this._ServiceBusClientOptions);
        } else {
            throw new Exception("No connection string or namespace provided");
        }
        this._SenderQueueName = this._QueueNameOtherMe;
        this._Sender = _Client.CreateSender(this._QueueNameOtherMe);
        this._ReceiverQueueName = this._QueueNameMeOther;
        this._Receiver = _Client.CreateReceiver(this._QueueNameMeOther);
        return Task.CompletedTask;
    }

    public async Task<int> Setup(ServiceBusAdministrationClient adminClient) {
        Azure.Response<QueueProperties>? responceGetQueueMeOther = null;
        try {
            responceGetQueueMeOther = await adminClient.GetQueueAsync(this._QueueNameMeOther);
        } catch {
        }
        if (responceGetQueueMeOther is not null) {
            System.Console.Out.WriteLine($"Found exisiting queue {_QueueNameMeOther}");
        } else {
            System.Console.Out.WriteLine($"Creating the queue {_QueueNameMeOther}");
            var responceCreateQueueAB = await adminClient.CreateQueueAsync(_QueueNameMeOther);
            if (responceCreateQueueAB is null) {
                System.Console.Error.WriteLine($"Failed to create queue {_QueueNameMeOther}");
                return -1;
            }
            var queueMeOther = responceCreateQueueAB.Value;
            queueMeOther.DefaultMessageTimeToLive = TimeSpan.FromHours(1);
            await adminClient.UpdateQueueAsync(queueMeOther);

        }
        Azure.Response<QueueProperties>? responceGetQueueOtherMe = null;
        try {
            responceGetQueueOtherMe = await adminClient.GetQueueAsync(this._QueueNameOtherMe);
        } catch {
        }
        if (responceGetQueueOtherMe is not null) {
            System.Console.Out.WriteLine($"Found exisiting queue {_QueueNameOtherMe}");
        } else {
            System.Console.Out.WriteLine($"Creating the queue {_QueueNameOtherMe}");
            var responceCreateQueueOtherMe = await adminClient.CreateQueueAsync(_QueueNameOtherMe);
            if (responceCreateQueueOtherMe is null) {
                System.Console.Error.WriteLine($"Failed to create queue {_QueueNameOtherMe}");
                return -1;
            }
            var queueOtherMe = responceCreateQueueOtherMe.Value;
            queueOtherMe.DefaultMessageTimeToLive = TimeSpan.FromHours(1);
            await adminClient.UpdateQueueAsync(queueOtherMe);
        }
        return 0;
    }


    public async Task Ping() {
        if (this._Sender is null) {
            throw new Exception("Sender is null");
        }
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }
        System.Console.Out.WriteLine($"Sending ping {_SenderQueueName}");
        ;
        var tick = Random.Shared.NextInt64().ToString();
        await this._Sender.SendMessageAsync(
            new ServiceBusMessage(tick) {
                ContentType = "text/plain",
                Subject = "ping"
            }
            );
        while (true) {
            System.Console.Out.WriteLine($"Wait for pong  {_ReceiverQueueName}");
            var msg = await this._Receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            if (msg is not null) {
                try {
                    if (msg.ContentType == "text/plain" && msg.Subject == "pong") {
                        if (msg.Body.ToString() == tick) {
                            System.Console.Out.WriteLine($"pong received {tick}");
                            break;
                        } else {
                            System.Console.Out.WriteLine($"Wrong tick {msg.Body.ToString()} != {tick}");
                        }
                    } else {
                        System.Console.Out.WriteLine($"Message received {msg.ContentType} {msg.Subject} {msg.Body}");
                    }
                } finally {
                    await this._Receiver.CompleteMessageAsync(msg);
                }
            }
            await Task.Delay(1000);
        }
        await this._Sender.SendMessageAsync(
            new ServiceBusMessage(tick) {
                ContentType = "text/plain",
                Subject = "pang"
            }
            );
    }
    public async Task WaitForPing() {
        if (this._Sender is null) {
            throw new Exception("Sender is null");
        }
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }

        string tick = "";
        while (true) {
            System.Console.Out.WriteLine($"Wait for ping  {_ReceiverQueueName}");
            var msg = await this._Receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            if (msg is not null) {
                try {
                    if (msg.ContentType == "text/plain" && msg.Subject == "ping") {
                        tick = msg.Body.ToString();
                        System.Console.Out.WriteLine($"Ping received {tick}");
                        break;
                    } else {
                        System.Console.Out.WriteLine($"Message received {msg.ContentType} {msg.Subject} {msg.Body}");
                    }
                } finally {
                    await this._Receiver.CompleteMessageAsync(msg);
                }
            }
            await Task.Delay(1000);
        }
        System.Console.Out.WriteLine($"Sending pong {_SenderQueueName} Tick:{tick}");
        await this._Sender.SendMessageAsync(
            new ServiceBusMessage(tick) {
                ContentType = "text/plain",
                Subject = "pong"
            }
            );
        while (true) {
            System.Console.Out.WriteLine($"Wait for pang  {_ReceiverQueueName}");
            var msg = await this._Receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            if (msg is not null) {
                try {
                    if (msg.ContentType == "text/plain" && msg.Subject == "pang") {
                        tick = msg.Body.ToString();
                        System.Console.Out.WriteLine($"Pang received {tick}");
                        break;
                    } else {
                        System.Console.Out.WriteLine($"Message received {msg.ContentType} {msg.Subject} {msg.Body}");
                    }
                } finally {
                    await this._Receiver.CompleteMessageAsync(msg);
                }
            }
            await Task.Delay(1000);
        }
    }

    public async Task Send(Message message) {
        if (this._Sender is null) {
            throw new Exception("Sender is null");
        }

        await this._Sender.SendMessageAsync(
            new ServiceBusMessage(message.Body) {
                ContentType = message.ContentType,
                Subject = message.Subject
            }
            );

    }
    public async Task<Message?> Receive(TimeSpan? timeout = default) {
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }
        var receivedMessage = await this._Receiver.ReceiveMessageAsync(timeout.GetValueOrDefault(TimeSpan.FromSeconds(10)));
        if (receivedMessage is null) {
            return null;
        } else {
            var result = new Message(receivedMessage.Subject, receivedMessage.ContentType, receivedMessage.Body);
            await this._Receiver.CompleteMessageAsync(receivedMessage);
            return result;
        }
    }


    public async ValueTask DisposeAsync() {
        if (this._Receiver is not null) {
            await this._Receiver.DisposeAsync();
            this._Receiver = null;
        }
        if (this._Sender is not null) {
            await this._Sender.DisposeAsync();
            this._Sender = null;
        }
        if (this._Client is not null) {
            await this._Client.DisposeAsync();
            this._Client = null;
        }
    }

}