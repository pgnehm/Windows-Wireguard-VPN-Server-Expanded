using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace WireGuardServerForWindows.Models
{
    public class ClientConfigurationList : ObservableObject
    {
        public ClientConfigurationList()
        {
            List.CollectionChanged += List_CollectionChanged;
        }

        public ObservableCollection<ClientConfiguration> List { get; } = new ObservableCollection<ClientConfiguration>();

        public ClientConfiguration SelectedClient
        {
            get => _selectedClient;
            set => Set(nameof(SelectedClient), ref _selectedClient, value);
        }
        private ClientConfiguration _selectedClient;

        public bool HasClients => List.Count > 0;

        public ICommand AddClientConfigurationCommand => _addClientConfigurationCommand ??= new RelayCommand(() =>
        {
            using (new WaitCursor(dispatcherPriority: DispatcherPriority.Render, restoreCursorToNull: true))
            {
                AddClientWithDefaults();
            }
        });
        private RelayCommand _addClientConfigurationCommand;

        public ClientConfiguration AddClientWithDefaults()
        {
            var clientConfiguration = new ClientConfiguration(this);
            clientConfiguration.InitializeNewClientDefaults();
            List.Add(clientConfiguration);
            SelectedClient = clientConfiguration;
            return clientConfiguration;
        }

        private void List_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (List.Count == 0)
            {
                SelectedClient = null;
            }
            else if (SelectedClient == null || List.Contains(SelectedClient) == false)
            {
                SelectedClient = List[0];
            }

            RaisePropertyChanged(nameof(HasClients));
        }
    }
}
