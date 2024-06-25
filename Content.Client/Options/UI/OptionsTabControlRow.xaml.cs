using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Options.UI;

/// <summary>
/// Control used on all tabs of the in-game options menu,
/// contains the "save" and "reset" buttons and controls the entire logic.
/// </summary>
/// <remarks>
/// <para>
/// Basic operation is simple: options tabs put this control at the bottom of the tab,
/// they bind UI controls to it with calls such as <see cref="AddOptionCheckBox"/>,
/// then they call <see cref="Initialize"/>. The rest is all handled by the control.
/// </para>
/// <para>
/// Individual options are implementations of <see cref="BaseOption"/>. See the type for details.
/// Common implementations for building on top of CVars are already exist,
/// but tabs can define their own if they need to.
/// </para>
/// <para>
/// Generally, options are added via helper methods such as <see cref="AddOptionCheckBox"/>,
/// however it is totally possible to directly instantiate the backing types
/// and add them via <see cref="AddOption{T}"/>.
/// </para>
/// <para>
/// The options system is general purpose enough that <see cref="OptionsTabControlRow"/> does not, itself,
/// know what a CVar is. It does automatically save CVars to config when save is pressed, but otherwise CVar interaction
/// is handled by <see cref="BaseOption"/> implementations.
/// </para>
/// <para>
/// Behaviorally, the row has 3 control buttons: save, reset changed, and reset to default.
/// "Save" writes the configuration changes and saves the configuration.
/// "Reset changed" discards changes made in the menu and re-loads the saved settings.
/// "Reset to default" resets the settings on the menu to be the default, out-of-the-box values.
/// Note that "Reset to default" does not save immediately, the user must still press save manually.
/// </para>
/// <para>
/// The disabled state of the 3 buttons is updated dynamically based on the values of the options.
/// </para>
/// </remarks>
[GenerateTypedNameReferences]
public sealed partial class OptionsTabControlRow : Control
{
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private ValueList<BaseOption> _options;

