using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal sealed class VideoInspectionOccurrenceEditDialog : Window
{
    private readonly InspectionSectionSession _section;
    private readonly ComboBox _occurrenceSelector = new();
    private readonly TextBlock _meterageLabel = new();
    private readonly TextBox _meterage = new();
    private readonly TextBlock _endMeterageLabel = new();
    private readonly TextBox _endMeterage = new();
    private readonly TextBlock _pointsLabel = new();
    private readonly TextBox _points = new();
    private readonly TextBox _description = new();
    private readonly Button _restoreButton = new();

    internal VideoInspectionOccurrenceEditDialog(InspectionSectionSession section)
    {
        _section = section ?? throw new ArgumentNullException(nameof(section));
        Title = $"Editar anomalia — {section.Name}";
        Width = 540;
        Height = 560;
        MinWidth = 460;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush("#080E1A");
        Foreground = Brush("#E6EDF7");
        Content = BuildContent();

        _occurrenceSelector.SelectionChanged += OnOccurrenceChanged;
        _occurrenceSelector.ItemsSource = section.Occurrences
            .Select(occurrence => new OccurrenceChoice(
                occurrence,
                ChoiceLabel(occurrence, section.IsOccurrenceEdited(occurrence.OccurrenceId))))
            .ToArray();
        _occurrenceSelector.DisplayMemberPath = nameof(OccurrenceChoice.Label);
        _occurrenceSelector.SelectedIndex = section.Occurrences.Count > 0 ? 0 : -1;
    }

    internal InspectionOccurrenceEdit? Edit { get; private set; }
    internal Guid? RestoreOccurrenceId { get; private set; }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(Label("OCORRÊNCIA"));
        ConfigureInput(_occurrenceSelector);
        _occurrenceSelector.Margin = new Thickness(0, 6, 0, 0);
        header.Children.Add(_occurrenceSelector);
        root.Children.Add(header);

        var formScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(formScroll, 1);
        var form = new StackPanel();
        formScroll.Content = form;

        _meterageLabel.Text = "METRAGEM";
        StyleLabel(_meterageLabel);
        form.Children.Add(_meterageLabel);
        ConfigureInput(_meterage);
        _meterage.Margin = new Thickness(0, 6, 0, 14);
        form.Children.Add(_meterage);

        _endMeterageLabel.Text = "METRAGEM FINAL";
        StyleLabel(_endMeterageLabel);
        form.Children.Add(_endMeterageLabel);
        ConfigureInput(_endMeterage);
        _endMeterage.Margin = new Thickness(0, 6, 0, 14);
        form.Children.Add(_endMeterage);

        _pointsLabel.Text = "PONTOS — SEPARE AS METRAGENS POR ;";
        StyleLabel(_pointsLabel);
        form.Children.Add(_pointsLabel);
        ConfigureInput(_points);
        _points.Margin = new Thickness(0, 6, 0, 14);
        form.Children.Add(_points);

        form.Children.Add(Label("TEXTO QUE SERÁ EXIBIDO NO ESTAQUEAMENTO"));
        ConfigureInput(_description);
        _description.AcceptsReturn = true;
        _description.TextWrapping = TextWrapping.Wrap;
        _description.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _description.MinHeight = 120;
        _description.Margin = new Thickness(0, 6, 0, 0);
        form.Children.Add(_description);

        var hint = new TextBlock
        {
            Text = "A edição é local no GeoAutoEngine até que as correções sejam exportadas e confirmadas no GeoAutoDocs.",
            Foreground = Brush("#7A9BC7"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        };
        form.Children.Add(hint);
        root.Children.Add(formScroll);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);

        ConfigureButton(_restoreButton, "Restaurar original", secondary: true);
        _restoreButton.Click += RestoreClick;
        footer.Children.Add(_restoreButton);

        var cancel = new Button();
        ConfigureButton(cancel, "Cancelar", secondary: true);
        cancel.Margin = new Thickness(8, 0, 0, 0);
        cancel.Click += (_, _) => DialogResult = false;
        Grid.SetColumn(cancel, 2);
        footer.Children.Add(cancel);

        var save = new Button();
        ConfigureButton(save, "Salvar edição", secondary: false);
        save.Margin = new Thickness(8, 0, 0, 0);
        save.Click += SaveClick;
        Grid.SetColumn(save, 3);
        footer.Children.Add(save);

        root.Children.Add(footer);
        return root;
    }

    private void OnOccurrenceChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_occurrenceSelector.SelectedItem is not OccurrenceChoice choice)
        {
            return;
        }

        var occurrence = choice.Occurrence;
        _meterage.Text = FormatDecimal(occurrence.Meterage);
        _endMeterage.Text = FormatDecimal(occurrence.EndMeterage);
        _points.Text = string.Join("; ", occurrence.MeteragePoints.Select(value =>
            value.ToString("0.##", CultureInfo.CurrentCulture)));
        _description.Text = occurrence.EffectiveDescription;

        var showMeterage = occurrence.LocationMode is InspectionLocationMode.Point or InspectionLocationMode.Range or InspectionLocationMode.From;
        var showEnd = occurrence.LocationMode == InspectionLocationMode.Range;
        var showPoints = occurrence.LocationMode == InspectionLocationMode.MultiPoint;
        SetVisible(_meterageLabel, showMeterage);
        SetVisible(_meterage, showMeterage);
        SetVisible(_endMeterageLabel, showEnd);
        SetVisible(_endMeterage, showEnd);
        SetVisible(_pointsLabel, showPoints);
        SetVisible(_points, showPoints);
        _meterageLabel.Text = occurrence.LocationMode == InspectionLocationMode.Range
            ? "METRAGEM INICIAL"
            : occurrence.LocationMode == InspectionLocationMode.From
                ? "METRAGEM INICIAL"
                : "METRAGEM";
        _restoreButton.IsEnabled = _section.IsOccurrenceEdited(occurrence.OccurrenceId);
    }

    private void SaveClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_occurrenceSelector.SelectedItem is not OccurrenceChoice choice)
        {
            ShowValidation("Selecione uma ocorrência para editar.");
            return;
        }

        var occurrence = choice.Occurrence;
        if (string.IsNullOrWhiteSpace(_description.Text))
        {
            ShowValidation("Informe o texto da ocorrência.");
            return;
        }

        if (!TryNullableDecimal(_meterage.Text, out var meterage) ||
            !TryNullableDecimal(_endMeterage.Text, out var endMeterage))
        {
            ShowValidation("Informe as metragens com valores numéricos válidos.");
            return;
        }

        IReadOnlyList<decimal> points = occurrence.MeteragePoints;
        if (occurrence.LocationMode == InspectionLocationMode.MultiPoint)
        {
            if (!TryPoints(_points.Text, out points))
            {
                ShowValidation("Informe os pontos usando ; entre as metragens. Exemplo: 2,5; 4,0; 7,25");
                return;
            }

            if (points.Count != occurrence.MeteragePoints.Count)
            {
                ShowValidation(
                    $"Esta ocorrência possui {occurrence.MeteragePoints.Count} ponto(s). " +
                    "Altere as metragens sem adicionar ou remover pontos.");
                return;
            }
        }

        Edit = new InspectionOccurrenceEdit(
            _section.SectionId,
            occurrence.OccurrenceId,
            meterage,
            endMeterage,
            points,
            _description.Text);
        RestoreOccurrenceId = null;
        DialogResult = true;
    }

    private void RestoreClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_occurrenceSelector.SelectedItem is not OccurrenceChoice choice ||
            !_section.IsOccurrenceEdited(choice.Occurrence.OccurrenceId))
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Restaurar a metragem e o texto originais recebidos do GeoAutoDocs?",
            "Restaurar ocorrência",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Edit = null;
        RestoreOccurrenceId = choice.Occurrence.OccurrenceId;
        DialogResult = true;
    }

    private static bool TryNullableDecimal(string value, out decimal? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        if (TryDecimal(value.Trim(), out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryPoints(string value, out IReadOnlyList<decimal> points)
    {
        var values = value.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<decimal>(values.Length);
        foreach (var item in values)
        {
            if (!TryDecimal(item.Trim(), out var meterage))
            {
                points = [];
                return false;
            }

            parsed.Add(meterage);
        }

        points = Array.AsReadOnly(parsed.ToArray());
        return parsed.Count > 0;
    }

    private static bool TryDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static string FormatDecimal(decimal? value) => value.HasValue
        ? value.Value.ToString("0.##", CultureInfo.CurrentCulture)
        : string.Empty;

    private static void SetVisible(UIElement element, bool visible) =>
        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private static TextBlock Label(string text)
    {
        var label = new TextBlock { Text = text };
        StyleLabel(label);
        return label;
    }

    private static void StyleLabel(TextBlock label)
    {
        label.Foreground = Brush("#7A9BC7");
        label.FontSize = 10;
        label.FontWeight = FontWeights.SemiBold;
    }

    private static void ConfigureInput(Control control)
    {
        control.Background = Brush("#0D1728");
        control.Foreground = Brush("#E6EDF7");
        control.BorderBrush = Brush("#28405F");
        control.BorderThickness = new Thickness(1);
        control.Padding = new Thickness(9, 7, 9, 7);
        control.FontSize = 13;
    }

    private static void ConfigureButton(Button button, string text, bool secondary)
    {
        button.Content = text;
        button.Padding = new Thickness(12, 8, 12, 8);
        button.FontSize = 12;
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
        button.Foreground = secondary ? Brush("#C8D5E5") : Brushes.White;
        button.Background = secondary ? Brush("#111D30") : Brush("#1677B8");
        button.BorderBrush = secondary ? Brush("#28405F") : Brush("#2696D6");
        button.BorderThickness = new Thickness(1);
    }

    private static SolidColorBrush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static string ChoiceLabel(InspectionOccurrenceSession occurrence, bool edited)
    {
        var position = occurrence.LocationMode switch
        {
            InspectionLocationMode.Point when occurrence.Meterage.HasValue => $"{occurrence.Meterage:0.##} m",
            InspectionLocationMode.Range when occurrence.Meterage.HasValue && occurrence.EndMeterage.HasValue =>
                $"{occurrence.Meterage:0.##} → {occurrence.EndMeterage:0.##} m",
            InspectionLocationMode.From when occurrence.Meterage.HasValue => $"A partir de {occurrence.Meterage:0.##} m",
            InspectionLocationMode.MultiPoint => string.Join(", ", occurrence.MeteragePoints.Select(value => $"{value:0.##}")),
            InspectionLocationMode.EntireSection => "Todo trecho",
            _ => occurrence.WireLocationMode,
        };
        var marker = edited ? " [EDITADO]" : string.Empty;
        var description = occurrence.EffectiveDescription.Length > 55
            ? occurrence.EffectiveDescription[..55] + "…"
            : occurrence.EffectiveDescription;
        return $"{position} — {description}{marker}";
    }

    private sealed record OccurrenceChoice(InspectionOccurrenceSession Occurrence, string Label);
}
