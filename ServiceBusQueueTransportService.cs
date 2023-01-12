namespace CopyFileRemote;

public class ServiceBusQueueTransportService : BaseTransportService, IAsyncDisposable {
    private readonly string _QueueNameMeOther;
    private readonly string _QueueNameOtherMe;
    private readonly string? _ServiceBusConnectionString;
    private readonly string? _ServiceBusNamespace;
    private readonly TokenCredential? _TokenCredential;
    private readonly ServiceBusClientOptions? _ServiceBusClientOptions;
    private ServiceBusClient? _Client;
    private ServiceBusSender? _Sender;
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
    public override Task OpenForLocalReadFromFS() {
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
    public override Task OpenForLocalWriteToFS() {
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

    public override async Task Ping() {
        if (this._Sender is null) {
            throw new Exception("Sender is null");
        }
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }
        await base.Ping();
    }

#if no

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

#endif
    public override async Task WaitForPing() {
        if (this._Sender is null) {
            throw new Exception("Sender is null");
        }
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }
        await base.WaitForPing();
    }
#if no
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
#endif

    public override  async Task Send(TransportMessage message) {
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

    public override  async Task<TransportMessage?> Receive(TimeSpan? timeout = default) {
        if (this._Receiver is null) {
            throw new Exception("Receiver is null");
        }
        var receivedMessage = await this._Receiver.ReceiveMessageAsync(timeout.GetValueOrDefault(TimeSpan.FromSeconds(10)));
        if (receivedMessage is null) {
            return null;
        } else {
            var result = new TransportMessage(receivedMessage.Subject, receivedMessage.ContentType, receivedMessage.Body);
            await this._Receiver.CompleteMessageAsync(receivedMessage);
            return result;
        }
    }

    public override  async ValueTask DisposeAsync() {
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