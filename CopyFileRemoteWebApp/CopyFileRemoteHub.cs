namespace CopyFileRemote.WebApp;

public class CopyFileRemoteHub : Hub<ICopyFileRemoteHubReceiver>, ICopyFileRemoteHubSender{
    public CopyFileRemoteHub()
    {
    }

    public override async Task OnConnectedAsync() {
        await base.OnConnectedAsync();       
    }

     public override async Task OnDisconnectedAsync(Exception? exception) {
        /*
        await this.Clients.AllExcept(this.Context.ConnectionId).ReceiveMessage(
            "Server", 
            $"Client {this.Context.ConnectionId} disconnected");
        */
        
        await base.OnDisconnectedAsync(exception);
    }

    // public async Task SendMessage(string user, string message)
    //     => await Clients.All.SendAsync("ReceiveMessage", user, message);
    public async Task SendMessage(TransportEnvelope envelope){
        //await Clients.All.ReceiveMessage(user, message);
        //if (envelope.Receiver == this.Context.ConnectionId){
        if (!string.IsNullOrEmpty(envelope.Receiver)){
            await Clients.Client(envelope.Receiver).ReceiveMessage(envelope.Message);
            return;
        }
        await this.Clients.AllExcept(this.Context.ConnectionId).ReceiveMessage(envelope.Message);
    }
}
