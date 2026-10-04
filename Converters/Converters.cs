using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Melodium.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = false;
        if (value is bool flag) b = flag;
        else if (value is int i) b = i > 0;
        else if (value is long l) b = l > 0;
        else if (value is double d) b = d > 0;
        else if (value is System.Collections.ICollection col) b = col.Count > 0;
        else if (value != null) b = true;

        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        bool isVis = value is Visibility v && v == Visibility.Visible;
        return Invert ? !isVis : isVis;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNotNull = value != null;
        if (value is string str) isNotNull = !string.IsNullOrWhiteSpace(str);
        if (Invert) isNotNull = !isNotNull;
        return isNotNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class StringEqualsToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string? val = value?.ToString();
        string? param = parameter?.ToString();
        bool matches = string.Equals(val, param, StringComparison.OrdinalIgnoreCase);
        if (Invert) matches = !matches;
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        return b ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToDoubleConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1.0;
    public double FalseValue { get; set; } = 0.4;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        return b ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToBrushConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty TrueBrushProperty =
        DependencyProperty.Register(nameof(TrueBrush), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(BoolToBrushConverter), new PropertyMetadata(null));

    public static readonly DependencyProperty FalseBrushProperty =
        DependencyProperty.Register(nameof(FalseBrush), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(BoolToBrushConverter), new PropertyMetadata(null));

    public Microsoft.UI.Xaml.Media.Brush? TrueBrush
    {
        get => (Microsoft.UI.Xaml.Media.Brush?)GetValue(TrueBrushProperty);
        set => SetValue(TrueBrushProperty, value);
    }

    public Microsoft.UI.Xaml.Media.Brush? FalseBrush
    {
        get => (Microsoft.UI.Xaml.Media.Brush?)GetValue(FalseBrushProperty);
        set => SetValue(FalseBrushProperty, value);
    }

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        return b ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToVector3Converter : IValueConverter
{
    public double TrueScale { get; set; } = 1.04;
    public double FalseScale { get; set; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        float s = (float)(b ? TrueScale : FalseScale);
        return new System.Numerics.Vector3(s, s, 1.0f);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToThicknessConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1.0;
    public double FalseValue { get; set; } = 0.0;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        double v = b ? TrueValue : FalseValue;
        return new Thickness(v);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
