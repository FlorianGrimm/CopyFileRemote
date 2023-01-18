using System.Threading.Channels;

namespace CopyFileRemote.Client;

public interface ISyncFileRemoteService {
    string MeName { get; }
    ICopyFileRemoteHubReceiver GetSyncFileRemoteService(string otherName, ICopyFileRemoteHubSender senderService))
}

public interface ISyncFileRemoteReceiverService : ICopyFileRemoteHubReceiver {
}

public class SyncFileRemoteService : ISyncFileRemoteService {
    private ConcurrentDictionary<string, SyncFileRemoteReceiverService> _SyncFileRemoteService = new ConcurrentDictionary<string, SyncFileRemoteReceiverService>();
    private readonly string _MeName;
    private readonly ILocalFileSystemService _LocalFileSystemService;

    public string MeName => this._MeName;

    //public ILocalFileSystemService LocalFileSystemService => this._LocalFileSystemService;

    public SyncFileRemoteService(
        string meName,
        ILocalFileSystemService localFileSystemService
        ) {
        this._MeName = meName;
        this._LocalFileSystemService = localFileSystemService;
    }

    public ICopyFileRemoteHubReceiver GetSyncFileRemoteService(string otherName, ICopyFileRemoteHubSender senderService) {
        while (true) {
            this._SyncFileRemoteService.TryGetValue(otherName, out var result);
            if (result is null) {
                result = new SyncFileRemoteReceiverService(
                    this._MeName,
                    otherName,
                    senderService,
                    this);
                if (this._SyncFileRemoteService.TryAdd(otherName, result)) {
                    return result;
                } else {
                    continue;
                }
            }
        }
    }

    // public async Task<ListFileHash> OnListFileHash() {
    //     var result = new ListFileHash(new());
    //     await Task.Delay(1);
    //     return result;
    // }

    // public async Task<DeleteFile> OnDeleteFile() {
    //     var result = new DeleteFile(new());
    //     await Task.Delay(1);
    //     return result;
    // }

    // public async Task<GetFileContent> OnGetFileContent(){
    //     var result = new GetFileContent(new());
    //     await Task.Delay(1);
    //     return result;
    // }

    // public async Task<PutFileContent> OnPutFileContent() {
    //     var result = new PutFileContent(new());
    //     await Task.Delay(1);
    //     return result;
    // }

    public async Task Sync(ISyncFileRemoteReceiverService otherService) {
        await Task.Delay(1);
        await otherService.Hello(new TransportEnvelope<Hello>(this._MeName, string.Empty, new Hello("Hello")));
        await otherService.ListFileHash(new TransportEnvelope<ListFileHash>(this._MeName, string.Empty, new ListFileHash(new())));
        var localListFileHash = await this._LocalFileSystemService.ListFileHash();
        // TODO ...
    }

    public async Task OnListFileHash(ListFileHash listFileHash) {
        await Task.Delay(1);
        //
        //var result=await this._LocalFileSystemService.GetFileContent(getFileContent.LstRelativeName);
    }

    public async Task GetFileContent(GetFileContent getFileContent) {
        await Task.Delay(1);

    }
    public async Task PutFileContent(PutFileContent putFileContent) {
        await Task.Delay(1);
    }
    public async Task DeleteFile(DeleteFile deleteFile) {
        await Task.Delay(1);
    }

    public Task<ListFileHash> LocalListFileHash() => this._LocalFileSystemService.ListFileHash();
    public Task<List<FileContent>> LocalGetFileContent(List<string> lstRelativeName) => this._LocalFileSystemService.GetFileContent(lstRelativeName);
    public Task<PutFileContent> LocalPutFileContent(List<FileContent> lstFileContent) => this._LocalFileSystemService.PutFileContent(lstFileContent);
    public Task<DeleteFile> LocalDeleteFile(List<string> lstRelativeName) => this._LocalFileSystemService.DeleteFile(lstRelativeName);
}
public class SyncFileRemoteReceiverService : ISyncFileRemoteReceiverService {
    private readonly string _MeName;
    private readonly string _OtherName;
    private readonly ICopyFileRemoteHubSender _Sender;
    private readonly SyncFileRemoteService _Owner;
    private readonly Channel<TransportEnvelope> _Channel;
    private readonly ChannelWriter<TransportEnvelope> _ChannelWriter;

    public SyncFileRemoteReceiverService(
        string meName,
        string otherName,
        ICopyFileRemoteHubSender sender,
        SyncFileRemoteService owner) {
        this._MeName = meName;
        this._OtherName = otherName;
        this._Sender = sender;
        this._Owner = owner;
        // this._Queue = new ConcurrentQueue<TransportEnvelope>();
        this._Channel = System.Threading.Channels.Channel.CreateUnbounded<TransportEnvelope>();
        this._ChannelWriter = this._Channel.Writer;
    }

    public async Task Sync() {
        await this._Sender.Hello(new TransportEnvelope<Hello>(this._MeName, this._OtherName, new Hello("Hello")));
        var localListFileHash = await this._Owner.LocalListFileHash();
        await this._Sender.ListFileHash(new TransportEnvelope<ListFileHash>(this._MeName, this._OtherName, localListFileHash));
        var remoteListFileHash = new Contracts.ListFileHash(new());
        while (await this._Channel.Reader.WaitToReadAsync()) {
            while (this._Channel.Reader.TryRead(out var envelope)) {
                switch (envelope) {
                    case TransportEnvelope<ListFileHash> listFileHash:
                        //await this._Owner.OnListFileHash(listFileHash);
                        remoteListFileHash = listFileHash.Message;
                        break;
                    case TransportEnvelope<GetFileContent> getFileContent:
                        //await this._Owner.GetFileContent(getFileContent);
                        var lstFileContent = await this._Owner.LocalGetFileContent(getFileContent.Message.LstRelativeName);
                        var putFileContent = new PutFileContent(lstFileContent);
                        await this._Sender.PutFileContent(new TransportEnvelope<PutFileContent>(this._MeName, this._OtherName, putFileContent));
                        break;
                    case TransportEnvelope<PutFileContent> putFileContent:
                        break;                }
                    case TransportEnvelope<DeleteFile> deleteFile:
                        break;
            }
        }
        await this._Sender.Goodbye(new TransportEnvelope<Goodbye>(this._MeName, this._OtherName, new Goodbye("Goodbye")));
    }

    public async Task Hello(TransportEnvelope<Hello> hello) {
        if (hello.Message.Message == "Hello") {
            await this._Sender.Hello(new TransportEnvelope<Hello>(this._MeName, this._OtherName, new Hello("Welcome!")));
        }
        await this._ChannelWriter.WriteAsync(hello);
    }
    public async Task Goodbye(TransportEnvelope<Goodbye> goodbye) {
        if (goodbye.Message.Message == "Goodbye") {
            await this._Sender.Goodbye(new TransportEnvelope<Goodbye>(this._MeName, this._OtherName, new Goodbye("See you!")));
        }
        await this._ChannelWriter.WriteAsync(goodbye);
    }

    public async Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash) {
        await this._ChannelWriter.WriteAsync(listFileHash);
        //await this._Owner.OnListFileHash(listFileHash.Message);
    }
    public async Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent) {
        await this._Owner.GetFileContent(getFileContent.Message);
    }
    public async Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent) {
        await this._Owner.PutFileContent(putFileContent.Message);
    }
    public async Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile) {
        await this._Owner.DeleteFile(deleteFile.Message);
    }
}