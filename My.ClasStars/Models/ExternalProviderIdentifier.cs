using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LogonServiceRequestTypes.Enums;
using SharedTypes.ExternalIntegration;

namespace My.ClasStars.Models;

public class ExternalProviderIdentifier : INotifyPropertyChanged
{
    private string _externalSourceID;
    public string ExternalSourceID
    {
        get => _externalSourceID;
        set
        {
            SetField(ref _externalSourceID, value);
            _externalDataProvider = null;
            OnPropertyChanged(nameof(ExternalDataProvider));
        }
    }

    private ExternalDataProvider? _externalDataProvider = null;
    public ExternalDataProvider ExternalDataProvider
    {
        get
        {
            _externalDataProvider ??= ExternalIdCorrelationHelper.GetExternalDataProviderFromCorrelatedID(ExternalSourceID);
            return _externalDataProvider.Value;
        }
    }

    private string _externalDataKey;

    public string ExternalDataKey
    {
        get
        {
            _externalDataKey ??= ExternalSourceID?.Substring(1);
            return _externalDataKey;
        }
    }

    public static string GetExternalDataID(ExternalDataProvider provider, string key)
    {
        return provider.ToString().Substring(0, 1) + key;
    }
    public void SetExternalDataID(ExternalDataProvider provider, string key)
    {
        ExternalSourceID = GetExternalDataID(provider, key);
    }

    public bool IsMatchedExternalProviderData(ExternalDataProvider provider, string dataId)
    {
        return provider == ExternalDataProvider && dataId == ExternalDataKey;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

