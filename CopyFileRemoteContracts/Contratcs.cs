namespace CopyFileRemote.Contracts;

/*
public record TransportEnvelope(
    string Sender,
    string Receiver,
    TransportMessage Message
);
public record TransportMessage(
    string Method,
    System.BinaryData Body
);
*/

public interface ICopyFileRemoteServiceSender {
    // Task Hello(TransportEnvelope<Hello> hello);
    // Task Goodbye(TransportEnvelope<Goodbye> goodbye);
    // Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash);
    // Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent);
    // Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent);
    // Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile);
}

public interface ICopyFileRemoteServiceReceiver {
    // Task Hello(TransportEnvelope<Hello> hello);
    // Task Goodbye(TransportEnvelope<Goodbye> goodbye);
    // Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash);
    // Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent);
    // Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent);
    // Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile);

    Task<ListFileHash> OnListFileHash();
    Task<GetFileContent> OnGetFileContent();
    Task<PutFileContent> OnPutFileContent();
    Task<DeleteFile> OnDeleteFile();
}

public record Hello(
    string Message
);

public record Goodbye(
    string Message
);

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

public record DeleteFile(
    List<string> LstRelativeName
);


public record TransportEnvelopeQueue(
    string Sender,
    string Receiver,
    TransportMessageQueue Message
);
public record TransportMessageQueue(
    string Subject,
    string ContentType,
    System.BinaryData Body
);

public record TransportEnvelope(
    string Sender,
    string Receiver
);

public record TransportEnvelope<T>(
    string Sender,
    string Receiver,
    T Message
) : TransportEnvelope(Sender, Receiver);

public interface ICopyFileRemoteHubSender {
    Task Hello(TransportEnvelope<Hello> hello);
    Task Goodbye(TransportEnvelope<Goodbye> goodbye);
    Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash);
    Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent);
    Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent);
    Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile);      
}
public interface ICopyFileRemoteHubReceiver {
    Task Hello(TransportEnvelope<Hello> hello);
    Task Goodbye(TransportEnvelope<Goodbye> goodbye);
    Task ListFileHash(TransportEnvelope<ListFileHash> listFileHash);
    Task GetFileContent(TransportEnvelope<GetFileContent> getFileContent);
    Task PutFileContent(TransportEnvelope<PutFileContent> putFileContent);
    Task DeleteFile(TransportEnvelope<DeleteFile> deleteFile);  
}