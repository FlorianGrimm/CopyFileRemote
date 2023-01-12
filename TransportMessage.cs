namespace CopyFileRemote;

public record TransportMessage(
    string Subject,
    string ContentType,
    System.BinaryData Body
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
