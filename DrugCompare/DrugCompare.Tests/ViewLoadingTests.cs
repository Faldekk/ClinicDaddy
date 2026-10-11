using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Presentation.Behaviors;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.EvidenceAssistant;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class ViewLoadingTests
{
    [TestMethod]
    public void FeatureViews_LoadAndLayoutWithSharedApplicationResources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();

                UserControl[] views =
                [
                    new EvidenceAssistantView(),
                    new ChPLNavigatorView(),
                    new InteractionCheckerView(),
                    new PolishRegistryView(),
                    new IcdLookerView(),
                    new DataManagementView(),
                    new DatabaseStatusView(),
                    new AuditLogView(),
                    new HistoryView(),
                    new SettingsView()
                ];

                foreach (var view in views)
                {
                    view.Measure(new Size(1100, 760));
                    view.Arrange(new Rect(0, 0, 1100, 760));
                    view.UpdateLayout();
                }

                var searchCalls = 0;
                var clearCalls = 0;
                var search = new RelayCommand(() => searchCalls++);
                var clear = new RelayCommand(() => clearCalls++);
                var icd = (IcdLookerView)views[4];
                icd.DataContext = new KeyboardFixture(search, clear);
                var icdInput = (TextBox)icd.FindName("IcdSearchTextBox");
                ExecuteKeyBinding(icdInput, Key.Return, search);
                ExecuteKeyBinding(icdInput, Key.Escape, clear);
                var interaction = (InteractionCheckerView)views[2];
                interaction.DataContext = new KeyboardFixture(search, clear);
                ExecuteKeyBinding((TextBox)interaction.FindName("DrugSearchTextBox"), Key.Return, search);
                var registry = (PolishRegistryView)views[3];
                registry.DataContext = new KeyboardFixture(search, clear);
                ExecuteKeyBinding((TextBox)registry.FindName("RegistrySearchTextBox"), Key.Escape, clear);
                Assert.AreEqual(2, searchCalls);
                Assert.AreEqual(2, clearCalls);
                foreach (var view in new UserControl[] { icd, interaction, registry })
                {
                    view.Measure(new Size(786, 630));
                    view.Arrange(new Rect(0, 0, 786, 630));
                    view.UpdateLayout();
                }
                Assert.IsTrue(icdInput.ActualWidth >= 150, "ICD input must remain usable at minimum window width.");
                var interactionLayout = (Grid)interaction.FindName("InteractionLayout");
                Assert.IsTrue(interactionLayout.ColumnDefinitions.All(column => column.ActualWidth >= 200),
                    "Interaction results must not collapse behind fixed-width input panels.");

                // Window resources are also resolved without opening a UI.
                // No ViewModel/services: these checks exercise only the window's navigation.
                var mainWindow = new MainWindow(null!);
                var tabs = (TabControl)mainWindow.FindName("MainTabs");
                var expanded = (ScrollViewer)mainWindow.FindName("ExpandedNavigation");
                var collapsed = (StackPanel)mainWindow.FindName("CollapsedNavigation");
                var toggle = (Button)mainWindow.FindName("NavigationToggle");
                var expandedButtons = Buttons(expanded).Where(button => button.Tag is not null).ToArray();
                var collapsedButtons = Buttons(collapsed).Where(button => button.Tag is not null).ToArray();
                Assert.AreEqual(9, tabs.Items.Count);
                Assert.AreEqual(9, expandedButtons.Length);
                Assert.AreEqual(9, collapsedButtons.Length);
                foreach (var button in expandedButtons)
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.AreEqual(int.Parse(button.Tag.ToString()!), tabs.SelectedIndex);
                    Assert.AreEqual(1, expandedButtons.Count(Selector.GetIsSelected));
                    Assert.AreEqual(1, collapsedButtons.Count(Selector.GetIsSelected));
                }
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(Visibility.Visible, collapsed.Visibility);
                Assert.AreEqual("Rozwiń menu", toggle.ToolTip);
                collapsedButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(0, tabs.SelectedIndex);
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(Visibility.Visible, expanded.Visibility);
                foreach (var size in new[] { new Size(1100, 700), new Size(1400, 900) })
                {
                    mainWindow.Measure(size);
                    mainWindow.Arrange(new Rect(new Point(), size));
                    mainWindow.UpdateLayout();
                }
                var rawTextWindow = new RawChplTextWindow("Przykładowy tekst ChPL");
                rawTextWindow.Close();
                mainWindow.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "WPF view loading did not finish.");

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<Button> Buttons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button button) yield return button;
            foreach (var descendant in Buttons(child)) yield return descendant;
        }
    }

    private static void ExecuteKeyBinding(TextBox input, Key key, ICommand expected)
    {
        var property = key == Key.Escape ? SearchInputBehavior.ClearCommandProperty : SearchInputBehavior.SubmitCommandProperty;
        // Resolve the declared command path against a fixture without requiring a live window.
        var declared = System.Windows.Data.BindingOperations.GetBinding(input, property);
        Assert.IsNotNull(declared);
        var path = declared.Path.Path;
        Assert.IsTrue(path is "SearchCommand" or "ClearCommand" or "FindDrugCommand");
        System.Windows.Data.BindingOperations.SetBinding(input, property,
            new System.Windows.Data.Binding(path) { Source = new KeyboardFixture(expected) }).UpdateTarget();
        Assert.AreSame(expected, input.GetValue(property), "Keyboard actions must use the same ViewModel command as buttons.");
        Assert.IsTrue(SearchInputBehavior.TryExecute(input, key));
    }

    public sealed class KeyboardFixture(ICommand command, ICommand? clear = null)
    {
        public ICommand SearchCommand => command;
        public ICommand ClearCommand => clear ?? command;
        public ICommand FindDrugCommand => command;
        public string SearchText { get; set; } = string.Empty;
        public string DrugNameInput { get; set; } = string.Empty;
    }
}
