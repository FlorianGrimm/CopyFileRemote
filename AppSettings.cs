namespace CopyFileRemote;

public class AppSettings {
    public ServiceType ServiceType { get; set; } = ServiceType.Unknown;
    public HostType HostType { get; set; } = HostType.Unknown;
    public string? HostMe { get; set; }
    public string? HostOther { get; set; }
    public string? Json { get; set; }
    public string? InputPath { get; set; }
    public string[] IncludeExtensions { get; set; } = new[] { ".sln", ".cs", ".csproj", ".ts", ".tsx", ".txt" };
    public string[] ExcludeDirectory { get; set; } = new[] { "node_modules", "bower_components", "obj", "bin", "packages" };
    public string? OutputPath { get; set; }
    public bool RecurseSubdirectories { get; set; } = true;
    public string? ServiceBusNamespace { get; set; }
    public string? ServiceBusConnectionString { get; set; }
    public string? ServiceBusQueueMeOther { get; set; }
    public string? ServiceBusQueueOtherMe { get; set; }
    public string GetServiceBusQueueMeOther() {
        return ServiceBusQueueMeOther ?? $"Queue-{HostMe}-{HostOther}";
    }
    public string GetServiceBusQueueOtherMe() {
        return ServiceBusQueueMeOther ?? $"Queue-{HostOther}-{HostMe}";
    }

    public string? ServiceBusTopic { get; set; }
    public string GetServiceBusTopic() {
        return ServiceBusTopic ?? $"{HostMe}-{HostOther}";
    }

    public ServiceBusClientOptions? ServiceBusClientOptions { get; set; }
    public DefaultAzureCredentialOptions? DefaultAzureCredentialOptions { get; set; }
    public ClientSecretOptions? ClientSecretOptions { get; set; }

    public AppSettings() {
        this.ServiceBusClientOptions = new ServiceBusClientOptions {
            TransportType = ServiceBusTransportType.AmqpWebSockets
        };
        this.ClientSecretOptions = new ClientSecretOptions();
        this.DefaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
    }
}

public enum HostType {
    Unknown,
    HostA,
    HostB,
}

public enum ServiceType {
    Unknown,
    ServiceBusQueue,
    ServiceBusTopic,
}


public class ClientSecretOptions {
    /// <summary>
    /// Gets the Azure Active Directory tenant (directory) Id of the service principal
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets the client (application) ID of the service principal
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Gets the client secret that was generated for the App Registration used to authenticate the client.
    /// </summary>
    public string? ClientSecret { get; set; }
}
