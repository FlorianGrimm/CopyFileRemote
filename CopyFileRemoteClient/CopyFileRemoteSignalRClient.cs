namespace CopyFileRemote.Client;

public class CopyFileRemoteSignalRClient
    : IAsyncDisposable
    , ICopyFileRemoteSender {
    private readonly string _Url;
    private readonly SyncFileRemoteService _SyncFileRemoteService;
    private HubConnection? _Connection;

    public CopyFileRemoteSignalRClient(string url, SyncFileRemoteService syncFileRemoteService) {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        this._Url = url + "/CopyFileRemoteWS";
        this._SyncFileRemoteService = syncFileRemoteService;
    }
    public async Task Open() {
        // TODO accessTokenProvider
        Func<Task<string?>>? accessTokenProvider = null;
        var connection = new HubConnectionBuilder()
                .WithUrl(this._Url,(options)=>{
                    options.AccessTokenProvider =accessTokenProvider;
                })
                .WithAutomaticReconnect()
                .Build();
        // connection.Reconnected += async (connectionId) => {
        //     await Task.Delay(Random.Shared.Next(0, 5) * 1000);
        //     await connection.StartAsync();
        // };
        // connection.Closed += async (error) => {
        //     await Task.Delay(Random.Shared.Next(0, 5) * 1000);
        //     await connection.StartAsync();
        // };
        connection.On<TransportEnvelope<Hello>>(
            nameof(ICopyFileRemoteHubReceiver.Hello),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).Hello(message).Ignore();
                });
        connection.On<TransportEnvelope<Goodbye>>(
            nameof(ICopyFileRemoteHubReceiver.Goodbye),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).Goodbye(message).Ignore();
                });
        connection.On<TransportEnvelope<ListFileHash>>(
            nameof(ICopyFileRemoteHubReceiver.ListFileHash),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).ListFileHash(message).Ignore();
                });
        connection.On<TransportEnvelope<GetFileContent>>(
            nameof(ICopyFileRemoteHubReceiver.GetFileContent),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).GetFileContent(message).Ignore();
                });
        connection.On<TransportEnvelope<PutFileContent>>(
            nameof(ICopyFileRemoteHubReceiver.PutFileContent),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).PutFileContent(message).Ignore();
                });
        connection.On<TransportEnvelope<DeleteFile>>(
            nameof(ICopyFileRemoteHubReceiver.DeleteFile),
                (message) => {
                    this._SyncFileRemoteService.GetSyncFileRemoteService(message.Sender, this).DeleteFile(message).Ignore();
                });
        await connection.StartAsync();
        this._Connection = connection;
    }

    public async ValueTask DisposeAsync() {
        if ((this._Connection is not null) && (this._Connection.State == HubConnectionState.Connected)) {
            await this._Connection.DisposeAsync();
            this._Connection = null;
        }
    }

    public async Task Hello(TransportEnvelope<Hello> hello) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.Hello),
            hello
        );
    }
    public async Task Goodbye(TransportEnvelope<Goodbye> goodbye) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.Goodbye),
            goodbye
            );
    }
    public async Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.ListFileHash),
            listFileHash
            );
    }
    public async Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.GetFileContent),
            getFileContent
            );
    }
    public async Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.PutFileContent),
            putFileContent
            );
    }
    public async Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile) {
        if (this._Connection == null)
            throw new InvalidOperationException("Connection is not open");
        await this._Connection.InvokeAsync(
            nameof(ICopyFileRemoteHubReceiver.DeleteFile),
            deleteFile
            );
    }
}
