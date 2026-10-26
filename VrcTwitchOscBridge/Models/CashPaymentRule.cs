using System.Collections.ObjectModel;
using System.Security.Cryptography;
using VrcTwitchOscBridge.Infrastructure;

namespace VrcTwitchOscBridge.Models;

public enum CashPaymentProvider
{
    StreamElements,
    Streamlabs,
    KoFi // Legacy saved-rule ID only; this provider is no longer supported.
}

public enum CashPaymentActionKind
{
    TriggerAction,
    AvatarScaling
}



public sealed class CashPaymentConnectionSettings : ObservableObject
{
    private bool streamElementsEnabled;
    private string streamElementsAccountId = string.Empty;
    private string streamElementsJwtToken = string.Empty;
    private bool streamlabsEnabled;
    private string streamlabsAccessToken = string.Empty;
    public bool StreamElementsEnabled
    {
        get => streamElementsEnabled;
        set
        {
            if (SetProperty(ref streamElementsEnabled, value))
            {
                RaisePropertyChanged(nameof(HasStreamElementsToken));
            }
        }
    }

    public string StreamElementsAccountId
    {
        get => streamElementsAccountId;
        set => SetProperty(ref streamElementsAccountId, value?.Trim() ?? string.Empty);
    }

    public string StreamElementsJwtToken
    {
        get => streamElementsJwtToken;
        set
        {
            if (SetProperty(ref streamElementsJwtToken, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(HasStreamElementsToken));
            }
        }
    }

    public bool HasStreamElementsToken => !string.IsNullOrWhiteSpace(StreamElementsJwtToken);

    public bool StreamlabsEnabled
    {
        get => streamlabsEnabled;
        set
        {
            if (SetProperty(ref streamlabsEnabled, value))
            {
                RaisePropertyChanged(nameof(HasStreamlabsToken));
            }
        }
    }

    public string StreamlabsAccessToken
    {
        get => streamlabsAccessToken;
        set
        {
            if (SetProperty(ref streamlabsAccessToken, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(HasStreamlabsToken));
            }
        }
    }

    public bool HasStreamlabsToken => !string.IsNullOrWhiteSpace(StreamlabsAccessToken);

}

public sealed class CashPaymentRule : ObservableObject
{
    private Guid id = Guid.NewGuid();
    private bool isEnabled = true;
    private string name = "New Cash Payment";
    private CashPaymentProvider provider;
    private decimal minimumAmount = 1m;
    private decimal maximumAmount;
    private string currencyCode = string.Empty;
    private string messageContains = string.Empty;
    private bool requireMessageKeyword;
    private int cooldownSeconds = 30;
    private CashPaymentActionKind actionKind = CashPaymentActionKind.TriggerAction;
    private TriggerRule triggerAction = CreateDefaultTriggerAction();
    private AvatarScaleRule scaleAction = CreateDefaultScaleAction();

    public CashPaymentRule()
    {
        WireNestedActions();
    }

    public Guid Id
    {
        get => id;
        set => SetProperty(ref id, value == Guid.Empty ? Guid.NewGuid() : value);
    }

