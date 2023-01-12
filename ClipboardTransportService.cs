namespace CopyFileRemote;

public class ClipboardTransportService : ITransportService {
    private readonly string _QueueNameMeOther;
    private readonly string _QueueNameOtherMe;
    public ClipboardTransportService(
        string queueNameMeOther, string queueNameOtherMe
    ) {
        if (string.IsNullOrEmpty(queueNameMeOther)) { throw new ArgumentNullException(nameof(queueNameMeOther)); }
        if (string.IsNullOrEmpty(queueNameOtherMe)) { throw new ArgumentNullException(nameof(queueNameOtherMe)); }
        this._QueueNameMeOther = queueNameMeOther;
        this._QueueNameOtherMe = queueNameOtherMe;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task OpenForLocalReadFromFS() => Task.CompletedTask;
    public Task OpenForLocalWriteToFS() => Task.CompletedTask;
    public Task Ping() {
        this.Send(new TransportMessage("Ping", "text/plain", System.BinaryData.FromString("Ping")));
        //System.Windows.Forms.Clipboard.SetText(this._QueueNameMeOther);
        return Task.CompletedTask; 
    }
    public Task WaitForPing() => throw new NotImplementedException();
    public Task<TransportMessage?> Receive(TimeSpan? timeout = null) => throw new NotImplementedException();
    public Task Send(TransportMessage message) => throw new NotImplementedException();
}
