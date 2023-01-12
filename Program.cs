namespace CopyFileRemote;

public class Program {
    private const string SubjectGoodbye = "Goodbye";
    private const string SubjectSubjectMoreQ = "More?";
    private const string SubjectContinue = "Continue";

    [System.STAThread()]
    public static async Task<int> Main(string[] args) {
        var appSettings = GetAppSettings(args);
        if (appSettings.ServiceType == ServiceType.Unknown) {
            System.Console.Error.WriteLine("ServiceType is not configured and cannot be determined.");
            return -1;
        }
        if (appSettings.ServiceType == ServiceType.ServiceBusQueue) {
            if (string.IsNullOrEmpty(appSettings.ServiceBusConnectionString)
                && string.IsNullOrEmpty(appSettings.ServiceBusNamespace)
            ) {
                System.Console.Error.WriteLine("ServiceBusConnectionString nor ServiceBusNamespace is not configured.");
                return -1;
            }
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

        if (appSettings.ServiceType == ServiceType.ServiceBusQueue) {
            System.Console.Out.WriteLine("Creating the Service Bus Administration Client object");
            var adminClient = createServiceBusAdministrationClient(appSettings);
            if (adminClient is null) {
                System.Console.Error.WriteLine("ServiceBusNamespace or ServiceBusConnectionString is not configured.");
                return (-1, null);
            }
            var transportService = (ServiceBusQueueTransportService?)CreateTransportService(appSettings);
            if (transportService is null) {
                System.Console.Error.WriteLine($"Service type '{appSettings.ServiceType}' is not supported.");
                return (-1, null);
            }
            await transportService.Setup(adminClient);
            return (0, transportService);
        }
        if (appSettings.ServiceType == ServiceType.ServiceBusTopic) {
            System.Console.Out.WriteLine("Creating the Service Bus Administration Client object");
            var adminClient = createServiceBusAdministrationClient(appSettings);
            if (adminClient is null) {
                System.Console.Error.WriteLine("ServiceBusNamespace or ServiceBusConnectionString is not configured.");
                return (-1, null);
            }
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
        inputPath = System.IO.Path.GetFullPath(inputPath);
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

            await transportService.Send(new TransportMessage(nameof(ListFileHash), "application/json", System.BinaryData.FromObjectAsJson(listFileHashSender)));
            foreach (var r in listFileHashSender.LstFileHash) {
                System.Console.WriteLine($"S:{r.RelativeName} - {r.Hash}");
            }
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
                    if (msg.Subject == SubjectGoodbye) {
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
                        System.Console.Out.WriteLine($"Skip {relativeName}");
                        continue;
                    }
                    System.Console.Out.WriteLine(relativeName);
                    var content = await System.IO.File.ReadAllBytesAsync(inputPath + relativeName);
                    
                    var fileContent = new FileContent(relativeName, Convert.ToBase64String(content));
                    size += fileContent.ContentBase64.Length + relativeName.Length +256;
                    if (size>262144){
                        if (putFileContent.LstFileContent.Count==0){
                            System.Console.WriteLine($"File is too big {relativeName}");
                        } else {
                            queueRelativeName.Enqueue(relativeName);
                        }
                    } else {
                        putFileContent.LstFileContent.Add(fileContent);
                    }
                    if (size > 10 * 1024) {
                        await transportService.Send(new TransportMessage(nameof(PutFileContent), "application/json", System.BinaryData.FromObjectAsJson(putFileContent)));
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
                            if (msg.Subject == SubjectGoodbye) {
                                return 0;
                            }
                            if (msg.Subject == SubjectContinue) {
                                break;
                            }
                        }
                    }
                }
                if (putFileContent.LstFileContent.Count > 0) {
                    await transportService.Send(new TransportMessage(nameof(PutFileContent), "application/json", System.BinaryData.FromObjectAsJson(putFileContent)));
                }
                await transportService.Send(new TransportMessage(SubjectSubjectMoreQ, "application/json", System.BinaryData.FromString(SubjectSubjectMoreQ)));
            }
        } finally {
            await transportService.Send(new TransportMessage(SubjectGoodbye, "application/json", System.BinaryData.FromString(SubjectGoodbye)));
            while ((await transportService.Receive(TimeSpan.FromSeconds(2))) is not null) { }
        }
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
                if (msg.Subject == nameof(ListFileHash)) {
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
            await transportService.Send(new TransportMessage(nameof(GetFileContent), "application/json", System.BinaryData.FromObjectAsJson(getFileContent)));

            while (true) {
                var msg = await transportService.Receive();
                if (msg is null) {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.NextInt64() % 500));
                    continue;
                }
                if (msg.Subject == nameof(PutFileContent)) {
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
                    await transportService.Send(new TransportMessage(SubjectContinue, "application/json", System.BinaryData.FromString(SubjectContinue)));
                    continue;
                }

                if (msg.Subject == SubjectGoodbye) {
                    break;
                }


                if (msg.Subject == SubjectSubjectMoreQ) {
                    break;
                }
            }
        } finally {
            await transportService.Send(new TransportMessage(SubjectGoodbye, "application/json", System.BinaryData.FromString(SubjectGoodbye)));
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
