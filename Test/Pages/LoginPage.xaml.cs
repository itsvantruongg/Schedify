using Test.ViewModels;

namespace Test.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((LoginViewModel)BindingContext).ClearForm();
    }
}