    public OptionsTabControlRow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        ResetButton.StyleClasses.Add(StyleBase.ButtonOpenRight);
        ApplyButton.OnPressed += ApplyButtonPressed;
        ResetButton.OnPressed += ResetButtonPressed;
        DefaultButton.OnPressed += DefaultButtonPressed;
    }

    /// <summary>
    /// Add a new option to be tracked by the control.
    /// </summary>
    /// <param name="option">The option object that manages this object's logic</param>
    /// <typeparam name="T">
    /// The type of option being passed in. Necessary to allow the return type to match the parameter type
    /// for easy chaining.
    /// </typeparam>
    /// <returns>The same <paramref name="option"/> as passed in, for easy chaining.</returns>
    public T AddOption<T>(T option) where T : BaseOption
    {
        _options.Add(option);
        return option;
    }

    /// <summary>
    /// Add a checkbox option backed by a simple boolean CVar.
    /// </summary>
    /// <param name="cVar">The CVar represented by the checkbox.</param>
    /// <param name="checkBox">The UI control for the option.</param>
    /// <param name="invert">
    /// If true, the checkbox is inverted relative to the CVar: if the CVar is true, the checkbox will be unchecked.
    /// </param>
    /// <returns>The option instance backing the added option.</returns>
    /// <seealso cref="OptionCheckboxCVar"/>
    public OptionCheckboxCVar AddOptionCheckBox(CVarDef<bool> cVar, CheckBox checkBox, bool invert = false)
    {
        return AddOption(new OptionCheckboxCVar(this, _cfg, cVar, checkBox, invert));
    }

    /// <summary>
    /// Add a slider option, displayed in percent, backed by a simple float CVar.
    /// </summary>
    /// <param name="cVar">The CVar represented by the slider.</param>
    /// <param name="slider">The UI control for the option.</param>
    /// <param name="min">The minimum value the slider should allow. The default value represents "0%"</param>
    /// <param name="max">The maximum value the slider should allow. The default value represents "100%"</param>
    /// <param name="scale">
    /// Scale with which to multiply slider values when mapped to the backing CVar.
    /// For example, if a scale of 2 is set, a slider at 75% writes a value of 1.5 to the CVar.
    /// </param>
    /// <returns>The option instance backing the added option.</returns>
    /// <remarks>
    /// <para>
    /// Note that percentage values are represented as ratios in code, i.e. a value of 100% is "1".
    /// </para>
    /// </remarks>
    public OptionSliderFloatCVar AddOptionPercentSlider(
        CVarDef<float> cVar,
        OptionSlider slider,
        float min = 0,
        float max = 1,
        float scale = 1)
    {
        return AddOption(new OptionSliderFloatCVar(this, _cfg, cVar, slider, min, max, scale, FormatPercent));
    }

    /// <summary>
    /// Add a slider option, backed by a simple integer CVar.
    /// </summary>
    /// <param name="cVar">The CVar represented by the slider.</param>
    /// <param name="slider">The UI control for the option.</param>
    /// <param name="min">The minimum value the slider should allow.</param>
    /// <param name="max">The maximum value the slider should allow.</param>
    /// <param name="format">
    /// An optional delegate used to format the textual value display of the slider.
    /// If not provided, the default behavior is to directly format the integer value as text.
    /// </param>
    /// <returns>The option instance backing the added option.</returns>
    public OptionSliderIntCVar AddOptionSlider(
        CVarDef<int> cVar,
        OptionSlider slider,
        int min,
        int max,
        Func<OptionSliderIntCVar, int, string>? format = null)
    {
        return AddOption(new OptionSliderIntCVar(this, _cfg, cVar, slider, min, max, format ?? FormatInt));
    }

    /// <summary>
    /// Add a drop-down option, backed by a CVar.
    /// </summary>
    /// <param name="cVar">The CVar represented by the drop-down.</param>
    /// <param name="dropDown">The UI control for the option.</param>
    /// <param name="options">
    /// The set of options that will be shown in the drop-down. Items are ordered as provided.
    /// </param>
    /// <typeparam name="T">The type of the CVar being controlled.</typeparam>
    /// <returns>The option instance backing the added option.</returns>
    public OptionDropDownCVar<T> AddOptionDropDown<T>(
        CVarDef<T> cVar,
        OptionDropDown dropDown,
        IReadOnlyCollection<OptionDropDownCVar<T>.ValueOption> options)
        where T : notnull
    {
        return AddOption(new OptionDropDownCVar<T>(this, _cfg, cVar, dropDown, options));
    }

    /// <summary>
    /// Initializes the control row. This should be called after all options have been added.
    /// </summary>
    public void Initialize()
    {
        foreach (var option in _options)
        {
            option.LoadValue();
        }

        UpdateButtonState();
    }

    /// <summary>
    /// Re-loads options in the settings from backing values.
    /// Should be called when the options window is opened to make sure all values are up-to-date.
    /// </summary>
    public void ReloadValues()
    {
        Initialize();
    }

    /// <summary>
    /// Called by <see cref="BaseOption"/> to signal that an option's value changed through user interaction.
    /// </summary>
    /// <remarks>
    /// <see cref="BaseOption"/> implementations should not call this function directly,
    /// instead they should call <see cref="BaseOption.ValueChanged"/>.
    /// </remarks>
    public void ValueChanged()
    {
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        var anyModified = _options.Any(option => option.IsModified());
        var anyModifiedFromDefault = _options.Any(option => option.IsModifiedFromDefault());

        DefaultButton.Disabled = !anyModifiedFromDefault;
        ApplyButton.Disabled = !anyModified;
        ResetButton.Disabled = !anyModified;
    }

    private void ApplyButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        foreach (var option in _options)
        {
            if (option.IsModified())
                option.SaveValue();
        }

        _cfg.SaveToFile();
        UpdateButtonState();
    }

    private void ResetButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        foreach (var option in _options)
        {
            option.LoadValue();
        }

        UpdateButtonState();
    }

    private void DefaultButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        foreach (var option in _options)
        {
            option.ResetToDefault();
        }

        UpdateButtonState();
    }

    private string FormatPercent(OptionSliderFloatCVar slider, float value)
    {
        return _loc.GetString("ui-options-value-percent", ("value", value));
    }

    private static string FormatInt(OptionSliderIntCVar slider, int value)
    {
        return value.ToString();
    }
}

