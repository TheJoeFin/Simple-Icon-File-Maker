using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Simple_Icon_File_Maker.Helpers;

namespace Simple_Icon_File_Maker.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private object? _lastParameterUsed;
    private Frame? _frame;

    public event NavigatedEventHandler? Navigated;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.m_window?.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => Frame != null && Frame.CanGoBack;

    public NavigationService(IPageService pageService)
    {
        _pageService = pageService;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        Type pageType = _pageService.GetPageType(pageKey);

        if (_frame != null && (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed))))
        {
            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var navigated = _frame.Navigate(pageType, parameter);
            if (navigated)
            {
                _lastParameterUsed = parameter;
                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
            }

            return navigated;
        }

        return false;
    }

    public async Task<bool> ShowModal(ContentDialog dialog, object? parameter = null)
    {
        if (_frame is null)
            return false;

        // When showing a modal, don't trigger navigated away events

        dialog.XamlRoot = _frame.XamlRoot;

        try
        {
            var result = await dialog.ShowAsync();
        }
        catch
        {
            return false;
        }
        return true;
    }

    public async Task<bool> ShowAsModal(string pageKey, object? parameter = null)
    {
        if (_frame is null)
            return false;

        var pageType = _pageService.GetPageType(pageKey);
        Frame tempFrame = new();

        var vmBeforeNavigation = tempFrame.GetPageViewModel();
        var navigated = tempFrame.Navigate(pageType, parameter);
        if (navigated && vmBeforeNavigation is INavigationAware navigationAware)
            navigationAware.OnNavigatedFrom();

        ContentDialog wrapper = new()
        {
            XamlRoot = _frame.XamlRoot,
            Content = tempFrame,
            PrimaryButtonText = "Done"
        };

        try
        {
            var result = await wrapper.ShowAsync();
        }
        catch
        {
            return false;
        }
        return true;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame)
            return;

        // clear navigation if tag is true
        if (frame.Tag is true)
        {
            frame.BackStack.Clear();
        }

        if (frame.GetPageViewModel() is INavigationAware navigationAware)
        {
            navigationAware.OnNavigatedTo(e.Parameter);
        }

        Navigated?.Invoke(sender, e);
    }
}
