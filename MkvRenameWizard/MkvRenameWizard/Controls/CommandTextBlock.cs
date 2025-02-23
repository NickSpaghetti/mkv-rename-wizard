using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace MkvRenameWizard.Controls;

public class CommandTextBlock : TextBlock
{
    public static readonly StyledProperty<ICommand> CommandProperty =
        AvaloniaProperty.Register<CommandTextBlock, ICommand>(nameof(Command));

    public static readonly StyledProperty<object> CommandParameterProperty =
        AvaloniaProperty.Register<CommandTextBlock, object>(nameof(CommandParameter));

    public ICommand Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public CommandTextBlock()
    {
        // Add a gesture recognizer for tap events
        Tapped += OnTapped;
    }

    private void OnTapped(object sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}