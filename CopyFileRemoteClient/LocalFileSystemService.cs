namespace CopyFileRemote.Client;

public interface ILocalFileSystemService {
    Task<ListFileHash> ListFileHash();
    Task<List<FileContent>> GetFileContent(List<string> lstRelativeName);
    Task<PutFileContent> PutFileContent(List<FileContent> lstFileContent);
    Task<DeleteFile> DeleteFile(List<string> lstRelativeName);
}

public class LocalFileSystemService : ILocalFileSystemService {
    public LocalFileSystemService(string rootPath)
    {
    }

    public Task<ListFileHash> ListFileHash() {
        var result = new ListFileHash(new());
        return Task.FromResult(result);
    }

    public Task<DeleteFile> DeleteFile(List<string> lstRelativeName){
        var result = new DeleteFile(new());
        return Task.FromResult(result);
    }

    public Task<List<FileContent>> GetFileContent(List<string> lstRelativeName){
        var result = new List<FileContent>();
        return Task.FromResult(result);
    }

    public Task<PutFileContent> PutFileContent(List<FileContent> lstFileContent){
        var result = new PutFileContent(new());
        return Task.FromResult(result);
    }
}
