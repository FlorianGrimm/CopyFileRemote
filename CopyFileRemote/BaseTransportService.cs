namespace CopyFileRemote;

public class BaseTransportService : ITransportService, IAsyncDisposable {
    protected string _SenderQueueName;
    protected string _ReceiverQueueName;
    public BaseTransportService() {
        this._SenderQueueName = "";
        this._ReceiverQueueName = "";
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public virtual Task OpenForLocalReadFromFS() => Task.CompletedTask;
    public virtual Task OpenForLocalWriteToFS() => Task.CompletedTask;
    public virtual Task<TransportMessage?> Receive(TimeSpan? timeout = null) => Task.FromResult<TransportMessage?>(null);
    public virtual Task Send(TransportMessage message) => Task.CompletedTask;

    public virtual async Task Ping() {
        System.Console.Out.WriteLine($"Sending ping {_SenderQueueName}");
        var tick = Random.Shared.NextInt64().ToString();
        await this.Send(new TransportMessage(
           Subject: "ping",
           ContentType: "text/plain",
           Body: System.BinaryData.FromString(tick)));

        while (true) {
            System.Console.Out.WriteLine($"Wait for pong  {_ReceiverQueueName}");
            var msg = await this.Receive(timeout: TimeSpan.FromSeconds(10));
            if (msg is not null) {
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
            }
            await Task.Delay(1000);
        }
        await this.Send(new TransportMessage(
            Subject: "pang",
            ContentType: "text/plain",
            Body: System.BinaryData.FromString(tick)));
    }
    public virtual async Task WaitForPing() {
        string tick = "";
        while (true) {
            System.Console.Out.WriteLine($"Wait for ping  {_ReceiverQueueName}");
            var msg = await this.Receive(TimeSpan.FromSeconds(10));
            if (msg is not null) {
                if (msg.ContentType == "text/plain" && msg.Subject == "ping") {
                    tick = msg.Body.ToString();
                    System.Console.Out.WriteLine($"Ping received {tick}");
                    break;
                } else {
                    System.Console.Out.WriteLine($"Message received {msg.ContentType} {msg.Subject} {msg.Body}");
                }
            }
            await Task.Delay(1000);
        }
        System.Console.Out.WriteLine($"Sending pong {_SenderQueueName} Tick:{tick}");

        await this.Send(new TransportMessage(
            Subject: "pong",
            ContentType: "text/plain",
            Body: System.BinaryData.FromString(tick)));

        while (true) {
            System.Console.Out.WriteLine($"Wait for pang  {_ReceiverQueueName}");
            var msg = await this.Receive(TimeSpan.FromSeconds(10));
            if (msg is not null) {
                if (msg.ContentType == "text/plain" && msg.Subject == "pang") {
                    tick = msg.Body.ToString();
                    System.Console.Out.WriteLine($"Pang received {tick}");
                    break;
                } else {
                    System.Console.Out.WriteLine($"Message received {msg.ContentType} {msg.Subject} {msg.Body}");
                }
            }
            await Task.Delay(1000);
        }
    }
}
