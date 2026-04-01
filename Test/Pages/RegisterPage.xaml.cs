using Test.ViewModels;

namespace Test.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        NavigationPage.SetHasNavigationBar(this, false);
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((RegisterViewModel)BindingContext).ClearForm();
    }

}