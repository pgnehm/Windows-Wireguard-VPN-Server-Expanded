using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using SharpConfig;
using WireGuardAPI;
using WireGuardAPI.Commands;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    public abstract class ConfigurationBase : ObservableObject
    {
        #region Constructor

        protected ConfigurationBase()
        {
            foreach (ConfigurationProperty property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => typeof(ConfigurationProperty).IsAssignableFrom(p.PropertyType))
                .Select(p => p.GetValue(this) as ConfigurationProperty)
                .OrderBy(p => p?.Index))
            {
                Properties.Add(property);
            }

            foreach (ConfigurationPropertyAction action in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => typeof(ConfigurationPropertyAction).IsAssignableFrom(p.PropertyType))
                .Select(p => p.GetValue(this) as ConfigurationPropertyAction))
            {
                TopLevelActions.Add(action);
            }
        }

        #endregion

        #region Public (abstract) methods

        public ConfigurationBase Load(Configuration configuration)
        {
            if (configuration.FirstOrDefault(s => s.Name == "Interface") is { } section)
            {
                foreach (Setting setting in section)
                {
                    if (Properties.FirstOrDefault(p => p.PersistentPropertyName == setting.Name) is { } property)
                    {
                        property.Value = IsSensitiveProperty(property)
                            ? DpapiSecretProtector.Unprotect(setting.StringValue)
                            : setting.StringValue;
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// The serialized representation of this configuration, not targeted to a particular config type, but intended to hold all properties
        /// </summary>
        public Configuration ToConfiguration()
        {
            string sectionName = "Interface";

            var configuration = new Configuration();
            configuration[sectionName].PreComment = NameProperty.Value ?? string.Empty;
            foreach (ConfigurationProperty property in Properties)
            {
                configuration[sectionName][property.PersistentPropertyName].RawValue =
                    IsSensitiveProperty(property) ? DpapiSecretProtector.Protect(property.Value) : property.Value;
            }

            return configuration;
        }

        /// <summary>
        /// The serialized representation of this configuration, targeted to <see cref="TTarget"/> config file.
        /// </summary>
        public Configuration ToConfiguration<TTarget>() where TTarget : ConfigurationBase
        {
            string sectionName = this is TTarget ? "Interface" : "Peer";

            var configuration = new Configuration();
            configuration[sectionName].PreComment = NameProperty.Value;
            foreach (ConfigurationProperty property in Properties.Where(p => p.TargetTypes.Contains(typeof(TTarget)) && string.IsNullOrEmpty(p.Value) == false))
            {
                configuration[sectionName][property.PersistentPropertyName].StringValue = property.Value;
            }

            return configuration;
        }

        #endregion

        #region Protected methods

        protected void SortProperties()
        {
            Properties.Sort((a, b) => a.Index - b.Index);
        }

        private static bool IsSensitiveProperty(ConfigurationProperty property)
        {
            return string.Equals(property.PersistentPropertyName, "PrivateKey", StringComparison.Ordinal)
                || string.Equals(property.PersistentPropertyName, "PresharedKey", StringComparison.Ordinal);
        }

        #endregion

        #region Public properties

        public ConfigurationProperty NameProperty => _nameProperty ??= new ConfigurationProperty(this)
        {
            Index = 0,
            PersistentPropertyName = "Name",
            Name = nameof(NameProperty),
            Description = "A friendly name shown inside this app. It is only for your reference and does not affect the VPN connection.",
            Validation = new EmptyStringValidation(Resources.EmptyClientNameError)
        };
        private ConfigurationProperty _nameProperty;

        public ConfigurationProperty PrivateKeyProperty => _privateKeyProperty ??= new ConfigurationProperty(this)
        {
            PersistentPropertyName = "PrivateKey",
            Name = nameof(PrivateKeyProperty),
            Description = "The secret identity for this server or client. Generate this for a new setup. Do not share it. Only keep an existing value when restoring a backup or migrating an existing VPN.",
            IsReadOnly = true,
            Action = new ConfigurationPropertyAction(this)
            {
                Name = $"{nameof(PrivateKeyProperty)}{nameof(ConfigurationProperty.Action)}",
                Action = (conf, prop) =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    prop.Value = new WireGuardExe().ExecuteCommand(new GeneratePrivateKeyCommand());
                    Mouse.OverrideCursor = null;
                }
            },
            Validation = new EmptyStringValidation(Resources.KeyValidationError)
        };
        private ConfigurationProperty _privateKeyProperty;

        public ConfigurationProperty PublicKeyProperty => _publicKeyProperty ??= new ConfigurationProperty(this)
        {
            PersistentPropertyName = "PublicKey",
            Name = nameof(PublicKeyProperty),
            Description = "The public identity created from the private key. This can be shared with peers. Generate it after the private key, or keep it when restoring an existing configuration.",
            IsReadOnly = true,
            Action = new ConfigurationPropertyAction(this)
            {
                Name = $"{nameof(PublicKeyProperty)}{nameof(ConfigurationProperty.Action)}",
                Action = (conf, prop) =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    prop.Value = new WireGuardExe().ExecuteCommand(new GeneratePublicKeyCommand(conf.PrivateKeyProperty.Value));
                    Mouse.OverrideCursor = null;
                },
                DependentProperty = PrivateKeyProperty,
                DependencySatisfiedFunc = prop => string.IsNullOrEmpty(prop.Value) == false
            },
            Validation = new EmptyStringValidation(Resources.KeyValidationError)
        };
        private ConfigurationProperty _publicKeyProperty;

        public ConfigurationProperty PresharedKeyProperty => _presharedKeyProperty ??= new ConfigurationProperty(this)
        {
            PersistentPropertyName = "PresharedKey",
            Name = nameof(PresharedKeyProperty),
            Description = "An extra shared secret between the server and clients. Generate this for a new setup. Keep an existing value when old clients already use it or when restoring from backup.",
            IsReadOnly = true,
            // Action is different on Server and Client, so it should be implemented there
            Validation = new EmptyStringValidation(Resources.KeyValidationError)
        };
        private ConfigurationProperty _presharedKeyProperty;

        public ConfigurationProperty AddressProperty => _addressProperty ??= new ConfigurationProperty(this)
        {
            PersistentPropertyName = "Address",
            Name = nameof(AddressProperty),
            // DefaultValue and Validation should be set by child class
        };
        private ConfigurationProperty _addressProperty;

        public List<ConfigurationProperty> Properties { get; } = new List<ConfigurationProperty>();

        public IEnumerable<ConfigurationProperty> UiProperties => Properties.Where(p => p.IsHidden == false);

        public List<ConfigurationPropertyAction> TopLevelActions { get; } = new List<ConfigurationPropertyAction>();

        #endregion
    }

    public static class ConfigurationBaseExtensions
    {
        public static TConfig Load<TConfig>(this TConfig @this, Configuration configuration) where TConfig : ConfigurationBase
        {
            return @this.Load(configuration) as TConfig;
        }
    }
}
