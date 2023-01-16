namespace CopyFileRemote;

public interface ITransportService : IAsyncDisposable {
    Task OpenForLocalReadFromFS();
    Task OpenForLocalWriteToFS();

    Task Ping();
    Task WaitForPing();

    Task Send(TransportMessage message);
    Task<TransportMessage?> Receive(TimeSpan? timeout = default);
}
