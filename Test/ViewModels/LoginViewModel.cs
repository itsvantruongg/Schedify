using System.Windows.Input;
using Test.Services;

namespace Test.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _usernameError = string.Empty;
    private string _passwordError = string.Empty;
    private bool _isPassword = true;

    public string PasswordIcon => IsPassword  ? "eye.png" : "hidden.png";
    public bool HasUsernameError => !string.IsNullOrEmpty(_usernameError);
    public bool HasPasswordError => !string.IsNullOrEmpty(_passwordError);

    public bool IsPassword
    {
        get => _isPassword;
        set { _isPassword = value; OnPropertyChanged(); OnPropertyChanged(nameof(PasswordIcon)); }
    }
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }
    public string UsernameError
    {
        get => _usernameError;
        set { _usernameError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUsernameError)); }
    }
    public string PasswordError
    {
        get => _passwordError;
        set { _passwordError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPasswordError)); }
    }

    public ICommand LoginCommand { get; }
    public ICommand GoToRegisterCommand { get; }
    public ICommand TogglePasswordCommand { get; }

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new Command(async () => await LoginAsync(), () => IsNotBusy);
        GoToRegisterCommand = new Command(async () => await Shell.Current.GoToAsync("RegisterPage"));
        TogglePasswordCommand = new Command(() => IsPassword = !IsPassword);
    }

    private async Task LoginAsync()
    {
        UsernameError = string.IsNullOrWhiteSpace(Username)
            ? "Vui lòng nhập tên đăng nhập" : string.Empty;
        PasswordError = string.IsNullOrWhiteSpace(Password)
            ? "Vui lòng nhập mật khẩu" : string.Empty;

        if (HasUsernameError || HasPasswordError) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        var (success, message, data) = await _authService.LoginAsync(Username, Password);

        IsBusy = false;

        if (success && data != null)
        {
            // ✅ AuthService.LoginAsync đã lưu hết token + session_marker rồi
            // Không cần lưu lại ở đây nữa
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            ErrorMessage = message;
        }
    }

    public void ClearForm()
    {
        Username = string.Empty;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        UsernameError = string.Empty;
        PasswordError = string.Empty;
        IsPassword = true;
    }
}