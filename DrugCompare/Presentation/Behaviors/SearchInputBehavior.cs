using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DrugCompare.Presentation.Behaviors;

public static class SearchInputBehavior
{
    public static readonly DependencyProperty SubmitCommandProperty = DependencyProperty.RegisterAttached(
        "SubmitCommand", typeof(ICommand), typeof(SearchInputBehavior), new PropertyMetadata(null, CommandsChanged));
    public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.RegisterAttached(
        "ClearCommand", typeof(ICommand), typeof(SearchInputBehavior), new PropertyMetadata(null, CommandsChanged));
    public static void SetSubmitCommand(DependencyObject target, ICommand value) => target.SetValue(SubmitCommandProperty, value);
    public static ICommand? GetSubmitCommand(DependencyObject target) => target.GetValue(SubmitCommandProperty) as ICommand;
    public static void SetClearCommand(DependencyObject target, ICommand value) => target.SetValue(ClearCommandProperty, value);
    public static ICommand? GetClearCommand(DependencyObject target) => target.GetValue(ClearCommandProperty) as ICommand;

    private static void CommandsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox input) return;
        input.KeyDown -= OnKeyDown;
        if (GetSubmitCommand(input) is not null || GetClearCommand(input) is not null) input.KeyDown += OnKeyDown;
    }

    private static void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (!args.Handled && Keyboard.Modifiers == ModifierKeys.None)
            args.Handled = TryExecute((TextBox)sender, args.Key);
    }

    public static bool TryExecute(TextBox input, Key key)
    {
        var command = key switch { Key.Enter => GetSubmitCommand(input), Key.Escape => GetClearCommand(input), _ => null };
        if (!input.IsEnabled || command is null || !command.CanExecute(null)) return false;
        command.Execute(null);
        return true;
    }
}
