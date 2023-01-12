namespace CopyFileRemote;

public record TransportMessage(
    string Subject,
    string ContentType,
    System.BinaryData Body
);