/// <summary>
/// Base class of a single "option" for <see cref="OptionsTabControlRow"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this class handle loading values from backing storage or defaults,
/// handling UI controls, and saving. The main <see cref="OptionsTabControlRow"/> does not know what a CVar is.
/// </para>
/// <para>
/// <see cref="BaseOptionCVar{TValue}"/> is a derived class that makes it easier to work with options
/// backed by a single CVar.
/// </para>
/// </remarks>
/// <param name="controller">The control row that owns this option.</param>
/// <seealso cref="OptionsTabControlRow"/>
public abstract class BaseOption(OptionsTabControlRow controller)
{
    /// <summary>
    /// Should be called by derived implementations to indicate that their value changed, due to user interaction.
    /// </summary>
    protected virtual void ValueChanged()
    {
        controller.ValueChanged();
    }

    /// <summary>
    /// Loads the value represented by this option from its backing store, into the UI state.
    /// </summary>
    public abstract void LoadValue();

    /// <summary>
    /// Saves the value in the UI state to the backing store.
    /// </summary>
    public abstract void SaveValue();

    /// <summary>
    /// Resets the UI state to that of the factory-default value. This should not write to the backing store.
    /// </summary>
    public abstract void ResetToDefault();

    /// <summary>
    /// Called to check if this option's UI value is different from the backing store value.
    /// </summary>
    /// <returns>If true, the UI value is different and was modified by the user.</returns>
    public abstract bool IsModified();

    /// <summary>
    /// Called to check if this option's UI value is different from the backing store's default value.
    /// </summary>
    /// <returns>If true, the UI value is different.</returns>
    public abstract bool IsModifiedFromDefault();
}

/// <summary>
/// Derived class of <see cref="BaseOption"/> intended for making mappings to simple CVars easier.
/// </summary>
/// <typeparam name="TValue">The type of the CVar.</typeparam>
/// <seealso cref="OptionsTabControlRow"/>
public abstract class BaseOptionCVar<TValue> : BaseOption
    where TValue : notnull
{
    /// <summary>
    /// Raised immediately when the UI value of this option is changed by the user, even before saving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This can be used to update parts of the options UI based on the state of a checkbox.
    /// </para>
    /// </remarks>
    public event Action<TValue>? ImmediateValueChanged;

    private readonly IConfigurationManager _cfg;
    private readonly CVarDef<TValue> _cVar;

    /// <summary>
    /// Sets and gets the actual CVar value to/from the frontend UI state or control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the simplest case, this function should set a UI control's state to represent the CVar,
    /// and inversely conver the UI control's state to the CVar value. For simple controls like a checkbox or slider,
    /// this just means passing through their value property.
    /// </para>
    /// </remarks>
    protected abstract TValue Value { get; set; }

    protected BaseOptionCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<TValue> cVar)
        : base(controller)
    {
        _cfg = cfg;
        _cVar = cVar;
    }

    public override void LoadValue()
    {
        Value = _cfg.GetCVar(_cVar);
    }

    public override void SaveValue()
    {
        _cfg.SetCVar(_cVar, Value);
    }

    public override void ResetToDefault()
    {
        Value = _cVar.DefaultValue;
    }

    public override bool IsModified()
    {
        return !IsValueEqual(Value, _cfg.GetCVar(_cVar));
    }

    public override bool IsModifiedFromDefault()
    {
        return !IsValueEqual(Value, _cVar.DefaultValue);
    }

    protected virtual bool IsValueEqual(TValue a, TValue b)
    {
        // Use different logic for floats so there's some error margin.
        // This check is handled cleanly at compile-time by the JIT.
        if (typeof(TValue) == typeof(float))
            return MathHelper.CloseToPercent((float) (object) a, (float) (object) b);

        return EqualityComparer<TValue>.Default.Equals(a, b);
    }

    protected override void ValueChanged()
    {
        base.ValueChanged();

        ImmediateValueChanged?.Invoke(Value);
    }
}

/// <summary>
/// Implementation of a CVar option that simply corresponds with a <see cref="CheckBox"/>.
/// </summary>
/// <remarks>
/// <para>
/// Generally, you should just call <c>AddOption</c> methods on <see cref="OptionsTabControlRow"/>
/// instead of instantiating this type directly.
/// </para>
/// </remarks>
/// <seealso cref="OptionsTabControlRow"/>
public sealed class OptionCheckboxCVar : BaseOptionCVar<bool>
{
    private readonly CheckBox _checkBox;
    private readonly bool _invert;

