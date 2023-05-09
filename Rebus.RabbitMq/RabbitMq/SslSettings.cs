using System.Net.Security;
using System.Security.Authentication;

namespace Rebus.RabbitMq;

/// <summary>
/// Represents ssl settings to be used in rabbitmq SSL connection, will be mapped to rabbitmq SslOptions
/// </summary>
public class SslSettings
{
    /// <summary>
    /// Constructs an SslSettings 
    /// </summary>
    public SslSettings(bool enabled, string serverName, string certificatePath = "", string certPassphrase = "", SslProtocols version = SslProtocols.None, SslPolicyErrors acceptablePolicyErrors = SslPolicyErrors.None)
    {
        Enabled = enabled;
        ServerName = serverName;
        CertPath = certificatePath;
        CertPassphrase = certPassphrase;

        Version = version;
        AcceptablePolicyErrors = acceptablePolicyErrors;
    }

    /// <summary>
    /// specify if Ssl should indeed be used     
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Retrieve or set server's Canonical Name.
    /// This MUST match the CN on the Server Certificate else the SSL connection will fail.
    /// </summary>
    public string ServerName { get; set; }

    /// <summary>
    /// Retrieve or set the set of ssl policy errors that are deemed acceptable.
    /// </summary>
    public SslPolicyErrors AcceptablePolicyErrors { get; set; }

    /// <summary>
    /// Retrieve or set the path to client certificate.
    /// </summary>
    public string CertPassphrase { get; set; }

    /// <summary>
    /// Retrieve or set the path to client certificate.
    /// </summary>
    public string CertPath { get; set; }

    /// <summary>
    /// Retrieve or set the Ssl protocol version.
    /// </summary>
    public SslProtocols Version { get; set; }
}