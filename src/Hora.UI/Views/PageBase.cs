using System.Windows;
using System.Windows.Controls;
using Hora.UI.Platform;

namespace Hora.UI.Views;

/// <summary>Base class for every destination page rendered inside the main window.</summary>
public abstract class PageBase : UserControl
{
    protected PageBase(AppContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Margin = new Thickness(28, 22, 28, 28);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        SetBinding(FlowDirectionProperty, new System.Windows.Data.Binding(nameof(LocalizationProxy.Flow))
        {
            Source = LocalizationProxy.Instance,
            Mode = System.Windows.Data.BindingMode.OneWay,
        });
    }

    protected AppContext Context { get; }

    /// <summary>Reloads the data shown by the page. Called on navigation and after a flush.</summary>
    public abstract void RefreshData();

    /// <summary>Re-renders localized text. Default implementation reloads the page.</summary>
    public virtual void ApplyLocalization() => RefreshData();

    protected void Toast(string message, ToastKind kind = ToastKind.Info) => ToastService.Show(message, kind);

    protected FrameworkElement Header(string titleKey, string subtitleKey)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        stack.Children.Add(Ui.Text(titleKey, "TitleText"));
        var subtitle = Ui.Text(subtitleKey, "MutedText");
        subtitle.Margin = new Thickness(0, 4, 0, 0);
        subtitle.MaxWidth = 720;
        subtitle.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(subtitle);
        return stack;
    }

    protected static Grid Columns(params (double Width, UIElement Element)[] cells)
    {
        var grid = new Grid();
        for (var index = 0; index < cells.Length; index += 1)
        {
            var (width, element) = cells[index];
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = width <= 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width, GridUnitType.Star),
            });
            Grid.SetColumn(element, index);
            grid.Children.Add(element);
        }

        return grid;
    }

    /// <summary>
    /// Runs a fire-and-forget page task and observes its fault. An unexpected
    /// exception is logged instead of disappearing as an unobserved task.
    /// </summary>
    protected void RunSafe(Func<Task> operation, string name) => _ = ObserveAsync(operation, name);

    private async Task ObserveAsync(Func<Task> operation, string name)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Context.Logger.Warn($"{name} failed: {exception.GetType().Name}");
        }
    }
}