    protected override bool Value
    {
        get => _checkBox.Pressed ^ _invert;
        set => _checkBox.Pressed = value ^ _invert;
    }

    /// <summary>
    /// Creates a new instance of this type.
    /// </summary>
    /// <param name="controller">The control row that owns this option.</param>
    /// <param name="cfg">The configuration manager to get and set values from.</param>
    /// <param name="cVar">The CVar that is being controlled by this option.</param>
    /// <param name="checkBox">The UI control for the option.</param>
    /// <param name="invert">
    /// If true, the checkbox is inverted relative to the CVar: if the CVar is true, the checkbox will be unchecked.
    /// </param>
    /// <remarks>
    /// <para>
    /// It is generally more convenient to call overloads on <see cref="OptionsTabControlRow"/>
    /// such as <see cref="OptionsTabControlRow.AddOptionCheckBox"/> instead of instantiating this type directly.
    /// </para>
    /// </remarks>
    public OptionCheckboxCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<bool> cVar,
        CheckBox checkBox,
        bool invert)
        : base(controller, cfg, cVar)
    {
        _checkBox = checkBox;
        _invert = invert;
        checkBox.OnToggled += _ =>
        {
            ValueChanged();
        };
    }
}

/// <summary>
/// Implementation of a CVar option that simply corresponds with a floating-point <see cref="OptionSlider"/>.
/// </summary>
/// <seealso cref="OptionsTabControlRow"/>
public sealed class OptionSliderFloatCVar : BaseOptionCVar<float>
{
    /// <summary>
    /// Scale with which to multiply slider values when mapped to the backing CVar.
    /// </summary>
    /// <remarks>
    /// For example, if a scale of 2 is set, a slider at 75% writes a value of 1.5 to the CVar.
    /// </remarks>
    public float Scale { get; }

    private readonly OptionSlider _slider;
    private readonly Func<OptionSliderFloatCVar, float, string> _format;

    protected override float Value
    {
        get => _slider.Slider.Value * Scale;
        set
        {
            _slider.Slider.Value = value / Scale;
            UpdateLabelValue();
        }
    }

    /// <summary>
    /// Creates a new instance of this type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is generally more convenient to call overloads on <see cref="OptionsTabControlRow"/>
    /// such as <see cref="OptionsTabControlRow.AddOptionPercentSlider"/> instead of instantiating this type directly.
    /// </para>
    /// </remarks>
    /// <param name="controller">The control row that owns this option.</param>
    /// <param name="cfg">The configuration manager to get and set values from.</param>
    /// <param name="cVar">The CVar that is being controlled by this option.</param>
    /// <param name="slider">The UI control for the option.</param>
    /// <param name="minValue">The minimum value the slider should allow.</param>
    /// <param name="maxValue">The maximum value the slider should allow.</param>
    /// <param name="scale">
    /// Scale with which to multiply slider values when mapped to the backing CVar. See <see cref="Scale"/>.
    /// </param>
    /// <param name="format">Function that will be called to format the value display next to the slider.</param>
    public OptionSliderFloatCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<float> cVar,
        OptionSlider slider,
        float minValue,
        float maxValue,
        float scale,
        Func<OptionSliderFloatCVar, float, string> format) : base(controller, cfg, cVar)
    {
        Scale = scale;
        _slider = slider;
        _format = format;

        slider.Slider.MinValue = minValue;
        slider.Slider.MaxValue = maxValue;

        slider.Slider.OnValueChanged += _ =>
        {
            ValueChanged();
            UpdateLabelValue();
        };
    }

    private void UpdateLabelValue()
    {
        _slider.ValueLabel.Text = _format(this, _slider.Slider.Value);
    }
}

/// <summary>
/// Implementation of a CVar option that simply corresponds with an integer <see cref="OptionSlider"/>.
/// </summary>
/// <seealso cref="OptionsTabControlRow"/>
public sealed class OptionSliderIntCVar : BaseOptionCVar<int>
{
    private readonly OptionSlider _slider;
    private readonly Func<OptionSliderIntCVar, int, string> _format;

    protected override int Value
    {
        get => (int) _slider.Slider.Value;
        set
        {
            _slider.Slider.Value = value;
            UpdateLabelValue();
        }
    }

