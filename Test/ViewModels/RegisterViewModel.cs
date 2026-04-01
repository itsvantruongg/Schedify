using System.Windows.Input;
using Test.Services;
namespace Test.ViewModels;
public class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _username = string.Empty;
    private string _displayName = string.Empty; // ← thêm
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;

    private string _usernameError = string.Empty;
    private string _emailError = string.Empty;
    private string _passwordError = string.Empty;
    private string _confirmPasswordError = string.Empty;

    private bool _isPassword = true;
    private bool _isConfirmPassword = true;
    public string PasswordIcon => IsPassword ? "eye.png" : "hidden.png";
    public string ConfirmPasswordIcon => IsConfirmPassword ? "eye.png" : "hidden.png";

    public bool HasUsernameError => !string.IsNullOrEmpty(_usernameError);
    public bool HasEmailError => !string.IsNullOrEmpty(_emailError);
    public bool HasPasswordError => !string.IsNullOrEmpty(_passwordError);
    public bool HasConfirmPasswordError => !string.IsNullOrEmpty(_confirmPasswordError);

    public bool IsPassword
    {
        get => _isPassword;
        set
        {
            _isPassword = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PasswordIcon));
        }
    }
    public bool IsConfirmPassword
    {
        get => _isConfirmPassword;
        set
        {
            _isConfirmPassword = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfirmPasswordIcon));
        }
    }
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRegister)); }
    }
    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRegister)); }
    }
    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRegister)); }
    }
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRegister)); }
    }
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { _confirmPassword = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRegister)); }
    }
    public string UsernameError
    {
        get => _usernameError;
        set { _usernameError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUsernameError)); }
    }
    public string EmailError
    {
        get => _emailError;
        set { _emailError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEmailError)); }
    }
    public string PasswordError
    {
        get => _passwordError;
        set { _passwordError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPasswordError)); }
    }
    public string ConfirmPasswordError
    {
        get => _confirmPasswordError;
        set { _confirmPasswordError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasConfirmPasswordError)); }
    }

    public ICommand RegisterCommand { get; }
    public ICommand GoToLoginCommand { get; }
    public ICommand TogglePasswordCommand { get; }
    public ICommand ToggleConfirmPasswordCommand { get; }

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        RegisterCommand = new Command(async () => await RegisterAsync(), () => IsNotBusy);
        GoToLoginCommand = new Command(async () => await Shell.Current.Navigation.PopAsync(animated: true));
        TogglePasswordCommand = new Command(() =>
        {
            IsPassword = !IsPassword;
        });
        ToggleConfirmPasswordCommand = new Command (() =>
        {
            IsConfirmPassword = !IsConfirmPassword;
        });
    }

    private async Task RegisterAsync()
    {
        UsernameError = string.IsNullOrWhiteSpace(Username)
        ? "Vui lòng nhập tên đăng nhập" : string.Empty;

        EmailError = string.IsNullOrWhiteSpace(Email)
            ? "Vui lòng nhập email"
            : (!Email.Contains('@') ? "Email không hợp lệ" : string.Empty);

        PasswordError = string.IsNullOrWhiteSpace(Password)
            ? "Vui lòng nhập mật khẩu"
            : (Password.Length < 6 ? "Mật khẩu tối thiểu 6 ký tự" : string.Empty);

        ConfirmPasswordError = string.IsNullOrWhiteSpace(ConfirmPassword)
            ? "Vui lòng xác nhận mật khẩu"
            : (ConfirmPassword != Password ? "Mật khẩu xác nhận không khớp" : string.Empty);

        if (HasUsernameError || HasEmailError ||
            HasPasswordError || HasConfirmPasswordError) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        // ← truyền thêm DisplayName
        var (success, message, _) = await _authService.RegisterAsync(
            Username, DisplayName, Email, Password);

        IsBusy = false;

        if (success)
            await Shell.Current.GoToAsync("//LoginPage");
        else
            ErrorMessage = message;
    }
    public void ClearForm()
    {
        Username = string.Empty;
        DisplayName = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = string.Empty;
        UsernameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        IsPassword = true;
        IsConfirmPassword = true;
    }
    public bool CanRegister =>
    !string.IsNullOrWhiteSpace(Username) &&
    !string.IsNullOrWhiteSpace(Email) &&
    !string.IsNullOrWhiteSpace(Password) &&
    !string.IsNullOrWhiteSpace(ConfirmPassword);
}