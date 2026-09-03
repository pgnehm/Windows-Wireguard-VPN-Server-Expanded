using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using WireGuardAPI;
using WireGuardAPI.Commands;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    public class ServerConfiguration : ConfigurationBase
    {
        private static readonly string[] BooleanOptions = { bool.TrueString, bool.FalseString };

        #region Constructor

        public ServerConfiguration()
        {
            // Server properties
            PrivateKeyProperty.TargetTypes.Add(GetType());
            MtuProperty.TargetTypes.Add(GetType());
            MtuProperty.TargetTypes.Add(typeof(ClientConfiguration));
            ListenPortProperty.TargetTypes.Add(GetType());

            // Client properties
            PresharedKeyProperty.TargetTypes.Add(typeof(ClientConfiguration));
            PublicKeyProperty.TargetTypes.Add(typeof(ClientConfiguration));
            AllowedIpsProperty.TargetTypes.Add(typeof(ClientConfiguration));
            EndpointProperty.TargetTypes.Add(typeof(ClientConfiguration));

            // Set some properties that are unique to server
            NameProperty.DefaultValue = $"{Environment.MachineName} Wireguard Server";
            AddressProperty.DefaultValue = "10.253.0.0/24";
            AddressProperty.Index = 3;
            AddressProperty.Description = "The private network used inside the VPN. Keep this different from your home or office network. The default works for most people.";

            // Do custom validation on the Address (we want a CIDR notation)
            AddressProperty.Validation = new ConfigurationPropertyValidation
            {
                Validate = obj =>
                {
                    string result = default;

                    if (IPNetwork.TryParse(obj.Value, out _) == false)
                    {
                        result = Resources.NetworkAddressValidationError;
                    }
                    else // TryParse succeeded
                    {
                        // IPNetwork.TryParse recognizes single IP addresses as CIDR (with 8 mask).
                        // This is not good, because we want an explicit CIDR for the server.
                        // Therefore, if IPNetwork.TryParse succeeds, and IPAddress.TryParse also succeeds, we have a problem.
                        if (IPAddress.TryParse(obj.Value, out _))
                        {
                            // This is just a regular address. We want CIDR.
                            result = Resources.NetworkAddressValidationError;
                        }
                    }

                    return result;
                }
            };

            // The Server actually generates the pre-shared key
            PresharedKeyProperty.Action = new ConfigurationPropertyAction(this)
            {
                Name = $"{nameof(PresharedKeyProperty)}{nameof(ConfigurationProperty.Action)}",
                Action = (conf, prop) =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    prop.Value = new WireGuardExe().ExecuteCommand(new GeneratePresharedKeyCommand());
                    Mouse.OverrideCursor = null;
                }
            };

            EndpointProperty.Action = new ConfigurationPropertyAction(this)
            {
                Name = $"{nameof(EndpointProperty)}{nameof(ConfigurationProperty.Action)}",
                Description = Resources.EndpointPropertyActionDescription,
                Action = async (conf, prop) => await DetectPublicIpAddressAsync(force: true, showStatusDelay: true)
            };

            ListenPortProperty.PropertyChanged += (_, args) =>
            {
                EndpointProperty.Port = ListenPortProperty.Value;
            };

            // Resort after changing the index of AddressProperty
            SortProperties();
        }

        #endregion

        #region Public properties

        public ConfigurationProperty ListenPortProperty => _listenPortProperty ??= new ConfigurationProperty(this)
        {
            Index = 1,
            PersistentPropertyName = "ListenPort", Name = nameof(ListenPortProperty), DefaultValue = "51820",
            Description = "The UDP port this server listens on. Keep 51820 unless you also change your router port forward and re-export client profiles.",
            Validation = new ConfigurationPropertyValidation
            {
                Validate = obj =>
                {
                    string result = default;

                    if (int.TryParse(obj.Value, out int port))
                    {
                        if (port < 0 || port > 65535)
                        {
                            result = Resources.PortRangeValidationError;
                        }
                    }
                    else
                    {
                        result = Resources.PortValidationError;
                    }

                    return result;
                }
            }
        };
        private ConfigurationProperty _listenPortProperty;

        public ConfigurationProperty AllowedIpsProperty => _allowedIpsProperty ??= new ConfigurationProperty(this)
        {
            Index = 2,
            PersistentPropertyName = "AllowedIPs",
            Name = nameof(AllowedIpsProperty), Description = "What client traffic should go through this VPN. Use 0.0.0.0/0 to send all IPv4 internet traffic through the server.",
            DefaultValue = "0.0.0.0/0",
            Validation = new ConfigurationPropertyValidation
            {
                Validate = obj =>
                {
                    string result = default;

                    // Support CSV allowed IPs
                    foreach (string address in obj.Value.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()))
                    {
                        if (IPNetwork.TryParse(address, out _) == false)
                        {
                            result = Resources.NetworkAddressValidationError;
                            break;
                        }
                    }

                    return result;
                }
            }
        };
        private ConfigurationProperty _allowedIpsProperty;

        /// <summary>
        /// The MTU used by the WireGuard server interface and generated client configurations.
        /// A typical Ethernet path uses 1420 because WireGuard adds 80 bytes of overhead.
        /// </summary>
        public ConfigurationProperty MtuProperty => _mtuProperty ??= new ConfigurationProperty(this)
        {
            Index = 4,
            PersistentPropertyName = "MTU",
            Name = nameof(MtuProperty),
            Description = "The packet size used by the VPN. 1420 is safest. Try 1500 only after the VPN works and you can test websites, downloads, and video calls.",
            DefaultValue = "1420",
            Validation = new ConfigurationPropertyValidation
            {
                Validate = obj =>
                {
                    if (int.TryParse(obj.Value, out int mtu) && mtu >= 1280 && mtu <= 65535)
                    {
                        return null;
                    }

                    return Resources.MtuValidationError;
                }
            }
        };
        private ConfigurationProperty _mtuProperty;

        public ConfigurationProperty KillSwitchProperty => _killSwitchProperty ??= new ConfigurationProperty(this)
        {
            Index = 5,
            PersistentPropertyName = "KillSwitch",
            Name = nameof(KillSwitchProperty),
            Description = "When True, block VPN-client traffic if the protected tunnel path is not available. Leave False while first setting up if you want fewer moving parts.",
            DefaultValue = bool.FalseString,
            Options = BooleanOptions,
            Validation = BooleanPropertyValidation
        };
        private ConfigurationProperty _killSwitchProperty;

        public ConfigurationProperty DnsLeakProtectionProperty => _dnsLeakProtectionProperty ??= new ConfigurationProperty(this)
        {
            Index = 6,
            PersistentPropertyName = "DnsLeakProtection",
            Name = nameof(DnsLeakProtectionProperty),
            Description = "When True, generated client profiles must include DNS servers so clients do not silently use their local network DNS.",
            DefaultValue = bool.TrueString,
            Options = BooleanOptions,
            Validation = BooleanPropertyValidation
        };
        private ConfigurationProperty _dnsLeakProtectionProperty;

        public ConfigurationProperty DisableIpv6Property => _disableIpv6Property ??= new ConfigurationProperty(this)
        {
            Index = 7,
            PersistentPropertyName = "DisableIPv6",
            Name = nameof(DisableIpv6Property),
            Description = "When True, IPv6 is blocked for VPN clients because this server currently routes IPv4 only. Leave True unless IPv6 support is added and tested.",
            DefaultValue = bool.TrueString,
            Options = BooleanOptions,
            Validation = BooleanPropertyValidation
        };
        private ConfigurationProperty _disableIpv6Property;

        private static ConfigurationPropertyValidation BooleanPropertyValidation => new ConfigurationPropertyValidation
        {
            Validate = property => bool.TryParse(property.Value, out _)
                ? null
                : "Value must be True or False."
        };

        public EndpointConfigurationProperty EndpointProperty => _endpointProperty ??= new EndpointConfigurationProperty(this)
        {
            Index = 3,
            PersistentPropertyName = "Endpoint",
            Name = nameof(EndpointProperty),
            Description = "The public address clients use to reach this server. Use a dynamic DNS name if your home public IP changes.",
            DefaultValue = $":{ListenPortProperty.Value}",
            Validation = new ConfigurationPropertyValidation
            {
                Validate = obj =>
                {
                    string result = default;

                    if (string.IsNullOrEmpty(obj.Value))
                    {
                        result = Resources.EmptyEndpointValidation;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(EndpointProperty.Host) || string.IsNullOrEmpty(EndpointProperty.Port))
                        {
                            result = Resources.EmptyEndpointValidation;
                        }
                        else if (EndpointProperty.Port != ListenPortProperty.Value)
                        {
                            result = Resources.EndpointPortMismatch;
                        }
                        
                        // If we get here, we passed all validation.
                    }

                    return result;
                }
            }
        };
        private EndpointConfigurationProperty _endpointProperty;

        // Note: Although this property is configured on the server, it goes in the peer (client) section of the server's config,
        // which means it also has to be defined on the client, targeted to the server's config.
        // The client should return the server's value, and the server should not target this property to any config type.
        public ConfigurationProperty PersistentKeepaliveProperty => _persistentKeepaliveProperty ??= new ConfigurationProperty(this)
        {
            PersistentPropertyName = "PersistentKeepalive", // Don't really need this since it isn't saved from here
            Name = nameof(PersistentKeepaliveProperty),
            Description = "How often clients send a small keep-alive packet when idle. Use 0 to turn it off. Use 25 if clients are behind strict routers or cellular networks.",
            DefaultValue = 0.ToString(),
            Validation = new ConfigurationPropertyValidation
            {
                Validate = prop =>
                {
                    string result = default;

                    if (string.IsNullOrEmpty(prop.Value) || int.TryParse(prop.Value, out _) == false)
                    {
                        result = Resources.PersistentKeepaliveValidationError;
                    }

                    return result;
                }
            }
        };
        private ConfigurationProperty _persistentKeepaliveProperty;

        /// <summary>
        /// This is a calculated field that generates a Server IP address based on the current <see cref="ServerConfiguration.AddressProperty"/> property.
        /// Returns an empty string if the IP address cannot be generated for any reason.
        /// </summary>
        public string IpAddress
        {
            get
            {
                string result = string.Empty;
                
                try
                {
                    IPNetwork network = IPNetwork.Parse(AddressProperty.Value);
                    result = NetworkAddressUtilities.GetFirstServerAddress(network)?.ToString() ?? string.Empty;
                }
                catch
                {
                    // Should never come here, because we should only invoke this method if the AddressProperty has already passed validation.
                    // But just to be safe...
                }

                return result;
            }
        }

        public async Task DetectPublicIpAddressAsync(bool force, bool showStatusDelay)
        {
            if (!force && ShouldKeepExistingEndpointHost())
            {
                return;
            }

            EndpointProperty.Action.DependencySatisfiedFunc = _ => false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string ip = (await httpClient.GetStringAsync("https://api.ipify.org")).Trim();

                if (IPAddress.TryParse(ip, out _))
                {
                    EndpointProperty.Host = ip;
                    EndpointProperty.Action.Name = nameof(Resources.Updated);
                }
                else
                {
                    EndpointProperty.Action.Name = nameof(Resources.FailedToIdentify);
                }
            }
            catch
            {
                EndpointProperty.Action.Name = nameof(Resources.FailedToIdentify);
            }
            finally
            {
                Mouse.OverrideCursor = null;

                if (showStatusDelay)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                EndpointProperty.Action.Name = $"{nameof(EndpointProperty)}{nameof(ConfigurationProperty.Action)}";
                EndpointProperty.Action.Description = Resources.EndpointPropertyActionDescription;
                EndpointProperty.Action.DependencySatisfiedFunc = null;
            }
        }

        public bool TryGenerateMissingKeys(out string error)
        {
            error = null;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var wireGuardExe = new WireGuardExe();

                if (string.IsNullOrWhiteSpace(PrivateKeyProperty.Value))
                {
                    PrivateKeyProperty.Value = wireGuardExe.ExecuteCommand(new GeneratePrivateKeyCommand());
                }

                if (string.IsNullOrWhiteSpace(PublicKeyProperty.Value) && string.IsNullOrWhiteSpace(PrivateKeyProperty.Value) == false)
                {
                    PublicKeyProperty.Value = wireGuardExe.ExecuteCommand(new GeneratePublicKeyCommand(PrivateKeyProperty.Value));
                }

                if (string.IsNullOrWhiteSpace(PresharedKeyProperty.Value))
                {
                    PresharedKeyProperty.Value = wireGuardExe.ExecuteCommand(new GeneratePresharedKeyCommand());
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private bool ShouldKeepExistingEndpointHost()
        {
            string host = EndpointProperty.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            string normalizedHost = host.Trim('[', ']');
            return IPAddress.TryParse(normalizedHost, out _) == false;
        }

        #endregion
    }

    /// <summary>
    /// An extension of <see cref="ConfigurationProperty"/> that is specific to <see cref="ServerConfiguration.EndpointProperty"/>,
    /// containing additional methods for parsing and setting parts of the endpoint.
    /// </summary>
    public class EndpointConfigurationProperty : ConfigurationProperty
    {
        public EndpointConfigurationProperty(ConfigurationBase configuration, ConfigurationProperty dependentProperty = null)
            : base(configuration, dependentProperty)
        {
        }

        /// <summary>
        /// Provides access to the host portion of the value. Will be empty string if not present. Can be set without affecting the port.
        /// </summary>
        public string Host
        {
            get => string.Join(':', (Value ?? string.Empty).Split(':').SkipLast(1));
            set
            {
                if (string.IsNullOrEmpty(Value) == false)
                {
                    if (string.IsNullOrEmpty(Host) == false && string.IsNullOrEmpty(Port) == false && Port.EndsWith(']') == false)
                    {
                        // It already has IP:Port, so just replace the IP part
                        Value = $"{value}:{Port}";
                    }
                    else if (Value.StartsWith(':'))
                    {
                        // It has no IP, just :PORT, so add the IP
                        Value = $"{value}{Value}";
                    }
                    else if (Value.EndsWith(':'))
                    {
                        // It only has IP: and no port, so replace the IP
                        Value = $"{value}:";
                    }
                }
                else
                {
                    // It's an empty string. We can at least populate the IP.
                    Value = $"{value}:";
                }
            }
        }

        /// <summary>
        /// Provides access to the port portion of the value. Will be empty string if not present. Can be set without affecting the host.
        /// </summary>
        public string Port
        {
            get => (Value ?? string.Empty).Split(':').LastOrDefault();
            set
            {
                if (string.IsNullOrEmpty(Value) == false)
                {
                    if (string.IsNullOrEmpty(Host) == false && string.IsNullOrEmpty(Port) == false && Port.EndsWith(']') == false)
                    {
                        // It already has IP:Port, so just replace the Port part
                        Value = $"{Host}:{value}";
                    }
                    else if (Value.StartsWith(':'))
                    {
                        // It has no IP, just :PORT, so replace the port
                        Value = $":{value}";
                    }
                    else if (Value.EndsWith(':'))
                    {
                        // It only has IP: and no port, so add the port
                        Value = $"{Value}{value}";
                    }
                }
                else
                {
                    // It's an empty string. We can at least populate the port.
                    Value = $":{value}";
                }
            }
        }
    }
}