    /// <summary>
    /// Creates a new instance of this type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is generally more convenient to call overloads on <see cref="OptionsTabControlRow"/>
    /// such as <see cref="OptionsTabControlRow.AddOptionPercentSlider"/> instead of instantiating this type directly.
    /// </para>
    /// </remarks>
    /// <param name="controller">The control row that owns this option.</param>
    /// <param name="cfg">The configuration manager to get and set values from.</param>
    /// <param name="cVar">The CVar that is being controlled by this option.</param>
    /// <param name="slider">The UI control for the option.</param>
    /// <param name="minValue">The minimum value the slider should allow.</param>
    /// <param name="maxValue">The maximum value the slider should allow.</param>
    /// <param name="format">Function that will be called to format the value display next to the slider.</param>
    public OptionSliderIntCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<int> cVar,
        OptionSlider slider,
        int minValue,
        int maxValue,
        Func<OptionSliderIntCVar, int, string> format) : base(controller, cfg, cVar)
    {
        _slider = slider;
        _format = format;

        slider.Slider.MinValue = minValue;
        slider.Slider.MaxValue = maxValue;
        slider.Slider.Rounded = true;

        slider.Slider.OnValueChanged += _ =>
        {
            ValueChanged();
            UpdateLabelValue();
        };
    }

    private void UpdateLabelValue()
    {
        _slider.ValueLabel.Text = _format(this, (int) _slider.Slider.Value);
    }
}

/// <summary>
/// Implementation of a CVar option via a drop-down.
/// </summary>
/// <seealso cref="OptionsTabControlRow"/>
public sealed class OptionDropDownCVar<T> : BaseOptionCVar<T> where T : notnull
{
    private readonly OptionDropDown _dropDown;
    private readonly ItemEntry[] _entries;

    protected override T Value
    {
        get => (T) _dropDown.Button.SelectedMetadata!;
        set => _dropDown.Button.SelectId(FindValueId(value));
    }

    /// <summary>
    /// Creates a new instance of this type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is generally more convenient to call overloads on <see cref="OptionsTabControlRow"/>
    /// such as <see cref="OptionsTabControlRow.AddOptionDropDown{T}"/> instead of instantiating this type directly.
    /// </para>
    /// </remarks>
    /// <param name="controller">The control row that owns this option.</param>
    /// <param name="cfg">The configuration manager to get and set values from.</param>
    /// <param name="cVar">The CVar that is being controlled by this option.</param>
    /// <param name="dropDown">The UI control for the option.</param>
    /// <param name="options">The list of options shown to the user.</param>
    public OptionDropDownCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<T> cVar,
        OptionDropDown dropDown,
        IReadOnlyCollection<ValueOption> options) : base(controller, cfg, cVar)
    {
        if (options.Count == 0)
            throw new ArgumentException("Need at least one option!");

        _dropDown = dropDown;
        _entries = new ItemEntry[options.Count];

        var button = dropDown.Button;
        var i = 0;
        foreach (var option in options)
        {
            _entries[i] = new ItemEntry
            {
                Key = option.Key,
            };

            button.AddItem(option.Label, i);
            button.SetItemMetadata(button.GetIdx(i), option.Key);
            i += 1;
        }

        dropDown.Button.OnItemSelected += args =>
        {
            dropDown.Button.SelectId(args.Id);
            ValueChanged();
        };
    }

    private int FindValueId(T value)
    {
        for (var i = 0; i < _entries.Length; i++)
        {
            if (IsValueEqual(_entries[i].Key, value))
                return i;
        }

        // This will just default select the first entry or whatever.
        return 0;
    }

    /// <summary>
    /// A single option for a drop-down.
    /// </summary>
    /// <param name="key">The value that this option has. This is what will be written to the CVar if selected.</param>
    /// <param name="label">The visual text shown to the user for the option.</param>
    /// <seealso cref="OptionDropDownCVar{T}"/>
    /// <seealso cref="OptionsTabControlRow.AddOptionDropDown{T}"/>
    public sealed class ValueOption(T key, string label)
    {
        /// <summary>
        /// The value that this option has. This is what will be written to the CVar if selected.
        /// </summary>
        public readonly T Key = key;

        /// <summary>
        /// The visual text shown to the user for the option.
        /// </summary>
        public readonly string Label = label;
    }

    private struct ItemEntry
    {
        public T Key;
    }
}
