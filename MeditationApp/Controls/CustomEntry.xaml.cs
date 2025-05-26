using System.Windows.Input;

namespace MeditationApp.Controls;

public partial class CustomEntry : ContentView
{
    #region Bindable Properties
    
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(CustomEntry), string.Empty, BindingMode.TwoWay);
    
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CustomEntry), string.Empty);
    
    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(CustomEntry), Colors.Gray);
    
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(CustomEntry), Colors.Black);
    
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(CustomEntry), 16.0);
    
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(CustomEntry), string.Empty);
    
    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(CustomEntry), false,
            propertyChanged: OnIsPasswordPropertyChanged);
    
    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(CustomEntry), Keyboard.Default);
    
    public static readonly BindableProperty ReturnTypeProperty =
        BindableProperty.Create(nameof(ReturnType), typeof(ReturnType), typeof(CustomEntry), ReturnType.Default);
    
    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(nameof(MaxLength), typeof(int), typeof(CustomEntry), int.MaxValue);
    
    public static readonly BindableProperty LeftIconProperty =
        BindableProperty.Create(nameof(LeftIcon), typeof(string), typeof(CustomEntry), string.Empty, 
            propertyChanged: OnIconPropertyChanged);
    
    public static readonly BindableProperty RightIconProperty =
        BindableProperty.Create(nameof(RightIcon), typeof(string), typeof(CustomEntry), string.Empty,
            propertyChanged: OnIconPropertyChanged);
    
    public static readonly BindableProperty IconColorProperty =
        BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(CustomEntry), Colors.Gray);
    
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(CustomEntry), Color.FromArgb("#E0E0E0"));
    
    public static readonly BindableProperty FocusedBorderColorProperty =
        BindableProperty.Create(nameof(FocusedBorderColor), typeof(Color), typeof(CustomEntry), Color.FromArgb("#71BEF1"));
    
    public static readonly BindableProperty CustomBackgroundColorProperty =
        BindableProperty.Create(nameof(CustomBackgroundColor), typeof(Color), typeof(CustomEntry), Color.FromArgb("#F8F9FA"));
    
    public static readonly BindableProperty IsFieldFocusedProperty =
        BindableProperty.Create(nameof(IsFieldFocused), typeof(bool), typeof(CustomEntry), false);
    
    public static readonly BindableProperty HasLeftIconProperty =
        BindableProperty.Create(nameof(HasLeftIcon), typeof(bool), typeof(CustomEntry), false);
    
    public static readonly BindableProperty HasRightIconProperty =
        BindableProperty.Create(nameof(HasRightIcon), typeof(bool), typeof(CustomEntry), false);
    
    public static readonly BindableProperty RightIconCommandProperty =
        BindableProperty.Create(nameof(RightIconCommand), typeof(ICommand), typeof(CustomEntry));
    
    public static readonly BindableProperty IsSpellCheckEnabledProperty =
        BindableProperty.Create(nameof(IsSpellCheckEnabled), typeof(bool), typeof(CustomEntry), true);
    
    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(CustomEntry), false,
            propertyChanged: OnHasErrorPropertyChanged);
    
    public static readonly BindableProperty ErrorBorderColorProperty =
        BindableProperty.Create(nameof(ErrorBorderColor), typeof(Color), typeof(CustomEntry), Color.FromArgb("#FF6B46C1"));

    #endregion
    
    #region Properties
    
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }
    
    public Color PlaceholderColor
    {
        get => (Color)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }
    
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
    
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    
    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }
    
    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }
    
    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }
    
    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }
    
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }
    
    public string LeftIcon
    {
        get => (string)GetValue(LeftIconProperty);
        set => SetValue(LeftIconProperty, value);
    }
    
    public string RightIcon
    {
        get => (string)GetValue(RightIconProperty);
        set => SetValue(RightIconProperty, value);
    }
    
    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }
    
    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }
    
    public Color FocusedBorderColor
    {
        get => (Color)GetValue(FocusedBorderColorProperty);
        set => SetValue(FocusedBorderColorProperty, value);
    }
    
    public Color CustomBackgroundColor
    {
        get => (Color)GetValue(CustomBackgroundColorProperty);
        set => SetValue(CustomBackgroundColorProperty, value);
    }
    
    public bool IsFieldFocused
    {
        get => (bool)GetValue(IsFieldFocusedProperty);
        set => SetValue(IsFieldFocusedProperty, value);
    }
    
    public bool HasLeftIcon
    {
        get => (bool)GetValue(HasLeftIconProperty);
        set => SetValue(HasLeftIconProperty, value);
    }
    
    public bool HasRightIcon
    {
        get => (bool)GetValue(HasRightIconProperty);
        set => SetValue(HasRightIconProperty, value);
    }
    
    public ICommand RightIconCommand
    {
        get => (ICommand)GetValue(RightIconCommandProperty);
        set => SetValue(RightIconCommandProperty, value);
    }
    
    public bool IsSpellCheckEnabled
    {
        get => (bool)GetValue(IsSpellCheckEnabledProperty);
        set => SetValue(IsSpellCheckEnabledProperty, value);
    }
    
    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }
    
    public Color ErrorBorderColor
    {
        get => (Color)GetValue(ErrorBorderColorProperty);
        set => SetValue(ErrorBorderColorProperty, value);
    }
    
    #endregion
    
    #region Events
    
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public new event EventHandler<FocusEventArgs>? Focused;
    public new event EventHandler<FocusEventArgs>? Unfocused;
    public event EventHandler? Completed;
    public event EventHandler? RightIconTapped;
    
    #endregion
    
    public CustomEntry()
    {
        InitializeComponent();
    }
    
    #region Event Handlers
    
    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        IsFieldFocused = true;
        Focused?.Invoke(this, e);
    }
    
    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        IsFieldFocused = false;
        Unfocused?.Invoke(this, e);
    }
    
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        Text = e.NewTextValue;
        TextChanged?.Invoke(this, e);
    }
    
    private void OnCompleted(object sender, EventArgs e)
    {
        Completed?.Invoke(this, e);
    }
    
    private void OnRightIconTapped(object sender, EventArgs e)
    {
        // If this is a password field and no custom command is set, toggle password visibility
        if (IsPassword && RightIconCommand == null)
        {
            IsPassword = !IsPassword;
            RightIcon = IsPassword ? "🙈" : "👁";
        }
        
        RightIconCommand?.Execute(null);
        RightIconTapped?.Invoke(this, e);
    }
    
    #endregion
    
    #region Property Changed Handlers
    
    private static void OnIconPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomEntry customEntry)
        {
            customEntry.HasLeftIcon = !string.IsNullOrEmpty(customEntry.LeftIcon);
            customEntry.HasRightIcon = !string.IsNullOrEmpty(customEntry.RightIcon);
        }
    }
    
    private static void OnIsPasswordPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomEntry customEntry)
        {
            // Disable spell check for password fields
            customEntry.IsSpellCheckEnabled = !(bool)newValue;
        }
    }

    private static void OnHasErrorPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomEntry customEntry)
        {
            // Optionally, you can add logic here to handle changes to the HasError property
        }
    }
    
    #endregion
    
    #region Public Methods
    
    public new void Focus()
    {
        EntryField?.Focus();
    }
    
    public new void Unfocus()
    {
        EntryField?.Unfocus();
    }
    
    #endregion
}
