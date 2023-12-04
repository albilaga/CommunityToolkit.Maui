﻿using System.ComponentModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Platform;

namespace CommunityToolkit.Maui;

/// <inheritdoc cref="IPopupService"/>
public class PopupService : IPopupService
{
	readonly IServiceProvider serviceProvider;

	static readonly Dictionary<Type, Type> viewModelToViewMappings = new();

	static Page CurrentPage =>
		PageExtensions.GetCurrentPage(
			Application.Current?.MainPage ?? throw new InvalidOperationException("Application.Current.MainPage cannot be null."));

	/// <summary>
	/// Creates a new instance of <see cref="PopupService"/>.
	/// </summary>
	/// <param name="serviceProvider">The <see cref="IServiceProvider"/> implementation.</param>
	[ActivatorUtilitiesConstructor]
	public PopupService(IServiceProvider serviceProvider)
	{
		this.serviceProvider = serviceProvider;
	}

	/// <summary>
	/// Creates a new instance of <see cref="PopupService"/>.
	/// </summary>
	public PopupService()
	{
		serviceProvider = Application.Current?.Handler?.MauiContext?.Services
							?? throw new InvalidOperationException("Could not locate IServiceProvider");
	}

	internal static void AddTransientPopupContent<TPopupContentView, TPopupViewModel>(IServiceCollection services)
		where TPopupContentView : View
		where TPopupViewModel : INotifyPropertyChanged
	{
		viewModelToViewMappings.Add(typeof(TPopupViewModel), typeof(TPopupContentView));

		services.AddTransient(typeof(TPopupContentView));
		services.AddTransient(typeof(TPopupViewModel));
	}

	internal static void AddTransientPopup<TPopupView, TPopupViewModel>(IServiceCollection services)
		where TPopupView : IPopup
		where TPopupViewModel : INotifyPropertyChanged
	{
		viewModelToViewMappings.Add(typeof(TPopupViewModel), typeof(TPopupView));

		services.AddTransient(typeof(TPopupView));
		services.AddTransient(typeof(TPopupViewModel));
	}

	/// <inheritdoc cref="IPopupService.ShowPopup{TViewModel}()"/>
	public void ShowPopup<TViewModel>() where TViewModel : INotifyPropertyChanged
	{
		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext<TViewModel>(popup, out _);

		CurrentPage.ShowPopup(popup);
	}

	/// <inheritdoc cref="IPopupService.ShowPopup{TViewModel}(TViewModel)"/>
	public void ShowPopup<TViewModel>(TViewModel viewModel) where TViewModel : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(viewModel);

		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext<TViewModel>(popup, out _);

		CurrentPage.ShowPopup(popup);
	}

	/// <inheritdoc cref="IPopupService.ShowPopup{TViewModel}(Action{TViewModel})"/>
	public void ShowPopup<TViewModel>(Action<TViewModel> onPresenting) where TViewModel : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(onPresenting);

		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext(popup, out TViewModel viewModel);

		onPresenting.Invoke(viewModel);

		CurrentPage.ShowPopup(popup);
	}

	/// <inheritdoc cref="IPopupService.ShowPopupAsync{TViewModel}(CancellationToken)"/>
	public Task<object?> ShowPopupAsync<TViewModel>(CancellationToken token = default) where TViewModel : INotifyPropertyChanged
	{
		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext<TViewModel>(popup, out _);

		return CurrentPage.ShowPopupAsync(popup, token);
	}

	/// <inheritdoc cref="IPopupService.ShowPopupAsync{TViewModel}(TViewModel, CancellationToken)"/>
	public Task<object?> ShowPopupAsync<TViewModel>(TViewModel viewModel, CancellationToken token = default) where TViewModel : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(viewModel);

		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext<TViewModel>(popup, out _);

		return CurrentPage.ShowPopupAsync(popup, token);
	}

	/// <inheritdoc cref="IPopupService.ShowPopupAsync{TViewModel}(Action{TViewModel}, CancellationToken)"/>
	public Task<object?> ShowPopupAsync<TViewModel>(Action<TViewModel> onPresenting, CancellationToken token = default) where TViewModel : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(onPresenting);

		var popup = GetPopup(typeof(TViewModel));

		ValidateBindingContext(popup, out TViewModel viewModel);

		onPresenting.Invoke(viewModel);

		return CurrentPage.ShowPopupAsync(popup, token);
	}

	/// <summary>
	/// Ensures that the BindingContext property of the Popup to present is properly assigned and of the expected type.
	/// </summary>
	/// <typeparam name="TViewModel"></typeparam>
	/// <param name="popup">The popup to be presented.</param>
	/// <param name="bindingContext">Validated View Model</param>
	/// <exception cref="InvalidOperationException"></exception>
	static void ValidateBindingContext<TViewModel>(Popup popup, out TViewModel bindingContext)
	{
		if (popup.BindingContext is TViewModel viewModel)
		{
			bindingContext = viewModel;
			return;			
		}
		else if (popup.Content?.BindingContext is TViewModel contentViewModel)
		{
			bindingContext = contentViewModel;
			return;
		}

		throw new InvalidOperationException($"Unexpected type has been assigned to the BindingContext of {popup.GetType().FullName}. Expected type {typeof(TViewModel).FullName} but was {popup.BindingContext?.GetType().FullName ?? "null"}");
	}

	Popup GetPopup(Type viewModelType)
	{
		var view = serviceProvider.GetService(viewModelToViewMappings[viewModelType]);

		if (view is null)
		{
			throw new InvalidOperationException(
				$"Unable to resolve popup type for {viewModelType} please make sure that you have called {nameof(AddTransientPopup)}");
		}

		Popup popup;

		if (view is View visualElement)
		{
			popup = new Popup
			{
				Content = visualElement
			};

			// Binding the BindingContext property up from the view to the popup so that it is nicely handled on macOS.
			popup.SetBinding(Popup.BindingContextProperty, new Binding { Source = view, Path = Popup.BindingContextProperty.PropertyName });
		}
		else if (view is Popup viewPopup)
		{
			popup = viewPopup;
		}
		else
		{
			throw new InvalidOperationException(
				$"Invalid type of view being used to present a Popup. Expected either IPopup or View.");
		}

		return popup;
	}
}