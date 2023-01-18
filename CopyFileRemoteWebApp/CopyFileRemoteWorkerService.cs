namespace CopyFileRemote.WebApp;

public class CopyFileRemoteWorkerService:BackgroundService{
    private readonly ILogger<CopyFileRemoteWorkerService> _logger;
    private readonly IHubContext<CopyFileRemoteHub, ICopyFileRemoteHubReceiver> _hubContext;

    public CopyFileRemoteWorkerService(
        ILogger<CopyFileRemoteWorkerService> logger, 
        IHubContext<CopyFileRemoteHub, ICopyFileRemoteHubReceiver> hubContext){
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken){
        while (!stoppingToken.IsCancellationRequested){
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            // await _hubContext.Clients.All.ReceiveMessage("Worker", "Hello from worker");
            await Task.Delay(1000, stoppingToken);
        }
    }
}