    public bool IsEnabled
    {
        get => isEnabled && Provider != CashPaymentProvider.KoFi;
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public string Name
    {
        get => name;
        set
        {
            if (SetProperty(ref name, value ?? string.Empty))
            {
                TriggerAction.Name = name;
                ScaleAction.Name = name;
                RaisePropertyChanged(nameof(DisplayTitle));
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public CashPaymentProvider Provider
    {
        get => provider;
        set
        {
            var normalizedValue = Enum.IsDefined(value) ? value : CashPaymentProvider.StreamElements;
            if (SetProperty(ref provider, normalizedValue))
            {
                RaisePropertyChanged(nameof(IsEnabled));
                RaisePropertyChanged(nameof(ProviderDisplayName));
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public decimal MinimumAmount
    {
        get => minimumAmount;
        set
        {
            if (SetProperty(ref minimumAmount, Math.Max(0m, value)))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public decimal MaximumAmount
    {
        get => maximumAmount;
        set
        {
            if (SetProperty(ref maximumAmount, Math.Max(0m, value)))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public string CurrencyCode
    {
        get => currencyCode;
        set
        {
            var normalizedValue = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (SetProperty(ref currencyCode, normalizedValue))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public string MessageContains
    {
        get => messageContains;
        set
        {
            if (SetProperty(ref messageContains, value?.Trim() ?? string.Empty))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public bool RequireMessageKeyword
    {
        get => requireMessageKeyword;
        set
        {
            if (SetProperty(ref requireMessageKeyword, value))
            {
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public int CooldownSeconds
    {
        get => cooldownSeconds;
        set => SetProperty(ref cooldownSeconds, Math.Max(0, value));
    }

    public CashPaymentActionKind ActionKind
    {
        get => actionKind;
        set
        {
            var normalizedValue = Enum.IsDefined(value) ? value : CashPaymentActionKind.TriggerAction;
            if (SetProperty(ref actionKind, normalizedValue))
            {
                RaisePropertyChanged(nameof(UsesTriggerAction));
                RaisePropertyChanged(nameof(UsesAvatarScaling));
                RaisePropertyChanged(nameof(TriggerSummary));
            }
        }
    }

    public TriggerRule TriggerAction
    {
        get => triggerAction;
        set
        {
            var nextValue = value ?? CreateDefaultTriggerAction();
            if (ReferenceEquals(triggerAction, nextValue))
            {
                return;
            }

            UnwireTriggerAction(triggerAction);
            triggerAction = nextValue;
            ApplyTriggerActionDefaults(triggerAction, Name);
            WireTriggerAction(triggerAction);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(TriggerSummary));
        }
    }

    public AvatarScaleRule ScaleAction
    {
        get => scaleAction;
        set
        {
            var nextValue = value ?? CreateDefaultScaleAction();
            if (ReferenceEquals(scaleAction, nextValue))
            {
                return;
            }

            UnwireScaleAction(scaleAction);
            scaleAction = nextValue;
            ApplyScaleActionDefaults(scaleAction, Name);
            WireScaleAction(scaleAction);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(TriggerSummary));
        }
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Name) ? "Cash Payment" : Name.Trim();

    public string ProviderDisplayName => Provider switch
    {
        CashPaymentProvider.Streamlabs => "Streamlabs",
        CashPaymentProvider.KoFi => "Unsupported legacy provider",
        _ => "StreamElements"
    };

    public bool UsesTriggerAction => ActionKind == CashPaymentActionKind.TriggerAction;

    public bool UsesAvatarScaling => ActionKind == CashPaymentActionKind.AvatarScaling;

    public string TriggerSummary
    {
        get
        {
            var enabledText = IsEnabled ? "Enabled" : "Disabled";
            var rangeText = MaximumAmount > 0
                ? $"{MinimumAmount:0.##}-{MaximumAmount:0.##}"
                : $"{MinimumAmount:0.##}+";
            var currencyText = string.IsNullOrWhiteSpace(CurrencyCode) ? "any currency" : CurrencyCode;
            var actionText = UsesAvatarScaling ? "avatar scaling" : TriggerAction.ActionType.ToString();
            var keywordText = RequireMessageKeyword
                ? (string.IsNullOrWhiteSpace(MessageContains) ? " | keyword required" : $" | keyword: {MessageContains}")
                : string.Empty;
            return $"{enabledText} | {ProviderDisplayName} | {rangeText} {currencyText}{keywordText} | {actionText}";
        }
    }

    public static TriggerRule CreateDefaultTriggerAction()
    {
        var rule = new TriggerRule
        {
            Name = "New Cash Payment",
            TriggerType = TwitchTriggerType.Bits,
            ChannelPointRewardTitle = string.Empty,
            ChatCommandEnabled = false,
            ChatCommandText = string.Empty,
            MinimumAmount = 1,
            AmountScaledDurationEnabled = false,
            ActionType = OscActionType.AvatarParameter,
            ParameterName = "VRCEmote",
            ParameterType = OscParameterType.Int,
            ParameterValue = "1",
            ResetValue = "0",
            RangeMinimum = 0,
            RangeMaximum = 5,
            DurationSeconds = 10,
            CooldownSeconds = 30,
            SharedRewardHelpText = "Cash payment Set Trigger",
            SetTriggerActions = new ObservableCollection<SetTriggerAction>()
        };
        return rule;
    }

    public static AvatarScaleRule CreateDefaultScaleAction()
    {
        return new AvatarScaleRule
        {
            Name = "New Cash Payment Scale",
            TriggerType = AvatarScaleTriggerType.Bits,
            ScaleMode = AvatarScaleMode.SetHeight,
            TargetHeightMeters = 1.6,
            MinimumHeightMeters = 0.5,
            MaximumHeightMeters = 2.5,
            RelativeHeightMeters = 0.25,
            HeightMultiplier = 1.25,
            Preset = AvatarScalePreset.Normal,
            ActiveTimeSeconds = 0,
            RestoreMode = AvatarScaleRestoreMode.ConfiguredHeight,
            RestoreHeightMeters = 1.6,
            SetHeightTransitionSeconds = 0,
            RandomHeightTransitionSeconds = 0,
            RelativeHeightTransitionSeconds = 0,
            MultiplierTransitionSeconds = 0,
            PresetTransitionSeconds = 0,
            GlitchyRandomHeightTransitionSeconds = 0,
            SupporterGrowthTransitionSeconds = 0
        };
    }

    private void WireNestedActions()
    {
        ApplyTriggerActionDefaults(triggerAction, Name);
        ApplyScaleActionDefaults(scaleAction, Name);
        WireTriggerAction(triggerAction);
        WireScaleAction(scaleAction);
    }

    private void WireTriggerAction(TriggerRule rule) => rule.PropertyChanged += NestedActionChanged;

    private void UnwireTriggerAction(TriggerRule rule) => rule.PropertyChanged -= NestedActionChanged;

    private void WireScaleAction(AvatarScaleRule rule) => rule.PropertyChanged += NestedActionChanged;

    private void UnwireScaleAction(AvatarScaleRule rule) => rule.PropertyChanged -= NestedActionChanged;

    private void NestedActionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(TriggerSummary));
    }

    private static void ApplyTriggerActionDefaults(TriggerRule rule, string ownerName)
    {
        rule.TriggerType = TwitchTriggerType.Bits;
        rule.RewardSyncMode = TwitchRewardSyncMode.CreateOrManage;
        rule.ChatCommandEnabled = false;
        rule.ChatCommandText = string.Empty;
        rule.ChannelPointRewardId = string.Empty;
        rule.ChannelPointRewardTitle = string.Empty;
        rule.MinimumAmount = 1;
        rule.SharedRewardHelpText = string.IsNullOrWhiteSpace(rule.SharedRewardHelpText)
            ? "Cash payment Set Trigger"
            : rule.SharedRewardHelpText;
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            rule.Name = ownerName;
        }
    }

    private static void ApplyScaleActionDefaults(AvatarScaleRule rule, string ownerName)
    {
        rule.TriggerType = AvatarScaleTriggerType.Bits;
        rule.RewardId = string.Empty;
        rule.RewardTitle = string.Empty;
        rule.CommandText = string.Empty;
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            rule.Name = ownerName;
        }
    }
}
